using System;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// What the roster shows for a role this build does not know (R-1.3f).
/// </summary>
/// <remarks>
/// <para>
/// <b><c>SessionRole</c> crosses the wire as an enum, so it can carry an int matching no defined
/// case.</b> BUG-57 closed the text case — a role can no longer arrive as arbitrary text — but not
/// this one. A newer client, a role added later, or a corrupted value all arrive identically, and a
/// <c>switch</c> with no arm for them throws inside a draw call, which is the worst place to find
/// out.
/// </para>
/// <para>
/// So it is decided rather than discovered, and the decision is asserted here rather than left in a
/// comment next to the drawing code where no window author looks.
/// </para>
/// </remarks>
public class AnUnknownRoleIsARenderingDecisionTests
{
    // DERIVED, not enumerated: a role added to the enum without a label fails this. An InlineData
    // list of the three we have today would pass forever while the fourth rendered as nothing.
    [Fact]
    public void EveryDefinedRoleHasALabel()
    {
        var unlabelled = Enum.GetValues<SessionRole>()
            .Where(role => SessionRoleLabel.For(role) is null)
            .ToList();

        Assert.True(
            unlabelled.Count == 0,
            "These roles are defined but render no label, so a participant holding one appears "
            + "unqualified: " + string.Join(", ", unlabelled));
    }

    [Fact]
    public void TheLabelsAreDistinct()
    {
        var labels = Enum.GetValues<SessionRole>().Select(SessionRoleLabel.For).ToList();

        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }

    // The case the wire can produce and the enum cannot. Values chosen either side of the defined
    // range, because "one past the end" is the only unknown people think of.
    [Theory]
    [InlineData(3)]
    [InlineData(99)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void AnUndefinedRoleRendersNoLabelAndDoesNotThrow(int overTheWire)
    {
        var role = (SessionRole)overTheWire;

        Assert.Null(SessionRoleLabel.For(role));
        Assert.False(SessionRoleLabel.IsKnown(role));
    }

    // The decision, stated as two refusals rather than one assertion, because each rules out a
    // different tempting alternative and only the pair pins the behaviour.
    [Fact]
    public void AnUndefinedRoleIsNotQuietlyTreatedAsThePlayerRole()
    {
        // If it defaulted to the lowest role, a client older than a future privileged role would
        // describe those participants as ordinary players -- a wrong claim about what they may do.
        Assert.NotEqual(SessionRoleLabel.For(SessionRole.Player), SessionRoleLabel.For((SessionRole)99));
    }

    [Fact]
    public void TheKnownRolesAreStillKnown()
    {
        // The positive control. Without it every assertion above is satisfied by a For() that
        // returns null for everything, which would render a roster of bare names and pass.
        Assert.Equal("Player", SessionRoleLabel.For(SessionRole.Player));
        Assert.Equal("Assistant", SessionRoleLabel.For(SessionRole.Assistant));
        Assert.Equal("DM", SessionRoleLabel.For(SessionRole.DungeonMaster));
        Assert.All(Enum.GetValues<SessionRole>(), role => Assert.True(SessionRoleLabel.IsKnown(role)));
    }
}
