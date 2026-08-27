using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace DungeonMasterXIV.Release;

/// <summary>
/// The zip that will be attached to the release, identified by the path to the actual file.
/// </summary>
/// <remarks>
/// <para>
/// <b>The name is derived from the file, never typed.</b> It used to be a constant reading
/// <c>DungeonMasterXIV.zip</c>, which DalamudPackager has never produced — it writes
/// <c>latest.zip</c>. The manifest was therefore well-formed, the release real, the plugin working,
/// and the download link dead, with nothing on our side looking wrong.
/// </para>
/// <para>
/// <b>There is no default and there must not be one.</b> A default is the typed value with a longer
/// fuse: it lets somebody believe the tool checked the name when it only repeated an assumption.
/// That is the argument that removed <c>--api-level</c> in C17, third outing.
/// </para>
/// <para>
/// <b>Every build writes <c>latest.zip</c>, so the name identifies nothing.</b> Five of them were on
/// this machine at once — 61KB to 119KB, spanning two days, indistinguishable by name. Attaching the
/// wrong one produces a plugin that installs and then misbehaves, which is worse than a dead link
/// because it looks like it worked. That is why <see cref="MustMatchTheAssembly"/> exists: the name
/// cannot tell these apart and the contents can.
/// </para>
/// </remarks>
public sealed class ReleaseAsset
{
    private const string PluginAssemblyName = "DungeonMasterXIV.dll";

    private const string PluginManifestName = "DungeonMasterXIV.json";

    private ReleaseAsset(FileInfo file) => File = file;

    /// <summary>The zip itself.</summary>
    public FileInfo File { get; }

    /// <summary>The asset name the download link uses, taken from the file on disk.</summary>
    public string Name => File.Name;

