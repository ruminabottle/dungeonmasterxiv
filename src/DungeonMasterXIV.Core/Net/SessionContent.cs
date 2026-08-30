using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What a session says to itself, inside the seal (D-11).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the first content this product has ever sent.</b> Until now
/// <see cref="SessionCipher"/> and <see cref="WireEnvelope.ForSessionPayload"/> existed with no
/// production caller: the relay routed a payload nobody sent. Everything here therefore establishes
/// a shape the rest of the session layer will follow, which is why it is a document with a growable
/// schema rather than a single field.
/// </para>
/// <para>
/// <b>It is sealed, and that is not a preference.</b> The published service policy says
/// <i>"everything you say inside a session is sealed and the relay cannot open it. The joining name
/// is the exception, it is the only one."</i> A roster travelling in the clear would make shipped
/// user-facing copy false — a D-8 false-copy risk, not a hardening choice. The joining name is
/// unsealed because at that moment no keys have been exchanged; here they have, so the exception
/// does not extend.
/// </para>
/// <para>
/// <b>Every field is optional, for D-14's reason.</b> The wire only grows: a peer that does not
/// understand a section ignores it, and a peer that has not heard of this document at all fails to
/// open nothing, because it never receives one it can decrypt. Adding a section must never require
/// both ends to ship together.
/// </para>
/// </remarks>
public sealed class SessionContent
{
    /// <summary>
    /// Who is in the session, as the host sees it (R-1.3f).
    /// </summary>
    /// <remarks>
    /// <b>The host authors this and a player renders it (D-3).</b> A client never originates a
    /// roster and never merges one into its own view — it replaces, so a participant who left is
    /// gone rather than lingering because no removal message arrived. That is also what makes
    /// A-1.13a work: a reconnecting client is sent the current roster and rebuilds from it, rather
    /// than starting empty and waiting for changes it missed.
    /// </remarks>
    public IReadOnlyList<RosterEntry>? Roster { get; init; }

    /// <summary>
    /// When the DM has ended this session and it stops, as UTC ticks — null while it is running
    /// (R-1.3g, A-1.16).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The host announces its own departure; a player's client never sets this.</b> R-1.3g's
    /// asymmetry is deliberate rather than an oversight to tidy up: a departed player costs the
    /// group nothing because the session and its record live on the DM's machine, whereas a departed
    /// DM costs them everything. So this section exists on the host's side of the wire only.
    /// </para>
    /// <para>
    /// <b>An instant, never a duration, and the ticks are the wire form of
    /// <see cref="SessionClosing"/>.</b> R-1.3g requires participants to see the session is closing
    /// AND how long remains — a duration would be stale before it arrived, which is the
    /// indefinite-wait failure the requirement names. Read it back through
    /// <see cref="SessionClosing.TryFromWire"/> rather than constructing a
    /// <c>DateTimeOffset</c> here: the value comes from another client and is rendered in a draw
    /// path, so an out-of-range number is a crash rather than a bad countdown.
    /// </para>
    /// <para>
    /// <b>Optional, like every field here (D-14).</b> A build that has not heard of a closing notice
    /// ignores the section and behaves exactly as it does today — which is what lets this ship
    /// without both ends releasing together.
    /// </para>
    /// <para>
    /// <b>WHAT R-1.3g's OTHER HALVES DO, so nobody reads the wrong thing as a gap.</b> R-1.3g has
    /// three parts and they fail separately:
    /// </para>
    /// <para>
    /// <b>1. This notice — the DM's outward announcement with time remaining. BUILT.</b>
    /// </para>
    /// <para>
    /// <b>2. Removal when a player DELIBERATELY QUITS (A-1.15, A-1.16a). NOT BUILT, and the reason is
    /// a capability rather than an omission here:</b> a host cannot READ member-authored content at
    /// all. <c>InboundHandlers.OpenWith</c> is a single key and a host holds one per admitted peer,
    /// so a departure notice would be forwarded by the relay and dropped unopened. That capability is
    /// R-1.3k / A-1.13c and it is <b>DMXENG-50</b>; A-1.15 and A-1.16a wait on it. Adding a departure
    /// section here before then would put a message on the wire that nothing can receive.
    /// </para>
    /// <para>
    /// <b>3. A member that VANISHES — a crash or a dropped link — IS NOT REMOVED, AND THAT IS
    /// CORRECT.</b> Not a gap, not a deferral: <b>R-1.5a holds that seat for the reconnect window</b>,
    /// and a build that removed vanished members would BREAK it. D-8's SQ-20 amendment is explicit
    /// that a DELIBERATE QUIT removes immediately and an ungraceful drop does not — the two are
    /// different events with different answers, and A-1.30 exists to keep them apart.
    /// <b>If you are here to "finish" R-1.3g by removing members who went quiet, stop: that is the
    /// defect, not the fix.</b>
    /// </para>
    /// </remarks>
    public long? ClosingAtUtcTicks { get; init; }

