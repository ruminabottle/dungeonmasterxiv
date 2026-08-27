using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// The keepalive is a contract with the plugin's connection adapter, not a tuning knob.
/// </summary>
/// <remarks>
/// RP sessions sit quiet between rolls, and an idle connection is what NAT tables and middleboxes
/// reap. The failure this guards is specific and nasty: a relay whose reap timeout is not clearly
/// longer than the client's ping interval kills the sessions the keepalive exists to keep alive,
/// during play, looking like someone else's network problem.
/// </remarks>
public sealed class KeepAliveContractTests
{
    /// <summary>
    /// The relay pings at the shared contract's interval, not at a number of its own. This is the
    /// test that fails if someone restates 30 seconds here instead of consuming it.
    /// </summary>
    [Fact]
    public void TheRelayPingsAtTheContractInterval()
    {
        Assert.Equal(TransportContract.KeepAliveInterval, new RelayOptions().KeepAliveInterval);
    }

    [Fact]
    public void TheDefaultReapTimeoutLeavesRoomForSeveralMissedPings()
    {
        var options = new RelayOptions();

        Assert.True(
            options.KeepAliveTimeout > TransportContract.KeepAliveInterval * 2,
            $"A reap timeout of {options.KeepAliveTimeout} against a {TransportContract.KeepAliveInterval} "
            + "ping leaves no margin: one dropped ping would end a live session.");
    }

    /// <summary>
    /// The reaper is derived from the interval rather than written down, so raising the contract's
    /// interval cannot leave a reaper stranded below it.
    /// </summary>
    /// <remarks>
    /// The bound couples two numbers, so it is guarded on the side that gets edited — the interval
    /// is a shared constant somebody may well change, and a literal reaper would silently stop
    /// clearing it. Note also that the relay's reaper is NOT
    /// <see cref="TransportContract.KeepAliveTimeout"/>: that is the client's tolerance for a
    /// missing pong, and the two coinciding today is not a reason to conflate them.
    /// </remarks>
    [Fact]
    public void RaisingTheIntervalRaisesTheReaperWithIt()
    {
        var slower = new RelayOptions { KeepAliveInterval = TransportContract.KeepAliveInterval * 4 };

        Assert.True(slower.KeepAliveTimeout > slower.KeepAliveInterval);
    }

    /// <summary>
    /// The reaper has no environment variable, so no deployment can set it below the interval the
    /// clients are pinging at. The coupled-pair rule taken one step further than a guard: leave only
    /// one of the two numbers settable, and the dangerous edit stops existing.
    /// </summary>
    [Fact]
    public void TheReaperCannotBeConfiguredFromTheEnvironment()
    {
        Environment.SetEnvironmentVariable(RelayOptions.EnvironmentPrefix + "KEEPALIVE_TIMEOUT_SECONDS", "1");
        try
        {
            var options = RelayOptions.FromEnvironment();

            Assert.True(
                options.KeepAliveTimeout > options.KeepAliveInterval,
                "A DMX_RELAY_KEEPALIVE_TIMEOUT_SECONDS variable now exists and can strand the reaper "
                + "below the ping interval, which is the edit this design removes rather than guards.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(RelayOptions.EnvironmentPrefix + "KEEPALIVE_TIMEOUT_SECONDS", null);
        }
    }

    /// <summary>
    /// The reaper must work when the far end does not close cleanly — a dead process sends no close
    /// frame, and BUG-5 is a live instance of a client that disposes its socket without one. So it
    /// is measured on ping/pong liveness, which the WebSocket layer owns, and never on message
    /// traffic, which would reap the quiet sessions it exists to protect.
    /// </summary>
    [Fact]
    public void TheReaperIsConfiguredOnTheWebSocketLayerRatherThanOnTraffic()
    {
        var options = new RelayOptions { Port = 0, UseTls = false };

        using var app = RelayApp.Build(options);

        Assert.True(options.KeepAliveTimeout > options.KeepAliveInterval);
    }

    [Fact]
    public void ARelayThatReapsFasterThanItPingsRefusesToStart()
    {
        var options = new RelayOptions
        {
            Port = 0,
            UseTls = false,
            KeepAliveInterval = TimeSpan.FromSeconds(90),
            KeepAliveTimeout = TimeSpan.FromSeconds(30),
        };

        var failure = Assert.Throws<InvalidOperationException>(() => RelayApp.Build(options));
        Assert.Contains("KeepAliveTimeout", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TlsWithoutACertificateFailsLoudlyRatherThanServingPlaintext()
    {
        var options = new RelayOptions { Port = 0, UseTls = true, CertificatePath = null };

        var failure = Assert.Throws<InvalidOperationException>(() =>
        {
            using var app = RelayApp.Build(options);
        });

        Assert.Contains("certificate", failure.Message, StringComparison.OrdinalIgnoreCase);
    }
}
