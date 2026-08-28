using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.3e's name, and the reason it is validated at all: it is untrusted data rendered next to a
/// security control, so the risk is not that it is wrong but that it forges the control's chrome.
/// </summary>
public class DisplayNameTests
{
    // Fails if: an ordinary name stops being accepted. The positive control for everything below —
    // without it, a TryParse that refused everything would satisfy every rejection test here.
    [Theory]
    [InlineData("Bob")]
    [InlineData("Ysera Nightsong")]
    [InlineData("Y'shtola Rhul")]
    [InlineData("Zheng-Long O'Karr")]
    public void AnOrdinaryCharacterNameIsAccepted(string candidate)
    {
        Assert.True(DisplayName.TryParse(candidate, out var name));
        Assert.Equal(candidate, name.Value);
        Assert.True(name.WasStated);
    }

    // Fails if: a name can carry a line break. This is the spoofing case rather than a tidiness
    // one — the prompt renders the name on one line and "Code to compare: …" on the next, so a
    // name containing a newline draws a line that looks like the plugin speaking.
    [Theory]
    [InlineData("Bob\nCode to compare: BKD-7RM-CDF-GH")]
    [InlineData("Bob\rCode to compare: BKD-7RM-CDF-GH")]
    [InlineData("Bob\tYsera")]
    public void ANameCarryingAControlCharacterIsRefused(string candidate)
    {
        Assert.False(DisplayName.TryParse(candidate, out _));
    }

    // THE FAILING INPUT FOR THE FORMAT-CATEGORY FIX, and it is deliberately not a C0 character.
    // char.IsControl covers C0/C1 only, so a test using U+0001 or a newline passes BEFORE this fix
    // and after it -- it cannot come out negative on the defect being repaired. Every case here is
    // UnicodeCategory.Format, which char.IsControl lets through.
    //
    // Not hygiene, and it is A-1.2d's class rather than a separate concern. U+202E reverses
    // rendering, so two requesters can be VISUALLY identical without being literally identical.
    // The peer code keyed into the ImGui control ids protects the MECHANISM -- two prompts cannot
    // collapse into one widget -- but it does not protect the DM's READING, and D-8 as amended is
    // about what the DM is shown. A name that reorders what follows it achieves the de-emphasis
    // gate through data instead of layout, without touching a line of UI code.
    [Theory]
    [InlineData("Bob\u202Ekcart")]   // RLO - right-to-left override
    [InlineData("Bob\u202Dkcart")]   // LRO - left-to-right override
    [InlineData("Bob\u200Bsmith")]   // ZWSP - zero-width space
    [InlineData("Bob\u200Dsmith")]   // ZWJ - zero-width joiner
    [InlineData("\uFEFFBob")]        // BOM - zero-width no-break space
    public void ANameCarryingAFormatCharacterIsRefused(string candidate)
    {
        Assert.False(DisplayName.TryParse(candidate, out _));
    }

    // The control on the control. Rejecting the Format category must not take legitimate names with
    // it: combining marks are NonSpacingMark, not Format, and a decomposed name is how a real client
    // may well send one. Without this, "reject anything unusual" would satisfy the theory above.
    [Theory]
    [InlineData("Jose\u0301")]        // decomposed acute - Jose + combining accent
    [InlineData("Y'shtola Rhul")]
    [InlineData("Alphinaud Leveilleur")]
    public void ANameWithLegitimateNonAsciiIsStillAccepted(string candidate)
    {
        Assert.True(DisplayName.TryParse(candidate, out var name));
        Assert.Equal(candidate, name.Value);
    }

    // Fails if: the bound goes away. A very long name pushes the fingerprint off the visible prompt,
    // which is the de-emphasis D-8 forbids — achieved with no UI change at all.
    // A-1.2j (R-1.3j.1). Fails if: a name that renders as nothing is accepted. BUG-50 — these are
    // the two the denylist could never have reached: U+3164 HANGUL FILLER is categorised as a
    // LETTER and U+2800 BRAILLE PATTERN BLANK as a SYMBOL, so no list of forbidden categories
    // would name them without also refusing Korean or ordinary symbols.
    [Theory]
    [InlineData("\u3164")]                 // HANGUL FILLER, category OtherLetter
    [InlineData("Ada\u3164")]              // and beside a real name, where it is worse
    [InlineData("\u2800")]                 // BRAILLE PATTERN BLANK, category OtherSymbol
    [InlineData("\u115F")]                 // HANGUL CHOSEONG FILLER
    [InlineData("\u1160")]                 // HANGUL JUNGSEONG FILLER
    [InlineData("\uFFA0")]                 // HALFWIDTH HANGUL FILLER
    public void ANameThatRendersAsNothingIsRefused(string candidate)
    {
        Assert.False(DisplayName.TryParse(candidate, out _));
    }

