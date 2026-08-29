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


    // THE UNIVERSAL, now over the right artefact. Fails by name on any factory this file does not
    // account for. Overloads collapse to one row on purpose: ForJoinRequest's two overloads are one
    // protocol action seen with and without a deadline.
    [Fact]
    public void EveryFactoryIsClassified()
    {
        var unaccounted = ClientSendTable.FactoryNames().Where(name => !ClientSendTable.Expected.ContainsKey(name)).ToList();

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
        foreach (var (name, how) in ClientSendTable.Expected.Where(e => e.Value.Origin == ClientSendTable.Origin.Client))
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
        var session = new ClientSendHarness.Session();
        var how = ClientSendTable.Expected[factory];

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
        var transport = new ClientSendHarness.RecordingTransport { OpenTheSocket = true };
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
        var transport = new ClientSendHarness.RecordingTransport();
        transport.Connect(new Uri(RelayEndpoint.Default));

        transport.Send(EnvelopeCodec.Encode(
            WireEnvelope.ForCodeRequest(SessionCode.FromValid("BCDFGH"))));

        Assert.Empty(transport.Sent);
    }

    // A-1.12a's DEMONSTRATION, over every ordered pair rather than the one pair somebody wrote out.
    // The criterion asks that a check TELL TWO ACTIONS APART, and #75 showed that for exactly one
    // pair -- ForJoinRequest against ForRelinkRequest -- leaving SIX client rows whose predicate was
    // only ever shown to ACCEPT ITS OWN ENVELOPE. Passing is not discriminating.
    //
    // DERIVED, so the six are covered without being listed and a NINTH row is covered the day it is
    // added. That is the same correction this file already made once at the level of the vocabulary:
    // enumerate nothing you can derive, because the enumeration is what goes stale.
    //
    // WHAT A FAILURE HERE MEANS: two rows claim the same envelope, so whichever is checked first
    // satisfies both and one action can hide inside the other. That is BUG-40's shape exactly --
    // relink hid inside JoinRequest because one predicate answered for two actions.
    [Theory]
    [MemberData(nameof(DistinctClientPairs))]
    public void NoClientPredicateAcceptsAnotherActionsEnvelope(string predicateOwner, string sampleFrom)
    {
        var predicate = ClientSendTable.Expected[predicateOwner].Matches!;
        var somebodyElses = ClientSendTable.Expected[sampleFrom].Sample!();

        Assert.False(
            predicate(somebodyElses),
            $"{predicateOwner}'s predicate ACCEPTS the envelope {sampleFrom} produces, so those two "
            + "actions are not told apart: whichever row is checked first satisfies both, and the "
            + "other can stop being sent without this file noticing. That is BUG-40's shape -- "
            + "relink hid inside JoinRequest because one predicate answered for two actions.");
    }

    // The other half, and without it the theory above is satisfied by a predicate that accepts
    // NOTHING. "Rejects everyone else's" is trivially true of `_ => false`, which would also make
    // DoingTheThingSendsTheMessage red for every row -- but this file is where that would be
    // diagnosed, so it says so here rather than leaving the diagnosis to whoever is unlucky.
    [Theory]
    [MemberData(nameof(ClientFactories))]
    public void EveryClientPredicateAcceptsItsOwnEnvelope(string factory)
    {
        var how = ClientSendTable.Expected[factory];

        Assert.True(
            how.Matches!(how.Sample!()),
            $"{factory}'s predicate rejects the envelope its OWN factory produces. Every other "
            + "assertion about this row is now vacuous -- a predicate matching nothing rejects all "
            + "comers, which reads as perfect discrimination.");
    }

    // Guards the pair above the way EveryClientFactoryHasBothATriggerAndAPredicate guards the row:
    // a client row without a sample silently drops out of BOTH, and dropping out is indistinguishable
    // from passing when the data is derived from the table.
    [Fact]
    public void EveryClientFactoryHasASampleToBeToldApartBy()
    {
        foreach (var (name, how) in ClientSendTable.Expected.Where(e => e.Value.Origin == ClientSendTable.Origin.Client))
        {
            Assert.True(
                how.Sample is not null,
                $"{name} is client-sent and has no sample envelope, so nothing checks that its "
                + "predicate rejects other actions -- it would pass by being absent.");
        }
    }

    public static TheoryData<string> ClientFactories()
    {
        var data = new TheoryData<string>();
        foreach (var name in ClientNames())
        {
            data.Add(name);
        }

        return data;
    }

    public static TheoryData<string, string> DistinctClientPairs()
    {
        var data = new TheoryData<string, string>();
        foreach (var owner in ClientNames())
        {
            foreach (var other in ClientNames().Where(n => !string.Equals(n, owner, StringComparison.Ordinal)))
            {
                data.Add(owner, other);
            }
        }

        return data;
    }

    private static IEnumerable<string> ClientNames() =>
        ClientSendTable.Expected
            .Where(e => e.Value.Origin == ClientSendTable.Origin.Client && e.Value.Matches is not null && e.Value.Sample is not null)
            .Select(e => e.Key)
            .OrderBy(name => name, StringComparer.Ordinal);

    // KEPT THOUGH THE THEORY ABOVE NOW SUBSUMES ITS COVERAGE, because its value was never the
    // coverage. It is the test that LICENSES DoingTheThingSendsTheMessage's red ForRelinkRequest row
    // to be read as "the send does not happen" rather than "the predicate matches nothing", and that
    // reasoning is written down here and nowhere else. Deleting it as a duplicate would remove an
    // explanation, not a redundancy.
    //
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

        var plainRow = ClientSendTable.Expected[nameof(WireEnvelope.ForJoinRequest)].Matches!;
        var relinkRow = ClientSendTable.Expected[nameof(WireEnvelope.ForRelinkRequest)].Matches!;

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
        foreach (var name in ClientSendTable.Expected.Where(e => e.Value.Origin == ClientSendTable.Origin.Client).Select(e => e.Key))
        {
            data.Add(name);
        }

        return data;
    }

    public static TheoryData<string, string> NotYetReachable()
    {
        var data = new TheoryData<string, string>();
        foreach (var (name, how) in ClientSendTable.Expected.Where(e => e.Value.Origin == ClientSendTable.Origin.NotYetReachable))
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
}
