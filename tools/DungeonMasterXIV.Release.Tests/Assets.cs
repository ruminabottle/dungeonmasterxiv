using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using DungeonMasterXIV.Release;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// Builds real zips on disk for the tests that need one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real files rather than a seam.</b> Everything <see cref="ReleaseAsset"/> does is read an
/// artefact off disk — the name off the path, the assembly out of the archive — so a substitute
/// would be testing the substitute. The defect this guards against was a name nobody had ever
/// compared against a file.
/// </para>
/// <para>
/// Each artefact gets its own subdirectory, so two tests may both ask for a <c>latest.zip</c> and
/// get different files. That is the situation being modelled, not an accident of the helper: every
/// build writes that one name, which is why the name identifies nothing.
/// </para>
/// </remarks>
internal static class Assets
{
    /// <summary>The name DalamudPackager actually writes, as opposed to the one the tool used to assume.</summary>
    public const string PackagerName = "latest.zip";

    /// <summary>The entry <see cref="ReleaseAsset.MustMatchTheAssembly"/> compares.</summary>
    public const string PluginAssembly = "DungeonMasterXIV.dll";

    /// <summary>
    /// The entry <see cref="ReleaseAsset.MustCarryTheSameMetadataAs"/> compares — the manifest
    /// Dalamud actually reads when it installs (BUG-16).
    /// </summary>
    public const string PluginManifestName = "DungeonMasterXIV.json";

    private static readonly DirectoryInfo Root = Directory.CreateTempSubdirectory("dmxiv-release-tests");

    private static int counter;

    /// <summary>A file with the given contents, at a path nothing else in the run will reuse.</summary>
    /// <remarks>
    /// <b>Bytes, on both sides, deliberately.</b> The first version of this helper wrote the file
    /// with <c>WriteAllText</c> and the zip entry through a <c>StreamWriter</c>, which prefixes a
    /// UTF-8 byte order mark. Identical text, three bytes apart, and the matching test failed — a
    /// helper defect, but it also demonstrates the comparison is genuinely over the bytes and not
    /// over anything that would forgive a stale build.
    /// </remarks>
    public static string File(string name, string content)
    {
        var path = Fresh(name);
        System.IO.File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
        return path;
    }

    /// <summary>A zip carrying the given entries, at a path nothing else in the run will reuse.</summary>
    public static string Zip(string name, params (string Entry, string Content)[] entries)
    {
        var path = Fresh(name);

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        foreach (var (entry, content) in entries)
        {
            using var stream = archive.CreateEntry(entry).Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        return path;
    }

    /// <summary>
    /// An asset for tests that need a well-formed one and are not about assets. Named as the
    /// packager names it, so nothing downstream is asserted against a name a test invented.
    /// </summary>
    public static ReleaseAsset Any(string name = PackagerName) =>
        ReleaseAsset.At(Zip(name, (PluginAssembly, "a build")));

    /// <summary>A path in a directory that DOES exist, for the file-absent-but-directory-present case.</summary>
    public static string PathBeside(string name) => Fresh(name);

    private static string Fresh(string name)
    {
        var directory = Directory.CreateDirectory(
            Path.Combine(Root.FullName, Interlocked.Increment(ref counter).ToString()));

        return Path.Combine(directory.FullName, name);
    }
}
