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
/// <b>FOUR MEMBERS IS AT THE PARAMETER FLAG, AND THE REASON IS REQUIRED RATHER THAN OPTIONAL.</b>
/// The standing ruling is that a façade between flag (4) and block (6) is compliant <i>with the
/// reason stated</i>, so here it is: <b>these are not four accumulated favours, they are one
/// concept enumerated.</b> Each is a thing Core is asked to have done because it cannot do it —
/// make a key pair, name the host, mint a participant, resolve a claimed one. Nothing here is a
/// value Core reads; every member is something Core CALLS. A fifth that fits that sentence belongs
/// here; a fifth that has to be argued into it does not, and is the signal that this record has
/// stopped being a concept and started being a parameter list with a name.
/// </para>
/// <para>
/// <b>They arrived separately and that is not an argument for merging any of them.</b>
/// <c>HostDisplayName</c> (DMXENG-33) and <c>ResolveRelink</c> (DMXENG-8) landed within an hour of
/// each other and collided textually on this parameter list. <b>Adjacency is not kinship</b> —
/// folding two members together because they arrived next to each other is a grouping chosen for
/// its arithmetic, which is what DMXENG-57 refused when it declined to move the log in here to
/// reach four.
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
/// <param name="HostDisplayName">
/// What the host calls itself, for its own roster entry (R-1.3e, A-1.13b). <b>Core cannot see the
/// game</b>, so the name arrives from outside exactly as the joining name does — and it is a
/// function rather than a value because it changes when the player switches character, which is the
/// reason <c>SessionCoordinator.RequestJoin</c> gives for taking the joining name the same way.
/// <b>Null is the honest default</b>: a client with no name supplied publishes the same unstated
/// label a joiner would, rather than a blank beside a code somebody is comparing.
/// </param>
/// <param name="MintParticipant">
/// Creates a participant for a joiner about to be admitted (R-1.5c). Null when not hosting into a
/// campaign. <b>Optional deliberately; the reasoning lives on <see cref="AdmissionControl"/>'s
/// parameter of the same name, where it is consumed.</b>
/// </param>
/// <param name="ResolveRelink">
/// Turns the participant id a joining client claims into what this host knows about it (R-1.5,
/// T-37), or null to resolve nothing. See <see cref="RelinkSource"/> for why the default is correct
/// for a joiner and reported for a host.
/// </param>
public sealed record SessionCapabilities(
    Func<SessionKeyExchange>? NewKeys = null,
    Func<DisplayName>? HostDisplayName = null,
    Func<DisplayName, Guid?>? MintParticipant = null,
    Func<string?, RelinkClaim>? ResolveRelink = null)
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

    /// <summary>
    /// Turns the participant id a joining client CLAIMS into what this host actually knows about it
    /// (R-1.5), or <see cref="RelinkClaim.None"/> when nothing is wired.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A CLAIM IS NOT A CREDENTIAL, and the return type is what keeps that true.</b> The joiner
    /// sends unauthenticated text; this returns what the host RESOLVED it to, and the DM still
    /// approves every relink, every session (R-1.5, D-8). Nothing is granted on the strength of the
    /// claim — its entire effect is to change what the DM's prompt SAYS.
    /// </para>
    /// <para>
    /// <b>The default is today's behaviour exactly:</b> every request resolves to
    /// <see cref="RelinkClaim.None"/> and every relink branch takes the not-a-relink path. That is
    /// correct for a JOINING client, which hosts nothing and resolves nobody.
    /// </para>
    /// <para>
    /// <b>A HOST wired without one is a real loss, and it is ALREADY REPORTED</b> — not silently
    /// tolerated. It has the same root cause as a host with no <see cref="MintParticipant"/>: no
    /// campaign. <see cref="AdmissionControl.Admit"/> already warns by peer code on exactly that
    /// condition, so a second warning here would fire on the same misconfiguration and teach a DM to
    /// skip both.
    /// </para>
    /// </remarks>
    public Func<string?, RelinkClaim> RelinkSource =>
        ResolveRelink ?? (static _ => RelinkClaim.None);

    /// <summary>The host's own name, with the unstated case filled in.</summary>
    /// <remarks>
    /// <c>DisplayName.None</c> renders as <c>DisplayName.Unstated</c> — never blank. An empty label
    /// beside a fingerprint reads as a rendering fault and invites the reader to look past it, which
    /// is the argument that type already makes for itself.
    /// </remarks>
    public Func<DisplayName> HostNameSource => HostDisplayName ?? (static () => DisplayName.None);
}
