using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// A session code: six characters from a deliberately restricted alphabet, displayed in two groups
/// of three. Parameters and their justification are PRD-1 R-1.2a; the exclusions are the decision.
/// </summary>
/// <remarks>
/// This type validates and formats a code. It says nothing about whether a code is <i>free</i> —
/// that is relay-wide knowledge and no client can answer it. See <see cref="SessionCodeGenerator"/>.
/// </remarks>
public readonly struct SessionCode : IEquatable<SessionCode>
{
    /// <summary>
    /// The 24 permitted characters. Defined by <see cref="SpeakableAlphabet"/>, which session
    /// codes share with key fingerprints (R-1.3a) so the product has one alphabet rather than two
    /// copies that must agree. Kept here as the name existing callers already use.
    /// </summary>
    public const string Alphabet = SpeakableAlphabet.Characters;

    /// <summary>Characters per code. R-1.2a forbids lengthening this to improve guess resistance.</summary>
    public const int Length = 6;

    /// <summary>Characters per displayed group, as in <c>BKD-7RM</c>. Shared — see <see cref="Alphabet"/>.</summary>
    public const int GroupSize = SpeakableAlphabet.GroupSize;

    private readonly string? _value;

    private SessionCode(string value) => _value = value;

    /// <summary>The six raw characters, unhyphenated and uppercase.</summary>
    public string Value => _value ?? throw new InvalidOperationException("Uninitialised SessionCode.");

    /// <summary>
    /// Wraps characters already known to be valid. Throws rather than returning false, because a
    /// caller reaching this with an invalid code has a bug rather than bad input.
    /// </summary>
    public static SessionCode FromValid(string value) =>
        TryParse(value, out var code)
            ? code
            : throw new ArgumentException($"Not a valid session code: '{value}'.", nameof(value));

    /// <summary>
    /// Parses a code a human may have typed: hyphens optional, case ignored. Everything else is
    /// rejected — wrong length, or any character outside <see cref="Alphabet"/>.
    /// </summary>
    public static bool TryParse(string? candidate, out SessionCode code)
    {
        code = default;
        if (candidate is null)
        {
            return false;
        }

        var raw = candidate.Replace("-", string.Empty).Trim().ToUpperInvariant();
        if (raw.Length != Length)
        {
            return false;
        }

        foreach (var character in raw)
        {
            if (!Alphabet.Contains(character))
            {
                return false;
            }
        }

        code = new SessionCode(raw);
        return true;
    }

    /// <summary>Renders the code the way it is read aloud and shown in the UI: <c>BKD-7RM</c>.</summary>
    public string ToDisplayString() => SpeakableAlphabet.Group(Value);

    /// <summary>
    /// The form that goes on the clipboard, which the join field must accept verbatim (R-1.3i).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Named separately from <see cref="ToDisplayString"/> although it currently returns the same
    /// text, because they answer different questions.</b> Display exists so the code can be read
    /// aloud (R-1.2a); this exists so a recipient can paste it. They agree today, and nothing said
    /// so — the copy button reached for the display form and it worked. A change made for reading
    /// aloud would have silently changed what gets pasted, and A-1.18 is precisely the criterion
    /// about those two drifting apart while each looks correct alone.
    /// </para>
    /// <para>
    /// <b>Load-bearing and now stated: this pastes only because <see cref="TryParse"/> strips
    /// hyphens.</b> The grouping is not neutral — it is accepted by leniency at the other end. That
    /// dependency was invisible in both files before this member existed; tighten <c>TryParse</c>
    /// and the copy breaks. <c>SessionCodeTests.ADisplayedCodeParsesBackToTheSameCode</c> is what
    /// holds the two ends together.
    /// </para>
    /// <para>
    /// <b>What is copied is deliberately unchanged.</b> Returning the raw six characters would make
    /// A-1.18 true by construction rather than by leniency, but it changes what a user sees on their
    /// clipboard — a product decision, and not this chunk's to take.
    /// </para>
    /// </remarks>
    public string ToClipboardString() => ToDisplayString();

    /// <inheritdoc />
    public bool Equals(SessionCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SessionCode other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    /// <inheritdoc />
    public override string ToString() => ToDisplayString();
}
