namespace DungeonMasterXIV.Net;

/// <summary>
/// Why a session connection is not working. R-1.8 requires these three to be distinguishable to the
/// user, because the action each one calls for is different: wait, check your own network, or check
/// the code you were given.
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
        _ => string.Empty,
    };
}
