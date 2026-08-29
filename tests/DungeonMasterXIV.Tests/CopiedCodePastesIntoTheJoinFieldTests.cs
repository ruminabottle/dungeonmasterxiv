using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.18: the copied session code is accepted <b>verbatim</b> by the join field.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two components, both correct alone, and the defect lives between them.</b> R-1.2a groups the
/// code so it can be read aloud, so the producer may reasonably copy the grouped form; the join
/// field may reasonably reject anything it was not taught to strip. A test on the copy and a test
/// on the join field can both pass while A-1.18 fails, which is why this one crosses.
/// </para>
/// <para>
/// <b>What this can and cannot hold, stated rather than left to be assumed.</b> It pins the two
/// Core halves against each other over the whole alphabet. It still cannot <i>execute</i> the
/// button: <c>DungeonMasterXIV.Tests</c> references Core alone and may never reference the plugin,
/// so <see cref="WhatTheCopyActionProduces"/> could not call the window. C34 removed the need to:
/// it calls <see cref="SessionCode.ToClipboardString"/>, the member the button calls.
/// <para>
/// <b>That mirror is no longer unchecked (BUG-44), and the sentence here used to say it was.</b> It
/// read "the one link a reviewer must still check by eye" — which was true of a linked test and not
/// of a source-reading one, and an unchecked link turned out to be an undetectable one: changing the
/// button to add a label left the whole suite green at 534 passed while producing a clipboard value
/// this very file lists as rejected. <see cref="TheButtonCopiesTheDisplayedCodeAndNothingElse"/>
/// reads the window's source and closes it, the way <c>TlsBypassFenceTests</c> already does for a
/// D-1 fence from this same project.
/// </para>
/// <para>
/// What remains unheld, so the next reader does not over-trust this: the source check asserts the
/// SHAPE of the copied expression, not that <c>ToClipboardString</c> and <c>TryParse</c> agree —
/// that is what the Core-to-Core tests below are for, and the two together are what A-1.18 needs.
/// Those tests now exercise the real clipboard member rather than a copy of its body, so the pair
/// covers the actual path instead of two expressions that happen to match.
/// </para>
/// </remarks>
public class CopiedCodePastesIntoTheJoinFieldTests
{
    /// <summary>
    /// What the Copy button puts on the clipboard.
    /// </summary>
    /// <remarks>
    /// <b>No longer a mirror (C34).</b> This used to restate <c>code.ToDisplayString()</c> and say
    /// so — a second expression that had to be kept in step with the window by hand. It now calls
    /// <see cref="SessionCode.ToClipboardString"/>, the SAME member the button calls, and
    /// <see cref="TheButtonCopiesTheNamedClipboardValueAndNothingElse"/> reads the window's source
    /// to prove the button calls it. One symbol, two callers, and the link between them checked
    /// rather than documented.
    /// </remarks>
    private static string WhatTheCopyActionProduces(SessionCode code) => code.ToClipboardString();

    /// <summary>
    /// The argument the Copy button actually passes to <c>SetClipboardText</c>, read out of
    /// <c>Windows/SessionWindow.cs</c>.
    /// </summary>
    /// <remarks>
    /// <b>Reading the source is how a Core-only project reaches a plugin line (BUG-44).</b> This
    /// project references Core alone and may never reference the plugin, so nothing here can execute
    /// <see cref="WhatTheCopyActionProduces"/>'s original. That was taken to mean the link could only
    /// be checked by eye — and an unchecked link is an undetectable one: changing the button to
    /// <c>$"Session code: {code.ToDisplayString()}"</c> left the whole suite green at 534 passed,
    /// while producing a clipboard value this very file lists as one the join field rejects.
    /// <para>
    /// <c>TlsBypassFenceTests</c> in this same project already reads source with
    /// <see cref="File.ReadAllText"/> and a <see cref="Regex"/>, and is trusted for a D-1 fence. The
    /// technique is established here; it had just not been pointed at this line.
    /// </para>
    /// </remarks>
    private static string WhatTheButtonPutsOnTheClipboard()
    {
        var calls = WindowSources()
            .SelectMany(source => Regex
                .Matches(File.ReadAllText(source), @"SetClipboardText\(\s*(?<argument>[^;]*?)\s*\)\s*;")
                .Select(match => match.Groups["argument"].Value))
            .ToList();

        // Exactly one, ACROSS EVERY WINDOW, so a second copy path cannot appear beside this one
        // unnoticed. BUG-48: this read only SessionWindow.cs while saying that, so the sentence was
        // true inside one file and false one file along — which is precisely the case it is about.
        // A copy path added to any other window was invisible to it.
        Assert.Single(calls);

        return calls[0];
    }

