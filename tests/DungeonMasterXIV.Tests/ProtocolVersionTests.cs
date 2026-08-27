using System;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.7b's client half: the version goes out on the connect request, and a refusal is read back as
/// which side is behind rather than as a generic failure (A-1.5i).
/// </summary>
public sealed class ProtocolVersionTests
{
    [Fact]
    public void TheVersionTravelsOnTheConnectRequest()
    {
        var dialled = ProtocolVersion.AppendTo(new Uri("wss://relay.example/session"));

        Assert.Contains($"{ProtocolVersion.QueryParameter}={ProtocolVersion.Current}", dialled.Query, StringComparison.Ordinal);
    }

    /// <summary>
    /// R-1.8 makes the relay address user-settable, so a query the user wrote has to survive. Losing
    /// it would be a bug they could see the symptom of and not the cause.
    /// </summary>
    [Fact]
    public void AQueryTheUserAlreadyWroteIsKept()
    {
        var dialled = ProtocolVersion.AppendTo(new Uri("wss://relay.example/session?room=west"));

        Assert.Contains("room=west", dialled.Query, StringComparison.Ordinal);
        Assert.Contains($"{ProtocolVersion.QueryParameter}={ProtocolVersion.Current}", dialled.Query, StringComparison.Ordinal);
    }

    [Fact]
    public void ARelaySpeakingANewerProtocolMeansThePluginIsBehind()
    {
        var failure = ProtocolVersion.ClassifyRefusal(upgradeRefused: true, $"{ProtocolVersion.Current + 1}");

        Assert.Equal(SessionFailure.PluginBehindRelay, failure);
    }

    [Fact]
    public void ARelaySpeakingAnOlderProtocolMeansTheRelayIsBehind()
    {
        var failure = ProtocolVersion.ClassifyRefusal(upgradeRefused: true, $"{ProtocolVersion.Current + 1}");
        var older = ProtocolVersion.ClassifyRefusal(upgradeRefused: true, "1");

        Assert.Equal(SessionFailure.PluginBehindRelay, failure);
        Assert.Equal(
            ProtocolVersion.Current > 1 ? SessionFailure.RelayBehindPlugin : SessionFailure.RelayUnreachable,
            older);
    }

    /// <summary>
    /// Anything that is not a well-formed refusal stays unreachable. Inventing a version story for a
    /// relay that answered oddly would be worse than the honest answer, and it is the failure that
    /// would otherwise send a user to update a plugin that was fine.
    /// </summary>
    [Theory]
    [InlineData(false, "99")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "not-a-number")]
    [InlineData(true, "0")]
    public void AnythingElseStaysAnUnreachableRelay(bool refused, string? stated)
    {
        Assert.Equal(SessionFailure.RelayUnreachable, ProtocolVersion.ClassifyRefusal(refused, stated));
    }

    /// <summary>
    /// Both version failures say which side has to change, and neither says "connection failed" —
    /// R-1.7b forbids a generic message because the action each one calls for is different.
    /// </summary>
    [Theory]
    [InlineData(SessionFailure.PluginBehindRelay)]
    [InlineData(SessionFailure.RelayBehindPlugin)]
    public void EachVersionFailureTellsTheUserWhichSideToUpdate(SessionFailure failure)
    {
        var message = SessionFailureMessage.For(failure);

        Assert.NotEmpty(message);
        Assert.DoesNotContain("connection failed", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("update", message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A version mismatch is a fourth ending, distinct from the three R-1.8 already names. If it
    /// shared a message with any of them the user would be sent to check their network or their
    /// code, neither of which is the problem.
    /// </summary>
    [Fact]
    public void AVersionMismatchIsNotConfusableWithTheOtherEndings()
    {
        string[] messages =
        [
            SessionFailureMessage.For(SessionFailure.RelayUnreachable),
            SessionFailureMessage.For(SessionFailure.ConnectionLost),
            SessionFailureMessage.For(SessionFailure.SessionCodeNotActive),
            SessionFailureMessage.For(SessionFailure.PluginBehindRelay),
            SessionFailureMessage.For(SessionFailure.RelayBehindPlugin),
        ];

        Assert.Equal(messages.Length, messages.Distinct(StringComparer.Ordinal).Count());
    }
}
