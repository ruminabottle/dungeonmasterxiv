using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

/// <summary>Where a session-log offer stands: still open, kept, or declined (also when its time runs out).</summary>
public enum SessionLogOfferOutcome
{
    Pending,

    Kept,

    Declined,
}

/// <summary>Holds a session's log for a one-time keep-or-discard choice, dropping it if declined or out of time.</summary>
public sealed class SessionLogOffer
{
    private readonly long _closesAtUtcTicks;

    private RetainedLog? _log;

    public SessionLogOffer(RetainedLog log, long closesAtUtcTicks)
    {
        ArgumentNullException.ThrowIfNull(log);

        _log = log;
        _closesAtUtcTicks = closesAtUtcTicks;
    }

    public SessionLogOfferOutcome Outcome { get; private set; } = SessionLogOfferOutcome.Pending;

    public bool IsOpen => Outcome == SessionLogOfferOutcome.Pending;

    public int LineCount => RetainedLogFormat.LineCount(Held);

    public bool HasAnything => RetainedLogFormat.HasAnything(Held);

    public IReadOnlyList<string> Participants => RetainedLogFormat.Participants(Held);

    public TimeSpan RemainingAt(long nowUtcTicks) =>
        nowUtcTicks >= _closesAtUtcTicks
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(_closesAtUtcTicks - nowUtcTicks);

    public RetainedLog Keep()
    {
        EnsureUnanswered();

        var kept = Held;
        Outcome = SessionLogOfferOutcome.Kept;

        return kept;
    }

    public void Decline()
    {
        EnsureUnanswered();

        _ = Held;
        Resolve();
    }

    public bool ElapseTo(long nowUtcTicks)
    {
        if (!IsOpen || nowUtcTicks < _closesAtUtcTicks)
        {
            return false;
        }

        Resolve();

        return true;
    }

    private void EnsureUnanswered()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException(
                "The offer has already been answered. A keep-or-lose choice is made once.");
        }
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
