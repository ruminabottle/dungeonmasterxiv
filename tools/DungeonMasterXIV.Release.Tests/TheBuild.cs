using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// Asks the real plugin project what it would do, rather than reimplementing it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The derivation under test lives in MSBuild, so it has to be tested through MSBuild.</b> A C#
/// copy of "strip the v, parse the rest" would pass while the build did something else entirely,
/// which is the exact shape of the defect these tests exist to catch. <c>-getProperty:</c> evaluates
/// properties without compiling, so this is cheap enough to call several times in a run.
/// </para>
/// <para>
/// <b>Evaluation is not the whole build.</b> <c>-getProperty:</c> does not run targets, so it does
/// not see the guard in <c>ReleaseTagIsSpeltForTheBuild</c> — <see cref="RefusesTag"/> invokes that
/// target directly for the cases the guard owns.
/// </para>
/// <para>
/// Extracted when a third test class needed it. Two copies of a filesystem walk were cheaper than a
/// shared file; three were not.
/// </para>
/// </remarks>
internal static class TheBuild
{
    /// <summary>The <c>Version</c> the plugin project evaluates to for a tag, with no file edited.</summary>
    public static string VersionStampedFor(string? releaseTag)
    {
        var (exitCode, output, errors) = Run($"msbuild \"{PluginProject()}\" -getProperty:Version", releaseTag);

        Assert.True(exitCode == 0, $"Evaluating Version for '{releaseTag}' failed, so this says nothing:\n{output}\n{errors}");

        return output;
    }

    /// <summary>Whether the build's own guard rejects this tag before it reaches NuGet restore.</summary>
    public static bool RefusesTag(string releaseTag) =>
        Run($"msbuild \"{PluginProject()}\" -t:ReleaseTagIsSpeltForTheBuild", releaseTag).ExitCode != 0;

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

    public static string PluginProject() => Path.Combine(RepositoryRoot().FullName, "DungeonMasterXIV.csproj");

    private static (int ExitCode, string Output, string Errors) Run(string arguments, string? releaseTag)
    {
        if (releaseTag is not null)
        {
            arguments += $" -p:ReleaseTag={releaseTag}";
        }

        using var msbuild = Process.Start(new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;

        var output = msbuild.StandardOutput.ReadToEnd().Trim();
        var errors = msbuild.StandardError.ReadToEnd().Trim();
        msbuild.WaitForExit();

        return (msbuild.ExitCode, output, errors);
    }
}
