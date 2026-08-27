using System.Text;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Sessions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// The routing rules, without a socket: what the relay does with each kind of envelope.
/// </summary>
public sealed class RelayRouterTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    private readonly SessionRegistry _registry = new();
    private readonly RelayRouter _router;

    public RelayRouterTests() => _router = new RelayRouter(_registry);

    [Fact]
    public void ACodeRequestForAFreeCodeIsAccepted()
    {
        var decision = _router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");

        Assert.Equal(RelayAction.ReplyToSender, decision.Action);
        Assert.Equal(RelayOutcome.CodeClaimed, decision.Outcome);
        Assert.Equal(WireMessageType.CodeAccepted, decision.Reply!.Type);
    }

    [Fact]
    public void ACodeRequestForALiveCodeIsRefusedSoTheHostRegenerates()
    {
        _router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");

        var decision = _router.Route(WireEnvelope.ForCodeRequest(Code), "host-2");

        Assert.Equal(RelayOutcome.CodeAlreadyLive, decision.Outcome);
        Assert.Equal(WireMessageType.CodeRefused, decision.Reply!.Type);
    }

    /// <summary>
    /// The narrowing the Engineering Lead confirmed: a join request reaches the host and nobody
    /// else, so a joiner's public key and the fact of an attempt stay off other members' wires.
    /// </summary>
    [Fact]
    public void AJoinRequestGoesToTheHostAndNobodyElse()
    {
        _router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");

        var decision = _router.Route(WireEnvelope.ForJoinRequest(Code, [1, 2, 3]), "joiner-1");

        Assert.Equal(RelayAction.Forward, decision.Action);
        Assert.Equal(RelayOutcome.JoinForwardedToHost, decision.Outcome);
        Assert.Equal(["host-1"], decision.Recipients);
    }

    /// <summary>
    /// R-1.8 requires the plugin to distinguish "that session code is not active" from a broken
    /// connection rather than showing a spinner, and the relay staying silent would make that
    /// impossible. CodeRefused is the only negative this protocol has; the gap is reported.
    /// </summary>
    [Fact]
    public void AJoinRequestForADeadCodeIsAnsweredRatherThanIgnored()
    {
        var decision = _router.Route(WireEnvelope.ForJoinRequest(Code, [1, 2, 3]), "joiner-1");

        Assert.Equal(RelayAction.ReplyToSender, decision.Action);
        Assert.Equal(RelayOutcome.SessionNotFound, decision.Outcome);
        Assert.Equal(WireMessageType.CodeRefused, decision.Reply!.Type);
    }

    [Fact]
    public void APayloadReachesTheOtherMembersOfItsSession()
    {
        _router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");
        _router.Route(WireEnvelope.ForJoinRequest(Code, [1, 2, 3]), "joiner-1");
        _registry.TryAdmit(Code.Value, [1, 2, 3], out _);

        var decision = _router.Route(SomePayload(), "host-1");

        Assert.Equal(RelayAction.Forward, decision.Action);
        Assert.Equal(RelayOutcome.PayloadForwarded, decision.Outcome);
        Assert.Equal(["joiner-1"], decision.Recipients);
    }

    /// <summary>
    /// The gate, R-1.3b: a joiner waiting on the DM is not routed into anything. It receives no
    /// session traffic and originates none, and encryption is not what stops it — not sending is.
    /// </summary>
    [Fact]
    public void APendingJoinerIsRoutedNothingAndCanSendNothing()
    {
        _router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");
        _router.Route(WireEnvelope.ForJoinRequest(Code, [1, 2, 3]), "joiner-1");

        var fromHost = _router.Route(SomePayload(), "host-1");
        Assert.Empty(fromHost.Recipients);

        var fromPending = _router.Route(SomePayload(), "joiner-1");
        Assert.Equal(RelayAction.Drop, fromPending.Action);
        Assert.Equal(RelayOutcome.SenderNotAdmitted, fromPending.Outcome);
    }

    [Fact]
    public void APayloadFromAConnectionInNoSessionIsDropped()
    {
        _router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");

        var decision = _router.Route(SomePayload(), "stranger");

        Assert.Equal(RelayAction.Drop, decision.Action);
        Assert.Equal(RelayOutcome.SenderNotInSession, decision.Outcome);
    }

    /// <summary>
    /// A session code that is not six characters of the R-1.2a alphabet never reaches the router:
    /// <see cref="EnvelopeCodec"/> refuses to decode it at all.
    /// </summary>
    /// <remarks>
    /// Asserted here rather than on the router because this is where the behaviour actually lives.
    /// An earlier version of this test drove a rewrapped envelope into <c>Route</c> and expected a
    /// <c>MalformedSessionCode</c> drop; C6 moved the validation into the codec, which made that
    /// arm unreachable from any real receive path. The router still refuses an unparseable code —
    /// it has to turn a string into a <see cref="SessionCode"/> somehow, and refusing is the only
    /// alternative to throwing — but the defence that fires in production is this one.
    /// </remarks>
    [Fact]
    public void AMalformedSessionCodeIsRejectedBeforeTheRouterSeesIt()
    {
        var wire = Encoding.UTF8.GetBytes("""{"Type":1,"SessionCode":"AEIOU1"}""");

        Assert.False(EnvelopeCodec.TryDecode(wire, out var envelope));
        Assert.Null(envelope);
    }

    /// <summary>
    /// A client cannot speak as the relay. The plugin never sends these, so one arriving means
    /// something is hand-rolling the protocol, and the relay must not pass it on as arbitration.
    /// </summary>
    [Fact]
    public void AClientCannotSendTheRelaysOwnAnswers()
    {
        _router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");

        var decision = _router.Route(WireEnvelope.ForCodeAccepted(Code), "impostor");

        Assert.Equal(RelayAction.Drop, decision.Action);
        Assert.Equal(RelayOutcome.RelayOnlyMessageFromClient, decision.Outcome);
    }

    private static WireEnvelope SomePayload() =>
        WireEnvelope.ForSessionPayload(Code, SealedPayload.FromWire(new byte[12], [9, 9, 9]));
}
