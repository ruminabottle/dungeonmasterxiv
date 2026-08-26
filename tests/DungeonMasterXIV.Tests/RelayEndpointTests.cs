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

    // Fails if: ws:// and wss:// are reported as equally protected. R-1.9 forbids overstating the
    // guarantee in either direction, and the UI needs to be able to say which one the user is on.
    [Fact]
    public void PlainWebSocketIsNotReportedAsEncryptedTransport()
    {
        Assert.True(RelayEndpoint.TryParse("wss://relay.example.org", out var secure));
        Assert.True(RelayEndpoint.TryParse("ws://relay.example.org", out var plain));

        Assert.True(RelayEndpoint.IsEncryptedTransport(secure!));
        Assert.False(RelayEndpoint.IsEncryptedTransport(plain!));
    }
}
