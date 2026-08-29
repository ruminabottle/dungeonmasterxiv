using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What this client knows about ITSELF as the host of a session — read at use time, never captured.
/// </summary>
/// <remarks>
/// <para>
/// <b>The host was structurally absent from its own roster, and this is the missing half.</b>
/// <see cref="RosterBroadcast"/> builds the roster from <see cref="SessionAudience.Recipients"/>,
/// and the host is deliberately not on that list — <i>"so nothing can be addressed to it"</i>. That
/// is correct for a SEND LIST and wrong for a MEMBERSHIP LIST, and the two had been the same
/// expression. This carries what the host needs to author its own entry (DMXENG-33, A-1.13b).
/// </para>
/// <para>
/// <b>Every member is a function, and that is the same decision the two it replaces already made.</b>
/// <c>hostKeys</c> and <c>hostCode</c> were already <c>Func</c>s so they are read at send time
/// rather than captured at construction — a session's keys and code change when a session ends and
/// another begins, and a captured value is stale exactly then. The name is a function for the
/// stronger version of that reason: it changes when the player switches character, which
/// <c>SessionCoordinator.RequestJoin</c>'s own doc gives as why the joining name is a
/// <c>Func&lt;DisplayName&gt;</c> too.
/// </para>
/// <para>
/// <b>WHY THIS REPLACES TWO PARAMETERS RATHER THAN ADDING TWO.</b> <see cref="RosterBroadcast"/>'s
/// constructor was at FIVE against a flag of four and a block of six. The host's name and its own
/// peer code are two more things, which would have been SEVEN — a breach — and even one would have
/// left it AT the block, which is the wall DMXENG-57 had just finished removing one level up. Four
/// members in one argument takes it to FOUR, which is AT the flag and clear of the block — and
/// under the ruling at <c>engineering-standards.md:13185</c>, four FLAGS rather than passing
/// silently, so this paragraph is the reason a flagged unit is required to carry. <b>It rides with this chunk rather
/// than being its own ticket because it is a precondition of exactly one chunk</b>, which is the
/// distinction DMXENG-57 established: a precondition of two or more independent chunks is shared
/// infrastructure and gets filed; a precondition of one ride with it.
/// </para>
/// </remarks>
/// <param name="Keys">The host's ephemeral session keys, or null when not hosting.</param>
/// <param name="Code">The session being hosted, or null when not hosting.</param>
/// <param name="Name">
/// What the host calls itself (R-1.3e). <b>Core cannot see the game</b>, so this arrives from the
/// plugin layer exactly as the joining name does; the two are the same setting read through the
/// same method, so the DM cannot appear under one name to a joiner and another to itself.
/// </param>
/// <param name="OwnPeerCode">
/// The peer code a joiner would compute for this host, or null when not hosting.
/// <para>
/// <b>DERIVED BY THE ONE DERIVATION, NEVER RECOMPUTED HERE.</b> This is a call into
/// <c>AdmissionControl.PeerCodeFor</c>, which is still the only site that turns a public key into a
/// peer code. Three alternatives were considered and refused on properties rather than convenience:
/// duplicating the SHA-256 derivation (two derivations of one identity, and identity is the worst
/// place for a second copy); a constant well-formed code (a second identity scheme, and it can
/// collide with a real participant); and leaving the field empty or invented, which
/// <c>SessionContentCodec</c> DROPS on the joiner's side — reintroducing the very absence this
/// exists to fix, though loudly rather than silently, since BUG-70 made that drop warn.
/// </para>
/// </param>
internal sealed record HostIdentity(
    Func<SessionKeyExchange?> Keys,
    Func<SessionCode?> Code,
    Func<DisplayName> Name,
    Func<PeerCode?> OwnPeerCode)
{
    /// <summary>
    /// Composes what a hosting <see cref="SessionCoordinator"/> is, from the pieces it holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Here rather than on the coordinator, and the reason is a measurement.</b> As a private
    /// method there it cost that class twenty lines against a block it then breached by four once
    /// another chunk landed. Composing a <see cref="HostIdentity"/> is this type's own business, so
    /// this is a move to where the knowledge already lives rather than room bought by deleting
    /// explanation.
    /// </para>
    /// <para>
    /// <b>Every member stays deferred, which is what makes this safe to call from a constructor.</b>
    /// Nothing here reads a key, a code, a name or the admission desk — it captures four functions
    /// and returns. DMXENG-45's ordering hazard does not reach it, and that is a property of the
    /// <c>Func</c>s rather than of where the call happens to sit.
    /// </para>
    /// <para>
    /// <b><paramref name="peerCodeFor"/> is THE one derivation, passed rather than repeated.</b> It
    /// is <c>AdmissionControl.PeerCodeFor</c>, still the only site that turns a public key into a
    /// peer code; this hands it the host's OWN key. Null when not hosting, which is what keeps a DM
    /// entry out of a roster that has no host.
    /// </para>
    /// </remarks>
    /// <param name="keys">The host's ephemeral session keys, read at use time.</param>
    /// <param name="code">The session being hosted, read at use time.</param>
    /// <param name="name">What the host calls itself.</param>
    /// <param name="peerCodeFor">The one derivation from a public key to a peer code.</param>
    public static HostIdentity ForHost(
        Func<SessionKeyExchange?> keys,
        Func<SessionCode?> code,
        Func<DisplayName> name,
        Func<byte[], PeerCode> peerCodeFor) =>
        new(keys, code, name, () => keys() is { } hostKeys ? peerCodeFor(hostKeys.PublicKey) : null);
}
