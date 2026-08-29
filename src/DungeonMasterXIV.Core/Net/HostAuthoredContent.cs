using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// How this client opens <b>host-authored</b> content, and what it does with what opens.
/// </summary>
/// <remarks>
/// <para>
/// <b>One of the two doors <see cref="InboundHandlers"/> bolds, made a type (DMXENG-59).</b> The key
/// belongs with the handler rather than beside it — as the record has said since DMXENG-50, it is
/// not configuration, it is the thing that decides whether <see cref="OnContent"/> can be called at
/// all. The pair is also null together: both are absent on a pure host, which authors the roster and
/// never receives one.
/// </para>
/// <para>
/// <b>THE MEMBERS ARE NAMED THE SAME AS <see cref="MemberAuthoredContent"/>'S AND THAT IS THE POINT.</b>
/// <c>OpenMemberContentWith</c> and <c>OnMemberContent</c> carried their qualifier in the member name
/// only because both doors lived in one flat parameter list. The type now supplies the qualifier, so
/// the call site reads <c>HostAuthored.OpenWith</c> against <c>MemberAuthored.OpenWith</c> — the
/// distinction stated once, where it is chosen.
/// </para>
/// <para>
/// <b>And the wrong door will not compile.</b> Supplying one where the other was meant is a compile
/// error rather than a D-3 inversion found in review — a stronger guarantee than the two-delegates
/// argument this pair was split on, and it comes free.
/// </para>
/// <para>
/// <b>THE MECHANISM IS TYPE IDENTITY, NOT MEMBER SHAPE (BUG-109).</b> This paragraph used to say the
/// guarantee holds because the two types "share names and share no types", naming the differing
/// <c>byte[]</c> versus function and the differing handler arities as the reason. That was the wrong
/// mechanism for a right conclusion. These are record structs, so they are NOMINALLY typed and never
/// interconvert whatever their members look like — measured, with two record structs whose shapes
/// were made IDENTICAL, which still refused the swap in both directions with
/// <c>CS1503, cannot convert</c>.
/// </para>
/// <para>
/// <b>What that retraction does NOT claim.</b> The guarantee itself is untouched and is if anything
/// stronger than the old wording allowed: it does not depend on the shapes staying different. The
/// shapes ARE different, and that remains true — it is simply not what refuses the swap.
/// </para>
/// <para>
/// <b>Why the wrong mechanism reads convincingly, which is the part worth keeping.</b> Swap the two
/// doors for real and the compiler emits a MIX of <c>CS1503</c> and <c>CS1593</c>, and the
/// <c>CS1593</c>s come from the delegate arity. So the evidence at the call site genuinely does look
/// like shape is doing the work, and a careful person reading their own probe output would write
/// exactly what was written here. The harmful direction is the other one: a future change that
/// ALIGNED the two shapes would not weaken this guarantee at all, while the old sentence implied it
/// would — inviting someone to preserve a difference they do not need, or to add a defensive test
/// for a case the compiler already refuses.
/// </para>
/// </remarks>
/// <param name="OpenWith">
/// The shared key to open inbound <b>host-authored</b> content with, or null before one exists. A
/// key derived during the same drain takes precedence — see the call site. Null on a pure host,
/// which is correct: a host authors the roster and never receives one.
/// </param>
/// <param name="OnContent">
/// Called for each <b>host-authored</b> payload this client could open (D-11). Payloads sealed for
/// somebody else are ordinary traffic and pass in silence.
/// </param>
public readonly record struct HostAuthoredContent(
    byte[]? OpenWith = null,
    Action<SessionContent>? OnContent = null);
