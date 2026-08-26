using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// One campaign on the DM's machine: who has played in it, and what it is called.
/// </summary>
/// <remarks>
/// <para>
/// <b>The identity is <see cref="CampaignId"/>, and only ever that.</b> R-1.2a settles that a
/// session code identifies a <i>live session</i>, not a campaign — the code is the campaign's
/// default label and nothing more. A DM whose usual code is unavailable at resume takes a new one
/// and keeps the campaign, which is only true if no lookup anywhere keys on the code.
/// </para>
/// <para>
/// <see cref="PreferredCode"/> is therefore deliberately mutable, deliberately optional, and
/// deliberately not unique: two campaigns may carry the same preferred code without being related,
/// and a campaign may carry none at all before it has ever been hosted.
/// </para>
/// </remarks>
public sealed class Campaign
{
    /// <summary>
    /// This campaign's identity: generated locally, never derived from anything, never reused.
    /// This is the store's only key (R-1.6).
    /// </summary>
    public Guid CampaignId { get; set; }

    /// <summary>
    /// The session code this DM likes to use for this campaign, as a label to display and to ask
    /// the relay for. Null when the campaign has never been hosted. Never a key — see the remarks.
    /// </summary>
    public string? PreferredCode { get; set; }

    /// <summary>Everyone the DM has admitted to this campaign, with their campaign-scoped UUIDs.</summary>
    public List<CampaignParticipant> Participants { get; set; } = new();

    /// <summary>When this campaign was first created, for ordering the campaign list.</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
