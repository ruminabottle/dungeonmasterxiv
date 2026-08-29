using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The things <see cref="SessionCoordinator"/> cannot do for itself, supplied from outside Core.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type exists so that adding a capability stops costing a constructor parameter.</b>
/// <see cref="SessionCoordinator"/>'s constructor was at six against a block of six
/// (<c>engineering-standards.md</c>: <c>| Parameters | 4 | 6 |</c>), so the next chunk that needed
/// one more thing from outside could not be cut at all — two were stopped on it at once, DMXENG-33
/// for the host's display name and DMXENG-8 for a campaign it could resolve a relink against.
/// <b>Growing this record costs the constructor nothing.</b> That is the whole of the fix.
/// </para>
/// <para>
/// <b>What belongs here is a CAPABILITY: something Core calls to have done what it cannot do.</b>
/// Making a key pair is one because the platform owns the entropy path (BUG-61); minting a
/// participant is one because Core has no campaign store. What does NOT belong is configuration —
/// <c>relayAddress</c> and <c>window</c> are values read from settings, not things Core asks
/// anybody to do, and folding them in here would make this "arguments that were in the way".
/// </para>
/// <para>
/// <b>The log was considered for this record and deliberately left out, which is worth knowing
/// because it is the obvious way to reach four.</b> Moving it would take the constructor from five
/// to four — under the flag rather than merely under the block — and the reason not to is in its
/// type name: <see cref="ISessionTransportLog"/> is the TRANSPORT's log, a sibling of
/// <see cref="ISessionTransport"/>, and the two are passed together for the same reason. Grouping it
/// with keys and campaigns would have been a grouping chosen for its arithmetic. <b>Five is over the
/// flag and stated, which is what the flag is for; six was the block, which is what could not
/// stand.</b>
/// </para>
/// <para>
/// <b>A named and UNBUILT option, so the next person does not have to rediscover it:</b> the two
/// remaining settings-sourced arguments, <c>relayAddress</c> and <c>window</c>, are one concept and
/// could become a second record, taking the constructor to four honestly. Not built here, because
/// this ticket is a move rather than a redesign and two parameter objects at once is a redesign.
/// </para>
/// <para>
/// <b>NOT named for the plugin, though the plugin is what supplies it today.</b> A test supplies
/// these too, and naming a Core type after one of its callers is how a layer starts depending on the
/// thing above it in comments before it does in code.
/// </para>
/// </remarks>
/// <param name="NewKeys">
/// How a session key pair is made. A seam so that a failure to make one can be driven from a test
/// (BUG-61): on the machine that reported it, this throws, and there was no seam between that throw
/// and the frame loop. <b>Null takes the platform default</b> rather than disabling anything.
/// </param>
/// <param name="MintParticipant">
/// Creates a participant for a joiner about to be admitted (R-1.5c). Null when not hosting into a
/// campaign. <b>Optional deliberately; the reasoning lives on <see cref="AdmissionControl"/>'s
/// parameter of the same name, where it is consumed.</b>
/// </param>
public sealed record SessionCapabilities(
    Func<SessionKeyExchange>? NewKeys = null,
    Func<DisplayName, Guid?>? MintParticipant = null)
{
    /// <summary>
    /// What a caller that supplies nothing gets: platform key generation, and no campaign.
    /// </summary>
    /// <remarks>
    /// <b>A default for the RECORD, never for the PARAMETER.</b> DMXENG-13's ruling is that an
    /// optional dependency production happens to supply is one refactor away from production not
    /// supplying it — so <see cref="SessionCoordinator"/> takes this record as a REQUIRED argument
    /// and a caller that wants the defaults must say <c>SessionCapabilities.Default</c> out loud.
    /// Defaulting the parameter itself would move exactly the guarantee DMXENG-13 bought back to
    /// where it was.
    /// </remarks>
    public static SessionCapabilities Default { get; } = new();

    /// <summary>How a key pair is made, with the platform default filled in.</summary>
    /// <remarks>
    /// Resolved HERE rather than at each use, so <c>?? new SessionKeyExchange()</c> exists in one
    /// place. Two call sites each applying their own fallback is how two paths come to disagree
    /// about what "no key source" means.
    /// </remarks>
    public Func<SessionKeyExchange> KeySource => NewKeys ?? (static () => new SessionKeyExchange());

    /// <summary>Minting, with the no-campaign case filled in.</summary>
    public Func<DisplayName, Guid?> ParticipantSource => MintParticipant ?? (static _ => null);
}
