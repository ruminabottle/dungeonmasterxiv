using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// A-1.5f, the relay's half: what reaches the relay is ciphertext, and the relay holds no key.
/// </summary>
/// <remarks>
/// <para>
/// <b>These assertions decode before they look.</b> C1 shipped a check that scanned raw envelope
/// bytes for plaintext and could not fail, because <c>EnvelopeCodec</c> writes byte arrays as
/// base64 and base64 hid the very bytes it was hunting. So the payload is base64-decoded by
/// <c>TryDecode</c> before anything is asserted about it, and the raw-bytes scan below is kept as a
/// second, weaker check rather than the only one.
/// </para>
/// <para>
/// The relay holding no key is asserted structurally as well: nothing in the relay's own code
/// references <see cref="SessionCipher"/> or <see cref="SessionKeyExchange"/>, and a payload only
/// ever passes through it as the bytes that arrived.
/// </para>
/// </remarks>
public sealed class RelayCarriesCiphertextTests
{
    private static readonly byte[] SecretBytes = Encoding.UTF8.GetBytes(FullSession.SecretMessage);

    [Fact]
    public async Task ForwardedPayloadIsCiphertextAfterDecoding()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        var result = await FullSession.RunAsync(relay, SessionCode.FromValid("BCDFGH"));
        var payload = result.ForwardedEnvelope.Payload;

        Assert.NotNull(payload);
        Assert.False(
            ContainsSequence(payload!, SecretBytes),
            "The decoded payload the relay forwarded contains the plaintext, so it is not encrypted.");
    }

    [Fact]
    public async Task ForwardedRawBytesDoNotContainPlaintext()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        var result = await FullSession.RunAsync(relay, SessionCode.FromValid("BCDFGH"));

        Assert.False(ContainsSequence(result.ForwardedBytes, SecretBytes));
    }

    /// <summary>
    /// The probe: substitute a cipher that does not encrypt, and the decoded-payload assertion must
    /// fail. Without this, both tests above would pass against a relay forwarding plaintext.
    /// </summary>
    /// <remarks>
    /// <b>This validates the suite against THIS substitution and nothing else.</b> The Code Reviewer
    /// measured on PR #4 that a crude null cipher trips five tests while a careful one preserving
    /// tag length evades some of them, so the honest claim is narrow: a payload that is exactly the
    /// plaintext is caught. A substitution that padded to the tag length would defeat a
    /// length-based check, which is why no assertion here is length-based.
    /// </remarks>
    [Fact]
    public async Task NullCipherSubstitutionIsCaught()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        var code = SessionCode.FromValid("BCDFGH");
        using var host = await relay.ConnectAsync();
        using var joiner = await relay.ConnectAsync();

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(code));
        await RelayUnderTest.ReceiveAsync(host);

        using var joinerKeys = new SessionKeyExchange();
        using var hostKeys = new SessionKeyExchange();
        await RelayUnderTest.SendAsync(joiner, WireEnvelope.ForJoinRequest(code, joinerKeys.PublicKey));
        var (request, _) = await RelayUnderTest.ReceiveAsync(host);

        // Admitted by a real message, not arranged through the registry. The arrangement was a
        // leftover from before C6 merged, and it was the one thing in this file whose ordering
        // depended on relay state rather than on a message having arrived.
        await RelayUnderTest.SendAsync(
            host,
            WireEnvelope.ForJoinAccepted(code, request.PublicKey!, hostKeys.PublicKey));
        await RelayUnderTest.ReceiveAsync(joiner);

        // The substitution: a "sealed" payload whose ciphertext is the plaintext.
        var nullSealed = SealedPayload.FromWire(new byte[SessionCipher.NonceSize], SecretBytes);
        await RelayUnderTest.SendAsync(host, WireEnvelope.ForSessionPayload(code, nullSealed));

        var (forwarded, forwardedBytes) = await RelayUnderTest.ReceiveAsync(joiner);

        Assert.True(
            ContainsSequence(forwarded.Payload!, SecretBytes),
            "The decoded-payload assertion cannot detect an unencrypted payload, so it is not a check.");

        Assert.False(
            ContainsSequence(forwardedBytes, SecretBytes),
            "Base64 hid the plaintext from the raw-byte scan, which is exactly why the decoded "
            + "assertion above is the one that counts. If this ever fails, the encoding changed.");
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (var start = 0; start <= haystack.Length - needle.Length; start++)
        {
            if (haystack.AsSpan(start, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }
}
