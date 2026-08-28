using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Every string this product ships to a user, with the file and declaration it came from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Split from <c>ShippedCopyMeetsItsConstraintsTests</c>, which had two reasons to change.</b>
/// How copy is FOUND — which files are swept, how a literal is matched to its declaration — moves
/// when the source layout does. What copy must SAY moves when a decision reverses. The standards
/// name that split directly: <i>"when a file starts needing a section comment to separate its
/// parts, those parts are two files."</i> The combined file also passed the 450-line blocking
/// limit once the review findings were fixed, which is what forced the issue rather than taste.
/// </para>
/// <para>
/// <b>Nothing here asserts anything.</b> It is the corpus and the extractor only, so a change to
/// what is refused cannot quietly change what is read.
/// </para>
/// </remarks>
internal static class ShippedCopyCorpus
{
/// <summary>Every string literal this product ships, with the file and declaration it came from.</summary>
/// <remarks>
/// <b>No length or shape filter, deliberately.</b> Filtering to "sentences" would be a second
/// place for a violating string to hide — "anonymous" alone is nine characters and would pass a
/// plausible one. The cost is that identifiers and format fragments are swept too; they contain
/// no refused phrasing, so the cost is nothing.
/// </remarks>
internal static IReadOnlyList<(string File, string Name, string Text)> ShippedCopy() =>
    SourcesSwept()
        .SelectMany(source => LiteralsIn(source)
            .Select(literal => (File: Path.GetFileName(source), literal.Name, literal.Text)))
        .ToList();

internal static IEnumerable<(string Name, string Text)> LiteralsIn(string source)
{
    // Comment lines are stripped first: this file's own commentary quotes refused phrasings, and
    // so does the source it reads — SessionFailure.cs explains BUG-49 by quoting the sentence it
    // removed. Sweeping commentary would refuse the explanation of the fix.
    var body = string.Join(
        "\n",
        File.ReadAllLines(source).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    // Which declaration each literal sits in, so the ruled constants can be told from the rest.
    // A literal outside any of them — a switch arm in SessionFailure.cs, for instance — is
    // engineering-authored, which is the safe default: it gets the stricter constraint.
    var declarations = Regex
        .Matches(body, @"(?:const\s+string|string\[\])\s+(?<name>\w+)\s*=(?<body>.*?);", RegexOptions.Singleline)
        .Select(match => (Name: match.Groups["name"].Value, match.Groups["body"].Index, match.Groups["body"].Length))
        .ToList();

    foreach (Match literal in Regex.Matches(body, @"""(?<literal>(?:[^""\\]|\\.)*)"""))
    {
        var owner = declarations.FirstOrDefault(
            declaration => literal.Index >= declaration.Index
                && literal.Index < declaration.Index + declaration.Length);

        yield return (owner.Name ?? "(inline)", literal.Groups["literal"].Value);
    }
}

/// <summary>
/// The Core files that carry user-facing copy, relative to the repository root.
/// </summary>
/// <remarks>
/// <b><c>DisplayName</c> was found missing from this list AFTER the sweep shipped</b>, by the
/// Code Reviewer. <c>Unstated</c> — "a player who gave no name" — is rendered in the admission
/// prompt and had never been swept. The PR that added this file described an unswept Core file
/// as a FUTURE risk; the instance already existed. That is the cost of an enumerated list, and
/// it is why <see cref="TheNamedCoreFilesAreAllSwept"/> now guards it.
/// </remarks>
internal static readonly string[] NamedCoreCopyFiles =
{
    Path.Combine("src", "DungeonMasterXIV.Core", "Net", "SessionFailure.cs"),
    Path.Combine("src", "DungeonMasterXIV.Core", "Net", "DisplayName.cs"),
};

/// <summary>
/// The files that carry user-facing copy: every window, plus the named Core files.
/// </summary>
/// <remarks>
/// <para>
/// <b>The windows are derived; the Core side is named, and the two fail differently.</b> Nothing
/// in the source marks a string as user-facing, so the Core boundary is drawn by hand.
/// </para>
/// <para>
/// <b>A named file that disappears now throws instead of vanishing.</b> This previously ended
/// <c>.Where(File.Exists)</c>, so renaming or deleting a named file silently shrank the corpus
/// and every sweep kept passing over less. The one place a file could go missing was the one
/// place nothing checked. It is now an exception naming the file.
/// </para>
/// <para>
/// <b>The residual, stated exactly.</b> Copy added to a Core file that is not on this list is
/// NOT swept, and nothing here fails when that happens — no test can, because "user-facing" is
/// not expressed anywhere a test could read. <see cref="TheSweepReadsEveryWindowOnDisk"/> covers
/// the derived half only. Sweeping all of Core was measured rather than dismissed: it is clean
/// today across all 73 files, so it is possible — but it would apply user-facing copy rules to
/// internal strings, where an exception message mentioning a "private" key would fail a COPY
/// sweep for a non-copy reason. That trade was not taken here; it is recorded so the next reader
/// can take it deliberately rather than rediscover it.
/// </para>
/// </remarks>
internal static IReadOnlyList<string> SourcesSwept() => SweptSources(RepositoryRoot(), NamedCoreCopyFiles);

/// <summary>
/// <see cref="SourcesSwept"/> with the root and the named list passed in, so the missing-file
/// path can be probed without editing the real list. A guard nobody has watched fail is a guard
/// nobody should trust.
/// </summary>
internal static IReadOnlyList<string> SweptSources(string root, IReadOnlyList<string> namedCoreFiles)
{
    var named = namedCoreFiles.Select(relative => Path.Combine(root, relative)).ToList();
    var missing = named.Where(path => !File.Exists(path)).ToList();

    if (missing.Count > 0)
    {
        throw new FileNotFoundException(
            "A file named in NamedCoreCopyFiles is not on disk, so the copy sweep would silently "
            + "cover less than it claims. Update the list or restore the file: "
            + string.Join(", ", missing));
    }

    return Directory.EnumerateFiles(Path.Combine(root, "Windows"), "*.cs")
        .Concat(named)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();
}

internal static string WindowsDirectory() => Path.Combine(RepositoryRoot(), "Windows");

internal static string RepositoryRoot()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Windows", "SessionWindow.cs")))
        {
            return directory.FullName;
        }
    }

    throw new InvalidOperationException(
        $"No repository root above {AppContext.BaseDirectory}; the copy this sweeps is missing.");
}

internal static string Excerpt(string text) => text.Length <= 70 ? text : text[..70] + "...";
}
