using System;

namespace DungeonMasterXIV.Net;

internal sealed class SessionWiring
{
    internal SessionWiring(
        ISessionTransport transport,
        Func<string> relayAddress,
        TimeSpan window,
        ISessionTransportLog log,
        SessionCapabilities capabilities)
    {
        ResolveRelink = capabilities.RelinkSource;
        var newKeys = capabilities.KeySource;

        Link = new RelayLink(transport, relayAddress, Inbox.Receive);
        Admissions = new AdmissionControl(
            new AdmissionAnnouncer(transport),
            () => Host.Code,
            () => HostKeys,
            capabilities.ParticipantSource,
            log);
        Handshake = new OutboundHandshake(Link, Host, Join, () => Joiner?.Keys);
        Roster = new RosterBroadcast(
            Link,
            Admissions.Audience,
            HostIdentity.ForHost(() => HostKeys, () => Host.Code, capabilities.HostNameSource, Admissions.PeerCodeFor),
            log);
        Resources = new SessionResources(
            Admissions,
            Inbox,
            () => Grace,
            new MemberContentKeys(Admissions.Audience, () => HostKeys, () => Host.Code, log),
            new MemberContentReceipts());
        Interruption = new SessionInterruption(Link, Host, Join, SynchroniseTransport, window);
        Joiner = new JoinRequester(Handshake, Interruption, Join, newKeys, SynchroniseTransport);
        Membership = new SessionMembership(Link, Joiner, () => Join.Code);
        Hosting = new HostRunner(Host, Resources, Handshake, newKeys, SynchroniseTransport);
    }

    internal HostSession Host { get; } = new();

    internal JoinAttempt Join { get; } = new();

    internal AdmissionInbox Inbox { get; } = new();

    internal SessionLiveness Liveness => new(Host, Join);

    internal void SynchroniseTransport()
    {
        var failure = Link.Synchronise(Liveness.RequiresRelayConnection);

        if (failure != SessionFailure.None)
        {
            Interruption.Fail(failure);
        }
    }

    internal RelayLink Link { get; }

    internal Func<string?, RelinkClaim> ResolveRelink { get; }

    internal AdmissionControl Admissions { get; }

    internal OutboundHandshake Handshake { get; }

    internal RosterBroadcast Roster { get; }

    internal SessionResources Resources { get; }

    internal SessionInterruption Interruption { get; }

    internal JoinRequester Joiner { get; }

    internal SessionMembership Membership { get; }

    internal HostRunner Hosting { get; }

    private GraceWindow Grace => Interruption.Grace;

    private SessionKeyExchange? HostKeys => Hosting.Keys;
}
