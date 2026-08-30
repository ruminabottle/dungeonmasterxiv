namespace DungeonMasterXIV.Net;

/// <summary>
/// One stamped entry, in the shape that crosses the wire (R-2.12).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE WIRE FORM OF <see cref="StreamEntry"/>, AND IT EXISTS BECAUSE THAT TYPE CANNOT TRAVEL.</b>
/// <c>StreamEntry</c> carries <see cref="PeerCode"/> and <see cref="StreamStamp"/> as domain types.
/// <b>Measured rather than assumed:</b> <c>PeerCode</c> is a readonly struct whose only members are
/// computed and get-only, so <c>System.Text.Json</c> serialises it as
/// <c>{"Value":"BCDFGH","IsPresent":true}</c> and deserialises it to <c>default</c> — <i>absent</i>,
/// and equal to every other absent code (DMXENG-105). <b>It looks correct leaving and arrives as the
/// collision.</b>
/// </para>
/// <para>
/// <b>Raw primitives here are the ruling, not a shortcut.</b> #86 settled it for
/// <see cref="RosterEntry"/>: put the gate at the DECODE BOUNDARY so it is the only door, and
/// <c>string</c> stays in the DTO — the wire format does not change. A DTO is not a door; it is the
/// shape of what crossed one.
/// </para>
/// <para>
/// <b>The stamp is split into its two numbers rather than nested.</b> <see cref="StreamStamp"/> is a
/// <c>readonly record struct</c> of two <c>long</c>s and would survive a round trip today, but
/// nesting it would put a domain type on the wire whose shape then cannot change without a wire
/// change — the same trap <c>PeerCode</c> fell into, one level up.
/// </para>
/// </remarks>
/// <param name="Sequence">The host's order for this entry. Host-minted; see <see cref="TryToEntry"/>.</param>
/// <param name="AtUtcTicks">The host's clock at the moment it stamped (A-2.5).</param>
/// <param name="Kind">What kind of event this was.</param>
/// <param name="Peer">Who it came from, as the session-scoped code.</param>
/// <param name="Text">What was said. Untrusted, and see the remarks on <see cref="TryToEntry"/>.</param>
public readonly record struct StreamLine(
    long Sequence,
    long AtUtcTicks,
    StreamEventKind Kind,
    string Peer,
    string Text)
{
    /// <summary>
    /// Turns this into the domain <see cref="StreamEntry"/>, reporting whether it was usable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THIS IS THE DOOR, AND IT IS THE ONLY ONE.</b> <c>SessionContentCodec.Vetted</c> uses this
    /// same method as its predicate, so a line that cannot become an entry is dropped at decode and
    /// never reaches a consumer. One expression, decided once — a second copy of these rules
    /// somewhere else is how <c>PeerCode</c> got through the roster gate (BUG-57).
    /// </para>
    /// <para>
    /// <b>An unparseable peer code DROPS the line rather than degrading it</b>, exactly as it does
    /// for a roster entry: the code is the IDENTITY, so an entry whose code is unusable attributes
    /// content to nobody, and keeping it would manufacture a speaker rather than remove a forgery.
    /// </para>
    /// <para>
    /// <b>AND A SEQUENCE BELOW 1 IS REFUSED, BECAUSE THE HOST IS THE SOLE MINTER (R-2.4).</b>
    /// <see cref="HostSequencer"/> issues from 1, so anything lower was not minted by a host —
    /// and 0 sorts to the FRONT of a populated log, which is the ordering hazard BUG-161 was raised
    /// for. <b>This is the door; <c>SessionStream.Record</c>'s identical check is the backstop it
    /// says it is</b>, and the two are deliberate rather than duplicated: this one refuses at the
    /// boundary where the value arrives from another client, that one refuses what the type system
    /// cannot.
    /// </para>
    /// <para>
    /// <b><see cref="Text"/> IS NOT VETTED, AND THAT IS A FINDING RATHER THAN AN OMISSION</b> — an
    /// unmentioned field is precisely what let a peer code through the roster gate. It is content a
    /// person typed, so there is no shape to hold it to: refusing newlines would refuse legitimate
    /// messages, and truncating would silently alter what somebody said. <b>What it therefore
    /// remains is untrusted text that a renderer must not draw beside anything a forged line could
    /// displace</b> — a rendering obligation, named here so it is inherited rather than rediscovered.
    /// </para>
    /// </remarks>
    /// <param name="entry">The decoded entry, or <c>default</c> when this returns false.</param>
    public bool TryToEntry(out StreamEntry entry)
    {
        entry = default!;

        if (Sequence < 1 || !PeerCode.TryParse(Peer, out var peer))
        {
            return false;
        }

        entry = new StreamEntry(new StreamStamp(Sequence, AtUtcTicks), Kind, peer, Text ?? string.Empty);
        return true;
    }
}
