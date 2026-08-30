using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Every member of every type <see cref="SessionContentCodec"/> decodes is named in a vetting
/// decision, so a new one cannot arrive unconsidered.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE LIMIT, STATED FIRST BECAUSE IT IS THE EASIEST THING TO READ PAST.</b> A reflection test
/// proves every member is <b>NAMED</b> in a decision. It does not prove the decision is
/// <b>CORRECT</b>. If someone writes <c>Role: no vetting needed</c> the test goes green and
/// <c>Role</c> is still unvetted. It converts a silent omission into a recorded choice — which is
/// the whole value, and is strictly less than it sounds like.
/// </para>
/// <para>
/// <b>Why this shape rather than the two that were tried first.</b> Vetting fields one at a time is
/// an enumeration standing in for a universal: <c>DisplayName</c> was vetted after PR #86 was
/// denied, <c>PeerCode</c> after BUG-57, each one after its own defect. The two obvious replacements
/// do not work and both were measured rather than argued —
/// </para>
/// <list type="bullet">
/// <item><b>Refusing unknown fields at run time</b> contradicts D-14: the wire only grows and a peer
/// must ignore what it does not understand, so refusing would break forward compatibility by
/// design.</item>
/// <item><b>Forcing a compile error</b> by constructing entries positionally instead of with
/// <c>with</c> catches only a field added WITHOUT a default. A field added WITH one — which is the
/// D-14-shaped way to add a field — compiles either way and passes through unvetted. Measured by
/// adding a fourth member both ways.</item>
/// </list>
/// <para>
/// So the decision list lives here, in the test, and is checked against the type at run time — the
/// same construction as <c>SessionFailureMessageTests.EveryFailureHasASentenceSomebodyRead</c>,
/// which derives its coverage from <c>Enum.GetValues</c>. Unlike a golden-master pin, it can fail on
/// a member nobody thought to pin.
/// </para>
/// </remarks>
public class EveryDecodedMemberHasAVettingDecisionTests
{
    /// <summary>
    /// Every type <see cref="SessionContentCodec.TryDecode"/> can produce.
    /// </summary>
    /// <remarks>
    /// Three. The codec deserialises <see cref="SessionContent"/>, which carries a list of
    /// <see cref="RosterEntry"/> and a list of <see cref="StreamLine"/>. <b>This list is the reason
    /// the census works on ADDITION</b> — a new decoded type whose members are not registered fails
    /// here, which is how <c>StreamLine</c> arrived (DMXENG-118).
    /// </remarks>
    private static readonly Type[] DecodedTypes =
        [typeof(SessionContent), typeof(RosterEntry), typeof(StreamLine)];

