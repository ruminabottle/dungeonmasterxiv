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
