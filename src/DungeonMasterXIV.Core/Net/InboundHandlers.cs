using System;
using System.Collections.Generic;

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
/// They travel together because they answer one question: <i>this frame arrived, now what?</i>
/// <see cref="OpenWith"/> belongs with them rather than beside them — it is not configuration, it is
/// the thing that decides whether <see cref="OnContent"/> can be called at all.
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
/// <see cref="OpenWith"/>/<see cref="OnContent"/> carry <b>host-authored</b> content to a joiner.
/// <see cref="OpenMemberContentWith"/>/<see cref="OnMemberContent"/> carry <b>member-authored</b>
/// content to a host. Merging them would be smaller and would be wrong: see
/// <see cref="OnMemberContent"/> for what it costs.
/// </para>
/// </remarks>
/// <param name="OnJoinRequest">
/// Called with the joiner's public key, self-declared name, and the participant id it CLAIMS, for
/// each inbound <see cref="WireMessageType.JoinRequest"/>, when this client is a host. Null when
/// there is nobody to tell, which is every joiner-only client (BUG-42).
/// <para>
/// <b>The claim travels as the raw string it arrived as (R-1.5, T-37).</b> This layer decodes and
/// routes; deciding whether a claimed participant is one this campaign knows needs the campaign,
/// which is not Core's to look up — see <see cref="SessionCapabilities.RelinkSource"/>. Null means
/// no claim was made, which is every first-time join.
/// </para>
/// </param>
/// <param name="OpenWith">
/// The shared key to open inbound <b>host-authored</b> content with, or null before one exists. A
/// key derived during the same drain takes precedence — see the call site. Null on a pure host,
/// which is correct: a host authors the roster and never receives one.
/// </param>
/// <param name="OnContent">
/// Called for each <b>host-authored</b> payload this client could open (D-11). Payloads sealed for
/// somebody else are ordinary traffic and pass in silence.
/// </param>
/// <param name="OnComparabilityReceipt">
/// Called with the joiner's public key when that joiner reports it held the host key and could
/// render the fingerprint (R-1.3a-iv, BUG-75). Null when there is nobody to tell, which is every
/// joiner-only client — only a host keeps a record this can establish anything on.
/// <para>
/// <b>It carries a CAPABILITY, never a comparison.</b> R-1.3a-iii forbids the second: an
/// acknowledgement of the human act would ride the channel an attacker controls, so it is forgeable
/// exactly when it matters. Its ABSENCE establishes nothing either — a fast admission (A-1.2p)
/// decides before any receipt could arrive, which is why
/// <see cref="ComparabilityEvidence.NotEstablished"/> is a state and not a false.
/// </para>
/// </param>
/// <param name="OpenMemberContentWith">
/// The keys a <b>host</b> may open <b>member-authored</b> content with, one per admitted peer
/// (R-1.3k). Null when there is nobody to hear from, which is every joiner-only client.
/// <para>
/// <b>A function rather than a list, because the answer changes between frames.</b> Admitting or
/// removing a participant changes the candidate set, and a list captured when the handlers were
/// built would be the set as it stood at the start of the tick.
/// </para>
/// </param>
/// <param name="OnMemberContent">
/// Called for each <b>member-authored</b> payload a host opened, with the peer whose key opened it
/// (R-1.3k, A-1.13c). Null when there is nobody to tell.
/// <para>
/// <b>SEPARATE FROM <see cref="OnContent"/> ON PURPOSE, AND MERGING THEM INVERTS D-3.</b>
/// <c>SessionCoordinator.Roster</c> documents that on a host it stays empty because <b>the host
/// authors the roster and never receives one</b> — and that invariant held only because a host had
/// no key to open anything with. The moment R-1.3k gives it keys, a shared handler would let a
/// MEMBER author what the HOST believes the roster is: D-3 inverted by a capability added for an
/// unrelated reason. Two delegates make that structural — member content cannot reach the roster
/// because it is not wired to it — rather than leaving it to whoever edits the lambda next.
/// </para>
/// <para>
/// <b>The peer is the one that DECRYPTED it, never one that was claimed.</b> Nothing on the wire
/// says who sent a <see cref="WireMessageType.SessionPayload"/>: <c>WireEnvelope.ForSessionPayload</c>
/// sets only the nonce and the ciphertext, and the relay forwards it unmodified. Identity is
/// therefore established by the seal — the key that opened it is shared with exactly one peer — and
/// a sender field would have been a forgeable hint that still had to be confirmed this way.
/// </para>
/// </param>
public readonly record struct InboundHandlers(
    Action<byte[], DisplayName, string?>? OnJoinRequest = null,
    byte[]? OpenWith = null,
    Action<SessionContent>? OnContent = null,
    Action<byte[]>? OnComparabilityReceipt = null,
    Func<IEnumerable<PeerContentKey>>? OpenMemberContentWith = null,
    Action<PeerCode, SessionContent>? OnMemberContent = null);
