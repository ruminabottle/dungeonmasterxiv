namespace DungeonMasterXIV.Net;

/// <summary>
/// Who is entitled to see a message (R-2.6). One expression, so nothing decides an audience twice.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PRIVACY DECISION LIVES HERE AND IS MADE ONCE.</b> A message is held, replayed, echoed and
/// exported by different code; if each decided the audience for itself, the four would drift and the
/// drift would be a disclosure. This is the function they must all consult.
/// </para>
/// <para>
/// <b>DEFAULT PEER CODES COLLIDE, AND THAT IS AN ACTIVE HAZARD IN ANYTHING KEYED OR COMPARED.</b>
/// <c>default(PeerCode)</c> equals every other default and hashes to 0 — DMXENG-105.
/// <b>No claim is made here about what any other type can or cannot do.</b> An earlier version of this
/// paragraph carried an argument — a premise, a <i>so</i>, and a conclusion — built on a premise that
/// was later withdrawn. Marking it retracted was not enough: <b>a dead citation is inert, but an
/// argument keeps producing conclusions after its premise dies</b>, so it is cut rather than annotated.
/// </para>
/// <para>
/// <b>AND THE SAME ROOT CAUSE WAS LIVE HERE.</b> <c>reader.Equals(sender)</c> is TRUE for two absent
/// codes, so an absent reader was admitted to DM-private traffic whenever the sender was also absent.
/// Measured on this file before it was fixed. <c>PeerCode</c>'s own remarks state the precondition —
/// <i>a caller that defaults one has an absent code, not a valid one, and must treat it as a
/// refusal</i> — and this is now the third writer found not honouring it.
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
        SessionRole readerRole)
    {
        // PeerCode's stated precondition, honoured rather than assumed: an absent code is not a
        // participant and must be treated as a refusal. Without this, reader.Equals(sender) is TRUE
        // for two defaults -- default equals default -- and an absent reader is admitted to
        // DM-private traffic. Refusing on BOTH codes rather than only the reader: an absent sender
        // means the message has no identifiable author, and admitting anyone on that basis is the
        // same defect with the operands swapped.
        if (!sender.IsPresent || !reader.IsPresent)
        {
            return false;
        }

        return target switch
        {
            MessageTarget.Everyone => true,
            MessageTarget.DungeonMasterOnly =>
                readerRole is SessionRole.DungeonMaster || reader.Equals(sender),

            // A target added later reaches here rather than defaulting to Everyone. Defaulting a new
            // privacy level to "visible to all" is the failure this arm exists to make impossible.
            _ => false,
        };
    }
}
