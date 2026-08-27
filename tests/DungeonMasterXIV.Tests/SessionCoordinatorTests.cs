using System;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class SessionCoordinatorTests
{
    // R-1.1's invariant at the level that actually holds the socket. Fails if: the connection
    // outlives the session — the one circumstance R-1.1 says must not exist.
    [Fact]
    public void EndingTheSessionDropsTheRelayConnection()
    {
        var (coordinator, transport) = Build();
        coordinator.StartHosting();
        Assert.True(transport.IsConnected);

        coordinator.StopHosting();

        Assert.False(transport.IsConnected);
    }

    // Fails if: the connection is opened at construction or on plugin load. R-1.1 says it opens
    // when the DM starts a session "and not before".
    [Fact]
    public void NoConnectionIsHeldBeforeASessionStarts()
    {
        var (_, transport) = Build();

        Assert.False(transport.IsConnected);
        Assert.Equal(0, transport.ConnectCount);
    }

    // R-1.8 + A-1.5c. Fails if: the relay is read once at construction, which would mean changing
    // it in settings did nothing until the plugin reloaded.
    [Fact]
    public void TheRelayIsReadWhenConnectingSoChangingItTakesEffect()
    {
        var address = "wss://first.example.org/session";
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => address);

        coordinator.StartHosting();
        coordinator.StopHosting();
        address = "wss://second.example.org/session";
        coordinator.StartHosting();

        Assert.Equal("second.example.org", transport.LastRelay!.Host);
    }

    // A-1.5b, forced failure. Fails if: an unusable relay address leaves the user connecting
    // forever instead of being told. Tests the failure path, not the success path.
    [Fact]
    public void AnUnusableRelayAddressFailsImmediatelyRatherThanHanging()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => "not-a-relay");

        coordinator.StartHosting();

        Assert.Equal(HostingPhase.Failed, coordinator.Host.Phase);
        Assert.Equal(SessionFailure.RelayUnreachable, coordinator.Host.Failure);
        Assert.False(transport.IsConnected);
        Assert.NotEmpty(SessionFailureMessage.For(coordinator.Host.Failure));
    }

    // Fails if: Fail and SynchroniseTransport call each other without terminating. They are mutually
    // recursive by design — failing changes whether a connection is wanted — and the input that
    // would expose an unbounded loop is exactly the one above, so it is asserted rather than
    // assumed. A stack overflow here would take the game client down with it.
    [Fact]
    public void FailingWhileSynchronisingTerminates()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => "not-a-relay");

        var exception = Record.Exception(() =>
        {
            coordinator.StartHosting();
            coordinator.RequestJoin(SessionCode.FromValid("BKD7RM"));
        });

        Assert.Null(exception);
    }

    // D-13 None, at the coordinator. Fails if: a denied participant is addressable afterwards, or
    // was ever counted.
    [Fact]
    public void ADeniedParticipantIsNeverAddressableAndNeverCounted()
    {
        var (coordinator, _) = Build();
        coordinator.StartHosting();
        coordinator.ReceiveJoinRequest(Request("PEER-1"));

        coordinator.Deny("PEER-1");

        Assert.Empty(coordinator.Admissions.Pending);
        Assert.False(coordinator.Audience.IsAdmitted("PEER-1"));
        Assert.Equal(0, coordinator.Audience.Count);
    }

    // Fails if: admitting leaves the prompt up, which would let the DM admit the same request twice
    // and inflate their own count.
    [Fact]
    public void AdmittingClearsThePromptAndMakesTheParticipantAddressable()
    {
        var (coordinator, _) = Build();
        coordinator.StartHosting();
        coordinator.ReceiveJoinRequest(Request("PEER-1"));

        coordinator.Admit("PEER-1");

        Assert.Empty(coordinator.Admissions.Pending);
        Assert.True(coordinator.Audience.IsAdmitted("PEER-1"));
    }

    // Fails if: ending a session leaves participants addressable, which would let state flow to
    // people whose session is over.
    [Fact]
    public void EndingTheSessionEmptiesTheAudience()
    {
        var (coordinator, _) = Build();
        coordinator.StartHosting();
        coordinator.Admit("PEER-1");

        coordinator.StopHosting();

        Assert.Equal(0, coordinator.Audience.Count);
    }

    // The regression test for "correct but unreached". Fails if: nothing drives the timeouts —
    // which is exactly what shipped, because the state machines were right and no caller existed.
    // Asserts through Tick rather than by calling ExpireIfRegistrationTimedOut directly, so a
    // future refactor that removes the tick fails here instead of passing on the unit test.
    [Fact]
    public void TickingPastTheTimeoutEndsARegistrationTheRelayNeverAnswered()
    {
        var (coordinator, transport) = Build();
        coordinator.StartHosting();

        coordinator.Tick(TimeSpan.Zero, Now);                              // settle into the phase
        coordinator.Tick(HostSession.RegistrationTimeout, Now);

        Assert.Equal(HostingPhase.Failed, coordinator.Host.Phase);
        Assert.Equal(SessionFailure.RelayUnreachable, coordinator.Host.Failure);
        Assert.False(transport.IsConnected);
    }

    // Fails if: elapsed time is not reset when a phase changes, which would carry a previous
    // phase's clock forward and time out a brand-new attempt instantly.
    [Fact]
    public void TheClockRestartsWhenThePhaseChanges()
    {
        var (coordinator, _) = Build();
        coordinator.StartHosting();
        coordinator.Tick(TimeSpan.Zero, Now);
        coordinator.Tick(HostSession.RegistrationTimeout - TimeSpan.FromSeconds(1), Now);

        coordinator.Host.Registered();
        coordinator.Tick(TimeSpan.Zero, Now);
        coordinator.Tick(TimeSpan.FromSeconds(2), Now);

        Assert.Equal(HostingPhase.Hosting, coordinator.Host.Phase);
    }

    // Non-blocking 6, and the reason SessionFailure.ConnectionLost was unreachable in the product.
    // Fails if: a transport failure never lands in the state machine — a relay that refuses would
    // then leave the DM watching a spinner with the reason only in a log they will not read.
    [Fact]
    public void ATransportFailureReachesTheSessionStateOnTheNextTick()
    {
        var (coordinator, transport) = Build();
        coordinator.StartHosting();

        transport.RaiseFailure(SessionFailure.ConnectionLost);
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(HostingPhase.Failed, coordinator.Host.Phase);
        Assert.Equal(SessionFailure.ConnectionLost, coordinator.Host.Failure);
    }

    // Blocking 2. Fails if: pending requests are one slot — four players clicking join at the start
    // of a session is the ordinary case, and a single slot strands every one but the last on
    // "waiting for the DM", which looks to them like a DM ignoring them.
    [Fact]
    public void EveryConcurrentJoinRequestIsHeldRatherThanTheNewestReplacingTheRest()
    {
        var (coordinator, _) = Build();
        coordinator.StartHosting();

        coordinator.ReceiveJoinRequest(Request("PEER-1"));
        coordinator.ReceiveJoinRequest(Request("PEER-2"));
        coordinator.ReceiveJoinRequest(Request("PEER-3"));

        Assert.Equal(3, coordinator.Admissions.Pending.Count);
    }

    // Fails if: deciding one request clears the others, which is the same stranding by a different
    // route — the DM admits the first and the rest vanish from the prompt.
    [Fact]
    public void DecidingOneRequestLeavesTheOthersPending()
    {
        var (coordinator, _) = Build();
        coordinator.StartHosting();
        coordinator.ReceiveJoinRequest(Request("PEER-1"));
        coordinator.ReceiveJoinRequest(Request("PEER-2"));

        coordinator.Admit("PEER-1");

        Assert.Single(coordinator.Admissions.Pending);
        Assert.Contains(coordinator.Admissions.Pending, r => r.PeerCode == "PEER-2");
        Assert.True(coordinator.Audience.IsAdmitted("PEER-1"));
        Assert.False(coordinator.Audience.IsAdmitted("PEER-2"));
    }

    // Fails if: a duplicate request adds a second prompt for the same person, which the DM would
    // have to dismiss twice.
    [Fact]
    public void ARepeatedRequestFromTheSamePeerDoesNotStack()
    {
        var (coordinator, _) = Build();
        coordinator.ReceiveJoinRequest(Request("PEER-1"));
        coordinator.ReceiveJoinRequest(Request("PEER-1"));

        Assert.Single(coordinator.Admissions.Pending);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 27, 3, 0, 0, TimeSpan.Zero);

    private static PendingAdmission Request(string peerCode) =>
        new(peerCode, "BKD-7RM-CDF-GH", AdmissionDeadline.DecidedByHost(Now));

    private static (SessionCoordinator Coordinator, FakeTransport Transport) Build()
    {
        var transport = new FakeTransport();
        return (new SessionCoordinator(transport, () => RelayEndpoint.Default), transport);
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);

        public bool IsConnected { get; private set; }

        public int ConnectCount { get; private set; }

        public Uri? LastRelay { get; private set; }

        public List<byte[]> Sent { get; } = new();

        public void Connect(Uri relay)
        {
            IsConnected = true;
            ConnectCount++;
            LastRelay = relay;
        }

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);
    }
}
