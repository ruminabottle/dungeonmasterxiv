using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

/// <summary>How a <see cref="SessionLogOffer"/> ended.</summary>
public enum SessionLogOfferOutcome
{
    /// <summary>Still open. The log is being held.</summary>
    Pending,

    /// <summary>Kept. The log survived the choice.</summary>
    Kept,

    /// <summary>Declined, whether said or left unanswered. The log is gone.</summary>
    Declined,
}

/// <summary>
/// The keep-or-lose choice a player is given as the session ends (R-2.12, A-2.23).
/// </summary>
/// <remarks>
/// <para>
/// <b>IT HOLDS THE LOG SO THAT TEARDOWN DOES NOT HAVE TO WAIT.</b> SQ-115 ruled that the offer does
/// NOT block teardown, and that what binds instead is that <i>the log survives until the choice
/// resolves</i> — <i>"a keep-or-lose choice presented after the thing is gone is not a choice."</i>
/// The ruling named three ways to arrange that and left the choice to engineering; this is the
/// third, <b>a hold on one object rather than on the teardown sequence</b>. The session can unwind
/// underneath an open offer, because the entries were copied into the log this object holds.
/// </para>
/// <para>
/// <b>DECLINING BY INACTION IS DECLINING, AND THAT IS WHY THE LOG IS ACTUALLY DROPPED.</b> Under
/// decision 4 an ignored offer means the log dies with the session, and A-1.2z is not breached
/// because <b>the offer is what makes the discard not-silent</b>. So <see cref="Decline"/> and a
/// lapse both release the log, and the prompt facts stop being readable — a build that kept the
/// entries around after a decline would satisfy every assertion about the OUTCOME while leaving the
/// thing the outcome was about still sitting there.
/// </para>
/// <para>
/// <b>THE WINDOW IS TAKEN, NOT CHOSEN HERE</b> (R-1.3c: no unbounded wait, anywhere). The closing
/// instant is a constructor parameter because <b>no requirement states a duration for this wait</b>
/// — R-1.3c's table names five bounded waits and this is not one of them. A number invented in this
/// file would become the product's answer by being the only one written down, so the caller supplies
/// it and this type holds no opinion.
/// </para>
/// <para>
/// <b>ONE LOG, AND A SECOND IS NOT EXPRESSIBLE</b> (A-2.16). There is no overload, no collection
/// parameter, and nothing here reaches for a log it was not handed. That is the live half of A-2.16
/// after SQ-109, and it is asserted by shape rather than by behaviour, <b>because a merging overload
/// passes every behavioural test written against the single-log one.</b>
/// </para>
/// <para>
/// <b>WHAT KEEPING DOES NOT DO, AND THE OFFER HAS TO SAY SO OUT LOUD</b> (A-2.23a).
/// <see cref="Keep"/> resolves the choice and hands back the log; <b>it writes nothing.</b>
/// <b>A build where a player accepts and no export is produced FAILS unless the offer states, AT
/// THE OFFER, that the export cannot yet be written</b> — so <see cref="NothingCanBeWrittenYet"/>
/// is not a courtesy and belongs on screen beside the choice, not in a release note. The log is
/// gone a second after the click; <b>a player who clicks yes and is told nothing believes it was
/// kept, and nothing afterwards can correct them because nothing is left to correct it with.</b>
/// <b>Both halves of A-2.23a fail separately:</b> silently writing nothing fails, and writing a
/// file carrying a participant identifier fails A-1.11a — which is why this type holds no store,
/// no archive and no formatter, and a test asserts that it cannot acquire one. A player's kept log has no
/// destination yet: A-2.17 records that the export "does not exist yet",
/// <see cref="RetainedLogStore.Retain"/> writes only for a hosting client (A-2.22), and
/// <see cref="RetainedLogFormat"/> is not an export and says so — reusing its bytes here would put a
/// peer code into a genuine export, which is the hazard the SQ-114 rename exists to prevent. So the
/// consumer of a kept log is owed by R-2.12's other half and is deliberately absent rather than
/// stubbed.
/// </para>
/// </remarks>
public sealed class SessionLogOffer
{
    /// <summary>
    /// What the offer must say beside the choice, because keeping cannot write anything yet
    /// (A-2.23a). This is the third of A-2.23a's dispositions: say so at the point of the claim,
    /// rather than offer a button that quietly does nothing.
    /// </summary>
    /// <remarks>
    /// <b>DELETE THIS STRING AND ITS USE WHEN THE EXPORT WRITER LANDS. IT BECOMES FALSE THE MOMENT
    /// A WRITER EXISTS</b>, and a user-facing sentence that silently stops being true is worse than
    /// one that was never written. <b>The removal is an explicit precondition on the export
    /// writer's own ticket, <b>DMXENG-123</b></b> (SQ-124 ruled option B on 2026-08-30, so the
    /// writer is no longer blocked — it is simply not DMXENG-115's, and was deliberately not folded
    /// into it).
    /// <para>
    /// <b>BOTH KEYS, so the expiry is discoverable from here rather than only from the board:</b>
    /// <b>DMXENG-115</b> is where this sentence lives, <b>DMXENG-123</b> is the ticket that deletes
    /// it and carries that deletion as an explicit obligation. Named in the code because a PR body
    /// is not where the next engineer is standing when they make this false.
    /// </para>
    /// </remarks>
    public const string NothingCanBeWrittenYet =
        "This choice is recorded, but no file can be written yet: the export format is not decided.";

