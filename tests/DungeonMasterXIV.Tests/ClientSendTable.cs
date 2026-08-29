using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Every public factory on <see cref="WireEnvelope"/>, who sends what it builds, how to drive that
/// send through production, and how to recognise its output on the wire.
/// </summary>
/// <remarks>
/// <para>
/// <b>The protocol VOCABULARY, separated from the assertions made over it.</b> Two test classes now
/// read this — one asking whether every action is SENT, one asking whether the predicates TELL
/// ACTIONS APART — and a table shared by two subjects belongs to neither.
/// </para>
/// <para>
/// <b>The seam was forced by the size limit and turned out to be real, which is worth saying in that
/// order.</b> The test class was 408 lines against a 400 block BEFORE DMXENG-40 — already breaching,
/// invisibly, until the sizes tool learned to measure classes. So this file exists because a limit
/// found it, not because anyone designed it.
/// </para>
/// <para>
/// <b>Keyed on FACTORY NAME, not message type — that was the original defect.</b>
/// <c>ForRelinkRequest</c> returns <see cref="WireMessageType.JoinRequest"/>, the same type an
/// ordinary join uses, so relink appeared in no bucket at all and the suite stayed green while R-1.5
/// was unreachable. Two actions can share one message type; the world that grows is ACTIONS.
/// </para>
/// </remarks>
internal static class ClientSendTable
{
    private static readonly DateTimeOffset Now = ClientSendHarness.Now;

    /// <summary>Who sends what this factory builds. Every factory needs one.</summary>
    internal enum Origin
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

    internal sealed record Classification(
        Origin Origin,
        string Reason,
        Action<ClientSendHarness.Session>? Trigger = null,
        Func<WireEnvelope, bool>? Matches = null,
        Func<WireEnvelope>? Sample = null);

    // Keyed on FACTORY NAME, not message type -- that was the defect. Adding a public static factory
    // to WireEnvelope without adding a row fails EveryFactoryIsClassified by name, which is the only
    // reason this table is allowed to exist.
    // ONE REPRESENTATIVE ENVELOPE PER CLIENT ROW, built by the REAL factory rather than hand-shaped.
    // A hand-built envelope would let a predicate pass against something the product never emits,
    // which is the same defect one layer down from the one this file was written to fix.
    internal static readonly SessionCode SampleCode = SessionCode.FromValid("BCDFGH");
    internal static readonly byte[] SampleKey = new SessionKeyExchange().PublicKey;
    internal static readonly byte[] SampleHostKey = new SessionKeyExchange().PublicKey;
    internal static readonly Guid SampleParticipant = Guid.NewGuid();

    internal static readonly Dictionary<string, Classification> Expected = new(StringComparer.Ordinal)
    {
        [nameof(WireEnvelope.ForCodeRequest)] = new(
            Origin.Client, "the host claims its code (R-1.2a). BUG-36: this had no call site",
            s => { s.Coordinator.StartHosting(); s.Ready(); s.Coordinator.Tick(TimeSpan.Zero, Now); },
            e => e.Type == WireMessageType.CodeRequest,
            Sample: () => WireEnvelope.ForCodeRequest(SampleCode)),

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
            e => e.Type == WireMessageType.JoinRequest && e.ClaimedParticipantId is null,
            Sample: () => WireEnvelope.ForJoinRequest(SampleCode, SampleKey)),

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
        // project, and this assembly cannot reference the window type.
        //
        // It is covered by TheJoinFlowSuppliesTheRelinkClaimTests, which reads JoinFlowView.cs as
        // TEXT and asserts the call carries three arguments with a non-null third -- the narrow
        // thing that distinguishes the overload carrying the claim from the two that drop it.
        //
        // THIS SENTENCE PREVIOUSLY CITED TheJoinerRemembersWhoItIsTests AND THAT WAS FALSE (BUG-100).
        // That file tests RelinkMemory storage: no view, no join, no envelope. The citation was
        // written in good faith because AStoredParticipantIsWhatAJoinWouldCarry is named for a claim
        // its body does not make. AN UNCOVERED PATH READS AS UNCOVERED; AN UNCOVERED PATH WITH A
        // CITATION READS AS COVERED, which is why a wrong citation costs more than no citation.
        [nameof(WireEnvelope.ForRelinkRequest)] = new(
            Origin.Client, "a returning client claims its participant (R-1.5)",
            s =>
            {
                s.Coordinator.RequestJoin(
                    SessionCode.FromValid("BCDFGH"), DisplayName.None, Guid.NewGuid());
                s.Ready();
                s.Coordinator.Tick(TimeSpan.Zero, Now);
            },
            e => e.Type == WireMessageType.JoinRequest && e.ClaimedParticipantId is not null,
            Sample: () => WireEnvelope.ForRelinkRequest(SampleCode, SampleKey, SampleParticipant)),

        [nameof(WireEnvelope.ForJoinPending)] = new(
            Origin.Client, "the host sends its key before deciding (R-1.3a-i)",
            s => { s.Hosting(); s.Coordinator.ReceiveJoinRequest(PeerCodes.Of("PRBCD2"), s.JoinerKey, Now); },
            e => e.Type == WireMessageType.JoinPending,
            Sample: () => WireEnvelope.ForJoinPending(
                SampleCode, SampleKey, SampleHostKey, AdmissionDeadline.DecidedByHost(Now))),

        [nameof(WireEnvelope.ForJoinAccepted)] = new(
            Origin.Client, "the host admits (R-1.3b)",
            s =>
            {
                s.Hosting();
                s.Coordinator.ReceiveJoinRequest(PeerCodes.Of("PRBCD2"), s.JoinerKey, Now);
                s.Coordinator.Admit(PeerCodes.Of("PRBCD2"));
            },
            e => e.Type == WireMessageType.JoinAccepted,
            Sample: () => WireEnvelope.ForJoinAccepted(SampleCode, SampleKey, SampleHostKey)),

        [nameof(WireEnvelope.ForJoinDenied)] = new(
            Origin.Client, "the host refuses, explicitly rather than by silence (R-1.3b)",
            s =>
            {
                s.Hosting();
                s.Coordinator.ReceiveJoinRequest(PeerCodes.Of("PRBCD2"), s.JoinerKey, Now);
                s.Coordinator.Deny(PeerCodes.Of("PRBCD2"));
            },
            e => e.Type == WireMessageType.JoinDenied,
            Sample: () => WireEnvelope.ForJoinDenied(SampleCode, SampleKey)),

        [nameof(WireEnvelope.ForJoinLapsed)] = new(
            Origin.Client, "the window closed and nobody looked -- never reported as a denial (R-1.3c)",
            s =>
            {
                s.Hosting();
                s.Coordinator.ReceiveJoinRequest(PeerCodes.Of("PRBCD2"), s.JoinerKey, Now);
                s.Coordinator.Tick(TimeSpan.Zero, Now + TimeSpan.FromHours(1));
            },
            e => e.Type == WireMessageType.JoinLapsed,
            Sample: () => WireEnvelope.ForJoinLapsed(SampleCode, SampleKey)),

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
            e => e.Type == WireMessageType.JoinerHoldsFingerprint,
            Sample: () => WireEnvelope.ForJoinerHoldsFingerprint(SampleCode, SampleKey)),

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
    internal static IEnumerable<string> FactoryNames() =>
        typeof(WireEnvelope)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(WireEnvelope))
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal);}
