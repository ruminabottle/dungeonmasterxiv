using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.9l: the picker says what resuming does not restore.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE MERGE GATE for this PR, and it guards a PROPERTY rather than fixing one.</b> The picker
/// offers resumption of a campaign whose roster is empty and will stay empty until relink lands.
/// The disclosure is the only thing making that offer honest, so a later change that quietly removed
/// it would restore the defect with nothing to notice — the sentence is not covered by any
/// behavioural test, because nothing can execute a window.
/// </para>
/// <para>
/// <b>A SOURCE SCAN, and its limit is stated rather than implied.</b> No test project references the
/// plugin. This proves the call is PRESENT and reachable on the same path as the control; it cannot
/// prove a human reads it, and it does not look at the screen. The end-to-end check is in-game.
/// </para>
/// <para>
/// <b>It deliberately does NOT assert the WORDING.</b> The sentence is the Product Owner's and they
/// may revise it; pinning the string here would make their copy un-editable without a test change.
/// The guard protects PRESENCE, the criterion protects CONTENT — see
/// <see cref="ShippedCopyMeetsItsConstraintsTests"/> for where copy itself is held.
/// </para>
/// </remarks>
public class TheResumeOfferDisclosesWhatItCannotDoTests
{
    private static string PickerSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Windows", "HostCampaignPicker.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        throw new InvalidOperationException("No Windows/HostCampaignPicker.cs above the test binary.");
    }

    /// <summary>The picker's body, with comment lines removed.</summary>
    /// <remarks>
    /// Stripped because this file's own reasoning names the constant while explaining it, and a scan
    /// that counted prose would pass on an explanation of a call that no longer exists.
    /// </remarks>
    private static string Drawing() =>
        string.Join(
            "\n",
            PickerSource()
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal)));

    // The vacuity control: every assertion below is a substring match, so if the reader returned
    // nothing they would all fail loudly while nothing proved the right file was read. This names
    // something only this file contains.
    [Fact]
    public void TheReaderIsReadingThePicker()
    {
        Assert.Contains("class HostCampaignPicker", Drawing(), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePickerRendersTheDisclosure()
    {
        Assert.Contains("ImGui.TextWrapped(ResumeDisclosure)", Drawing(), StringComparison.Ordinal);
    }

    // ORDER ONLY, and that is now stated rather than implied (BUG-82). This establishes that the
    // disclosure comes after the nothing-to-resume return and before the control. IT DOES NOT
    // ESTABLISH THAT THE DISCLOSURE IS UNCONDITIONAL -- this comment used to claim it did, and
    // qa-2 wrote the mutation that proved otherwise: wrapping the call in `if (...)` moves it a few
    // characters and changes neither ordering, so all four tests passed. Nesting is what that needs
    // and it is asserted below, separately, so a failure names which of the two properties broke.
    [Fact]
    public void TheDisclosureIsDrawnWheneverThePickerIs()
    {
        var drawing = Drawing();

        var earlyReturn = drawing.IndexOf("return;", StringComparison.Ordinal);
        var disclosure = drawing.IndexOf("ImGui.TextWrapped(ResumeDisclosure)", StringComparison.Ordinal);
        var combo = drawing.IndexOf("ImGui.BeginCombo", StringComparison.Ordinal);

        Assert.True(earlyReturn >= 0 && disclosure >= 0 && combo >= 0, "The picker no longer has the shape this reads.");
        Assert.True(disclosure > earlyReturn, "The disclosure must come after the nothing-to-resume return, so a first run is unaffected.");
        Assert.True(disclosure < combo, "The disclosure must precede the control, not sit inside it — the belief forms on seeing the offer.");
    }

    // THE ASSERTION THAT MATTERS. The failure guarded is the disclosure surviving inside something
    // that stops running -- present in the file, absent from the screen.
    //
    // WHAT IT DECIDES, and this is the whole claim: THE STATEMENT IS NOT LEXICALLY GOVERNED BY A
    // CONDITION INSIDE Draw. Not "the disclosure always reaches the screen". A scan can see the
    // CALL SITE; it cannot see the EFFECT, and C# lets those come apart. That distinction is
    // qa-2's and it is why this comment states a smaller thing than its predecessor did.
    //
    // WRITTEN AS A WHITELIST, WHICH IS THE DESIGN (BUG-86). The previous version asked "is it
    // inside a block" -- a DENYLIST of one way to be conditional. C# has others: `if (c) stmt;`
    // needs no braces, and `#if` is resolved before a block is ever involved. Adding a case per
    // shape makes the list longer, not complete, which is an enumeration standing in for a
    // universal -- the defect this file has now had twice. So this asserts what an UNGOVERNED
    // STATEMENT LOOKS LIKE, and a shape nobody anticipated fails a positive test rather than
    // slipping past a missing negative:
    //
    //   1. at Draw's top statement level, so no block encloses it;
    //   2. the statement is the ENTIRE line, so no `if (c)`, ternary, `&&`, lambda or local
    //      function holds it;
    //   3. the line above completes a statement or opens a block, so no braceless
    //      `if`/`else`/`while`/`for`/`foreach`/`using`/`lock`/`do` governs it -- every such header
    //      ends in `)` or a keyword and never in `;`, `{` or `}`;
    //   4. preprocessor depth is zero, counted like brace depth: `#if` opens, `#endif` closes,
    //      `#else`/`#elif` change nothing because either arm is equally conditional.
    //
    // AN EARLY RETURN IS STILL NOT A VIOLATION, and rule 3 admits a line ending in `;` for that
    // reason: a `return` skips the CONTROL too, so "if the picker draws, the disclosure drew"
    // holds.
    //
    // WHAT IT CANNOT SEE, named rather than left to be discovered:
    //
    //   * WHETHER THE EFFECT HAPPENS. Nothing here proves Draw is CALLED, or that ImGui puts the
    //     result on screen. The in-game check is the end-to-end coverage and stays load-bearing.
    //   * A BUILD-TIME REMOVAL BEYOND THIS FILE. Rule 4 sees directives inside Draw; the artefact
    //     check is what asks the compiled output whether the sentence shipped, and that is the
    //     honest instrument for that question rather than more text matching.
    //   * INDIRECTION, IN PRINCIPLE. Every shape measured is caught -- a local function, an
    //     expression-bodied lambda, a block-bodied lambda with the call alone on its line, and a
    //     [Conditional] local function, by rules 2 and 1 -- but CAUGHT IN EVERY SHAPE TRIED IS NOT
    //     PROVEN IMPOSSIBLE, and this comment will not make that upgrade. The class this defends
    //     is the ACCIDENTAL edit: someone wrapping the disclosure in a condition, or dropping it
    //     through build configuration. A determined hostile edit wins eventually and a guard that
    //     claimed otherwise would be the same defect a fourth time.
    [Fact]
    public void NothingGovernsTheDisclosure()
    {
        const string Statement = "ImGui.TextWrapped(ResumeDisclosure);";

        var drawing = Drawing();
        var at = drawing.IndexOf(Statement, StringComparison.Ordinal);
        Assert.True(at >= 0, "The picker no longer has the shape this reads.");

        // 1. No block encloses it.
        Assert.Equal(TopLevelOfDraw, DepthInDraw(drawing, at));

        var lines = drawing.Split('\n');
        var line = drawing.Take(at).Count(c => c == '\n');

        // 2. The statement is the whole line: nothing shares it and nothing wraps it.
        Assert.Equal(Statement, lines[line].Trim());

        // 3. Nothing single-statement governs it from the line above.
        // Blank lines and PREPROCESSOR lines are skipped: a directive never governs the next
        // statement the way an `if` header does, and rule 4 is what judges directives. Without this
        // a bare #region above the disclosure reddens a disclosure that ships -- a false positive on
        // an organising edit, which is worse than the hole being closed.
        var above = Enumerable.Range(0, line).Reverse()
            .Select(i => lines[i].Trim())
            .FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#')) ?? "{";

        Assert.True(
            above.EndsWith(';') || above.EndsWith('{') || above.EndsWith('}'),
            $"The line above the disclosure is '{above}', which does not complete a statement or "
            + "open a block -- so a braceless conditional may govern the disclosure (BUG-86).");

        // 4. It is not inside a region the compiler can drop. DIRECTIVE DEPTH, counted the same way
        //    as brace depth: #if opens, #endif closes, and #else/#elif change nothing because a
        //    statement in EITHER arm is equally conditional. One uniform mechanism rather than a
        //    case per directive -- and #region and #pragma are ignored rather than refused, so
        //    organising the file does not redden a disclosure that still ships.
        var body = drawing.Take(drawing.IndexOf('{', drawing.IndexOf("public void Draw()", StringComparison.Ordinal)))
            .Count(c => c == '\n');

        var open = lines.Skip(body).Take(line - body)
            .Select(l => l.TrimStart())
            .Sum(l => l.StartsWith("#if", StringComparison.Ordinal) ? 1
                    : l.StartsWith("#endif", StringComparison.Ordinal) ? -1 : 0);

        Assert.True(
            open == 0,
            $"The disclosure sits inside {open} unclosed preprocessor conditional(s). The compiler "
            + "may drop the statement while this file still contains it, which is a green guard over "
            + "a binary with no sentence in it (BUG-86).");
    }

    // Directly inside Draw's body: one unclosed brace since the body opened.
    private const int TopLevelOfDraw = 1;

    /// <summary>Brace depth at <paramref name="index"/>, counted from the opening of <c>Draw</c>'s body.</summary>
    /// <remarks>
    /// <para>
    /// <b>Its limits, stated rather than implied.</b> It counts braces in the comment-stripped
    /// source, so a brace inside a string literal, a char literal, or a TRAILING comment would skew
    /// it. An interpolated hole is balanced and cancels out; a lone <c>"{"</c> would not. The
    /// failure direction is a wrong depth and therefore a false FAIL, which is the one somebody
    /// investigates.
    /// </para>
    /// <para>
    /// And it says nothing about whether <c>Draw</c> is called, or about what ImGui puts on screen.
    /// Those are the caller's business and the in-game check's respectively. This decides exactly
    /// one question, whether the statement is nested, and that question is genuinely textual.
    /// </para>
    /// </remarks>
    private static int DepthInDraw(string drawing, int index)
    {
        var method = drawing.IndexOf("public void Draw()", StringComparison.Ordinal);
        Assert.True(method >= 0 && method < index, "Draw() is not where this expects it.");

        var body = drawing.IndexOf('{', method);
        Assert.True(body >= 0 && body < index, "Draw() has no body before the statement being measured.");

        var depth = 0;

        for (var i = body; i < index; i++)
        {
            depth += drawing[i] switch { '{' => 1, '}' => -1, _ => 0 };
        }

        return depth;
    }

    // The sentence must actually BE a sentence. A constant emptied to silence a failing scan would
    // pass every assertion above while disclosing nothing.
    //
    // Read out of the SOURCE rather than referenced, because the test project cannot reference the
    // plugin — which is the same reason every assertion here is textual. Asserting its LENGTH and
    // not its WORDS is deliberate: the Product Owner may revise the sentence, and a guard that
    // pinned the string would make their copy un-editable without a test change.
    [Fact]
    public void TheDisclosureSaysSomething()
    {
        // Bounded at the statement's semicolon. An unbounded read runs on into the NEXT string
        // literal in the file and reports a constant that is longer than it is — which is how a
        // guard against an emptied constant would itself stop working.
        var body = Drawing()[Drawing().IndexOf("ResumeDisclosure =", StringComparison.Ordinal)..];
        var statement = body[..body.IndexOf(';', StringComparison.Ordinal)];

        var literal = string.Concat(
            Regex.Matches(statement, "\"([^\"]*)\"").Select(match => match.Groups[1].Value));

        Assert.False(string.IsNullOrWhiteSpace(literal), "ResumeDisclosure is empty.");
        Assert.True(literal.Length > 40, $"A one-word disclosure is not one. Got '{literal}'.");
    }
}
