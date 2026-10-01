using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

/// <summary>A session's log: a campaign identifier, the session's end in UTC ticks, and the logged entries.</summary>
public sealed record RetainedLog(Guid CampaignId, long EndedAtUtcTicks, IReadOnlyList<LoggedEntry> Entries);
