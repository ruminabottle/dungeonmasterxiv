using System;
using System.Globalization;
using System.Text;

namespace DungeonMasterXIV.Net;

/// <summary>
/// A participant's self-declared display name (R-1.3e).
/// </summary>
/// <remarks>
/// <para>
/// <b>It never authenticates, and this type exists partly to keep saying so.</b> D-8's 2026-08-27
/// amendment permits showing a name and forbids acting on one: it is self-declared, unverified and
/// trivially spoofable, so the <see cref="KeyFingerprint"/> remains the security-bearing element.
/// Any UI that shows a name while omitting or de-emphasising the fingerprint is denied — that is
/// D-11's substitution attack returning through a friendly label.
/// </para>
/// <para>
/// <b>Why it is validated at all, given that it is untrusted anyway.</b> Not to make it
/// trustworthy — it cannot be. To stop a name from forging the UI around it. The admission prompt
/// renders the name <b>immediately above</b> <c>"Code to compare: …"</c>, so a name containing a
/// newline could draw a line that looks like the plugin speaking. <b>The refusal does not depend on
/// that adjacency</b> — a forged line reads as the plugin from anywhere in the prompt — but adjacency
/// is what puts it in the reader's eye beside the value it is imitating. Stated as a RELATION rather
/// than a distance on purpose: the earlier wording said "two lines below", which was a line number
/// wearing a disguise. It carried no digits, so no sweep for stale line numbers could find it, and it
/// had gone stale (BUG-81). A name is data
/// rendered next to a security control, which makes control characters a spoofing surface rather
/// than a tidiness problem. Length is bounded for the same reason: a very long name pushes the
/// fingerprint off the visible prompt, which is the de-emphasis D-8 forbids, achieved without any
/// UI change.
/// </para>
/// <para>
/// <b>The <see cref="UnicodeCategory.Format"/> class is refused for the same reason and it is not a
/// second rule.</b> <c>char.IsControl</c> is C0/C1 only, so RLO, LRO, ZWSP, ZWJ and the BOM pass it
/// — and a directional override reverses rendering, which reaches the same D-8 gate through data
/// instead of layout. <b>It also refines A-1.2d rather than sitting beside it:</b> the peer code
/// keyed into the prompt's control ids stops two identical names collapsing into one widget, but
/// that protects the MECHANISM. Two names that are literally different and <i>render</i> identically
/// defeat the DM's READING while every id stays distinct, and what the DM is shown is what D-8 is
/// about.
/// </para>
/// <para>
/// <b>Barred from exports (A-1.2a, D-8 unchanged).</b> Names are campaign-scoped. Nothing here
/// serialises, and nothing that writes an export may reach for one.
/// </para>
/// <para>
/// <b>Duplicates are expected, not exceptional.</b> Nothing prevents or verifies two participants
/// choosing the same name (A-1.2d), so no caller may treat this as an identifier. It is a label.
/// </para>
/// </remarks>
public readonly struct DisplayName : IEquatable<DisplayName>
{
    /// <summary>
    /// Longest name accepted, counted in GRAPHEME CLUSTERS — what a reader would call characters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Grapheme clusters, not <c>string.Length</c> (R-1.3j.3).</b> <c>string.Length</c> counts
    /// UTF-16 code units, so it charges two for anything outside the BMP and one per combining mark
    /// — a Devanagari or Vietnamese name is refused while a Japanese name that looks exactly as long
    /// is accepted, because CJK is BMP and costs one. The limit was silently a different limit for
    /// different scripts.
    /// </para>
    /// <para>
    /// <b>THE PROPERTY OUTRANKS THE NUMBER: the limit must never reject a name FFXIV itself
    /// permits.</b> The product PRE-FILLS the player's character name (A-1.2g), so a bound below the
    /// game's own maximum makes our default invalid on arrival — and it would hit the player with the
    /// longest name, who did nothing wrong. 32 is the current answer to that property, not the rule.
    /// </para>
    /// <para>
    /// <b>And 32 rests on an UNVERIFIED fact.</b> It is believed to clear FFXIV's maximum of
    /// 15 + 15 + a space = 31, but <b>the repository records the game's limit nowhere</b> and nobody
    /// has checked the game. Do not treat 31 as confirmed because it is written down; if something
    /// comes to depend on the exact number rather than on the property above, that dependency needs
    /// the fact settled first.
    /// </para>
    /// </remarks>
    public const int MaxLength = 32;

    /// <summary>
    /// How many characters <paramref name="value"/> has as a reader would count them.
    /// </summary>
    /// <param name="value">The text to measure.</param>
    /// <remarks>
    /// <b>Grapheme clusters via <see cref="StringInfo"/>, because that is the unit the rule is
    /// written in.</b> <c>string.Length</c> counts UTF-16 code units and
    /// <c>EnumerateRunes().Count()</c> counts code points — the second is closer and still wrong,
    /// because a base letter plus a combining mark is ONE character to the person who typed it and
    /// two runes. Anything that measures storage rather than perception makes the bound depend on
    /// the writing system.
    /// </remarks>
    internal static int PerceivedLength(string value) => new StringInfo(value).LengthInTextElements;

    /// <summary>
    /// How many BYTES a text buffer needs to hold a name this type accepts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ImGui input buffers are sized in BYTES, and <see cref="MaxLength"/> counts CHARACTERS.</b>
    /// Sizing one from the other treated a character as a byte, which was true only for ASCII. A
    /// 32-character Devanagari name is <b>192 UTF-8 bytes</b> and the buffer was 33 — so the field
    /// would have truncated a name this type accepts, giving a build that ACCEPTS a name and a UI
    /// that CANNOT HOLD IT.
    /// </para>
    /// <para>
    /// <b>Eight bytes per character, which is generous rather than exact.</b> The heaviest realistic
    /// script here is Devanagari at about six; Latin with combining marks runs to five. <b>There is
    /// no exact answer to compute:</b> a grapheme cluster may carry arbitrarily many marks, and marks
    /// are permitted (A-1.2i needs them), so no finite buffer holds every string this type would
    /// accept.
    /// </para>
    /// <para>
    /// <b>Which is why the buffer is not the rule.</b> It is UI capacity; <see cref="TryParse"/> is
    /// the gate.
    /// </para>
    /// <para>
    /// <b>A paragraph here used to argue that running out of room was harmless</b> — that a
    /// pathological name is truncated in the field, that what the user is left looking at is what
    /// gets validated, and that the two therefore never disagree. <b>It is struck rather than
    /// reworded, because it was not a clumsy sentence: it was a considered position, and A-1.2v
    /// decided against it.</b> A field that stops accepting keystrokes with no explanation fails the
    /// criterion (BUG-92), and the reasoning was wrong in a way worth keeping visible — it took
    /// "the user sees what gets validated" as the property that mattered, when the property that
    /// matters is whether <b>the user can tell that anything happened at all</b>.
    /// </para>
    /// <para>
    /// <b>Running out of room is now SAID, and <see cref="NameInputCapacity"/> is what says it.</b>
    /// </para>
    /// </remarks>
    public const int MaxUtf8Bytes = (MaxLength * 8) + 1;

    /// <summary>
    /// What the prompt shows when a client sent no usable name — an older build, or a name that was
    /// rejected. <b>Never blank</b>: an empty label beside a fingerprint reads as a rendering fault
    /// and invites the DM to look past it.
    /// </summary>
    public const string Unstated = "a player who gave no name";

    private readonly string? _value;

    private DisplayName(string value) => _value = value;

    /// <summary>The name as it is shown. Never null, never empty, never multi-line.</summary>
    public string Value => _value ?? Unstated;

    /// <summary>Whether a usable name was actually supplied by the far side.</summary>
    public bool WasStated => _value is not null;

    /// <summary>The absent name, shown as <see cref="Unstated"/>.</summary>
    public static DisplayName None => default;

    /// <summary>
    /// Accepts <paramref name="candidate"/> if it can be shown safely beside a fingerprint.
    /// </summary>
    /// <remarks>
    /// Surrounding whitespace is trimmed, because a client sending <c>" Bob "</c> means Bob and the
    /// difference is invisible on screen. Nothing else is repaired: a name carrying a control
    /// character is REFUSED rather than stripped, because stripping silently changes what the DM is
    /// shown from what the joiner sent, and those two must be the same string.
    /// </remarks>
    public static bool TryParse(string? candidate, out DisplayName name)
    {
        name = default;

        if (candidate is null)
        {
            return false;
        }

        var trimmed = candidate.Trim();
        if (trimmed.Length == 0 || PerceivedLength(trimmed) > MaxLength)
        {
            return false;
        }

        var rendersSomething = false;

        // RUNES, not chars. Iterating UTF-16 units gives a lone surrogate for every astral-plane
        // code point, whose category is Surrogate -- which no allowlist would name, so CJK
        // extension names would be refused wholesale. That is the expensive direction A-1.2i and
        // A-1.2m exist to catch, and a denylist never had to think about it.
        foreach (var rune in trimmed.EnumerateRunes())
        {
            if (!IsPermitted(rune))
            {
                return false;
            }

            rendersSomething |= HasAGlyphOfItsOwn(rune);
        }

        // R-1.3j.1. Marks and spaces are permitted BESIDE something, never as the whole name: a
        // name made only of combining marks passes every category check and renders as nothing,
        // which leaves R-1.3e's prompt blank where a name is required.
        if (!rendersSomething)
        {
            return false;
        }

        name = new DisplayName(trimmed);
        return true;
    }

    /// <summary>
    /// The name to show for <paramref name="candidate"/>, falling back to <see cref="None"/>.
    /// </summary>
    /// <remarks>
    /// The receiving side of the wire, where a refusal must not drop the request: a joiner running
    /// an older build sends no name at all, and one sending a bad name is still a person waiting to
    /// be admitted. Either way the DM gets a prompt with the fingerprint in it.
    /// </remarks>
    public static DisplayName OrNone(string? candidate) =>
        TryParse(candidate, out var name) ? name : None;

    /// <summary>
    /// Whether <paramref name="rune"/> is one a display name may contain (R-1.3j).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An ALLOWLIST, and that shape is the fix rather than a detail.</b> This was
    /// <c>char.IsControl</c>, then <c>+ UnicodeCategory.Format</c>, and BUG-50 was a request for a
    /// third category. <c>U+2028 LINE SEPARATOR</c> is <c>Zl</c> and <c>U+2029</c> is <c>Zp</c> —
    /// neither Control nor Format — so the validator refused the ASCII line break and accepted the
    /// Unicode one, which is the attack the ASCII rule exists to stop. <b>A denylist over Unicode
    /// cannot be completed</b>; the categories nobody has thought of are refused here by default.
    /// </para>
    /// <para>
    /// This is C18's argument, already made in this repository for the TLS fence:
    /// <i>"naming what is forbidden goes stale the first time somebody adds a project … naming what
    /// is permitted means a project added tomorrow is scanned by default."</i> It transfers exactly.
    /// </para>
    /// <para>
    /// <b>Every script, deliberately (R-1.3j.5, D-8 clause of 2026-08-28).</b> The allowed letter
    /// categories are script-blind, so Japanese, Korean, Cyrillic and Arabic pass. Restricting
    /// script would make the DEFAULT invalid for the players it excluded — the default is the
    /// character name — and the organising line is
    /// <i>restrict what can attack the display; never restrict what language a person speaks.</i>
    /// </para>
    /// </remarks>
    private static bool IsPermitted(Rune rune) => Rune.GetUnicodeCategory(rune) switch
    {
        // Letters of any script, and the marks that compose them. Decomposed forms are ordinary in
        // real names, so refusing marks would refuse "Jose" + combining acute (A-1.2i).
        UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.DecimalDigitNumber => !IsInvisibleDespiteItsCategory(rune),

        // Everything else by explicit code point, kept deliberately short. Whole punctuation
        // categories would readmit the class this replaces: Po alone carries the Arabic and Hebrew
        // marks that reorder text, which is R-1.3j.2's attack.
        _ => rune.Value is Space or Apostrophe or TypographicApostrophe or Hyphen or FullStop,
    };

    /// <summary>
    /// Code points that pass the category test and still render as nothing (R-1.3j.1).
    /// </summary>
    /// <remarks>
    /// <b>The allowlist alone does not reach these, and that is why they are named.</b>
    /// <c>U+3164 HANGUL FILLER</c> is categorised <c>OtherLetter</c> — a letter — so no category
    /// rule that admits Korean can exclude it. <c>U+2800 BRAILLE PATTERN BLANK</c> needs no entry
    /// here: it is <c>OtherSymbol</c>, and symbols are not permitted, so the allowlist already
    /// refuses it. This list is short because it is the residue of one property that character
    /// class cannot express, not a denylist growing back.
    /// </remarks>
    private static bool IsInvisibleDespiteItsCategory(Rune rune) =>
        rune.Value is 0x115F or 0x1160 or 0x3164 or 0xFFA0;

    /// <summary>
    /// Whether this rune puts a glyph on the screen by itself, as opposed to composing with or
    /// spacing another (R-1.3j.1).
    /// </summary>
    private static bool HasAGlyphOfItsOwn(Rune rune) => Rune.GetUnicodeCategory(rune) switch
    {
        UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.DecimalDigitNumber => !IsInvisibleDespiteItsCategory(rune),
        _ => rune.Value is Apostrophe or TypographicApostrophe or Hyphen or FullStop,
    };

    private const int Space = 0x0020;
    private const int Apostrophe = 0x0027;
    private const int Hyphen = 0x002D;
    private const int FullStop = 0x002E;
    private const int TypographicApostrophe = 0x2019;


    /// <inheritdoc />
    public bool Equals(DisplayName other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DisplayName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(DisplayName left, DisplayName right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(DisplayName left, DisplayName right) => !left.Equals(right);
}
