using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>One line of a member's view, with the sequence of the recorded entry it came from.</summary>
internal readonly record struct ViewedLine(long RecordSequence, StreamLine Line);

/// <summary>Builds the stream one member may receive from the host's record, numbered 1, 2, 3… for that member alone.</summary>
internal static class MemberView
{
    public static List<ViewedLine> Of(IReadOnlyList<StreamEntry> record, string seat, bool addressable)
    {
        var view = new List<ViewedLine>(record.Count);
        foreach (var entry in record)
        {
            if (Shape(entry, seat, addressable) is { } line)
            {
                view.Add(new ViewedLine(entry.Stamp.Sequence, line with { Sequence = view.Count + 1 }));
            }
        }

        return view;
    }

    private static StreamLine? Shape(StreamEntry entry, string seat, bool addressable)
    {
        if (entry.Privacy is not { } privacy || privacy.IsRevealed || (addressable && privacy.Entitles(seat)))
        {
            return StreamLine.From(entry);
        }

        if (entry.Kind != StreamEventKind.Roll)
        {
            return null;
        }

        return new StreamLine(
            0, entry.Stamp.AtUtcTicks, StreamEventKind.Message, entry.Peer.Value, privacy.Placeholder, Withheld: true);
    }
}
