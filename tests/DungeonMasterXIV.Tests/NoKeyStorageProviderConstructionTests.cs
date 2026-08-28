using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-61's completion condition: no construction in Core may depend on the key-storage provider,
/// so a fifth site added later fails rather than silently reintroducing the bug.
/// </summary>
/// <remarks>
/// <para>
/// <b>This reads the COMPILED ASSEMBLY, not the source, and that is what makes it a universal.</b>
/// A .NET assembly records every externally-referenced type in its metadata, so a
/// <c>System.Security.Cryptography</c> EC type used anywhere in Core — in any file, under a
/// <c>using</c> alias, in a nested helper, inside a lambda — appears here. A source scan would have
/// had to enumerate spellings and would have grown a hole per spelling it did not think of, in a
/// file whose own commentary quotes the very names it must refuse.
/// </para>
/// <para>
/// <b>The four construction sites were the ticket's definition of done, and enumerating four is
/// exactly the shape that goes stale.</b> The requirement is not "those four changed"; it is that
/// none of them comes back.
/// </para>
/// <para>
/// <b>What this deliberately does NOT forbid: the rest of</b> <c>System.Security.Cryptography</c>.
/// HKDF, SHA-256, AES and <c>CryptographicOperations</c> are unaffected by BUG-61 — the failure is
/// specific to EC through the provider — and the KDF is on the wire, so forbidding it would demand
/// the change the ticket forbids.
/// </para>
/// </remarks>
public class NoKeyStorageProviderConstructionTests
{
    private const string BclCryptography = "System.Security.Cryptography";

    [Fact]
    public void CoreReferencesNoEllipticCurveTypeFromTheProviderBackedApi()
    {
        var offenders = ReferencedTypes()
            .Where(type => type.Namespace == BclCryptography && DependsOnTheProvider(type.Name))
            .Select(type => $"{type.Namespace}.{type.Name}")
            .Distinct()
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "DungeonMasterXIV.Core references key-storage-provider-backed types, which BUG-61 "
            + "showed cannot work under the Wine prefix at all:\n  " + string.Join("\n  ", offenders));
    }

    // THE VACUITY CONTROL, and without it every assertion here is worthless: a scanner that read
    // nothing, or read the wrong file, would report zero offenders and pass forever. Core provably
    // DOES use HKDF -- the KDF the ticket forbids changing -- so the scan must find it.
    [Fact]
    public void TheScanIsReadingRealMetadata()
    {
        var cryptography = ReferencedTypes().Where(type => type.Namespace == BclCryptography).ToList();

        Assert.NotEmpty(cryptography);
        Assert.Contains(cryptography, type => type.Name == "HKDF");
    }

    // THE POSITIVE CONTROL on the predicate itself. The list of provider-backed spellings is the one
    // hand-written thing here, so each entry is shown to be caught -- otherwise a typo would read as
    // a working exclusion, which is the failure mode this whole file exists to prevent.
    [Theory]
    [InlineData("ECDiffieHellman")]
    [InlineData("ECDiffieHellmanCng")]
    [InlineData("ECDsa")]
    [InlineData("ECDsaCng")]
    [InlineData("ECCurve")]
    [InlineData("ECParameters")]
    [InlineData("CngKey")]
    [InlineData("CngAlgorithm")]
    public void ThePredicateCatchesEachProviderBackedSpelling(string name) =>
        Assert.True(DependsOnTheProvider(name), $"{name} would not have been caught.");

    // And the negative control: it must stay silent on the cryptography Core legitimately uses, or
    // the guard fails for a reason that has nothing to do with BUG-61 and gets weakened in a hurry.
    [Theory]
    [InlineData("HKDF")]
    [InlineData("SHA256")]
    [InlineData("Aes")]
    [InlineData("AesGcm")]
    [InlineData("CryptographicOperations")]
    [InlineData("CryptographicException")]
    [InlineData("RandomNumberGenerator")]
    [InlineData("HashAlgorithmName")]
    public void ThePredicateIsSilentOnTheCryptographyWeStillUse(string name) =>
        Assert.False(DependsOnTheProvider(name), $"{name} would have been flagged wrongly.");

    // The fix, stated positively: the managed implementation IS what Core now depends on. If someone
    // removed BouncyCastle and went back to the BCL, the test above would catch it -- but this says
    // out loud which way the dependency points, so a green suite is not merely an absence.
    [Fact]
    public void TheManagedImplementationIsWhatCoreDependsOnInstead()
    {
        var bouncyCastle = ReferencedTypes()
            .Where(type => type.Namespace.StartsWith("Org.BouncyCastle", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(bouncyCastle);
        Assert.Contains(bouncyCastle, type => type.Name == "ECKeyPairGenerator");
    }

    /// <summary>
    /// Whether a <c>System.Security.Cryptography</c> type name is one of the provider-backed EC
    /// entry points. <c>Cng</c> is the provider itself; the <c>EC</c> prefix covers the rest.
    /// </summary>
    private static bool DependsOnTheProvider(string name) =>
        name.StartsWith("EC", StringComparison.Ordinal)
        || name.Contains("Cng", StringComparison.Ordinal);

    private static IReadOnlyList<(string Namespace, string Name)> ReferencedTypes()
    {
        var assembly = typeof(SessionKeyExchange).Assembly.Location;

        Assert.True(File.Exists(assembly), $"No compiled Core assembly at '{assembly}' to read.");

        using var stream = File.OpenRead(assembly);
        using var portableExecutable = new PEReader(stream);
        var metadata = portableExecutable.GetMetadataReader();

        return metadata.TypeReferences
            .Select(metadata.GetTypeReference)
            .Select(reference => (
                Namespace: metadata.GetString(reference.Namespace),
                Name: metadata.GetString(reference.Name)))
            .ToList();
    }
}
