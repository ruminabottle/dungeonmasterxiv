using System;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// One participant as the DM's machine remembers them, within a single campaign.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ParticipantId"/> is generated fresh for each campaign a person appears in, and is
/// what a returning client relinks to (R-1.5). It deliberately carries no information: it is not
/// derived from a label, a character name, an account, or the session code, so the same person in
/// two campaigns is two unrelated UUIDs and nothing correlates them (D-8, A-1.11).
/// </para>
/// <para>
/// <see cref="Label"/> may hold a real character name. D-8 permits that here — this is the DM's
/// own local history — and forbids it in exports and in log lines. Nothing in this namespace
/// writes a label to a log.
/// </para>
/// </remarks>
public sealed class CampaignParticipant
{
    /// <summary>This participant's campaign-scoped identity. Stable within a campaign, meaningless outside it.</summary>
    public Guid ParticipantId { get; set; }

    /// <summary>What the DM calls them locally: an alias, or the character name the DM has seen.</summary>
    public string Label { get; set; } = string.Empty;
}
