using System.IO;
using System.Linq;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-141 / A-1.2z: with no campaign open, the name box does not take input it will never keep.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE FAILURE WAS NOT SILENCE, IT WAS A CONTRADICTION.</b> The box accepted a name and stored
/// none of it, and the line directly below went on showing the character name. The user saw one name
/// in the field and a different one in "You will join as", with nothing explaining why — which reads
/// as a broken PREVIEW rather than as a refusal to store. They were not uninformed; they were
/// misinformed about which half was wrong.
/// </para>
/// <para>
/// <b>THE REMEDY IS A TELLING, NOT A STORING.</b> A-2.31 as amended by SQ-112 permits exactly ONE
/// globally-stored name-shaped value, whose only reader is the pre-fill path. Giving the no-campaign
/// name somewhere to live is the cheapest fix and it is the forbidden one, so nothing here stores
/// anything — and <see cref="NothingIsRecordedWithoutACampaign"/> asserts the ABSENCE of the save
/// rather than only the presence of a message. Those are different claims and only one is about
/// A-2.31.
/// </para>
/// <para>
/// <b>And the box is DISABLED rather than merely captioned</b>, because a control offered where it
/// cannot work is itself the defect: announcing the discard while still accepting keystrokes is the
/// same defect with a caption on it.
/// </para>
/// </remarks>
public class TheNameBoxDoesNotTakeWhatItCannotKeepTests
{
    private static readonly DisplayName CharacterName = DisplayName.OrNone("Ysera");

    // CONTROL. I first wrote that this asserts the contradiction DIRECTLY rather than by proxy, and
    // the mutation says otherwise: reverting ConfigWindow to the broken version leaves this GREEN.
    //
    // The two halves only diverge once the user TYPES -- ImGui mutates the box's buffer and the
    // preview goes on reading the campaign -- and nothing in this project can drive ImGui. Their
    // starting values always agreed, on both builds. So this guards the base state rather than
    // detecting the defect, which is a different and smaller claim than the one I made for it.
    [Fact]
    public void WithNoCampaignTheBoxAndThePreviewShowTheSameName()
    {
        var box = CampaignDisplayName.ToEdit(null, CharacterName);
        var preview = CampaignDisplayName.Or(null, CharacterName);

        Assert.Equal(preview.Value, box);
    }

    // CONTROL, AND THE A-2.31 ONE. The window saves only when RecordChosen reports true, so a false
    // here is the absence of the save. Green on both builds by design: it does not detect BUG-141,
    // it detects a FIX that introduced storage to solve it -- which is the forbidden repair, and the
    // one the cheapest route leads to. Asserted for a name that WOULD have been recorded against a
    // real campaign, or it would pass for a reason unrelated to the campaign being missing.
    [Fact]
    public void NothingIsRecordedWithoutACampaign()
    {
        Assert.False(CampaignDisplayName.RecordChosen(null, "Someone Else", CharacterName));
        Assert.Equal(string.Empty, CampaignDisplayName.Stored(null));
    }

    // THE CONTROL, and without it "tells the user" is indistinguishable from "broke the feature".
    // With a campaign the name is still recorded and the preview still follows it.
    [Fact]
    public void WithACampaignTheNameIsStillRecordedAndThePreviewAgrees()
    {
        var campaign = new Campaign { Name = "Whitewind" };

        Assert.True(CampaignDisplayName.RecordChosen(campaign, "Someone Else", CharacterName));
        Assert.Equal("Someone Else", CampaignDisplayName.Stored(campaign));
        Assert.Equal("Someone Else", CampaignDisplayName.Or(campaign, CharacterName).Value);
        Assert.Equal("Someone Else", CampaignDisplayName.ToEdit(campaign, CharacterName));
    }

    // AND THE BOX AND PREVIEW STILL AGREE WITH A CAMPAIGN. The no-campaign case above could be
    // satisfied by making them agree ALWAYS on the character name -- which would break the feature
    // while passing the first test.
    [Fact]
    public void WithACampaignTheBoxAndThePreviewStillShowTheStoredName()
    {
        var campaign = new Campaign { Name = "Whitewind" };
        CampaignDisplayName.RecordChosen(campaign, "Someone Else", CharacterName);

        Assert.Equal(
            CampaignDisplayName.Or(campaign, CharacterName).Value,
            CampaignDisplayName.ToEdit(campaign, CharacterName));
    }

    // >>> THE ONLY DETECTOR IN THIS FILE, AND IT IS A TEXTUAL PROXY. MEASURED, NOT ASSUMED. <<<
    //
    // Reverting ConfigWindow to the pre-fix version reddens THIS TEST AND NOTHING ELSE here: 1 failed,
    // 5 passed. Every other case above is a control that holds on both builds.
    //
    // That is the ceiling rather than a choice. The defect lives in a state only reachable by typing
    // into an ImGui widget, and nothing in this project links a renderer -- so no test here can put
    // a character in that box and read the two lines back. What this CAN say is that the window names
    // the disabled state and renders the explanation; it cannot say either reaches a screen. The same
    // limit TheNameFieldSaysWhenItIsFullTests records for its own scan.
    //
    // Reported to the Deployment Manager rather than left for a reader to discover, because "assert
    // on what the UI actually renders" was an explicit requirement and this is as close as the
    // project can get to it.
    //
    // Comments are stripped first, so a sentence DESCRIBING the control cannot stand in for it --
    // and the word "no campaign" already appeared in a comment on the broken version.
    [Fact]
    public void TheWindowDisablesTheBoxAndSaysWhy()
    {
        var source = WindowSource("ConfigWindow.cs");

        Assert.Contains("BeginDisabled", source);
        Assert.Contains("NameNeedsACampaign", source);
    }

    // The guard on the guard (BUG-48's shape): a scan over a path that does not resolve matches
    // nothing and goes green, so the read is asserted rather than assumed.
    [Fact]
    public void TheScanReadsARealFile()
    {
        Assert.NotEmpty(WindowSource("ConfigWindow.cs"));
    }

    private static string WindowSource(string fileName)
    {
        var path = Path.Combine(ShippedCopyCorpus.WindowsDirectory(), fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"{fileName} is not where the scan looks, so it would pass over nothing.", path);
        }

        return string.Join(
            "\n",
            File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("//", System.StringComparison.Ordinal)));
    }
}