    /// <summary>
    /// What <c>SessionContentCodec.Vetted</c> does with each decoded member. A change here is a
    /// change a person read.
    /// </summary>
    /// <remarks>
    /// The values are prose on purpose. They are not asserted against behaviour — see the limit on
    /// the class — and pretending otherwise by making them machine-checkable would claim the thing
    /// this test explicitly does not do.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> VettingDecisions =
        new Dictionary<string, string>
        {
            ["SessionContent.Roster"] =
                "Rebuilt. Vetted filters the list and returns a new SessionContent, so no entry "
                + "reaches a caller without passing the arms below. Null is returned untouched.",

            ["SessionContent.ClosingAtUtcTicks"] =
                "CARRIED THROUGH UNCHANGED (SessionContentCodec: "
                + "ClosingAtUtcTicks = content.ClosingAtUtcTicks). A long?, so it cannot carry text "
                + "and cannot forge a line — the same argument Role carries. RANGE IS VETTED AT THE "
                + "ONLY DOOR: SessionClosing.TryFromWire returns null outside "
                + "[0, DateTime.MaxValue.Ticks], and SessionClosing's sole constructor is PRIVATE, "
                + "so there is no path by which a consumer obtains an out-of-range instant. The "
                + "reason is in that type's own remark — Instant throws outside the range and "
                + "RemainingAt is read in front of a participant watching a countdown, so an "
                + "out-of-range value from another client is not a bad number, it is a crash in a "
                + "draw path. JOINER-SIDE HANDLING MUST GO THROUGH TryFromWire; it is not a fresh "
                + "decision and constructing a DateTimeOffset from these ticks directly is the "
                + "defect the private constructor exists to prevent. SessionContent's own doc on "
                + "this member says the same (DMXENG-11, #143; corrected #150's entry, which "
                + "recorded the decision as pending because it searched for CONSUMERS — a search "
                + "that cannot see a rule enforced by the only constructor that can PRODUCE the "
                + "value).",

            ["SessionContent.Leaving"] =
                "CARRIED THROUGH UNCHANGED (SessionContentCodec: Leaving = content.Leaving). A "
                + "bool?, so it cannot carry text and cannot forge a line — the same argument Role "
                + "and ClosingAtUtcTicks carry. THE VETTING THAT MATTERS FOR THIS MEMBER IS NOT ITS "
                + "VALUE BUT ITS SUBJECT: it says only that the SENDER is leaving, and the sender is "
                + "identified by the KEY THE PAYLOAD OPENED UNDER (MemberContentReader), never by "
                + "anything inside the document. So a true here removes exactly one member — "
                + "whoever sealed it — and there is no field a caller could add to name somebody "
                + "else. THE ONLY MEMBER-AUTHORED FIELD IN THIS TYPE: the others travel host to "
                + "member, and D-3 is not inverted because this asserts the sender's own intent "
                + "rather than shared state, with the host deciding what follows (DMXENG-60, "
                + "A-1.16a). A quit is not a vanish — see AdmissionControl.Departed against "
                + "RecordDrop, which share no code by design (A-1.30).",

            ["RosterEntry.PeerCode"] =
                "The whole entry is DROPPED unless PeerCode.TryParse accepts it. Nothing is "
                + "repaired: the roster is host-authored and sealed, so a malformed code means our "
                + "own encoder is broken or a keyholder is forging, and dropping answers both "
                + "(BUG-57).",

            ["RosterEntry.DisplayName"] =
                "Replaced with DisplayName.OrNone(value).Value, so a name that could forge a line "
                + "in the prompt is degraded rather than passed through (PR #86).",

            ["SessionContent.Entries"] =
                "Rebuilt. VettedEntries filters the list with StreamLine.TryToEntry as the "
                + "predicate and returns a new SessionContent, so a line that cannot become a "
                + "domain entry never reaches a caller. Null is returned untouched. DELIBERATELY "
                + "NOT INSIDE THE ROSTER'S NULL CHECK: the previous Vetted returned the document "
                + "untouched when Roster was null, which is why the departure guard is named "
                + "WhenARosterIsPresent. A stamped broadcast ordinarily carries no roster, so "
                + "vetting reached only via the roster would be unvetted on the common case "
                + "(DMXENG-118).",

            ["StreamLine.Sequence"] =
                "The whole line is DROPPED below 1. HostSequencer issues from 1, so anything lower "
                + "was not host-minted, and R-2.4 makes the host the sole minter. 0 is the specific "
                + "hazard: it sorts to the FRONT of a populated log (BUG-161). This is the DOOR; "
                + "SessionStream.Record's identical check is the backstop it says it is.",

            ["StreamLine.AtUtcTicks"] =
                "CARRIED THROUGH UNCHANGED. A long, so it cannot carry text and cannot forge a "
                + "line — the same argument Role, ClosingAtUtcTicks and Leaving carry. IT IS THE "
                + "HOST'S CLOCK AND NEVER THE READER'S (A-2.5), so no client re-stamps on receipt. "
                + "RANGE IS NOT VETTED HERE AND THAT IS A NAMED RESIDUAL: unlike "
                + "ClosingAtUtcTicks there is no TryFromWire equivalent, because this value orders "
                + "and timestamps a log rather than driving a countdown in a draw path. A renderer "
                + "that converts it to DateTimeOffset owes the range check ClosingAtUtcTicks "
                + "already has.",

            ["StreamLine.Kind"] =
                "DELIBERATELY UNVETTED, on RosterEntry.Role's reasoning and inheriting its limit. "
                + "An enum over int with no string converter: every string form is refused at "
                + "decode because the deserialiser throws and TryDecode returns false, and an "
                + "out-of-range number arrives as an undefined member whose ToString is digits. So "
                + "it cannot carry text and cannot forge a line; it can present a value matching no "
                + "case, which is a rendering question. THIS TEST GOES GREEN EITHER WAY.",

            ["StreamLine.Peer"] =
                "The whole line is DROPPED unless PeerCode.TryParse accepts it — the same answer as "
                + "RosterEntry.PeerCode and for the same reason: the code is the IDENTITY, so a "
                + "line whose code is unusable attributes content to nobody, and keeping it would "
                + "manufacture a speaker rather than remove a forgery. IT IS A STRING ON THE WIRE "
                + "BY MEASUREMENT, NOT PREFERENCE: a PeerCode serialises as "
                + "{\"Value\":\"BCDFGH\",\"IsPresent\":true} and deserialises to default — absent, "
                + "and equal to every other absent code (DMXENG-105).",

            ["StreamLine.Text"] =
                "DELIBERATELY UNVETTED, and named rather than omitted because an unmentioned field "
                + "is exactly what let PeerCode through the roster gate (BUG-57). It is content a "
                + "person typed, so there is no shape to hold it to: refusing newlines would refuse "
                + "legitimate messages and truncating would silently alter what somebody said. WHAT "
                + "IT REMAINS IS UNTRUSTED TEXT, so a renderer must not draw it beside anything a "
                + "forged line could displace — the obligation DisplayName.OrNone discharges for "
                + "names cannot be discharged here, and it moves to the renderer.",

            ["RosterEntry.Role"] =
                "DELIBERATELY UNVETTED, and this is the entry the limit above is about. An enum "
                + "over int with no string converter: every string form is refused at decode, and "
                + "an out-of-range number arrives as an undefined member whose ToString is digits, "
                + "so it cannot carry text and cannot forge a line. What it can do is present a "
                + "value matching no case, which is a rendering question. BUG-91 asks whether that "
                + "reasoning is still right; THIS TEST GOES GREEN EITHER WAY.",
        };

    // THE INSTRUMENT. Fails when a member of either decoded type is not named above — including one
    // added with a default, which is the case both compile-time approaches miss.
    //
    // Set equality rather than a subset check, in BOTH directions: a decision naming a member that
    // no longer exists is drift too, and a one-directional check would let the list rot while
    // reading as coverage.
    [Fact]
    public void EveryDecodedMemberHasARecordedVettingDecision()
    {
        var members = DecodedTypes
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => $"{type.Name}.{property.Name}"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            VettingDecisions.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList(),
            members);
    }

    // A decision that says nothing is the same hole wearing a filled-in form. Fails if an entry is
    // added as a placeholder to quiet the test above.
    [Fact]
    public void EveryDecisionSaysSomething()
    {
        Assert.All(VettingDecisions, decision =>
            Assert.False(
                string.IsNullOrWhiteSpace(decision.Value),
                $"{decision.Key} has an empty vetting decision."));
    }
}
