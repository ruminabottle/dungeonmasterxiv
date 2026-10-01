using System;

namespace DungeonMasterXIV.Net;

/// <summary>A join request awaiting the host's answer, with its fingerprint, deadline, relink claim and name.</summary>
public sealed class PendingAdmission
{
    public PendingAdmission(
        PeerCode peerCode,
        string fingerprint,
        AdmissionDeadline deadline,
        RelinkClaim relink = default,
        byte[]? joinerPublicKey = null,
        DisplayName displayName = default)
    {
        PeerCode = peerCode;
        Fingerprint = fingerprint;
        Deadline = deadline;
        Relink = relink;
        JoinerPublicKey = joinerPublicKey;
        DisplayName = displayName;
    }

    public DisplayName DisplayName { get; }

    public PeerCode PeerCode { get; }

    public string Fingerprint { get; }

    public AdmissionDeadline Deadline { get; }

    public RelinkClaim Relink { get; }

    public bool IsRelink => Relink.Matched;

    public string? RelinkLabel => Relink.Label;

    public byte[]? JoinerPublicKey { get; }

    public bool FingerprintConfirmed { get; private set; }

    public ComparabilityEvidence Comparability { get; private set; }

    public void JoinerReportedItCanCompare() =>
        Comparability = ComparabilityEvidence.EstablishedCapable;

    public AdmissionVerification Verification =>
        FingerprintConfirmed ? AdmissionVerification.Confirmed : AdmissionVerification.NotCompared;

    public void ConfirmFingerprintMatched()
    {
        if (Comparability == ComparabilityEvidence.EstablishedIncapable)
        {
            return;
        }

        FingerprintConfirmed = true;
    }

    public TimeSpan RemainingAt(DateTimeOffset now) => Deadline.RemainingAt(now);

    public bool HasLapsedAt(DateTimeOffset now) => Deadline.HasLapsedAt(now);
}
