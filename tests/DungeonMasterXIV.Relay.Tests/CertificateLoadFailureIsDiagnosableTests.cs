using System.Text.RegularExpressions;
using DungeonMasterXIV.Relay;
using DungeonMasterXIV.Relay.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// BUG-15: a certificate the relay cannot load must produce an error naming the file, the cause,
/// and — where that is what went wrong — the identity that could not read it.
/// </summary>
public sealed class CertificateLoadFailureIsDiagnosableTests(ITestOutputHelper output)
{
    /// <summary>
    /// The regression test. It goes through <see cref="RelayApp.Build"/> rather than calling
    /// <see cref="CertificateLoadFailure.Compose"/>, because the defect was not a missing message
    /// — it was that nothing wrapped the load at all, and only the wiring proves that is fixed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The certificate here is <b>malformed, not unreadable</b>, and the difference is load-bearing.
    /// An unreadable file raises <c>UnauthorizedAccessException</c> on macOS — which names the path
    /// already — while on Linux it is the bare <c>BIO routines::system lib</c> BUG-15 reports. A
    /// test built on chmod would therefore pass on a developer's machine without the fix and prove
    /// nothing. A malformed file fails the same way everywhere: no path, no reason.
    /// </para>
    /// <para>
    /// <b>It no longer asserts the uid, and that is BUG-17.</b> This file is readable, so a message
    /// naming a uid here would be the misleading output BUG-17 was filed about. The uid belongs to
    /// the permissions case and is asserted in the test below, on a file that really is unreadable.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFailedLoadNamesTheFileAndTheCause()
    {
        var directory = Directory.CreateTempSubdirectory("dmx-bug15");
        var certificate = Path.Combine(directory.FullName, "relay-certificate.pfx");
        File.WriteAllBytes(certificate, [0x00, 0x01, 0x02, 0x03]);

        var thrown = Assert.ThrowsAny<Exception>(() => RelayApp.Build(new RelayOptions
        {
            Port = 0,
            UseTls = true,
            CertificatePath = certificate,
            ContentRoot = directory.FullName,
        }));

        output.WriteLine(thrown.Message);

        Assert.Contains(certificate, thrown.Message, StringComparison.Ordinal);

        // The original is kept, not replaced: the operator still gets the crypto layer's own words,
        // and gets them first. See CertificateFailuresNameTheirOwnCauseTests for why "first" is the
        // assertion that matters.
        Assert.NotNull(thrown.InnerException);
        Assert.Contains(thrown.InnerException!.Message, thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A certificate this process genuinely may not read still names the uid, because that is the
    /// number the operator types into <c>chown</c>.
    /// </summary>
    /// <remarks>
    /// This is the branch BUG-15 exists for, now that BUG-17 has confined it to the case it is true
    /// of. Neither path through this test is a no-op: where file modes cannot be made to bite — on
    /// Windows, or as a user that bypasses them — the wording is asserted directly instead, so the
    /// test never reports a pass for a check that did not run.
    /// </remarks>
    [Fact]
    public void AnUnreadableCertificateNamesTheUid()
    {
        var directory = Directory.CreateTempSubdirectory("dmx-bug15");
        var certificate = Path.Combine(directory.FullName, "relay-certificate.pfx");
        File.WriteAllBytes(certificate, [0x00, 0x01, 0x02, 0x03]);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(certificate, UnixFileMode.None);
        }

        if (!CertificateLoadFailure.CannotBeRead(certificate))
        {
            output.WriteLine("File modes do not bite for this user; asserting the wording directly.");
            var composed = CertificateLoadFailure.Compose(
                certificate, "uid 1654", "error:10080002:BIO routines::system lib", cannotBeRead: true);

            Assert.Contains("uid 1654", composed, StringComparison.Ordinal);
            Assert.Contains("must be able to read", composed, StringComparison.Ordinal);
            return;
        }

        var thrown = Assert.ThrowsAny<Exception>(() => RelayApp.Build(new RelayOptions
        {
            Port = 0,
            UseTls = true,
            CertificatePath = certificate,
            ContentRoot = directory.FullName,
        }));

        output.WriteLine(thrown.Message);

        Assert.Contains(certificate, thrown.Message, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"uid \d+"), thrown.Message);
        Assert.Contains("must be able to read", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>The message names all three of the things an operator needs, not merely one.</summary>
    [Fact]
    public void TheMessageCarriesThePathTheIdentityAndTheUnderlyingError()
    {
        var message = CertificateLoadFailure.Compose(
            "/run/secrets/relay-certificate",
            "uid 1654",
            "error:10080002:BIO routines::system lib",
            cannotBeRead: true);

        Assert.Contains("/run/secrets/relay-certificate", message, StringComparison.Ordinal);
        Assert.Contains("uid 1654", message, StringComparison.Ordinal);
        Assert.Contains("error:10080002:BIO routines::system lib", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The identity is the uid the process actually has, because that is the number the operator
    /// types into <c>chown</c>. A user name would not be actionable and neither would a build-time
    /// constant; see <see cref="CertificateLoadFailure.CurrentIdentity"/>.
    /// </summary>
    [Fact]
    public void TheIdentityIsAUidWhereThereIsOneToAskFor()
    {
        var identity = CertificateLoadFailure.CurrentIdentity();

        // Asserted on every platform rather than returning early off Unix: a test that quietly does
        // nothing where it cannot do the real check still reports as a pass and is counted as cover.
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            Assert.Matches(new Regex(@"^uid \d+$"), identity);
            return;
        }

        Assert.Matches(new Regex(@"^user '.+'$"), identity);
    }
}
