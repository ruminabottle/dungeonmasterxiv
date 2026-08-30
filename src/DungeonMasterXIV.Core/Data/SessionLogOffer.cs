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
/// <b>WHAT KEEPING DOES NOT DO, AND IT STILL DOES NOT DO IT</b> (A-2.23a).
/// <see cref="Keep"/> resolves the choice and hands back the log; <b>it writes nothing.</b>
/// A-2.23a is now satisfied by the FIRST of its dispositions rather than the third — DMXENG-123
/// shipped the writer, so the disclosure that stood in for it is gone and the caller performs the
/// act at the moment of the click.
/// <b>Both halves of A-2.23a fail separately:</b> silently writing nothing fails, and writing a
/// file carrying a participant identifier fails A-1.11a — <b>which is why this type still holds no
/// store, no archive and no formatter, and a test asserts that it cannot acquire one.</b> That
/// remains true with a writer in the tree and is MORE load-bearing now, not less: the export is
/// composed by the caller from <see cref="SessionExportFormat"/> and written through
/// <see cref="ISessionExportDestination"/>, so nothing here can reach
/// <see cref="RetainedLogFormat"/> and put a peer code into a genuine export.
/// </para>
/// </remarks>
public sealed class SessionLogOffer
{
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
