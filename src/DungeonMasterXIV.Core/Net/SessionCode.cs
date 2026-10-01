using System;

namespace DungeonMasterXIV.Net;

public readonly struct SessionCode : IEquatable<SessionCode>
{
    public const string Alphabet = SpeakableAlphabet.Characters;

    public const int Length = 6;

    public const int GroupSize = SpeakableAlphabet.GroupSize;

    private readonly string? _value;

    private SessionCode(string value) => _value = value;

    public string Value => _value ?? throw new InvalidOperationException("Uninitialised SessionCode.");

    public static SessionCode FromValid(string value) =>
        TryParse(value, out var code)
            ? code
            : throw new ArgumentException($"Not a valid session code: '{value}'.", nameof(value));

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

    public string ToDisplayString() => SpeakableAlphabet.Group(Value);

    public string ToClipboardString() => ToDisplayString();

    public bool Equals(SessionCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SessionCode other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public override string ToString() => ToDisplayString();
}
