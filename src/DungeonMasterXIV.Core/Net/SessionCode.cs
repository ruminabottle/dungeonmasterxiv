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
    /// The 24 permitted characters. Vowels are absent so a code cannot spell a word, which is what
    /// removes the need for a profanity filter; L, S, Z, Q, 0, 1 and 5 are absent because they are
    /// confusable read aloud or written down. R-1.2a states both reasons.
    /// </summary>
    public const string Alphabet = "BCDFGHJKMNPRTVWXY2346789";

    /// <summary>Characters per code. R-1.2a forbids lengthening this to improve guess resistance.</summary>
    public const int Length = 6;

    /// <summary>Characters per displayed group, as in <c>BKD-7RM</c>.</summary>
    public const int GroupSize = 3;

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
    public string ToDisplayString() => $"{Value[..GroupSize]}-{Value[GroupSize..]}";

    /// <inheritdoc />
    public bool Equals(SessionCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SessionCode other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    /// <inheritdoc />
    public override string ToString() => ToDisplayString();
}
