using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// One participant as the DM's machine remembers them, within a single campaign.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ParticipantId"/> is generated fresh for each campaign a person appears in, and is
/// what a returning client relinks to (R-1.5). It deliberately carries no information: it is not
/// derived from a label, a character name, an account, or the session code, so <b>the identifiers
/// are uncorrelated across campaigns</b> (D-8).
/// </para>
/// <para>
/// <b>That is necessary for A-1.11 and it is not sufficient, so do not read it as the whole
/// promise.</b> <see cref="Label"/> is not rotated and is the field most likely to be identical
/// across two campaigns, because it is how the DM recognises someone — so a reader of the stored
/// file can still correlate a person across two session codes by label alone. The label is
/// retained deliberately: D-8 permits real character names in the DM's own local history and
/// forbids them only in exports and in lines we log. Whether that retention satisfies A-1.11 as
/// written is an open product question, not an oversight here.
/// </para>
/// </remarks>
public sealed class CampaignParticipant
{
    /// <summary>This participant's campaign-scoped identity. Stable within a campaign, meaningless outside it.</summary>
    public Guid ParticipantId { get; set; }

    /// <summary>What the DM calls them locally: an alias, or the character name the DM has seen.</summary>
    public string Label { get; set; } = string.Empty;

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
}
