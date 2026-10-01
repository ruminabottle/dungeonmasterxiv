using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

public sealed record RetainedLog(Guid CampaignId, long EndedAtUtcTicks, IReadOnlyList<LoggedEntry> Entries);
