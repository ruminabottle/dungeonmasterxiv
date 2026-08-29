using System;
using System.Threading.Tasks;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-122: <c>IsReadyToSend</c> answers the question it documents — whether a frame sent right now
/// would actually go out.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PROPERTY ASKED THE SOCKET AND THE SOCKET ANSWERED TOO EARLY.</b> It was
/// <c>_socket?.State == WebSocketState.Open</c>, and <c>ClientWebSocket.State</c> reports
/// <c>Open</c> BEFORE <c>ConnectAsync</c> has returned — at which point <c>SendAsync</c> still throws
/// <c>InvalidOperationException: The WebSocket is not connected</c>. Measured directly: of 300 sends
/// issued the instant <c>State</c> read <c>Open</c>, <b>191 threw</b>.
/// </para>
/// <para>
/// <b>It surfaced as a 2% suite flake, and that is why it went unfixed for so long.</b>
/// <c>JoinOverASocketTests.WhatTheHostSendsArrivesAsAnEnvelope</c> failed about one full-suite run in
/// fifty and never in isolation, because the key generation between the readiness check and the send
/// usually gives the connect time to land. The symptom looked like test infrastructure. It was not:
/// <c>Send</c> is documented to DROP a frame that arrives too early, and instead the exception left
/// <c>Send</c> and travelled out through <c>AdmissionAnnouncer</c> and
/// <c>SessionCoordinator.ReceiveJoinRequest</c> into the caller. A host that receives a join request
/// in the window just after connecting takes that exception in the product, not only in a test.
/// </para>
/// <para>
/// <b>WHY THESE LOOP.</b> A single round reproduced the defect roughly two thirds of the time, so one
/// round would be a coin toss rather than a test. Over <see cref="Rounds"/> rounds the chance of the
/// old code passing this file is about one in ten billion — which is what makes a flake into an
/// assertion. The loops are not a timeout in disguise: nothing here waits on a duration, and each
/// round asserts rather than samples.
/// </para>
/// </remarks>
public class TheTransportIsNotReadyBeforeItIsConnectedTests
{
    private const int Rounds = 25;
    private static readonly byte[] Frame = { 1, 2, 3, 4 };
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    // THE DEFECT. Fails if IsReadyToSend goes true before the connect completes: Send then reaches
    // ClientWebSocket.SendAsync, which throws InvalidOperationException straight through this test.
    //
    // THE READINESS IS ASSERTED, NOT ASSUMED. If the property were stuck false, the spin would simply
    // expire and the send would never happen -- and a test that never sends cannot fail the way this
    // bug fails. So the deadline is a failure, not a way out.
    [Fact]
    public async Task AFrameSentTheMomentTheTransportSaysItIsReadyGoesOut()
    {
        for (var round = 0; round < Rounds; round++)
        {
            await using var server = new TestWebSocketServer();
            using var transport = new WebSocketSessionTransport(new SilentLog());
            transport.Connect(server.Address);

            // THE SPIN IS TIGHT ON PURPOSE, AND IT IS THE WHOLE TEST. The property claims a frame
            // sent RIGHT NOW would go out, so "right now" is the only honest moment to send. An
            // `await Task.Delay(1)` here hands the connect exactly the slice it needs to finish, and
            // the defect disappears -- measured: with a 1ms delay this file passed 3/3 against the
            // UNFIXED property, which is a regression test that does not regress.
            var deadline = DateTime.UtcNow + Patience;
            while (!transport.IsReadyToSend && DateTime.UtcNow < deadline)
            {
            }

            // NOTHING RUNS BETWEEN THE CHECK AND THE SEND -- NOT EVEN AN ASSERTION MESSAGE. The
            // readiness is captured into a local and asserted AFTERWARDS, and the payload is a
            // pre-allocated static.
            //
            // This is not fastidiousness, it is the second thing that hid this bug from its own
            // test. An `Assert.True(ready, $"Round {round}: ...")` placed here builds its
            // interpolated string BEFORE the call, on every iteration and not only on failure, and
            // those few microseconds are enough for ConnectAsync to finish. Measured on the UNFIXED
            // property, same binary, same tight spin: this file passed 3/3 with the interpolation
            // present, while a probe differing only in that detail threw 129 times in 200.
            var reportedReady = transport.IsReadyToSend;

            transport.Send(Frame);

            Assert.True(reportedReady, "The transport never became ready, so nothing was tested.");

            // NOT THROWING IS NOT ENOUGH. Send is allowed to drop, so a version that swallowed the
            // failure would pass the line above while losing every frame -- the silent-loss shape
            // this codebase keeps finding. The frame has to arrive.
            Assert.True(
                server.Received.TryTake(out var frame, (int)Patience.TotalMilliseconds),
                $"Round {round}: the transport reported ready and the frame never reached the server.");
            Assert.Equal(Frame, frame);
        }
    }

