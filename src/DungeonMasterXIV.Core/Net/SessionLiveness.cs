namespace DungeonMasterXIV.Net;

/// <summary>
/// What this client's two phases mean: whether a session is live, and whether one needs the relay.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because the rule had no home, and the absence has already cost a bug.</b> Four
/// halves of one question — "which phases mean this client is in a session" — were in four places,
/// each correct on its own:
/// </para>
/// <list type="bullet">
/// <item>the host's connection rule on <see cref="HostSession.RequiresRelayConnection"/>;</item>
/// <item>the join half of the same rule, a PRIVATE method on <see cref="SessionCoordinator"/>, which
/// <c>SessionWindow</c>'s doc has to point at by name because there was nothing public to cite;</item>
/// <item>the host's liveness rule on <see cref="SessionCoordinator"/>;</item>
/// <item>its join twin on <see cref="SessionInterruption.InAJoinedSession"/>.</item>
/// </list>
/// <para>
/// BUG-115 is what that shape produces: the window's exclusivity guard was gated on the JOIN side
/// alone, so a live host was one click from starting a second session. Nobody noticed the host half
/// was missing, because there was no one place where its absence would have been visible.
/// </para>
/// <para>
/// <b>What is deliberately NOT here: <see cref="SessionInterruption.InAJoinedSession"/>.</b> It looks
/// like the fourth member of this set and it is not, because the phase cannot answer it — an
/// admitted joiner whose link dropped is still holding a seat, and four predecessors reach
/// <c>Failed</c> while only one of them holds one (BUG-53). <b>The seat clock is what expires, so
/// the seat clock is what is asked.</b> This type is the rules the PHASES can answer; that one
/// stays with the clock it depends on.
/// </para>
/// <para>
/// A struct over the two phase objects rather than a wired collaborator: it reads two enums and
/// holds nothing — no link, no keys, no log — so there is no state for it to get wrong and nothing
/// to dispose.
/// </para>
/// </remarks>
internal readonly record struct SessionLiveness(HostSession Host, JoinAttempt Join)
{
    /// <summary>
    /// Whether this client is hosting a session someone could still be in (R-1.3h, BUG-115).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Registering counts, Failed does not.</b> Registering is already a session a DM can lose,
    /// so the offer must be withdrawn before the code comes back. A FAILED attempt is the opposite
    /// case: there is nothing to protect and the DM's next action is to try again, so hiding the
    /// button there would strand them. That distinction is what separates "hosting is live" from
    /// "hosting was attempted", and it is what stops this predicate becoming always-true.
    /// </para>
    /// <para>
    /// The window asks this rather than reading <see cref="HostSession.Phase"/> itself, for the same
    /// reason it does not read the join phase: which phases count is Core's decision, and a window
    /// that enumerates them is a second place to update when one is added.
    /// </para>
    /// </remarks>
    public bool InAHostedSession => Host.Phase is HostingPhase.Registering or HostingPhase.Hosting;

    /// <summary>
    /// Whether a join in progress needs the relay connection held open.
    /// </summary>
    /// <remarks>
    /// The join-side twin of <see cref="HostSession.RequiresRelayConnection"/>. It was private on
    /// <see cref="SessionCoordinator"/>, which is why <c>SessionWindow</c>'s remarks cite it as
    /// <c>SessionCoordinator.JoinNeedsConnection</c> — a doc reference to a member the citing file
    /// cannot see. The phases are the ones during which somebody is waiting on an answer that can
    /// only arrive down the socket.
    /// </remarks>
    public bool JoinRequiresRelayConnection =>
        Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted;

    /// <summary>
    /// Whether the transport should be holding a relay connection right now, on either side.
    /// </summary>
    /// <remarks>
    /// R-1.1: "There is no circumstance in which the plugin holds a relay connection while no
    /// session is running." <b>The whole answer, in one property.</b> The two halves were composed
    /// at the call site in <c>SessionCoordinator.SynchroniseTransport</c>, which meant R-1.1 had a
    /// stated single home (<see cref="HostSession.RequiresRelayConnection"/>, per
    /// <c>RelayLink</c>'s remarks) that covered only the hosting side of it.
    /// </remarks>
    public bool RequiresRelayConnection => Host.RequiresRelayConnection || JoinRequiresRelayConnection;
}
