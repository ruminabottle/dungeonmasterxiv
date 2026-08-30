using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Builds a session's collaborators, in the order they depend on each other.
/// </summary>
/// <remarks>
/// <para>
/// <b>COMPOSING THE COLLABORATORS IS NOT COORDINATING THEM (DMXENG-128).</b> This is the same cut
/// <see cref="InboundWiring"/> made at DMXENG-65 and for the same stated reason: the point is WHERE
/// THE NEXT COLLABORATOR LANDS. A twelfth collaborator now edits <i>this</i> type and leaves
/// <see cref="SessionCoordinator"/>'s class span unchanged — where before, every one of the eleven
/// below enlarged it, which is how it reached margin 0 and blocked the chunk behind it.
/// </para>
/// <para>
/// <b>THE ORDER HERE IS LOAD-BEARING AND IS THE REASON THIS IS A TYPE RATHER THAN A METHOD.</b>
/// Several collaborators close over ones built further down, so the sequence is a correctness
/// property rather than a style. It is not merely commented: DMXENG-45 made it DETECTED —
/// <see cref="JoinRequester"/> guards its collaborators, so building it early throws rather than
/// passing a null nothing refuses. The per-line notes below record which reads are deferred and why.
/// </para>
/// <para>
/// <b>A class rather than a struct, for the reason <see cref="InboundWiring"/> records:</b> CS9111
/// forbids a lambda inside a struct instance member from capturing a primary constructor parameter,
/// and most of the wiring below is lambdas.
/// </para>
/// </remarks>
internal sealed class SessionWiring
{
    /// <summary>Wires one session's collaborators.</summary>
    /// <param name="transport">The socket adapter.</param>
    /// <param name="relayAddress">Reads the configured relay at the moment of connecting.</param>
    /// <param name="window">How long a session survives an interruption (A-1.23, A-1.27).</param>
    /// <param name="log">Where transport decisions are recorded.</param>
    /// <param name="capabilities">What Core cannot do for itself (DMXENG-13).</param>
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
        // Null-conditional because Joiner is built FURTHER DOWN this constructor: the closure is
        // not INVOKED until after construction, but the compiler cannot know that. Suppressing with
        // ! would assert something this constructor does not yet guarantee. That reasoning stands.
        //
        // The order itself is now DETECTED (DMXENG-45): JoinRequester guards its collaborators, so
        // building it before these throws rather than passing a null nothing refuses. Measured --
        // with the order swapped and no guard, the suite passed clean.
        Handshake = new OutboundHandshake(Link, Host, Join, () => Joiner?.Keys);
        Roster = new RosterBroadcast(
            Link,
            Admissions.Audience,
            HostIdentity.ForHost(() => HostKeys, () => Host.Code, capabilities.HostNameSource, Admissions.PeerCodeFor),
            log);
        // What RosterBroadcast reads to SEAL, read here to OPEN (R-1.3k).
        Resources = new SessionResources(
            Admissions,
            Inbox,
            () => Grace,
            new MemberContentKeys(Admissions.Audience, () => HostKeys, () => Host.Code, log),
            new MemberContentReceipts());
        Interruption = new SessionInterruption(Link, Host, Join, SynchroniseTransport, window);
        Joiner = new JoinRequester(Handshake, Interruption, Join, newKeys, SynchroniseTransport);
        // AFTER Joiner, and that ordering is the point: SessionMembership closes over the joiner
        // rather than over the coordinator, which is one fewer escaped reference.
        Membership = new SessionMembership(Link, Joiner, () => Join.Code);
        // AFTER Interruption, which owns the Grace window this reads. The Func defers that read to
        // use time, so the ordering hazard DMXENG-45 detected does not extend to it -- but HostRunner
        // guards every argument anyway, which is the point of those guards.
        Hosting = new HostRunner(Host, Resources, Handshake, newKeys, SynchroniseTransport);
    }

    /// <summary>This client's hosting lifecycle. Owned here because the collaborators read it.</summary>
    internal HostSession Host { get; } = new();

    /// <summary>This client's join attempt, likewise.</summary>
    internal JoinAttempt Join { get; } = new();

    /// <summary>Where the link delivers what arrives.</summary>
    internal AdmissionInbox Inbox { get; } = new();

    /// <summary>What this client's phases mean. See <see cref="SessionLiveness"/>.</summary>
    internal SessionLiveness Liveness => new(Host, Join);

    /// <summary>
    /// Brings the socket into line with whether a session needs one.
    /// </summary>
    /// <remarks>
    /// R-1.1's invariant lives here and in <see cref="SessionLiveness.RequiresRelayConnection"/>
    /// and nowhere else, so there is one answer to "should we be connected" rather than a rule each
    /// call site is trusted to remember. It sits beside the collaborators it reconciles, which is
    /// what lets this type be built from the coordinator's own five arguments and nothing else.
    /// </remarks>
    internal void SynchroniseTransport()
    {
        // The link reports rather than applies, so the mutual recursion between this and Fail still
        // terminates the way it always has: Fail leaves nothing wanting a connection, so the next
        // call through here disconnects and returns None.
        var failure = Link.Synchronise(Liveness.RequiresRelayConnection);

        if (failure != SessionFailure.None)
        {
            Interruption.Fail(failure);
        }
    }

    /// <summary>The relay connection every other collaborator sends through.</summary>
    internal RelayLink Link { get; }

    /// <summary>Turns a claimed relink into a seat, or refuses it.</summary>
    internal Func<string?, RelinkClaim> ResolveRelink { get; }

    /// <summary>Admission: who is asking, who is in, who lapsed.</summary>
    internal AdmissionControl Admissions { get; }

    /// <summary>What this client owes the relay and has not yet sent.</summary>
    internal OutboundHandshake Handshake { get; }

    /// <summary>Publishes the roster the host owns.</summary>
    internal RosterBroadcast Roster { get; }

    /// <summary>The session's shared state, including what this client recorded.</summary>
    internal SessionResources Resources { get; }

    /// <summary>Turns a dropped link into a window rather than an ending (R-1.4).</summary>
    internal SessionInterruption Interruption { get; }

    /// <summary>Drives this client's own join attempt.</summary>
    internal JoinRequester Joiner { get; }

    /// <summary>This client's seat in a session it joined.</summary>
    internal SessionMembership Membership { get; }

    /// <summary>Drives a session this client hosts.</summary>
    internal HostRunner Hosting { get; }

    /// <summary>
    /// The interruption's grace window, read through <see cref="Interruption"/>.
    /// </summary>
    /// <remarks>
    /// Read through a member rather than inline for the reason the coordinator always did: the
    /// <c>Func</c> above defers to use time, and going through a member is what tells the compiler
    /// so. Inlining it warns CS8602 on a read that cannot happen during construction.
    /// </remarks>
    private GraceWindow Grace => Interruption.Grace;

    /// <summary>
    /// The host's key exchange, read through <see cref="Hosting"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT null-conditional, matching what <see cref="SessionCoordinator.HostKeys"/>
    /// has always done: every caller above defers the read behind a <c>Func</c>, so a throw here
    /// would mean a collaborator read it during construction, which is the ordering defect
    /// DMXENG-45 exists to surface rather than to hide.
    /// </remarks>
    private SessionKeyExchange? HostKeys => Hosting.Keys;
}
