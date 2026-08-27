using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DungeonMasterXIV.Release;

/// <summary>
/// The repository manifest as COMMITTED at the repository root — the file a tester's Dalamud reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>The generated artefact, read back.</b> <see cref="RepositoryManifest"/> writes this file; this
/// reads the copy that actually landed. Publishing it was a hand step with no mechanism and it was
/// skipped, so a tester's URL 404'd while every other check stayed green.
/// </para>
/// <para>
/// <b>The whole document is compared, not a list of fields (BUG-27).</b> The first version of this
/// class named four fields it cared about. <c>IsTestingExclusive</c> was not among them — so half of
/// D-12's gate could be crossed by editing one boolean in a committed file, with the suite green,
/// and the only thing standing in the way was somebody noticing a one-line diff at review. That is
/// enforcement by review, which D-15 rejects. Generation is deterministic, so the invariant is
/// <i>this file is what the tool would produce</i>, and every field is covered by consequence rather
/// than by being remembered. Derive the invariant, do not enumerate it — the rule that closed
/// BUG-24, one level up.
/// </para>
/// <para>
/// <b>Compared as parsed JSON, never as bytes.</b> A byte comparison fails on key order and
/// whitespace, which are not defects — an instrument that produces false failures trains people to
/// ignore it, and that is worse than one that cannot fail (BUG-16's caution).
/// </para>
/// </remarks>
public sealed class PublishedManifest
{
    private readonly JsonNode document;

    private PublishedManifest(string path, JsonNode document)
    {
        Path = path;
        this.document = document;
    }

    /// <summary>Where the manifest was read from, so a refusal can name it.</summary>
    public string Path { get; }

    /// <summary>
    /// The manifest at <paramref name="path"/>, refusing anything that is not a readable one.
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

        return new PublishedManifest(path, Parse(File.ReadAllText(path), path));
    }

    /// <summary>
    /// Throws unless this file is exactly what the release tool would generate for
    /// <paramref name="tag"/>.
    /// </summary>
    /// <param name="generated">The manifest the tool produces, regenerated from the artefacts.</param>
    /// <param name="tag">The release tag it was generated for, so the refusal can print the fix.</param>
    public void MustMatch(string generated, string tag)
    {
        var expected = Parse(generated, "the freshly generated manifest");

        var differences = Differences(document, expected).ToList();

        if (differences.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"'{Path}' is not what the release tool generates for '{tag}':\n" +
            string.Join("\n", differences) + "\n" +
            "The tool's output is the authority — this file is generated, never hand-edited (R-7.2). " +
            "Every field is checked, not a chosen few, so this catches an edit to any of them " +
            "including IsTestingExclusive, which is half of D-12's gate between a testing-only " +
            "plugin and one offered to everyone holding the URL.\n" +
            RegenerateWith(tag));
    }

    private static IEnumerable<string> Differences(JsonNode committed, JsonNode expected)
    {
        var here = Fields(committed);
        var there = Fields(expected);

        foreach (var field in here.Keys.Union(there.Keys).OrderBy(name => name, StringComparer.Ordinal))
        {
            var mine = here.GetValueOrDefault(field);
            var theirs = there.GetValueOrDefault(field);

            if (mine == theirs)
            {
                continue;
            }

            yield return mine is null ? $"  {field}: absent here, generated as {theirs}"
                : theirs is null ? $"  {field}: present here as {mine}, not generated at all"
                : $"  {field}: this file says {mine}, the tool generates {theirs}";
        }
    }

    /// <summary>Every field of the single plugin entry, canonicalised so ordering is not a difference.</summary>
    private static Dictionary<string, string> Fields(JsonNode manifest) =>
        manifest.AsArray()[0]!.AsObject().ToDictionary(
            property => property.Key,
            property => Canonical(property.Value),
            StringComparer.Ordinal);

    // Key order and whitespace are not differences, so they are normalised away before comparing
    // rather than reported as defects.
    private static string Canonical(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject entry => "{" + string.Join(
            ",",
            entry.OrderBy(property => property.Key, StringComparer.Ordinal)
                .Select(property => $"{JsonSerializer.Serialize(property.Key)}:{Canonical(property.Value)}")) + "}",
        JsonArray items => "[" + string.Join(",", items.Select(Canonical)) + "]",
        _ => node.ToJsonString(),
    };

    private static JsonNode Parse(string content, string describedAs)
    {
        JsonNode? parsed;

        try
        {
            parsed = JsonNode.Parse(content);
        }
        catch (JsonException notJson)
        {
            throw new InvalidOperationException(
                $"'{describedAs}' is not readable as JSON, so nothing can be checked against it. " +
                "Dalamud would reject it too, and the tester sees only an empty repository.", notJson);
        }

        if (parsed is not JsonArray entries || entries.Count != 1 || entries[0] is not JsonObject)
        {
            throw new InvalidOperationException(
                $"'{describedAs}' is not a repository manifest: it must be a JSON array of exactly " +
                "one plugin entry.");
        }

        return parsed;
    }

    /// <summary>The exact two commands that fix this, with the tag already substituted.</summary>
    /// <remarks>
    /// Spelled out rather than described. Regenerating takes two commands and four paths, and a
    /// refusal that says "regenerate it" leaves someone to reconstruct those from memory at the one
    /// moment they are in a hurry — which is when the file gets hand-edited into agreement instead.
    /// </remarks>
    private static string RegenerateWith(string tag) =>
        $"    dotnet build -c Release -p:ReleaseTag={tag}\n" +
        "    dotnet run --project tools/DungeonMasterXIV.Release -- \\\n" +
        "        --assembly bin/x64/Release/DungeonMasterXIV.dll \\\n" +
        "        --plugin-manifest bin/x64/Release/DungeonMasterXIV.json \\\n" +
        "        --asset bin/x64/Release/DungeonMasterXIV/latest.zip \\\n" +
        $"        --tag {tag} --out repo.json\n" +
        "  then commit repo.json.";
}
