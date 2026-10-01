using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>What the relay does with a received message: drop it, reply to the sender, or forward it.</summary>
public enum RelayAction
{
    Drop = 0,

    ReplyToSender = 1,

    Forward = 2,
}

/// <summary>Why the router decided as it did for a message, such as a claimed code or an unadmitted sender.</summary>
public enum RelayOutcome
{
    MalformedEnvelope = 0,

    MalformedSessionCode = 1,

    CodeClaimed = 2,

    CodeAlreadyLive = 3,

    JoinForwardedToHost = 4,

    SessionNotFound = 5,

    PayloadForwarded = 6,

    SenderNotInSession = 7,

    RelayOnlyMessageFromClient = 8,

    SenderNotAdmitted = 9,

    UnrecognisedMessageType = 10,

    JoinerAdmitted = 11,

    JoinerRejected = 12,

    AdmissionFromNonHost = 13,

    UnknownJoiner = 14,

    PendingNoticeForwarded = 15,
}

/// <summary>The router's verdict for one message: its action, outcome, any reply, and who receives it.</summary>
public readonly record struct RelayDecision(
    RelayAction Action,
    RelayOutcome Outcome,
    WireEnvelope? Reply,
    IReadOnlyList<string> Recipients,
    bool CloseRecipients = false)
{
    public static RelayDecision Drop(RelayOutcome outcome) => new(RelayAction.Drop, outcome, null, []);

    public static RelayDecision Respond(RelayOutcome outcome, WireEnvelope reply) =>
        new(RelayAction.ReplyToSender, outcome, reply, []);

    public static RelayDecision Forward(
        RelayOutcome outcome,
        IReadOnlyList<string> recipients,
        bool closeAfterwards = false) =>
        new(RelayAction.Forward, outcome, null, recipients, closeAfterwards);

    public string Reason => Outcome.ToString();
}
