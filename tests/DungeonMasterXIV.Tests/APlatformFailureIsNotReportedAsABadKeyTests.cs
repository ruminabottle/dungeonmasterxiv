using System;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Org.BouncyCastle.Crypto;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-61's second half: <c>CanAgreeWith</c> must tell "this key is bad" from "this platform cannot
/// do this".
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the defect that a partial fix would have created, and it is worse than the crash it
/// replaces.</b> The validator used to wrap the probe-key CONSTRUCTION in the same <c>try</c> as the
/// import of the peer's bytes, under a <c>catch</c> that reads any failure as malformed input. A
/// platform failure raises the same exception type — so on an affected machine every key, including
/// perfectly valid ones, was reported as bad, and nothing was logged.
/// </para>
/// <para>
/// <b>The crash at construction was hiding it.</b> Fix only the line in the stack trace and the loud
/// failure becomes a silent one: every peer refused, no error anywhere, on a build that passes every
/// test on a Windows CI.
/// </para>
/// <para>
/// So the platform half is injected here and made to fail on demand. There is no other way to
/// produce a platform failure on a machine where the platform works.
/// </para>
/// </remarks>
public class APlatformFailureIsNotReportedAsABadKeyTests
{
    private sealed class TheProviderIsUnavailable : Exception
    {
        public TheProviderIsUnavailable()
            : base("Stand-in for the key-storage provider failing, as it does under the wrapper.")
        {
        }
    }

    // THE CENTRAL ONE. A valid key plus a broken platform must NOT come back as "bad key".
    [Fact]
    public void AFailureToGenerateTheProbeKeyIsNotAVerdictOnTheKey()
    {
        var perfectlyGoodKey = AValidPublicKey();

        Assert.Throws<TheProviderIsUnavailable>(
            () => SessionKeyExchange.CanAgreeWith(
                perfectlyGoodKey, () => throw new TheProviderIsUnavailable()));
    }

    // The positive control: the same key, with the platform working, is accepted. Without this the
    // test above would pass against a CanAgreeWith that rejected everything.
    [Fact]
    public void TheSameKeyIsAcceptedWhenThePlatformWorks()
    {
        Assert.True(SessionKeyExchange.CanAgreeWith(AValidPublicKey()));
    }

    // And bad input is still refused quietly rather than thrown, which is BUG-56's requirement and
    // must survive the narrowing. A throwing validator on the inbound join path is a denial of
    // service that any stranger can trigger.
    [Theory]
    [InlineData(new byte[] { 1, 2, 3 })]                 // junk: measured to raise EndOfStreamException
    [InlineData(new byte[] { 0 })]                       // one byte: measured to raise IOException
    [InlineData(new byte[0])]
    public void MalformedBytesAreRefusedWithoutThrowing(byte[] malformed)
    {
        Assert.False(SessionKeyExchange.CanAgreeWith(malformed));
    }

    [Fact]
    public void NullIsRefusedWithoutThrowing() => Assert.False(SessionKeyExchange.CanAgreeWith(null));

    // An RSA key parses successfully into a type that is not EC, so it is refused by TYPE rather
    // than by a failed parse. Measured; a cast would have thrown here instead.
    [Fact]
    public void AWellFormedKeyOfTheWrongKindIsRefused()
    {
        using var rsa = RSA.Create(2048);

        Assert.False(SessionKeyExchange.CanAgreeWith(rsa.ExportSubjectPublicKeyInfo()));
    }

    // A well-formed key on the WRONG CURVE imports cleanly and can only be refused by attempting the
    // agreement -- which is why this validator does the operation rather than inspecting the bytes.
    [Theory]
    [InlineData("nistP384")]
    [InlineData("nistP521")]
    public void AKeyOnAnotherCurveIsRefused(string curveName)
    {
        using var other = ECDiffieHellman.Create(ECCurve.CreateFromFriendlyName(curveName));

        Assert.False(SessionKeyExchange.CanAgreeWith(other.PublicKey.ExportSubjectPublicKeyInfo()));
    }

    // The measured ordering, held open as a test rather than left as a comment: junk bytes must be
    // refused WITHOUT paying for a key pair. Generating first made three junk bytes cost almost as
    // much as a real key -- the cheapest attacker input, on the inbound join path.
    [Fact]
    public void JunkBytesAreRefusedBeforeAnyKeyIsGenerated()
    {
        var generated = 0;

        var accepted = SessionKeyExchange.CanAgreeWith(
            new byte[] { 1, 2, 3 },
            () => { generated++; throw new TheProviderIsUnavailable(); });

        Assert.False(accepted);
        Assert.Equal(0, generated);
    }

    private static byte[] AValidPublicKey()
    {
        using var peer = new SessionKeyExchange();

        return peer.PublicKey;
    }
}
