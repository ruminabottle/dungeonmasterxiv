using System;
using System.Linq;
using System.Reflection;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Both windows read one settable value and neither is a literal in the code path (A-1.27, BUG-55).
/// </summary>
/// <remarks>
/// <para>
/// <b>It asserts the NOT-A-LITERAL clause, and it must, because the agreement clause was already
/// true while A-1.27 was false.</b> A-1.27 has three parts — both windows / one settable value /
/// neither is a literal. After #101 merged there were genuinely two windows and they AGREED,
/// because <c>Grace</c> was <c>new()</c> and <c>Seat</c> was <c>new GraceWindow(null)</c> and both
/// fell through to <see cref="GraceWindow.Default"/>. A test shaped <i>"the two windows match"</i>
/// would have gone green at that moment with the third clause still failing.
/// <b>De-duplicating a literal is not single-sourcing it to a setting</b> — that is BUG-55's insight
/// one layer along, and it is the whole reason this file asserts what it does.
/// </para>
/// <para>
/// <b>So the assertion is positive, not comparative.</b> The window is set to a DISTINCTIVE value
/// that is deliberately not the default, and every clock must report THAT. Agreement on the literal
/// yields <see cref="GraceWindow.Default"/>, which is not the distinctive value, so this cannot pass
/// by coincidence. One assertion catches three different defects: a window reading the literal, a
/// window nobody wired, and a window added later that quietly takes neither.
/// </para>
/// <para>
/// <b>Derived over the type that OWNS the clocks, not the one that forwards them.</b>
/// <c>SessionCoordinator</c> exposes only <c>Grace</c> — it has no <c>Seat</c> property — so a sweep
/// over the coordinator would have found ONE window, passed, and silently missed the seat clock
/// entirely. <c>SessionInterruption</c> holds both, and is where a third would be added.
/// </para>
/// <para>
/// <b>What a green run does NOT mean.</b> It means no window in this type reads a literal. It says
/// nothing about a clock introduced in some other type, and nothing about whether the DURATION is
/// correct — <c>TheInterruptionWindowIsSettableTests</c> owns the value and its refusals.
/// </para>
/// </remarks>
public sealed class EveryWindowReadsTheOneSettingTests
{
    // Deliberately not GraceWindow.Default, and deliberately not a round number that somebody might
    // plausibly hard-code. If a window reads the literal it reports five minutes, and five minutes
    // is not this.
    private static readonly TimeSpan Distinctive = TimeSpan.FromSeconds(377);

    // THE UNIVERSAL. Fails on any clock that did not come from the one supplied value.
    [Fact]
    public void EveryWindowReportsTheLengthItWasGivenRatherThanTheDefault()
    {
        var unwired = WindowsOf(Build(Distinctive))
            .Where(clock => clock.Window.Remaining != Distinctive)
            .Select(clock => $"{clock.Name} reports {clock.Window.Remaining} instead of {Distinctive}")
            .ToList();

        Assert.True(
            unwired.Count == 0,
            "These clocks are not reading the configured value:\n  "
            + string.Join("\n  ", unwired)
            + "\n\nA clock constructed with `new()` or a defaulted `TimeSpan?` reads "
            + $"GraceWindow.Default ({GraceWindow.Default}), which is the literal A-1.27 forbids.");
    }

    // THE CONTROL, and without it the universal is worth nothing: if the sweep found no clocks at
    // all -- a renamed property, a changed type, a reflection call that quietly returns empty -- the
    // universal would pass while asking nothing whatsoever.
    //
    // Asserts TWO rather than "at least one", because one is the number that would have hidden the
    // original bug: the seat clock is the window that did not exist when this criterion was written.
    [Fact]
    public void TheSweepFindsBothClocksSoTheUniversalMeansSomething()
    {
        var found = WindowsOf(Build(Distinctive)).Select(clock => clock.Name).ToList();

        Assert.True(
            found.Count >= 2,
            $"The sweep found {found.Count} clock(s) [{string.Join(", ", found)}]. A-1.27 is about "
            + "BOTH windows; over a set of one it passes vacuously, which is exactly the failure "
            + "BUG-55 exists to prevent.");
    }

    // THE PROOF THAT THE DISTINCTIVE VALUE DISCRIMINATES. If it happened to equal the default, every
    // assertion above would pass on a build where nothing was wired at all. Cheap, and it is the
    // difference between a test and a coincidence.
    [Fact]
    public void TheDistinctiveValueIsNotTheLiteralItIsLookingFor()
    {
        Assert.NotEqual(GraceWindow.Default, Distinctive);
    }

    // Fails if: the two clocks are wired to separate values. A-1.27 is "ONE settable value", so a
    // build that threaded two independent durations would satisfy the universal above and still be
    // wrong -- the criterion is about the relationship, not about each window in isolation.
    [Fact]
    public void OneValueFeedsBothSoTheyCannotDriftApart()
    {
        var lengths = WindowsOf(Build(Distinctive))
            .Select(clock => clock.Window.Remaining)
            .Distinct()
            .ToList();

        Assert.True(
            lengths.Count == 1,
            $"The clocks read {lengths.Count} different lengths [{string.Join(", ", lengths)}]. "
            + "Changing the setting must move them together.");
    }

    private static SessionInterruption Build(TimeSpan window) =>
        new(new RelayLink(new SilentTransport(), () => RelayEndpoint.Default, _ => { }),
            new HostSession(),
            new JoinAttempt(),
            () => { },
            window);

    // Derived from the type, not from a list of names: a clock added next month is swept without
    // anyone remembering to extend anything. Remaining equals the constructed length before any
    // tick, which is what makes the length observable without exposing a field for the test.
    private static (string Name, GraceWindow Window)[] WindowsOf(SessionInterruption interruption) =>
        [.. typeof(SessionInterruption)
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(GraceWindow))
            .Select(p => (p.Name, (GraceWindow)p.GetValue(interruption)!))];

    private sealed class SilentTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public bool IsConnected => false;

        public bool IsReadyToSend => false;

        public void Connect(Uri relay)
        {
        }

        public void Disconnect()
        {
        }

        public void Send(byte[] envelope)
        {
        }

        public void Raise(SessionFailure failure) => Failed?.Invoke(failure);

        public void Deliver(byte[] frame) => Received?.Invoke(frame);
    }
}
