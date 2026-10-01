using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Data;

public static class StreamLogProjection
{
    public static LoggedEntry From(StreamEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new LoggedEntry(
            new LoggedStamp(entry.Stamp.Sequence, entry.Stamp.AtUtcTicks),
            NameOf(entry.Kind),
            entry.Peer.Value,
            entry.Text);
    }

    public static IReadOnlyList<LoggedEntry> From(IEnumerable<StreamEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Select(From).ToList();
    }

    private static string NameOf(StreamEventKind kind) => kind switch
    {
        StreamEventKind.Message => "message",
        StreamEventKind.Roll => "roll",
        StreamEventKind.Joined => "joined",
        StreamEventKind.Left => "left",
        StreamEventKind.Dropped => "dropped",
        StreamEventKind.Reconnected => "reconnected",

        StreamEventKind.Gap => "gap",

        _ => throw new NotSupportedException(
            $"The log projection has not been taught the stream event kind '{kind}'. "
            + "The stream gained a kind after this file was written; teach it here rather than "
            + "letting an export invent or discard the line."),
    };
}
