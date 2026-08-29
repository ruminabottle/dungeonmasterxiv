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
    /// Two, and that is why the wide scope was affordable: the codec deserialises
    /// <see cref="SessionContent"/>, whose only member is a list of <see cref="RosterEntry"/>.
    /// Counted before choosing wide over narrow rather than after.
    /// </remarks>
    private static readonly Type[] DecodedTypes = [typeof(SessionContent), typeof(RosterEntry)];

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

            ["RosterEntry.PeerCode"] =
                "The whole entry is DROPPED unless PeerCode.TryParse accepts it. Nothing is "
                + "repaired: the roster is host-authored and sealed, so a malformed code means our "
                + "own encoder is broken or a keyholder is forging, and dropping answers both "
                + "(BUG-57).",

            ["RosterEntry.DisplayName"] =
                "Replaced with DisplayName.OrNone(value).Value, so a name that could forge a line "
                + "in the prompt is degraded rather than passed through (PR #86).",

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
