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

    // >>> THE STATE NEITHER SUITE COVERED, WHICH IS WHY #217 AND THIS COLLIDED SILENTLY. <<<
    //
    // THE HAZARD IS REAL AND THIS PROVES IT RATHER THAN ASSERTING IT. #217 made ToEdit fall back to a
    // name carried over from before campaign-scoping. With NO campaign it returns that carried name --
    // while the preview below the box still returns the CHARACTER name. Those are the same two lines
    // that disagreed in BUG-141, rebuilt out of #217's parts, in a box now DISABLED so the user
    // cannot even correct it.
    //
    // Nothing in either suite reaches here: #217's tests pass because the pre-fill is offered, mine
    // pass because the box is disabled. The contradiction only exists where both changes are true.
    [Fact]
    public void ACarriedOverNameWouldContradictThePreviewWhenThereIsNoCampaign()
    {
        var box = CampaignDisplayName.ToEdit(null, "Carried Over", CharacterName);
        var preview = CampaignDisplayName.Or(null, CharacterName);

        Assert.Equal("Carried Over", box);
        Assert.NotEqual(preview.Value, box);
    }

    // >>> DMXENG-120: THE SAME RULE, ASSERTED BEHAVIOURALLY INSTEAD OF AS TEXT <<<
    //
    // This was Assert.Contains("noCampaign ? null : carriedOverDefault", source) -- and qa-1 defeated
    // it by ADDING ONE LINE after the ternary: the contradiction live, the asserted string untouched,
    // 1442 passed, 0 failed. THE PROXY FOLLOWED FROM WHERE THE DECISION SAT, NOT FROM THE RENDERER
    // CEILING. The decision is a boolean over two inputs; it never needed a renderer to test. It now
    // lives in CampaignDisplayName.ToPreFill and is answerable directly.
    [Fact]
    public void TheCarriedOverNameIsNotOfferedWhenThereIsNoCampaign()
    {
        Assert.Equal(
            CharacterName.Value,
            CampaignDisplayName.ToPreFill(null, "Carried Over", CharacterName));
    }

    // THE CONTROL, and without it the row above is satisfied by a helper that ignores the carried
    // value ALWAYS -- which would silently delete SQ-87's pre-fill rather than scope it.
    [Fact]
    public void TheCarriedOverNameIsStillOfferedWhenThereIsACampaign()
    {
        Assert.Equal(
            "Carried Over",
            CampaignDisplayName.ToPreFill(new Campaign { Name = "Whitewind" }, "Carried Over", CharacterName));
    }

    // AND THE BOX AGREES WITH THE PREVIEW, which is the harm BUG-141 actually names. Distinct from
    // the row far above: THAT one guards the base state with NO carried value, and passed on both
    // builds. This is the case where a carried value EXISTS and would have been offered -- the state
    // only the merged code could reach. Asserted as the RELATION rather than as two constants: a
    // build that moved both together would satisfy two equality checks and still show two names.
    [Fact]
    public void WithNoCampaignTheBoxAndThePreviewAgreeEvenWhenAnameWasCarriedOver()
    {
        var box = CampaignDisplayName.ToPreFill(null, "Carried Over", CharacterName);
        var preview = CampaignDisplayName.Or(null, CharacterName);

        Assert.Equal(preview.Value, box);
    }

    // THE WIRING, AND THIS PART GENUINELY IS UNDER THE CEILING. The rows above prove the RULE is
    // right; none of them proves the window CONSULTS it. A window that computed its own answer would
    // pass every one. Same shape as TheRetainedLogWiringIsPresentTests, and a textual proxy is the
    // declared limit for "does this file call that".
    // THREE BYPASSES OF THIS SCAN ARE DEMONSTRATED AND ALL THREE RED. Named here because a guard's
    // strength is the list of things it has been shown to catch, and that list belongs beside it:
    //
    //   BLOCK    /* the call */ then do something else            -> RED (blocks stripped, Singleline)
    //   TRAILING ToEdit(...);  // ToPreFill(campaign, carried...) -> RED (//[^\n]* , not StartsWith)
    //   ONE-WORD ToPreFill -> ToEdit, identical signature          -> RED
    //
    // THE TRAILING SHAPE IS THE ONE THAT MATTERS MOST AND IT WAS MISSED TWICE -- by me, and by the
    // instruction that told me to copy a method rather than to satisfy three properties. It needs no
    // adversary: leaving the old expression as a note beside its replacement is ordinary editing.
    [Fact]
    public void TheWindowConsultsTheHelperRatherThanDecidingForItself()
    {
        var source = WindowSource("ConfigWindow.cs");

        Assert.Contains("CampaignDisplayName.ToPreFill(campaign, carriedOverDefault, characterName)", source);
    }

    // >>> AND THE VALUE IS NAMED EXACTLY TWICE, WHICH IS THE HALF Contains CANNOT DO <<<
    //
    // A Contains passes BOTH demonstrated defeats: qa-1's attack B keeps the call and overrides the
    // result on the next line, and the Code Reviewer's bypass comments the call out in a /* */ block
    // and stops consulting the helper entirely. Each ADDS a third mention of carriedOverDefault --
    // once in the signature, once in the ToPreFill call, and once more to defeat it.
    //
    // So the count is the assertion. Same ceiling, one extra line, two demonstrated defeats closed.
    //
    // THE SEVERITY THIS IS ACTUALLY PROTECTING, and it is why a weak textual guard was not good
    // enough here: ToEdit(Campaign?, string?, DisplayName) and ToPreFill(Campaign?, string?,
    // DisplayName) have IDENTICAL SIGNATURES AND DIFFERENT RULES. Changing ToPreFill to ToEdit is a
    // ONE-WORD EDIT that compiles clean and reinstates BUG-141.
    //
    // THE RESIDUAL, STATED WHERE THE ASSERTION IS: re-reaching the value by another route -- reading
    // _configurationStore.Configuration.Settings.DisplayNameAlias directly inside the method --
    // names carriedOverDefault zero extra times and still defeats this. SMALLER, NOT CLOSED.
    [Fact]
    public void TheWindowNamesTheCarriedOverValueExactlyTwice()
    {
        var body = MethodBody(WindowSource("ConfigWindow.cs"), "private string DrawNameBox");

        // AN EQUALITY, WHICH CATCHES BOTH DIRECTIONS. Over-counting is qa-1's attack B (a third
        // mention added to override the result). UNDER-counting is the trailing-comment variant that
        // passes null to ToEdit and names the value only in the comment -- once the comment is
        // stripped the count is 1, and that variant reddens this row AND the wiring row. Measured.
        Assert.Equal(2, Occurrences(body, "carriedOverDefault"));
    }

    /// <summary>The source from a signature to the end of the file — enough to scope a count.</summary>
    private static string MethodBody(string source, string signature)
    {
        var at = source.IndexOf(signature, System.StringComparison.Ordinal);
        Assert.True(at >= 0, $"{signature} is not in the scanned source, so the count below would be over nothing.");

        var next = source.IndexOf("\n    private ", at + signature.Length, System.StringComparison.Ordinal);
        return next < 0 ? source[at..] : source[at..next];
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        for (var at = haystack.IndexOf(needle, System.StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(needle, at + needle.Length, System.StringComparison.Ordinal))
        {
            count++;
        }

        return count;
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

        // >>> THREE PROPERTIES, NOT A COPY. A COPY LOSES THE ONE IT DOES NOT SHARE, SILENTLY. <<<
        //
        // (1) BLOCKS FIRST, then lines: a // inside a /* */ belongs to the block, so stripping lines
        //     first strands the block's delimiters.
        // (2) SINGLELINE on the block strip, because a commented-out wiring call is MULTI-LINE.
        // (3) //[^\n]* RATHER THAN A StartsWith TEST -- and this is the property the first version
        //     could not express. StartsWith only drops a line that BEGINS with //, so a TRAILING
        //     comment survived and satisfied the scan while the code did something else.
        //
        // THE TRAILING SHAPE ARRIVES BY ACCIDENT, WHICH IS WHY IT MATTERS MORE THAN THE BLOCK ONE.
        // Leaving the old expression as a note beside its replacement is ordinary editing; the block
        // case needs somebody debugging and forgetting.
        //
        // It also strips // inside string literals. That makes the scan MORE aggressive, never less,
        // so it cannot manufacture a false PASS -- only a false failure, and only for an asserted
        // string containing //, which none here does.
        var code = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(path), @"/\*.*?\*/", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        return System.Text.RegularExpressions.Regex.Replace(code, @"//[^\n]*", string.Empty);
    }
}
