namespace DungeonMasterXIV.Net;

/// <summary>
/// What a client does with what arrives, and the keys it can open content with.
/// </summary>
/// <remarks>
/// <para>
/// <b>One parameter because it is one concern</b>, not to shorten a signature.
/// <see cref="AdmissionInbox.Drain"/> had reached six parameters — the block row in the engineering
/// standards — and four of them defaulted, which is the readability tell that goes with it: call
/// sites had begun carrying meaning in argument order rather than in names.
/// </para>
/// <para>
/// They travel together because they answer one question: <i>this frame arrived, now what?</i> Each
/// key belongs with the handler it enables rather than beside it — it is not configuration, it is
/// the thing that decides whether that handler can be called at all.
/// </para>
/// <para>
/// <b>Moved out of <c>AdmissionInbox.cs</c> by DMXENG-50, and the move fixed a defect rather than
/// only making room.</b> The two types shared one contiguous doc-comment block there, so the
/// compiler attached <i>all</i> of it — including the paragraphs written about the inbox — to this
/// record, and <c>AdmissionInbox</c> came out of the build with no documentation at all. Two types
/// in one file is legal; two types sharing one comment block silently reassigns the prose.
/// </para>
/// <para>
/// <b>THERE ARE TWO DOORS HERE, NOT ONE, AND THE SPLIT IS THE D-3 BOUNDARY MADE STRUCTURAL.</b>
/// <see cref="HostAuthoredContent"/> carries <b>host-authored</b> content to a joiner.
/// <see cref="MemberAuthoredContent"/> carries <b>member-authored</b> content to a host. Merging them
/// would be smaller and would be wrong: see <see cref="MemberAuthoredContent.OnContent"/> for what it
/// costs. Since DMXENG-59 the two doors are two TYPES, so the wrong one will not compile.
/// </para>
/// <para>
/// <b>Why the six members group into exactly these three, and not some other three (DMXENG-59).</b>
/// The record was AT the parameter block (6 of 6), so <see cref="MemberAuthoredContent"/>'s sibling
/// door could not gain a seventh member and DMXENG-58 was blocked behind it. Grouping by the D-3
/// boundary the file already bolded gives the two doors and leaves admission as the remainder —
/// which is a group in its own right, not a leftover: both its members are supplied from the
/// admission side and both are null on exactly the same clients.
/// </para>
/// <para>
/// <b>The residual, stated rather than left for a reviewer to find.</b>
/// <see cref="JoinerAdmission"/> and <see cref="MemberAuthoredContent"/> share a nullity condition —
/// both are host-only — so co-nullity alone would have permitted merging them into one host-side
/// object and reaching two parameters instead of three.
/// </para>
/// <para>
/// They are kept apart <b>because admission is a group by PROVENANCE</b>: both its members are
/// <b>supplied from the admission side</b>, which is what makes it a group in its own right rather
/// than whatever was left over. A shape that reads better against the parameter row is not worth
/// dissolving a grouping that has its own reason to exist.
/// </para>
/// <para>
/// That reason is the CLAUSE and not the sentence it sits in. The paragraph above joins it to
/// "both are null on exactly the same clients" with an <i>and</i>, and that second half is
/// co-nullity — the property this residual exists to call weaker. Anyone following the reference
/// lands on the whole sentence, so: the load-bearing half is provenance, and the co-nullity half is
/// carried along, not relied upon.
/// </para>
/// <para>
/// <b>This paragraph used to cite the D-3 door boundary, and that was the wrong reason for the
/// right conclusion (BUG-109).</b> D-3 as stated above is the boundary BETWEEN THE TWO CONTENT
/// DOORS. <see cref="JoinerAdmission"/> carries <see cref="JoinerAdmission.OnJoinRequest"/> and
/// <see cref="JoinerAdmission.OnComparabilityReceipt"/>, and neither is content — so it sits on
/// neither side of that boundary, and merging it into <see cref="MemberAuthoredContent"/> would not
/// cross D-3 at all. The host-authored/member-authored split would survive such a merge untouched.
/// A reader who tested the old reason would find it did not apply, and the natural next move is to
/// conclude the merge is fine after all: <b>a wrong justification where a change is meant to be
/// prevented is worse than none, because it invites the review that overturns it.</b>
/// </para>
/// <para>
/// <b>WHAT THIS RETRACTION DOES NOT CLAIM, because a reader who believed the old reason needs to
/// know exactly which part failed.</b> The D-3 boundary between the two content doors is UNTOUCHED
/// by any of this and the doors are not in question: what was wrong is that the boundary was cited
/// for a merge it does not reach, not that the boundary is weak. Only the third party to the
/// question moved. Without this sentence the next reader re-derives the finding and concludes the
/// doors themselves are in danger, which they are not.
/// <para>
/// The co-nullity clause is deliberately not load-bearing here. It is the property this paragraph
/// exists to call weaker, so resting the replacement on it would inherit the weakness the original
/// was written to escape.
/// </para>
/// <para>
/// <b>And the protection D-3 does give is NOMINAL rather than structural.</b> The two doors are
/// distinct named record structs, so passing one where the other belongs fails on TYPE IDENTITY —
/// measured with two record structs whose member shapes were made IDENTICAL, which still refused
/// with <c>CS1503, cannot convert</c>. It would therefore still fire if a future change aligned the
/// two doors' shapes. Worth stating because a swap probe on the real types emits <c>CS1593</c> from
/// the differing delegate arities, which reads as though the shapes were the protection.
/// </para>
/// </remarks>
/// <param name="Admission">
/// What a host is told while a joiner is trying to get in. <c>default</c> on every joiner-only
/// client — see <see cref="JoinerAdmission"/>.
/// </param>
/// <param name="HostAuthored">
/// How this client opens host-authored content and what it does with it. <c>default</c> on a pure
/// host — see <see cref="HostAuthoredContent"/>.
/// </param>
/// <param name="MemberAuthored">
/// How a host opens member-authored content and what it does with it. <c>default</c> on every
/// joiner-only client — see <see cref="MemberAuthoredContent"/>.
/// </param>
public readonly record struct InboundHandlers(
    JoinerAdmission Admission = default,
    HostAuthoredContent HostAuthored = default,
    MemberAuthoredContent MemberAuthored = default);
