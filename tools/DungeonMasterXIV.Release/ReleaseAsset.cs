using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

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
