using System;
using System.Linq;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Resolves a returning client's claimed participant against a campaign's own records (R-1.5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolving a claim admits nobody.</b> This returns a <see cref="RelinkClaim"/>, which carries a
/// label and no authority, and there is deliberately no method here that accepts, admits or
/// approves. The DM approves every relink, every session; a match changes the wording of the prompt
/// and never the number of steps to get past it.
/// </para>
/// <para>
/// The claimed id is <b>unauthenticated text from the joining client</b>. Anyone can send any UUID.
/// That is tolerable only because nothing is granted on the strength of it — the worst a correct
/// guess achieves is a prompt that says "relink as Ysera" instead of "join", which a DM still has to
/// answer, and which a DM who does not recognise the request denies.
/// </para>
/// </remarks>
public static class CampaignRelink
{
    /// <summary>
    /// Looks up a claimed participant. Returns <see cref="RelinkClaim.None"/> for no claim, a claim
    /// that is not a UUID, an unknown campaign, or a participant this campaign has never seen.
    /// </summary>
    /// <param name="campaign">The campaign being joined, or <c>null</c> if none is open.</param>
    /// <param name="claimedParticipantId">What the joining client sent, if anything.</param>
    public static RelinkClaim Resolve(Campaign? campaign, string? claimedParticipantId)
    {
        if (campaign is null || !Guid.TryParseExact(claimedParticipantId, "D", out var claimed))
        {
            return RelinkClaim.None;
        }

        var participant = campaign.Participants.FirstOrDefault(known => known.ParticipantId == claimed);

        // The label is read off the participant that was found, never off the request. What the DM
        // reads has to describe who this campaign already knows, not who the caller says they are.
        return participant is null ? RelinkClaim.None : new RelinkClaim(true, participant.Label);
    }
}
