using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Who this client has been TOLD is in the session (R-1.3f), and the rule for replacing it.
/// </summary>
/// <remarks>
/// <para>
/// <b>On the HOST this stays empty, and that is not an oversight.</b> The host authors the roster
/// from <c>SessionAudience</c> and never receives one — D-3 makes it the author, so a host reading
/// its own broadcast back would be believing a copy of what it already knows. This is what a PLAYER
/// was told, which is the only place the distinction matters.
/// </para>
/// <para>
/// <b>Replaced, never merged.</b> A participant who left is gone because the next roster does not
/// list them, rather than lingering until a removal message that may never arrive.
/// </para>
/// <para>
/// <b>Why it is a type rather than a field on <see cref="SessionCoordinator"/>.</b> Every other
/// piece of session state that coordinator holds lives in a collaborator — hosting, joining,
/// admissions, resources, timeouts, interruption — and this was the one exception: a bare mutable
/// field whose replacement rule was a <c>??</c> inside a closure passed as an argument inside the
/// frame loop. That is the "rule each call site is trusted to remember" shape this codebase argues
/// against everywhere else, and it left the null case with no name and no test to hang on.
/// </para>
/// <para>
/// The inverse is <c>MemberContentReceipts</c>: that is what members told the HOST, and it already
/// lives on <c>SessionResources</c>. The two halves of "what this client knows about who else is
/// here" were split between a collaborator and a field.
/// </para>
/// </remarks>
internal sealed class ReceivedRoster
{
    /// <summary>What the host last said the membership is. Empty until one arrives.</summary>
    public IReadOnlyList<RosterEntry> Entries { get; private set; } = [];

    /// <summary>
    /// Takes what arrived in a payload, if it carried a roster at all.
    /// </summary>
    /// <param name="entries">
    /// The roster from the message, or null. <b>Null leaves the previous one standing</b> — a
    /// payload that says nothing about the membership is not a payload saying the session emptied,
    /// and most payloads carry no roster.
    /// </param>
    public void Replace(IReadOnlyList<RosterEntry>? entries) => Entries = entries ?? Entries;
}
