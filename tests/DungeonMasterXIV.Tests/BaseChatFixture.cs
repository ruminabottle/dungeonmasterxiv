using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The two-member session the base-chat fixtures are built on: a host, an admitted speaker, an
/// admitted listener, and a transport that keeps what the host sent.
/// </summary>
/// <remarks>
/// <para>
/// <b>SHARED SO THE SPLIT COSTS NO ASSERTIONS.</b> DMXENG-133 separates A-2.34 (the message reaches
/// a different member) from A-2.35 (the bound refuses rather than truncates), and <b>both need a
/// real session</b>: A-2.35's host arm asserts that an over-long arrival is not rebroadcast, which
/// can only be said about a host with somebody to rebroadcast to. Copying these helpers into two
/// files would have been two copies free to drift, on the exact fixture whose correctness both
/// criteria rest on.
/// </para>
/// <para>
/// <b>Consumed with <c>using static</c>, which is what keeps the split a MOVE.</b> Every test body
/// in both files is byte-identical to what it was in the single file, so the diff shows relocation
/// and nothing else — there is nowhere for a weakened assertion to hide in it.
/// </para>
/// </remarks>
internal static class BaseChatFixture
{
    internal static readonly DateTimeOffset Now = new(2026, 8, 30, 15, 0, 0, TimeSpan.Zero);
    internal const string Speaker = "PRBCD2";
    internal const string Listener = "JNKBCD";

    /// <summary>
    /// Every stamped line the host sent that <paramref name="member"/> can actually open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE POPULATION IS "ENTRIES THIS MEMBER CAN DECRYPT", NOT "ENVELOPES SENT".</b> Admitting a
    /// participant publishes a roster, so the transport is never empty and an assertion over
    /// everything sent would pass or fail on traffic that has nothing to do with chat. That is the
    /// intake error this helper exists to prevent, and the over-long test below is exactly where it
    /// would have produced a confident wrong answer.
    /// </para>
    /// <para>
    /// <b>A wrong key THROWS rather than returning null</b> — <c>AesGcm.Decrypt</c> raises an
    /// authentication-tag mismatch — so envelopes sealed for somebody else are skipped by catching,
    /// not by a null check. Written after the first version of this helper crashed on the roster
    /// broadcast sealed for the other member.
    /// </para>
    /// </remarks>
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
                continue;
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
            transport,
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            log: new SilentLog(),
            capabilities: SessionCapabilities.Default);

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

    internal static WireEnvelope SealedBy(
        SessionKeyExchange member, SessionCoordinator host, SessionContent content)
    {
        var code = host.Host.Code!.Value;
        var sealedPayload = SessionCipher.Seal(
            member.DeriveSharedKey(host.HostKeys!.PublicKey, code),
            SessionContentCodec.Encode(content),
            WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        return WireEnvelope.ForSessionPayload(code, sealedPayload);
    }

    internal sealed class SilentLog : ISessionTransportLog
    {
        public void Information(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Warning(Exception exception, string message)
        {
        }
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
