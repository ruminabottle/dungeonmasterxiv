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
/// object and reaching two parameters instead of three. They are kept apart because the D-3 door
/// boundary is a stronger claim than co-nullity, and a shape that reads better against the parameter
/// row is not worth blurring the boundary this file exists to hold.
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
