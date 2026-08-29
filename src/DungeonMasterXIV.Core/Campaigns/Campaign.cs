using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// What the DM calls this campaign, or null when they have never renamed it (A-1.9k, R-1.5d).
    /// </summary>
    /// <remarks>
    /// <b>Null is the ordinary case, not a missing value.</b> A campaign nobody has renamed shows an
    /// automatic name composed from <see cref="CreatedUtc"/> at display time — see
    /// <see cref="CampaignName"/> — so this field exists to hold a RENAME rather than to be
    /// populated at creation. That is what makes the field additive: every campaign written by an
    /// older build reads back with null here and still displays correctly, so nothing is backfilled
    /// and <c>CurrentSchemaVersion</c> does not move.
    /// </remarks>
    public string? Name { get; set; }

    /// <summary>Everyone the DM has admitted to this campaign, with their campaign-scoped UUIDs.</summary>
    public List<CampaignParticipant> Participants { get; set; } = new();

    /// <summary>When this campaign was first created, for ordering the campaign list.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Properties this build does not know about, kept so a load-and-save cycle does not silently
    /// delete them. Without this, <c>System.Text.Json</c> drops unrecognised fields on read and the
    /// next write erases them — data loss with no error, in someone else's build.
    /// </summary>
    /// <remarks>
    /// Under D-12 a tester rolling back to an older build is an expected case rather than a
    /// corruption scenario, which is what makes this worth carrying. It does not replace the schema
    /// version gate: a document from a NEWER schema version is still refused and preserved rather
    /// than read through this. This covers fields added without a version bump.
    /// </remarks>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownProperties { get; set; }

    /// <summary>
    /// Unknown properties from the enclosing campaign FILE, carried so a load-and-save cycle does
    /// not delete them either. Not serialized as part of the campaign — the file codec lifts it
    /// back onto the file root on write.
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, JsonElement>? FileUnknownProperties { get; set; }
}
