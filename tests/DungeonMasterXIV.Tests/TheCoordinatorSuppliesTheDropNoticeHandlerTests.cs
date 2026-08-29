using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-118: the coordinator SUPPLIES the A-1.28 drop-notice handler. Not that the handler works —
/// that it is handed over.
/// </summary>
/// <remarks>
/// <para>
/// <b>A-1.28's mechanism was well covered and its WIRING was covered by nothing.</b> Deleting the
/// entire <c>Transport:</c> argument from <c>InboundWiring</c> produced 0 build errors and left the
/// whole suite green — measured. Every existing test proves the handler works WHEN SUPPLIED:
/// <c>TheHostLearnsAMemberDroppedTests</c> and <c>EveryMessageTypeReachesAnArmTests</c> both build
/// their own <see cref="TransportNotices"/> and drive the inbox directly. None proves anyone supplies
/// it. Those tests are correct for what they test; this is the sentence they do not say.
/// </para>
/// <para>
/// <b>Tested at <c>InboundWiring</c> because that IS the coordinator's wiring</b> —
/// <c>SessionCoordinator</c> constructs <c>new InboundWiring(...)</c> and delegates to it, so this is
/// the defect's own home rather than a stand-in for it. It is reached directly because the drop
/// record has no public accessor on the coordinator; see the note below, which is a separate finding.
/// </para>
/// <para>
/// <b>SUPPLIED IS NOT ENOUGH, so both are asserted.</b> A handler wired to a no-op would satisfy a
/// null check while recording nothing — the same defect with a decoration. So one test asks whether
/// it was handed over and the other invokes it and looks at what changed.
/// </para>
/// </remarks>
public class TheCoordinatorSuppliesTheDropNoticeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 9, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    // THE CONTROL. If For() ever returned a wholly default InboundHandlers -- a broken fixture, a
    // constructor that threw and was swallowed -- the defect test below would redden for a reason
    // that has nothing to do with the Transport argument. This says the object is real first.
    [Fact]
    public void TheWiringProducesRealHandlers()
    {
        var (handlers, _, _, _) = Wired();

        Assert.NotNull(handlers.Admission.OnJoinRequest);
        Assert.NotNull(handlers.HostAuthored.OnContent);
    }

    // THE PREMISE. RecordDrop refuses a key this host has not admitted and returns false, so an
    // unadmitted member would make the recording test pass against nothing.
    [Fact]
    public void TheMemberIsAdmittedBeforeAnyDropIsDelivered()
    {
        var (_, admissions, _, peerCode) = Wired();

        Assert.True(admissions.Audience.IsAdmitted(peerCode));
        Assert.Null(admissions.Drops.WhenDropped(peerCode));
    }

    // THE DEFECT. Fails if: the Transport argument is dropped from InboundWiring, which compiles
    // cleanly and leaves every other test in the tree green.
    [Fact]
    public void TheWiringSuppliesADropNoticeHandler()
    {
        var (handlers, _, _, _) = Wired();

        Assert.NotNull(handlers.Transport.OnConnectionDropped);
    }

    // AND IT IS WIRED TO SOMETHING. Supplied-but-inert passes the check above and records nothing,
    // so this invokes the handler the coordinator would have handed over and asks the admissions
    // desk what changed.
    [Fact]
    public void TheSuppliedHandlerRecordsTheDrop()
    {
        var (handlers, admissions, memberKey, peerCode) = Wired();

        handlers.Transport.OnConnectionDropped!(memberKey);

        Assert.Equal(Now, admissions.Drops.WhenDropped(peerCode));
    }

    /// <summary>
    /// The handlers the coordinator would build, from the same class it builds them with, over a
    /// host that has admitted one member.
    /// </summary>
    private static (InboundHandlers Handlers, AdmissionControl Admissions, byte[] MemberKey, PeerCode Peer) Wired()
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
        var peerCode = admissions.PeerCodeFor(joiner.PublicKey);
        admissions.Receive(peerCode, joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));
        admissions.Admit(peerCode);

        var resources = new SessionResources(
            admissions,
            new AdmissionInbox(),
            () => new GraceWindow(),
            new MemberContentKeys(admissions.Audience, () => hostKeys, () => host.Code, SilentLog.Instance),
            new MemberContentReceipts());

        var wiring = new InboundWiring(admissions, resources, static _ => RelinkClaim.None);

        return (wiring.For(Now, sessionKey: null, onHostContent: _ => { }), admissions, joiner.PublicKey, peerCode);
    }

    private sealed class SilentTransport : ISessionTransport
    {
        public bool IsConnected => true;

        public bool IsReadyToSend => true;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay)
        {
        }

        public void Disconnect()
        {
        }

        public void Send(byte[] envelope)
        {
        }
    }
}
