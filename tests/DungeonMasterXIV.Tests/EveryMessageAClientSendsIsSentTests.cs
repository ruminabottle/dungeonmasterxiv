using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.12a: for every message the protocol requires a client to send, a test fails when it is not
/// sent.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file has now been wrong twice in the same direction, and the second time is the
/// interesting one.</b> A-1.12 enumerated three things to unit-test and missed a fourth, which
/// shipped as BUG-40. The first version of this file replaced that enumeration with a universal over
/// <see cref="WireMessageType"/> — and was itself blind, because
/// <c>ForRelinkRequest</c> returns <see cref="WireMessageType.JoinRequest"/>, the same type an
/// ordinary join uses. Relink appeared in no bucket at all, <c>JoinRequest</c> was already
/// classified and already sent, and the suite stayed green while R-1.5 relink was unreachable.
/// </para>
/// <para>
/// <b>Deriving instead of enumerating does not save you if the thing you derive from is coarser
/// than the thing you are covering.</b> The world that grows is protocol <i>actions</i>, and two
/// actions can share one message type. So the vocabulary here is
/// <see cref="WireEnvelope"/>'s public static factories — one per action — and each row carries a
/// predicate that identifies <i>that factory's</i> output on the wire rather than merely its type.
/// A plain join and a relink claim are told apart by <see cref="WireEnvelope.ClaimedParticipantId"/>,
/// which is the field that distinguishes them.
/// </para>
/// <para>
/// <b>Each trigger drives the production entry point, never the factory.</b> A test that constructs
/// <c>ForJoinRequest</c> and checks the wire accepts it passes on the build BUG-40 describes: the
/// factory was always correct and nothing called it.
/// </para>
/// </remarks>
public class EveryMessageAClientSendsIsSentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 2, 0, 0, TimeSpan.Zero);

    /// <summary>Who sends what this factory builds. Every factory needs one.</summary>
    private enum Origin
    {
        /// <summary>A client sends it, and <see cref="Classification.Trigger"/> is the path that does.</summary>
        Client,

        /// <summary>The relay sends it. A client sending one would be something hand-rolled.</summary>
        Relay,

        /// <summary>
        /// A client will have to send it and no feature produces one yet. <b>Loud on purpose</b>, and
        /// <b>not a place to park an unmet requirement</b> — see the note on relink below.
        /// </summary>
        NotYetReachable,
    }

    private sealed record Classification(
        Origin Origin,
        string Reason,
        Action<Session>? Trigger = null,
        Func<WireEnvelope, bool>? Matches = null);

    // Keyed on FACTORY NAME, not message type -- that was the defect. Adding a public static factory
    // to WireEnvelope without adding a row fails EveryFactoryIsClassified by name, which is the only
    // reason this table is allowed to exist.
    private static readonly Dictionary<string, Classification> Expected = new(StringComparer.Ordinal)
    {
        [nameof(WireEnvelope.ForCodeRequest)] = new(
            Origin.Client, "the host claims its code (R-1.2a). BUG-36: this had no call site",
            s => { s.Coordinator.StartHosting(); s.Ready(); s.Coordinator.Tick(TimeSpan.Zero, Now); },
            e => e.Type == WireMessageType.CodeRequest),

        [nameof(WireEnvelope.ForCodeAccepted)] = new(
            Origin.Relay, "the relay arbitrates the code namespace; a client sending one is laundering"),

        [nameof(WireEnvelope.ForCodeRefused)] = new(
            Origin.Relay, "as CodeAccepted -- the relay's own answer"),

        // Matched on the ABSENCE of a claim, so this row cannot be satisfied by a relink and the
        // relink row cannot be satisfied by a plain join. Before this file derived over factories,
        // one assertion covered both and the weaker one was doing all the work.
        [nameof(WireEnvelope.ForJoinRequest)] = new(
            Origin.Client, "the joiner asks to be admitted (R-1.3). BUG-40: this had no call site",
            s =>
            {
                s.Coordinator.RequestJoin(SessionCode.FromValid("BCDFGH"));
                s.Ready();
                s.Coordinator.Tick(TimeSpan.Zero, Now);
            },
            e => e.Type == WireMessageType.JoinRequest && e.ClaimedParticipantId is null),

        // EXPECTED TO FAIL ON TODAY'S MAIN, and deliberately NOT parked in NotYetReachable.
        //
        // R-1.5 requires a returning client to be able to claim a participant it believes is its
        // own. Nothing constructs one: ForRelinkRequest has no production call site, so a returning
        // player is indistinguishable from a stranger. That is an UNMET REQUIREMENT, not an
        // unbuilt-by-design path, and the difference is the whole reason NotYetReachable must not
        // absorb it -- a category for being honest about what is not built becomes a place to hide
        // what should be the moment it accepts something the product already requires.
        //
        // GREEN SINCE DMXENG-1 (#141), AND THE TRIGGER HAD TO CHANGE FOR IT -- said plainly, because
        // "the row went green when the feature landed" would be a nicer story and is not what
        // happened. Until #141 nothing in the product could produce a claim: the joiner had no
        // storage, so passing one here would have fabricated a state production could not reach,
        // which is why this trigger deliberately called the ONE-ARGUMENT overload and this row
        // deliberately failed.
        //
        // Now a claim exists -- JoinFlowView reads it from RelinkMemory and passes it to the
        // three-argument RequestJoin -- so driving that same entry point with one is no longer a
        // fabrication. IT IS THE PRODUCTION PATH, one layer below the window this assembly cannot
        // reference.
        //
        // WHAT THIS STILL MEASURES, and it is the original defect: BUG-41 was the middle overload
        // NULLING the claim on its way through. Break that again and this row fails again, because
        // the predicate reads the WIRE and not the argument.
        //
        // WHAT IT NO LONGER MEASURES: that the UI supplies one. That is JoinFlowView, in the plugin
        // project, and it is covered by TheJoinerRemembersWhoItIsTests instead.
        [nameof(WireEnvelope.ForRelinkRequest)] = new(
            Origin.Client, "a returning client claims its participant (R-1.5)",
            s =>
            {
                s.Coordinator.RequestJoin(
                    SessionCode.FromValid("BCDFGH"), DisplayName.None, Guid.NewGuid());
                s.Ready();
                s.Coordinator.Tick(TimeSpan.Zero, Now);
            },
            e => e.Type == WireMessageType.JoinRequest && e.ClaimedParticipantId is not null),

        [nameof(WireEnvelope.ForJoinPending)] = new(
            Origin.Client, "the host sends its key before deciding (R-1.3a-i)",
            s => { s.Hosting(); s.Coordinator.ReceiveJoinRequest(PeerCodes.Of("PRBCD2"), s.JoinerKey, Now); },
            e => e.Type == WireMessageType.JoinPending),

        [nameof(WireEnvelope.ForJoinAccepted)] = new(
            Origin.Client, "the host admits (R-1.3b)",
            s =>
            {
                s.Hosting();
                s.Coordinator.ReceiveJoinRequest(PeerCodes.Of("PRBCD2"), s.JoinerKey, Now);
                s.Coordinator.Admit(PeerCodes.Of("PRBCD2"));
            },
            e => e.Type == WireMessageType.JoinAccepted),

        [nameof(WireEnvelope.ForJoinDenied)] = new(
            Origin.Client, "the host refuses, explicitly rather than by silence (R-1.3b)",
            s =>
            {
                s.Hosting();
                s.Coordinator.ReceiveJoinRequest(PeerCodes.Of("PRBCD2"), s.JoinerKey, Now);
                s.Coordinator.Deny(PeerCodes.Of("PRBCD2"));
            },
            e => e.Type == WireMessageType.JoinDenied),

        [nameof(WireEnvelope.ForJoinLapsed)] = new(
            Origin.Client, "the window closed and nobody looked -- never reported as a denial (R-1.3c)",
            s =>
            {
                s.Hosting();
                s.Coordinator.ReceiveJoinRequest(PeerCodes.Of("PRBCD2"), s.JoinerKey, Now);
                s.Coordinator.Tick(TimeSpan.Zero, Now + TimeSpan.FromHours(1));
            },
            e => e.Type == WireMessageType.JoinLapsed),

        // Carried forward from main during the rebase, re-keyed to its FACTORY. It arrived on main
        // (#88/#92) after this branch was cut; dropping it would lose coverage that already existed,
        // and EveryFactoryIsClassified would fail by name for the missing factory regardless.
        [nameof(WireEnvelope.ForJoinerHoldsFingerprint)] = new(
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
            },
            e => e.Type == WireMessageType.JoinerHoldsFingerprint),

        // Genuinely unbuilt rather than unmet: no feature produces session state to share, so there
        // is nothing that SHOULD be sending one today. Contrast with relink above.
        [nameof(WireEnvelope.ForSessionPayload)] = new(
            Origin.NotYetReachable,
            "NOTHING SEALS A PAYLOAD. SessionCipher.Seal has no production caller, so no client can "
            + "emit session traffic -- the encrypted path D-11 exists for has never run outside a "
            + "test. Unlike relink, no requirement is unmet: nothing yet produces shared state. "
            + "Becomes Origin.Client the moment one does; R-1.3f's roster is the first that will"),
    };

    /// <summary>Every public static factory on <see cref="WireEnvelope"/>, by name.</summary>
    private static IEnumerable<string> FactoryNames() =>
        typeof(WireEnvelope)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(WireEnvelope))
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal);

    // THE UNIVERSAL, now over the right artefact. Fails by name on any factory this file does not
    // account for. Overloads collapse to one row on purpose: ForJoinRequest's two overloads are one
    // protocol action seen with and without a deadline.
    [Fact]
    public void EveryFactoryIsClassified()
    {
        var unaccounted = FactoryNames().Where(name => !Expected.ContainsKey(name)).ToList();

        Assert.True(
            unaccounted.Count == 0,
            $"WireEnvelope has {unaccounted.Count} public factory/factories this file does not "
            + $"account for: {string.Join(", ", unaccounted)}. Add a row saying who sends it. If a "
            + "client sends it, give it a trigger that drives the production path AND a predicate "
            + "that identifies its output -- keying on message type alone is what let relink hide.");
    }

    // Guards the correction itself. Keying on type is the defect this ticket exists to fix, so a
    // future edit that drops a predicate must not silently fall back to a type-only match.
    [Fact]
    public void EveryClientFactoryHasBothATriggerAndAPredicate()
    {
        foreach (var (name, how) in Expected.Where(e => e.Value.Origin == Origin.Client))
        {
            Assert.True(how.Trigger is not null, $"{name} is client-sent and has no trigger.");
            Assert.True(how.Matches is not null, $"{name} is client-sent and has no predicate.");
        }
    }

    // A-1.12a itself, per FACTORY rather than per message type.
    //
    // A RED ROW HERE IS AMBIGUOUS ON ITS OWN, AND APlainJoinAndARelinkClaimAreNotInterchangeable IS
    // WHAT RESOLVES IT. "The ForRelinkRequest row is red" has two readings: the send does not happen,
    // or the relink PREDICATE matches nothing -- and the second would hold even if relink were being
    // sent correctly. That test excludes the second reading by proving the two predicates are not
    // interchangeable, so only the first survives. It does not sit beside this test; IT LICENSES
    // THIS TEST'S RESULT.
    //
    // Which is why A-1.12a's demonstration -- "for any two distinct actions, show a run where one is
    // absent and the check reddens WHILE THE OTHER IS PRESENT" -- is discharged by the pair and not
    // by either alone. This Theory supplies the run: ForRelinkRequest red and ForJoinRequest green
    // in one execution, each row driving the production entry point. The other supplies the reason
    // that contrast means what it appears to mean.
    //
    // THE DEPENDENCY IS WRITTEN DOWN BECAUSE IT IS INVISIBLE FROM EITHER SIDE. Read this Theory and
    // you see a red row; read the other and you see predicate hygiene. Neither says the first
    // depends on the second, and that is exactly the kind of relationship that lives in nobody's
    // head six weeks from now. Anyone weakening, merging or deleting that test is not tidying a
    // neighbour -- they are removing the thing that makes this row's failure interpretable.
    [Theory]
    [MemberData(nameof(ClientSent))]
    public void DoingTheThingSendsTheMessage(string factory)
    {
        var session = new Session();
        var how = Expected[factory];

        how.Trigger!(session);

        Assert.True(
            session.Sent.Any(how.Matches!),
            $"Nothing a client does produced the envelope {factory} builds. {how.Reason}. "
            + $"What reached the transport: {Describe(session.Sent)}.");
    }

    // THE CONTROL, and it must not depend on any row above. An earlier version drove StartHosting
    // and asserted "something was sent", so suppressing the CodeRequest send failed the control too
    // -- it could not tell "the harness is broken" from "CodeRequest is missing". Measured, not
    // reasoned: injecting BUG-36 reddened both. This asks only whether the fixture can carry and
    // decode a frame, using no production send path.
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

    // The other half of the fixture, and why a naive double would have hidden BUG-36: a frame sent
    // before the socket opens must be discarded here exactly as the real transport discards it.
    [Fact]
    public void TheHarnessDiscardsAFrameSentBeforeTheSocketOpens()
    {
        var transport = new RecordingTransport();
        transport.Connect(new Uri(RelayEndpoint.Default));

        transport.Send(EnvelopeCodec.Encode(
            WireEnvelope.ForCodeRequest(SessionCode.FromValid("BCDFGH"))));

        Assert.Empty(transport.Sent);
    }

    // The predicates must tell the two JoinRequest actions apart, or the relink row is satisfiable
    // by a plain join and this whole correction achieves nothing. Asserted against constructed
    // envelopes because it is a property of the PREDICATES, not of the production path.
    //
    // THIS IS LOAD-BEARING FOR DoingTheThingSendsTheMessage AND READS LIKE HYGIENE. That Theory's
    // red ForRelinkRequest row means "the send does not happen" ONLY because this test rules out
    // "the relink predicate matches nothing" -- which would redden the same row on a build where
    // relink was sent correctly. Weakening, merging or deleting this does not tidy a neighbour; it
    // makes that row's failure uninterpretable, and A-1.12a's demonstration goes with it.
    [Fact]
    public void APlainJoinAndARelinkClaimAreNotInterchangeable()
    {
        var code = SessionCode.FromValid("BCDFGH");
        var key = new SessionKeyExchange().PublicKey;
        var plain = WireEnvelope.ForJoinRequest(code, key);
        var relink = WireEnvelope.ForRelinkRequest(code, key, Guid.NewGuid());

        var plainRow = Expected[nameof(WireEnvelope.ForJoinRequest)].Matches!;
        var relinkRow = Expected[nameof(WireEnvelope.ForRelinkRequest)].Matches!;

        Assert.True(plainRow(plain));
        Assert.False(plainRow(relink));
        Assert.True(relinkRow(relink));
        Assert.False(relinkRow(plain));
    }

    // A message parked as unreachable has to name what would make it reachable. The category is a
    // place to be honest about an unbuilt path, not a place to hide an unmet requirement.
    [Theory]
    [MemberData(nameof(NotYetReachable))]
    public void AnUnreachableMessageSaysWhatWouldMakeItReachable(string factory, string reason)
    {
        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.True(
            reason.Length > 40,
            $"{factory} is parked as not-yet-reachable with a reason too short to be an account.");
    }

    public static TheoryData<string> ClientSent()
    {
        var data = new TheoryData<string>();
        foreach (var name in Expected.Where(e => e.Value.Origin == Origin.Client).Select(e => e.Key))
        {
            data.Add(name);
        }

        return data;
    }

    public static TheoryData<string, string> NotYetReachable()
    {
        var data = new TheoryData<string, string>();
        foreach (var (name, how) in Expected.Where(e => e.Value.Origin == Origin.NotYetReachable))
        {
            data.Add(name, how.Reason);
        }

        return data;
    }

    private static string Describe(IReadOnlyList<WireEnvelope> sent) =>
        sent.Count == 0
            ? "nothing"
            : string.Join(", ", sent.Select(e =>
                e.ClaimedParticipantId is null ? $"{e.Type}" : $"{e.Type}+claim"));

    /// <summary>A real coordinator over a transport that records what left the machine.</summary>
    private sealed class Session
    {
        private readonly RecordingTransport _transport = new();

        public Session() =>
            Coordinator = new SessionCoordinator(_transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);

        public SessionCoordinator Coordinator { get; }

        public byte[] JoinerKey { get; } = new SessionKeyExchange().PublicKey;

        public IReadOnlyList<WireEnvelope> Sent => _transport.Sent
            .Select(bytes => EnvelopeCodec.TryDecode(bytes, out var e) ? e : null)
            .Where(e => e is not null)
            .Select(e => e!)
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
            if (IsReadyToSend)
            {
                Sent.Add(envelope);
            }
        }

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
