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
    }

    /// <summary>The relay delivered the request and the DM has not decided yet.</summary>
    public void AwaitDecision()
    {
        if (Phase != JoinPhase.Contacting)
        {
            return;
        }

        Phase = JoinPhase.AwaitingDecision;
    }

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
