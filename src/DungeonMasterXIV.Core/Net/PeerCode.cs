using System;

namespace DungeonMasterXIV.Net;

public readonly struct PeerCode : IEquatable<PeerCode>
{
    private readonly string? _value;

    private PeerCode(string value) => _value = value;

    public string Value => _value ?? string.Empty;

    public bool IsPresent => _value is not null;

    public static bool TryParse(string? candidate, out PeerCode peerCode)
    {
        peerCode = default;

        if (candidate is null || candidate.Length != SessionCode.Length)
        {
            return false;
        }

        foreach (var character in candidate)
        {
            if (!SpeakableAlphabet.Characters.Contains(character, StringComparison.Ordinal))
            {
                return false;
            }
        }

        peerCode = new PeerCode(candidate);
        return true;
    }

    internal static PeerCode FromGenerated(string generated) =>
        TryParse(generated, out var peerCode)
            ? peerCode
            : throw new InvalidOperationException(
                $"PeerCodeFor produced '{generated}', which is not a valid peer code. The generator " +
                "and PeerCode.TryParse have diverged.");

    public bool Equals(PeerCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PeerCode other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public override string ToString() => Value;

    public static bool operator ==(PeerCode left, PeerCode right) => left.Equals(right);

    public static bool operator !=(PeerCode left, PeerCode right) => !left.Equals(right);
}
