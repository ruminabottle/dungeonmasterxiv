using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// A-1.5e: start the relay we ship, run a full session through it, then assert it wrote nothing.
/// </summary>
/// <remarks>
/// <para>
/// PRD-1 says this is the criterion most likely to be skipped and the one that matters most in two
/// years, because R-1.7a ships users the words "no server storing your sessions" and this is the
/// only thing that keeps them true.
/// </para>
/// <para>
/// <b>Two assertions, because neither alone is enough.</b> The filesystem snapshot catches a write;
/// the restart catches persistence that a snapshot could miss by writing somewhere unwatched. A
/// relay that saved sessions to a database on another host would pass the first and fail the second.
/// </para>
/// <para>
/// <b>Two tenses, deliberately.</b> The snapshot answers "does a file remain"; the
/// <see cref="WriteObserver"/> answers "was anything ever written". Only the second sees a file that
/// is created and deleted again — a temp file, a lock, a key ring cleaned up on shutdown — and
/// "the relay stores nothing" is a claim about writing, not about what survives. The instrument was
/// originally scoped to the end state, which is the wrong tense for the criterion the PRD says
/// matters most in two years.
/// </para>
/// <para>
/// <b>What it watches.</b> The content root, the relay's temp directory (via <c>TMPDIR</c>, redirected
/// into the sandbox for the test, which is why this assembly runs tests one at a time), and the
/// data-protection key ring under the user profile — the three places an ASP.NET Core application
/// writes without being told to. It would not see a write to an unrelated absolute path, which is
/// why the csproj also refuses persistence packages at build time and the container root is
/// read-only: three mechanisms, because the one that fails is never the one you were watching.
/// </para>
/// </remarks>
public sealed class RelayStoresNothingTests
{
    [Fact]
    public async Task RelayWritesNothingWhileCarryingAFullSession()
    {
        using var sandbox = new RelaySandbox();
        var before = FileSystemSnapshot.Of(sandbox.ContentRoot);
        using var observer = new WriteObserver(sandbox.ContentRoot, sandbox.TempRoot, sandbox.KeyRingRoot);

        // A clean result is only evidence about the corpus the instrument actually read.
        Assert.Empty(observer.UnwatchedRoots);

        await using (var relay = await RelayUnderTest.StartAsync(sandbox.ContentRoot))
        {
            await FullSession.RunAsync(relay, SessionCode.FromValid("BCDFGH"));
        }

        await WriteObserver.SettleAsync();

        Assert.True(
            observer.Writes.Count == 0,
            "The relay wrote to disk while carrying a session, so A-1.5e fails and R-1.7a's shipped "
            + "copy that there is no server storing your sessions is false: "
            + string.Join("; ", observer.Writes));

        Assert.Empty(FileSystemSnapshot.Of(sandbox.ContentRoot).ChangesSince(before));
    }

    /// <summary>
    /// The gap the old instrument had: a file created and deleted again during the session. The
    /// snapshot cannot see it; the observer must.
    /// </summary>
    /// <remarks>
    /// This is the probe that shows the new instrument is strictly stronger rather than merely
    /// different — it asserts that the snapshot stays silent AND that the observer does not. A temp
    /// file, a lock file or a key ring written and cleaned up on shutdown all look exactly like this.
    /// </remarks>
    [Fact]
    public async Task ACreatedThenDeletedFileIsCaughtEvenThoughNothingRemains()
    {
        using var sandbox = new RelaySandbox();
        var before = FileSystemSnapshot.Of(sandbox.ContentRoot);
        using var observer = new WriteObserver(sandbox.ContentRoot, sandbox.TempRoot, sandbox.KeyRingRoot);

        await using (var relay = await RelayUnderTest.StartAsync(sandbox.ContentRoot))
        {
            await FullSession.RunAsync(relay, SessionCode.FromValid("BCDFGH"));

            var transient = Path.Combine(sandbox.ContentRoot, "keyring.tmp");
            await File.WriteAllTextAsync(transient, "written and then tidied away");
            File.Delete(transient);
        }

        await WriteObserver.SettleAsync();

        Assert.Empty(FileSystemSnapshot.Of(sandbox.ContentRoot).ChangesSince(before));
        Assert.Contains(observer.Writes, write => write.Contains("keyring.tmp", StringComparison.Ordinal));
    }

