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
/// <b>What it watches</b>, per <see cref="RelaySandbox.WatchedRoots"/>: the content root; the relay's
/// temp directory (via <c>TMPDIR</c>, redirected into the sandbox, which is why this assembly runs
/// tests one at a time); the reserved key-ring root; <b>and the process's current directory and
/// <c>AppContext.BaseDirectory</c></b>, which is where a write that names no directory actually lands.
/// </para>
/// <para>
/// <b>Those last two are BUG-10 and they were the likely case, not an exotic one.</b> This comment
/// used to say only that the instrument could not see "an unrelated absolute path". That understated
/// it: <c>File.WriteAllText("relay.log", …)</c> names no directory at all, resolves against the
/// process's current directory, and was unwatched. Measured — a bare append in
/// <c>RelayLog.ConnectionOpened</c> put ten lines on disk while these six tests passed green. The
/// instrument was not broken; it was aimed slightly away from where the shot would come from.
/// </para>
/// <para>
/// <b>What it still cannot see, so that confidence matches reach.</b> A write to an unrelated absolute
/// path outside every watched root. A write from a background thread after the observer is disposed —
/// unprobed, and claimed for neither. Memory-mapped files and <c>O_TMPFILE</c> handles, which need not
/// raise a directory event at all — also unprobed. A file written by a process other than this one.
/// These are named rather than left implicit because the previous version of this list omitted the one
/// that turned out to matter.
/// </para>
/// <para>
/// <b>The build guard has the same hole, so neither one's coverage implies the other's.</b>
/// <c>NoDurableStorage</c> in the relay csproj fails the build on a persistence <i>package</i>, and it
/// is real — it was validated by adding <c>Serilog.Sinks.File</c> on purpose. It cannot catch a bare
/// <c>File.WriteAllText</c>, which needs no package. Two guards that look independent missing the same
/// shape is how a gap survives review, which is why the container root being read-only is the third
/// mechanism rather than a belt-and-braces flourish.
/// </para>
/// </remarks>
public sealed class RelayStoresNothingTests
{
    [Fact]
    public async Task RelayWritesNothingWhileCarryingAFullSession()
    {
        using var sandbox = new RelaySandbox();
        var before = FileSystemSnapshot.Of(sandbox.ContentRoot);
        using var observer = new WriteObserver([.. sandbox.WatchedRoots]);

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
        using var observer = new WriteObserver([.. sandbox.WatchedRoots]);

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
        using var observer = new WriteObserver([.. sandbox.WatchedRoots]);

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
    /// BUG-10: a write that names no directory at all is seen. <b>This is the likely sink, not an
    /// exotic one</b> — it is what <c>File.WriteAllText("relay.log", …)</c> does when nobody names a
    /// directory, and it resolves against the process's current directory rather than anywhere under
    /// the sandbox.
    /// </summary>
    /// <remarks>
    /// Fails if the watch list is ever narrowed back to the sandbox's own three roots, which is the
    /// regression this guards. <see cref="RelayWritesAreDetected"/> cannot catch that narrowing: it
    /// writes into the content root, so it stays green on exactly the instrument that let a bare write
    /// through. No relay is started here on purpose — the claim under test is the watch list's reach,
    /// and running a session would add a dependency without adding evidence.
    /// </remarks>
    [Fact]
    public Task AWriteThatNamesNoDirectoryIsDetected() =>
        AssertAmbientWriteIsSeen("bug10-bare-relative.log");

    /// <summary>
    /// BUG-10: the other place a naive write lands — beside the test assembly, via
    /// <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    /// <remarks>
    /// Separate from the bare-relative case rather than folded into it, because the defect being
    /// guarded was that <i>some</i> sinks were seen and others were not. Two facts stay individually
    /// attributable; one fact with two assertions would stop at the first miss and hide the second.
    /// </remarks>
    [Fact]
    public Task AWriteBesideTheTestAssemblyIsDetected() =>
        AssertAmbientWriteIsSeen(Path.Combine(AppContext.BaseDirectory, "bug10-base-directory.log"));

    private static async Task AssertAmbientWriteIsSeen(string path)
    {
        using var sandbox = new RelaySandbox();
        using var observer = new WriteObserver([.. sandbox.WatchedRoots]);
        Assert.Empty(observer.UnwatchedRoots);

        var full = Path.GetFullPath(path);
        try
        {
            await File.WriteAllTextAsync(full, "BCD-FGH joined");
            await WriteObserver.SettleAsync();

            // Checked before the detection assertion so that a write which never happened fails as
            // itself rather than as a blind spot.
            Assert.True(File.Exists(full), $"the probe never wrote its file, so this proves nothing: {full}");
            Assert.Contains(observer.Writes, write => write.Contains(Path.GetFileName(full), StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(full))
            {
                File.Delete(full);
            }
        }
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
