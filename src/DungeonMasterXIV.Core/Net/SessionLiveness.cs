namespace DungeonMasterXIV.Net;

internal readonly record struct SessionLiveness(HostSession Host, JoinAttempt Join)
{
    public bool InAHostedSession => Host.Phase is HostingPhase.Registering or HostingPhase.Hosting;

    public bool JoinRequiresRelayConnection =>
        Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted;

    public bool RequiresRelayConnection => Host.RequiresRelayConnection || JoinRequiresRelayConnection;
}
