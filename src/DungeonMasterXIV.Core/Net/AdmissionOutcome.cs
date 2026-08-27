using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// How an admission request ended. A closed set of three, and consumers must handle all three.
/// </summary>
/// <remarks>
/// <para>
/// <b>Denied and Lapsed are distinct, and the distinction is behavioural rather than cosmetic.</b> A
/// lapsed request means nobody looked — the DM may have been mid-encounter — so the player may
/// sensibly ask again, and R-1.3c says to tell them so. A denial means someone looked and said no,
/// and inviting a retry into a refusal is worse than saying nothing. Collapsing them either
/// encourages a player to knock again on a closed door, or tells someone they were refused when in
/// fact nobody was looking.
/// </para>
/// <para>
/// <see cref="Match{T}"/> exists so a consumer <b>cannot</b> silently drop a case: omitting one is a
/// compile error rather than a branch that quietly does nothing. A <c>switch</c> would only warn.
/// </para>
/// <para>
/// Note the deliberate asymmetry with D-14. The <i>wire</i> tolerates what it does not recognise —
/// an unknown <see cref="WireMessageType"/> decodes to <see cref="WireMessageType.Unknown"/> and is
/// ignored, so an old client survives a newer relay. This <i>local</i> vocabulary is exhaustive
/// instead, because a consumer that has already decoded an outcome it knows about must not be able
/// to forget to handle it. Tolerant at the boundary, exhaustive inside it.
/// </para>
/// </remarks>
public abstract class AdmissionOutcome
{
    private AdmissionOutcome()
    {
    }

    /// <summary>The DM admitted the requester.</summary>
    public static AdmissionOutcome Accepted(byte[] hostPublicKey) => new AcceptedOutcome(hostPublicKey);

    /// <summary>The DM refused the requester. Somebody looked and said no.</summary>
    public static AdmissionOutcome Denied() => DeniedOutcome.Instance;

    /// <summary>The window closed with no answer. Nobody looked; asking again is reasonable.</summary>
    public static AdmissionOutcome Lapsed() => LapsedOutcome.Instance;

    /// <summary>
    /// Handles every outcome. Every parameter is required, so a consumer that adds no branch for a
    /// case does not compile.
    /// </summary>
    /// <param name="onAccepted">Given the host's public key, which the joiner needs to derive a key.</param>
    /// <param name="onDenied">Somebody refused.</param>
    /// <param name="onLapsed">Nobody answered.</param>
    public abstract T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed);

    private sealed class AcceptedOutcome : AdmissionOutcome
    {
        private readonly byte[] _hostPublicKey;

        public AcceptedOutcome(byte[] hostPublicKey) => _hostPublicKey = hostPublicKey;

        public override T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed) =>
            onAccepted(_hostPublicKey);
    }

    private sealed class DeniedOutcome : AdmissionOutcome
    {
        public static readonly DeniedOutcome Instance = new();

        public override T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed) =>
            onDenied();
    }

    private sealed class LapsedOutcome : AdmissionOutcome
    {
        public static readonly LapsedOutcome Instance = new();

        public override T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed) =>
            onLapsed();
    }
}
