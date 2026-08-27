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
/// <b>That is necessary and not sufficient, so do not read it as the whole promise.</b>
/// <see cref="Label"/> is not rotated and is the field most likely to be identical across two
/// campaigns, because it is how the DM recognises someone — so a reader of the stored file can
/// still correlate a person across two session codes by label alone.
/// </para>
/// <para>
/// That is permitted here and forbidden elsewhere, and the split is deliberate. <b>A-1.11 was
/// narrowed on 2026-08-27 to cover what leaves the machine</b> — exports and relay traffic — after
/// it was found to contradict D-8, which explicitly allows real character names in the DM's own
/// local history. So retaining a label locally is correct rather than tolerated. What the single
/// shared file is inconsistent with is <b>A-1.11b</b> (no one campaign file holds more than one
/// session code), which exists to bound blast radius when someone attaches a file to a bug
/// report — not as a claim that per-campaign files deliver A-1.11.
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
