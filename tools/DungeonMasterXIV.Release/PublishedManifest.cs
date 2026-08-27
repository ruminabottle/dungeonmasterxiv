using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DungeonMasterXIV.Release;

/// <summary>
/// The repository manifest as COMMITTED at the repository root — the file a tester's Dalamud reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>The generated artefact, read back.</b> <see cref="RepositoryManifest"/> writes this file;
/// this reads the copy that actually landed. They are deliberately different directions: generating
/// says what the manifest ought to be, and only reading back says what it is. Publishing it was a
/// hand step with no mechanism, and it was skipped — the file was absent from <c>main</c> while the
/// release was real, so a tester's URL 404'd with every other check green.
/// </para>
/// <para>
/// <b>Nothing here tolerates a missing or unreadable file.</b> A check that shrugs at an absent
/// manifest passes on the exact failure it exists to catch, and this file's absence is that failure.
/// Every way of not having a usable version throws, and each says which file and what to do.
/// </para>
/// </remarks>
public sealed class PublishedManifest
{
    private PublishedManifest(string path, Version advertisedVersion, IReadOnlyList<string> downloadLinks)
    {
        Path = path;
        AdvertisedVersion = advertisedVersion;
        DownloadLinks = downloadLinks;
    }

    /// <summary>The fields Dalamud fetches the artefact from. All three name the same tagged asset.</summary>
    private static readonly string[] LinkFields =
        { "DownloadLinkTesting", "DownloadLinkInstall", "DownloadLinkUpdate" };

    /// <summary>Where the manifest was read from, so a refusal can name it.</summary>
    public string Path { get; }

    /// <summary>The version this manifest offers to Dalamud, from <c>TestingAssemblyVersion</c>.</summary>
    public Version AdvertisedVersion { get; }

    /// <summary>The URLs this manifest points a tester at.</summary>
    public IReadOnlyList<string> DownloadLinks { get; }

    /// <summary>
    /// The manifest at <paramref name="path"/>, refusing every way of not having a usable version.
    /// </summary>
    public static PublishedManifest At(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"No repository manifest at '{path}'. This is the file a tester pastes into Dalamud " +
                "as a custom repository; without it the URL 404s while the release itself looks " +
                "fine. Generate it with the release tool and commit it (R-7.2).",
                path);
        }

        var entry = EntryIn(path);

        return new PublishedManifest(path, VersionIn(path, entry), LinksIn(path, entry));
    }

    /// <summary>
    /// Throws unless this manifest advertises the version <paramref name="tag"/> names.
    /// </summary>
    /// <remarks>
    /// <b>The tag is the authority, not this file (D-16, R-7.4a).</b> The manifest is generated from
    /// it, so when they disagree the manifest is the stale one — it was not regenerated after the tag
    /// moved. That is worse than the absence this file's own history records: an absent manifest
    /// 404s loudly, while a stale one resolves, offers a version the release does not carry, and
    /// looks like it worked.
    /// </remarks>
    public void MustDescribeTheReleaseTagged(string tag)
    {
        var tagged = TaggedVersion.Of(tag);

        // Checked as well as the version, because the version alone can be brought into agreement by
        // editing one field -- and a manifest whose version says 0.2.0.0 while its links still fetch
        // v0.1.0's asset installs the OLD build under the NEW version number. Dalamud then believes
        // the tester is up to date and never offers the real thing. Four fields have to agree, so a
        // partial hand-edit is caught and R-7.2's "generated, never hand-edited" keeps its teeth.
        foreach (var link in DownloadLinks)
        {
            if (!link.Contains($"/download/{tag}/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"'{Path}' points at '{link}', which is not an asset of the latest release tag " +
                    $"'{tag}'. Regenerate the manifest with the release tool at that tag: a link to " +
                    "an older tag's asset installs that older build under this manifest's version " +
                    "number, and Dalamud then has no reason to offer the real one.\n" +
                    RegenerateWith(tag));
            }
        }

        if (AdvertisedVersion != tagged)
        {
            throw new InvalidOperationException(
                $"'{Path}' advertises version {AdvertisedVersion}, but the latest release tag " +
                $"'{tag}' names {tagged}. The manifest is generated from the tag, so it is the stale " +
                "one. Nothing else reports this — a stale manifest resolves and serves the wrong " +
                "version, which is why it is checked rather than remembered.\n" +
                RegenerateWith(tag));
        }
    }

    /// <summary>
    /// The exact two commands that fix this, with the tag already substituted.
    /// </summary>
    /// <remarks>
    /// Spelled out rather than described. Regenerating takes two commands and four paths, and a
    /// refusal that says "regenerate it" leaves someone to reconstruct those from memory at the one
    /// moment they are in a hurry — which is when the manifest gets hand-edited into agreement
    /// instead, satisfying the version check while the links still fetch the previous release.
    /// </remarks>
    private static string RegenerateWith(string tag) =>
        $"    dotnet build -c Release -p:ReleaseTag={tag}\n" +
        "    dotnet run --project tools/DungeonMasterXIV.Release -- \\\n" +
        "        --assembly bin/x64/Release/DungeonMasterXIV.dll \\\n" +
        "        --plugin-manifest bin/x64/Release/DungeonMasterXIV.json \\\n" +
        "        --asset bin/x64/Release/DungeonMasterXIV/latest.zip \\\n" +
        $"        --tag {tag} --out repo.json\n" +
        "  then commit repo.json.";

    private static JsonElement EntryIn(string path)
    {
        JsonElement root;

        try
        {
            root = JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
        }
        catch (JsonException notJson)
        {
            throw new InvalidOperationException(
                $"'{path}' is not readable as JSON, so nothing can be checked against it. Dalamud " +
                "would reject it too, and the tester sees only an empty repository.", notJson);
        }

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1)
        {
            throw new InvalidOperationException(
                $"'{path}' is not a repository manifest: it must be a JSON array of exactly one " +
                $"plugin entry, and this is {root.ValueKind} with " +
                $"{(root.ValueKind == JsonValueKind.Array ? root.GetArrayLength() : 0)} entries.");
        }

        return root[0];
    }

    private static Version VersionIn(string path, JsonElement entry)
    {
        if (!entry.TryGetProperty("TestingAssemblyVersion", out var advertised) ||
            advertised.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"'{path}' carries no TestingAssemblyVersion, so it advertises no version at all. " +
                "Dalamud offers nothing for an entry it cannot version, and says nothing about why.");
        }

        if (!Version.TryParse(advertised.GetString(), out var version))
        {
            throw new InvalidOperationException(
                $"'{path}' advertises '{advertised.GetString()}', which is not a version.");
        }

        return TaggedVersion.Pad(version);
    }

    private static IReadOnlyList<string> LinksIn(string path, JsonElement entry)
    {
        var links = new List<string>();

        foreach (var field in LinkFields)
        {
            if (!entry.TryGetProperty(field, out var link) || link.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"'{path}' carries no {field}. Dalamud reads all three even for a " +
                    "testing-exclusive plugin, and an entry it cannot fetch from is one it silently " +
                    "never offers.");
            }

            links.Add(link.GetString()!);
        }

        return links;
    }
}