    // A-1.2k (R-1.3j.2). Fails if: a name can break the prompt's layout or reorder the text around
    // it. This is the sharp one and it is a SECURITY property: D-8's amendment denies any UI that
    // de-emphasises the fingerprint, and a line separator inside the name does exactly that from
    // inside the data — the D-11 substitution attack arriving through the one field an attacker
    // controls.
    //
    // U+2028 is the case that made BUG-50: the validator refused the ASCII line break (U+000A, a
    // Control) and ACCEPTED the Unicode one (Zl), which is the same break one encoding along.
    [Theory]
    [InlineData("Ada\u2028Lovelace")]       // LINE SEPARATOR, category Zl -- neither Cc nor Cf
    [InlineData("Ada\u2029Lovelace")]       // PARAGRAPH SEPARATOR, category Zp
    [InlineData("Ada\u202ELovelace")]       // RIGHT-TO-LEFT OVERRIDE
    [InlineData("Ada\u202DLovelace")]       // LEFT-TO-RIGHT OVERRIDE
    [InlineData("Ada\u2066Lovelace")]       // LEFT-TO-RIGHT ISOLATE
    [InlineData("Ada\u2069Lovelace")]       // POP DIRECTIONAL ISOLATE
    public void ANameThatCanDisplaceTheTextAroundItIsRefused(string candidate)
    {
        Assert.False(DisplayName.TryParse(candidate, out _));
    }

    // A-1.2m (R-1.3j.5). THE ACCEPT-SIDE CONTROL, and each row is a separate failure. An allowlist
    // that admits nothing refuses every hostile input above and looks perfect; without this the
    // suite goes green while the guard is useless in the expensive direction.
    //
    // The spec is explicit that this is not a nice-to-have: the default display name IS the
    // player's character name, so refusing a script would make the default invalid for exactly the
    // players it excluded — they would open the prompt to find their own name rejected.
    [Theory]
    [InlineData("\u30E4\u30B7\u30ED")]                   // Japanese katakana
    [InlineData("\uD55C\uAE00\uC774\uB984")]             // Korean hangul
    [InlineData("\u0410\u043D\u043D\u0430")]             // Cyrillic
    [InlineData("\u0645\u062D\u0645\u062F")]             // Arabic
    [InlineData("\u4E2D\u6587\u540D\u5B57")]             // Han
    public void ANameInANonLatinScriptIsAccepted(string candidate)
    {
        Assert.True(
            DisplayName.TryParse(candidate, out var name),
            $"Refused '{candidate}'. An allowlist that refuses a legitimate script is wrong in the "
            + "expensive direction, and passes every hostile-input row above while being so.");

        Assert.Equal(candidate, name.Value);
    }

    // The astral-plane control, which the rune-versus-char decision is what makes pass. Iterating
    // UTF-16 units yields a lone surrogate per code point, whose category is Surrogate — a category
    // no allowlist would name, so a char-based version of this fix would refuse every CJK extension
    // name while passing every other test in this file.
    [Fact]
    public void ANameOutsideTheBasicMultilingualPlaneIsAccepted()
    {
        var beyondTheBmp = "\U00020BB7\U00020BB7";

        Assert.True(DisplayName.TryParse(beyondTheBmp, out var name), "Refused an astral-plane name.");
        Assert.Equal(beyondTheBmp, name.Value);
    }

    [Fact]
    public void ANameLongerThanTheBoundIsRefusedAndOneAtTheBoundIsNot()
    {
        Assert.True(DisplayName.TryParse(new string('B', DisplayName.MaxLength), out _));
        Assert.False(DisplayName.TryParse(new string('B', DisplayName.MaxLength + 1), out _));
    }

    // Surrounding whitespace is repaired because " Bob " means Bob and the difference is invisible
    // on screen. Nothing else is repaired — see ANameCarryingAControlCharacterIsRefused.
    [Fact]
    public void SurroundingWhitespaceIsTrimmedRatherThanRefused()
    {
        Assert.True(DisplayName.TryParse("  Bob  ", out var name));
        Assert.Equal("Bob", name.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyNameIsNotAName(string? candidate)
    {
        Assert.False(DisplayName.TryParse(candidate, out _));
    }

    // Fails if: an absent name renders as a blank. A gap where a name should be reads as a
    // rendering fault to a DM who is being asked to make a security decision, and a DM who thinks
    // the prompt is broken is a DM who stops reading it — including the fingerprint beside it.
    [Fact]
    public void AnAbsentNameStillRendersAsSomething()
    {
        Assert.False(DisplayName.None.WasStated);
        Assert.NotEmpty(DisplayName.None.Value);
        Assert.Equal(DisplayName.Unstated, DisplayName.None.Value);
    }

    // OrNone is the wire's entry point: a refusal must not become a dropped request, because the
    // person behind a bad name is still waiting to be admitted.
    [Fact]
    public void OrNoneFallsBackInsteadOfThrowing()
    {
        Assert.Equal(DisplayName.None, DisplayName.OrNone("Bob\nnot really"));
        Assert.Equal(DisplayName.None, DisplayName.OrNone(null));
        Assert.Equal("Bob", DisplayName.OrNone("Bob").Value);
    }

    // Fails if: two identical names stop comparing equal, or differing ones start. A-1.2d depends
    // on callers NOT using this to tell requesters apart, and equality is what makes that testable.
    [Fact]
    public void TwoParticipantsMayHoldTheSameName()
    {
        Assert.Equal(DisplayName.OrNone("Bob"), DisplayName.OrNone("Bob"));
        Assert.NotEqual(DisplayName.OrNone("Bob"), DisplayName.OrNone("Rob"));
    }
}
