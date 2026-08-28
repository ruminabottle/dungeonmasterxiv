using System;
using System.Globalization;

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
/// renders the name on one line and <c>"Code to compare: …"</c> two lines below, so a name
/// containing a newline could draw a line that looks like the plugin speaking. A name is data
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
    /// Longest name accepted. Comfortably above a full FFXIV character name, and bounded so a name
    /// cannot push the fingerprint out of the prompt.
    /// </summary>
    public const int MaxLength = 32;

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
        if (trimmed.Length == 0 || trimmed.Length > MaxLength)
        {
            return false;
        }

        foreach (var character in trimmed)
        {
            // BOTH, and the second is not a widening of the first. char.IsControl is C0/C1 only, so
            // the whole UnicodeCategory.Format class walks through it -- RLO, LRO, ZWSP, ZWJ, BOM.
            // Those are invisible by definition, which is exactly what makes them the dangerous
            // half: a reviewer reading the name cannot see one, and neither can the DM.
            if (char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format)
            {
                return false;
            }
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
