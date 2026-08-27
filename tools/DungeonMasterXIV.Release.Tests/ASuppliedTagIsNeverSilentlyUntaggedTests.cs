using System;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-32: a tag that was supplied either gives the build its version, or is refused. It never
/// leaves the build looking untagged.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defaulting erased the evidence.</b> One property held both "what the tag derived to" and
/// "what the build uses", and the line that replaced an empty value with <c>0.0.0.0</c> ran during
/// evaluation, before any guard. So a bare <c>v</c> derived to nothing, was defaulted, and every
/// guard downstream was handed a perfectly good shape the tag had nothing to do with — it built
/// silently and stamped <c>0.0.0.0</c>, indistinguishable from an untagged developer build, while
/// every other malformed shape was named.
/// </para>
/// <para>
/// <b>Asserted as derivation, not as a list of spellings.</b> <c>v</c> and <c>vv</c> were what got
/// reported; <c>vvv</c> and anything else that strips to nothing are the same defect. The property
/// below — <i>a tag the build accepts stamps the version that tag carries</i> — is what closes the
/// class, and it is written without naming any internal property so it keeps holding if the
/// mechanism changes.
/// </para>
/// <para>
/// <b>Why <c>v0.0.0</c> is in the accept list and matters.</b> It legitimately stamps the same
/// version an untagged build carries. So "the stamped version is not the sentinel" would be the
/// wrong property — it would fail on a tag the Deployment Manager has ruled permitted. What
/// separates the two cases is whether the version was DERIVED from the tag, which is exactly what
/// the assertion checks.
/// </para>
/// </remarks>
public class ASuppliedTagIsNeverSilentlyUntaggedTests
{
    // The property. Every tag the build accepts must carry the version it stamps -- including the
    // ones the release tool goes on to refuse as aliases, and including v0.0.0, whose version only
    // LOOKS like an untagged build's.
    [Theory]
    [InlineData("v0.1.0")]
    [InlineData("v1.2.3.4")]
    [InlineData("vv0.1.0")]
    [InlineData("v2.3")]
    [InlineData("v1")]
    [InlineData("v0.0.0")]
    [InlineData("v0.1.0-rc1")]
    public void ATagTheBuildAcceptsStampsTheVersionThatTagCarries(string tag)
    {
        Assert.False(TheBuild.FailsToBuild(tag), $"'{tag}' should build.");

        Assert.Equal(tag.TrimStart('v'), TheBuild.VersionStampedFor(tag));
    }

    // The other side: a tag that strips to nothing is refused rather than defaulted. vvv is here and
    // was never reported -- if this only covered v and vv it would be the enumerate-versus-derive
    // failure a seventh time, in the test rather than the guard.
    [Theory]
    [InlineData("v")]
    [InlineData("vv")]
    [InlineData("vvv")]
    public void ATagThatStripsToNothingIsRefusedRatherThanTreatedAsUntagged(string tag)
    {
        var refusal = TheBuild.RefusalFromARealBuild(tag);

        Assert.Contains($"ReleaseTag '{tag}'", refusal, StringComparison.Ordinal);
    }

    // The boundary that must not move: supplying NO tag is the legitimate developer build, and it
    // still carries the untagged version. Without this the fix could satisfy everything above by
    // refusing every build that has no tag.
    [Fact]
    public void SupplyingNoTagStillBuildsAsAnUntaggedDeveloperBuild()
    {
        Assert.False(TheBuild.FailsToBuild(null));

        Assert.Equal(TaggedVersion.UntaggedBuild.ToString(), TheBuild.VersionStampedFor(null));
    }
}
