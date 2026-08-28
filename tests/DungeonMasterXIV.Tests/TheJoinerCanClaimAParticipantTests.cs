using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.9a: a returning client can actually make the relink claim R-1.5 requires.
/// </summary>
/// <remarks>
/// <para>
/// <b>The claim reached the wire types and the host's resolver and never travelled.</b>
/// <c>RelinkClaim relink = default</c> sat on three signatures, so every caller omitted it, every
/// call got <c>None</c>, and every relink branch took the not-a-relink path — while the suite stayed
/// green throughout. <b>A missing argument is a compile error; a defaulted one is silence.</b>
/// </para>
/// <para>
/// <b>These assert the FIELD, never the message type, and that is not a stylistic preference.</b>
/// <c>ForRelinkRequest</c> returns <see cref="WireMessageType.JoinRequest"/> — the same type an
/// ordinary join returns. A test keyed on the type cannot tell the two apart, which is exactly why
/// the A-1.12a coverage keyed on message type reported relink as sent while relink was unreachable.
/// <see cref="TheMessageTypeAloneCannotTellARelinkFromAJoin"/> pins that, so the reason survives the
/// next person who wonders why this file is written the way it is.
/// </para>
/// <para>
/// Frames are decoded with the production <see cref="EnvelopeCodec"/> rather than inspected as
/// objects, so a field that never reaches the wire fails here.
/// </para>
/// </remarks>
public class TheJoinerCanClaimAParticipantTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 2, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    // The defect. Fails before this chunk: nothing could put a claim on the wire, because no overload
    // accepted one and the send site called ForJoinRequest unconditionally.
    [Fact]
    public void AClaimedParticipantIdReachesTheWire()
    {
        var claimed = Guid.NewGuid();

        var sent = Assert.Single(Join(claimed));

        Assert.Equal(claimed.ToString("D"), sent.ClaimedParticipantId);
    }

    // The differential half. Without it, "the field is populated" is satisfied by a send site that
    // always populates it — and an ordinary join carrying somebody's participant id would be a
    // privacy defect rather than a cosmetic one.
    [Fact]
    public void AnOrdinaryJoinCarriesNoClaim()
    {
        var sent = Assert.Single(Join(claimedParticipantId: null));

        Assert.Null(sent.ClaimedParticipantId);
    }

    // Why every assertion here is about the field. Both paths produce the SAME message type, so a
    // test that checked the type would pass for a relink that had silently become a plain join —
    // which is the failure R-1.5 calls out by name: the claim dropped while every test stayed green.
    [Fact]
    public void TheMessageTypeAloneCannotTellARelinkFromAJoin()
    {
        var relink = Assert.Single(Join(Guid.NewGuid()));
        var ordinary = Assert.Single(Join(claimedParticipantId: null));

        Assert.Equal(WireMessageType.JoinRequest, relink.Type);
        Assert.Equal(relink.Type, ordinary.Type);
        Assert.NotEqual(relink.ClaimedParticipantId, ordinary.ClaimedParticipantId);
    }

    // The claim travels with the joiner's key, because the host answers by that key and a claim it
    // cannot address is a claim it cannot resolve.
    [Fact]
    public void AClaimTravelsWithTheKeyTheHostAnswersBy()
    {
        var sent = Assert.Single(Join(Guid.NewGuid()));

        Assert.NotNull(sent.PublicKey);
        Assert.NotEmpty(sent.PublicKey!);
    }

    /// <summary>Drives a real join to the point the request leaves, and returns what went out.</summary>
    private static List<WireEnvelope> Join(Guid? claimedParticipantId)
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default);

        coordinator.RequestJoin(Code, DisplayName.OrNone("Bob"), claimedParticipantId);
        coordinator.SynchroniseTransport();
        coordinator.Tick(TimeSpan.Zero, Now);

        return transport.Sent
            .Select(bytes => EnvelopeCodec.TryDecode(bytes, out var e) ? e : null)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        // Declared because the interface requires them and deliberately never raised: these tests
        // assert only what the client SENDS. Empty accessors rather than a suppression, so the fact
        // that nothing arrives here is visible at the member instead of hidden behind a pragma.
        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);
    }
}
