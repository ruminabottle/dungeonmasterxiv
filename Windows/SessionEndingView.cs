using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// The session ending, from the joiner's side: the DM's closing notice, and leaving (R-1.3g).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two triggers, one situation, which is why they are one view.</b> Either the DM ends the
/// session and this client is told, or the player chooses to go. Both end the same thing and neither
/// waits for the other side to agree.
/// </para>
/// <para>
/// <b>The countdown is a REQUIREMENT, not a courtesy</b> (R-1.3g). "The session is closing" without
/// "how long remains" is the indefinite wait R-1.3c and R-1.8 both exist to forbid, so the two lines
/// are one sentence and a build that showed the first without the second would fail the criterion
/// while looking finished.
/// </para>
/// <para>
/// <b>No duration is decided here.</b> The sixty seconds are applied once, by the host, in
/// <c>SessionClosing.DecidedByHost</c>; this reads <c>RemainingAt</c> against the instant that
/// arrived. A second place that knew the number is how a host and a client come to disagree, which
/// R-1.3c names in terms.
/// </para>
/// <para>
/// <b>The leave button is ABSENT rather than disabled when there is nothing to leave</b>, which is
/// the treatment R-1.3h states for the exclusivity affordances: a greyed control that still occupies
/// the window invites the question its absence answers.
/// </para>
/// </remarks>
internal sealed class SessionEndingView
{
    private readonly SessionCoordinator _coordinator;

    /// <param name="coordinator">The session layer this surface reflects and acts on.</param>
    public SessionEndingView(SessionCoordinator coordinator) => _coordinator = coordinator;

    /// <summary>Draws the closing notice, and the way out.</summary>
    /// <param name="join">The attempt being rendered.</param>
    public void Draw(JoinAttempt join)
    {
        ArgumentNullException.ThrowIfNull(join);

        // Read once. Asking twice would let the countdown and the sentence above it come from two
        // different instants, which is exactly the drift the single-instant design prevents.
        if (_coordinator.Membership.Closing is { } closing)
        {
            ImGui.TextUnformatted(
                $"The DM has ended this session. It closes in {closing.RemainingAt(DateTimeOffset.UtcNow):mm\\:ss}");
        }

        if (join.Phase != JoinPhase.Admitted)
        {
            return;
        }

        // R-1.3g: the player leaves and the DM's roster reflects it. The notice to the host is best
        // effort and the departure is not -- see SessionMembership.Leave, where that is decided.
        if (ImGui.Button("Leave session"))
        {
            _coordinator.Membership.Leave();
        }
    }
}