    /// <summary>
    /// Every window's source, enumerated from disk rather than named.
    /// </summary>
    /// <remarks>
    /// Listing the windows here would put the guard's reach in a second place that has to be kept
    /// up to date, and a window added tomorrow would be unscanned with nothing to say so — the same
    /// shape as the defect this replaces, moved into the test file.
    /// </remarks>
    /// <summary>Every <c>.cs</c> file beneath <c>Windows/</c>, found by walking rather than by globbing.</summary>
    /// <remarks>
    /// <para>
    /// <b>This exists to be a SECOND SOURCE, and the recursion is written out for that reason
    /// (BUG-67, reaching this file as BUG-101).</b> The obvious implementation is
    /// <c>EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)</c> — which is the call
    /// <see cref="WindowSources"/> makes. Comparing a function against itself is what left the old
    /// control blind: both sides missed subdirectories, missed them EQUALLY, and the equality
    /// passed. TWO CALLS TO ONE FUNCTION AGREE BY CONSTRUCTION, AND THAT AGREEMENT IS NOT EVIDENCE.
    /// </para>
    /// <para>
    /// <b>Measured, not argued.</b> With one subdirectory added under <c>Windows/</c>: before
    /// BUG-101 both sides were top-level and the guard PASSED while missing the file entirely;
    /// with only <see cref="WindowSources"/> made recursive it FAILED with "Collections differ",
    /// blaming the guard rather than its own narrower control. Both sides now see the same tree by
    /// different routes.
    /// </para>
    /// <para>
    /// So this descends explicitly and takes no <c>SearchOption</c>. Narrowing
    /// <see cref="WindowSources"/> back to top-level cannot narrow this with it, which is the
    /// property the control needs and the one that was missing.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> EveryCsFileBeneath(string directory)
    {
        foreach (var file in Directory.GetFiles(directory, "*.cs"))
        {
            yield return file;
        }

        foreach (var subdirectory in Directory.GetDirectories(directory))
        {
            foreach (var file in EveryCsFileBeneath(subdirectory))
            {
                yield return file;
            }
        }
    }

    private static IReadOnlyList<string> WindowSources() =>
        Directory.EnumerateFiles(WindowsDirectory(), "*.cs", SearchOption.AllDirectories)
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

    // BUG-48. Fails if: the guard above is pointed back at one named file. The comment on it claims
    // a property of the CODEBASE — that no second copy path can appear unnoticed — and Assert.Single
    // counts only what it was handed, so a reader looking at one window makes that sentence false
    // one file along while it still reads clean.
    [Fact]
    public void TheClipboardGuardReadsEveryWindowRatherThanOneNamedFile()
    {
        var scanned = WindowSources().Select(Path.GetFileName).ToList();
        var onDisk = EveryCsFileBeneath(WindowsDirectory()).Select(Path.GetFileName).ToList();

        // Derived from disk on both sides, so a window added tomorrow is covered without anyone
        // remembering to add it here.
        Assert.Equal(onDisk.OrderBy(name => name, StringComparer.Ordinal), scanned.OrderBy(name => name, StringComparer.Ordinal));

        // And the part that fails if it is narrowed back: more than one file is actually read.
        Assert.True(
            scanned.Count > 1,
            $"The guard scanned {scanned.Count} file(s). It must read every window, or its claim that "
            + "a second copy path cannot appear unnoticed is false one file along.");
    }

    // BUG-44. A-1.18 requires the copied value to be accepted VERBATIM by the join field, and the
    // mirror above only asserts that of a string this test file invents. This asserts it of the
    // button.
    //
    // The shape rather than the exact text, deliberately: renaming the local is harmless and must
    // not fail, while anything that wraps, labels or concatenates the value must. A literal string
    // comparison would forbid the first as loudly as the second, and a guard that fails on a benign
    // edit gets relaxed rather than fixed.
    [Fact]
    public void TheButtonCopiesTheNamedClipboardValueAndNothingElse()
    {
        var argument = WhatTheButtonPutsOnTheClipboard();

        // ToClipboardString, not ToDisplayString (C34). The button must reach for the member that
        // means "what a recipient pastes", not the one that means "how this reads aloud". They
        // return the same text today; the point is that a change made for one stops silently
        // changing the other, which is the drift A-1.18 exists to catch.
        Assert.Matches(@"^[A-Za-z_][A-Za-z0-9_]*\.ToClipboardString\(\)$", argument);
    }

