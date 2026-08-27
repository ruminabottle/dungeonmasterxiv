using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using DungeonMasterXIV.Relay;
using DungeonMasterXIV.Relay.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Xunit;
using Xunit.Abstractions;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// BUG-17: a load failure must name the cause it has evidence for, and no other.
/// </summary>
public sealed class CertificateFailuresNameTheirOwnCauseTests(ITestOutputHelper output)
{
    /// <summary>
    /// The control, and it runs first for a reason: it proves this harness can produce a success,
    /// so the failure in the test below is attributable to the password and not to the fixture.
    /// </summary>
    [Fact]
    public async Task TheRightPasswordLoadsTheCertificate()
    {
        var directory = Directory.CreateTempSubdirectory("dmx-bug17");
        var certificate = MintPasswordProtected(directory.FullName, "correct");

        var app = RelayApp.Build(new RelayOptions
        {
            Port = 0,
            UseTls = true,
            CertificatePath = certificate,
            CertificatePassword = "correct",
            ContentRoot = directory.FullName,
        });

        await app.DisposeAsync();
    }

    /// <summary>
    /// A wrong password on a valid, readable, correctly-owned certificate must not be reported as
    /// a permissions problem.
    /// </summary>
    /// <remarks>
    /// An assertion of ABSENCE, which is the shape BUG-15's tests could not have. They assert the
    /// message contains the path, the uid and the underlying error — and asserting that a message
    /// contains the right things cannot detect that it also asserts a wrong thing. All three passed
    /// on output telling the operator to chown a file whose ownership was already correct.
    /// </remarks>
    [Fact]
    public void AWrongPasswordIsNotReportedAsAPermissionsProblem()
    {
        var directory = Directory.CreateTempSubdirectory("dmx-bug17");
        var certificate = MintPasswordProtected(directory.FullName, "correct");

        var thrown = Assert.ThrowsAny<Exception>(() => RelayApp.Build(new RelayOptions
        {
            Port = 0,
            UseTls = true,
            CertificatePath = certificate,
            CertificatePassword = "wrong",
            ContentRoot = directory.FullName,
        }));

        output.WriteLine(thrown.Message);

        Assert.Contains("password", thrown.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(new Regex(@"uid \d+"), thrown.Message);
        Assert.DoesNotContain("must be able to read", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("widening its mode", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The corrupt-file half of BUG-17. A malformed certificate this process can read perfectly
    /// well is not a permissions problem either, and the shipped message said it was.
    /// </summary>
    [Fact]
    public void AMalformedButReadableCertificateIsNotReportedAsAPermissionsProblem()
    {
        var directory = Directory.CreateTempSubdirectory("dmx-bug17");
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

        Assert.DoesNotMatch(new Regex(@"uid \d+"), thrown.Message);
        Assert.DoesNotContain("must be able to read", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Where the permissions advice DOES belong, the cause still comes first.
    /// </summary>
    /// <remarks>
    /// Position, not presence, is what BUG-17 was about: the accurate clause was in the message all
    /// along, sixty-five words down, and an operator who reads top-down had already acted.
    /// </remarks>
    [Fact]
    public void EvenInThePermissionsCaseTheCauseLeads()
    {
        var message = CertificateLoadFailure.Compose(
            "/run/secrets/relay-certificate",
            "uid 1654",
            "error:10080002:BIO routines::system lib",
            cannotBeRead: true);

        var cause = message.IndexOf("error:10080002", StringComparison.Ordinal);
        var advice = message.IndexOf("The relay runs as", StringComparison.Ordinal);

        Assert.True(cause >= 0 && advice >= 0, "Both clauses must be present.");
        Assert.True(cause < advice, $"The cause must lead. Cause at {cause}, advice at {advice}.");
    }

    /// <summary>A real PKCS#12, minted in-process so the suite needs no openssl on the path.</summary>
    private static string MintPasswordProtected(string directory, string password)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var path = Path.Combine(directory, "real.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, password));
        return path;
    }
}
