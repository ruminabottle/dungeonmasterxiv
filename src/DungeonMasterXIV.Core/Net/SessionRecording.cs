using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// This client's own log of what it RECEIVED, for the session it is in (R-2.12, SQ-116).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE HALF R-2.12 NEVER HAD.</b> Nine types were built, merged and unit-tested against
/// retention and export, and <b>nothing in the product ever constructed a
/// <see cref="SessionStream"/> or recorded into one</b> — so retention would have written an empty
/// file every hosted session. A unit test constructs its own subject, and an absent producer has no
/// failing test.
/// </para>
/// <para>
/// <b>EVERY CLIENT RECORDS ITS OWN LOG, AND RECORDING IS NOT AUTHORING (SQ-116).</b> The host still
/// authors the shared stream and its order; a client writes down what ARRIVED. A client that
/// invented entries the host never sent would violate D-3; one that writes down what it received
/// does not.
/// </para>
/// <para>
/// <b>IT RECORDS FROM THE INBOUND PATH, AND THAT IS A CORRECTNESS CONSTRAINT RATHER THAN A
/// STYLISTIC ONE.</b> A-2.16 — <i>an export contains only what its owner could see</i> — is
/// <b>entailed</b> rather than filtered, and the entailment holds ONLY because the log is built from
/// what THIS client received. Record from anything the host assembles and the entailment is false,
/// which resurrects the visibility filter measured as absent at SQ-115.
/// </para>
/// <para>
/// <b>RECORDING DOES NOT REQUIRE A SEQUENCER, AND THAT IS DELIBERATE (amended DMXENG-116
/// obligation 3).</b> <see cref="Record(StreamEntry)"/> takes an entry that ARRIVED already stamped;
/// <see cref="RecordAsHost"/> mints first and is a convenience for the one client that is the
/// authority on order. <b>The host owning a sequencer is a fact about the host, not a precondition
/// of writing something down</b> — so when stamps travel, admitting the member path is a wiring
/// change rather than taking this type apart.
/// </para>
/// <para>
/// <b>ONE MINTER, AND THIS TYPE IS NOT IT.</b> <see cref="HostSequencer"/> is the only thing that
/// makes a <see cref="StreamStamp"/>, because R-2.4 exists to prevent a second sequencer — two
/// minters and two clients' logs disagree on order. The frame's instant is fed to that sequencer
/// rather than a clock being captured here, so the stamp carries the moment the frame was advanced
/// at and the sequence stays this session's.
/// </para>
/// <para>
/// <b>WHAT THIS CANNOT RECORD, AND IT IS MOST OF WHAT THE FEATURE IS FOR.</b> Measured at
/// <c>cb334c9</c>: <see cref="SessionContent"/> carries a roster, a closing instant and a leaving
/// flag — <b>no message, no roll, and no stamp.</b> So a NON-HOST client cannot record at all: it
/// has no host-minted stamp and cannot mint one, and <see cref="SessionStream.Record"/> refuses an
/// unminted stamp by construction (BUG-161). <b>That is the wire's gap, not this type's, and it has
/// its own ticket — an absence on a board survives, and a comment in a file nobody opens does
/// not.</b>
/// </para>
/// </remarks>
internal sealed class SessionRecording
{
    private SessionStream _stream = new();
    private HostSequencer _sequencer;
    private DateTimeOffset _at;

    /// <summary>Starts an empty log with its own sequence.</summary>
    public SessionRecording() => _sequencer = NewSequencer();

    /// <summary>What this client has recorded, in the host's order.</summary>
    public IReadOnlyList<StreamEntry> Entries => _stream.Entries;

