using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay.Sessions;

public enum RelayAction
{
    Drop = 0,

    ReplyToSender = 1,

    Forward = 2,
}

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
