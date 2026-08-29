using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>
/// One piece of member-authored content the host opened, and who it was from.
/// </summary>
/// <param name="Peer">
/// The participant whose key opened it. <b>Knowing this at all is the proof of decryption</b> —
/// nothing on the wire names a sender, so a client that merely RECEIVED the frame could not fill
/// this in.
/// </param>
/// <param name="Order">
/// Where this sat in <b>host receipt order</b>, counting from one. The host is the only party that
/// can establish this ordering, which is why A-2.5 puts the roll log in it rather than in any order
/// a member could claim.
/// </param>
/// <param name="Content">What they sent.</param>
public readonly record struct MemberContentReceipt(PeerCode Peer, int Order, SessionContent Content);

/// <summary>
/// What the host has heard from its members (R-1.3k, A-1.13c).
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS EXISTS BECAUSE A-1.13c IS WRITTEN OVER THE ACTION AND NOT THE ARRIVAL.</b> The criterion
/// fails a build in which member content is "dropped unopened", and a test that asserts a frame
/// arrived would have been green against every build this product has ever had — the relay routed
/// it correctly the whole time and the host discarded it. So the host needs state that <b>cannot
/// change unless a payload was actually opened</b>, and <see cref="MemberContentReceipt.Peer"/> is
/// exactly that: the sender is established by which key decrypted the payload, so an unopened
/// payload cannot produce a receipt naming anybody.
/// </para>
/// <para>
/// <b>IT IS NOT THE ROLL LOG A-2.5 SPECIFIES AND MUST NOT BE MISTAKEN FOR ONE.</b> That log needs
/// every event a member ever sends, in order, and belongs to PRD-2. This keeps <b>the most recent
/// receipt per peer</b> so it stays bounded by the size of the session rather than by how long the
/// session has been running — an unbounded list fed from the network is a defect, not a feature.
/// The <see cref="MemberContentReceipt.Order"/> counter is the piece PRD-2 will want, and it counts
/// every receipt rather than every entry kept.
/// </para>
/// <para>
/// <b>NO PRODUCTION CODE SENDS MEMBER-AUTHORED CONTENT YET, SO THIS IS EMPTY IN THE SHIPPED
/// PRODUCT.</b> <see cref="WireEnvelope.ForSessionPayload"/> has one production caller,
/// <c>RosterBroadcast</c>, which is the host. <b>The sending half is DMXENG-11 / A-1.15</b>, a live
/// ticket held by another engineer and blocked on this one. The capability is real and reachable —
/// <c>RelayRouter.ForwardPayload</c> already routes a member's payload to the other members — but
/// <b>a model with no production caller is not a shipped behaviour</b>, and a reader who takes this
/// for one has been misled.
/// </para>
/// </remarks>
public sealed class MemberContentReceipts
{
    private readonly Dictionary<string, MemberContentReceipt> _latest = new(StringComparer.Ordinal);
    private int _received;

    /// <summary>How many pieces of member content this host has opened since the session began.</summary>
    /// <remarks>
    /// Counts <b>receipts</b>, not entries kept. Two payloads from one peer are two receipts and one
    /// entry, and a count that quietly meant the second thing would understate what the host heard.
    /// </remarks>
    public int Received => _received;

    /// <summary>
    /// The most recent thing each peer sent, in host receipt order — oldest first.
    /// </summary>
    public IReadOnlyList<MemberContentReceipt> Latest =>
        _latest.Values.OrderBy(receipt => receipt.Order).ToList();

    /// <summary>Records content the host opened, from the peer whose key opened it.</summary>
    /// <remarks>
    /// <b>Internal while the reads are public, deliberately.</b> This type is handed out so callers
    /// can SEE what the host heard; a public writer would let anything in the plugin manufacture a
    /// receipt naming a participant, which is the one claim the whole mechanism exists to make
    /// unforgeable.
    /// <para>
    /// The order is assigned <b>here</b>, at the moment the host reads it, rather than being taken
    /// from anything a member sent. A-2.5 orders the log by host receipt order precisely so that no
    /// member can place its own event ahead of another's by claiming a timestamp.
    /// </para>
    /// </remarks>
    internal void Record(PeerCode peer, SessionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        _received++;
        _latest[peer.Value] = new MemberContentReceipt(peer, _received, content);
    }

    /// <summary>Empties the record, for the end of a session.</summary>
    /// <remarks>
    /// The counter resets with it: <see cref="Received"/> is "since the session began", and a count
    /// that survived into the next session would be describing a session nobody is in.
    /// </remarks>
    internal void Clear()
    {
        _latest.Clear();
        _received = 0;
    }
}
