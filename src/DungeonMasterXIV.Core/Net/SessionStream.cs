using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

public sealed class SessionStream
{
    private readonly List<StreamEntry> _entries = new();

    public IReadOnlyList<StreamEntry> Entries => _entries;

    public bool Record(StreamEntry entry)
    {
        if (entry.Stamp.Sequence < 1)
        {
            return false;
        }

        var at = _entries.Count;
        while (at > 0 && _entries[at - 1].Stamp.Sequence > entry.Stamp.Sequence)
        {
            at--;
        }

        if (at > 0 && _entries[at - 1].Stamp.Sequence == entry.Stamp.Sequence)
        {
            return false;
        }

        _entries.Insert(at, entry);
        return true;
    }
}
