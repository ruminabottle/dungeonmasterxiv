using System;
using System.Globalization;
using System.Text;

namespace DungeonMasterXIV.Chat;

/// <summary>
/// The outcome of composing a message: the text to send, or a fault naming what was wrong (R-2.19,
/// A-2.35).
/// </summary>
/// <param name="Text">What to send, or null when <paramref name="Fault"/> is set.</param>
/// <param name="Fault">Which fault, or <see cref="MessageFault.None"/>.</param>
/// <param name="Reason">A human-readable statement of the fault, or null.</param>
/// <remarks>
/// <para>
/// <b>THREE OUTCOMES AND ONLY ONE PASSES A-2.35: accepted, or refused WITH THE FAULT NAMED.</b>
/// Silent truncation fails the criterion because the sender believes they said something they did
/// not; a silent drop fails it for the same reason one layer along. <b>So there is deliberately no
/// way to express "shortened it for you" in this type</b> — the shape is what keeps the third
/// outcome unreachable rather than merely discouraged.
/// </para>
/// <para>
/// <b>Bounded BEFORE it is reachable, not after</b> — <see cref="DungeonMasterXIV.Rolls.RollLimits"/>
/// records why: <i>the input is untrusted the day it is wired, not the day someone remembers</i>.
/// </para>
/// </remarks>
public readonly record struct MessageDraft(string? Text, MessageFault Fault, string? Reason)
{
    /// <summary>Whether this draft may be sent.</summary>
    public bool IsAccepted => Fault == MessageFault.None;

    /// <summary>
    /// Reads <paramref name="text"/> against <paramref name="limits"/>, accepting it or refusing it
    /// with the fault named.
    /// </summary>
    /// <param name="text">What the person typed. May be null.</param>
    /// <param name="limits">The bounds to apply.</param>
    /// <remarks>
    /// <para>
    /// <b>Trimmed before measuring, and the trimmed text is what travels.</b> Trailing whitespace is
    /// not something a person means to say, and measuring before trimming would refuse a message
    /// for length the sender cannot see.
    /// </para>
    /// <para>
    /// <b>THE CHARACTER BOUND IS CHECKED FIRST AND THAT ORDER IS DELIBERATE.</b> Both bounds can be
    /// crossed at once by a long message in a heavy script, and the two refusals do not read the
    /// same to the person who typed it: <see cref="MessageFault.TooLong"/> names a length they can
    /// see and act on, while <see cref="MessageFault.TooLarge"/> names an encoded size they cannot.
    /// <b>Reporting the byte fault for an ordinarily-too-long message would be true and useless.</b>
    /// </para>
    /// </remarks>
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

    /// <summary>A refusal naming its fault.</summary>
    private static MessageDraft Refused(MessageFault fault, string reason) => new(null, fault, reason);
}
