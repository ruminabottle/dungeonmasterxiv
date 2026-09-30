using System.IO;
using System.IO.Compression;
using System.Text;
using DungeonMasterXIV.Release;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>Builds a real release zip on disk for tests that need a well-formed asset.</summary>
internal static class Assets
{
    /// <summary>A zip named as DalamudPackager names it, carrying a plugin assembly entry.</summary>
    public static ReleaseAsset Any()
    {
        var directory = Directory.CreateTempSubdirectory("dmxiv-release-tests");
        var path = Path.Combine(directory.FullName, "latest.zip");

        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        using (var stream = archive.CreateEntry("DungeonMasterXIV.dll").Open())
        {
            var bytes = Encoding.UTF8.GetBytes("a build");
            stream.Write(bytes, 0, bytes.Length);
        }

        return ReleaseAsset.At(path);
    }
}
