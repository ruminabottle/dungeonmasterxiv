using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// <see cref="RelayLink"/> directly: whether a connection should exist, how a socket-thread failure
/// reaches the tick that applies it, and that teardown actually lets go.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because the coverage this type had was incidental.</b> It was covered only through
/// <see cref="SessionCoordinator"/>, which is true today and is kept true by nothing — the next
/// person to edit the file the socket now lives in gets no signal from a test that was never aiming
/// at them. "A fair follow-up" was an intention, and intentions are not coverage.
/// </para>
/// <para>
/// <b>Nothing here asserts a failure MESSAGE, deliberately.</b> Only the
/// <see cref="SessionFailure"/> value. The sentences are owned by
/// <c>SessionFailureMessageTests</c>, and BUG-37 is rewriting them: a message assertion added here
/// would pin the text that is being replaced, so it would go red when somebody does the right thing
/// and look exactly like a caught regression at the moment it is least true. A test that defends the
/// old behaviour is worse than no test, because it argues against the fix with a straight face.
/// </para>
/// </remarks>
public class RelayLinkTests
{
    private const string Usable = "wss://relay.example.org/session";

    // Fails if: wanting a connection does not open one, which is the state BUG-36 produced one layer
    // up — a session that believes it is connecting and a socket nobody opened.
    [Fact]
    public void WantingAConnectionOpensOne()
    {
        var (link, transport) = Link();

        var failure = link.Synchronise(wanted: true);

        Assert.Equal(SessionFailure.None, failure);
        Assert.True(transport.IsConnected);
        Assert.Equal(Usable, transport.LastRelay!.ToString());
    }

    // R-1.1: the connection does not outlive the session that needs it. Fails if: nothing releases
    // the socket, so a plugin sitting idle holds one open.
    [Fact]
    public void NoLongerWantingOneClosesIt()
    {
        var (link, transport) = Link();
        link.Synchronise(wanted: true);

        link.Synchronise(wanted: false);

        Assert.False(transport.IsConnected);
    }

    // Fails if: a second call dials again while the first connection is live. IsConnected is true
    // while a connect is still IN FLIGHT, so re-dialling here would abort the socket that is opening
    // — which is a real failure mode this project has already hit once.
    [Fact]
    public void AskingTwiceDoesNotDialTwice()
    {
        var (link, transport) = Link();

        link.Synchronise(wanted: true);
        link.Synchronise(wanted: true);

        Assert.Equal(1, transport.ConnectCount);
    }

    // Fails if: not wanting a connection we do not have does something anyway.
    [Fact]
    public void NotWantingOneWhenThereIsNoneIsQuiet()
    {
        var (link, transport) = Link();

        var failure = link.Synchronise(wanted: false);

        Assert.Equal(SessionFailure.None, failure);
        Assert.Equal(0, transport.ConnectCount);
        Assert.Equal(0, transport.DisconnectCount);
    }

