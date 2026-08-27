namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// Every file under a directory, with its size and last-write time, so a later snapshot can be
/// compared against it.
/// </summary>
/// <remarks>
/// Size and timestamp are captured as well as the path because "wrote nothing" has to mean nothing
/// was appended to an existing file, not merely that no new file appeared. A log that grows is a
/// log.
/// </remarks>
public sealed class FileSystemSnapshot
{
    private readonly Dictionary<string, (long Length, DateTime LastWrite)> _entries;

    private FileSystemSnapshot(Dictionary<string, (long, DateTime)> entries) => _entries = entries;

    /// <summary>Records the current contents of <paramref name="root"/>, recursively.</summary>
    public static FileSystemSnapshot Of(string root)
    {
        var entries = new Dictionary<string, (long, DateTime)>(StringComparer.Ordinal);

        if (Directory.Exists(root))
        {
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(path);
                entries[path] = (info.Length, info.LastWriteTimeUtc);
            }
        }

        return new FileSystemSnapshot(entries);
    }

    /// <summary>
    /// Files that appeared, grew, or were rewritten since <paramref name="earlier"/>. Empty means
    /// nothing was written.
    /// </summary>
    public IReadOnlyList<string> ChangesSince(FileSystemSnapshot earlier)
    {
        ArgumentNullException.ThrowIfNull(earlier);

        var changes = new List<string>();

        foreach (var (path, current) in _entries)
        {
            if (!earlier._entries.TryGetValue(path, out var before))
            {
                changes.Add($"created: {path} ({current.Length} bytes)");
            }
            else if (before != current)
            {
                changes.Add($"modified: {path} ({before.Length} -> {current.Length} bytes)");
            }
        }

        return changes;
    }
}
