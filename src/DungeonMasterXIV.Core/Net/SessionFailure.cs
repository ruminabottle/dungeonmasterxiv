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

    /// <summary>
    /// The relay accepted the connection but never confirmed the session code (BUG-36).
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="RelayUnreachable"/> because the relay <b>was</b> reached: it
    /// answered, upgraded the connection and held it open. Reporting this as "unreachable" sent an
    /// evening into DNS, TLS and certificate checks on a relay that was healthy the whole time,
    /// while the actual fault was that this client never spoke. A failure message that names the
    /// wrong half of a system is worse than a vague one, because it is actionable and the action is
    /// wasted.
    /// </remarks>
    RegistrationNotAnswered = 6,

    /// <summary>
    /// The relay address in settings could not be parsed, so nothing was contacted (BUG-37).
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="RelayUnreachable"/> because <b>no connection was attempted</b>. The
    /// address never became a URL, so no socket was opened and this build has learned nothing about
    /// whether the relay is up. Reporting it as "unreachable" blamed a third party for the user's
    /// own typo, and sent them to a relay status page over a missing "s" in "wss".
    /// <para>
    /// The pair with <see cref="RegistrationNotAnswered"/> is the point: that one says the relay was
    /// reached, this one says nothing was. Between them, <see cref="RelayUnreachable"/> is left to
    /// mean what it says.
    /// </para>
    /// </remarks>
    RelayAddressUnreadable = 7,

    /// <summary>
    /// The connection to the relay never finished opening before the clock ran out (BUG-38).
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="RegistrationNotAnswered"/>, which is the case where the socket DID
    /// open and the relay then said nothing. Reporting this as that one told a user whose firewall
    /// was dropping the connection that the relay had accepted it and that their network was not the
    /// problem — ruling out the actual cause by name, which is worse than merely naming the wrong
    /// party.
    /// <para>
    /// A refused port fails in about a millisecond and never reaches here. A DROPPED one hangs the
    /// full timeout, which is why this is invisible on a machine whose network refuses and shows up
    /// on the other end of "test it with a friend".
    /// </para>
    /// </remarks>
    ConnectionNeverOpened = 8,

    /// <summary>
    /// The host's acceptance carried a public key this client cannot agree with (BUG-59).
    /// </summary>
    /// <remarks>
    /// The mirror of BUG-56, at the other end of the exchange: that one stopped a host admitting a
    /// joiner whose key it could never use, this one stops a joiner deriving from a host key it
    /// cannot use. Distinct from every value above because <b>nothing about the connection is
    /// wrong</b> — the relay is reachable, the socket is open, the frame decoded, and the DM said
    /// yes. Reporting it as any kind of connection failure would send the user to their router over
    /// a session that was never cryptographically possible.
    /// <para>
    /// <b>What this client has established, and no more (A-1.5j).</b> It knows the key on the
    /// acceptance cannot be agreed with. It does <b>not</b> know whether the host is broken, the
    /// relay tampered, or the two ends are on different builds — those are indistinguishable from
    /// here, and the sentence says so rather than picking one.
    /// </para>
    /// </remarks>
    HostKeyUnusable = 9,
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
        // BUG-49. This said "This is not your connection — the relay itself is unreachable", which
        // rules out a cause that produces it: a firewall REFUSING with a TCP RST lands here, and a
        // refusal is evidence something answered — something that can sit on the user's side of the
        // path. The same file's ConnectionNeverOpened text already says as much ("one that refuses
        // fails immediately"), so the two sentences contradicted each other on this exact case.
        //
        // "Unreachable" is kept because reachability is a property of the PATH and stays true either
        // way. What is dropped is the claim to know WHICH END, which the client cannot tell.
        SessionFailure.RelayUnreachable =>
            "The relay is not responding — the connection was refused or could not be made. "
            + "Reachability is a property of the path, so this does not say which end is at "
            + "fault: a firewall that rejects the connection outright looks exactly the same. "
            + "Check your own network as well as the relay address in settings, or point the "
            + "plugin at a different relay.",
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
        SessionFailure.RelayAddressUnreadable =>
            "The relay address in settings could not be read, so nothing was contacted — this says "
            + "nothing about the relay or about your own network. Check what you typed in settings: "
            + "it has to be a full address beginning with wss://, like " + RelayEndpoint.Default + ".",
        SessionFailure.ConnectionNeverOpened =>
            "The connection to the relay never finished opening — it was still being attempted when "
            + "time ran out. That can be the relay, and it can equally be something between you and "
            + "it: a firewall that silently drops a connection looks exactly like a relay that is "
            + "not there, where one that refuses fails immediately. Check your own network as well "
            + "as the relay address in settings.",
        SessionFailure.RegistrationNotAnswered =>
            "The relay accepted the connection but never confirmed the session code. The relay is "
            + "reachable, so this is not your network — try starting the session again, and if it "
            + "keeps happening the relay is not answering registrations.",
        // BUG-59. Constrained rather than transcribed: R-1.7a governs only the strings it QUOTES, so
        // this is engineering-authored under A-1.7e, and A-1.5j bounds what it may assert.
        //
        // It says the answer could not be used, and REFUSES TO SAY WHY, because this client cannot
        // tell a broken host from a tampering relay from a version skew. It names no network — the
        // relay is reachable, so blaming it would be false and exonerating it is the BUG-49 mistake
        // in the other direction. It claims no protection (D-8): there is no session to protect.
        // "You can ask to join again" is true at the moment it is shown — MayRequestAgain includes
        // Failed — and TheRetryOfferIsTrueWhenItIsShown asserts that rather than trusting it.
        SessionFailure.HostKeyUnusable =>
            "The host's answer to your request could not be used: it carried a key this plugin "
            + "cannot agree with, so no shared key was established and you have not joined. This "
            + "does not say why — a host running a different build, and something altering the "
            + "answer on the way, look the same from here and this client cannot tell them apart. "
            + "You can ask to join again.",
        _ => string.Empty,
    };
}
