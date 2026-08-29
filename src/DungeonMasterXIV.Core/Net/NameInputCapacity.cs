using System.Text;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Whether a name field has run out of room, so the window can say so (A-1.2v).
/// </summary>
/// <remarks>
/// <para>
/// <b>A-1.2v: a name refused for length is refused VISIBLY — never silently truncated, never
/// silently dropped.</b> It binds every layer that can discard what the user typed, and the input
/// field is one of those layers. A box that stops accepting keystrokes with no explanation is the
/// failure the criterion exists to forbid: the user is entering a name and the product is not
/// responding.
/// </para>
/// <para>
/// <b>This is NOT in <see cref="DisplayName"/>, and the reason is that type's own doc.</b> It says
/// the buffer <i>"is not the rule — it is UI capacity; <c>TryParse</c> is the gate."</i> Taking that
/// at its word, whether a UI buffer is full is not a question about whether a name is valid, and it
/// does not belong beside the validity rule. <see cref="JoinFlowCode"/> is the precedent: a decision
/// about what an input field accepts, kept in Core so a test can call the same thing the window
/// calls rather than re-deriving it and asserting the two agree in a comment.
/// </para>
/// <para>
/// <b>WHY THIS CANNOT BE FIXED BY A BIGGER BUFFER, which is the reason the check exists at all.</b>
/// A grapheme cluster may carry arbitrarily many combining marks and A-1.2i needs marks, so a
/// name of <see cref="DisplayName.MaxLength"/> graphemes has <b>no finite byte ceiling</b>. Measured
/// against the shipped constant: a base letter with three marks each is 224 bytes and fits; with
/// four it is 288 and does not; with five, 352. <b>All three are accepted by
/// <see cref="DisplayName.TryParse"/>.</b> Doubling the buffer moves the cliff to eight marks per
/// character; it does not remove one. A-1.2v-note rules the attempt out in those terms — a change
/// that raises the buffer and reports nothing does not discharge the criterion however large the
/// number. <b>The only conforming answer is to tell the user, which is what this is for.</b>
/// </para>
/// </remarks>
public static class NameInputCapacity
{
    /// <summary>
    /// The most bytes a single Unicode code point takes in UTF-8.
    /// </summary>
    /// <remarks>
    /// <b>A code point, not a character.</b> A grapheme cluster is one or more code points, so
    /// four bytes of headroom does not promise that one more <i>character</i> fits — it is the
    /// point past which not even one more code point is guaranteed to. That is deliberately the
    /// weaker claim, because the stronger one is not available: see the class remarks.
    /// </remarks>
    private const int LargestCodePointBytes = 4;

    /// <summary>
    /// True when <paramref name="typed"/> has filled the field, so further keystrokes are refused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Fires slightly EARLY, on purpose, and the direction is the whole design.</b> Firing late
    /// means the box is full and nothing is said — which is the A-1.2v failure itself, so a late
    /// check is no check. Firing early means the message appears while a character or two of room
    /// remains, which is a smaller and visible cost. Given a choice between the two, the criterion
    /// picks for us.
    /// </para>
    /// <para>
    /// <b>It is also why this does not depend on where exactly the field stops.</b> ImGui's
    /// <c>buf_size</c> is a byte count and the terminator lives inside it, so the largest content is
    /// one less than <see cref="DisplayName.MaxUtf8Bytes"/> — but whether the true ceiling is that
    /// or one byte either side, a threshold four bytes short of it is under both. <b>Nothing here
    /// rests on a claim about ImGui's internals</b>, which is the class of claim that produced the
    /// wrong answer the first time this was reasoned about rather than measured.
    /// </para>
    /// <para>
    /// <b>What it does NOT tell you.</b> Only that no more will be accepted from here — never that
    /// something already WAS lost. A user who typed exactly to the ceiling and stopped has lost
    /// nothing, and the copy this drives must not claim otherwise.
    /// </para>
    /// </remarks>
    public static bool IsFull(string typed) =>
        typed is not null
        && Encoding.UTF8.GetByteCount(typed) + LargestCodePointBytes >= DisplayName.MaxUtf8Bytes;
}
