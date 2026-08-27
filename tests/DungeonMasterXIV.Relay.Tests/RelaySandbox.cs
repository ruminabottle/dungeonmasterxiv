namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// An isolated filesystem for one relay: its content root, its temp directory, and the place an
/// ASP.NET Core application would put a data-protection key ring.
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

    /// <summary>Stands in for the data-protection key ring location.</summary>
    public string KeyRingRoot { get; }

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