    // The malformed-address path. ASSERTS THE VALUE AND NOT THE SENTENCE: BUG-37 is rewriting the
    // wording behind this failure, and pinning it here would defend the text being replaced.
    // Fails if: an unusable address dials anyway, or reports success and leaves the caller waiting.
    [Theory]
    [InlineData("not-a-relay")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://relay.example.org/session")]
    public void AnAddressThatIsNotAUsableRelayFailsWithoutDialling(string configured)
    {
        var (link, transport) = Link(() => configured);

        var failure = link.Synchronise(wanted: true);

        // RelayAddressUnreadable, not RelayUnreachable (BUG-37). Nothing was dialled, so nothing was
        // contacted and this build has learned nothing about the relay — and ConnectCount == 0 on the
        // next line is the proof of exactly that, which is why the two assertions belong together.
        Assert.Equal(SessionFailure.RelayAddressUnreadable, failure);
        Assert.Equal(0, transport.ConnectCount);
        Assert.False(transport.IsConnected);
    }

    // The seam that made the split worth doing: the link REPORTS a failure, it does not apply one.
    // Fails if: it reaches into session state, which would put "what a session does about a broken
    // socket" inside the type that owns the socket.
    [Fact]
    public void TheAddressIsReadAtDialTimeRatherThanAtConstruction()
    {
        var configured = "not-a-relay";
        var (link, transport) = Link(() => configured);

        Assert.Equal(SessionFailure.RelayAddressUnreadable, link.Synchronise(wanted: true));

        // R-1.8: changing the relay in settings takes effect on the next session, without a reload.
        configured = Usable;

        Assert.Equal(SessionFailure.None, link.Synchronise(wanted: true));
        Assert.True(transport.IsConnected);
    }

    // Fails if: a failure raised by the transport is applied immediately instead of being held for
    // the caller's tick — mutating session state from a socket callback races the draw.
    [Fact]
    public void AReportedFailureIsHeldUntilItIsAskedFor()
    {
        var (link, transport) = Link();

        transport.RaiseFailure(SessionFailure.ConnectionLost);

        Assert.True(link.TryTakeReportedFailure(out var failure));
        Assert.Equal(SessionFailure.ConnectionLost, failure);
    }

    // Fails if: taking a failure does not clear it, so one dropped connection is reported on every
    // subsequent frame and the session can never recover.
    [Fact]
    public void AFailureIsReportedOnceAndThenForgotten()
    {
        var (link, transport) = Link();
        transport.RaiseFailure(SessionFailure.ConnectionLost);
        link.TryTakeReportedFailure(out _);

        Assert.False(link.TryTakeReportedFailure(out var second));
        Assert.Equal(SessionFailure.None, second);
    }

    // Fails if: "nothing went wrong" is reported as a failure whose value happens to be None, which
    // would have the caller apply a failure every tick.
    [Fact]
    public void NothingGoingWrongIsNotAFailure()
    {
        var (link, _) = Link();

        Assert.False(link.TryTakeReportedFailure(out var failure));
        Assert.Equal(SessionFailure.None, failure);
    }

    // Fails if: a later failure is dropped because an earlier one is still sitting there. The most
    // recent state of the socket is the one worth acting on.
    [Fact]
    public void TheLatestFailureIsTheOneReported()
    {
        var (link, transport) = Link();

        transport.RaiseFailure(SessionFailure.ConnectionLost);
        transport.RaiseFailure(SessionFailure.RelayUnreachable);

        Assert.True(link.TryTakeReportedFailure(out var failure));
        Assert.Equal(SessionFailure.RelayUnreachable, failure);
    }

    // THIS DOES NOT TEST THE LOCK, and I measured that rather than assuming it either way.
    //
    // I first wrote this as a lock test, claiming it "fails reliably on an unsynchronised
    // implementation". That was false. Removing BOTH locks from RelayLink leaves this green, three
    // runs out of three: SessionFailure is an enum, so its read and write are already atomic, there
    // is nothing to tear, and a lost update is indistinguishable from ordinary interleaving. The
    // claim was the confident kind that never gets checked because it sounds like diligence.
    //
    // What it DOES establish: the two entry points can be driven concurrently without throwing, and
    // every value that comes out is one that went in — which would catch an implementation that
    // buffered into a plain collection, or one that composed a value rather than passing it through.
    // That is worth having and is not what the lock is for.
    //
    // The lock's actual property — that a socket-thread callback cannot interleave with a drain — is
    // not observable through this type's public surface. Reported as a finding rather than papered
    // over with a test that would imply coverage it does not have.
    [Fact]
    public async Task ConcurrentFailureTrafficNeitherThrowsNorInventsValues()
    {
        var (link, transport) = Link();
        var seen = new List<SessionFailure>();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var raising = Task.Run(() =>
        {
            for (var i = 0; i < 5_000 && !stop.IsCancellationRequested; i++)
            {
                transport.RaiseFailure(SessionFailure.ConnectionLost);
            }
        });

        var draining = Task.Run(() =>
        {
            while (!raising.IsCompleted && !stop.IsCancellationRequested)
            {
                if (link.TryTakeReportedFailure(out var failure))
                {
                    seen.Add(failure);
                }
            }
        });

        await Task.WhenAll(raising, draining);
        link.TryTakeReportedFailure(out var last);

        // Every value that came out is one that went in. A torn read would surface something else.
        Assert.All(seen, f => Assert.Equal(SessionFailure.ConnectionLost, f));
        Assert.True(last is SessionFailure.None or SessionFailure.ConnectionLost);
    }

    // Fails if: teardown leaves the link subscribed, so a disposed session keeps reacting to a socket
    // it no longer owns — the shape that leaks a plugin reload into the next one.
    [Fact]
    public void DetachingStopsFailuresArriving()
    {
        var (link, transport) = Link();

        link.Detach();
        transport.RaiseFailure(SessionFailure.ConnectionLost);

        Assert.False(link.TryTakeReportedFailure(out _));
    }

    // The other half of the same subscription, and it is the half a Detach written from memory
    // forgets. Fails if: frames keep arriving at a sink the session has finished with.
    [Fact]
    public void DetachingStopsFramesArriving()
    {
        var frames = new List<byte[]>();
        var transport = new FakeTransport();
        var link = new RelayLink(transport, () => Usable, frames.Add);

        transport.DeliverRaw([1, 2, 3]);
        Assert.Single(frames);

        link.Detach();
        transport.DeliverRaw([4, 5, 6]);

        Assert.Single(frames);
    }

    // Fails if: the link forwards a send the socket cannot carry. Send discards a frame that arrives
    // before the socket opens, silently, which is how BUG-36 stayed invisible.
    [Fact]
    public void ReadinessIsReportedFromTheSocketRatherThanFromBeingConnected()
    {
        var (link, transport) = Link();
        link.Synchronise(wanted: true);
        transport.OpenTheSocket = false;

        Assert.True(transport.IsConnected);
        Assert.False(link.IsReadyToSend);
    }

    private static (RelayLink Link, FakeTransport Transport) Link(Func<string>? address = null)
    {
        var transport = new FakeTransport();
        return (new RelayLink(transport, address ?? (() => Usable), _ => { }), transport);
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public int ConnectCount { get; private set; }

        public int DisconnectCount { get; private set; }

        public Uri? LastRelay { get; private set; }

        public bool OpenTheSocket { get; set; } = true;

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected && OpenTheSocket;

        public List<byte[]> Sent { get; } = new();

        public void Connect(Uri relay)
        {
            ConnectCount++;
            LastRelay = relay;
            IsConnected = true;
        }

        public void Disconnect()
        {
            DisconnectCount++;
            IsConnected = false;
        }

        public void Send(byte[] envelope) => Sent.Add(envelope);

        public void DeliverRaw(byte[] frame) => Received?.Invoke(frame);

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
