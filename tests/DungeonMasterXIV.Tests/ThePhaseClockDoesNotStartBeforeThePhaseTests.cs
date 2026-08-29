using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The two halves of the phase clock nothing was watching (DMXENG-56).
/// </summary>
/// <remarks>
/// <para>
/// <b>DMXENG-56 required the move to be pinned by mutation rather than by a green suite, and the
/// mutation is what found these.</b> Deleting each step of <see cref="PhaseTimeouts.Advance"/> in
/// turn, against the suite as it stood:
/// </para>
/// <code>
/// the phase-change reset      -> SUITE GREEN     not covered
/// the clock accumulating      -> 4 tests fail    covered
/// join.ExpireIfContactTimedOut-> SUITE GREEN     not covered
/// host.ExpireIfRegistration...-> 4 tests fail    covered
/// </code>
/// <para>
/// <b>That table was measured against the whole BLOCK, and it hid a line (BUG-95).</b> Deleting the
/// reset block is caught, because it takes the early return with it. Deleting only
/// <c>_timeInPhase = TimeSpan.Zero</c> was invisible to all 978 tests, including the four this file
/// added for that very line. The reason is a convention rather than an oversight: <b>no test ticked
/// the coordinator before the phase changed</b>, so the clock was always already zero when the reset
/// ran, and zeroing zero cannot be observed. A mutation survey measures the intersection of what you
/// deleted and what your setup arranged, and a convention every test shares is invisible to every
/// test. <see cref="ASessionStartedAfterThePluginHasBeenTickingIsNotInstantlyStale"/> is the one that
/// ticks first; the single-line deletion now reddens it.
/// </para>
/// <para>
/// <b>Pre-existing rather than introduced, and measured rather than assumed</b> — the same two
/// deletions made to the code in its old home in <c>SessionCoordinator.Tick</c> at <c>origin/main</c>
/// are equally invisible. Half the clock was being moved on the strength of a suite that could not
/// see it.
/// </para>
/// <para>
/// <b>Both are about a REAL hazard rather than about coverage for its own sake.</b> The reset is what
/// stops one long frame ending a session that has only just begun, and the contact timeout is the
/// entire reason a joiner does not sit on the open-ended spinner R-1.8 forbids.
/// </para>
/// </remarks>
public sealed class ThePhaseClockDoesNotStartBeforeThePhaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 4, 0, 0, TimeSpan.Zero);

    // Fails if: the clock accumulates on the frame a phase CHANGED.
    //
    // THE HAZARD IS ONE LONG FRAME, AND IT IS NOT HYPOTHETICAL ON THIS PLATFORM. The plugin supplies
    // the delta from the game's own frame time, and a zone load, an alt-tab or a stalled machine
    // hands the next tick a delta of seconds. Without the reset, a registration that began in that
    // same frame is charged the whole stall and fails instantly -- the DM presses Host and is told
    // the relay never answered, before a single frame of waiting has actually happened.
    [Fact]
    public void ASessionThatJustStartedSurvivesOneVeryLongFrame()
    {
        var host = Coordinator(out _);

        host.StartHosting();
        Assert.Equal(HostingPhase.Registering, host.Host.Phase);

        // The first tick after the phase moved. Far longer than the timeout, and it must not count.
        host.Tick(HostSession.RegistrationTimeout * 10, Now);

        Assert.Equal(HostingPhase.Registering, host.Host.Phase);
        Assert.Equal(SessionFailure.None, host.Host.Failure);
    }

    // BUG-95. THE ONE ABOVE CANNOT SEE THE ZEROING, AND NEITHER COULD ANY OF THE 978.
    //
    // Delete only `_timeInPhase = TimeSpan.Zero` — leaving the two phase assignments and the early
    // return — and the whole suite stays green. Not because the tests are weak, but because EVERY
    // test transitions the phase before it ever ticks, so the clock is ALREADY ZERO when the reset
    // runs. Zeroing something that is zero is unobservable by construction. The four tests written
    // for this very line share that shape, which is why the mutation round that produced them could
    // not have caught it: a mutation survey measures the intersection of what you deleted and what
    // your setup arranged, and a convention every test follows is invisible to every test.
    //
    // THIS ONE TICKS FIRST. That is the whole difference, and it is a change inside the test rather
    // than to the fixture — Coordinator() is untouched, so no other test's floor moves.
    //
    // THE HAZARD IT PINS IS WORSE THAN THE ONE ABOVE. That needs a stalled frame; this needs
    // nothing. Both timeouts are ten seconds, so a plugin merely LOADED AND TICKING for ten seconds
    // hands the new phase the old phase's clock: play for ten seconds, press Host, and be told the
    // relay never answered. Every ordinary session, no stall required.
    [Fact]
    public void ASessionStartedAfterThePluginHasBeenTickingIsNotInstantlyStale()
    {
        var host = Coordinator(out _);

        // Loaded and ticking, NOT hosting. This is every real session before the DM presses Host.
        host.Tick(TimeSpan.FromSeconds(30), Now);
        host.Tick(TimeSpan.FromSeconds(30), Now);

        host.StartHosting();
        Assert.Equal(HostingPhase.Registering, host.Host.Phase);

        host.Tick(TimeSpan.FromMilliseconds(16), Now);   // the frame the phase changed on
        host.Tick(TimeSpan.FromMilliseconds(16), Now);   // one ordinary frame after it

        // Two frames of 16ms against a ten-second timeout. Only an inherited clock can fail this.
        Assert.Equal(HostingPhase.Registering, host.Host.Phase);
        Assert.Equal(SessionFailure.None, host.Host.Failure);
    }

    // The positive control. Without it the test above would pass just as well against a build whose
    // registration can never time out at all -- which is the state A-1.5b exists to forbid, and
    // exactly what "the state machines know how to time out, nothing calls them" looked like.
    [Fact]
    public void TheVeryNextFrameOfTheSameLengthDoesTimeItOut()
    {
        var host = Coordinator(out _);

        host.StartHosting();
        host.Tick(TimeSpan.Zero, Now);
        host.Tick(HostSession.RegistrationTimeout * 10, Now);

        Assert.Equal(HostingPhase.Failed, host.Host.Phase);
    }

    // Fails if: a joiner's contact timeout is never reached through the coordinator.
    //
    // R-1.8 forbids the open-ended spinner, and this is the only thing that ends one for a joiner
    // whose relay never answers. HostSession's half was covered four times over; JoinAttempt's was
    // covered nowhere, so deleting it left the suite green.
    [Fact]
    public void AJoinerWhoseRelayNeverAnswersIsToldRatherThanLeftSpinning()
    {
        var player = Coordinator(out _);

        player.RequestJoin(SessionCode.FromValid("BCDFGH"), DisplayName.OrNone("Bob"));
        player.SynchroniseTransport();
        Assert.Equal(JoinPhase.Contacting, player.Join.Phase);

        player.Tick(TimeSpan.Zero, Now);
        player.Tick(JoinAttempt.ContactTimeout + TimeSpan.FromSeconds(1), Now);

        Assert.Equal(SessionFailure.RelayUnreachable, player.Join.Failure);
    }

    // The joining half of the reset, for the same reason as the hosting half: a join that began in a
    // stalled frame must not be told the relay is unreachable before it has waited at all.
    [Fact]
    public void AJoinThatJustStartedAlsoSurvivesOneVeryLongFrame()
    {
        var player = Coordinator(out _);

        player.RequestJoin(SessionCode.FromValid("BCDFGH"), DisplayName.OrNone("Bob"));
        player.SynchroniseTransport();

        player.Tick(JoinAttempt.ContactTimeout * 10, Now);

        Assert.Equal(JoinPhase.Contacting, player.Join.Phase);
        Assert.Equal(SessionFailure.None, player.Join.Failure);
    }

    private static SessionCoordinator Coordinator(out SilentTransport transport)
    {
        transport = new SilentTransport();
        return new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: new SilentLog());
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

    /// <summary>A relay that accepts the connection and then never says anything.</summary>
    private sealed class SilentTransport : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
        }
    }
}