    // The half that names what went wrong rather than only forbidding it: the reported break put a
    // label in front of the code, and the mutation that proved the gap was exactly that.
    [Fact]
    public void TheButtonAddsNoLabelOrPunctuationAroundTheCode()
    {
        var argument = WhatTheButtonPutsOnTheClipboard();

        Assert.DoesNotContain("\"", argument, StringComparison.Ordinal);
        Assert.DoesNotContain("+", argument, StringComparison.Ordinal);
    }

    /// <summary>
    /// What the join field accepts — <b>the production decision itself, called (DMXENG-15).</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This used to RE-IMPLEMENT it, and the difference is the point.</b> It read
    /// <c>SessionCode.TryParse(pasted, out code)</c> under a doc comment saying it "mirrors
    /// <c>DrawJoining</c>" — and a comment is not an assertion. The mirror and the join field could
    /// have diverged at any time with every test in this file still green, because nothing but prose
    /// connected them.
    /// </para>
    /// <para>
    /// <b>Why <see cref="JoinFlowCode"/> rather than <see cref="SessionCode.TryParse"/> directly.</b>
    /// <c>JoinFlowCode.Accepts</c> DELEGATES to that parser and behaves identically today — the
    /// gain is not behavioural. It is that this test now names the join field's OWN rule, so if
    /// the field's rule ever diverges from the general parser's, these tests follow the field
    /// rather than silently tracking the wrong one.
    /// </para>
    /// <para>
    /// <b>What still is not proven here, stated rather than left implied.</b> This proves the
    /// tests and the join field agree on the DECISION. That the BUTTON reaches for it is a separate
    /// claim and a textual one — <see cref="TheJoinButtonReachesForTheSharedDecision"/> — because
    /// no test project references the plugin and this call cannot be observed from a running window.
    /// </para>
    /// </remarks>
    private static bool WhatTheJoinFieldAccepts(string pasted, out SessionCode code) =>
        JoinFlowCode.Accepts(pasted, out code);

