using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

/// <summary>
/// The one step teardown takes for R-2.12: keep this session's log, if this client is the one that
/// keeps logs.
/// </summary>
/// <remarks>
/// <para>
/// <b>IT EXISTS SO THE ORDER CAN BE TESTED.</b> Retention has to run <i>before</i> the coordinator
/// tears the session down — after <c>Detach</c> there is no session left to record. The Engineering
/// Lead ruled that the ordered sequence lives in <c>SessionTeardown</c> rather than in a lambda,
/// because a lambda in the plugin project cannot be reached by a test. This type is what that
/// sequence calls, so the position is assertable.
/// </para>
/// <para>
/// <b>The entries and the campaign are supplied, not fetched.</b> Measured rather than assumed:
/// <c>SessionCoordinator</c> exposes no stream — there is no <c>Stream</c> property on it — so a
/// retention step inside teardown cannot reach the log it is meant to keep. Both come from the
/// composition root, which is why this takes them and holds no opinion about where they came from.
/// </para>
/// <para>
/// <b>Hosting is asked of the coordinator, not passed.</b> <c>InAHostedSession</c> is already public
/// and is the fact R-2.12 turns on — the DM's client retains, a player's does not. Taking it as a
/// parameter would let a caller assert something the session itself could contradict.
/// </para>
/// </remarks>
public sealed class SessionLogRetention(
    RetainedLogStore store,
    Guid campaignId,
    Func<IReadOnlyList<LoggedEntry>> entries)
{
    private readonly RetainedLogStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    private readonly Func<IReadOnlyList<LoggedEntry>> _entries =
        entries ?? throw new ArgumentNullException(nameof(entries));

    /// <summary>How many times retention has been asked to run, so a test can pin its position.</summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// Keeps the session's log when <paramref name="isHosting"/>, and does nothing otherwise.
    /// </summary>
    /// <param name="isHosting">Whether this client hosted — from the session, not from a caller.</param>
    /// <param name="endedAtUtcTicks">When the session ended.</param>
    /// <returns>True when a log was written.</returns>
    public bool Retain(bool isHosting, long endedAtUtcTicks)
    {
        Attempts++;

        var log = new RetainedLog(campaignId, endedAtUtcTicks, _entries());
        return _store.Retain(log, isHosting);
    }
}
