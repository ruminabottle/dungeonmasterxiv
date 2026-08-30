using System;
using System.IO;
using System.Text.RegularExpressions;
using DungeonMasterXIV.Chat;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.1's invocation: <b>the product reaches <c>RollEvaluator</c></b>, and <c>/roll</c> is what
/// reaches it (A-2.33a).
/// </summary>
/// <remarks>
/// <para>
/// <b>DMXENG-84's tests already prove the evaluator EVALUATES. An absent caller has no failing
/// test</b>, which is exactly how it reached Pending Deployment as a leaf with zero production
/// references. So what is pinned here is that the product INVOKES it — a different claim, and one
/// no test of the evaluator can make.
/// </para>
/// <para>
/// <b>The <c>/r</c> half fails separately and is not a courtesy.</b> A-2.33a requires <c>/r</c> to
/// be claimed in no way — not a handler, not an alias, not a completion. <b><c>/r</c> is REPLY in
/// FFXIV</b>, so claiming it sends a player's tell somewhere else, possibly in front of the table.
/// A prefix match on <c>/r</c> would pass every roll test here and fail that one.
/// </para>
/// <para>
/// <b>THIS FILE DELIBERATELY ADDS ONE INSTANCE TO DMXENG-122's POPULATION.</b> Its source-reading
/// helper is re-implemented rather than shared, because 122's own summary puts helper migration in a
/// LATER ticket and extracting one here would settle that ticket's option by building it. Named
/// rather than done quietly, so the census counts it.
/// </para>
/// </remarks>
public class TheProductInvokesTheRollEvaluatorTests
{
    // ---- A-2.33a, first half: /roll invokes.

    [Theory]
    [InlineData("/roll 1d20", "1d20")]
    [InlineData("/roll 4d6dl1", "4d6dl1")]
    [InlineData("  /roll 2+2  ", "2+2")]
    [InlineData("/roll", "")]
    public void TheRollTokenIsRecognisedAndYieldsTheExpression(string typed, string expected)
    {
        Assert.True(RollCommand.TryRead(typed, out var expression));
        Assert.Equal(expected, expression);
    }

    // ---- A-2.33a, second half: /r claims NOTHING. Fails separately, by design.

    [Theory]
    [InlineData("/r")]
    [InlineData("/r hello")]
    [InlineData("/r 1d20")]
    public void TheReplyCommandIsClaimedInNoWay(string typed)
    {
        // /r is REPLY in FFXIV. A prefix match would claim it and pass every test above.
        Assert.False(RollCommand.TryRead(typed, out _));
    }

    [Theory]
    [InlineData("/rolling my eyes")]
    [InlineData("/rollback")]
    [InlineData("/rolls")]
    public void ALongerWordBeginningWithTheTokenIsNotTheToken(string typed)
    {
        Assert.False(RollCommand.TryRead(typed, out _));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData(null)]
    public void OrdinaryTextIsNotARollCommand(string? typed)
    {
        Assert.False(RollCommand.TryRead(typed, out _));
    }

    // ---- The invocation itself, in the product rather than in a test.

    [Fact]
    public void TheComposeSurfaceCallsTheEvaluator()
    {
        var source = CodeOf("Windows", "MessageComposeView.cs");

        Assert.Contains("RollCommand.TryRead(", source, StringComparison.Ordinal);
        Assert.Contains(".Evaluate(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheResultCarriesItsNoticeAndNotOnlyItsTotal()
    {
        // A-2.3b: RollOutcome computes the notice centrally so no call site can drop every die and
        // forget to say so. A display showing the total alone puts that defect straight back, and
        // the number would read wrongly rather than look wrong.
        var source = CodeOf("Windows", "MessageComposeView.cs");

        Assert.Contains("outcome.Notice", source, StringComparison.Ordinal);
        Assert.Contains("outcome.Message", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheScanActuallyReadsTheFileItClaimsTo()
    {
        Assert.Contains("class MessageComposeView", CodeOf("Windows", "MessageComposeView.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheScanIgnoresCommentsAndWouldNotPassOnProseAlone()
    {
        // This file's subject documents its own reasoning in `///` lines that name RollCommand and
        // the evaluator. Without stripping, deleting the wiring leaves the words standing and the
        // assertions above go green on documentation.
        var stripped = CodeOf("Windows", "MessageComposeView.cs");
        var raw = File.ReadAllText(Path.Combine(RepositoryRoot(), "Windows", "MessageComposeView.cs"));

        Assert.Contains("A-2.33a", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("A-2.33a", stripped, StringComparison.Ordinal);
    }

    private static string CodeOf(params string[] parts)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));
        var withoutBlocks = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return Regex.Replace(withoutBlocks, @"//[^\n]*", string.Empty);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DungeonMasterXIV.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
