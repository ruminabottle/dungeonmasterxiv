using System;

namespace DungeonMasterXIV.Net;

/// <summary>A join request awaiting the host's answer, with its deadline, relink claim and name.</summary>
public sealed class PendingAdmission
{
    public PendingAdmission(
        PeerCode peerCode,
        AdmissionDeadline deadline,
        RelinkClaim relink = default,
        byte[]? joinerPublicKey = null,
        DisplayName displayName = default,
        bool supportsAudiences = false)
    {
        PeerCode = peerCode;
        Deadline = deadline;
        Relink = relink;
        JoinerPublicKey = joinerPublicKey;
        DisplayName = displayName;
        SupportsAudiences = supportsAudiences;
    }

    public DisplayName DisplayName { get; }

    public PeerCode PeerCode { get; }

    public AdmissionDeadline Deadline { get; }

    public RelinkClaim Relink { get; }

    public bool IsRelink => Relink.Matched;

    public string? RelinkLabel => Relink.Label;

    public byte[]? JoinerPublicKey { get; }

    public bool SupportsAudiences { get; }

    public TimeSpan RemainingAt(DateTimeOffset now) => Deadline.RemainingAt(now);

    public bool HasLapsedAt(DateTimeOffset now) => Deadline.HasLapsedAt(now);
}
