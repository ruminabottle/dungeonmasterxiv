using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// The DM's pending admission prompts (R-1.3a, R-1.3e).
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <see cref="SessionWindow"/>, and the seam was a mistake rather than a size
/// problem.</b> This drew from inside <c>DrawJoining</c> — the JOINER's method rendering the HOST's
/// surface. A DM answering requests and a player waiting to be admitted are two different people
/// looking at two different things, and the only reason they shared a method is that the pending
/// list happened to be reachable there.
/// </para>
/// <para>
/// Its own type also means the copy travels with the thing it describes: the three strings below are
/// this surface's and no other's, and <c>SessionWindow</c> no longer carries wording for a prompt it
/// does not draw.
/// </para>
/// <para>
/// <b>D-8 as amended is approve-blocking here specifically.</b> A name may be shown and may never
/// authenticate; a prompt that omits <i>or de-emphasises</i> the fingerprint is denied. The order
/// below is load-bearing: headline, then the fingerprint, then the out-of-band instruction, then a
/// deliberate unticked confirmation — and nothing may be inserted that pushes the fingerprint down.
/// </para>
/// </remarks>
internal sealed class AdmissionPromptView
{
    // R-1.7a, replaced 2026-08-28 by SQ-34. Literal product copy: substitute nothing, and do not
    // "improve" it. The previous sentence said the prompt shows a code and NOT a character name,
    // while the headline beside it rendered "Bob (PEER-3) is asking to join" — it contradicted
    // R-1.3e from the moment R-1.3e was decided (BUG-52). The code was byte-identical to what
    // R-1.7a then said, so the specification was the thing that was false, not this file.
    //
    // It denies the name any authority in the same breath as admitting one is shown, and it is two
    // sentences on purpose: the prompt's ordering is load-bearing and a longer disclosure would push
    // the fingerprint down, which D-8's amendment makes approve-blocking.
    private const string AdmissionDisclosure =
        "The name shown is chosen by the requester, not proof of who they are - the code is. Only admit "
        + "people you arranged to play with.";
    private const string CompareOutOfBand =
        "Ask the joining player to read their code back to you over voice or chat, and confirm it "
        + "matches. Do not ask them for it through the plugin - a channel someone has tampered with "
        + "cannot prove it has not been tampered with.";
    private const string UnverifiedWarning =
        "Admitted without the code being compared. This session is not protected against someone "
        + "sitting in the middle of it.";

    private readonly SessionCoordinator _coordinator;

    /// <summary>Draws the prompts for <paramref name="coordinator"/>'s pending requests.</summary>
    /// <param name="coordinator">The session layer. Read for pending requests; told the answer.</param>
    public AdmissionPromptView(SessionCoordinator coordinator) => _coordinator = coordinator;

    /// <summary>Draws one prompt per pending request, or nothing when there are none.</summary>
    public void Draw()
    {
        // Every pending request gets its own prompt. One slot would strand all but the newest, and
        // the stranded players would see a DM who appears to be ignoring them.
        var pending = _coordinator.Admissions.Pending;
        if (pending.Count == 0)
        {
            return;
        }

        ImGui.Separator();
        ImGui.TextWrapped(AdmissionDisclosure);

        var now = DateTimeOffset.UtcNow;

        // Copied because Admit and Deny mutate the pending list, and this is a draw callback.
        foreach (var request in pending.ToArray())
        {
            ImGui.Separator();
            // The ONLY thing a resolved relink changes. Everything below this line — the
            // fingerprint, the out-of-band warning, the deliberate confirmation, the two buttons —
            // is identical for a relink and a first-time join, and must stay identical. R-1.5:
            // the DM approves every relink, every session, and a match must not shorten the path.
            ImGui.TextUnformatted(AdmissionPrompt.Headline(request));

            ImGui.TextUnformatted($"Code to compare: {request.Fingerprint}");
            ImGui.TextWrapped(CompareOutOfBand);

            // R-1.3c: the wait is visibly bounded WHILE it runs, not only when it ends.
            var remaining = request.RemainingAt(now);
            ImGui.TextUnformatted($"This request lapses in {remaining:mm\\:ss}");

            // R-1.3a: a deliberate act, never a pre-ticked box. Starts false every time.
            var confirmed = request.FingerprintConfirmed;
            if (ImGui.Checkbox($"The code matched what they read to me##{request.PeerCode}", ref confirmed)
                && confirmed)
            {
                request.ConfirmFingerprintMatched();
            }

            if (!request.FingerprintConfirmed)
            {
                ImGui.TextWrapped(UnverifiedWarning);
            }

            // The prompt starts with NEITHER answer selected, for a relink exactly as for a first
            // join. Pre-selecting Accept for a recognised returning player would be the helpful
            // thing and it is forbidden: a DM pressing Enter on a focused button leaves the same
            // record as a DM who compared the fingerprint, which makes the record false rather than
            // merely weaker (R-1.5, R-1.3a). The favoured action is decided in Core so a change of
            // mind has to happen where a test is watching.
            if (ImGui.Button($"Admit##{request.PeerCode}"))
            {
                _coordinator.Admit(request.PeerCode);
            }

            if (AdmissionPrompt.Favoured(request) == AdmissionAction.Admit)
            {
                ImGui.SetItemDefaultFocus();
            }

            ImGui.SameLine();
            if (ImGui.Button($"Deny##{request.PeerCode}"))
            {
                _coordinator.Deny(request.PeerCode);
            }
        }
    }
}
