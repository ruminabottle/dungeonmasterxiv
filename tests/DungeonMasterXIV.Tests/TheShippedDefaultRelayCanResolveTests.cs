using System;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The value a real user actually dials. BUG-35: the plugin shipped pointing at
/// <c>relay.dungeonmasterxiv.invalid</c>, which RFC 2606 guarantees can never resolve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing in the suite touched this constant.</b> Every test that exercises the relay supplies
/// its own endpoint — which is right, because a unit test must not depend on DNS — and the effect is
/// that the one value no test overrides is the one every user gets. A full green suite said nothing
/// about whether the shipped default could be reached.
/// </para>
/// <para>
/// <b>This does not make a network call and must not.</b> Resolvability is asserted structurally: a
/// reserved TLD is unreachable <i>by specification</i>, so no lookup is needed to prove a placeholder
/// is one. That is a narrower claim than "the relay is up" and it is deliberately narrower — a test
/// that dialled the network would fail on a flight, and would fail for the wrong reason during an
/// outage.
/// </para>
/// <para>
/// So it catches a <b>placeholder</b>, not an outage. A hostname that is real but misspelled, or real
/// and pointed at nothing, passes here. Only A-1.5a — two machines and a deployed relay — closes
/// that, and this file does not pretend otherwise.
/// </para>
/// </remarks>
public class TheShippedDefaultRelayCanResolveTests
{
    // RFC 2606 reserves .test, .example, .invalid and .localhost so they can never be delegated.
    // .local is RFC 6762 multicast DNS: resolvable on a LAN, never on the internet, so a shipped
    // default in it is equally broken for a remote player.
    private static readonly string[] ReservedTopLevelDomains =
        ["invalid", "test", "example", "localhost", "local"];

    // THE CRITERION. Fails if: the shipped default sits in a TLD that cannot resolve — which is
    // exactly what v0.1.0 shipped, and what no other test in this repository could have caught.
    [Fact]
    public void TheShippedDefaultIsNotAPlaceholderHostname()
    {
        Assert.True(
            RelayEndpoint.TryParse(RelayEndpoint.Default, out var endpoint),
            $"The shipped default is not even a valid relay address: {RelayEndpoint.Default}");

        Assert.False(
            IsUnresolvableBySpecification(endpoint!.Host),
            $"The shipped default relay '{endpoint.Host}' is in a reserved top-level domain and can "
            + "never resolve. This is a placeholder that was never replaced (BUG-35).");
    }

    // NEGATIVE CONTROL, and the reason the test above is worth anything. A check that has never been
    // seen rejecting something is indistinguishable from a check that matches nothing. The first row
    // is the exact string that shipped.
    [Theory]
    [InlineData("relay.dungeonmasterxiv.invalid")]
    [InlineData("relay.dungeonmasterxiv.test")]
    [InlineData("relay.dungeonmasterxiv.example")]
    [InlineData("relay.dungeonmasterxiv.localhost")]
    [InlineData("relay.dungeonmasterxiv.local")]
    [InlineData("RELAY.DUNGEONMASTERXIV.INVALID")]
    public void TheCheckRejectsAPlaceholderInEveryReservedDomain(string host) =>
        Assert.True(
            IsUnresolvableBySpecification(host),
            $"'{host}' is in a reserved TLD and the check failed to say so.");

    // The other half of the control: it must not reject ordinary hostnames, or it would be a check
    // that fails everything and proves nothing about the one it was written for.
    [Theory]
    [InlineData("relay.ruminabottle.com")]
    [InlineData("relay.example.org")]
    [InlineData("invalid.ruminabottle.com")]
    [InlineData("localhost.ruminabottle.com")]
    public void TheCheckAcceptsOrdinaryHostnames(string host) =>
        Assert.False(
            IsUnresolvableBySpecification(host),
            $"'{host}' is an ordinary hostname and the check wrongly rejected it.");

    // R-1.8's swappability rests on the default being a normal value rather than a special case, and
    // the TLS reasoning in RelayEndpoint's remarks applies to it like any other address.
    [Fact]
    public void TheShippedDefaultIsItselfTlsAndUsesTheOneSessionPath()
    {
        Assert.True(RelayEndpoint.TryParse(RelayEndpoint.Default, out var endpoint));

        Assert.Equal("wss", endpoint!.Scheme);
        Assert.Equal(RelayEndpoint.SessionPath, endpoint.AbsolutePath);
    }

    // Matches the LAST label only. "invalid.ruminabottle.com" is a perfectly ordinary host, and a
    // substring check would reject it — the same trap RelayEndpoint's own remarks record about
    // "localhost.example.org", one file over.
    private static bool IsUnresolvableBySpecification(string host) =>
        ReservedTopLevelDomains.Contains(
            host.Split('.').Last(),
            StringComparer.OrdinalIgnoreCase);
}
