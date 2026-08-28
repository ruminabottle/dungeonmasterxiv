using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// A participant's session-scoped code — the value that identifies them on the DM's screen and in
/// the roster (R-1.3, D-8).
/// </summary>
/// <remarks>
/// <para>
/// <b>A validated type must be the only door.</b> This is the third time that rule has been applied
/// here — C19's tag into <c>ReleaseInputs</c>, then <see cref="DisplayName"/> on the content wire,
/// now this — and the first time the unvalidated value already had a consumer. BUG-57's hotfix vets
/// a peer code at <see cref="SessionContentCodec"/>, which is the right shape for a hotfix and the
/// wrong end state: a point-vet at one door leaves every other door open, and the code was a raw
/// <c>string</c> on the pending request, on the admitted peer and on the roster.
/// </para>
/// <para>
/// <b>It is an IDENTITY, and that is the whole reason this type differs from
/// <see cref="DisplayName"/>.</b> A display name is a label: two participants may hold the same one
/// (A-1.2d), nothing may key on it, and a bad one degrades to
/// <see cref="DisplayName.Unstated"/> so a display defect never becomes a membership one. A peer
/// code is what tells those two participants apart. So <b>there is deliberately no
/// <c>OrNone</c> here</b> — nothing may degrade an unusable code into a usable-looking one, because
/// that manufactures a participant rather than removing a forgery. The caller must handle the
/// refusal, which is why the only entry point is <see cref="TryParse"/>.
/// </para>
/// <para>
/// <b>That asymmetry is Breakfix-Engineer-1's, argued at <see cref="SessionContentCodec"/> when the
/// hotfix shipped, and this type carries it rather than re-deciding it.</b> The roster is
/// host-authored and sealed, so a malformed code means our own encoder is broken or a keyholder is
/// forging — and dropping is the safe answer to both.
/// </para>
/// <para>
/// <b>The shape, and deliberately not <see cref="SessionCode.TryParse"/>.</b> That method strips
/// hyphens, trims and upper-cases so a pasted code works, which is right for something a human types
/// and wrong here: it would accept <c>"PEE-R3"</c>, which <c>AdmissionControl.PeerCodeFor</c> never
/// emits, and accepting a code the product cannot have generated is the thing this exists to stop.
/// Nothing is repaired — a code is the shape it is or it is refused.
/// </para>
/// <para>
/// <b>The length and the alphabet are taken from <see cref="SessionCode"/> and
/// <see cref="SpeakableAlphabet"/> rather than restated</b>, so there is no second copy to drift.
/// A peer code and a session code are rendered in the same alphabet at the same length because
/// <c>PeerCodeFor</c> renders a digest in exactly that base.
/// </para>
/// <para>
/// <b>The residual hole, named rather than left implicit.</b> This is a <c>readonly struct</c>, so
/// <c>default(PeerCode)</c> exists and no parse gate can stop it being written. It is
/// <see cref="IsPresent"/><c> == false</c> and its <see cref="Value"/> is empty, so it can never
/// equal a real code and can never be mistaken for one — but a caller that defaults one has an
/// absent code, not a valid one, and must treat it as a refusal. This is the same concern that made
/// <see cref="AdmittedPeer"/> a <c>class</c>; the difference is that a peer code is a value with
/// equality semantics, where a struct is the right shape and the default is worth naming instead.
/// </para>
/// </remarks>
public readonly struct PeerCode : IEquatable<PeerCode>
{
    private readonly string? _value;

    private PeerCode(string value) => _value = value;

    /// <summary>
    /// The code as it is rendered and compared. <b>Empty when no code is present</b> — see the
    /// remarks on <c>default</c>; callers must check <see cref="IsPresent"/> rather than treating an
    /// empty string as a code.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Whether an actual code was supplied and passed <see cref="TryParse"/>.</summary>
    public bool IsPresent => _value is not null;

    /// <summary>
    /// Accepts <paramref name="candidate"/> if it is the shape <c>AdmissionControl.PeerCodeFor</c>
    /// actually produces: exactly <see cref="SessionCode.Length"/> characters, every one of them
    /// drawn from <see cref="SpeakableAlphabet.Characters"/>.
    /// </summary>
    /// <remarks>
    /// <b>There is no degrading counterpart to this method and there must not be one.</b> A caller
    /// that cannot parse a code has no participant to act on — see the remarks on this type.
    /// </remarks>
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

    /// <summary>
    /// The code for a value this product just generated, for <c>AdmissionControl.PeerCodeFor</c>.
    /// </summary>
    /// <remarks>
    /// <b>The generator's output goes through the same gate the wire's does, and that is the point
    /// rather than ceremony.</b> <c>PeerCodeFor</c> renders a digest in
    /// <see cref="SpeakableAlphabet"/> at <see cref="SessionCode.Length"/>, so its output is valid by
    /// construction — but "valid by construction" is a claim about code that can change, and if it
    /// ever stops being true this throws at the source instead of putting a code the product cannot
    /// have generated into a roster. A bypass here would be the fourth instance of the door this type
    /// exists to close.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The generator produced a code that is not valid.</exception>
    internal static PeerCode FromGenerated(string generated) =>
        TryParse(generated, out var peerCode)
            ? peerCode
            : throw new InvalidOperationException(
                $"PeerCodeFor produced '{generated}', which is not a valid peer code. The generator " +
                "and PeerCode.TryParse have diverged.");

    /// <inheritdoc />
    public bool Equals(PeerCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PeerCode other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(PeerCode left, PeerCode right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(PeerCode left, PeerCode right) => !left.Equals(right);
}
