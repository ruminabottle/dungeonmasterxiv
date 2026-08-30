using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Chat;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// Where a person types a message and sends it (R-2.19, A-2.41).
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS IS THE PRODUCT SURFACE R-2.19 WAS MISSING.</b> <c>SessionMembership.Say</c> shipped with
/// DMXENG-121, correct and public, and nothing outside Core called it — nine message types built,
/// merged and green with no route by which a player could construct one. A-2.41 names no screen and
/// neither does this type's existence; <b>what it answers is that there IS one.</b>
/// </para>
/// <para>
/// <b>WHY A CONTROL IN THE SESSION WINDOW RATHER THAN A SLASH COMMAND OR A WINDOW OF ITS OWN, and
/// the reason is measured rather than taste:</b>
/// </para>
/// <para>
/// <b>1. It is where the person already is.</b> A message is part of a live session, and the session
/// window is the surface open while one runs. A slash command satisfies A-2.41 equally on paper and
/// puts the compose path somewhere the player is not looking during the thing it is for.
/// </para>
/// <para>
/// <b>2. A NEW WINDOW WOULD HAVE ADDED A FAILURE MODE THIS TICKET EXISTS TO CLOSE.</b>
/// <c>Plugin.Register</c> adds each window explicitly, so a fifth that skipped that line would be a
/// surface nothing can reach — <b>the same zero-producer defect one layer up</b>, and a call-site
/// test would not notice. Drawn from <see cref="SessionWindow"/>, which is already registered, there
/// is no registration to forget.
/// </para>
/// <para>
/// <b>3. The members with room are the ones this touches.</b> Measured at <c>2719162</c>:
/// <c>SessionWindow.DrawHosting</c> is 103 lines against a 60 block (margin -43) and
/// <c>Plugin</c>'s constructor is 88 (margin -28) — both grandfathered breaches that <b>may not
/// grow</b>. This adds nothing to either: two lines to <c>SessionWindow.Draw</c>, one field, and
/// its own file.
/// </para>
/// <para>
/// <b>THE REFUSAL IS SHOWN, WHICH IS HALF OF WHAT THIS OWES (A-2.35).</b> <c>Say</c> returns a
/// <see cref="MessageDraft"/> that names its own fault, and a surface that dropped it would fail the
/// criterion's <i>"the person who typed it is TOLD"</i> while the wire behaviour underneath stayed
/// perfectly correct. So the last refusal is held and rendered until the next attempt replaces it.
/// </para>
/// </remarks>
internal sealed class MessageComposeView
{
    private readonly SessionCoordinator _coordinator;

    /// <summary>What the person has typed and not yet sent.</summary>
    private string _entry = string.Empty;

    /// <summary>
    /// The last refusal, held so it stays on screen after the frame that produced it.
    /// </summary>
    /// <remarks>
    /// <b>Null once a send is accepted</b>, so a stale refusal cannot sit under a message that went
    /// out. ImGui redraws every frame and keeps nothing, so a fault reported into a local would be
    /// gone before it was read.
    /// </remarks>
    private string? _refusal;

    /// <param name="coordinator">The session layer this surface sends through.</param>
    public MessageComposeView(SessionCoordinator coordinator) => _coordinator = coordinator;

    /// <summary>The last refusal shown to the person, or null when the last send was accepted.</summary>
    internal string? Refusal => _refusal;

    /// <summary>Draws the compose box and the send control.</summary>
    public void Draw()
    {
        ImGui.InputText("Say", ref _entry, MessageLimits.Default.MaxUtf8Bytes);

        if (ImGui.Button("Send"))
        {
            Submit();
        }

        if (_refusal is { } refusal)
        {
            ImGui.TextUnformatted(refusal);
        }
    }

    /// <summary>
    /// Sends what has been typed, and reports the outcome the way A-2.35 requires.
    /// </summary>
    /// <remarks>
    /// <b>Separated from <see cref="Draw"/> so the send path can be EXERCISED.</b> No test in this
    /// repository can drive ImGui — the test project references Core alone and may never reference
    /// the plugin — so a compose path that existed only inside a draw call could be asserted by
    /// reading it and never by running it. That is the shape this ticket exists to close, and it
    /// would have been reproduced one layer further in.
    /// </remarks>
    internal void Submit()
    {
        var draft = _coordinator.Membership.Say(_entry);

        // The fault's own reason, never a sentence invented here: MessageFault distinguishes empty
        // from too-long from too-large from not-in-a-session, and a surface that flattened those
        // would tell the person less than the build knows.
        _refusal = draft.IsAccepted ? null : draft.Reason;

        if (draft.IsAccepted)
        {
            _entry = string.Empty;
        }
    }
}
