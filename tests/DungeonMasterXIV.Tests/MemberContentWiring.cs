using System;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The wiring the member-content criteria are all asserted through, shared by the classes DMXENG-145
/// split apart.
/// </summary>
/// <remarks>
/// <b>IT IS HERE SO THERE IS ONE OF IT.</b> DMXENG-145 split
/// <see cref="AMemberCannotMakeTheHostRetainWhatItRefusedTests"/> by criterion because the class had
/// twenty-one lines of margin left. Both halves drive the same production path, and a second copy of
/// this construction is a second thing to keep in step — the defect DMXENG-137 fixed was an ORDERING
/// inside <c>InboundWiring</c>, which only a test that goes through the real wiring can see at all.
/// </remarks>
internal static class MemberContentWiring
{
    /// <summary>One byte past what the stream will accept, so the payload below is genuinely refused.</summary>
    public const int OverTheStreamsBound = 80001;


    private static readonly DateTimeOffset Now = new(2026, 8, 30, 1, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    // Drives the PRODUCTION handler rather than constructing a receipts object: the defect was the
    // ORDER of two calls in InboundWiring, and a test that called Record directly could not see it.
    public static (InboundHandlers Handlers, SessionResources Resources, PeerCode Peer, SessionAudience Roster) Wired()
    {
        var host = new HostSession();
        host.Start(Code);
        var hostKeys = new SessionKeyExchange();

        var admissions = new AdmissionControl(
            new AdmissionAnnouncer(new SilentTransport()),
            () => host.Code,
            () => hostKeys,
            static _ => null,
            SilentLog.Instance);

        var joiner = new SessionKeyExchange();
        var peer = admissions.PeerCodeFor(joiner.PublicKey);
        admissions.Receive(peer, joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));
        admissions.Admit(peer);

        var resources = new SessionResources(
            admissions,
            new AdmissionInbox(),
            () => new GraceWindow(),
            new MemberContentKeys(admissions.Audience, () => hostKeys, () => host.Code, SilentLog.Instance),
            new MemberContentReceipts());

        var broadcast = new RosterBroadcast(
            new RelayLink(new SilentTransport(), () => RelayEndpoint.Default, static _ => { }),
            admissions.Audience,
            new HostIdentity(() => hostKeys, () => host.Code, () => DisplayName.None, () => null),
            SilentLog.Instance);

        var wiring = new InboundWiring(admissions, resources, static _ => RelinkClaim.None, broadcast);

        return (wiring.For(Now, sessionKey: null, onHostContent: _ => { }), resources, peer, admissions.Audience);
    }

    private sealed class SilentTransport : ISessionTransport
    {
        public bool IsConnected => true;
        public bool IsReadyToSend => true;
        public event Action<SessionFailure>? Failed { add { } remove { } }
        public event Action<byte[]>? Received { add { } remove { } }
        public void Connect(Uri relay) { }
        public void Disconnect() { }
        public void Send(byte[] envelope) { }
    }
}
