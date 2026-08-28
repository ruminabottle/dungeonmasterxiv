using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The joiner's outbound side: asking to join, and the keys that asking creates (R-1.3, R-1.5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <see cref="SessionCoordinator"/> by DMXENG-31, and it is a PURE MOVE.</b> No
/// behaviour changes here, no criterion is claimed, and nothing was fixed on the way past. The seam
/// is the one the ticket names: <c>RequestJoin</c>'s overloads and the key-pair helper, cut before
/// six queued tickets add to a class that stood at 395 against a block of 400.
/// </para>
/// <para>
/// <b>The KEYS came with the sequence, and that is the seam rather than a widening.</b>
/// <see cref="Keys"/> and <see cref="SessionKey"/> are written by exactly two places — the request
/// below, and the drain on each tick. Leaving them on the coordinator would have meant this type
/// reaching back through setters to mutate state it is the only author of, which is a seam in name
/// and a dependency in fact.
/// </para>
/// <para>
/// <b><see cref="SessionCoordinator.RequestJoin(SessionCode, DisplayName, Guid?)"/> and its
/// siblings REMAIN CALLABLE with their current signatures</b>,
/// as thin forwarders. That fence is not about size: PR #75's A-1.12a table drives production
/// through those entry points and carries an approve-blocking gate, so moving them off the type
/// would break a table this split has no business touching.
/// </para>
/// <para>
/// <b>The key-pair helper did NOT come here, and was NOT duplicated</b> — hosting needs it too. It
/// is <see cref="SessionKeyPair"/>, reachable by both and owned by neither.
/// </para>
/// </remarks>
internal sealed class JoinRequester
{
    private readonly OutboundHandshake _handshake;
    private readonly SessionInterruption _interruption;
    private readonly JoinAttempt _join;
    private readonly Func<SessionKeyExchange> _newKeys;
    private readonly Action _synchronise;

    /// <param name="handshake">What actually puts the request on the wire.</param>
    /// <param name="interruption">Holds the seat; told when a deliberate re-ask releases it.</param>
    /// <param name="join">The join phase machine this drives.</param>
    /// <param name="newKeys">How a key pair is made (BUG-61).</param>
    /// <param name="synchronise">Brings the socket into line once the phase has moved.</param>
    public JoinRequester(
        OutboundHandshake handshake,
        SessionInterruption interruption,
        JoinAttempt join,
        Func<SessionKeyExchange> newKeys,
        Action synchronise)
    {
        _handshake = handshake;
        _interruption = interruption;
        _join = join;
        _newKeys = newKeys;
        _synchronise = synchronise;
    }

    /// <summary>This client's key pair when joining somebody else's session, or null.</summary>
    public SessionKeyExchange? Keys { get; private set; }

    /// <summary>
    /// The key this client derived on being admitted, or null. Present only once the host's key has
    /// arrived — which is why the acceptance has to carry it.
    /// </summary>
    public byte[]? SessionKey { get; internal set; }

    /// <summary>
    /// Requests to join <paramref name="code"/>, claiming a participant we believe is ours (R-1.5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A separate overload rather than a defaulted parameter, and that is the whole lesson of this
    /// change.</b> The claim reached the wire types and the host's resolver and never travelled,
    /// because <c>RelinkClaim relink = default</c> sat on three signatures: every caller omitted it,
    /// every call got <c>None</c>, and every relink branch took the not-a-relink path while the suite
    /// stayed green. A missing argument is a compile error; a defaulted one is silence.
    /// </para>
    /// <para>
    /// <b>Nothing here remembers the id between sessions.</b> Storing it is a retention decision and
    /// it belongs to whoever owns joiner-side persistence, not to making the path reachable.
    /// </para>
    /// </remarks>
    /// <param name="code">The session to ask to join.</param>
    /// <param name="name">What to call ourselves. Never authenticates.</param>
    /// <param name="claimedParticipantId">The participant we claim, or null for an ordinary join.</param>
    public void Request(SessionCode code, DisplayName name, Guid? claimedParticipantId)
    {
        _handshake.JoiningAs(name, claimedParticipantId);

        // R-1.5a: a deliberate quit removes the seat immediately, and asking to join again is that.
        // Without this the suppression would outlive the intent that justified it.
        _interruption.SeatReleased();
        Keys?.Dispose();
        Keys = null;
        SessionKey = null;

        // The same guard, because joining fails identically to hosting: both make a key pair, which
        // is why an affected machine has nothing left that works (BUG-61).
        if (!SessionKeyPair.TryMake(_newKeys, out var joinerKeys))
        {
            _join.Fail(SessionFailure.SessionKeysUnavailable);
            return;
        }

        Keys = joinerKeys;
        _join.Request(code);

        // Cleared so asking again for the SAME code re-sends. R-1.3c makes that the ordinary case —
        // a lapse means the DM was mid-encounter, not that they refused — and the host's equivalent
        // never needs it because R-1.2a regenerates a fresh code on every refusal.
        _handshake.ForgetJoinRequest();
        _synchronise();
    }
}