    /// <summary>
    /// Writes down one thing this client received.
    /// </summary>
    /// <remarks>
    /// <b>The instant is a PARAMETER because the frame already carries one.</b>
    /// <c>InboundWiring.For</c> is handed the moment the frame is being advanced at, so capturing a
    /// separate clock here would invent a second reading of the same event — and the two would
    /// disagree under a test clock, which is where this is actually exercised.
    /// </remarks>
    /// <param name="kind">What happened.</param>
    /// <param name="peer">Whom it happened to, established by the key the payload opened under.</param>
    /// <param name="text">What was said, empty for events that say nothing.</param>
    /// <param name="at">The instant the frame carrying it was advanced at.</param>
    /// <returns>Whether the log changed — false for a duplicate or an unminted stamp.</returns>
    public bool RecordAsHost(StreamEventKind kind, PeerCode peer, string text, DateTimeOffset at) =>
        StampAsHost(kind, peer, text, at) is not null;

    /// <summary>
    /// Mints a stamp, records the entry, and hands it back so it can be sent on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE REBROADCAST NEEDS THE STAMPED ENTRY, WHICH <see cref="RecordAsHost"/> CANNOT RETURN.</b>
    /// A host that receives a member's message stamps it and sends it to everybody (R-2.19, A-2.34),
    /// so the caller needs the sequence and instant that were just minted. <b>Reading them back out
    /// of <see cref="Entries"/> would work today and is the coupling worth refusing</b>: it makes the
    /// last element of a shared list mean "the one I just added", which stops being true the moment
    /// anything else records between the two calls.
    /// </para>
    /// <para>
    /// <b><see cref="RecordAsHost"/> now delegates here rather than repeating the minting.</b> Its
    /// bool contract is unchanged and it stays the name for callers that only record — a membership
    /// change is not sent on, so making them all handle an entry would be noise.
    /// </para>
    /// </remarks>
    /// <param name="kind">What happened.</param>
    /// <param name="peer">Whom it happened to, established by the key the payload opened under.</param>
    /// <param name="text">What was said, empty for events that say nothing.</param>
    /// <param name="at">The instant the frame carrying it was advanced at.</param>
    /// <returns>The stamped entry, or null when the log did not change.</returns>
    public StreamEntry? StampAsHost(StreamEventKind kind, PeerCode peer, string text, DateTimeOffset at)
    {
        _at = at;
        var entry = new StreamEntry(_sequencer.Next(), kind, peer, text);
        return Record(entry) ? entry : null;
    }

    /// <summary>
    /// Writes down an entry that ARRIVED already stamped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THIS IS THE PRIMITIVE, AND MINTING IS NOT PART OF IT.</b> Recording requires no sequencer:
    /// the host owning one is a fact about the HOST, not a precondition of writing something down.
    /// <see cref="RecordAsHost"/> is the convenience for the one client that is the authority on
    /// order (R-2.4), layered on top rather than built in.
    /// </para>
    /// <para>
    /// <b>SO THE MEMBER PATH IS A WIRING CHANGE, NOT A REDESIGN.</b> When stamps travel — DMXENG-118
    /// — a non-host client decodes an already-stamped entry and calls THIS. No new type, no new
    /// method, and nothing here to unpick. <b>A recorder that could only mint would have had to be
    /// taken apart to admit the member, which is the host-only assumption this shape refuses.</b>
    /// </para>
    /// </remarks>
    /// <param name="entry">An entry stamped by the host, decoded from the wire or minted here.</param>
    /// <returns>Whether the log changed — false for a duplicate or an unminted stamp.</returns>
    public bool Record(StreamEntry entry) => _stream.Record(entry);

    /// <summary>
    /// Drops the log when the session ends. <b>A player's log dies with the session unless it was
    /// exported</b> (R-2.12), and this is where it dies.
    /// </summary>
    /// <remarks>
    /// <b>A NEW STREAM AND A NEW SEQUENCE, NOT A CLEARED ONE.</b> The next session's first entry
    /// must be sequence 1: carrying a counter across would number a fresh log from where the last
    /// one stopped, and an export would then claim an order it never had.
    /// </remarks>
    public void Release()
    {
        _stream = new SessionStream();
        _sequencer = NewSequencer();
    }

    private HostSequencer NewSequencer() => new(() => _at);
}
