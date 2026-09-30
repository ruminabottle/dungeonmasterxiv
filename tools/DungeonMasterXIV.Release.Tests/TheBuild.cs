using System;
using System.IO;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>Locates the repository the tests run from.</summary>
internal static class TheBuild
{
    public static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DungeonMasterXIV.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!;
    }
}
