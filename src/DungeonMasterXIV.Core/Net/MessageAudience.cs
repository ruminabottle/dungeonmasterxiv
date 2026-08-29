namespace DungeonMasterXIV.Net;

/// <summary>
/// Who is entitled to see a message (R-2.6). One expression, so nothing decides an audience twice.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PRIVACY DECISION LIVES HERE AND IS MADE ONCE.</b> A message is held, replayed, echoed and
/// exported by different code; if each decided the audience for itself, the four would drift and the
/// drift would be a disclosure. <c>MissedMessages</c> was measured to be structurally incapable of
/// widening an audience — <c>Replay</c> returns exactly what was <c>Hold</c>ed for that member — so
/// <b>the leak, if there is one, is authored at the moment of holding, by whoever chose to hold.</b>
/// This is the function that choice must consult.
/// </para>
/// <para>
/// <b>THE SENDER IS ALWAYS IN THE AUDIENCE OF THEIR OWN MESSAGE</b>, including a private one — a
/// person who cannot see what they just said would reasonably think it failed to send.
/// </para>
/// <para>
/// <b>ONLY THE HOST, NOT "ANYONE DM-ISH".</b> The privileged reader is
/// <see cref="SessionRole.DungeonMaster"/> alone. An Assistant is not the host: D-3 makes the host the
/// sole author of shared state, and widening this to a second role would hand DM-private traffic to
/// somebody the requirement never named.
/// </para>
/// <para>
/// <b>AND THE COUPLING TO R-2.10, WHICH THE PRD SAYS MUST NOT BE FORGOTTEN.</b> Holding messages for
/// a dropped member is only safe because there is no player-to-player privacy — every message the
/// host queues is one the host is already a legitimate party to. <b>If a third target is ever added,
/// this function and <c>MissedMessages</c> must change in the same breath</b>, because the hold path
/// would then be storing content the host must not read.
/// </para>
/// </remarks>
public static class MessageAudience
{
    /// <summary>
    /// Whether <paramref name="reader"/> is entitled to see a message with this target.
    /// </summary>
    /// <param name="target">Who the message was addressed to.</param>
    /// <param name="sender">Who sent it.</param>
    /// <param name="reader">The participant being considered.</param>
    /// <param name="readerRole">The reader's session-assigned role — never the sender's claim.</param>
    public static bool Includes(
        MessageTarget target,
        PeerCode sender,
        PeerCode reader,
        SessionRole readerRole) => target switch
    {
        MessageTarget.Everyone => true,
        MessageTarget.DungeonMasterOnly =>
            readerRole is SessionRole.DungeonMaster || reader.Equals(sender),

        // A target added later reaches here rather than defaulting to Everyone. Defaulting a new
        // privacy level to "visible to all" is the failure this arm exists to make impossible.
        _ => false,
    };
}
