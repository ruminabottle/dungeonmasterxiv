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

    /// <summary>Starts watching <paramref name="roots"/>, recursively. Missing roots are watched
    /// by their parent, so a directory the relay creates is itself a write.</summary>
    public WriteObserver(params string[] roots)
    {
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