    // THE OTHER ARM. A property hardcoded true would pass nothing above -- but one hardcoded FALSE
    // would be caught only by the readiness assertion, and this says plainly what the answer must be
    // before a connect is ever asked for.
    [Fact]
    public void ATransportThatHasNotConnectedIsNotReadyToSend()
    {
        using var transport = new WebSocketSessionTransport(new SilentLog());

        Assert.False(transport.IsReadyToSend);
    }

    // A RECONNECT IN FLIGHT IS NOT READY. Nothing else covers the moment a second Connect has
    // replaced the socket and its ConnectAsync has not yet returned.
    //
    // WHAT THIS DOES NOT HOLD, MEASURED RATHER THAN ASSUMED. It was proposed as a pin on the choice
    // to store the connected SOCKET rather than a bool. It is not one, and it does not pin the
    // BUG-122 fix either -- three variants, same four tests:
    //
    //     socket reference (shipped)   this passes
    //     plain bool                   this passes
    //     the PRE-FIX raw socket state this passes   <- only AFrameSent... reddens here
    //
    // The reason is that Connect() calls Disconnect() first, and Disconnect clears readiness under
    // BOTH designs; and under the pre-fix property a freshly constructed socket is not Open anyway.
    // So all three agree here by different routes. It is kept for what it DOES hold -- that a
    // reconnect in flight reports not-ready, which would redden if Connect ever stopped disconnecting
    // first -- and labelled, because an assertion that cannot fail for the reason it was added reads
    // as protection nobody is getting.
    [Fact]
    public async Task ATransportReconnectingIsNotReadyUntilTheNewConnectLands()
    {
        await using var first = new TestWebSocketServer();
        await using var second = new TestWebSocketServer();
        using var transport = new WebSocketSessionTransport(new SilentLog());
        transport.Connect(first.Address);

        var deadline = DateTime.UtcNow + Patience;
        while (!transport.IsReadyToSend && DateTime.UtcNow < deadline)
        {
        }

        Assert.True(transport.IsReadyToSend, "The first connect never landed, so the premise is untested.");

        transport.Connect(second.Address);

        Assert.False(transport.IsReadyToSend);
    }

    // AND READINESS DOES NOT SURVIVE THE CONNECTION. Disconnect clears the recorded socket, so a
    // reconnect cannot inherit the previous one's readiness -- the reason the fix holds a reference
    // rather than a bool.
    [Fact]
    public async Task ADisconnectedTransportIsNotReadyToSend()
    {
        await using var server = new TestWebSocketServer();
        using var transport = new WebSocketSessionTransport(new SilentLog());
        transport.Connect(server.Address);

        var deadline = DateTime.UtcNow + Patience;
        while (!transport.IsReadyToSend && DateTime.UtcNow < deadline)
        {
            await Task.Delay(1);
        }

        Assert.True(transport.IsReadyToSend, "The transport never became ready, so the premise is untested.");

        transport.Disconnect();

        Assert.False(transport.IsReadyToSend);
    }
}
