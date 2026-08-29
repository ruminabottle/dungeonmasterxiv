using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-126: the containment check's network call is bounded, so an origin that is reached but never
/// answers ends in a skip rather than in a suite that never returns.
/// </summary>
/// <remarks>
/// <para>
/// <b>UNRESPONSIVE, NOT REFUSED — and the distinction is the entire bug.</b> A refused connection
/// returns at once because the host sends RST, and BUG-124's arm 4 already handles it. A DROPPED
/// connection sends nothing at all, so nothing below the wait ever ends it. A test that pointed at a
/// closed port would pass against the unfixed code, because that path was never broken.
/// </para>
/// <para>
/// <b>THE HANG IS IN THE READ, NOT IN THE WAIT.</b> <c>ReadToEnd()</c> runs before
/// <c>WaitForExit()</c> and returns only at end of stream, so a bound on <c>WaitForExit</c> alone
/// would look like a fix and change nothing — control never reaches it. Measured against this
/// fixture: still blocked after 6s, while <c>WaitForExit(1s)</c> returned false.
/// </para>
/// <para>
/// <b>Every timing claim here is paired with a control, because a fast return proves nothing on its
/// own</b> — it is equally what a probe that never ran produces. That is not hypothetical: the first
/// run of this reproduction reported 0s for both arms because <c>timeout</c> does not exist on this
/// platform, and exit 127 read as a fast success.
/// </para>
/// </remarks>
public class AnUnresponsiveOriginCannotHangTheGateTests
{
    /// <summary>Long enough that a slow machine cannot fail it, short enough to be a test.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(2);

    /// <summary>How long the assertion waits before calling the call unbounded.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    // THE DETECTOR. Without the bound this does not return at all, so the assertion is on COMPLETION
    // rather than on a duration -- a hanging test fails its suite by wedging it, which is the very
    // failure being fixed. Running it off-thread converts that hang into an ordinary red.
    [Fact]
    public async Task AnOriginThatNeverAnswersEndsTheWaitInsteadOfRunningForever()
    {
        using var sink = new UnresponsiveOrigin();

        var call = Task.Run(() => ContainsMainFactAttribute.Git(
            $"ls-remote git://127.0.0.1:{sink.Port}/unresponsive.git", Bound));

        // Raced against a timer rather than blocked on, so an unbounded call fails this test instead
        // of wedging the runner -- and without a blocking wait, which xUnit1031 rightly refuses.
        var finished = await Task.WhenAny(call, Task.Delay(Patience));

        Assert.True(
            ReferenceEquals(finished, call),
            $"The call had not returned after {Patience.TotalSeconds:F0}s against an origin that "
            + "accepts connections and never answers. That is the defect: the gate waits forever.");

        var result = await call;

        Assert.True(
            result.TimedOut,
            "The call returned but did not report that it timed out. git exited on its own with "
            + $"code {result.Code}, stdout <{result.Output.Trim()}>, stderr <{result.Errors.Trim()}>.");
    }

    // THE CONTROL, and the half that makes the detector mean anything. A bounded call must still let
    // a working origin answer -- otherwise "it returned" would be satisfied by a call that refuses
    // everything, and the fix would be a suite that skips permanently.
    //
    // The non-empty output is the load-bearing assertion, not the timing: it is positive evidence
    // that git RAN and produced a real answer through this same helper. A duration alone cannot tell
    // a working probe from one that never started.
    [Fact]
    public void AReachableOriginStillAnswersOnItsOwnAndIsNotTimedOut()
    {
        var repository = TheBuild.RepositoryRoot().FullName;

        var elapsed = Stopwatch.StartNew();
        var (code, output, _, timedOut) =
            ContainsMainFactAttribute.Git($"ls-remote \"{repository}\"", Bound);
        elapsed.Stop();

        Assert.False(timedOut, $"A reachable origin was reported as timed out after {elapsed.Elapsed}.");
        Assert.Equal(0, code);
        Assert.NotEmpty(output);
    }

    // THE PREMISE. Everything above rests on this fixture modelling UNRESPONSIVE rather than
    // REFUSED, and those differ only in a behaviour nothing else here observes. If the socket were
    // closed the detector would still fail -- but for a reason that reads identically to the defect
    // being unfixed, so the distinction is asserted rather than assumed.
    [Fact]
    public void TheFixtureAcceptsTheConnectionRatherThanRefusingIt()
    {
        using var sink = new UnresponsiveOrigin();
        using var client = new TcpClient();

        client.Connect(IPAddress.Loopback, sink.Port);

        Assert.True(client.Connected);
    }

    // The reason and the bound cannot drift apart: the message quotes the number, so a change to one
    // that forgets the other reddens here rather than shipping a sentence naming the wrong duration.
    [Fact]
    public void TheReasonNamesTheBoundItActuallyWaited()
    {
        Assert.Contains(
            $"within {ContainsMainFactAttribute.RemoteTimeout.TotalSeconds:F0}s",
            ContainsMainFactAttribute.TimedOutDetail,
            StringComparison.Ordinal);
    }

    // AND IT MUST NOT BORROW THE OTHER ARM'S CAUSE. Origin WAS reached; saying otherwise sends the
    // reader to check a network that is working. This is the one assertion that would still be worth
    // having if the timeout itself were removed.
    [Fact]
    public void TheReasonDoesNotClaimOriginWasUnreachable()
    {
        Assert.DoesNotContain(
            "could not reach origin",
            ContainsMainFactAttribute.TimedOutDetail,
            StringComparison.Ordinal);

        Assert.Contains(
            "did not answer",
            ContainsMainFactAttribute.TimedOutDetail,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A socket that completes the handshake and then says nothing — the condition git has no
    /// defence against, and the one arm 4 does not cover.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Accepted connections are held rather than closed, because closing one would send FIN or RST
    /// and let the client conclude something — which is the refused case, and the case that already
    /// worked.
    /// </para>
    /// <para>
    /// <b>THE ACCEPTED CLIENTS ARE HELD IN A FIELD, AND THAT LINE IS THE FIXTURE.</b> Discarding
    /// them makes the socket unreachable, so the finalizer closes it and the peer sees RST — the
    /// fixture silently becomes the REFUSED case it exists to be distinguished from. Measured, not
    /// theorised: discarded, this test passed when run alone and failed inside the full suite with
    /// <c>fatal: read error: Connection reset by peer</c>, because the surrounding tests allocate
    /// enough to bring on a collection. A GC-timing-dependent test that passes in isolation is the
    /// worst shape available, so the reference is kept deliberately rather than incidentally.
    /// </para>
    /// </remarks>
    private sealed class UnresponsiveOrigin : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly ConcurrentBag<TcpClient> _held = new();

        public UnresponsiveOrigin()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Accept();
        }

        public int Port { get; }

        public void Dispose()
        {
            _listener.Stop();

            foreach (var held in _held)
            {
                held.Dispose();
            }
        }

        private void Accept() =>
            _listener.BeginAcceptTcpClient(
                result =>
                {
                    try
                    {
                        // Never read from, never written to, and never allowed to become
                        // unreachable -- the client is left waiting on a reply that does not come.
                        _held.Add(_listener.EndAcceptTcpClient(result));
                        Accept();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Dispose() raced the callback. Nothing left to accept.
                    }
                    catch (InvalidOperationException)
                    {
                        // The listener stopped between the callback firing and being served.
                    }
                },
                state: null);
    }
}
