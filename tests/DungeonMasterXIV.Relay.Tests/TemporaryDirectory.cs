namespace DungeonMasterXIV.Relay.Tests;

/// <summary>An empty directory that exists for the length of one test and is then removed.</summary>
public sealed class TemporaryDirectory : IDisposable
{
    /// <summary>Creates the directory.</summary>
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dmx-relay-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Where it is.</summary>
    public string Path { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a passing test over.
        }
    }
}
