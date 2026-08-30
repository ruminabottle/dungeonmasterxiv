using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
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
/// <para>
/// <b>THE KEEP-OR-LOSE OFFER IS RAISED HERE BECAUSE THIS IS THE JOINER'S SIDE</b> (R-2.12, A-2.23).
/// A player's log dies with the session unless it is kept; the DM's retains without being asked, so
/// the host's half of the window raises nothing. <b>A-2.23 fails a build whose only route is a
/// settings menu</b> — <i>"a keep-or-lose choice presented after the thing is gone is not a
/// choice"</i> — which is why the offer is taken at the moment the player leaves rather than left
/// for them to find later.
/// </para>
/// <para>
/// <b>THE OFFER IS TAKEN BEFORE THE DEPARTURE — AND THAT ORDER IS DEFENSIVE TODAY, NOT
/// LOAD-BEARING. Measured rather than assumed, because the obvious claim here is false.</b>
/// <c>SessionMembership.Leave</c> is an announcement, a cleared closing notice and
/// <c>_joiner.Left()</c>; <b>it does not release the recording</b>, so taking the offer after it
/// would still see the entries and this ordering is currently pinned by no test.
/// <para>
/// It is still the right order, and the reason is the OTHER path: the host's
/// <c>HostRunner.Stop</c> calls <c>SessionResources.Release</c>, which calls
/// <c>Recording.Release</c> — <i>"R-2.12: a log dies with the session."</i> So a release on
/// departure already exists in this codebase, on the half of it that does not raise an offer.
/// <b>Writing the fragile order would be correct only until the joiner's leave grew the same
/// call</b>, and it would then fail exactly as the retention-ordering defect in
/// <c>SessionTeardown</c> did: still running, still looking finished, describing an empty log.
/// </para>
/// </para>
/// </remarks>
internal sealed class SessionEndingView
{
    private readonly SessionCoordinator _coordinator;

    private readonly KeepOrLose _keepOrLose;

    /// <summary>The open offer, or null when there is no choice outstanding.</summary>
    private SessionLogOffer? _offer;

    /// <param name="coordinator">The session layer this surface reflects and acts on.</param>
    /// <param name="keepOrLose">
    /// Opens the keep-or-lose choice over the session's log, and says where an accepted export goes
    /// (R-2.12, A-2.23a). Supplied rather than built here: the campaign, the clock, the length of
    /// the window and the export directory are the composition root's, and a window class that
    /// decided any of them would be deciding product.
    /// </param>
    public SessionEndingView(SessionCoordinator coordinator, KeepOrLose keepOrLose)
    {
        _coordinator = coordinator;
        _keepOrLose = keepOrLose;
    }

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

        if (DrawTheOffer())
        {
            return;
        }

        if (join.Phase != JoinPhase.Admitted)
        {
            return;
        }

        // R-1.3g: the player leaves and the DM's roster reflects it. The notice to the host is best
        // effort and the departure is not -- see SessionMembership.Leave, where that is decided.
        if (ImGui.Button("Leave session"))
        {
            // Taken FIRST, while the session still holds the entries. See the remarks.
            _offer = _keepOrLose.Open();
            _coordinator.Membership.Leave();
        }
    }

    /// <summary>Draws the keep-or-lose choice, and says whether it is holding the window.</summary>
    /// <returns>True while a choice is outstanding, so the caller draws nothing else.</returns>
    private bool DrawTheOffer()
    {
        if (_offer is not { IsOpen: true } offer)
        {
            return false;
        }

        // R-1.3c: the bound is shown WHILE the wait happens. An offer that expired silently would
        // be the indefinite wait the requirement forbids, wearing a countdown's clothes.
        var remaining = offer.RemainingAt(DateTimeOffset.UtcNow.UtcTicks);
        if (offer.ElapseTo(DateTimeOffset.UtcNow.UtcTicks))
        {
            return false;
        }

        ImGui.TextUnformatted(
            offer.HasAnything
                ? $"Keep this session's log? {offer.LineCount} lines, {offer.Participants.Count} people. {remaining:mm\\:ss}"
                : $"This session recorded nothing to keep. {remaining:mm\\:ss}");

        // A-2.23a: accepting must not present an act the build does not perform. It performs one
        // now -- DMXENG-123 shipped the writer, so the disclosure that stood in for it is gone
        // rather than left to age into a lie. The log is gone a second after the click, so this is
        // the only moment the write can happen.
        if (ImGui.Button("Keep"))
        {
            SessionExport.Produce(offer, _keepOrLose.Export);
        }

        if (ImGui.Button("Discard"))
        {
            offer.Decline();
        }

        return true;
    }
}
