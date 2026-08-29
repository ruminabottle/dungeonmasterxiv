using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// Shows the player every participant id their client stores, and lets them delete one (A-1.9b,
/// A-1.9c).
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own view rather than another section of the settings window</b>, for the same reason
/// <c>AdmissionPromptView</c> and <c>JoinFlowView</c> are: one subject, drawn in one place, with the
/// wording it renders decided in Core where a test can watch it.
/// </para>
/// <para>
/// <b>THE ONLY STATE HERE IS WHICH ROW IS ASKING.</b> Everything else is read from the memory each
/// frame, so a deletion that happens is reflected immediately and nothing is cached that could
/// disagree with what a join will actually carry.
/// </para>
/// </remarks>
public sealed class RelinkMemoryView
{
    private readonly Func<RelinkMemory> _relink;
    private readonly Action _persist;

    private string _confirming = string.Empty;

    /// <param name="relink">What this client remembers, read live.</param>
    /// <param name="persist">
    /// Writes the memory to disk. <b>Required, and called on every deletion</b> — a removal the
    /// player was shown and the disk never heard about brings the id back on next launch with
    /// nothing to say why, which is worse than refusing to delete at all.
    /// </param>
    public RelinkMemoryView(Func<RelinkMemory> relink, Action persist)
    {
        ArgumentNullException.ThrowIfNull(relink);
        ArgumentNullException.ThrowIfNull(persist);

        _relink = relink;
        _persist = persist;
    }

    /// <summary>Draws the list and, for at most one row, the deletion warning.</summary>
    public void Draw()
    {
        ImGui.TextWrapped(RelinkDisclosure.WhatIsStored);

        var remembered = _relink().All;

        if (remembered.Count == 0)
        {
            // Said rather than left blank. An empty region reads as "not implemented yet"; this says
            // the client is storing nothing, which is a fact the player asked for.
            ImGui.TextWrapped("Your client is not storing any participant ids right now.");
            return;
        }

        foreach (var entry in remembered.ToArray())
        {
            ImGui.Separator();

            // BOTH VALUES SHOWN. A-1.9b is "list what their client stores", and the id IS what is
            // stored -- showing only the code would list the label and hide the thing.
            ImGui.TextUnformatted($"Session code {entry.SessionCode}");
            ImGui.TextUnformatted($"Participant {entry.ParticipantId:D}");

            if (_confirming == entry.SessionCode)
            {
                DrawConfirmation(entry.SessionCode);
                continue;
            }

            if (ImGui.Button($"{RelinkDisclosure.BeginForgetting}##{entry.SessionCode}"))
            {
                // ARMS THE QUESTION, DELETES NOTHING. A-1.9c: a one-click irreversible delete fails.
                _confirming = entry.SessionCode;
            }
        }
    }

    private void DrawConfirmation(string sessionCode)
    {
        ImGui.TextWrapped(RelinkDisclosure.BeforeForgetting(sessionCode));

        // KEEP IS FIRST AND IS THE ORDINARY ANSWER. The destructive control is never the one a
        // player reaches by pressing where the previous button was.
        if (ImGui.Button($"{RelinkDisclosure.KeepIt}##keep-{sessionCode}"))
        {
            _confirming = string.Empty;
        }

        ImGui.SameLine();

        if (!ImGui.Button($"{RelinkDisclosure.ConfirmForget}##forget-{sessionCode}"))
        {
            return;
        }

        // PERSISTED IMMEDIATELY, and only if something was actually removed. A-1.9b requires the
        // UUID to be gone from disk, not merely from this list -- and A-1.9e is satisfied by there
        // being nothing here that could send: this view holds a memory and a save, and no transport.
        if (_relink().Forget(SessionCode.FromValid(sessionCode)))
        {
            _persist();
        }

        _confirming = string.Empty;
    }
}
