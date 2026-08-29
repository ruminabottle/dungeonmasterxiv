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
/// <b>And the wrong door will not compile.</b> The two types' members share names and share no
/// types: a key here is a single <c>byte[]</c>, there a function returning many; a handler here takes
/// content alone, there content and the peer that decrypted it. Supplying one where the other was
/// meant is a compile error rather than a D-3 inversion found in review — which is a stronger
/// guarantee than the two-delegates argument this pair was split on, and it comes free.
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
