using System;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-22: a version has exactly one legal tag, so two tags cannot advertise one version.
/// </summary>
/// <remarks>
/// <para>
/// <b>BUG-14's fix left this standing.</b> Making the tag the single author fixed the ordinary
/// increment — <c>v0.1.0</c> then <c>v0.1.1</c> — but tag-to-version is many-to-one: <c>v0.1.0</c>,
/// <c>v0.1.0.0</c>, <c>v01.2.3</c> and <c>vv0.1.0</c> all pad to a version another tag also names.
/// Two such tags are two distinct git refs carrying two distinct assets and advertising one version,
/// and Dalamud does not reject the second — it never offers it.
/// </para>
/// <para>
/// <b>The realistic route is re-cutting a botched release</b>, which is the expected path when a
/// tester reports something, not an exceptional one: <c>v0.1.0</c> is published and wrong, so you
/// re-cut under something distinguishable and <c>v0.1.0.0</c> is the obvious choice. Note which way
/// the trap runs — <c>v0.1.0-fix</c> was already refused; the spelling that looks safest is the one
/// that collided.
/// </para>
/// </remarks>
public class CanonicalTagTests
{
    // Every one of these exits 0 today and advertises a version another tag also names.
    [Theory]
    [InlineData("v0.1.0.0", "v0.1.0")]
    [InlineData("v01.2.3", "v1.2.3")]
    [InlineData("vv0.1.0", "v0.1.0")]
    [InlineData("v2.3", "v2.3.0")]
    [InlineData("v1.0", "v1.0.0")]
    public void ATagThatIsAnotherSpellingOfItsVersionIsRefused(string tag, string canonical)
    {
        var failure = Assert.Throws<ArgumentException>(() => TaggedVersion.Of(tag));

        Assert.Contains($"'{canonical}'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("never offers it", failure.Message, StringComparison.Ordinal);
    }

    // The other half, and the one that would break the documented release command if the rule were
    // drawn wrong: every spelling this repository already uses stays legal.
    [Theory]
    [InlineData("v0.1.0", 0, 1, 0, 0)]
    [InlineData("v0.1.1", 0, 1, 1, 0)]
    [InlineData("v1.2.3", 1, 2, 3, 0)]
    [InlineData("v1.2.3.4", 1, 2, 3, 4)]
    [InlineData("v0.0.0.1", 0, 0, 0, 1)]
    [InlineData("v0.0.0", 0, 0, 0, 0)]
    public void TheCanonicalSpellingIsAccepted(string tag, int major, int minor, int build, int revision)
    {
        Assert.Equal(new Version(major, minor, build, revision), TaggedVersion.Of(tag));
    }

    // Whitespace and a capital V are read well enough to NAME the canonical form and then refused.
    // The build does not trim either, so accepting them would be a fresh divergence (BUG-23).
    [Theory]
    [InlineData(" v0.1.0")]
    [InlineData("v0.1.0 ")]
    [InlineData("V0.1.0")]
    public void ASpellingTheBuildWouldNotAcceptIsRefusedWithTheOneItWould(string tag)
    {
        var failure = Assert.Throws<ArgumentException>(() => TaggedVersion.Of(tag));

        Assert.Contains("'v0.1.0'", failure.Message, StringComparison.Ordinal);
    }

    // A tag that names no version at all is a DIFFERENT refusal, and must not be folded into the
    // canonical one -- "use v1.2.3 instead" is unhelpful advice about "not-a-tag-at-all".
    [Theory]
    [InlineData("not-a-tag-at-all")]
    [InlineData("v")]
    [InlineData("v1")]
    [InlineData("v0.1.0-rc1")]
    public void ATagThatNamesNoVersionIsRefusedAsThatRatherThanAsMisspelt(string tag)
    {
        var failure = Assert.Throws<ArgumentException>(() => TaggedVersion.Of(tag));

        Assert.Contains("does not name a version", failure.Message, StringComparison.Ordinal);
    }

    // The property the whole rule exists for, stated directly: canonicalising is idempotent, so the
    // legal tag for a version is a fixed point and no second spelling can reach it.
    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(1, 2, 3, 4)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(12, 0, 0, 0)]
    public void TheCanonicalTagForAVersionRoundTripsToThatVersion(int major, int minor, int build, int revision)
    {
        var version = new Version(major, minor, build, revision);
        var tag = TaggedVersion.CanonicalTagFor(version);

        Assert.Equal(version, TaggedVersion.Of(tag));
        Assert.Equal(tag, TaggedVersion.CanonicalTagFor(TaggedVersion.Of(tag)));
    }
}
