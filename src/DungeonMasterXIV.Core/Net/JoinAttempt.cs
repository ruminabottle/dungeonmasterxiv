using System;

namespace DungeonMasterXIV.Net;

public sealed class JoinAttempt
{
    public static readonly TimeSpan ContactTimeout = TimeSpan.FromSeconds(10);

    public JoinPhase Phase { get; private set; } = JoinPhase.Idle;

    public SessionCode? Code { get; private set; }

    public SessionFailure Failure { get; private set; } = SessionFailure.None;

    public AdmissionDeadline? Deadline { get; private set; }

    public bool MayReceiveSessionState => Phase == JoinPhase.Admitted;

    public string? Fingerprint { get; private set; }

    public bool FingerprintWasComparableAtDecision { get; private set; }

    public Guid? ParticipantId { get; private set; }

    public void Request(SessionCode code)
    {
        Phase = JoinPhase.Contacting;
        Code = code;
        Failure = SessionFailure.None;
        Deadline = null;
        Fingerprint = null;
        FingerprintWasComparableAtDecision = false;

        ParticipantId = null;
    }

    public void Left()
    {
        Phase = JoinPhase.Idle;
        Code = null;
        Failure = SessionFailure.None;
        Deadline = null;
        Fingerprint = null;
        FingerprintWasComparableAtDecision = false;
        ParticipantId = null;
    }

    public void HostKeyOffered(byte[] hostPublicKey, byte[] ownPublicKey)
    {
        ArgumentNullException.ThrowIfNull(hostPublicKey);
        ArgumentNullException.ThrowIfNull(ownPublicKey);

        if (Phase is not (JoinPhase.Contacting or JoinPhase.AwaitingDecision))
        {
            return;
        }

        Fingerprint = KeyFingerprint.Of(hostPublicKey, ownPublicKey);
    }

    public void AwaitDecision(AdmissionDeadline? deadline = null)
    {
        if (Phase != JoinPhase.Contacting)
        {
            return;
        }

        Phase = JoinPhase.AwaitingDecision;
        Deadline = deadline;
    }

    public void Lapsed()
    {
        Phase = JoinPhase.Lapsed;
        Failure = SessionFailure.None;
    }

    public bool MayRequestAgain => Phase is JoinPhase.Lapsed or JoinPhase.Failed or JoinPhase.Idle;

    public TimeSpan RemainingAt(DateTimeOffset now) =>
        Deadline?.RemainingAt(now) ?? TimeSpan.Zero;

    public void Admitted()
    {
        if (Phase != JoinPhase.AwaitingDecision)
        {
            return;
        }

        FingerprintWasComparableAtDecision = Fingerprint is not null;
        Phase = JoinPhase.Admitted;
    }

    public void ToldItIsParticipant(Guid participantId)
    {
        if (Phase != JoinPhase.Admitted)
        {
            return;
        }

        ParticipantId = participantId;
    }

    public void Denied()
    {
        Phase = JoinPhase.Denied;
        Failure = SessionFailure.None;
    }

    public void Fail(SessionFailure failure)
    {
        Phase = JoinPhase.Failed;
        Failure = failure;
    }

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
