using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>Ending a session sends an admitted participant a closing notice they can open.</summary>
public class EndingASessionAnnouncesItTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 2, 0, 0, TimeSpan.Zero);

    /// <summary>The tail of the alphabet, so a peer code can never equal the session code.</summary>
    private static readonly string PeerCode = SpeakableAlphabet.Characters[^SessionCode.Length..];

    [Fact]
    public void EndingTheSessionTellsAnAdmittedParticipant()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance, capabilities: SessionCapabilities.Default);
        coordinator.StartHosting();
        coordinator.Host.Registered();
        coordinator.SynchroniseTransport();

        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest(
            PeerCodes.Of(PeerCode), joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));
        coordinator.Admit(PeerCodes.Of(PeerCode));

        // Derived before StopHosting, which disposes the host's key pair.
        var key = joiner.DeriveSharedKey(coordinator.HostKeys!.PublicKey, coordinator.Host.Code!.Value);
        transport.Sent.Clear();

        coordinator.StopHosting(Now);

        var closing = SessionClosing.TryFromWire(ClosingTicksOpenedWith(key, transport))!.Value;
        Assert.Equal(SessionClosing.DecidedByHost(Now), closing);
        Assert.Equal(SessionClosing.Window, closing.RemainingAt(Now));
    }

    private static long ClosingTicksOpenedWith(byte[] key, FakeTransport transport)
    {
        foreach (var sent in transport.Sent)
        {
            if (!EnvelopeCodec.TryDecode(sent, out var envelope) || envelope!.TryGetSealedPayload() is not { } sealedPayload)
            {
                continue;
            }

            byte[] plaintext;
            try
            {
                plaintext = SessionCipher.Open(key, sealedPayload, envelope!.AssociatedData());
            }
            catch (CryptographicException)
            {
                continue;   // sealed for somebody else
            }

            Assert.True(SessionContentCodec.TryDecode(plaintext, out var content));
            Assert.NotNull(content!.ClosingAtUtcTicks);
            return content.ClosingAtUtcTicks!.Value;
        }

        throw new InvalidOperationException("No sealed payload this participant could open: the session ended in silence.");
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);
    }
}
