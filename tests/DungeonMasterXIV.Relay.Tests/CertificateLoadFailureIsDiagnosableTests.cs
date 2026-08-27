using System.Text.RegularExpressions;
using DungeonMasterXIV.Relay;
using DungeonMasterXIV.Relay.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// BUG-15: a certificate the relay cannot load must produce an error naming the file and the
/// identity that could not read it.
/// </summary>
public sealed class CertificateLoadFailureIsDiagnosableTests(ITestOutputHelper output)
{
    /// <summary>
    /// The regression test. It goes through <see cref="RelayApp.Build"/> rather than calling
    /// <see cref="CertificateLoadFailure.Describe"/>, because the defect was not a missing message
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
    /// <c>uid \d+</c> is matched rather than compared against
    /// <see cref="CertificateLoadFailure.CurrentIdentity"/>, so a <c>CurrentIdentity</c> that
    /// returned nothing useful could not satisfy both this and the message at once.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFailedLoadNamesTheFileAndTheUid()
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
        Assert.Matches(new Regex(@"uid \d+"), thrown.Message);
        Assert.Contains("must be able to read", thrown.Message, StringComparison.Ordinal);

        // The original is kept, not replaced: the operator still gets the crypto layer's own words.
        Assert.NotNull(thrown.InnerException);
    }

    /// <summary>The message names all three of the things an operator needs, not merely one.</summary>
    [Fact]
    public void TheMessageCarriesThePathTheIdentityAndTheUnderlyingError()
    {
        var message = CertificateLoadFailure.Describe(
            "/run/secrets/relay-certificate",
            "uid 1654",
            "error:10080002:BIO routines::system lib");

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