    /// <summary>
    /// Set by a MEMBER to say it is leaving deliberately (R-1.3g, A-1.16a). Null on every other
    /// document, which is all of them today.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE ONLY MEMBER-AUTHORED FIELD IN THIS TYPE, AND THE DIRECTION IS THE WHOLE POINT.</b>
    /// <see cref="Roster"/> and <see cref="ClosingAtUtcTicks"/> travel host to member; this travels
    /// member to host. D-3 forbids a player client originating shared state, and this does not: it
    /// asserts nothing about the session, only about the sender's own intent. <b>The host decides
    /// what follows</b> — it removes the sender and republishes, and a member cannot remove anybody
    /// else because the peer code comes from the KEY the payload opened under, never from the
    /// payload.
    /// </para>
    /// <para>
    /// <b>A QUIT IS NOT A VANISH, and that distinction is A-1.30.</b> This field is a deliberate
    /// departure and the host removes the seat AT ONCE (R-1.5a). A member that merely stops
    /// answering produces no document at all — the relay reports it and the host RECORDS the drop
    /// while HOLDING the seat (A-1.28, <see cref="MemberDrops"/>). <b>Two different inbound paths,
    /// two different outcomes</b>, and nothing here may be reached by a silence.
    /// </para>
    /// <para>
    /// <b>Nullable so absence is the ordinary case and older peers decode unchanged (D-14).</b> A
    /// <c>bool</c> would serialise <c>false</c> onto every roster broadcast the host ever sends.
    /// </para>
    /// </remarks>
    public bool? Leaving { get; init; }

    /// <summary>
    /// Stamped content the host has broadcast, in the host's order — the only way anything other
    /// than membership and liveness reaches a client's log (R-2.12, SQ-116).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE HOST STAMPS AND EVERY CLIENT RECORDS WHAT IT RECEIVED, INCLUDING THE ORIGINATOR.</b>
    /// A member sends content, the host stamps and rebroadcasts, and the sender records when the
    /// stamped rebroadcast arrives — not when it sent. A client that logged its own send locally
    /// would build its log from something other than what it received, which is exactly what
    /// A-2.16's owner-scoping is entailed by, and it would reorder that one client's log against
    /// everybody else's.
    /// </para>
    /// <para>
    /// <b>WHY THIS IS NOT <see cref="StreamEntry"/>, WHICH IS THE OBVIOUS THING TO PUT HERE.</b>
    /// That type carries <see cref="PeerCode"/> and <see cref="StreamStamp"/> as domain types, and
    /// <c>PeerCode</c> CANNOT SURVIVE THIS WIRE. Measured, not reasoned: it is a readonly struct
    /// whose only members are computed and get-only, so <c>System.Text.Json</c> writes
    /// <c>{"Value":"BCDFGH","IsPresent":true}</c> and reads back <c>default</c> — absent, and equal
    /// to every other absent code (DMXENG-105). A round trip through <c>StreamEntry</c> would look
    /// correct on the way out and arrive as the collision.
    /// </para>
    /// <para>
    /// <b>So this follows <see cref="RosterEntry"/>'s ruling rather than inventing one</b>: raw
    /// primitives on the wire, and the gate at the decode boundary. #86 settled that for
    /// <c>DisplayName</c> and it governs here identically.
    /// </para>
    /// <para>
    /// <b>Optional like every section (D-14).</b> A build that has not heard of stamped content
    /// ignores it and behaves as it does today.
    /// </para>
    /// </remarks>
    public IReadOnlyList<StreamLine>? Entries { get; init; }
}

/// <summary>
/// One participant, as the roster carries them.
/// </summary>
/// <remarks>
/// <b>The name is a label and the code is the identity</b>, exactly as in the admission prompt:
/// names are self-declared and two participants may hold the same one (A-1.2d), so nothing may key
/// on <see cref="DisplayName"/>. <see cref="PeerCode"/> is session-scoped and derived, so it
/// identifies within this session and links nothing across two (A-1.2a).
/// <para>
/// <b>Both are raw strings here on purpose, and that is not a door left open.</b> This was ruled on
/// for <see cref="DisplayName"/> in #86 and the ruling governs <see cref="PeerCode"/> identically:
/// <i>put the gate at the DECODE BOUNDARY so it is the only door, and <c>string</c> stays in
/// <c>RosterEntry</c> — the wire format does not change.</i>
/// </para>
/// <para>
/// <b>A DTO is not a door; it is the shape of what crossed one.</b> The door is <c>Vetted</c>,
/// called inside <c>TryDecode</c> before it returns true, so no path yields a decoded
/// <see cref="SessionContent"/> without passing it and a later reader cannot forget. The validated
/// types live on the domain side, in <see cref="AdmittedPeer"/> and <see cref="PendingAdmission"/>.
/// </para>
/// <para>
/// D-14 reaches the same conclusion independently: System.Text.Json reads and writes these fields,
/// so changing one to a struct changes the serialised shape — a wire change wearing a refactor's
/// clothes.
/// </para>
/// </remarks>
/// <param name="PeerCode">The participant's session-scoped code.</param>
/// <param name="DisplayName">What they call themselves. Shown, never acted on.</param>
/// <param name="Role">What they may do (E-11).</param>
public readonly record struct RosterEntry(string PeerCode, string DisplayName, SessionRole Role);
