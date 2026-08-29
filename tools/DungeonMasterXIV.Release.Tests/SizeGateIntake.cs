using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// Which files the gate measures, taken from git rather than from the filesystem.
/// </summary>
/// <remarks>
/// <b>`git ls-files`, never a hand-written `find` (DMXENG-70).</b> A `find` rooted at the directories
/// somebody remembered is what dropped <c>Plugin.cs</c> from an earlier census — it sits at the
/// repository root alongside fourteen other tracked <c>.cs</c> files, which is exactly where a search
/// that starts at <c>src/</c> and <c>tests/</c> will never look. Asking git for the tracked set
/// removes the guess.
/// </remarks>
internal static class SizeGateIntake
{
    /// <summary>Repository-relative paths of every tracked C# file the gate measures.</summary>
    public static IReadOnlyList<string> Files()
    {
        var root = TheBuild.RepositoryRoot().FullName;
        var listed = Git("ls-files -z *.cs", root);

        return [.. listed
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => !path.Contains("/obj/", StringComparison.Ordinal))
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>The file's text, read from the working tree.</summary>
    public static string Read(string repositoryRelativePath) =>
        File.ReadAllText(Path.Combine(TheBuild.RepositoryRoot().FullName, repositoryRelativePath));

    private static string Git(string arguments, string workingDirectory)
    {
        using var git = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("git did not start");

        var output = git.StandardOutput.ReadToEnd();
        var errors = git.StandardError.ReadToEnd();
        git.WaitForExit();

        // NOT SUPPRESSED. A silent git failure yields an empty list, and an empty intake measures
        // nothing while reporting no breaches -- the vacuous pass this whole ticket exists to stop.
        return git.ExitCode == 0
            ? output
            : throw new InvalidOperationException($"git {arguments} exited {git.ExitCode}: {errors}");
    }
}
