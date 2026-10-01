namespace DungeonMasterXIV.Net;

/// <summary>Tells whether hosting or joining is active and so whether the relay connection is needed.</summary>
internal readonly record struct SessionLiveness(HostSession Host, JoinAttempt Join)
{
    public bool InAHostedSession => Host.Phase is HostingPhase.Registering or HostingPhase.Hosting;

    public bool JoinRequiresRelayConnection =>
        Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted;

    public bool RequiresRelayConnection => Host.RequiresRelayConnection || JoinRequiresRelayConnection;
}
