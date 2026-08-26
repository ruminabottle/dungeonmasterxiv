using System.Net.WebSockets;
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// Drives a complete session through a running relay: a host claims a code, a joiner asks to be
/// admitted, and an encrypted payload travels between them.
/// </summary>
/// <remarks>
/// <para>
/// Shared because more than one criterion needs "a full session ran" and each should assert about
/// the same traffic. A-1.5e needs it to have happened before it can say nothing was written;
/// A-1.5f needs the bytes it produced.
/// </remarks>
public static class FullSession
{
    /// <summary>The plaintext the session sends, used to prove it never appears on the wire.</summary>
    public const string SecretMessage = "the-quick-brown-fox-rolled-a-natural-twenty";

    /// <summary>Runs a host and a joiner through <paramref name="relay"/> and reports what crossed.</summary>
    public static async Task<FullSessionResult> RunAsync(RelayUnderTest relay, SessionCode code)
    {
        ArgumentNullException.ThrowIfNull(relay);

        using var host = await relay.ConnectAsync();
        using var joiner = await relay.ConnectAsync();

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(code));
        var (accepted, _) = await RelayUnderTest.ReceiveAsync(host);
        Assert.Equal(WireMessageType.CodeAccepted, accepted.Type);

        using var joinerKeys = new SessionKeyExchange();
        using var hostKeys = new SessionKeyExchange();

        await RelayUnderTest.SendAsync(joiner, WireEnvelope.ForJoinRequest(code, joinerKeys.PublicKey));
        var (joinRequest, _) = await RelayUnderTest.ReceiveAsync(host);
        Assert.Equal(WireMessageType.JoinRequest, joinRequest.Type);

        // SENT, not arranged. The host admits over the wire and the relay routes it, so the gate is
        // opened by the same path a real DM opens it by. The joiner takes the host's key from the
        // message rather than from the test, which is the half that would silently not exist if
        // JoinAccepted carried only one key.
        await RelayUnderTest.SendAsync(
            host,
            WireEnvelope.ForJoinAccepted(code, joinRequest.PublicKey!, hostKeys.PublicKey));

        var (admission, _) = await RelayUnderTest.ReceiveAsync(joiner);
        Assert.Equal(WireMessageType.JoinAccepted, admission.Type);
        Assert.NotNull(admission.HostPublicKey);

        var hostKey = hostKeys.DeriveSharedKey(joinRequest.PublicKey!, code);
        var joinerKey = joinerKeys.DeriveSharedKey(admission.HostPublicKey!, code);

        var payload = SessionCipher.Seal(
            hostKey,
            Encoding.UTF8.GetBytes(SecretMessage),
            WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForSessionPayload(code, payload));
        var (forwarded, forwardedBytes) = await RelayUnderTest.ReceiveAsync(joiner);
        Assert.Equal(WireMessageType.SessionPayload, forwarded.Type);

        var opened = SessionCipher.Open(joinerKey, forwarded.TryGetSealedPayload()!, forwarded.AssociatedData());
        Assert.Equal(SecretMessage, Encoding.UTF8.GetString(opened));

        await host.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        return new FullSessionResult(forwardedBytes, forwarded);
    }
}

/// <summary>What a full session left behind for a criterion to assert about.</summary>
/// <param name="ForwardedBytes">The exact bytes the relay passed to the joiner.</param>
/// <param name="ForwardedEnvelope">Those bytes decoded, so assertions are not made over base64.</param>
public sealed record FullSessionResult(byte[] ForwardedBytes, WireEnvelope ForwardedEnvelope);
