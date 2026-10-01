using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

/// <summary>A session log kept for a campaign: the campaign, the session's end in UTC ticks, and its entries.</summary>
public sealed record RetainedLog(Guid CampaignId, long EndedAtUtcTicks, IReadOnlyList<LoggedEntry> Entries);
