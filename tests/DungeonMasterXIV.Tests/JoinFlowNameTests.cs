using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.2n's pre-fill rule: settings may fill the join-flow name box, never overwrite the user.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the rule lived in a window, where nothing could exercise it. A source scan
/// proves the control is present; only this proves the rule is right.
/// </para>
/// <para>
/// <b>Every case asserts BOTH returned values.</b> The field and the seed it was written from are
/// one fact, and asserting only the field would leave the seed free to drift — which is the residue
/// the first version of this shipped with: the decision extracted, its invariant left behind.
/// </para>
/// </remarks>
public class JoinFlowNameTests
{
    // The ordinary case: nothing typed yet, so the field takes the settings value — and the seed
    // records that this rule is what put it there.
    [Fact]
    public void AnUntouchedFieldTakesTheSettingsValue()
    {
        Assert.Equal(
            new PreFilledName("Ysera", "Ysera"),
            JoinFlowName.Resolve(fromSettings: "Ysera", lastSeeded: "", typed: ""));
    }

    // The case a once-only seed gets wrong: a player who switches character mid-session would sit
    // looking at the previous character's name and send it.
    [Fact]
    public void SwitchingCharacterRefillsAFieldTheUserHasNotTouched()
    {
        Assert.Equal(
            new PreFilledName("Alphinaud", "Alphinaud"),
            JoinFlowName.Resolve(fromSettings: "Alphinaud", lastSeeded: "Ysera", typed: "Ysera"));
    }

    // An edit must survive the source changing. Without the second condition this overwrites it —
    // and the seed must NOT move either, or the next call would treat the edit as ours to replace.
    [Fact]
    public void AnEditSurvivesTheSourceChangingAndDoesNotBecomeTheSeed()
    {
        Assert.Equal(
            new PreFilledName("Tataru", "Ysera"),
            JoinFlowName.Resolve(fromSettings: "Alphinaud", lastSeeded: "Ysera", typed: "Tataru"));
    }

    // Without the FIRST condition every frame overwrites the field and nothing can be typed at all.
    [Fact]
    public void AnUnchangedSourceLeavesBothAlone()
    {
        Assert.Equal(
            new PreFilledName("Ysera", "Ysera"),
            JoinFlowName.Resolve(fromSettings: "Ysera", lastSeeded: "Ysera", typed: "Ysera"));

        Assert.Equal(
            new PreFilledName("Tataru", "Ysera"),
            JoinFlowName.Resolve(fromSettings: "Ysera", lastSeeded: "Ysera", typed: "Tataru"));
    }

    // Deliberately cleared, after the source had settled. The emptiness is the field's current value
    // and is left alone; the user sees the DM told "gave no name", which is their choice.
    [Fact]
    public void ADeliberatelyEmptiedFieldIsNotRefilled()
    {
        Assert.Equal(
            new PreFilledName("", "Ysera"),
            JoinFlowName.Resolve(fromSettings: "Ysera", lastSeeded: "Ysera", typed: ""));
    }

    // WHAT THE RULE CANNOT DO, pinned so the doc comment cannot drift back into claiming it.
    //
    // An earlier version of this file's justification said the comparison distinguishes "the user
    // has not touched this" from "the user deliberately typed the seeded value back". It does not:
    // those are the SAME INPUTS and no state available here separates them. The window offers no
    // edit signal. What the rule guarantees is narrower and sufficient — a field holding anything
    // this rule did not write is never overwritten.
    [Fact]
    public void ItCannotTellAnUntouchedFieldFromOneTypedBackToTheSeed()
    {
        var neverTouched = JoinFlowName.Resolve(fromSettings: "Carol", lastSeeded: "Alice", typed: "Alice");
        var typedItBack = JoinFlowName.Resolve(fromSettings: "Carol", lastSeeded: "Alice", typed: "Alice");

        Assert.Equal(neverTouched, typedItBack);
        Assert.Equal(new PreFilledName("Carol", "Carol"), neverTouched);
    }

    // Ordinal, not culture-sensitive: two names differing only by case are two different names, and
    // a culture-aware comparison would make the rule depend on the player's locale.
    [Fact]
    public void ComparisonIsOrdinal()
    {
        Assert.Equal(
            new PreFilledName("ysera", "ysera"),
            JoinFlowName.Resolve(fromSettings: "ysera", lastSeeded: "Ysera", typed: "Ysera"));
    }
}
