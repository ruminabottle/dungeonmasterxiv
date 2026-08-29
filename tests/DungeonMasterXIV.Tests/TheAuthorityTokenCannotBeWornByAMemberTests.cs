using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.7a — a member must not be able to produce a line indistinguishable from a host-authored one,
/// and the host is marked structurally rather than by a name.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFENCE IS NOT THE PARENTHETICAL, AND SAYING SO IS THE POINT OF THIS FILE.</b> An earlier
/// finding held that it was, and was approve-blocking; the Product Owner <b>withdrew it as false</b>,
/// because a member could set speaker <c>Renn</c> and display name <c>DM</c> and the panel would
/// render <c>Renn (DM)</c> — the defence rendering the impersonation. The defence is
/// <see cref="SessionRole"/>, which the session assigns and the sender cannot set.
/// </para>
/// <para>
/// <b>THE GUARANTEE RESTS ON A COUPLING BETWEEN TWO FILES THAT DO NOT REFERENCE EACH OTHER</b>, and
/// that is what this file exists to pin. <c>SessionRoleLabel</c> renders the authority token;
/// <c>DisplayName</c> refuses it as a name (R-1.3j.6). <b>Neither mentions the other.</b> Change
/// either — a new label, a relaxed reserved list — and the impersonation reopens with no test
/// failing anywhere else.
/// </para>
/// <para>
/// <b>AND THE GUARANTEE IS NON-IDENTITY, NOT NON-RESEMBLANCE.</b> Stated at its true strength because
/// this area has already shipped three claims one notch stronger than the mechanism delivered:
/// <c>the DM</c>, <c>D.M.</c> and <c>D M</c> all parse as display names, exactly as R-1.3j.6's own
/// leak list says. A member cannot render an IDENTICAL host line; they can render an adjacent one.
/// </para>
/// </remarks>
public class TheAuthorityTokenCannotBeWornByAMemberTests
{
    // THE COUPLING, ASSERTED DIRECTLY RATHER THAN LEFT TO HOLD BY LUCK.
    //
    // If SessionRoleLabel's authority token stops being a word DisplayName reserves, a member can set
    // it as their display name and render a line identical to the host's. Nothing else in the suite
    // would notice: the roster tests check rendering, the DisplayName tests check parsing, and the
    // property lives in the gap between them.
    [Fact]
    public void TheAuthorityTokenIsAWordNoDisplayNameMayCarry()
    {
        var token = SessionRoleLabel.For(SessionRole.DungeonMaster);

        Assert.NotNull(token);
        Assert.False(
            DisplayName.TryParse(token, out _),
            $"SessionRoleLabel renders the host as '{token}', but DisplayName ACCEPTS that as a name. "
            + "A member could wear it and render a line identical to a host-authored one, which is "
            + "exactly what R-2.7a forbids. Either reserve the token in DisplayName (R-1.3j.6) or "
            + "mark the host by something a name cannot contain.");
    }

    // THE NEGATIVE HALF, AND WITHOUT IT THE ROW ABOVE PASSES AGAINST A DisplayName THAT REFUSES
    // EVERYTHING. A non-authority label must still be usable as an ordinary name -- otherwise the
    // test is satisfied by a parser that has stopped working rather than by a reserved word.
    [Fact]
    public void AnOrdinaryRoleLabelIsStillAPerfectlyUsableName()
    {
        Assert.True(DisplayName.TryParse(SessionRoleLabel.For(SessionRole.Player), out _));
        Assert.True(DisplayName.TryParse(SessionRoleLabel.For(SessionRole.Assistant), out _));
    }

    // THE RESIDUAL, PINNED SO IT IS NOT REDISCOVERED AS A SURPRISE. These are R-1.3j.6's own listed
    // leaks. They are NOT a defect of this chunk -- they are the reason the guarantee is stated as
    // non-identity. A future reader finding "the DM" in a roster should meet this row, not a claim
    // that it was impossible.
    [Theory]
    [InlineData("the DM")]
    [InlineData("D.M.")]
    [InlineData("D M")]
    public void AnAdjacentImitationIsNotCaughtAndThatIsStated(string imitation)
    {
        Assert.True(
            DisplayName.TryParse(imitation, out _),
            "If this now REFUSES, R-1.3j.6's leak list has narrowed and the residual documented in "
            + "MessageLine is out of date. That is good news and this row should be updated, not "
            + "deleted -- the claim it guards is about what the mechanism delivers.");
    }

    // R-2.7a, THE PROPERTY ITSELF: the same speaker and the same person, rendered by a member and by
    // the host, must not produce the same line.
    [Fact]
    public void AMemberCannotRenderTheLineTheHostRenders()
    {
        Assert.True(DisplayName.TryParse("Tuka", out var person));

        var host = MessageLine.Render(
            MessageKind.InCharacter, MessageTarget.Everyone, "Renn", person, SessionRole.DungeonMaster, "hold");
        var member = MessageLine.Render(
            MessageKind.InCharacter, MessageTarget.Everyone, "Renn", person, SessionRole.Player, "hold");

        Assert.NotEqual(host, member);
        Assert.Contains("[DM]", host, StringComparison.Ordinal);
        Assert.DoesNotContain("[DM]", member, StringComparison.Ordinal);
    }
}
