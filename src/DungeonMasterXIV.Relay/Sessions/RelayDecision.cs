using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>What the relay does with one received envelope.</summary>
public enum RelayAction
{
    /// <summary>Discard it. <see cref="RelayDecision.Reason"/> says why, for the forensic log.</summary>
    Drop = 0,

    /// <summary>Answer the sender directly. Used for code arbitration (R-1.2a).</summary>
    ReplyToSender = 1,

    /// <summary>Pass the received bytes on to <see cref="RelayDecision.Recipients"/> unchanged.</summary>
    Forward = 2,
}

/// <summary>
/// The outcome of one routing decision, in the vocabulary the forensic log uses (A-1.5a-r).
/// </summary>
/// <remarks>
/// These are the reasons QA reads after an attempt with no human present, so they name what
/// happened rather than where in the code it happened.
/// </remarks>
public enum RelayOutcome
{
    /// <summary>Bytes arrived that were not a well-formed envelope.</summary>
    MalformedEnvelope = 0,

    /// <summary>A session code that is not six characters of the R-1.2a alphabet.</summary>
    MalformedSessionCode = 1,

    /// <summary>A host asked for a code and got it.</summary>
    CodeClaimed = 2,

    /// <summary>A host asked for a code that was already live and must regenerate (R-1.2a).</summary>
    CodeAlreadyLive = 3,

    /// <summary>A join request reached the host of a live session.</summary>
    JoinForwardedToHost = 4,

    /// <summary>A join request named a code no session is live under.</summary>
    SessionNotFound = 5,

    /// <summary>An encrypted payload was passed on to the other members of its session.</summary>
    PayloadForwarded = 6,

    /// <summary>A payload arrived from a connection attached to no session.</summary>
    SenderNotInSession = 7,

    /// <summary>
    /// A client sent a message type only the relay may send. Not an error the plugin can produce;
    /// it means something is speaking the protocol by hand.
    /// </summary>
    RelayOnlyMessageFromClient = 8,

    /// <summary>
    /// A payload arrived from a connection that has asked to join but has not been admitted. It is
    /// dropped rather than forwarded: not admitted, not routed (R-1.3b).
    /// </summary>
    SenderNotAdmitted = 9,

    /// <summary>
    /// A message type this relay does not know. Under D-14 the wire format only grows, so this is
    /// an expected future message rather than an error, and it is ignored rather than refused.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="MalformedEnvelope"/> deliberately. Filing a newer client under
    /// "malformed" would tell whoever reads the forensic log that clients are sending garbage, at
    /// exactly the moment the truth is that the relay is behind — the opposite conclusion, drawn
    /// from the one artifact meant to prevent guessing (A-1.5a-r).
    /// </remarks>
    UnrecognisedMessageType = 10,

    /// <summary>The host admitted a joiner, which is the point it starts receiving traffic (R-1.3b).</summary>
    JoinerAdmitted = 11,

    /// <summary>
    /// The host refused a joiner or let the window lapse. It is told which, and its connection is
    /// closed — a refused player must not sit holding a live socket that never does anything.
    /// </summary>
    JoinerRejected = 12,

    /// <summary>
    /// An admission decision arrived from a connection that does not host the session. Only the DM
    /// decides who is at the table (D-3), so it is refused rather than obeyed.
    /// </summary>
    AdmissionFromNonHost = 13,

    /// <summary>An admission decision named a joiner that is not waiting on one.</summary>
    UnknownJoiner = 14,

    /// <summary>
    /// The host's public key was carried to a joiner still waiting on a decision (R-1.3a-i), and
    /// the gate was left exactly as it was.
    /// </summary>
    PendingNoticeForwarded = 15,
}

/// <summary>
/// One routing decision. Produced by <see cref="RelayRouter"/> from a parsed envelope, applied by
/// the transport, and recorded by the forensic log.
/// </summary>
/// <remarks>
/// The decision is a value rather than a set of calls onto a socket, which is what lets the routing
/// rules be tested without a network and lets the transport be replaced without touching them.
/// </remarks>
/// <param name="Action">What to do.</param>
/// <param name="Outcome">Why, in log vocabulary.</param>
/// <param name="Reply">The envelope to send back, when <paramref name="Action"/> is a reply.</param>
/// <param name="Recipients">Connections to forward to, when <paramref name="Action"/> forwards.</param>
/// <param name="CloseRecipients">
/// Whether to close the recipients once the message reaches them. Only a rejection does this:
/// R-1.3b requires a refused player not to be left holding a socket that never does anything.
/// </param>
public readonly record struct RelayDecision(
    RelayAction Action,
    RelayOutcome Outcome,
    WireEnvelope? Reply,
    IReadOnlyList<string> Recipients,
    bool CloseRecipients = false)
{
    /// <summary>Discard the message, recording <paramref name="outcome"/>.</summary>
    public static RelayDecision Drop(RelayOutcome outcome) => new(RelayAction.Drop, outcome, null, []);

    /// <summary>Answer the sender with <paramref name="reply"/>.</summary>
    public static RelayDecision Respond(RelayOutcome outcome, WireEnvelope reply) =>
        new(RelayAction.ReplyToSender, outcome, reply, []);

    /// <summary>Pass the message on to <paramref name="recipients"/>.</summary>
    public static RelayDecision Forward(
        RelayOutcome outcome,
        IReadOnlyList<string> recipients,
        bool closeAfterwards = false) =>
        new(RelayAction.Forward, outcome, null, recipients, closeAfterwards);

    /// <summary>The reason string the forensic log records.</summary>
    public string Reason => Outcome.ToString();
}
