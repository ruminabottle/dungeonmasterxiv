using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Net;

/// <summary>Encodes session content to JSON and decodes it, dropping roster entries and lines that fail checks.</summary>
public static class SessionContentCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static SessionContent Vetted(
        SessionContent content, out int droppedRoster, out int droppedEntries)
    {
        var kept = VettedRoster(content.Roster, out droppedRoster);
        var lines = VettedEntries(content.Entries, out droppedEntries);

        return new SessionContent
        {
            Roster = kept,
            ClosingAtUtcTicks = content.ClosingAtUtcTicks,
            Leaving = content.Leaving,
            Entries = lines,
            Saying = content.Saying,
        };
    }

    private static IReadOnlyList<RosterEntry>? VettedRoster(
        IReadOnlyList<RosterEntry>? roster, out int dropped)
    {
        dropped = 0;
        if (roster is null)
        {
            return null;
        }

        var kept = roster
            .Where(entry => PeerCode.TryParse(entry.PeerCode, out _))
            .Select(entry => entry with
            {
                DisplayName = DisplayName.OrNone(entry.DisplayName).Value,
            })
            .ToList();

        dropped = roster.Count - kept.Count;
        return kept;
    }

    private static IReadOnlyList<StreamLine>? VettedEntries(
        IReadOnlyList<StreamLine>? entries, out int dropped)
    {
        dropped = 0;
        if (entries is null)
        {
            return null;
        }

        var kept = entries.Where(line => line.TryToEntry(out _)).ToList();
        dropped = entries.Count - kept.Count;
        return kept;
    }

    public static byte[] Encode(SessionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return JsonSerializer.SerializeToUtf8Bytes(content, Options);
    }

    public static bool TryDecode(
        byte[] plaintext,
        out SessionContent? content,
        ISessionTransportLog? log = null)
    {
        content = null;

        if (plaintext is null || plaintext.Length == 0)
        {
            return false;
        }

        try
        {
            content = JsonSerializer.Deserialize<SessionContent>(plaintext, Options);
        }
        catch (JsonException)
        {
            return false;
        }

        if (content is null)
        {
            return false;
        }

        content = Vetted(content, out var dropped, out var droppedEntries);

        if (droppedEntries > 0)
        {
            log?.Warning(
                $"Dropped {droppedEntries} stamped {(droppedEntries == 1 ? "entry" : "entries")} "
                + "that could not be read back as host-minted content. The rejected values are "
                + "deliberately not recorded here.");
        }

        if (dropped > 0)
        {
            log?.Warning(
                $"Dropped {dropped} roster {(dropped == 1 ? "entry" : "entries")} whose peer code "
                + "this build cannot have produced. Either this client's encoder is writing codes it "
                + "cannot parse back, or a keyholder is sending forged ones. The rejected value is "
                + "deliberately not recorded here.");
        }

        return true;
    }
}
