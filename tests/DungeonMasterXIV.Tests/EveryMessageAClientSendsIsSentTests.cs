using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.12a: for every message the protocol requires a client to send, a test fails when it is not
/// sent.
/// </summary>
/// <remarks>
/// <para>
/// <b>This replaces an enumeration with a universal, and the enumeration is what failed.</b> A-1.12
/// named three things to unit-test — code generation, roster transitions, grace expiry — and none of
/// them was "the joiner sends a join request". BUG-40 was that omission: <c>ForJoinRequest</c> had no
/// production call site, nobody could join, and a full green suite said nothing about it. BUG-36 was
/// the same defect on the host's side an evening earlier. <b>An invariant that lists a world which
/// grows fails silently in the direction of passing.</b>
/// </para>
/// <para>
/// <b>So the vocabulary is derived, not listed.</b> The cases come from
/// <see cref="WireMessageType"/> itself, and
/// <see cref="EveryMessageTypeIsClassified"/> fails on any value this file has not accounted for.
/// A message added next month is covered without anyone remembering to extend anything — that is
/// the property A-1.12a asks for, and the reason a plain list of trigger tests would not satisfy it
/// however complete it looked today.
/// </para>
/// <para>
/// <b>Each trigger drives the production entry point, never the envelope factory.</b> A test that
/// constructs <c>WireEnvelope.ForJoinRequest</c> and checks the wire accepts it passes on the build
/// BUG-40 describes: the factory was always correct and nothing called it. What is asserted here is
/// that doing the thing a user does puts the message on the transport.
/// </para>
/// </remarks>
public class EveryMessageAClientSendsIsSentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 2, 0, 0, TimeSpan.Zero);

    /// <summary>How a message gets sent, or why no client sends it. Every type needs one.</summary>
    private enum Origin
    {
        /// <summary>A client sends it, and <see cref="Trigger"/> is the production path that does.</summary>
        Client,

        /// <summary>The relay sends it. A client sending one would be something hand-rolled.</summary>
        Relay,

        /// <summary>Never sent by anyone — see the reason.</summary>
        Never,

        /// <summary>
        /// A client will have to send it, and no feature produces one yet. <b>Loud on purpose.</b>
        /// </summary>
        NotYetReachable,
    }

    private sealed record Classification(Origin Origin, string Reason, Action<Session>? Trigger = null);

    // The whole vocabulary, accounted for. Adding a WireMessageType without adding a row here fails
    // EveryMessageTypeIsClassified by name -- which is the only reason this dictionary is allowed to
    // exist at all. It cannot go stale quietly.
    private static readonly Dictionary<WireMessageType, Classification> Expected = new()
    {
        [WireMessageType.Unknown] = new(
            Origin.Never,
            "the deserializer's name for a type this build does not recognise; never constructed to send"),

        [WireMessageType.CodeRequest] = new(
            Origin.Client, "the host claims its code (R-1.2a)",
            s => { s.Coordinator.StartHosting(); s.Ready(); s.Coordinator.Tick(TimeSpan.Zero, Now); }),

        [WireMessageType.CodeAccepted] = new(
            Origin.Relay, "the relay arbitrates the code namespace; a client sending one is laundering"),

        [WireMessageType.CodeRefused] = new(
            Origin.Relay, "as CodeAccepted -- the relay's own answer"),

        [WireMessageType.JoinRequest] = new(
            Origin.Client, "the joiner asks to be admitted (R-1.3). BUG-40: this had no call site",
            s =>
            {
                s.Coordinator.RequestJoin(SessionCode.FromValid("BCDFGH"));
                s.Ready();
                s.Coordinator.Tick(TimeSpan.Zero, Now);
            }),

        [WireMessageType.JoinPending] = new(
            Origin.Client, "the host sends its key before deciding (R-1.3a-i)",
            s => { s.Hosting(); s.Coordinator.ReceiveJoinRequest("PEER-1", s.JoinerKey, Now); }),

        [WireMessageType.JoinAccepted] = new(
            Origin.Client, "the host admits (R-1.3b)",
            s =>
            {
                s.Hosting();
                s.Coordinator.ReceiveJoinRequest("PEER-1", s.JoinerKey, Now);
                s.Coordinator.Admit("PEER-1");
            }),

        [WireMessageType.JoinDenied] = new(
            Origin.Client, "the host refuses, explicitly rather than by silence (R-1.3b)",
            s =>
            {
                s.Hosting();
                s.Coordinator.ReceiveJoinRequest("PEER-1", s.JoinerKey, Now);
                s.Coordinator.Deny("PEER-1");
            }),

        [WireMessageType.JoinLapsed] = new(
            Origin.Client, "the window closed and nobody looked -- never reported as a denial (R-1.3c)",
            s =>
            {
                s.Hosting();
                s.Coordinator.ReceiveJoinRequest("PEER-1", s.JoinerKey, Now);
                s.Coordinator.Tick(TimeSpan.Zero, Now + TimeSpan.FromHours(1));
            }),

        [WireMessageType.JoinerCanCompare] = new(
            Origin.Client,
            "the joiner reports it holds the host key and can render a fingerprint (R-1.3a-iii)",
            s =>
            {
                s.Coordinator.RequestJoin(SessionCode.FromValid("BCDFGH"));
                s.Ready();
                s.Coordinator.Tick(TimeSpan.Zero, Now);

                // The receipt is gated on a fingerprint EXISTING, so the host key has to arrive
                // first -- which is the whole point of it being a receipt rather than a declaration.
                s.Coordinator.Tick(TimeSpan.Zero, Now);
                s.HostKeyArrives();
                s.Coordinator.Tick(TimeSpan.Zero, Now);
            }),

        [WireMessageType.SessionPayload] = new(
            Origin.NotYetReachable,
            "NOTHING SEALS A PAYLOAD. SessionCipher.Seal has no production caller, so no client can "
            + "emit session traffic -- the encrypted path D-11 exists for has never run outside a "
            + "test. Becomes Origin.Client the moment any shared-state feature ships; R-1.3f's "
            + "roster is the first that will need it"),
    };

    // THE UNIVERSAL. Fails by name on any WireMessageType this file does not account for, which is
    // what stops the dictionary above from becoming the enumeration A-1.12 was rewritten to remove.
    [Fact]
    public void EveryMessageTypeIsClassified()
    {
        var unaccounted = Enum.GetValues<WireMessageType>()
            .Where(type => !Expected.ContainsKey(type))
            .ToList();

        Assert.True(
            unaccounted.Count == 0,
            $"WireMessageType has {unaccounted.Count} value(s) this file does not account for: "
            + $"{string.Join(", ", unaccounted)}. Add a row to Expected saying who sends it. If a "
            + "client sends it, give it a trigger that drives the production path -- A-1.12a is why.");
    }

    // A-1.12a itself. Fails when a client-sent message stops being sent, which is exactly what
    // BUG-36 and BUG-40 were: correct factories with no caller.
    [Theory]
    [MemberData(nameof(ClientSent))]
    public void DoingTheThingSendsTheMessage(WireMessageType type)
    {
        var session = new Session();

        Expected[type].Trigger!(session);

        Assert.Contains(type, session.Sent);
    }

    // THE CONTROL, and it must not depend on any row above. The first version of it drove
    // StartHosting and asserted "something was sent" -- so suppressing the CodeRequest send failed
    // the control too, and it could not tell "the harness is broken" from "CodeRequest is missing".
    // A control that fails alongside its subject is not a control. Measured rather than reasoned:
    // injecting BUG-36 reddened both, which is how this was found.
    //
    // This one asks only whether the fixture can carry and decode a frame, using no production send
    // path at all.
    [Fact]
    public void TheHarnessRecordsAFrameAndDecodesIt()
    {
        var transport = new RecordingTransport { OpenTheSocket = true };
        transport.Connect(new Uri(RelayEndpoint.Default));

        transport.Send(EnvelopeCodec.Encode(
            WireEnvelope.ForCodeRequest(SessionCode.FromValid("BCDFGH"))));

        var single = Assert.Single(transport.Sent);
        Assert.True(EnvelopeCodec.TryDecode(single, out var decoded));
        Assert.Equal(WireMessageType.CodeRequest, decoded!.Type);
    }

    // The other half of the fixture, and the reason a naive double would have hidden BUG-36: a frame
    // sent before the socket opens must be discarded here exactly as the real transport discards it.
    // A fake that accepted everything would pass a build that sends too early.
    [Fact]
    public void TheHarnessDiscardsAFrameSentBeforeTheSocketOpens()
    {
        var transport = new RecordingTransport();
        transport.Connect(new Uri(RelayEndpoint.Default));

        transport.Send(EnvelopeCodec.Encode(
            WireEnvelope.ForCodeRequest(SessionCode.FromValid("BCDFGH"))));

        Assert.Empty(transport.Sent);
    }

    // Fails if a message is parked as unreachable without saying what must exist first. The category
    // is a place to be honest about an unbuilt path, not a place to hide one -- so it has to name
    // its own exit condition.
    [Theory]
    [MemberData(nameof(NotYetReachable))]
    public void AnUnreachableMessageSaysWhatWouldMakeItReachable(WireMessageType type, string reason)
    {
        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.True(
            reason.Length > 40,
            $"{type} is parked as not-yet-reachable with a reason too short to be an account of it.");
    }

    public static TheoryData<WireMessageType> ClientSent()
    {
        var data = new TheoryData<WireMessageType>();
        foreach (var (type, how) in Expected.Where(e => e.Value.Origin == Origin.Client))
        {
            Assert.True(how.Trigger is not null, $"{type} is client-sent and has no trigger.");
            data.Add(type);
        }

        return data;
    }

    public static TheoryData<WireMessageType, string> NotYetReachable()
    {
        var data = new TheoryData<WireMessageType, string>();
        foreach (var (type, how) in Expected.Where(e => e.Value.Origin == Origin.NotYetReachable))
        {
            data.Add(type, how.Reason);
        }

        return data;
    }

    /// <summary>A real coordinator over a transport that records what left the machine.</summary>
    private sealed class Session
    {
        private readonly RecordingTransport _transport = new();

        public Session() =>
            Coordinator = new SessionCoordinator(_transport, () => RelayEndpoint.Default);

        public SessionCoordinator Coordinator { get; }

        public byte[] JoinerKey { get; } = new SessionKeyExchange().PublicKey;

        public IReadOnlyList<WireMessageType> Sent => _transport.Sent
            .Select(bytes => EnvelopeCodec.TryDecode(bytes, out var e) ? e!.Type : WireMessageType.Unknown)
            .ToList();

        /// <summary>The socket finished opening. Sending before this is discarded (BUG-36).</summary>
        public void Ready() => _transport.OpenTheSocket = true;

        /// <summary>The host answers with its key, so this client can render a fingerprint.</summary>
        public void HostKeyArrives() =>
            _transport.Deliver(WireEnvelope.ForJoinPending(
                SessionCode.FromValid("BCDFGH"),
                Coordinator.JoinerKeys!.PublicKey,
                new SessionKeyExchange().PublicKey,
                AdmissionDeadline.DecidedByHost(Now)));

        /// <summary>A host with a code the relay has confirmed.</summary>
        public void Hosting()
        {
            Coordinator.StartHosting();
            Ready();
            Coordinator.Host.Registered();
        }
    }

    private sealed class RecordingTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public List<byte[]> Sent { get; } = new();

        public bool OpenTheSocket { get; set; }

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected && OpenTheSocket;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
            // Mirrors the real transport, which discards a frame sent before the socket opens.
            // A fake that accepted everything would pass a build that sends too early.
            if (IsReadyToSend)
            {
                Sent.Add(envelope);
            }
        }

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
