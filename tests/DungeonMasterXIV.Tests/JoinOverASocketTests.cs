using System;
using System.Threading.Tasks;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>A joiner is admitted over a real socket and ends up holding the session key.</summary>
public class JoinOverASocketTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 7, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AJoinCompletesAcrossARealSocket()
    {
        await using var server = new TestWebSocketServer();
        using var transport = new WebSocketSessionTransport(new SilentLog());
        var coordinator = new SessionCoordinator(transport, () => server.Address.ToString(), GraceWindow.Default, log: SilentLog.Instance, capabilities: SessionCapabilities.Default);
        using var host = new SessionKeyExchange();

        // The coordinator dials the socket itself; calling Connect here too would open a second one.
        coordinator.RequestJoin(Code);
        await server.Connected.WaitAsync(Patience);
        coordinator.Join.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));

        await server.SendAsync(EnvelopeCodec.Encode(
            WireEnvelope.ForJoinAccepted(Code, coordinator.Membership.Keys!.PublicKey, host.PublicKey)));

        await WaitForAsync(() =>
        {
            coordinator.Tick(TimeSpan.Zero, Now);
            return coordinator.Join.Phase == JoinPhase.Admitted;
        });

        Assert.Equal(JoinPhase.Admitted, coordinator.Join.Phase);
        Assert.Equal(
            host.DeriveSharedKey(coordinator.Membership.Keys!.PublicKey, Code),
            coordinator.Membership.SessionKey);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Condition was never met within the timeout.");
    }
}
