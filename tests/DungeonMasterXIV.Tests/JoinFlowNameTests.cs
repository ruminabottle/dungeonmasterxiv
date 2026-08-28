using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.2n's pre-fill rule: settings may fill the join-flow name box, never overwrite the user.
/// </summary>
/// <remarks>
/// This exists because the rule is three-state and lived in a window, where nothing could exercise
/// it. A source scan proves the control is present; only this proves the rule is right.
/// </remarks>
public class JoinFlowNameTests
{
    // The ordinary case: nothing typed yet, so the field takes the settings value.
    [Fact]
    public void AnUntouchedFieldTakesTheSettingsValue()
    {
        Assert.True(JoinFlowName.ShouldReplace(fromSettings: "Ysera", lastSeeded: "", typed: ""));
    }

    // The case a once-only seed gets wrong. A player who switches character mid-session would sit
    // looking at the previous character's name and send it.
    [Fact]
    public void SwitchingCharacterRefillsAFieldTheUserHasNotTouched()
    {
        Assert.True(JoinFlowName.ShouldReplace(fromSettings: "Alphinaud", lastSeeded: "Ysera", typed: "Ysera"));
    }

    // The case comparing against the CURRENT setting instead of the last seed gets wrong. This is
    // the whole reason the rule keeps a separate "what I last wrote" value.
    [Fact]
    public void AUserWhoTypedTheDefaultBackIsNotOverwritten()
    {
        // They typed "Ysera" themselves after the source had already moved on to "Alphinaud".
        Assert.False(JoinFlowName.ShouldReplace(fromSettings: "Alphinaud", lastSeeded: "Alphinaud", typed: "Ysera"));
    }

    // An edit must survive a character switch. Without the second condition this overwrites it.
    [Fact]
    public void AnEditSurvivesTheSourceChanging()
    {
        Assert.False(JoinFlowName.ShouldReplace(fromSettings: "Alphinaud", lastSeeded: "Ysera", typed: "Tataru"));
    }

    // Without the FIRST condition every frame overwrites the field and nothing can be typed at all.
    [Fact]
    public void AnUnchangedSourceLeavesTheFieldAlone()
    {
        Assert.False(JoinFlowName.ShouldReplace(fromSettings: "Ysera", lastSeeded: "Ysera", typed: "Ysera"));
        Assert.False(JoinFlowName.ShouldReplace(fromSettings: "Ysera", lastSeeded: "Ysera", typed: "Tataru"));
    }

    // Deliberately cleared. The user emptied the box after the source had settled, so the emptiness
    // is an edit and survives — they see "gave no name" on the DM's prompt, which is their choice.
    [Fact]
    public void ADeliberatelyEmptiedFieldIsNotRefilled()
    {
        Assert.False(JoinFlowName.ShouldReplace(fromSettings: "Ysera", lastSeeded: "Ysera", typed: ""));
    }

    // Ordinal, not culture-sensitive: two names differing only by case are two different names, and
    // a culture-aware comparison would make the rule depend on the player's locale.
    [Fact]
    public void ComparisonIsOrdinal()
    {
        Assert.True(JoinFlowName.ShouldReplace(fromSettings: "ysera", lastSeeded: "Ysera", typed: "Ysera"));
    }
}
