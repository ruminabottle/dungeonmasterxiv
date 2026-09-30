using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Tests;

/// <summary>A hosted session with admitted members and a transport that keeps what the host sent.</summary>
internal static class BaseChatFixture
{
    internal static readonly DateTimeOffset Now = new(2026, 8, 30, 15, 0, 0, TimeSpan.Zero);
    internal const string Speaker = "PRBCD2";
    internal const string Listener = "JNKBCD";

    /// <summary>Every stamped line the host sent that <paramref name="member"/> can open; others are skipped.</summary>
    internal static List<StreamLine> StampedLinesFor(
        SessionKeyExchange member, SessionCoordinator host, CapturingTransport transport)
    {
        var code = host.Host.Code!.Value;
        var key = member.DeriveSharedKey(host.HostKeys!.PublicKey, code);
        var associatedData = WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload);
        var lines = new List<StreamLine>();

        foreach (var sent in transport.Sent)
        {
            if (!EnvelopeCodec.TryDecode(sent, out var envelope)
                || envelope!.TryGetSealedPayload() is not { } payload)
            {
                continue;
            }

            byte[] opened;
            try
            {
                opened = SessionCipher.Open(key, payload, associatedData);
            }
            catch (CryptographicException)
            {
                continue;   // sealed for somebody else
            }

            if (SessionContentCodec.TryDecode(opened, out var content) && content!.Entries is { } entries)
            {
                lines.AddRange(entries);
            }
        }

        return lines;
    }

    internal static SessionCoordinator Hosting(out CapturingTransport transport)
    {
        transport = new CapturingTransport();
        var host = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance, capabilities: SessionCapabilities.Default);

        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();
        return host;
    }

    internal static PeerCode Admitted(SessionCoordinator host, string code, SessionKeyExchange keys)
    {
        var peerCode = PeerCodes.Of(code);
        host.ReceiveJoinRequest(peerCode, keys.PublicKey, Now);
        host.Admit(peerCode);
        return peerCode;
    }

    internal static WireEnvelope SealedBy(SessionKeyExchange member, SessionCoordinator host, SessionContent content)
    {
        var code = host.Host.Code!.Value;
        var sealedPayload = SessionCipher.Seal(
            member.DeriveSharedKey(host.HostKeys!.PublicKey, code),
            SessionContentCodec.Encode(content),
            WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        return WireEnvelope.ForSessionPayload(code, sealedPayload);
    }

    internal sealed class CapturingTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));
    }
}
