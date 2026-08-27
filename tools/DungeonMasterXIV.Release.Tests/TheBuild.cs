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
/// <b>Evaluation is not the whole build, and one target is not either.</b> <c>-getProperty:</c> does
/// not run targets; <see cref="GuardRefusesTag"/> runs exactly one. Neither reaches the compiler, so
/// a claim about what THE BUILD does needs <see cref="FailsToBuild"/> — that distinction is BUG-25,
/// and it cost a test that passed on a live counter-example to its own invariant.
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

    /// <summary>
    /// Whether the build's spelling GUARD rejects this tag — the guard alone, not the build.
    /// </summary>
    /// <remarks>
    /// <b>Named for what it asks (BUG-25).</b> This used to be called <c>RefusesTag</c> and was used
    /// to test the claim "no tag the tool accepts is one the build refuses". It runs one target, so
    /// what it actually answers is whether the <i>guard</i> refuses — and <c>v70000.0.0</c> passes the
    /// guard while the real build fails it with CS7034. Four cases went green over a tag that
    /// violated the very invariant they were written to enforce. Use <see cref="FailsToBuild"/> for
    /// any claim about the build; use this only for claims about the guard.
    /// </remarks>
    public static bool GuardRefusesTag(string releaseTag) =>
        Run($"msbuild \"{PluginProject()}\" -t:ReleaseTagIsSpeltForTheBuild", releaseTag).ExitCode != 0;

    /// <summary>Whether a real build of the plugin fails for this tag.</summary>
    /// <remarks>
    /// <para>
    /// <b>A real build, because anything narrower has the reach problem this replaces.</b> The
    /// failures worth catching here come from the compiler, not from a target we wrote — CS7034 is
    /// emitted while compiling the generated <c>AssemblyInfo.cs</c>, which no evaluation-only or
    /// single-target invocation reaches.
    /// </para>
    /// <para>
    /// <b>Output is redirected so a test cannot overwrite the artefact other tests read.</b>
    /// <c>ManifestMatchesTheBuiltPluginTests</c> asserts against whatever sits in <c>bin/</c>;
    /// building there from a test would let one test rewrite another's fixture.
    /// <c>BaseOutputPath</c> only — redirecting <c>BaseIntermediateOutputPath</c> as well makes the
    /// project reference fail with <c>NETSDK1005</c>, so every tag would look like a build failure
    /// and this helper would answer true for everything. Measured, not assumed.
    /// </para>
    /// <para>
    /// About 0.7s per call, which is why callers use a handful of representative tags rather than
    /// every case in a theory.
    /// </para>
    /// </remarks>
    public static bool FailsToBuild(string releaseTag) => RealBuild(releaseTag).ExitCode != 0;

    /// <summary>What a real build says when it refuses this tag.</summary>
    /// <remarks>
    /// <b>The real build, not the guard target (BUG-30).</b> The claim being tested is that the
    /// operator gets a readable refusal, and the operator runs <c>dotnet build</c> — so the text has
    /// to come from there. Two shapes reached past the guards and failed inside MSBuild itself,
    /// which a single-target invocation would never have shown.
    /// </remarks>
    public static string RefusalFromARealBuild(string releaseTag)
    {
        var (exitCode, output, errors) = RealBuild(releaseTag);

        Assert.True(exitCode != 0, $"Expected a real build of '{releaseTag}' to fail, and it succeeded.");

        return output + errors;
    }

    private static (int ExitCode, string Output, string Errors) RealBuild(string releaseTag) =>
        Run(
            $"build \"{PluginProject()}\" -c Release -p:BaseOutputPath=\"{IsolatedOutput.FullName}/\"",
            releaseTag);

    private static readonly DirectoryInfo IsolatedOutput =
        Directory.CreateTempSubdirectory("dmxiv-tag-builds");

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
