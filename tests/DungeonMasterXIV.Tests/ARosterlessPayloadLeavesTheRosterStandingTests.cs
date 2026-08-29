using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.3f: a payload that says nothing about the membership is not a payload saying it emptied.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file exists because a mutation survived.</b> The rule was a <c>??</c> inside a closure
/// passed as an argument inside the frame loop — <c>content =&gt; _receivedRoster = content.Roster ??
/// _receivedRoster</c> — and replacing the fallback with an empty list, so that any rosterless
/// payload wiped the roster, left all 1,122 tests green. DMXENG-69 gave the rule an owner; this
/// gives it a test.
/// </para>
/// <para>
/// <b>Most payloads carry no roster</b>, which is what makes the surviving mutation expensive rather
/// than theoretical: the roster is one optional field on <c>SessionContent</c>, so under the mutant
/// the next ordinary message a joined player receives empties the list they are looking at.
/// </para>
/// <para>
/// <b>What this does NOT reach, stated rather than implied.</b> It pins the rule on
/// <see cref="ReceivedRoster"/>, not the wiring in <c>SessionCoordinator.Tick</c> that calls it.
/// That wiring is covered separately — cutting it so rosters are never applied kills five tests —
/// but a test driving a rosterless payload through a real frame would cover both at once, and this
/// is not that test.
/// </para>
/// </remarks>
public class ARosterlessPayloadLeavesTheRosterStandingTests
{
    private static readonly IReadOnlyList<RosterEntry> Party =
    [
        new RosterEntry("BKD7RM", "Nanamo", SessionRole.DungeonMaster),
        new RosterEntry("VWXYZ2", "Ysera", SessionRole.Player),
    ];

    // THE MUTATION THIS CLOSES: Entries = entries ?? [].
    [Fact]
    public void APayloadCarryingNoRosterDoesNotEmptyTheOneWeHave()
    {
        var received = new ReceivedRoster();
        received.Replace(Party);

        received.Replace(null);

        Assert.Equal(2, received.Entries.Count);
        Assert.Same(Party, received.Entries);
    }

    // The other direction, or "keep the previous one" is satisfied by never applying anything.
    [Fact]
    public void ARosterThatDoesArriveReplacesWhatWasThere()
    {
        var received = new ReceivedRoster();
        received.Replace(Party);

        received.Replace([new RosterEntry("BKD7RM", "Nanamo", SessionRole.DungeonMaster)]);

        Assert.Equal(SessionRole.DungeonMaster, Assert.Single(received.Entries).Role);
    }

    // Replaced, never merged: a participant who left is gone because the next roster does not list
    // them. A merge would keep Ysera here, and nothing else in the suite would notice.
    [Fact]
    public void AParticipantWhoIsNoLongerListedIsGone()
    {
        var received = new ReceivedRoster();
        received.Replace(Party);

        received.Replace([new RosterEntry("BKD7RM", "Nanamo", SessionRole.DungeonMaster)]);

        Assert.DoesNotContain(received.Entries, entry => entry.DisplayName == "Ysera");
    }

    // Nothing has arrived yet, so there is nothing to show -- and null must not be one of the
    // answers a caller has to handle.
    [Fact]
    public void BeforeAnythingArrivesTheRosterIsEmptyRatherThanAbsent()
    {
        Assert.Empty(new ReceivedRoster().Entries);
    }
}
