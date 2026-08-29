using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// How a <b>host</b> opens <b>member-authored</b> content, and what it does with what opens.
/// </summary>
/// <remarks>
/// <para>
/// <b>The second of the two doors, and the one whose separation is load-bearing (R-1.3k, DMXENG-59).</b>
/// Both members are null on every joiner-only client, which has nobody to hear from. See
/// <see cref="HostAuthoredContent"/> for why the two types name their members identically, and
/// <see cref="InboundHandlers"/> for the D-3 boundary they make structural.
/// </para>
/// <para>
/// <b>SEPARATE FROM <see cref="HostAuthoredContent"/> ON PURPOSE, AND MERGING THEM INVERTS D-3.</b>
/// <c>SessionCoordinator.Roster</c> documents that on a host it stays empty because <b>the host
/// authors the roster and never receives one</b> — and that invariant held only because a host had
/// no key to open anything with. The moment R-1.3k gives it keys, a shared handler would let a
/// MEMBER author what the HOST believes the roster is: D-3 inverted by a capability added for an
/// unrelated reason. Two delegates make that structural — member content cannot reach the roster
/// because it is not wired to it — rather than leaving it to whoever edits the lambda next.
/// </para>
/// </remarks>
/// <param name="OpenWith">
/// The keys a <b>host</b> may open <b>member-authored</b> content with, one per admitted peer
/// (R-1.3k). Null when there is nobody to hear from, which is every joiner-only client.
/// <para>
/// <b>A function rather than a list, because the answer changes between frames.</b> Admitting or
/// removing a participant changes the candidate set, and a list captured when the handlers were
/// built would be the set as it stood at the start of the tick.
/// </para>
/// </param>
/// <param name="OnContent">
/// Called for each <b>member-authored</b> payload a host opened, with the peer whose key opened it
/// (R-1.3k, A-1.13c). Null when there is nobody to tell.
/// <para>
/// <b>The peer is the one that DECRYPTED it, never one that was claimed.</b> Nothing on the wire
/// says who sent a <see cref="WireMessageType.SessionPayload"/>: <c>WireEnvelope.ForSessionPayload</c>
/// sets only the nonce and the ciphertext, and the relay forwards it unmodified. Identity is
/// therefore established by the seal — the key that opened it is shared with exactly one peer — and
/// a sender field would have been a forgeable hint that still had to be confirmed this way.
/// </para>
/// </param>
public readonly record struct MemberAuthoredContent(
    Func<IEnumerable<PeerContentKey>>? OpenWith = null,
    Action<PeerCode, SessionContent>? OnContent = null);
