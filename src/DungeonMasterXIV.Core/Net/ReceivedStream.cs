using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>Keeps the stream lines a member has received from the host, in sequence order.</summary>
internal sealed class ReceivedStream
{
    private readonly SortedDictionary<long, StreamLine> _lines = new();

    public IReadOnlyList<StreamLine> Lines => _lines.Values.ToList();

    public long LastSequence => _lines.Count == 0 ? 0 : _lines.Keys.Last();

    public void Add(IReadOnlyList<StreamLine>? lines)
    {
        if (lines is null)
        {
            return;
        }

        foreach (var line in lines)
        {
            _lines[line.Sequence] = line;
        }
    }

    public void Clear() => _lines.Clear();
}
