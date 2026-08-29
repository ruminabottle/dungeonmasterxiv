using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The fixture <c>EveryMessageAClientSendsIsSentTests</c> and its siblings drive: a coordinator
/// wired to a transport that records what was actually put on the wire.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own file because the limit forced a cut and this was the honest place for it.</b> The test
/// class was 408 lines against a 400 block BEFORE DMXENG-40 -- already breaching, invisibly, until
/// the sizes tool learned to measure classes. A test double is not an assertion, so the seam between
/// them is real rather than convenient.
/// </para>
/// <para>
/// <b>The transport is deliberately not a naive double.</b> It discards a frame sent before the
/// socket opens, exactly as the real one does -- which is the behaviour BUG-36 hid behind, and a
/// double that accepted everything would have made that bug untestable.
/// </para>
/// </remarks>
internal sealed class ClientSendHarness
{
    // The one instant every trigger uses. Lives here with the harness rather than in a test class,
    // because it is a property of the fixture and two files now drive it.
    internal static readonly DateTimeOffset Now = new(2026, 8, 28, 2, 0, 0, TimeSpan.Zero);

    internal sealed class Session
    {
        private readonly RecordingTransport _transport = new();

        public Session() =>
            Coordinator = new SessionCoordinator(_transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance, capabilities: SessionCapabilities.Default);

        public SessionCoordinator Coordinator { get; }

        public byte[] JoinerKey { get; } = new SessionKeyExchange().PublicKey;

        public IReadOnlyList<WireEnvelope> Sent => _transport.Sent
            .Select(bytes => EnvelopeCodec.TryDecode(bytes, out var e) ? e : null)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        /// <summary>The socket finished opening. Sending before this is discarded (BUG-36).</summary>
        public void Ready() => _transport.OpenTheSocket = true;

        /// <summary>The host answers with its key, so this client can render a fingerprint.</summary>
        public void HostKeyArrives() =>
            _transport.Deliver(WireEnvelope.ForJoinPending(
                SessionCode.FromValid("BCDFGH"),
                Coordinator.Membership.Keys!.PublicKey,
                new SessionKeyExchange().PublicKey,
                AdmissionDeadline.DecidedByHost(Now)));

        /// <summary>A host with a code the relay has confirmed.</summary>
        public void Hosting()
        {
            Coordinator.StartHosting();
            Ready();
            Coordinator.Host.Registered();
        }
    }

    internal sealed class RecordingTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public List<byte[]> Sent { get; } = new();

        public bool OpenTheSocket { get; set; }

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected && OpenTheSocket;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
            // Mirrors the real transport, which discards a frame sent before the socket opens.
            if (IsReadyToSend)
            {
                Sent.Add(envelope);
            }
        }

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }}