    /// <summary>
    /// The probe that makes the assertion above a check rather than a decoration: with a file
    /// written during the session, the same comparison must fail.
    /// </summary>
    /// <remarks>
    /// Run because "describe the input that makes this fail" is the standards' test for whether
    /// something is a check, and because a snapshot comparison that silently watched the wrong
    /// directory would pass forever. This is that input, executed rather than argued.
    /// </remarks>
    [Fact]
    public async Task RelayWritesAreDetected()
    {
        using var sandbox = new RelaySandbox();
        var before = FileSystemSnapshot.Of(sandbox.ContentRoot);
        using var observer = new WriteObserver(sandbox.ContentRoot, sandbox.TempRoot, sandbox.KeyRingRoot);

        await using (var relay = await RelayUnderTest.StartAsync(sandbox.ContentRoot))
        {
            await FullSession.RunAsync(relay, SessionCode.FromValid("BCDFGH"));

            // Exactly what a file-logging sink or a cache with a file backing would do.
            await File.WriteAllTextAsync(Path.Combine(sandbox.ContentRoot, "sessions.log"), "BCD-FGH joined");
        }

        await WriteObserver.SettleAsync();

        var changes = FileSystemSnapshot.Of(sandbox.ContentRoot).ChangesSince(before);

        Assert.True(changes.Count > 0, "The no-write assertion cannot detect a write, so it is not a check.");
        Assert.Contains(changes, change => change.Contains("sessions.log", StringComparison.Ordinal));
        Assert.Contains(observer.Writes, write => write.Contains("sessions.log", StringComparison.Ordinal));
    }

    /// <summary>
    /// No session state survives the process: a restarted relay has never heard of the code, so the
    /// same code is claimable again. This is what D-2 means by forwarding and forgetting, and what
    /// R-1.8 relies on when it says sessions re-establish from the DM's client.
    /// </summary>
    [Fact]
    public async Task RestartedRelayHasNoMemoryOfTheSession()
    {
        using var sandbox = new RelaySandbox();
        var code = SessionCode.FromValid("BCDFGH");

        await using (var first = await RelayUnderTest.StartAsync(sandbox.ContentRoot))
        {
            using var host = await first.ConnectAsync();
            await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(code));
            var (accepted, _) = await RelayUnderTest.ReceiveAsync(host);
            Assert.Equal(WireMessageType.CodeAccepted, accepted.Type);
        }

        await using var second = await RelayUnderTest.StartAsync(sandbox.ContentRoot);
        using var newHost = await second.ConnectAsync();
        await RelayUnderTest.SendAsync(newHost, WireEnvelope.ForCodeRequest(code));
        var (afterRestart, _) = await RelayUnderTest.ReceiveAsync(newHost);

        Assert.Equal(WireMessageType.CodeAccepted, afterRestart.Type);
    }

    /// <summary>
    /// The relay never learns a character name, because names live inside payloads it cannot open.
    /// Asserted against the forwarded bytes rather than argued from the design (D-8).
    /// </summary>
    [Fact]
    public async Task ForwardedTrafficCarriesNoReadableName()
    {
        using var sandbox = new RelaySandbox();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.ContentRoot);

        var result = await FullSession.RunAsync(relay, SessionCode.FromValid("BCDFGH"));

        Assert.DoesNotContain(
            FullSession.SecretMessage,
            Encoding.UTF8.GetString(result.ForwardedBytes),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The instrument says so when it could not watch what it was asked to watch.
    /// </summary>
    /// <remarks>
    /// Without this, pointing the no-write test at a key-ring path that does not exist yet would
    /// produce a green run over an empty corpus — a D-2 instrument reporting clean because it read
    /// nothing. The probe is the same shape as the create-then-delete one: prove the failure is
    /// reachable rather than assert the guarantee.
    /// </remarks>
    [Fact]
    public void ARootThatDoesNotExistIsReportedRatherThanSkippedSilently()
    {
        using var sandbox = new RelaySandbox();
        var missing = Path.Combine(sandbox.ContentRoot, "a-key-ring-nobody-created-yet");

        using var observer = new WriteObserver(sandbox.ContentRoot, missing);

        Assert.Equal([missing], observer.UnwatchedRoots);
    }
}
