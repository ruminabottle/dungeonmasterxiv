using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// A player's side of joining (R-1.3). Every state is one the UI can name, because R-1.3 requires
/// the player to know which one they are in rather than watching an ambiguous spinner.
/// </summary>
/// <remarks>
/// As with <see cref="HostSession"/>, elapsed time is passed in rather than read from the clock.
/// </remarks>
public sealed class JoinAttempt
{
    /// <summary>How long to wait for the relay before reporting it unreachable (A-1.5b).</summary>
    public static readonly TimeSpan ContactTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Where this attempt is.</summary>
    public JoinPhase Phase { get; private set; } = JoinPhase.Idle;

    /// <summary>The code being used, or null when idle.</summary>
    public SessionCode? Code { get; private set; }

    /// <summary>Why <see cref="JoinPhase.Failed"/>, or <see cref="SessionFailure.None"/>.</summary>
    public SessionFailure Failure { get; private set; } = SessionFailure.None;

    /// <summary>
    /// When this request lapses, as told to us by the DM's client. Null until the host answers.
    /// </summary>
    /// <remarks>
    /// <b>Given, never started here.</b> R-1.3c requires the admission wait and R-1.3a's prompt
    /// expiry to be the same window seen from two sides; a duration this client counted for itself
    /// would be a second clock, and the two drift on network delay, clock skew or a suspended
    /// client. The drift is not cosmetic — it produces a player told the request lapsed while the DM
    /// still holds a live prompt, so the DM accepts into nothing and neither side sees a fault.
    /// </remarks>
    public AdmissionDeadline? Deadline { get; private set; }

    /// <summary>
    /// Whether this client may receive session state.
    /// </summary>
    /// <remarks>
    /// True only while admitted. R-1.3 says a denied or pending client receives "no roster, no
    /// state, no events — not a filtered view, nothing", which is D-13's None level: absent from
    /// the payload rather than hidden on arrival.
    /// </remarks>
    public bool MayReceiveSessionState => Phase == JoinPhase.Admitted;

    /// <summary>The player asked to join. A human action, never automatic (R-1.3).</summary>
    public void Request(SessionCode code)
    {
        Phase = JoinPhase.Contacting;
        Code = code;
        Failure = SessionFailure.None;
        Deadline = null;
    }

    /// <summary>
    /// The relay delivered the request and the DM has not decided yet.
    /// </summary>
    /// <param name="deadline">
    /// When the DM's client says the window closes. Carried so this client can show a countdown
    /// toward it — R-1.3c requires the player to see the wait is bounded <i>while it runs</i>, not
    /// only when it ends. Being told after fifteen silent minutes is better than silence and worse
    /// than knowing.
    /// </param>
    public void AwaitDecision(AdmissionDeadline? deadline = null)
    {
        if (Phase != JoinPhase.Contacting)
        {
            return;
        }

        Phase = JoinPhase.AwaitingDecision;
        Deadline = deadline;
    }

    /// <summary>
    /// The window closed with no answer (R-1.3c, A-1.5h).
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="Denied"/>. Nobody refused this player — the DM may have been
    /// mid-encounter — so asking again is reasonable and the UI must say so. Reporting a lapse as a
    /// refusal tells someone they were turned away when nobody looked, and the two call for
    /// different behaviour: a lapsed player may re-request, a denied one should not be invited to.
    /// </remarks>
    public void Lapsed()
    {
        Phase = JoinPhase.Lapsed;
        Failure = SessionFailure.None;
    }

    /// <summary>
    /// Whether this attempt can simply be tried again without a new code (A-1.5h).
    /// </summary>
    public bool MayRequestAgain => Phase is JoinPhase.Lapsed or JoinPhase.Failed or JoinPhase.Idle;

    /// <summary>How long the player has left, for the countdown. Zero when there is no deadline.</summary>
    public TimeSpan RemainingAt(DateTimeOffset now) =>
        Deadline?.RemainingAt(now) ?? TimeSpan.Zero;

    /// <summary>The DM accepted.</summary>
    public void Admitted()
    {
        if (Phase != JoinPhase.AwaitingDecision)
        {
            return;
        }

        Phase = JoinPhase.Admitted;
    }

    /// <summary>
    /// The DM declined, or removed this client mid-session. Both end in the same place: no session
    /// state flows from this point, and R-1.3 requires the player to be told rather than dropped.
    /// </summary>
    public void Denied()
    {
        Phase = JoinPhase.Denied;
        Failure = SessionFailure.None;
    }

    /// <summary>Abandons the attempt without a decision having been made.</summary>
    public void Fail(SessionFailure failure)
    {
        Phase = JoinPhase.Failed;
        Failure = failure;
    }

    /// <summary>
    /// Fails the attempt if the relay has not answered in time.
    /// </summary>
    /// <remarks>
    /// Only <see cref="JoinPhase.Contacting"/> times out. Waiting on the DM does not, because a DM
    /// taking a minute to decide is normal and telling the player their attempt failed would be
    /// false — R-1.3's requirement is that the player knows they are waiting on a person, which is
    /// what <see cref="JoinPhase.AwaitingDecision"/> says.
    /// </remarks>
    /// <param name="elapsedSinceRequest">How long the relay has had the request.</param>
    /// <returns>True if this call ended the attempt.</returns>
    public bool ExpireIfContactTimedOut(TimeSpan elapsedSinceRequest)
    {
        if (Phase != JoinPhase.Contacting || elapsedSinceRequest < ContactTimeout)
        {
            return false;
        }

        Fail(SessionFailure.RelayUnreachable);
        return true;
    }
}
