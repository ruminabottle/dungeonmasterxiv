using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DungeonMasterXIV.Relay.Diagnostics;
using DungeonMasterXIV.Relay.Sessions;
using DungeonMasterXIV.Relay.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// BUG-78: one exception type carried two causes and the handler named only the rarer one.
/// </summary>
/// <remarks>
/// <para>
/// <c>RelayApp</c> passes <c>context.RequestAborted</c>, which fires when the <b>client</b> goes
/// away — a dropped network, a crashed game client, a force-quit. A genuine shutdown reaches the
/// same <see cref="OperationCanceledException"/> arm. Nothing in the exception distinguishes them
/// and the token is the same object either way, so the only thing that can tell them apart is
/// whether the process is actually stopping.
/// </para>
/// <para>
/// <b>Both directions are asserted.</b> A test that only proved the client case would pass against a
/// handler that had simply swapped the string, leaving a real shutdown mislabelled the other way —
/// which is the same one-directional shape the bug itself has.
/// </para>
/// <para>
/// <b>Measured against a running image before this was written.</b> The abrupt-drop case is a RACE
/// between this arm and <see cref="WebSocketException"/>: over six runs it landed here once and on
/// <c>ConnectionClosedPrematurely</c> five times. That is why the test drives the cancellation
/// directly rather than dropping a socket and hoping — a container repro reproduces the defect about
/// one time in six.
/// </para>
/// </remarks>
public sealed class ADroppedClientIsNotAShutdownTests
{
    // THE DEFECT. Fails against the shipped handler, which reports every cancellation as a shutdown.
    [Fact]
    public async Task AClientThatVanishesIsNotReportedAsTheRelayShuttingDown()
    {
        var (endpoint, captured) = Endpoint(stopping: false);
        using var cts = new CancellationTokenSource();
        using var socket = ASocketThatNeverReceives();

        var serving = endpoint.ServeAsync(socket, cts.Token);
        await cts.CancelAsync();
        await serving;

        Assert.Contains(captured.Lines, line => line.Contains("closed by peer without a close frame", StringComparison.Ordinal));
        Assert.DoesNotContain(captured.Lines, line => line.Contains("relay shutting down", StringComparison.Ordinal));
    }

    // THE OTHER DIRECTION, and it is what stops this being a swapped string. A relay that really is
    // stopping must still say so — the string was never wrong, only its audience.
    [Fact]
    public async Task AGenuineShutdownIsStillReportedAsOne()
    {
        var (endpoint, captured) = Endpoint(stopping: true);
        using var cts = new CancellationTokenSource();
        using var socket = ASocketThatNeverReceives();

        var serving = endpoint.ServeAsync(socket, cts.Token);
        await cts.CancelAsync();
        await serving;

        Assert.Contains(captured.Lines, line => line.Contains("relay shutting down", StringComparison.Ordinal));
    }

    /// <summary>A server-side WebSocket over a stream that never yields a byte and never ends.</summary>
    /// <remarks>
    /// The cancellation has to come from the TOKEN rather than from the stream: that is how it
    /// arrives in production, where Kestrel cancels <c>RequestAborted</c> while the read is pending.
    /// </remarks>
    private static WebSocket ASocketThatNeverReceives() =>
        WebSocket.CreateFromStream(
            new NeverReadableStream(),
            isServer: true,
            subProtocol: null,
            keepAliveInterval: Timeout.InfiniteTimeSpan);

    private static (WebSocketRelayEndpoint Endpoint, CapturingLogger Captured) Endpoint(bool stopping)
    {
        var captured = new CapturingLogger();
        var log = new RelayLog(captured);
        var directory = new ConnectionDirectory();
        var registry = new SessionRegistry();
        var hub = new RelayHub(new RelayRouter(registry), registry, directory, log);

        return (
            new WebSocketRelayEndpoint(hub, directory, log, new RelayOptions(), new Lifetime(stopping)),
            captured);
    }

    /// <summary>An application lifetime that is, or is not, already stopping.</summary>
    private sealed class Lifetime(bool stopping) : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping =
            stopping ? new CancellationTokenSource(millisecondsDelay: 0) : new CancellationTokenSource();

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();
    }

    private sealed class NeverReadableStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            new(Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => 0, TaskScheduler.Default));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class CapturingLogger : ILogger<RelayLog>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }
}
