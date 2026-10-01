using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

/// <summary>Builds a campaign's session log from supplied entries and asks the store to retain it.</summary>
public sealed class SessionLogRetention(
    RetainedLogStore store,
    Guid campaignId,
    Func<IReadOnlyList<LoggedEntry>> entries)
{
    private readonly RetainedLogStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    private readonly Func<IReadOnlyList<LoggedEntry>> _entries =
        entries ?? throw new ArgumentNullException(nameof(entries));

    public int Attempts { get; private set; }

    public bool Retain(bool isHosting, long endedAtUtcTicks)
    {
        Attempts++;

        var log = new RetainedLog(campaignId, endedAtUtcTicks, _entries());
        return _store.Retain(log, isHosting);
    }
}
