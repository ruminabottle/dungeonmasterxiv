using System;
using System.Globalization;
using System.Text;

namespace DungeonMasterXIV.Chat;

public readonly record struct MessageDraft(string? Text, MessageFault Fault, string? Reason)
{
    public bool IsAccepted => Fault == MessageFault.None;

    public static MessageDraft Compose(string? text, MessageLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Refused(MessageFault.Empty, "There was nothing to send.");
        }

        var trimmed = text.Trim();
        var characters = new StringInfo(trimmed).LengthInTextElements;

        if (characters > limits.MaxLength)
        {
            return Refused(
                MessageFault.TooLong,
                $"The message was {characters} characters; the limit is {limits.MaxLength}.");
        }

        var bytes = Encoding.UTF8.GetByteCount(trimmed);

        if (bytes > limits.MaxUtf8Bytes)
        {
            return Refused(
                MessageFault.TooLarge,
                $"The message was {bytes} bytes encoded; the limit is {limits.MaxUtf8Bytes}.");
        }

        return new MessageDraft(trimmed, MessageFault.None, null);
    }

    private static MessageDraft Refused(MessageFault fault, string reason) => new(null, fault, reason);
}
