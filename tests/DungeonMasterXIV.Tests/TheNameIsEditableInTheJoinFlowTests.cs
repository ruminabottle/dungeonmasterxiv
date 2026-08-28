using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.2n's machine half: the name that will be sent is shown and editable in the join flow.
/// </summary>
/// <remarks>
/// <para>
/// <b>A-1.2n is split and this discharges ONE HALF.</b> The criterion says so itself — <i>machine for
/// where the control is reachable, in-game for it being on the path.</i> A control can exist, be
/// reachable, and still not sit on the path a joining player walks, and nothing runnable here can
/// tell anyone whether it does. <b>The in-game half is NOT discharged by this file</b>, the same way
/// C33 could not discharge A-1.18's first link and T-16 could not discharge A-1.17.
/// </para>
/// <para>
/// <b>Read rather than executed, because no test project links the plugin.</b>
/// <c>DungeonMasterXIV.Tests</c> references Core alone, deliberately, to keep Dalamud out of its
/// graph — so <c>SessionWindow</c> cannot be constructed here. Three tests already read window
/// source for exactly this reason; this is the fourth.
/// </para>
/// <para>
/// <b>Two controls, both learned from defects in the tests that came before.</b> The scan asserts
/// the source was actually FOUND, because a scan over an unresolved path matches nothing and goes
/// green (BUG-48's shape). And it matches only CODE — comments are stripped first, so a sentence
/// describing the control cannot stand in for the control.
/// </para>
/// </remarks>
public class TheNameIsEditableInTheJoinFlowTests
{
    // The criterion. Fails on a build whose only name control is in settings — which is what this
    // repository shipped until now, and which passes every settings test there is.
    [Fact]
    public void TheJoinFlowRendersAnEditableNameControl()
    {
        var code = JoinFlowCode();

        Assert.NotNull(NameField(code));
    }

    // The half that stops the control being decoration. A field the user can type into, whose value
    // is then ignored in favour of the stored setting, satisfies "editable" and fails the criterion:
    // the user would be shown one name and send another.
    [Fact]
    public void TheNameThatIsSentComesFromThatControl()
    {
        var code = JoinFlowCode();
        var field = NameField(code);

        Assert.NotNull(field);

        var requestJoin = code.FirstOrDefault(line => line.Contains("RequestJoin(", StringComparison.Ordinal));
        Assert.NotNull(requestJoin);
        Assert.Contains(field!, requestJoin!, StringComparison.Ordinal);
    }

    // THE CONTROL ON THE CONTROL, and the reason the two tests above are worth anything. A scan that
    // resolves no files matches nothing and reports success — the exact way BUG-48's guard was blind
    // while its comment claimed otherwise. If this ever finds nothing to read, the criterion above
    // is being satisfied by an empty corpus.
    [Fact]
    public void TheScanActuallyFoundTheWindowsItClaimsToRead()
    {
        var sources = WindowSources();

        Assert.NotEmpty(sources);
        Assert.Contains(sources, path => path.EndsWith("SessionWindow.cs", StringComparison.Ordinal));
        Assert.NotEmpty(JoinFlowCode());
    }

    // The second control: a comment is not a control. Without this, a scan for "Name" would be
    // satisfied by the paragraph explaining why the name matters, and the file is full of those.
    [Fact]
    public void CommentsAreNotMistakenForCode()
    {
        var raw = File.ReadAllLines(SessionWindowPath());

        Assert.Contains(raw, line => line.TrimStart().StartsWith("//", StringComparison.Ordinal));
        Assert.DoesNotContain(JoinFlowCode(), line => line.TrimStart().StartsWith("//", StringComparison.Ordinal));
    }

    /// <summary>
    /// The identifier the join flow's name box is bound to, or null if there is no such box.
    /// </summary>
    /// <remarks>
    /// Matched on <c>ImGui.InputText</c> with a label naming what it holds, and the bound field is
    /// returned rather than merely detected — <see cref="TheNameThatIsSentComesFromThatControl"/>
    /// needs the identifier to tie the control to the send, which is what stops a field existing
    /// beside a value taken from somewhere else.
    /// </remarks>
    private static string? NameField(IReadOnlyList<string> code)
    {
        foreach (var line in code)
        {
            var match = Regex.Match(line, @"ImGui\.InputText\(""([^""]*)""\s*,\s*ref\s+(\w+)");
            if (match.Success && match.Groups[1].Value.Contains("Name", StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups[2].Value;
            }
        }

        return null;
    }

    /// <summary>The join flow's code, comments removed.</summary>
    private static IReadOnlyList<string> JoinFlowCode() =>
        File.ReadAllLines(SessionWindowPath())
            .Select(line => line.TrimEnd())
            .Where(line => line.Length > 0 && !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .ToList();

    private static string SessionWindowPath() =>
        WindowSources().Single(path => path.EndsWith("SessionWindow.cs", StringComparison.Ordinal));

    /// <summary>
    /// Every window's source, enumerated from disk rather than named — a window added tomorrow is
    /// read tomorrow, and a list here would be a second place to keep up to date.
    /// </summary>
    private static IReadOnlyList<string> WindowSources() =>
        Directory.EnumerateFiles(WindowsDirectory(), "*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static string WindowsDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Windows");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "SessionWindow.cs")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"No Windows/ containing SessionWindow.cs above {AppContext.BaseDirectory}; the windows this reads are missing.");
    }
}
