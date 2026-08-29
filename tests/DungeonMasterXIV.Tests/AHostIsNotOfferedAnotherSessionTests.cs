using System;
using System.IO;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A live host is not offered "Start session" (R-1.3h, BUG-115).
/// </summary>
/// <remarks>
/// <para>
/// <b>The exclusivity guard was join-side only.</b> It asked whether this client had JOINED a
/// session and never whether it was HOSTING one, so the button that starts a session was one click
/// away from a host mid-session. Both outcomes of that click are bad and the Product Owner declined
/// to choose between them: either the audience persists while the host re-keys, leaving the table
/// live against a key pair it was never admitted under, or one click ejects the whole table
/// mid-combat with no confirmation. <b>The mis-click is removable, so it was removed instead.</b>
/// </para>
/// <para>
/// <b>ABSENT, not disabled</b>, which is the neighbouring comment's existing requirement and its
/// reason: a greyed control that still occupies the UI invites exactly the question the exclusivity
/// exists to remove.
/// </para>
/// <para>
/// <b>Two tests because there are two claims.</b> The behaviour test decides WHEN the predicate is
/// true, and can fail on a predicate that is always true — which is what "do not just widen the
/// guard into always-hidden" means. The source test decides that the BUTTON consults it, which no
/// behaviour test can reach: this assembly cannot construct the window.
/// </para>
/// </remarks>
public class AHostIsNotOfferedAnotherSessionTests
{
    // WHEN it is true. A predicate that answered "yes" always would hide the button forever and
    // satisfy any absence assertion, so the false cases are the load-bearing half here.
    [Fact]
    public void HostingIsLiveOnlyWhileThereIsASessionToProtect()
    {
        var coordinator = Coordinator();

        Assert.False(coordinator.InAHostedSession, "Before hosting, the offer must stand.");

        coordinator.StartHosting();
        Assert.True(coordinator.InAHostedSession, "Registering is already a session someone can lose.");

        coordinator.Host.Registered();
        Assert.True(coordinator.InAHostedSession, "Hosting is the case this exists for.");

        coordinator.StopHosting(new DateTimeOffset(2026, 8, 29, 7, 0, 0, TimeSpan.Zero));
        Assert.False(coordinator.InAHostedSession, "After stopping there is nothing to protect.");
    }

    // A failed attempt must NOT hide the button: there is no session to lose and the DM's next
    // action is to try again. This is the case that separates "hosting is live" from "hosting was
    // attempted".
    [Fact]
    public void AFailedAttemptStillOffersTheAction()
    {
        var coordinator = Coordinator();

        coordinator.StartHosting();
        coordinator.Fail(SessionFailure.RelayUnreachable);

        Assert.False(coordinator.InAHostedSession);
    }

    // THE HALF NO BEHAVIOUR TEST CAN REACH. The predicate being right is worth nothing if the
    // button does not consult it, and this assembly cannot construct the window.
    [Fact]
    public void TheStartButtonIsInsideAGuardThatConsultsBothSides()
    {
        var window = WindowSource();

        Assert.Contains("class SessionWindow", window, StringComparison.Ordinal);

        var guard = window
            .Split('\n')
            .SkipWhile(line => !line.Contains("ImGui.Button(\"Start session\")", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(guard);

        var above = window[..window.IndexOf("ImGui.Button(\"Start session\")", StringComparison.Ordinal)];

        Assert.Contains("InAJoinedSession()", above, StringComparison.Ordinal);
        Assert.True(
            above.Contains("InAHostedSession()", StringComparison.Ordinal),
            "The Start session button is reachable without consulting the hosting side. A live host "
            + "is one click from re-keying or ejecting its own table (R-1.3h, BUG-115).");
    }

    private static SessionCoordinator Coordinator() =>
        new(new SilentTransport(), () => RelayEndpoint.Default, GraceWindow.Default,
            log: SilentLog.Instance, capabilities: SessionCapabilities.Default);

    private static string WindowSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Windows", "SessionWindow.cs");

            if (File.Exists(candidate))
            {
                return string.Join(
                    "\n",
                    File.ReadAllLines(candidate)
                        .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            }
        }

        throw new InvalidOperationException("No Windows/SessionWindow.cs above the test binary.");
    }

    private sealed class SilentTransport : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) { _ = Failed; _ = Received; }
    }
}
