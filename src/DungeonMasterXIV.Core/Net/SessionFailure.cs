namespace DungeonMasterXIV.Net;

public enum SessionFailure
{
    None = 0,

    RelayUnreachable = 1,

    ConnectionLost = 2,

    SessionCodeNotActive = 3,

    PluginBehindRelay = 4,

    RelayBehindPlugin = 5,

    RegistrationNotAnswered = 6,

    RelayAddressUnreadable = 7,

    ConnectionNeverOpened = 8,

    HostKeyUnusable = 9,

    SessionKeysUnavailable = 10,
}

public static class SessionFailureMessage
{
    public static string For(SessionFailure failure) => failure switch
    {
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
        SessionFailure.SessionKeysUnavailable =>
            "The session did not start: this build could not create the session keys it needs. "
            + "Nothing was sent and no session exists. The plugin cannot tell why they could not be "
            + "created, so this is not a setting to change here — please report it with your "
            + "platform and how you launch the game.",
        SessionFailure.HostKeyUnusable =>
            "The host's answer to your request could not be used: it carried a key this plugin "
            + "cannot agree with, so no shared key was established and you have not joined. This "
            + "does not say why — a host running a different build, and something altering the "
            + "answer on the way, look the same from here and this client cannot tell them apart. "
            + "You can ask to join again.",
        _ => string.Empty,
    };
}
