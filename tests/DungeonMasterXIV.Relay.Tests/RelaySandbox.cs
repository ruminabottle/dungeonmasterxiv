namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// An isolated filesystem for one relay: its content root and its temp directory, plus a key-ring
/// root that is reserved rather than used — see <see cref="KeyRingRoot"/>.
/// </summary>
/// <remarks>
/// <b>Redirects <c>TMPDIR</c> for the life of the test</b>, so anything the relay writes to a temp
/// path lands somewhere watchable instead of in the shared system temp. That is process-wide, which
/// is why this assembly disables test parallelisation — see <c>AssemblyInfo.cs</c>. The alternative
/// was watching the real temp directory and filtering out other tests' files, and a no-write check
/// that filters is one step from not being a check.
/// </remarks>
public sealed class RelaySandbox : IDisposable
{
    private readonly string _root;
    private readonly string? _previousTempDirectory;

    /// <summary>Creates the sandbox and points the process's temp directory into it.</summary>
    public RelaySandbox()
    {
        _root = Path.Combine(Path.GetTempPath(), "dmx-relay-" + Guid.NewGuid().ToString("n"));
        ContentRoot = Path.Combine(_root, "content");
        TempRoot = Path.Combine(_root, "temp");
        KeyRingRoot = Path.Combine(_root, "keyring");

        Directory.CreateDirectory(ContentRoot);
        Directory.CreateDirectory(TempRoot);
        Directory.CreateDirectory(KeyRingRoot);

        _previousTempDirectory = Environment.GetEnvironmentVariable("TMPDIR");
        Environment.SetEnvironmentVariable("TMPDIR", TempRoot + Path.DirectorySeparatorChar);
    }

    /// <summary>Where the relay's host treats itself as rooted.</summary>
    public string ContentRoot { get; }

    /// <summary>Where <c>Path.GetTempPath()</c> resolves to while this exists.</summary>
    public string TempRoot { get; }

    /// <summary>
    /// Reserved for a data-protection key ring. <b>Nothing writes here, and nothing is expected to.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// BUG-11, and the comment is the fix rather than a wiring change. <b>Nothing in this solution uses
    /// ASP.NET DataProtection</b> — there is no <c>AddDataProtection</c> and no <c>PersistKeysTo</c>
    /// anywhere in <c>src</c> or <c>tests</c>, and no package reference to it. So this is a directory
    /// nothing will ever write to, which makes watching it a check that cannot fail. It was previously
    /// described as "the key ring under the user profile", which read as coverage of a real key ring to
    /// anyone auditing the watch list; it never was.
    /// </para>
    /// <para>
    /// <b>Where the real one would be, and why it is still unwatched.</b> A relay that called
    /// <c>AddDataProtection</c> without <c>PersistKeysTo</c> would write to
    /// <c>~/.aspnet/DataProtection-Keys</c>, which is outside this sandbox and is not on the watch
    /// list — that path does not exist on this machine, and <see cref="WriteObserver"/> reports a
    /// missing root as unwatched rather than watching its parent, so adding it would fail the
    /// <c>UnwatchedRoots</c> assertion rather than quietly cover nothing.
    /// </para>
    /// <para>
    /// <b>What would have to change for this to matter.</b> The relay would have to take a
    /// DataProtection dependency <i>and</i> be pointed here with <c>PersistKeysTo</c>. Until both are
    /// true this root is a placeholder held deliberately: it is kept, rather than deleted, so the next
    /// person to add DataProtection finds the watch already wired to the right place and finds this
    /// note explaining that the gap was considered. Deleting it would remove the misleading claim and
    /// the record along with it.
    /// </para>
    /// </remarks>
    public string KeyRingRoot { get; }

    /// <summary>
    /// Every root the no-write instrument must watch: this sandbox's three, plus the two directories a
    /// write that names no directory actually lands in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BUG-10. The sandbox roots alone cover the exotic case and miss the likely one. A <b>bare relative
    /// path</b> — <c>File.WriteAllText("relay.log", …)</c>, which is exactly what a sink writes when
    /// nobody names a directory — resolves against <see cref="Environment.CurrentDirectory"/>, and
    /// <see cref="AppContext.BaseDirectory"/> is the other place a naive write lands. Neither is under
    /// this sandbox. Measured rather than reasoned: with a bare <c>File.AppendAllText</c> in
    /// <c>RelayLog.ConnectionOpened</c>, the relay wrote a line on every connection — ten lines on disk
    /// — and <c>RelayStoresNothingTests</c> passed 6/6 green.
    /// </para>
    /// <para>
    /// Deduplicated, and the trailing separator normalised first, because under <c>dotnet test</c> the
    /// current directory and the base directory are normally the same path spelled two ways. Watching
    /// one directory twice would report every write twice and make a failure message read as though two
    /// files had been written.
    /// </para>
    /// <para>
    /// These two are process-wide rather than per-sandbox, which is another reason this assembly runs
    /// its tests one at a time — see <c>AssemblyInfo.cs</c>. A parallel test writing a file beside the
    /// test assembly would otherwise be indistinguishable from the relay doing it.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> WatchedRoots =>
    [
        .. new[] { ContentRoot, TempRoot, KeyRingRoot, Environment.CurrentDirectory, AppContext.BaseDirectory }
            .Select(root => Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)))
            .Distinct(StringComparer.Ordinal),
    ];

    /// <inheritdoc />
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("TMPDIR", _previousTempDirectory);

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a passing test over.
        }
    }
}