    /// <summary>
    /// The asset at <paramref name="path"/>, refusing a path with nothing at the end of it.
    /// </summary>
    /// <param name="path">Path to the built zip, as produced by DalamudPackager.</param>
    public static ReleaseAsset At(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "A path to the built release zip is required. The asset name is taken from that file " +
                "rather than assumed, because every build writes the same name and the name alone " +
                "identifies nothing.");
        }

        var file = new FileInfo(path);

        if (!file.Exists)
        {
            throw new FileNotFoundException(
                $"No release asset at '{file.FullName}'. Nothing is generated from a path that does " +
                "not resolve: a manifest pointing at a file that is not there produces a dead " +
                "download link and looks correct from every angle on our side.",
                file.FullName);
        }

        return new ReleaseAsset(file);
    }

    /// <summary>
    /// Confirms this zip carries the very assembly the manifest is describing.
    /// </summary>
    /// <remarks>
    /// The name cannot distinguish one build's zip from another's — they are all <c>latest.zip</c> —
    /// and the version cannot either, because a version is rarely bumped between builds. The bytes
    /// can. Comparing them is what stops a stale zip being attached to a manifest that describes a
    /// newer build, which installs and then behaves like neither.
    /// </remarks>
    /// <param name="assemblyPath">The built assembly the manifest's version was read from.</param>
    public void MustMatchTheAssembly(string assemblyPath)
    {
        // This runs BEFORE PluginAssemblyVersion.Of, which used to be the first thing to touch
        // --assembly and carried this guard. Without it here, reading a path whose DIRECTORY is
        // missing throws DirectoryNotFoundException -- a SIBLING of FileNotFoundException under
        // IOException, not a subclass -- so it escapes Program.cs's filter and the run ends in a
        // stack trace instead of a sentence. Widening that filter to IOException would stop the
        // crash and lose the sentence, which is the trade this file exists to refuse.
        if (!System.IO.File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                $"No built plugin assembly at '{assemblyPath}' to compare the zip against. " +
                "Build the plugin before generating a manifest.",
                assemblyPath);
        }

        using var archive = OpenZip();

        var packaged = archive.GetEntry(PluginAssemblyName)
            ?? throw new InvalidOperationException(
                $"'{File.FullName}' contains no {PluginAssemblyName}, so it is not a plugin release zip.");

        using var packagedStream = packaged.Open();

        if (!Sha256Of(packagedStream).AsSpan().SequenceEqual(Sha256OfFile(assemblyPath)))
        {
            throw new InvalidOperationException(
                $"The {PluginAssemblyName} inside '{File.FullName}' is not the same build as " +
                $"'{assemblyPath}'. The zip is from a different build than the assembly this manifest " +
                "describes. Every build writes the same file name, so this is the only thing that " +
                "tells them apart — attaching the wrong one ships a plugin that installs and then " +
                "misbehaves.");
        }
    }

    /// <summary>
    /// Confirms the manifest inside this zip says the same things as the built manifest the
    /// repository entry is generated from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The DLL check cannot see this (BUG-16).</b> A metadata-only edit leaves the assembly
    /// byte-identical — measured, twice, by two people — so a previous build's zip satisfies
    /// <see cref="MustMatchTheAssembly"/> while carrying the previous build's metadata. The
    /// repository entry then advertises one <c>DalamudApiLevel</c> and links an archive declaring
    /// another, which is the failure mode <see cref="ReleaseInputs"/> singles out as the worst kind:
    /// Dalamud does not reject it, it simply never offers the plugin.
    /// </para>
    /// <para>
    /// <b>Values, never bytes.</b> Both files are JSON produced by different steps, and key order,
    /// indentation and escaping may legitimately differ between them; today they happen to be
    /// byte-identical, so a byte comparison would pass now and start failing the first time any of
    /// that moved. A guard that produces false FAILs is worse than one that cannot fail, because a
    /// noisy guard gets relaxed rather than fixed.
    /// </para>
    /// <para>
    /// <b>The fields are enumerated by hand, and a test keeps the enumeration complete.</b> The
    /// list covers every property <see cref="PluginManifest"/> carries, which is also every field
    /// the repository entry republishes. An earlier version of this comment said the list was
    /// derived from <see cref="RepositoryManifest.Build"/> "so it moves when that does" — that
    /// described how the list was written and guaranteed nothing (BUG-24): a ninth property was
    /// added, republished, and compared by nothing, with the whole suite green. The guarantee now
    /// lives in <c>EveryFieldTheManifestCarriesIsComparedTests</c>, which varies each property in
    /// turn and requires this method to refuse and name it.
    /// <para>
    /// It stays hand-written rather than reflective because the per-field message is the point:
    /// <i>"Punchline: the build says X, the zip says Y"</i> ends an investigation where "the
    /// manifests differ" starts one. What is mechanical is the proof of completeness, not the
    /// comparison.
    /// </para>
    /// <para>
    /// <c>InternalName</c> is absent and is not a property: it is a constant the entry republishes
    /// directly, the source manifest carries no such field, and building under another assembly
    /// name fails outright — so there is no build-produced zip whose <c>InternalName</c> differs
    /// while a matching <c>DungeonMasterXIV.dll</c> is present.
    /// </para>
    /// </para>
    /// </remarks>
    /// <param name="built">The built manifest the repository entry is generated from.</param>
    /// <param name="builtPath">Where that manifest was read from, for the message.</param>
    public void MustCarryTheSameMetadataAs(PluginManifest built, string builtPath)
    {
        var packaged = PackagedManifest();

        var differences = Differences(built, packaged).ToList();

        if (differences.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The {PluginManifestName} inside '{File.FullName}' does not say what '{builtPath}' says, " +
            $"so the repository entry would describe something other than what a user installs:{Environment.NewLine}" +
            string.Join(Environment.NewLine, differences) + Environment.NewLine +
            "The zip is from a different build than the manifest this entry is generated from. A " +
            "metadata-only change leaves the assembly byte-identical, so the assembly comparison " +
            "cannot see this. Package the build you are releasing rather than attaching a zip from " +
            "an earlier one.");
    }

    // Named field by field so the message says WHICH value disagrees. "The manifests differ" sends
    // somebody diffing two files by hand; naming DalamudApiLevel ends the investigation.
    private static IEnumerable<string> Differences(PluginManifest built, PluginManifest packaged)
    {
        foreach (var (field, fromBuilt, fromPackaged) in new[]
        {
            ("Name", built.Name, packaged.Name),
            ("Author", built.Author, packaged.Author),
            ("Punchline", built.Punchline, packaged.Punchline),
            ("Description", built.Description, packaged.Description),
            ("RepoUrl", built.RepoUrl, packaged.RepoUrl),
            // Coalesced because an explicit "Tags": null in the zip deserialises to null and
            // string.Join threw "Value cannot be null. (Parameter 'values')" -- a refusal naming
            // neither the field, the file, nor what to do, in a method whose other refusals all do.
            // Treated as no tags rather than as an error: null and [] mean the same thing here, so
            // a zip carrying one against a build carrying the other is not a difference.
            ("Tags", Spelt(built.Tags), Spelt(packaged.Tags)),
            ("DalamudApiLevel", $"{built.DalamudApiLevel}", $"{packaged.DalamudApiLevel}"),
            ("AssemblyVersion", built.AssemblyVersion, packaged.AssemblyVersion),
        })
        {
            if (!string.Equals(fromBuilt, fromPackaged, StringComparison.Ordinal))
            {
                yield return $"  {field}: the build says '{fromBuilt}', the zip says '{fromPackaged}'";
            }
        }
    }

    private static string Spelt(List<string>? tags) => string.Join(", ", tags ?? new List<string>());

    private PluginManifest PackagedManifest()
    {
        using var archive = OpenZip();

        var entry = archive.GetEntry(PluginManifestName)
            ?? throw new InvalidOperationException(
                $"'{File.FullName}' contains no {PluginManifestName}, so it is not a plugin release " +
                "zip. That file is what Dalamud reads when it installs, and without it there is " +
                "nothing to check the repository entry against.");

        using var stream = entry.Open();

        try
        {
            return JsonSerializer.Deserialize<PluginManifest>(stream)
                ?? throw new InvalidOperationException(
                    $"The {PluginManifestName} inside '{File.FullName}' is empty.");
        }
        catch (JsonException malformed)
        {
            throw new InvalidOperationException(
                $"The {PluginManifestName} inside '{File.FullName}' is not readable as a plugin " +
                "manifest, so the repository entry cannot be checked against the archive it links to.",
                malformed);
        }
    }

    // The raw failure here reads "End of Central Directory record could not be found", which names
    // neither the file nor the mistake. The mistake is nearly always --asset pointed at the assembly
    // instead of the zip: they sit in sibling directories and differ by one path segment.
    private ZipArchive OpenZip()
    {
        try
        {
            return ZipFile.OpenRead(File.FullName);
        }
        catch (InvalidDataException notAZip)
        {
            throw new InvalidOperationException(
                $"'{File.FullName}' is not a zip. The release asset is the packaged zip " +
                "DalamudPackager writes, not the built assembly beside it.", notAZip);
        }
    }

    private static byte[] Sha256Of(Stream stream) => SHA256.HashData(ReadFully(stream));

    private static byte[] Sha256OfFile(string path) => SHA256.HashData(System.IO.File.ReadAllBytes(path));

    private static byte[] ReadFully(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
