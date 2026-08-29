using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.16 through the COORDINATOR: ending a session actually tells the people in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file exists because the sender being right proved nothing about the wiring.</b>
/// <see cref="TheClosingNoticeReachesParticipantsTests"/> drives <see cref="RosterBroadcast"/>
/// directly and demonstrates the notice thoroughly — and with all of it green,
/// <b>deleting the <c>PublishClosing</c> call out of <c>StopHosting</c> altogether left the whole
/// suite passing.</b> Measured, not feared: 953 passed with the call gone.
/// </para>
/// <para>
/// <b>AND THE ORDERING MUTATION PASSED TOO.</b> Moving the publish to AFTER the delegation — where
/// teardown has already emptied the audience, so the notice is sealed to nobody and A-1.16 fails
/// with nothing sent — was equally invisible. Two distinct ways to ship a session that ends in
/// silence, both green.
/// </para>
/// <para>
/// <b>So the predicate under test is the one the window actually calls</b>, the same reason
/// <see cref="ADroppedJoinerKeepsItsSeatTests"/> gives for going through the coordinator. A test at
/// the broadcast layer cannot fail on a call site that does not exist.
/// </para>
/// <para>
/// <b>This is fe-3's three-of-five finding arriving in my own work.</b> Three of
/// <c>StopHosting</c>'s five teardown steps deleted invisibly; my closing notice was the fourth
/// until this file. "All tests pass" would have been a true sentence and a weak claim.
/// </para>
/// </remarks>
public class EndingASessionAnnouncesItTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 2, 0, 0, TimeSpan.Zero);

    /// <summary>The tail of the alphabet, so a peer code can never equal the session code.</summary>
    private static readonly string PeerCode = SpeakableAlphabet.Characters[^SessionCode.Length..];

    // THE WIRING. Fails when the PublishClosing call is deleted from StopHosting, which is the
    // mutation the entire suite passed before this test existed.
    [Fact]
    public void EndingTheSessionTellsAnAdmittedParticipant()
    {
        var (coordinator, transport, joiner, key) = HostingWithAnAdmittedParticipant();

        coordinator.StopHosting(Now);

        var closing = ClosingNoticeOpenedWith(key, transport);
        Assert.Equal(SessionClosing.DecidedByHost(Now), closing);
        Assert.Equal(SessionClosing.Window, closing.RemainingAt(Now));
        joiner.Dispose();
    }

    // THE ORDERING, AND IT NEEDS ITS OWN NAME BECAUSE IT IS A SEPARATE MUTATION. Teardown lives
    // inside HostRunner.Stop since DMXENG-51 and it empties the admissions, so a publish that runs
    // afterwards seals to an audience of nobody: NOTHING IS SENT AT ALL and no assertion about the
    // notice's contents can fire, because there is no notice.
    //
    // Asserting on the COUNT rather than only on the contents is what makes this fail rather than
    // throw somewhere confusing — "one payload went out" is the claim, and it is false the moment
    // the two lines swap.
    [Fact]
    public void TheNoticeGoesOutWhileThereIsStillSomebodyToSendItTo()
    {
        var (coordinator, transport, joiner, _) = HostingWithAnAdmittedParticipant();

        coordinator.StopHosting(Now);

        Assert.NotEmpty(Payloads(transport));
        joiner.Dispose();
    }

    // WHAT IS ON THE WIRE IS THE DEADLINE, NOT THE MOMENT OF ENDING, and the difference is the
    // whole of A-1.16b rather than a detail of encoding.
    //
    // If the wire carried endedAt and each client added its own sixty seconds, the build would PASS
    // A-1.16b's stated demonstration — varying endedAt does move every client's displayed time —
    // while failing the sentence underneath it: "a build whose clients compute the deadline locally
    // on receipt". The window would then live in a constant on the host AND in one on each client,
    // which is the two-places drift the criterion exists to forbid.
    //
    // So this asserts the raw wire value against BOTH candidates. Equality alone would not
    // discriminate; the inequality is the half that fails on the defect.
    [Fact]
    public void TheWireCarriesTheDeadlineRatherThanTheMomentTheHostEnded()
    {
        var (coordinator, transport, joiner, key) = HostingWithAnAdmittedParticipant();

        coordinator.StopHosting(Now);

        var onTheWire = RawClosingTicks(key, transport);
        Assert.Equal(Now.Add(SessionClosing.Window).UtcTicks, onTheWire);
        Assert.NotEqual(Now.UtcTicks, onTheWire);
        joiner.Dispose();
    }

    // The other half of A-1.16b at this layer: the deadline the participant reads is the one the
    // HOST sent, so changing when the host ends moves it. A client computing sixty seconds locally
    // on receipt passes every other test here.
    [Fact]
    public void TheDeadlineTheParticipantReadsFollowsWhenTheHostEnded()
    {
        var (first, firstTransport, firstJoiner, firstKey) = HostingWithAnAdmittedParticipant();
        var (second, secondTransport, secondJoiner, secondKey) = HostingWithAnAdmittedParticipant();

        first.StopHosting(Now);
        second.StopHosting(Now.AddMinutes(5));

        Assert.NotEqual(
            ClosingNoticeOpenedWith(firstKey, firstTransport),
            ClosingNoticeOpenedWith(secondKey, secondTransport));
        Assert.Equal(
            TimeSpan.FromMinutes(5),
            ClosingNoticeOpenedWith(secondKey, secondTransport).Instant
                - ClosingNoticeOpenedWith(firstKey, firstTransport).Instant);

        firstJoiner.Dispose();
        secondJoiner.Dispose();
    }

    /// <summary>
    /// A host with one admitted participant, and the key that participant would open with.
    /// </summary>
    /// <remarks>
    /// <b>The key is derived HERE, before the session ends, and that is not tidiness.</b> Teardown
    /// disposes the host's key pair, so a helper that derived it after <c>StopHosting</c> would
    /// throw on a null and report a crash where the real answer is "the notice never went out".
    /// </remarks>
    private static (SessionCoordinator Coordinator, FakeTransport Transport, SessionKeyExchange Joiner, byte[] Key)
        HostingWithAnAdmittedParticipant()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);

        coordinator.StartHosting();
        coordinator.Host.Registered();
        coordinator.SynchroniseTransport();

        var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest(
            PeerCodes.Of(PeerCode), joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));
        coordinator.Admit(PeerCodes.Of(PeerCode));

        var key = joiner.DeriveSharedKey(coordinator.HostKeys!.PublicKey, coordinator.Host.Code!.Value);

        // The admission traffic is not what is under test; only what StopHosting sends is.
        transport.Sent.Clear();

        return (coordinator, transport, joiner, key);
    }

    private static SessionClosing ClosingNoticeOpenedWith(byte[] key, FakeTransport transport) =>
        SessionClosing.TryFromWire(RawClosingTicks(key, transport))!.Value;

    /// <summary>
    /// The closing value exactly as it arrived, before <see cref="SessionClosing"/> touches it.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ClosingNoticeOpenedWith"/> so one test can assert on the RAW number.
    /// Reading it back through the type would compare the host's arithmetic against itself.
    /// </remarks>
    private static long RawClosingTicks(byte[] key, FakeTransport transport)
    {
        foreach (var payload in Payloads(transport))
        {
            byte[] plaintext;
            try
            {
                plaintext = SessionCipher.Open(key, payload.TryGetSealedPayload()!, payload.AssociatedData());
            }
            catch (CryptographicException)
            {
                continue;   // sealed for somebody else
            }

            Assert.True(SessionContentCodec.TryDecode(plaintext, out var content));

            Assert.NotNull(content!.ClosingAtUtcTicks);
            return content.ClosingAtUtcTicks!.Value;
        }

        throw new InvalidOperationException(
            "No sealed payload this participant could open, so the session ended in silence (A-1.16).");
    }

    private static IEnumerable<WireEnvelope> Payloads(FakeTransport transport)
    {
        foreach (var sent in transport.Sent)
        {
            if (EnvelopeCodec.TryDecode(sent, out var envelope) && envelope!.TryGetSealedPayload() is not null)
            {
                yield return envelope;
            }
        }
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);
    }
}
