namespace DungeonMasterXIV.Net;

/// <summary>The kinds of message exchanged through the relay.</summary>
public enum WireMessageType
{
    Unknown = 0,

    CodeRequest = 1,

    CodeAccepted = 2,

    CodeRefused = 3,

    JoinRequest = 4,

    SessionPayload = 5,

    JoinAccepted = 6,

    JoinDenied = 7,

    JoinLapsed = 8,

    JoinPending = 9,

    ConnectionDropped = 11,

    JoinHello = 12,

    HostKey = 13,
}
