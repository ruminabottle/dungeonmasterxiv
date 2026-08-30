using System;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Deleting a campaign deletes <b>everything stored for it</b>, including its retained log (R-2.12,
/// R-1.7a).
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS EXISTS SO A SENTENCE THE PRODUCT ALREADY SHIPS STAYS TRUE.</b> <c>ConfigWindow</c> tells
/// the user there is <i>"nothing to delete anywhere but here"</i>. Retention put a second thing on
/// disk. Without this composition the delete control would reach the campaign and leave the log
/// behind, and the shipped copy would assert a property the build lacks — <b>which misleads a user
/// who cannot check, about deletion, in a privacy notice.</b> The remedy is to make the sentence
/// true, not to edit it: the copy is R-1.7a verbatim and changing it is the Spec Owner's.
/// </para>
/// <para>
/// <b>DELIBERATELY NOT A METHOD ON <see cref="Campaigns.CampaignStore"/>.</b> A campaign persists a
/// roster, which is metadata; a log is what people said and did. DMXENG-103 rules that <b>a roll log
/// is NOT campaign data</b> and that moving one into the other is a PRODUCT decision rather than an
/// implementation one. Teaching the campaign store to own logs would settle that by writing it. Two
/// stores, one caller that deletes from both.
/// </para>
/// <para>
/// <b>And it is here rather than in the window because a rule with no possible test is where a defect
/// sits unseen.</b> No test project links the plugin, so a lambda in <c>CampaignListWindow</c> could
/// be read but never exercised — and "the delete control reaches the log" is exactly the claim that
/// must be exercised rather than read.
/// </para>
/// </remarks>
/// <param name="campaigns">The campaign store the existing control already deletes from.</param>
/// <param name="logs">The retained logs that must go with them.</param>
public sealed class CampaignDeletion(CampaignStore campaigns, RetainedLogStore logs)
{
    private readonly CampaignStore _campaigns =
        campaigns ?? throw new ArgumentNullException(nameof(campaigns));

    private readonly RetainedLogStore _logs =
        logs ?? throw new ArgumentNullException(nameof(logs));

    /// <summary>
    /// Deletes the campaign and its retained log, reporting whether <b>anything</b> was removed.
    /// </summary>
    /// <remarks>
    /// <b>BOTH SIDES ARE ALWAYS ATTEMPTED.</b> Written as <c>a | b</c> rather than <c>a || b</c>:
    /// <c>||</c> stops as soon as the LEFT side is true, so it would skip the log <b>every time the
    /// campaign was successfully deleted</b> — the ordinary path, and the one this type exists for.
    /// The log would survive its campaign on exactly the deletions a user actually performs.
    /// <para>
    /// <b>The reason above is corrected, and the correction is the point.</b> It first read
    /// <i>"short-circuiting would skip the log whenever the campaign was ALREADY GONE"</i>, which is
    /// backwards — an already-gone campaign returns false, so <c>||</c> would evaluate the log side
    /// and that case would keep working. <b>The code was right and the stated cause was wrong</b>,
    /// which no test catches on its own. What caught it was mutating <c>|</c> to <c>||</c> and
    /// noticing that the orphaned-log test <i>did not</i> redden the way the comment predicted:
    /// the reds landed on the ordinary-deletion cases instead.
    /// </para>
    /// </remarks>
    /// <param name="campaignId">The campaign being deleted.</param>
    /// <returns>True when a campaign or a log was removed.</returns>
    public bool Delete(Guid campaignId) => _campaigns.Delete(campaignId) | _logs.DeleteFor(campaignId);
}
