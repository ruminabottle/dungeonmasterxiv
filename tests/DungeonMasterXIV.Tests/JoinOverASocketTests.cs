using System;
using System.Linq;
using System.Threading.Tasks;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// C5's merge bar, transferred here: one completing join and one completing denial <b>over a real
/// socket</b>, rather than asserted inside <see cref="SessionCoordinator"/> against a fake.
/// </summary>
/// <remarks>
/// Everything before this proved the two halves of the admission flow in isolation. What none of it
/// proved is that a decision leaves one machine as bytes and arrives at another as a decision — the
/// step where a framing disagreement, a serialisation gap or a transport that never reads would
/// show up and nothing else would catch it.
/// </remarks>
public class JoinOverASocketTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 7, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    // THE BAR, positive half. Fails if: anything between the host's decision and the joiner's state
    // is broken — the encoder, the frame, the receive loop, the decoder, or the dispatch. Each of
    // those is covered in isolation elsewhere; only this covers them composed.
    [Fact]
    public async Task AJoinCompletesAcrossARealSocket()
    {
        await using var server = new TestWebSocketServer();
        using var transport = new WebSocketSessionTransport(new SilentLog());
        var coordinator = new SessionCoordinator(transport, () => server.Address.ToString(), GraceWindow.Default);
        using var host = new SessionKeyExchange();

        // RequestJoin dials through SynchroniseTransport using the address factory above. Calling
        // Connect here as well opened a second socket and aborted the first, which is worth a note:
        // the transport is driven by the coordinator, not alongside it.
        coordinator.RequestJoin(Code);
        await server.Connected.WaitAsync(Patience);
        coordinator.Join.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));

        await server.SendAsync(EnvelopeCodec.Encode(
            WireEnvelope.ForJoinAccepted(Code, coordinator.JoinerKeys!.PublicKey, host.PublicKey)));

        await WaitForAsync(() =>
        {
            coordinator.Tick(TimeSpan.Zero, Now);
            return coordinator.Join.Phase == JoinPhase.Admitted;
        });

        Assert.Equal(JoinPhase.Admitted, coordinator.Join.Phase);
        Assert.Equal(
            host.DeriveSharedKey(coordinator.JoinerKeys!.PublicKey, Code),
            coordinator.SessionKey);
    }

    // THE BAR, negative half. Fails if: a denial does not survive the same path. Kept separate from
    // the join rather than folded into it — the two travel the same wire but a receiver that
    // special-cased acceptance would pass one and fail the other.
    [Fact]
    public async Task ADenialCompletesAcrossARealSocket()
    {
        await using var server = new TestWebSocketServer();
        using var transport = new WebSocketSessionTransport(new SilentLog());
        var coordinator = new SessionCoordinator(transport, () => server.Address.ToString(), GraceWindow.Default);

        // RequestJoin dials through SynchroniseTransport using the address factory above. Calling
        // Connect here as well opened a second socket and aborted the first, which is worth a note:
        // the transport is driven by the coordinator, not alongside it.
        coordinator.RequestJoin(Code);
        await server.Connected.WaitAsync(Patience);
        coordinator.Join.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));

        await server.SendAsync(EnvelopeCodec.Encode(
            WireEnvelope.ForJoinDenied(Code, coordinator.JoinerKeys!.PublicKey)));

        await WaitForAsync(() =>
        {
            coordinator.Tick(TimeSpan.Zero, Now);
            return coordinator.Join.Phase == JoinPhase.Denied;
        });

        Assert.Equal(JoinPhase.Denied, coordinator.Join.Phase);
        Assert.False(coordinator.Join.MayReceiveSessionState);
        Assert.Null(coordinator.SessionKey);
    }

    // The other direction over the same socket. Fails if: what the host sends never reaches the
    // wire — which is the half PR #19 could only assert against a fake.
    [Fact]
    public async Task WhatTheHostSendsArrivesAsAnEnvelope()
    {
        await using var server = new TestWebSocketServer();
        using var transport = new WebSocketSessionTransport(new SilentLog());
        var coordinator = new SessionCoordinator(transport, () => server.Address.ToString(), GraceWindow.Default);

        coordinator.StartHosting();
        coordinator.Host.Registered();
        await server.Connected.WaitAsync(Patience);

        // The server accepting is not the client being ready: ClientWebSocket.ConnectAsync returns
        // slightly later, and a Send before that is dropped rather than queued. Waiting on the
        // client's own readiness rather than sleeping, so this asserts the send path instead of
        // racing it.
        await WaitForAsync(() => transport.IsReadyToSend);

        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest("PEER-1", joiner.PublicKey, Now);
        coordinator.Admit("PEER-1");

        // The acceptance is no longer the FIRST thing the host sends: R-1.3a-i puts a JoinPending
        // carrying the host's key on the wire when the request is recorded, before the DM decides.
        // So this looks for the acceptance among what arrived rather than assuming it leads. The
        // subject of this test is the send path, not the order — the ordering is asserted in
        // TheJoinerCanCompareBeforeAdmissionTests, where it is the point rather than a side effect.
        //
        // TryTake with a bound rather than Take: a frame that never arrives should fail this test,
        // not hang the suite. An unbounded wait turns a broken send path into a stuck CI run, which
        // is a worse signal than a red one.
        WireEnvelope? acceptance = null;
        while (acceptance is null
               && server.Received.TryTake(out var frame, (int)Patience.TotalMilliseconds))
        {
            if (EnvelopeCodec.TryDecode(frame, out var envelope)
                && envelope!.Type == WireMessageType.JoinAccepted)
            {
                acceptance = envelope;
            }
        }

        Assert.True(acceptance is not null, "No acceptance reached the socket within the timeout.");
        Assert.Equal(coordinator.HostKeys!.PublicKey, acceptance!.HostPublicKey);
    }

    // The probe found this gap rather than the plan: truncating a frame by one byte failed both
    // inbound tests, but every payload here fits in one 8 KB read, so the reassembly loop that
    // stitches a frame across continuations was never exercised at all. A defect there would only
    // appear once a real session carried something large — a roster, an encounter — which is a long
    // way from here.
    //
    // Fails if: the receive loop stops reassembling and treats the first continuation as the whole
    // frame.
    [Fact]
    public async Task AFrameLargerThanOneReadIsReassembledRatherThanTruncated()
    {
        await using var server = new TestWebSocketServer();
        using var transport = new WebSocketSessionTransport(new SilentLog());
        var coordinator = new SessionCoordinator(transport, () => server.Address.ToString(), GraceWindow.Default);
        using var host = new SessionKeyExchange();

        coordinator.RequestJoin(Code);
        await server.Connected.WaitAsync(Patience);
        coordinator.Join.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));

        // Padded past the 8 KB receive buffer by an unknown field, which D-14 requires be ignored —
        // so the frame stays valid while forcing the multi-read path.
        var accepted = EnvelopeCodec.Encode(
            WireEnvelope.ForJoinAccepted(Code, coordinator.JoinerKeys!.PublicKey, host.PublicKey));
        var json = System.Text.Encoding.UTF8.GetString(accepted);
        var padded = json[..^1] + ",\"PaddingFromAFutureVersion\":\"" + new string('x', 20_000) + "\"}";
        Assert.True(padded.Length > 8192);

        await server.SendAsync(System.Text.Encoding.UTF8.GetBytes(padded));

        await WaitForAsync(() =>
        {
            coordinator.Tick(TimeSpan.Zero, Now);
            return coordinator.Join.Phase == JoinPhase.Admitted;
        });

        Assert.Equal(JoinPhase.Admitted, coordinator.Join.Phase);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Condition was never met within the timeout.");
    }

    private sealed class SilentLog : ISessionTransportLog
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
}
