using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// One campaign file: a schema version and exactly one campaign.
/// </summary>
/// <remarks>
/// <para>
/// <b>The root holds a single campaign, and that is what satisfies A-1.11b</b> — "no single
/// campaign file contains more than one session code". A file cannot carry two codes because the
/// shape cannot express two campaigns. That is a stronger guarantee than a rule saying not to, and
/// it is the reason this is a separate type rather than the old document with a length check.
/// </para>
/// <para>
/// <b>What this is NOT.</b> Splitting the store into one file per campaign does not deliver A-1.11
/// and must not be described as though it does. Two files in one folder, each naming the same
/// person under a different code, link that person exactly as well as one file did — people zip
/// folders, not files. A-1.11 was rescoped on 2026-08-27 to cover what leaves the machine, and the
/// honest reason for this layout is narrower: it bounds blast radius when someone attaches a single
/// file to a bug report.
/// </para>
/// </remarks>
public sealed class CampaignFileDocument
{
    /// <summary>
    /// The schema version this build writes for a campaign file. Independent of the version the
    /// old single-file store used; the two are told apart by file name, not by number.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The schema version of this file, stamped immediately before every write so it records the
    /// shape that was written rather than the shape that was loaded.
    /// </summary>
    public int Version { get; set; } = CurrentSchemaVersion;

    /// <summary>The campaign this file holds. One, never more.</summary>
    public Campaign? Campaign { get; set; }

    /// <summary>
    /// Properties this build does not know about, kept so a load-and-save cycle does not silently
    /// delete them.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownProperties { get; set; }
}