    private readonly long _closesAtUtcTicks;

    /// <summary>Null once the choice has resolved against keeping — the log dying, in one field.</summary>
    private RetainedLog? _log;

    /// <param name="log">The session's log, already copied out of the session that is ending.</param>
    /// <param name="closesAtUtcTicks">When the offer stops being answerable (R-1.3c).</param>
    public SessionLogOffer(RetainedLog log, long closesAtUtcTicks)
    {
        ArgumentNullException.ThrowIfNull(log);

        _log = log;
        _closesAtUtcTicks = closesAtUtcTicks;
    }

    /// <summary>How the choice ended, or that it has not.</summary>
    public SessionLogOfferOutcome Outcome { get; private set; } = SessionLogOfferOutcome.Pending;

    /// <summary>Whether the choice is still open and the log still held.</summary>
    public bool IsOpen => Outcome == SessionLogOfferOutcome.Pending;

    /// <summary>How many lines the offer is about (A-2.23's prompt).</summary>
    public int LineCount => RetainedLogFormat.LineCount(Held);

    /// <summary>Whether there is anything to keep at all.</summary>
    public bool HasAnything => RetainedLogFormat.HasAnything(Held);

    /// <summary>The peer codes in the log, so the prompt can say who is in it.</summary>
    public IReadOnlyList<string> Participants => RetainedLogFormat.Participants(Held);

    /// <summary>
    /// What is left of the window at <paramref name="nowUtcTicks"/>, never negative.
    /// </summary>
    /// <remarks>
    /// <b>R-1.3c requires the bound to be VISIBLE while the wait is happening</b>, not only
    /// announced when it ends, which is why this is readable rather than internal to the lapse.
    /// </remarks>
    public TimeSpan RemainingAt(long nowUtcTicks) =>
        nowUtcTicks >= _closesAtUtcTicks
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(_closesAtUtcTicks - nowUtcTicks);

    /// <summary>Keeps the log, and returns it because nothing here can write it.</summary>
    /// <returns>The log the offer was holding.</returns>
    /// <exception cref="InvalidOperationException">The choice has already resolved.</exception>
    public RetainedLog Keep()
    {
        var kept = Held;
        Outcome = SessionLogOfferOutcome.Kept;

        return kept;
    }

    /// <summary>Declines, and the log dies with the session.</summary>
    /// <exception cref="InvalidOperationException">The choice has already resolved.</exception>
    public void Decline()
    {
        _ = Held;
        Resolve();
    }

    /// <summary>
    /// Ends the window when <paramref name="nowUtcTicks"/> has reached it, and does nothing before.
    /// </summary>
    /// <remarks>
    /// <b>A lapse is a decline and not a third outcome</b> — decision 4 rules that an ignored offer
    /// loses the log, so an <c>Expired</c> case would be a distinction the product does not make.
    /// </remarks>
    /// <returns>True when this call is what closed it.</returns>
    public bool ElapseTo(long nowUtcTicks)
    {
        if (!IsOpen || nowUtcTicks < _closesAtUtcTicks)
        {
            return false;
        }

        Resolve();

        return true;
    }

    private RetainedLog Held =>
        _log ?? throw new InvalidOperationException(
            "The offer has resolved and the log is gone. A declined log is dropped, not retained.");

    private void Resolve()
    {
        Outcome = SessionLogOfferOutcome.Declined;
        _log = null;
    }
}
