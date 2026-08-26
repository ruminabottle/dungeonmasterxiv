using System;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class KeyFingerprintTests
{
    // Fails if: the fingerprint is not a function of the key. The DM and the joiner compare it out
    // of band, so it has to be the same string on both screens for the same key.
    [Fact]
    public void TheSameKeyAlwaysGivesTheSameFingerprint()
    {
        using var party = new SessionKeyExchange();

        Assert.Equal(KeyFingerprint.Of(party.PublicKey), KeyFingerprint.Of(party.PublicKey));
    }

    // Fails if: the fingerprint is a constant. A fingerprint that matches for every key would show
    // the DM a reassuring match no matter who was actually on the other end.
    [Fact]
    public void DifferentKeysGiveDifferentFingerprints()
    {
        using var first = new SessionKeyExchange();
        using var second = new SessionKeyExchange();

        Assert.NotEqual(KeyFingerprint.Of(first.PublicKey), KeyFingerprint.Of(second.PublicKey));
    }

    // Fails if: the grouping changes to something a person cannot read aloud in one breath.
    [Fact]
    public void TheFingerprintIsThreeHyphenatedGroupsOfFourHexCharacters()
    {
        using var party = new SessionKeyExchange();

        var groups = KeyFingerprint.Of(party.PublicKey).Split('-');

        Assert.Equal(3, groups.Length);
        Assert.All(groups, group => Assert.Equal(4, group.Length));
        Assert.All(groups, group => Assert.True(group.All(Uri.IsHexDigit)));
    }
}
