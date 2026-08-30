using System;
using System.Collections.Generic;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.12: the DM's client retains its log at session end — and <b>the retention step runs BEFORE
/// the session is torn down</b>, because after the teardown there is nothing left to keep.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE POSITION IS PINNED BY AN ORDER ASSERTION, NOT A HAPPENED ONE, AND THIS FILE'S OWN
/// NEIGHBOUR IS WHY.</b> <c>QuittingTheGameAnnouncesDepartureTests</c> records that with the
/// departure announcement moved after the detach, <i>the test asserting a departure is sent still
/// passed</i> — only the test asserting the ORDER caught it. Retention has exactly that shape: a
/// step moved below <c>StopHosting</c> still runs, still writes a file, and keeps an empty log.
/// It would look correct in every way except the one that matters.
/// </para>
/// <para>
/// <b>So the observation is what retention COULD SEE when it ran</b>, taken from the session
/// itself rather than from a caller: if it runs first the session is still hosting, and if it runs
/// after <c>StopHosting</c> it is not. Both guards below exist so that a case which cannot
/// distinguish the two fails loudly instead of passing vacuously.
/// </para>
/// </remarks>
public class RetentionRunsBeforeTheSessionIsTornDownTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RetentionSeesALiveSessionRatherThanATornDownOne()
    {
        var host = Hosting();
        var sawHostingAtRetentionTime = new List<bool>();

        var retention = RetentionThatRecords(host, sawHostingAtRetentionTime);

        // GUARD ONE: the session must be hosting before the teardown, or "it was hosting when
        // retention ran" is true of nothing and the ordering is untested.
        Assert.True(host.InAHostedSession, "Not hosting before teardown, so the ordering is untested.");

        host.EndSessionForTeardown(Now, retention);

        // GUARD TWO: the teardown must actually have stopped hosting, or there is no "after" for
        // retention to have run in and the assertion below cannot fail.
        Assert.False(host.InAHostedSession, "Teardown did not stop hosting, so the ordering is untested.");

        Assert.Equal(1, retention.Attempts);
        Assert.Equal([true], sawHostingAtRetentionTime);
    }

    // The happened-assertion, kept deliberately AND labelled as insufficient. It passes whether
    // retention runs first or last, which is exactly why it is not the test above.
    [Fact]
    public void RetentionIsInvokedAtAll()
    {
        var host = Hosting();
        var retention = RetentionThatRecords(host, []);

        host.EndSessionForTeardown(Now, retention);

        Assert.Equal(1, retention.Attempts);
    }

    [Fact]
    public void TeardownWithNoRetentionSuppliedStillTearsDown()
    {
        var host = Hosting();

        host.EndSessionForTeardown(Now);

        // The parameter is optional because nothing supplies it yet. An unwired build must still
        // tear down exactly as it did before -- this is the regression guard on that.
        Assert.False(host.InAHostedSession);
    }

    [Fact]
    public void AJoinersTeardownRetainsNothing()
    {
        // R-2.12's asymmetry, at the teardown seam rather than in the store: a client that was not
        // hosting reaches retention and is refused by it, which is a different fact from never
        // reaching it.
        var joiner = new SessionCoordinator(
            new QuietTransport(), () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, SessionCapabilities.Default);

        var archive = new CountingLogArchive();
        var retention = new SessionLogRetention(
            new RetainedLogStore(archive), Guid.NewGuid(), () => []);

        joiner.EndSessionForTeardown(Now, retention);

        Assert.Equal(1, retention.Attempts);
        Assert.Equal(0, archive.Writes);
    }

    private static SessionCoordinator Hosting()
    {
        var host = new SessionCoordinator(
            new QuietTransport(), () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, SessionCapabilities.Default);

        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();
        return host;
    }

    private static SessionLogRetention RetentionThatRecords(
        SessionCoordinator coordinator,
        List<bool> sawHosting) =>
        new(
            new RetainedLogStore(new CountingLogArchive()),
            Guid.NewGuid(),
            () =>
            {
                // Read at the moment retention runs. This is the observation the order turns on.
                sawHosting.Add(coordinator.InAHostedSession);
                return [];
            });

    /// <summary>A transport that accepts everything and remembers nothing. The session's own state
    /// is what these tests observe, so the wire is deliberately inert.</summary>
    private sealed class QuietTransport : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
            // Deliberately inert: nothing here is observed, and raising the events keeps the
            // compiler from warning that they are never used.
            _ = Failed;
            _ = Received;
        }
    }

    private sealed class CountingLogArchive : IRetainedLogArchive
    {
        private readonly Dictionary<Guid, string> _logs = [];

        public int Writes { get; private set; }

        public IReadOnlyList<Guid> Campaigns() => [.. _logs.Keys];

        public string? Read(Guid campaignId) => _logs.GetValueOrDefault(campaignId);

        public void Write(Guid campaignId, string contents)
        {
            Writes++;
            _logs[campaignId] = contents;
        }

        public bool Delete(Guid campaignId) => _logs.Remove(campaignId);
    }
}
