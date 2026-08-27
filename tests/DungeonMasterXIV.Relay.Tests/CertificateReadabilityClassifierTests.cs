using DungeonMasterXIV.Relay;
using DungeonMasterXIV.Relay.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// Direct tests of <see cref="CertificateLoadFailure.CannotBeRead"/>, the classifier that decides
/// whether the permissions advice is true enough to print.
/// </summary>
/// <remarks>
/// <para>
/// <b>BUG-20: nothing tested this against a real file, and the suite could not fail without it.</b>
/// The end-to-end test of BUG-15's guarantee reaches its real branch only when the classifier
/// returns <c>true</c> — so the function under test chose which branch tested it, and blinding the
/// classifier made that test quietly take a fallback that asserts a pure function called with the
/// answer hard-coded. One line changed, and the whole relay suite stayed byte-identical to green
/// while a genuinely unreadable certificate produced no advice at all.
/// </para>
/// <para>
/// These are deliberately <b>direct</b>. A test routed through <see cref="RelayApp.Build"/> would
/// inherit the same problem: whatever the classifier says becomes the premise of the assertion.
/// </para>
/// </remarks>
public sealed class CertificateReadabilityClassifierTests
{
    /// <summary>
    /// <b>The gate BUG-20 found missing.</b> A file this process genuinely may not read is
    /// classified as unreadable.
    /// </summary>
    /// <remarks>
    /// <b>Attributed rather than guarded with <c>if (!OperatingSystem.IsWindows())</c>, and the
    /// difference is not cosmetic.</b> CA1416 accepts either, and the probe below does use the
    /// <c>if</c> — but there it is live logic that decides the answer on Windows. Here the guarded
    /// body would be dead code, unreachable because <see cref="FileModesBiteFactAttribute"/> skips
    /// this test wherever modes do not bite, and the only thing it could ever do if reached is
    /// assert that a readable file is unreadable. The attribute states the contract the skip
    /// already enforces; the <c>if</c> would state a branch that must never run.
    /// </remarks>
    [FileModesBiteFact]
    [UnsupportedOSPlatform("windows")]
    public void AnUnreadableFileIsClassifiedAsUnreadable()
    {
        var directory = Directory.CreateTempSubdirectory("dmx-bug20");
        var file = Path.Combine(directory.FullName, "relay-certificate.pfx");
        File.WriteAllBytes(file, [0x00]);
        File.SetUnixFileMode(file, UnixFileMode.None);

        Assert.True(
            CertificateLoadFailure.CannotBeRead(file),
            "A file with no mode bits set must be classified unreadable, or BUG-15's advice never prints.");
    }

    /// <summary>
    /// The control for the test above, and it is not optional: a classifier hard-wired to
    /// <c>true</c> would satisfy that assertion and print the chown advice for everything, which is
    /// BUG-17 all over again.
    /// </summary>
    [Fact]
    public void AReadableFileIsNotClassifiedAsUnreadable()
    {
        var directory = Directory.CreateTempSubdirectory("dmx-bug20");
        var file = Path.Combine(directory.FullName, "relay-certificate.pfx");
        File.WriteAllBytes(file, [0x00]);

        Assert.False(CertificateLoadFailure.CannotBeRead(file));
    }

    /// <summary>
    /// BUG-21. A path pointing at a directory is the wrong kind of thing, not a refusal.
    /// </summary>
    /// <remarks>
    /// The realistic trigger is <c>/run/secrets</c> where <c>/run/secrets/relay-certificate</c> was
    /// meant. On Unix <see cref="File.OpenRead"/> throws <see cref="UnauthorizedAccessException"/>
    /// for a directory — the same type an outright refusal throws — so the exception type cannot
    /// separate the two and the <see cref="IOException"/> arm never sees it.
    /// </remarks>
    [Fact]
    public void ADirectoryIsNotAPermissionsFinding()
    {
        var directory = Directory.CreateTempSubdirectory("dmx-bug21");

        Assert.False(CertificateLoadFailure.CannotBeRead(directory.FullName));
    }

    /// <summary>A path that does not exist is not a permissions finding either.</summary>
    [Fact]
    public void AMissingFileIsNotAPermissionsFinding()
    {
        var directory = Directory.CreateTempSubdirectory("dmx-bug21");

        Assert.False(CertificateLoadFailure.CannotBeRead(Path.Combine(directory.FullName, "absent.pfx")));
    }

    /// <summary>
    /// And end to end: the operator pointed at a directory is not told to chown it.
    /// </summary>
    [Fact]
    public void ADirectoryDoesNotGetThePermissionsAdvice()
    {
        var directory = Directory.CreateTempSubdirectory("dmx-bug21");

        var thrown = Assert.ThrowsAny<Exception>(() => RelayApp.Build(new RelayOptions
        {
            Port = 0,
            UseTls = true,
            CertificatePath = directory.FullName,
            ContentRoot = directory.FullName,
        }));

        Assert.Contains(directory.FullName, thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"uid \d+"), thrown.Message);
        Assert.DoesNotContain("must be able to read", thrown.Message, StringComparison.Ordinal);
    }
}

/// <summary>
/// A fact that runs only where Unix file modes actually restrict this process, and says so loudly
/// when they do not.
/// </summary>
/// <remarks>
/// Not a silent early return. A test that quietly does nothing where it cannot do the real check
/// still reports as a pass and is counted as coverage — which is the shape of BUG-20 itself. Modes
/// do not bite on Windows, and do not bite for a user that bypasses them, so the condition is
/// measured rather than assumed from the platform.
/// </remarks>
public sealed class FileModesBiteFactAttribute : FactAttribute
{
    /// <summary>Skips the test, with the reason, where an unreadable file cannot be produced.</summary>
    public FileModesBiteFactAttribute()
    {
        if (!FileModesBite.Value)
        {
            Skip = "Unix file modes do not restrict this process — Windows, or a user that bypasses "
                 + "them such as root — so a genuinely unreadable file cannot be created here.";
        }
    }

    /// <summary>Whether a file with no mode bits set is actually refused to this process.</summary>
    private static readonly Lazy<bool> FileModesBite = new(() =>
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        var directory = Directory.CreateTempSubdirectory("dmx-modes");
        var file = Path.Combine(directory.FullName, "probe");
        File.WriteAllBytes(file, [0x00]);
        File.SetUnixFileMode(file, UnixFileMode.None);

        try
        {
            using var stream = File.OpenRead(file);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    });
}
