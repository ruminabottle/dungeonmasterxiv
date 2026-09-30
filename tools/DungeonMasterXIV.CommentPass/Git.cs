using System.Diagnostics;

namespace DungeonMasterXIV.CommentPass;

/// <summary>git, run in the repository root. A non-zero exit throws, so nothing reads as empty.</summary>
public static class Git
{
    public static string Root { get; } = Run(Environment.CurrentDirectory, "rev-parse", "--show-toplevel").Trim();

    public static string Show(string baseRef, string path) => Run(Root, "show", $"{baseRef}:{path}");

    /// <summary>Files differing between <paramref name="baseRef"/> and the working tree, with a one-letter status.</summary>
    public static IReadOnlyList<(char Status, string Path)> Changed(string baseRef, IReadOnlyList<string> pathspecs) =>
        [.. Run(Root, ["diff", "--name-status", "--no-renames", baseRef, "--", .. pathspecs])
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => (line[0], line[(line.IndexOf('\t') + 1)..]))];

    public static IReadOnlyList<string> Tracked(IReadOnlyList<string> pathspecs) =>
        [.. Run(Root, ["ls-files", "-z", "--", .. pathspecs]).Split('\0', StringSplitOptions.RemoveEmptyEntries)];

    private static string Run(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start");
        var output = git.StandardOutput.ReadToEnd();
        var errors = git.StandardError.ReadToEnd();
        git.WaitForExit();
        return git.ExitCode == 0 ? output : throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {errors}");
    }
}
