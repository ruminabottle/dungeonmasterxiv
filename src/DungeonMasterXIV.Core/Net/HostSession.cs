using System;

namespace DungeonMasterXIV.Net;

/// <summary>Tracks hosting from code registration to stop: the phase, the current code and any failure.</summary>
public sealed class HostSession
{
    public static readonly TimeSpan RegistrationTimeout = TimeSpan.FromSeconds(10);

    public HostingPhase Phase { get; private set; } = HostingPhase.NotHosting;

    public SessionCode? Code { get; private set; }

    public SessionFailure Failure { get; private set; } = SessionFailure.None;

    public SessionCode? SupersededCode { get; private set; }

    public bool CodeChangedMidSession => SupersededCode is not null;

    public bool RequiresRelayConnection =>
        Phase is HostingPhase.Registering or HostingPhase.Hosting;

    public void Start(SessionCode code)
    {
        Phase = HostingPhase.Registering;
        Code = code;
        Failure = SessionFailure.None;
    }

    public void CodeSuperseded(SessionCode replacement)
    {
        SupersededCode = Code;
        Code = replacement;
    }

    public void AcknowledgeCodeChange() => SupersededCode = null;

    public void Registered()
    {
        if (Phase != HostingPhase.Registering)
        {
            return;
        }

        Phase = HostingPhase.Hosting;
    }

    public void Stop()
    {
        Phase = HostingPhase.NotHosting;
        Code = null;
        Failure = SessionFailure.None;
        SupersededCode = null;
    }

    public void CodeAlreadyLive(SessionCode replacement)
    {
        if (Phase != HostingPhase.Registering)
        {
            return;
        }

        Code = replacement;
    }

    public void Fail(SessionFailure failure)
    {
        Phase = HostingPhase.Failed;
        Code = null;
        Failure = failure;
    }

    public bool ExpireIfRegistrationTimedOut(TimeSpan elapsedSinceStart, bool requestWasSent)
    {
        if (Phase != HostingPhase.Registering || elapsedSinceStart < RegistrationTimeout)
        {
            return false;
        }

        Fail(requestWasSent
            ? SessionFailure.RegistrationNotAnswered
            : SessionFailure.ConnectionNeverOpened);

        return true;
    }
}
