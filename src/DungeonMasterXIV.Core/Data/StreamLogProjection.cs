using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Data;

/// <summary>Converts session stream entries into log entries, naming each event kind.</summary>
public static class StreamLogProjection
{
    public static LoggedEntry From(StreamEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var privacy = entry.Privacy;
        return new LoggedEntry(
            new LoggedStamp(entry.Stamp.Sequence, entry.Stamp.AtUtcTicks),
            NameOf(entry.Kind),
            entry.Peer.Value,
            entry.Text,
            privacy is null ? null : AudienceNameOf(privacy.Audience.Kind),
            privacy?.Audience.To,
            privacy?.RevealedBy,
            privacy?.RevealedAtUtcTicks);
    }

    public static IReadOnlyList<LoggedEntry> From(IEnumerable<StreamEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Select(From).ToList();
    }

    private static string AudienceNameOf(AudienceKind kind) => kind switch
    {
        AudienceKind.DmSide => "dm-side",
        AudienceKind.Blind => "blind",
        AudienceKind.Player => "player",

        _ => throw new NotSupportedException(
            $"The log projection has not been taught the audience kind '{kind}'. "
            + "A new private audience kind needs a word here, or exports will throw."),
    };

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
