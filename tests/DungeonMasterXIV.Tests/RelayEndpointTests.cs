using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class RelayEndpointTests
{
    // A-1.5c, the machine half: a user can point the plugin at a different relay. Fails if:
    // validation hard-codes the default or rejects anything that is not it.
    [Fact]
    public void AUserSuppliedRelayIsAccepted()
    {
        Assert.True(RelayEndpoint.TryParse("wss://relay.example.org/session", out var endpoint));
        Assert.Equal("relay.example.org", endpoint!.Host);
    }

    // Fails if: the default stops being a valid endpoint, which would make a fresh install unable
    // to connect at all.
    [Fact]
    public void TheDefaultRelayIsItselfValid()
    {
        Assert.True(RelayEndpoint.TryParse(RelayEndpoint.Default, out _));
    }

    // Fails if: validation is loosened to accept schemes D-2 does not permit. A setting that took
    // https:// or a bare hostname would be a route to a destination outside the permitted set, and
    // that is the reason for the check rather than whether the connection would succeed.
    [Theory]
    [InlineData("https://relay.example.org")]
    [InlineData("http://relay.example.org")]
    [InlineData("relay.example.org")]
    [InlineData("file:///etc/passwd")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnythingThatIsNotAWebSocketRelayIsRejected(string? candidate)
    {
        Assert.False(RelayEndpoint.TryParse(candidate, out var endpoint));
        Assert.Null(endpoint);
    }

    // Fails if: ws:// is accepted to a host on an observable network path. D-11 keeps payloads
    // encrypted either way, so this is not about content — anyone on the path still sees the session
    // code, timing, sizes and cadence, which is the cross-session correlation D-8 forbids.
    [Theory]
    [InlineData("ws://relay.example.org/session")]
    [InlineData("ws://192.168.1.10/session")]
    [InlineData("ws://[2001:db8::1]/session")]
    public void PlainWebSocketToARemoteHostIsRejected(string candidate)
    {
        Assert.False(RelayEndpoint.TryParse(candidate, out _));
    }

    // The exception, and the reason it is principled rather than a compromise: there is no
    // observable path, so none of the above applies. Fails if: loopback stops being reachable, which
    // would make a local test relay impossible to point at.
    [Theory]
    [InlineData("ws://localhost:8080/session")]
    [InlineData("ws://127.0.0.1:8080/session")]
    [InlineData("ws://[::1]:8080/session")]
    public void PlainWebSocketToLoopbackIsAllowed(string candidate)
    {
        Assert.True(RelayEndpoint.TryParse(candidate, out _));
    }

    // The trap in the obvious implementation. Every one of these is an ordinary remote host that a
    // hostname string comparison would wave through. Fails if: the loopback check becomes a
    // Contains or StartsWith on the host rather than Uri.IsLoopback.
    [Theory]
    [InlineData("ws://localhost.example.org/session")]
    [InlineData("ws://127.0.0.1.example.org/session")]
    [InlineData("ws://notlocalhost/session")]
    [InlineData("ws://localhost@evil.example.org/session")]
    public void AHostThatMerelyLooksLikeLoopbackIsRejected(string candidate)
    {
        Assert.False(RelayEndpoint.TryParse(candidate, out _));
    }

    // Fails if: TLS stops being accepted to ordinary hosts, which is the normal case.
    [Fact]
    public void EncryptedWebSocketToARemoteHostIsAllowed()
    {
        Assert.True(RelayEndpoint.TryParse("wss://relay.example.org/session", out _));
    }
}
