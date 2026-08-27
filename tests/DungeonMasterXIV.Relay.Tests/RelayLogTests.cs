using System.Net;
using System.Reflection;
using DungeonMasterXIV.Relay.Diagnostics;
using DungeonMasterXIV.Relay.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// A-1.5a-r: the relay's log shows the outcome of a connection and the reason for a failure,
/// readable by QA with no human present — and shows neither a character name nor an address (D-8).
/// </summary>
public sealed class RelayLogTests
{
    [Fact]
    public void AFailedRouteRecordsTheReason()
    {
        var captured = new CapturingLogger();
        var log = new RelayLog(captured);

        log.Routed("conn-1", "BCDFGH", RelayDecision.Drop(RelayOutcome.SenderNotInSession));

        var line = Assert.Single(captured.Lines);
        Assert.Contains("SenderNotInSession", line, StringComparison.Ordinal);
        Assert.Contains("conn-1", line, StringComparison.Ordinal);
        Assert.Contains("BCDFGH", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AClosedConnectionRecordsWhetherItEndedTheSession()
    {
        var captured = new CapturingLogger();
        var log = new RelayLog(captured);

        log.ConnectionClosed(
            "conn-1",
            new ConnectionRemoval([new SessionDeparture("BCDFGH", EndedSession: true, ["conn-2"])]),
            "closed by peer");

        var line = Assert.Single(captured.Lines);
        Assert.Contains("closed by peer", line, StringComparison.Ordinal);
        Assert.Contains("ended=True", line, StringComparison.Ordinal);
        Assert.Contains("orphaned=1", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ordinary payload traffic stays at Debug, so the default log is failure forensics and not a
    /// record of who spoke to whom and when.
    /// </summary>
    /// <summary>
    /// A connection that hosted one session and joined another gets a line for each. A single line
    /// would have to choose which session to name, and QA reading this after a failed attempt needs
    /// every session the connection was in rather than whichever it happened to hold first.
    /// </summary>
    [Fact]
    public void AConnectionInTwoSessionsIsReportedForBoth()
    {
        var captured = new CapturingLogger();
        var log = new RelayLog(captured);

        log.ConnectionClosed(
            "conn-1",
            new ConnectionRemoval(
            [
                new SessionDeparture("BCDFGH", EndedSession: true, ["conn-2"]),
                new SessionDeparture("JKMNPR", EndedSession: false, []),
            ]),
            "closed by peer");

        Assert.Equal(2, captured.Lines.Count);
        Assert.Contains(captured.Lines, line => line.Contains("BCDFGH", StringComparison.Ordinal));
        Assert.Contains(captured.Lines, line => line.Contains("JKMNPR", StringComparison.Ordinal));
    }

    [Fact]
    public void ForwardedPayloadsAreNotLoggedAtInformation()
    {
        var captured = new CapturingLogger();
        var log = new RelayLog(captured);

        log.Routed("conn-1", "BCDFGH", RelayDecision.Forward(RelayOutcome.PayloadForwarded, ["conn-2"]));

        Assert.Equal(LogLevel.Debug, Assert.Single(captured.Levels));
    }

    /// <summary>
    /// D-8, enforced on the shape of the API rather than on the discipline of whoever edits it
    /// next: an address is the correlator that links a client across two session codes, so there
    /// must be no parameter through which one could reach a log line. This fails the moment
    /// somebody adds an overload taking an <see cref="IPAddress"/>, an <see cref="EndPoint"/> or an
    /// <see cref="HttpContext"/> to be helpful.
    /// </summary>
    [Fact]
    public void NoLogMethodCanBeGivenAnAddress()
    {
        var forbidden = new[] { typeof(IPAddress), typeof(EndPoint), typeof(IPEndPoint), typeof(HttpContext) };

        var offenders = typeof(RelayLog)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters(), (method, parameter) => (method, parameter))
            .Where(pair => forbidden.Any(type => type.IsAssignableFrom(pair.parameter.ParameterType)))
            .Select(pair => $"{pair.method.Name}({pair.parameter.ParameterType.Name})")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "The forensic log can be handed a network address, which D-8 forbids: " + string.Join(", ", offenders));
    }

    private sealed class CapturingLogger : ILogger<RelayLog>
    {
        public List<string> Lines { get; } = [];

        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Lines.Add(formatter(state, exception));
        }
    }
}
