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

    // A-1.2n's own sentence: the name that WILL BE SENT is shown. A box the user types into whose
    // contents are then re-resolved on the way out satisfies "editable" and breaks the criterion —
    // DisplayName refuses a large class of ordinary invented names, so the field would show Bob_123
    // while the wire carried "a player who gave no name", under a label that is the very promise.
    //
    // ===================================================================================
    // THIS IS A TEXTUAL PROXY FOR A DATA-FLOW PROPERTY. IT IS NOT A PROOF OF ONE, AND A
    // GREEN RUN HERE IS NOT EVIDENCE THAT A-1.2n HOLDS.
    // ===================================================================================
    //
    // A-1.2n is about where a VALUE goes. Everything below reads SOURCE TEXT, and the most a
    // textual scan can ever say is "this identifier does not appear here". That is one assignment
    // away from wrong, always.
    //
    // AN ALIAS DEFEATS IT. These four lines pass both assertions below — measured, not reasoned:
    //
    //     var willSend = DisplayName.OrNone(_nameEntry);
    //     var typed = _nameEntry;                            // the whole defeat
    //     ImGui.TextWrapped($"Resolved: {willSend.Value}");  // satisfies the value-position check
    //     ImGui.TextWrapped($"They will see: {typed}");      // renders the RAW value
    //
    // 4 passed, 0 failed. The first assertion is satisfied by the OTHER, correct statement; the
    // second looks for `_nameEntry` in a statement that says `typed`. The alias line is not an
    // ImGui.Text* call, so it is never scanned at all. Found by qa-1 (BUG-65); reproduced here
    // before this comment was written.
    //
    // THREE GUARDS SO FAR, AND EACH KEPT THE SAME VERB:
    //     line match      -> defeated by a wrapped ternary whose CONDITION named the local (BUG-64)
    //     statement match -> defeated by rendering a second, correct statement (BUG-65)
    //     name match      -> defeated by renaming the value first (BUG-65)
    // Every replacement narrowed the gap and none changed what is being matched: TEXT, where the
    // property is about a VALUE. A fourth narrowing buys one more hop.
    //
    // A REAL FIX ASSERTS OVER BEHAVIOUR OR OVER A PARSE, not over source text — observe what the
    // window renders and what it sends and compare them, or read the syntax tree and follow the
    // assignment. Both are larger than this file. The tempting middle option, asserting that EVERY
    // interpolation hole resolves to the sent value, is already considered and rejected: it is
    // false the moment the join flow renders anything else — a code, a countdown, a remaining time
    // — so it would fail on correct code or need an exception list, and an exception list is a
    // denylist wearing an allowlist's name.
    //
    // SO THE END-TO-END COVERAGE IS THE IN-GAME CHECK, DMXHUM-6, AND IT IS LOAD-BEARING RATHER THAN
    // SUPPLEMENTARY. This narrows the ways the criterion can break by accident. It does not
    // establish that it holds, and nothing in this file can.
    //
    // What the assertions below are still worth: they hold against the three defeats already found,
    // and their teeth were measured rather than assumed — BUG-64's mutation reddens the first, and
    // removing either one reddens this test. AND WHAT DELETING THEM WOULD COST, which is the half
    // that survives an argument: "it is only a proxy" reads as a case for removal right up until
    // the regression has a name. DELETE THESE AND BUG-64 AND BUG-65 COME BACK WITH NOTHING TO
    // NOTICE — a wrapped ternary putting the raw field in the branch it displays, an alias
    // rendering it under a second statement, and the box reading Bob! while the wire carries "a
    // player who gave no name". Both were live and green before someone went looking. A proxy that
    // holds against three known defeats is worth less than a proof and considerably more than no
    // check at all. Kept exactly as they are.
    //
    // So this asserts the STRONGER property the fix establishes: one resolved value, rendered and
    // sent. Not "both mention the field" — the SAME identifier in both places, which is what makes
    // the criterion true by construction rather than by anyone keeping two expressions in step.
    [Fact]
    public void TheValueShownIsTheSameValueSent()
    {
        var code = JoinFlowCode();
        var field = NameField(code);
        Assert.NotNull(field);

        // The field is resolved exactly once, into a named local.
        var resolved = code
            .Select(line => Regex.Match(line, @"var (\w+) = DisplayName\.OrNone\(" + Regex.Escape(field!) + @"\)"))
            .FirstOrDefault(m => m.Success)?.Groups[1].Value;

        Assert.True(resolved is not null, $"The join flow never resolves '{field}' through DisplayName.");

        // STATEMENTS, not lines, and that is the fix rather than a detail. The render is a ternary
        // that wraps, so its CONDITION and its BRANCHES are on different source lines. The previous
        // check asked for a line holding both "ImGui.Text" and the resolved local, and
        // `ImGui.TextWrapped(willSend.WasStated` satisfies that on its own — the condition matched
        // before either branch was looked at, so the raw field could be put back in the displayed
        // branch with the suite green. That is PR #89's denial reproduced (BUG-64).
        var rendered = RenderedStatements(code);
        Assert.NotEmpty(rendered);

        // A VALUE POSITION: inside an interpolation hole, {resolved} or {resolved.Something}. A
        // mention anywhere in the statement is what the old check accepted.
        var shownAsAValue = rendered.Any(statement =>
            Regex.IsMatch(statement, @"\{" + Regex.Escape(resolved!) + @"[.}]"));

        Assert.True(
            shownAsAValue,
            $"No rendered statement in the join flow interpolates '{resolved}'. A-1.2n requires the "
            + "name that will be SENT to be the name SHOWN, so the resolved value has to be what is "
            + "rendered — naming it in a condition is not showing it.");

        // AND THE HALF THAT ACTUALLY CLOSES IT. The defect is displaying the raw field, so forbid
        // displaying the raw field. Without this, any future render that mentions the resolved local
        // somewhere and shows the field elsewhere passes the assertion above.
        var showsTheRawField = rendered.FirstOrDefault(statement =>
            Regex.IsMatch(statement, @"\b" + Regex.Escape(field!) + @"\b"));

        Assert.True(
            showsTheRawField is null,
            $"A rendered statement in the join flow shows the raw field '{field}' rather than the "
            + $"resolved value: {showsTheRawField}. The box would read what the user typed while the "
            + "wire carried something else, which is the criterion inverted.");

        var sent = code.FirstOrDefault(l => l.Contains("RequestJoin(", StringComparison.Ordinal));
        Assert.NotNull(sent);
        Assert.Contains(resolved!, sent!, StringComparison.Ordinal);

        // And the raw field is NOT what goes on the wire — that is the defect this replaced.
        Assert.DoesNotContain($"RequestJoin(code, {field})", sent!, StringComparison.Ordinal);
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

    /// <summary>
    /// Every <c>ImGui.Text*</c> call in the join flow, each as one whole statement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Balanced parentheses, not a line and not a split on <c>;</c>.</b> A line-based scan cannot
    /// see a wrapped ternary, and splitting on <c>;</c> would cut a statement in half the first time
    /// somebody writes a semicolon inside an interpolated string. This walks from the call to its
    /// closing parenthesis, skipping over string literals so that brackets and quotes inside a
    /// message cannot move the boundary.
    /// </para>
    /// <para>
    /// <b>It is a bracket matcher, not a parser, and the difference is worth stating.</b> It does not
    /// understand C#; it understands nesting and string literals, which is what this property needs.
    /// If a future check needs to know that an expression is a condition rather than a branch, that
    /// is a real parse and this helper should be replaced rather than extended.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<string> RenderedStatements(IReadOnlyList<string> code)
    {
        var text = string.Join("\n", code);
        var statements = new List<string>();

        foreach (Match call in Regex.Matches(text, @"ImGui\.Text\w*\s*\("))
        {
            var open = text.IndexOf('(', call.Index);
            var depth = 0;
            var inString = false;
            var verbatim = false;

            for (var i = open; i < text.Length; i++)
            {
                var c = text[i];

                if (inString)
                {
                    if (c == '\\' && !verbatim)
                    {
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        verbatim = i > 0 && text[i - 1] == '@';
                        break;
                    case '(':
                        depth++;
                        break;
                    case ')':
                        depth--;
                        if (depth == 0)
                        {
                            statements.Add(text[call.Index..(i + 1)]);
                            i = text.Length;
                        }

                        break;
                }
            }
        }

        return statements;
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
