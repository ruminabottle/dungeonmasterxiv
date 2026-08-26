using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The DM's side of the hosting lifecycle (R-1.1). Holds no socket — it decides what the transport
/// should be doing, and the adapter in the plugin's <c>Net/</c> obeys it.
/// </summary>
/// <remarks>
/// Time is a parameter, never read from the clock. A state machine that called
/// <see cref="DateTime.UtcNow"/> would sit in exactly the right project and still be untestable,
/// and the failure would look like a flaky test rather than a layering problem.
/// </remarks>
public sealed class HostSession
{
    /// <summary>How long registering may take before it is reported as a failure (A-1.5b).</summary>
    public static readonly TimeSpan RegistrationTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Where the DM is in the lifecycle.</summary>
    public HostingPhase Phase { get; private set; } = HostingPhase.NotHosting;

    /// <summary>The code being claimed or held, or null when not hosting.</summary>
    public SessionCode? Code { get; private set; }

    /// <summary>Why <see cref="HostingPhase.Failed"/>, or <see cref="SessionFailure.None"/>.</summary>
    public SessionFailure Failure { get; private set; } = SessionFailure.None;

    /// <summary>
    /// Whether the transport should be holding a relay connection right now.
    /// </summary>
    /// <remarks>
    /// R-1.1: "There is no circumstance in which the plugin holds a relay connection while no
    /// session is running." Expressed as one derived property rather than as a rule the adapter is
    /// asked to remember, so the two cannot drift apart.
    /// </remarks>
    public bool RequiresRelayConnection =>
        Phase is HostingPhase.Registering or HostingPhase.Hosting;

    /// <summary>Begins claiming <paramref name="code"/>. The connection opens now and not before.</summary>
    public void Start(SessionCode code)
    {
        Phase = HostingPhase.Registering;
        Code = code;
        Failure = SessionFailure.None;
    }

    /// <summary>The relay confirmed the code. The session is live.</summary>
    public void Registered()
    {
        if (Phase != HostingPhase.Registering)
        {
            return;
        }

        Phase = HostingPhase.Hosting;
    }

    /// <summary>
    /// Ends the session. Called for an explicit stop, for plugin unload and for dispose — R-1.1
    /// treats all three the same, so there is one path and no way to end one without the others.
    /// </summary>
    public void Stop()
    {
        Phase = HostingPhase.NotHosting;
        Code = null;
        Failure = SessionFailure.None;
    }

    /// <summary>Records a transport failure and leaves hosting.</summary>
    public void Fail(SessionFailure failure)
    {
        Phase = HostingPhase.Failed;
        Code = null;
        Failure = failure;
    }

    /// <summary>
    /// Fails the attempt if registering has taken too long, so the DM is never left watching an
    /// open-ended spinner (A-1.5b).
    /// </summary>
    /// <param name="elapsedSinceStart">How long the current registration has been running.</param>
    /// <returns>True if this call ended the attempt.</returns>
    public bool ExpireIfRegistrationTimedOut(TimeSpan elapsedSinceStart)
    {
        if (Phase != HostingPhase.Registering || elapsedSinceStart < RegistrationTimeout)
        {
            return false;
        }

        Fail(SessionFailure.RelayUnreachable);
        return true;
    }
}
