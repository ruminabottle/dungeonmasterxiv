using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-56: nothing validated that a joiner's public key was a well-formed SPKI blob, so a peer the
/// host could never speak to could be admitted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every request here arrives on the wire.</b> A real <see cref="WireEnvelope"/> goes through
/// <see cref="EnvelopeCodec.Encode"/> into the transport's <c>Received</c> event, so the production
/// decode and the production <c>OnJoinRequest</c> wiring are what run. That is not incidental: the
/// defect survived because the test that admits a junk key
/// (<c>AParticipantWithAnUnusableKeyCannotBreakTheBroadcast</c>) calls
/// <c>ReceiveJoinRequest</c> directly, which is downstream of the door being fixed here.
/// </para>
/// <para>
/// <b>The positive case is load-bearing.</b> A validator that admits nothing passes every negative
/// test in this file, so <see cref="AWellFormedRequestIsStillAdmittedToTheQueue"/> is what makes the
/// set falsifiable — see the mutation recorded in the PR.
/// </para>
/// </remarks>
public class AJoinerKeyIsValidatedAtTheWireTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 3, 0, 0, TimeSpan.Zero);

    /// <summary>Bytes that are not an SPKI blob at all — the case in the bug report.</summary>
    private static readonly byte[] NotAKey = { 1, 2, 3 };

    /// <summary>
    /// A <b>well-formed</b> SPKI blob on the wrong curve.
    /// </summary>
    /// <remarks>
    /// This is the case a format-only check misses and it is worse than the junk one.
    /// <c>ImportSubjectPublicKeyInfo</c> ACCEPTS it — measured — and the failure surfaces later out
    /// of <c>DeriveRawSecretAgreement</c> as an <see cref="ArgumentException"/>, which is not the
    /// <see cref="CryptographicException"/> that PR #86's broadcast guard catches.
    /// </remarks>
    private static byte[] WrongCurve()
    {
        using var other = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);
        return other.PublicKey.ExportSubjectPublicKeyInfo();
    }

    // The bug. Before the fix the request became a prompt, the DM could admit it, and the resulting
    // participant was addressable by the relay and unreachable by the host with nothing logged.
    [Fact]
    public void AJoinRequestWithAnUnusableKeyNeverBecomesAPendingAdmission()
    {
        var (coordinator, transport) = Hosting();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, NotAKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Empty(coordinator.Admissions.Pending);
    }

    // The half a "does it parse as SPKI" check would let through, and the half that escapes #86's
    // guard as an ArgumentException rather than being skipped.
    [Fact]
    public void AJoinRequestOnTheWrongCurveNeverBecomesAPendingAdmission()
    {
        var (coordinator, transport) = Hosting();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, WrongCurve()));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Empty(coordinator.Admissions.Pending);
    }

    // THE POSITIVE CONTROL. Without it every assertion above is satisfied by a validator that
    // refuses everything, which would break joining outright while the suite stayed green.
    [Fact]
    public void AWellFormedRequestIsStillAdmittedToTheQueue()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, joiner.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        var pending = Assert.Single(coordinator.Admissions.Pending);
        Assert.Equal(joiner.PublicKey, pending.JoinerPublicKey);
    }

    // The property the fix exists to establish, asserted over the ADMITTED set rather than over the
    // door: whatever the DM admits, the host can actually derive a key for. This is what "no admitted
    // peer holds an unusable key" means, and it is stated in terms of the operation that was throwing.
    [Fact]
    public void NoPeerAdmittedFromTheWireHoldsAKeyTheHostCannotDeriveFrom()
    {
        var (coordinator, transport) = Hosting();
        using var good = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, NotAKey));
        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, WrongCurve()));
        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, good.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        // Snapshot first: Admit decides the request, which mutates the pending list underneath us.
        foreach (var peerCode in coordinator.Admissions.Pending.Select(request => request.PeerCode).ToList())
        {
            coordinator.Admit(peerCode);
        }

        var code = coordinator.Host.Code!.Value;
        foreach (var peer in coordinator.Audience.Recipients)
        {
            // Throws today for a peer holding an unusable key. No try/catch: the exception IS the
            // failure, and swallowing it here would leave the assertion passing on a skipped peer.
            Assert.NotEmpty(coordinator.HostKeys!.DeriveSharedKey(peer.PublicKey!, code));
        }

        Assert.Single(coordinator.Audience.Recipients);
    }

    // A refused key must not cost a legitimate joiner arriving in the SAME drain their prompt.
    // Dropping the whole batch would hand any client a way to suppress everyone else's admission,
    // which is the shape ANameTheHostRefusesDoesNotCostTheJoinerTheirPrompt guards on the name.
    [Fact]
    public void ARefusedKeyDoesNotSuppressAGoodRequestInTheSameDrain()
    {
        var (coordinator, transport) = Hosting();
        using var good = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, NotAKey));
        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, good.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(good.PublicKey, Assert.Single(coordinator.Admissions.Pending).JoinerPublicKey);
    }

    // Nothing goes back to a refused joiner, and that is deliberate rather than overlooked: whether
    // the DM is told someone tried to join with an unusable key is a product decision (D-8), left
    // untouched here. An unusable key is treated exactly as any other unparseable input on this path.
    [Fact]
    public void ARefusedKeyIsAnsweredWithSilenceRatherThanADecision()
    {
        var (coordinator, transport) = Hosting();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, NotAKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Empty(Sent(transport));
    }

    private static (SessionCoordinator Coordinator, FakeTransport Transport) Hosting()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);
        coordinator.StartHosting();
        coordinator.Host.Registered();
        transport.Sent.Clear();
        return (coordinator, transport);
    }

    private static List<WireEnvelope> Sent(FakeTransport transport)
    {
        var decoded = new List<WireEnvelope>();
        foreach (var bytes in transport.Sent)
        {
            if (EnvelopeCodec.TryDecode(bytes, out var envelope) && envelope is not null)
            {
                decoded.Add(envelope);
            }
        }

        return decoded;
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        /// <summary>Puts a real encoded frame on the wire, the way the relay would.</summary>
        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
