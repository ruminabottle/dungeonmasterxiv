namespace DungeonMasterXIV.Data;

/// <summary>
/// Who keeps a session log after the session ends (R-2.12).
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule is asymmetric and the asymmetry is the requirement, not an implementation choice:</b>
/// <i>a player's log dies with the session unless EXPORTED; the DM's client retains its log
/// automatically, on their machine, where campaign data lives.</i>
/// </para>
/// <para>
/// <b>It is a type rather than an <c>if</c> at the call site</b> so that A-2.22 can be tested
/// without running a session — the criterion is about who retains, and that is a decision this
/// answers directly. An inline check at the one place logs are written would make the rule true and
/// unfalsifiable in the same stroke.
/// </para>
/// <para>
/// <b>Hosting is the whole of the question.</b> Not role, not entitlement, not whether the client
/// happens to have a campaign open — the DM's client is the one hosting, and that is what the
/// requirement names.
/// </para>
/// </remarks>
public static class LogRetention
{
    /// <summary>
    /// Whether this client keeps its log once the session ends.
    /// </summary>
    /// <param name="isHosting">Whether this client was the host — the DM's side.</param>
    /// <returns>True for the host, false for everyone else.</returns>
    /// <remarks>
    /// <b>False is not "discard later", it is "never wrote it down".</b> A player's log existing on
    /// disk and being cleaned up afterwards would satisfy the sentence and miss the point: the
    /// requirement is that it does not survive, and the only way to be sure of that is not to
    /// persist it. Export is the deliberate act that makes a copy, and it is the caller's, not this
    /// type's.
    /// </remarks>
    public static bool KeepsItsLog(bool isHosting) => isHosting;
}
