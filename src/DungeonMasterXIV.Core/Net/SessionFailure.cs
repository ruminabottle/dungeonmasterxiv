namespace DungeonMasterXIV.Net;

/// <summary>
/// Why a session connection is not working. R-1.8 requires the first three to be distinguishable to
/// the user, because the action each one calls for is different: wait, check your own network, or
/// check the code you were given. R-1.7b adds two more for the same reason — a version mismatch
/// calls for updating one side or the other, and "connection failed" would send the user looking at
/// their router.
/// </summary>
public enum SessionFailure
{
    /// <summary>Nothing is wrong.</summary>
    None = 0,

    /// <summary>The relay did not answer. Nothing the user can fix; the relay is down.</summary>
    RelayUnreachable = 1,

    /// <summary>The relay answered once and then stopped. Most likely the user's own connection.</summary>
    ConnectionLost = 2,

    /// <summary>The relay answered and reported no live session under that code.</summary>
    SessionCodeNotActive = 3,

    /// <summary>
    /// The relay speaks a newer protocol than this plugin. The user has to update the plugin
    /// (R-1.7b).
    /// </summary>
    PluginBehindRelay = 4,

    /// <summary>
    /// This plugin speaks a newer protocol than the relay. Nothing the user can fix on their side —
    /// the relay operator has to update, or they can point at a different relay (R-1.7b, R-1.8).
    /// </summary>
    RelayBehindPlugin = 5,
}

/// <summary>
/// The user-facing sentence for each failure.
/// </summary>
/// <remarks>
/// Separate from the enum because A-1.5b is about what the user is told, not about what the code
/// knows. R-1.8 forbids "connection failed" and forbids an indefinite spinner, so every state here
/// says which of the three things happened and what it means for the person reading it.
/// <para>
/// These are not R-1.7a strings. R-1.7a covers the session window, the admission prompt and the
/// settings section, and its wording is literal and may not be substituted. Failure text is not in
/// that set, so it is written here — under the same constraint that none of R-1.7a's forbidden
/// phrasings may appear.
/// </para>
/// </remarks>
public static class SessionFailureMessage
{
    /// <summary>The sentence shown for <paramref name="failure"/>.</summary>
    public static string For(SessionFailure failure) => failure switch
    {
        SessionFailure.RelayUnreachable =>
            "The relay is not responding. This is not your connection — the relay itself is unreachable. "
            + "You can try again, or point the plugin at a different relay in settings.",
        SessionFailure.ConnectionLost =>
            "The connection to the relay dropped. The relay was reachable a moment ago, so check your "
            + "own network first.",
        SessionFailure.SessionCodeNotActive =>
            "No session is running under that code. Check the code with your DM — codes belong to a "
            + "session that is live now, so one from last week will not work until they start again.",
        SessionFailure.PluginBehindRelay =>
            "This plugin is too old for that relay. Update the plugin and try again — the relay "
            + "speaks a newer version of the session protocol than this build does.",
        SessionFailure.RelayBehindPlugin =>
            "That relay is older than this plugin and cannot speak to it. Nothing on your side is "
            + "wrong: the relay has to be updated, or you can point the plugin at a different one in "
            + "settings.",
        _ => string.Empty,
    };
}
