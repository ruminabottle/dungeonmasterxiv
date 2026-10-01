using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace DungeonMasterXIV.Release;

/// <summary>The built release zip, checked to hold the same plugin assembly and manifest as the build.</summary>
public sealed class ReleaseAsset
{
    private const string PluginAssemblyName = "DungeonMasterXIV.dll";

    private const string PluginManifestName = "DungeonMasterXIV.json";

    private ReleaseAsset(FileInfo file) => File = file;

    public FileInfo File { get; }

    public string Name => File.Name;

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

    public void MustMatchTheAssembly(string assemblyPath)
    {
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

    private static IEnumerable<string> Differences(PluginManifest built, PluginManifest packaged)
    {
        foreach (var (field, fromBuilt, fromPackaged) in new[]
        {
            ("Name", built.Name, packaged.Name),
            ("Author", built.Author, packaged.Author),
            ("Punchline", built.Punchline, packaged.Punchline),
            ("Description", built.Description, packaged.Description),
            ("RepoUrl", built.RepoUrl, packaged.RepoUrl),
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
