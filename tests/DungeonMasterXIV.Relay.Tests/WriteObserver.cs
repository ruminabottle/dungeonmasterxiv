namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// Watches directories and records every file write that happens while it is running.
/// </summary>
/// <remarks>
/// <para>
/// <b>This measures a different tense from a before-and-after snapshot, and the difference is the
/// point.</b> A snapshot answers "does a file remain", which is silent about a file that was created
/// and deleted again — a temp file, a lock file, a key ring written and cleaned up. A-1.5e is the
/// claim that the relay <i>stores nothing</i>, and R-1.7a ships that claim to users in those words,
/// so the question it has to answer is whether anything was ever <i>written</i>.
/// </para>
/// <para>
/// Kept alongside the snapshot rather than replacing it: the snapshot still catches a file that
/// appears without an event reaching us, and the two failing modes are different enough that neither
/// subsumes the other.
/// </para>
/// </remarks>
public sealed class WriteObserver : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Lock _gate = new();
    private readonly List<string> _writes = [];

    /// <summary>
    /// Starts watching <paramref name="roots"/>, recursively.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A root that does not exist is NOT watched, and is reported in
    /// <see cref="UnwatchedRoots"/> rather than skipped quietly.</b> An earlier version of this
    /// comment claimed missing roots were watched via their parent; the code filtered them out
    /// instead, so the two disagreed. Harmless while every root was pre-created, and live the moment
    /// anyone pointed it at a real key-ring path that did not exist yet — at which point this
    /// instrument would have watched nothing and reported clean, which is a D-2 check passing over an
    /// empty corpus.
    /// </para>
    /// <para>
    /// The comment was the wrong half to keep. Watching a missing root's nearest existing ancestor
    /// means recursively watching whatever that turns out to be — for a user-profile key-ring path,
    /// the home directory — which is a real hazard traded for a narrow gain. So the behaviour stands
    /// and the silence goes: callers assert <see cref="UnwatchedRoots"/> is empty, which turns
    /// "I did not read that" from an invisible gap into a failing test.
    /// </para>
    /// </remarks>
    public WriteObserver(params string[] roots)
    {
        UnwatchedRoots = roots.Where(root => !Directory.Exists(root)).ToArray();

        foreach (var root in roots.Where(Directory.Exists))
        {
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            };

            watcher.Created += (_, e) => Record("created", e.FullPath);
            watcher.Changed += (_, e) => Record("changed", e.FullPath);
            watcher.Renamed += (_, e) => Record("renamed", e.FullPath);
            watcher.EnableRaisingEvents = true;

            _watchers.Add(watcher);
        }
    }

    /// <summary>
    /// Roots this was asked to watch and could not, because they did not exist when it started.
    /// </summary>
    /// <remarks>
    /// A caller that does not check this is trusting a result about a corpus that may be empty. The
    /// tests assert it is empty for exactly that reason — a clean run is only evidence about what
    /// the instrument actually read.
    /// </remarks>
    public IReadOnlyList<string> UnwatchedRoots { get; }

    /// <summary>Everything written since this began, whether or not it still exists.</summary>
    public IReadOnlyList<string> Writes
    {
        get
        {
            lock (_gate)
            {
                return _writes.ToArray();
            }
        }
    }

    /// <summary>
    /// Gives the file system time to deliver events already queued. Filesystem notifications are
    /// asynchronous, so reading <see cref="Writes"/> immediately after an action can miss one — the
    /// direction that would make this instrument silently weaker rather than noisier.
    /// </summary>
    public static Task SettleAsync() => Task.Delay(400);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }

    private void Record(string what, string path)
    {
        lock (_gate)
        {
            _writes.Add($"{what}: {path}");
        }
    }
}
