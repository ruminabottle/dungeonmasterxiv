using System;

namespace DungeonMasterXIV.Net;

/// <summary>Formats a chat message as one line with its speaker, role tag, privacy tag and kind.</summary>
public static class MessageLine
{
    public static string Attribution(string speaker, DisplayName person) =>
        string.Equals(speaker, person.Value, StringComparison.Ordinal)
            ? person.Value
            : $"{speaker} ({person.Value})";

    public static string Render(
        MessageKind kind,
        MessageTarget target,
        string speaker,
        DisplayName person,
        SessionRole role,
        string text)
    {
        var who = Attribution(speaker, person);
        var authority = role is SessionRole.DungeonMaster ? $"[{SessionRoleLabel.For(role)}] " : string.Empty;
        var privacy = target is MessageTarget.DungeonMasterOnly ? "(private) " : string.Empty;

        return kind switch
        {
            MessageKind.Emote => $"{privacy}{authority}* {who} {text}",
            MessageKind.OutOfCharacter => $"{privacy}{authority}(OOC) {who}: {text}",
            MessageKind.InCharacter => $"{privacy}{authority}{who}: {text}",

            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "Unknown message kind — R-2.5 requires each to be distinguishable."),
        };
    }
}
