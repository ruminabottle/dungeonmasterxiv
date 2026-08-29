using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.2v (BUG-92): a name field that has run out of room says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this was written against.</b> The name boxes accepted keystrokes up to a byte
/// ceiling and then stopped, silently. A user entering a long name — Devanagari, or Latin carrying
/// combining marks — got no response and no explanation. A-1.2v forbids exactly that: a name refused
/// for length is refused VISIBLY, and the criterion binds every layer that can discard what was
/// typed, not only the parse.
/// </para>
/// <para>
/// <b>THE EXPECTED VALUES BELOW ARE LITERAL BYTE COUNTS, NOT ARITHMETIC OVER THE CONSTANT.</b>
/// A-1.2u-oracle: a boundary test must not read its expected value from the constant it is checking,
/// because an expectation computed from the implementation agrees with it at every value — including
/// a wrong one. So <c>252</c> and <c>253</c> are written out. If <c>MaxUtf8Bytes</c> moves, these
/// fail, and that is the instrument working: the numbers are a second opinion, and a second opinion
/// derived from the first is not one.
/// </para>
/// <para>
/// <b>Both directions, because the DM asked for both and they fail differently.</b> A check that
/// never fires leaves the silence in place. A check that always fires is a warning beside every
/// name, which is the same silence one layer over — nothing it says can be news.
/// </para>
/// </remarks>
public class TheNameFieldSaysWhenItIsFullTests
{
    /// <summary>One character, four code units, seven UTF-8 bytes: 'e' with three combining marks.</summary>
    private const string OneCharacterWithThreeMarks = "è́̂";

    /// <summary>One character, five code units, nine UTF-8 bytes: 'e' with four combining marks.</summary>
    private const string OneCharacterWithFourMarks = "è́̂̃";

    // THE CRITERION, at the boundary. 253 bytes leaves fewer than one code point of headroom in the
    // shipped 257-byte buffer, so nothing more is guaranteed to fit and the user must be told.
    [Fact]
    public void AFieldWithNoRoomLeftIsFull()
    {
        var atTheCeiling = new string('B', 253);

        Assert.Equal(253, Encoding.UTF8.GetByteCount(atTheCeiling));
        Assert.True(NameInputCapacity.IsFull(atTheCeiling), "No room remains, so the user must be told.");
    }

    // THE OTHER DIRECTION, and it is not symmetry for its own sake: a check that always fires puts a
    // warning beside every name, which tells the user nothing and restores the silence it replaced.
    [Fact]
    public void AFieldWithRoomLeftIsNotFull()
    {
        var oneByteShort = new string('B', 252);

        Assert.Equal(252, Encoding.UTF8.GetByteCount(oneByteShort));
        Assert.False(NameInputCapacity.IsFull(oneByteShort), "Room remains, so there is nothing to say.");
        Assert.False(NameInputCapacity.IsFull("Bob"), "An ordinary name must draw no warning at all.");
        Assert.False(NameInputCapacity.IsFull(string.Empty));
    }

    // THE CASE THE BUG IS ACTUALLY ABOUT, and it is the pair that matters: both of these are names
    // the product ACCEPTS — full length, valid, indistinguishable to the user — and only one fits.
    // Nothing in DisplayName can tell them apart, because the difference is bytes and the rule is
    // characters.
    [Fact]
    public void TwoNamesOfTheSameLegalLengthDifferOnlyInWhetherTheBoxCanHoldThem()
    {
        var fits = Repeat(OneCharacterWithThreeMarks, DisplayName.MaxLength);
        var doesNot = Repeat(OneCharacterWithFourMarks, DisplayName.MaxLength);

        Assert.Equal(DisplayName.MaxLength, new StringInfo(fits).LengthInTextElements);
        Assert.Equal(DisplayName.MaxLength, new StringInfo(doesNot).LengthInTextElements);
        Assert.True(DisplayName.TryParse(fits, out _), "A legal name, so the box must hold it silently.");
        Assert.True(DisplayName.TryParse(doesNot, out _), "Equally legal, and this is the one that overflows.");

        Assert.Equal(224, Encoding.UTF8.GetByteCount(fits));
        Assert.Equal(288, Encoding.UTF8.GetByteCount(doesNot));

        Assert.False(NameInputCapacity.IsFull(fits), "224 bytes fits, so no warning.");
        Assert.True(NameInputCapacity.IsFull(doesNot), "288 bytes cannot fit, and the user must be told.");
    }

    // A-1.2v-note, pinned as a test rather than left in prose: raising the buffer does not discharge
    // the criterion, because a legal name has NO finite byte ceiling. Whatever the buffer is, a name
    // exists that overflows it — so the check must exist however large the number gets.
    [Fact]
    public void ALegalNameCanExceedAnyBufferThisTypeCouldDeclare()
    {
        var absurd = Repeat("e" + new string('̀', 40), DisplayName.MaxLength);

        Assert.Equal(DisplayName.MaxLength, new StringInfo(absurd).LengthInTextElements);
        Assert.True(DisplayName.TryParse(absurd, out _), "Marks are permitted (A-1.2i), so this is a name.");
        Assert.True(Encoding.UTF8.GetByteCount(absurd) > DisplayName.MaxUtf8Bytes * 4);
        Assert.True(NameInputCapacity.IsFull(absurd));
    }

    // THE WIRING, AND IT IS A TEXTUAL PROXY. What it can say is that both windows name the check and
    // render something under it; it cannot say the message reaches a screen. The stronger claim needs
    // the plugin running in-game, which nothing here links. Stated so a green run is not read as more
    // than it is -- the same limit TheNameIsEditableInTheJoinFlowTests records for its own scan.
    //
    // BOTH SURFACES, because a joiner who never opens settings meets JoinFlowView and no other name
    // box. A fix present in ConfigWindow alone leaves the criterion unmet where most people type.
    [Theory]
    [InlineData("ConfigWindow.cs")]
    [InlineData("JoinFlowView.cs")]
    public void EveryWindowWithANameBoxAsksWhetherItIsFull(string window)
    {
        var source = WindowSource(window);

        Assert.Contains("NameInputCapacity.IsFull", source);
        Assert.Contains("NameFieldIsFull", source);
    }

    // The guard on the guard (BUG-48's shape): a scan over a path that does not resolve matches
    // nothing and goes green, so the read is asserted rather than assumed.
    [Fact]
    public void TheScanReadsRealFiles()
    {
        Assert.NotEmpty(WindowSource("ConfigWindow.cs"));
        Assert.NotEmpty(WindowSource("JoinFlowView.cs"));
    }

    private static string Repeat(string character, int times) =>
        string.Concat(Enumerable.Repeat(character, times));

    /// <summary>
    /// The window's source with comments stripped, so a sentence describing the control cannot
    /// stand in for the control.
    /// </summary>
    private static string WindowSource(string fileName)
    {
        var path = Path.Combine(ShippedCopyCorpus.WindowsDirectory(), fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{fileName} is not where the scan looks, so it would pass over nothing.", path);
        }

        return Regex.Replace(File.ReadAllText(path), @"//[^\n]*", string.Empty);
    }
}