    // The other half, in the same shape as TheButtonCopiesTheNamedClipboardValueAndNothingElse:
    // above proves the shared decision behaves; this proves the join button is what asks it.
    // Without it the extraction produces a testable seam and the window quietly keeps its own copy.
    //
    // ===================================================================================
    // THIS IS A TEXTUAL PROXY FOR A "SOLE DECISION" PROPERTY. IT IS NOT A PROOF OF ONE,
    // AND A GREEN RUN HERE IS NOT EVIDENCE THAT THE JOIN FIELD HAS ONE WAY IN.
    // ===================================================================================
    //
    // DECLARED AT THE DEPLOYMENT MANAGER'S DIRECTION BEFORE MERGE (DMXENG-15), because this is the
    // FIFTH member of a family this board has ruled on three times, and it would otherwise have
    // arrived undeclared and become tomorrow's bug against finished work.
    //
    // THE TWO PROPERTIES ARE DIFFERENT KINDS OF THING AND ONLY ONE IS A PROOF:
    //
    //   VALUE -- a proof, and not bypassable. JoinFlowCode lives in Core, is callable, and
    //   WhatTheJoinFieldAccepts CALLS it. Whatever the shared decision does, these tests see.
    //
    //   USE -- a TEXTUAL PROXY, and this test. Contains proves the sanctioned call is PRESENT.
    //   DoesNotContain bans ONE identifier. NEITHER SAYS THE SHARED DECISION IS THE ONLY ONE
    //   CONSULTED.
    //
    // THE DEFEAT, shown rather than described. Both assertions pass with this added and the
    // sanctioned call left untouched:
    //
    //     if (ImGui.Button("Join anyway") && _codeEntry.Replace("-", "").Length == 6)
    //     {
    //         _coordinator.RequestJoin(SessionCode.FromValid(...), DisplayName.OrNone(_nameEntry));
    //     }
    //
    // It never names SessionCode.TryParse, so DoesNotContain is satisfied; the real call is still
    // there, so Contains is satisfied. The join field now accepts codes JoinFlowCode would refuse.
    // This is structurally BUG-66 -- sanctioned call present, second path added, all green.
    //
    // MEASURED AGAINST THE ASSERTIONS BELOW, every row executed rather than reasoned about:
    //     sanctioned call REPLACED by SessionCode.TryParse     -> CAUGHT      (1 failed)
    //     sanctioned call REPLACED by a hand-rolled check      -> CAUGHT      (1 failed)
    //     SECOND path that NAMES the banned identifier         -> CAUGHT      (1 failed)
    //     SECOND path, hand-rolled, banned identifier absent   -> NOT CAUGHT  (9 passed, 0 failed)
    //     SECOND path in ANOTHER window file entirely          -> NOT CAUGHT  (9 passed, 0 failed)
    //
    // THE LAST ROW IS A SECOND GAP AND IS MINE, not the one I was asked to declare: this reads
    // JoinFlowView.cs ALONE, so an acceptance path in any other window is invisible to it. Widening
    // to the directory does not fix it either -- it would only move the boundary out one file.
    //
    // NO FOURTH ASSERTION, DELIBERATELY. Banning every other parser call needs an exception list,
    // and an exception list is a denylist wearing an allowlist's name -- already considered and
    // rejected twice on this board. A REAL FIX ASSERTS OVER BEHAVIOUR OR OVER A PARSE: drive the
    // window and compare what it accepts against JoinFlowCode, or read the syntax tree and find
    // every call reaching RequestJoin. Both are larger than this file, so THE END-TO-END COVERAGE
    // IS THE IN-GAME CHECK and it is load-bearing rather than supplementary.
    //
    // AND WHAT DELETING THIS WOULD COST, which is the half that survives someone tidying up:
    // "it is only a proxy" reads as a case for removal right up until the regression has a name.
    // Delete it and the extraction silently reverts to a seam nobody uses -- the window keeping its
    // own parse while WhatTheJoinFieldAccepts calls Core and the two are never compared, which is
    // the exact state this chunk existed to end. Three of the five shapes above stop being caught.
    [Fact]
    public void TheJoinButtonReachesForTheSharedDecision()
    {
        var joinFlow = string.Join(
            "\n",
            File.ReadAllLines(WindowSources().Single(path => path.EndsWith("JoinFlowView.cs", StringComparison.Ordinal)))
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        Assert.Contains("JoinFlowCode.Accepts(", joinFlow, StringComparison.Ordinal);

        // And it must not have kept a second way in beside it, which is the shape the extraction
        // exists to prevent — a shared decision that one caller bypasses is not a shared decision.
        Assert.DoesNotContain("SessionCode.TryParse", joinFlow, StringComparison.Ordinal);
    }

    // Fails if: the copy carries display grouping the join field rejects. Over many codes rather
    // than one, because a single sample cannot distinguish "the grouping is accepted" from "this
    // code happened not to need stripping".
    [Fact]
    public void ACopiedCodePastesIntoTheJoinFieldUnaltered()
    {
        foreach (var original in Enumerable.Range(0, 200).Select(_ => SessionCodeGenerator.Next()))
        {
            var copied = WhatTheCopyActionProduces(original);

            Assert.True(
                WhatTheJoinFieldAccepts(copied, out var pasted),
                $"The join field rejected the copied code '{copied}'.");
            Assert.Equal(original, pasted);
        }
    }

    // Fails if: the join field accepts anything at all. Without this, the test above passes just as
    // happily against a TryParse that never rejects — and "the paste was accepted" would be a
    // property of the parser rather than a fact about the copy. Both spellings are ones a
    // reasonable copy action might have produced.
    [Theory]
    [InlineData("BKD 7RM")]                 // grouped with a space instead of a hyphen
    [InlineData("Session code: BKD-7RM")]   // the rendered line copied along with its label
    public void TheJoinFieldRejectsSpellingsACopyMightPlausiblyHaveProduced(string pasted)
    {
        Assert.False(WhatTheJoinFieldAccepts(pasted, out _));
    }

    // Recorded as acceptance rather than rejection, because it IS accepted: TryParse trims. A
    // clipboard that picked up a trailing newline or space still pastes, and stating that here
    // stops the theory above from reading as "anything unusual is refused".
    [Fact]
    public void SurroundingWhitespaceStillPastes()
    {
        Assert.True(WhatTheJoinFieldAccepts(" BKD-7RM\n", out var pasted));
        Assert.Equal(SessionCode.FromValid("BKD7RM"), pasted);
    }

    // Fails if: the hyphen stops being the separator on one side only. This is the specific drift
    // A-1.18 names — the copy would still "succeed" and the paste would stop working.
    [Fact]
    public void TheSeparatorTheCopyEmitsIsOneTheJoinFieldStrips()
    {
        var copied = WhatTheCopyActionProduces(SessionCode.FromValid("BKD7RM"));

        Assert.Equal("BKD-7RM", copied);
        Assert.True(WhatTheJoinFieldAccepts(copied, out _));
    }
}
