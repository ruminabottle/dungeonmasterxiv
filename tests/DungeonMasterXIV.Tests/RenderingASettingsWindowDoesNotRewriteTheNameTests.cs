using System.Linq;
using System.Text;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.2v-2: drawing the campaign window must not rewrite the stored name. Nothing the user did not
/// type may be persisted over what they did.
/// </summary>
/// <remarks>
/// <para>
/// <b>NOT ONE OF THESE TESTS TOUCHES ImGui, AND THAT IS THE POINT RATHER THAN A CONVENIENCE.</b> Two
/// facts about the widget are unmeasured and unmeasurable here — whether it truncates or refuses at
/// the byte boundary, and whether it reports a change when IT truncated rather than when the user
/// typed. A guard that needs neither answer makes both questions stop mattering. If a test in this
/// file ever needs a real <c>InputText</c>, the guard has moved to the wrong place.
/// </para>
/// <para>
/// <b>The situation, stated without any claim about the widget:</b> a 288-byte alias is VALID and can
/// be stored. The box is <see cref="DisplayName.MaxUtf8Bytes"/> — 257 — so a stored name can be
/// larger than the field that displays it. Everything after that is the widget's business; the guard
/// only refuses to PERSIST a shortening of a name the field could not have shown whole.
/// </para>
/// <para>
/// <b>Both directions, because a guard that refuses everything breaks the campaign screen</b> and
/// would pass every negative test here. The positives are not decoration: they are the half that
/// fails if the guard is too broad.
/// </para>
/// </remarks>
public class RenderingASettingsWindowDoesNotRewriteTheNameTests
{
    private static readonly DisplayName CharacterName = DisplayName.OrNone("Ysera");

    /// <summary>
    /// 32 grapheme clusters, each a base letter with four combining marks: <b>288 UTF-8 bytes</b>,
    /// larger than the 257-byte field that displays it.
    /// </summary>
    /// <remarks>
    /// <b>Derived from the constants, not typed as a literal.</b> The count comes from
    /// <see cref="DisplayName.MaxLength"/>, so if the limit moves this fixture moves with it rather
    /// than quietly becoming a name of some other size that no longer demonstrates anything.
    /// </remarks>
    private static readonly string TooLargeToShow =
        string.Concat(Enumerable.Repeat("Á̂̃̄", DisplayName.MaxLength));

    // THE PREMISE, ASSERTED RATHER THAN ASSUMED. Every test below is vacuous if a 288-byte name
    // cannot be stored in the first place -- there would be nothing for a render to shorten. This is
    // feature-engineer-2's finding, pinned here so the suite cannot quietly stop exercising the
    // situation it was written for.
    [Fact]
    public void ANameLargerThanTheFieldIsValidAndStorable()
    {
        var campaign = new Campaign();

        Assert.True(
            DisplayName.TryParse(TooLargeToShow, out _),
            "The oversized fixture is not a VALID name, so nothing could store it and every test in "
            + "this file is vacuous. The clause under test is TryParse's, not the field's.");

        Assert.True(CampaignDisplayName.RecordChosen(campaign, TooLargeToShow, CharacterName));

        Assert.True(
            Encoding.UTF8.GetByteCount(CampaignDisplayName.Stored(campaign)) > DisplayName.MaxUtf8Bytes,
            "The stored alias fits inside the input field, so the field could show it whole and no "
            + "render could shorten it. The clause under test is byte-count-exceeds-field-capacity.");
    }

    // THE DEFECT. Fails if: a shorter value arriving from a render is written over a stored name the
    // field could never have shown whole. The user typed nothing; this is stored-data mutation
    // without user action, and it outlives the session that caused it.
    [Fact]
    public void AShorterValueDoesNotOverwriteANameTheFieldCouldNotShow()
    {
        var campaign = new Campaign();
        CampaignDisplayName.RecordChosen(campaign, TooLargeToShow, CharacterName);

        var asTheFieldMightReturnIt = TooLargeToShow[..120];
        var changed = CampaignDisplayName.RecordChosen(campaign, asTheFieldMightReturnIt, CharacterName);

        Assert.False(
            changed,
            "A shortening of an unshowable stored name was reported as a change, so the caller saves. "
            + "The clause under test is incoming-is-shorter-than-an-unshowable-stored-name.");
        Assert.Equal(TooLargeToShow, CampaignDisplayName.Stored(campaign));
    }

    // THE OTHER DIRECTION, and the half a refuse-everything guard fails. An ordinary name is stored
    // and edited normally; the guard must be invisible here, which is every real user.
    [Fact]
    public void AnOrdinaryNameIsStillRecorded()
    {
        var campaign = new Campaign();

        Assert.True(CampaignDisplayName.RecordChosen(campaign, "The Cartographer", CharacterName));

        Assert.Equal("The Cartographer", CampaignDisplayName.Stored(campaign));
    }

    // Shortening an ordinary alias is a normal edit and must keep working. This is the case a naive
    // "never accept anything shorter" guard breaks, so it is asserted rather than assumed.
    [Fact]
    public void AnOrdinaryNameCanStillBeShortened()
    {
        var campaign = new Campaign();
        CampaignDisplayName.RecordChosen(campaign, "The Cartographer", CharacterName);

        Assert.True(CampaignDisplayName.RecordChosen(campaign, "Carto", CharacterName));

        Assert.Equal("Carto", CampaignDisplayName.Stored(campaign));
    }

    // The escape hatch, and it is required rather than a nicety: without it a user whose stored alias
    // is too large to display could never change it from this box again. Clearing is unambiguously
    // the user's act -- a field cannot truncate 288 bytes to nothing -- so it is always honoured.
    [Fact]
    public void AnUnshowableNameCanStillBeClearedByTheUser()
    {
        var campaign = new Campaign();
        CampaignDisplayName.RecordChosen(campaign, TooLargeToShow, CharacterName);

        Assert.True(CampaignDisplayName.RecordChosen(campaign, string.Empty, CharacterName));

        Assert.Equal(string.Empty, CampaignDisplayName.Stored(campaign));
    }

    // And replacing it outright works, so the guard refuses only SHORTENING rather than refusing to
    // let the name be changed at all. Longer than the stored value, so the shortening clause cannot
    // be what admits it.
    [Fact]
    public void AnUnshowableNameCanStillBeReplacedWithALongerOne()
    {
        var campaign = new Campaign();
        CampaignDisplayName.RecordChosen(campaign, TooLargeToShow, CharacterName);

        var longer = TooLargeToShow + "̅";

        Assert.True(CampaignDisplayName.RecordChosen(campaign, longer, CharacterName));

        Assert.Equal(longer, CampaignDisplayName.Stored(campaign));
    }
}
