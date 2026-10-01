using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

public sealed class AdmissionControl
{
    private readonly AdmissionAnnouncer _announcer;
    private readonly Func<DisplayName, Guid?> _mintParticipant;
    private readonly ISessionTransportLog _log;
    private readonly Func<SessionCode?> _hostCode;
    private readonly Func<SessionKeyExchange?> _hostKeys;

    public AdmissionControl(
        AdmissionAnnouncer announcer,
        Func<SessionCode?> hostCode,
        Func<SessionKeyExchange?> hostKeys,
        Func<DisplayName, Guid?> mintParticipant,
        ISessionTransportLog log)
    {
        ArgumentNullException.ThrowIfNull(mintParticipant);
        ArgumentNullException.ThrowIfNull(log);

        _announcer = announcer;
        _hostCode = hostCode;
        _hostKeys = hostKeys;
        _mintParticipant = mintParticipant;
        _log = log;
    }

    public SessionAudience Audience { get; } = new();

    public AdmissionDesk Desk { get; } = new();

    public IReadOnlyList<PendingAdmission> JustLapsed { get; private set; } = Array.Empty<PendingAdmission>();

    public void Receive(PendingAdmission request) => Desk.Receive(request);

    public void AdmitToTheQueue(
        byte[] joinerPublicKey,
        DateTimeOffset now,
        DisplayName displayName = default,
        RelinkClaim relink = default) =>
        Receive(PeerCodeFor(joinerPublicKey), joinerPublicKey, now, relink, displayName);

    public PeerCode PeerCodeFor(byte[] joinerPublicKey)
    {
        var scope = System.Text.Encoding.UTF8.GetBytes(_hostCode()?.Value ?? string.Empty);
        var digest = System.Security.Cryptography.SHA256.HashData([.. scope, .. joinerPublicKey]);
        var value = new System.Numerics.BigInteger(digest, isUnsigned: true, isBigEndian: true);

        var rendered = new char[SessionCode.Length];
        for (var i = rendered.Length - 1; i >= 0; i--)
        {
            value = System.Numerics.BigInteger.DivRem(value, SpeakableAlphabet.Length, out var symbol);
            rendered[i] = SpeakableAlphabet.Characters[(int)symbol];
        }

        return PeerCode.FromGenerated(new string(rendered));
    }

    public PendingAdmission? Receive(
        PeerCode peerCode,
        byte[] joinerPublicKey,
        DateTimeOffset now,
        RelinkClaim relink = default,
        DisplayName displayName = default)
    {
        if (_hostKeys() is not { } hostKeys)
        {
            return null;
        }

        var deadline = AdmissionDeadline.DecidedByHost(now);
        var request = new PendingAdmission(
            peerCode,
            KeyFingerprint.Of(joinerPublicKey, hostKeys.PublicKey),
            deadline,
            relink,
            joinerPublicKey,
            displayName);

        Desk.Receive(request);

        if (_hostCode() is { } hostedCode)
        {
            _announcer.Pending(hostedCode, joinerPublicKey, hostKeys.PublicKey, deadline);
        }

        return request;
    }

    public MemberDrops Drops { get; } = new();

    public bool RecordDrop(byte[] memberPublicKey, DateTimeOffset when)
    {
        ArgumentNullException.ThrowIfNull(memberPublicKey);

        var peerCode = PeerCodeFor(memberPublicKey);
        if (!Audience.IsAdmitted(peerCode))
        {
            return false;
        }

        Drops.Record(peerCode, when);
        return true;
    }

    public AdmittedPeer Admit(PeerCode peerCode, SessionRole role = SessionRole.Player)
    {
        var request = Desk.Decide(peerCode);
        var peer = Audience.Admit(
            peerCode,
            role,
            request?.Verification ?? AdmissionVerification.NotCompared,
            request?.JoinerPublicKey,
            request?.DisplayName ?? DisplayName.None);

        Drops.Forget(peerCode);

        var participantId = _mintParticipant(peer.DisplayName);

        if (_hostCode() is { } code && _hostKeys() is { } hostKeys && request?.JoinerPublicKey is { } joinerKey)
        {
            _announcer.Accepted(code, joinerKey, hostKeys.PublicKey, participantId);
        }

        if (participantId is null)
        {
            _log.Warning(
                $"Admitted {peerCode} without creating a participant, so this session has no "
                + "campaign to record them in. They will not be able to relink and will be a new "
                + "request next time.");
        }

        return peer;
    }

    public void RecordComparabilityReceipt(byte[] joinerPublicKey) =>
        Desk.Find(PeerCodeFor(joinerPublicKey))?.JoinerReportedItCanCompare();

    public bool Departed(PeerCode peerCode) => Audience.Remove(peerCode);

    public void Deny(PeerCode peerCode)
    {
        var request = Desk.Decide(peerCode);
        Audience.Remove(peerCode);

        if (_hostCode() is { } code && request?.JoinerPublicKey is { } joinerKey)
        {
            _announcer.Denied(code, joinerKey);
        }
    }

    private void AnnounceLapsed()
    {
        if (_hostCode() is { } code)
        {
            _announcer.Lapsed(code, JustLapsed);
        }
    }
    public void ExpireLapsed(DateTimeOffset now)
    {
        JustLapsed = Desk.ExpireLapsed(now);
        AnnounceLapsed();
    }

    public void Clear()
    {
        Audience.Clear();
        Desk.Clear();
        Drops.Clear();
        JustLapsed = Array.Empty<PendingAdmission>();
    }
}
