using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// The path the relay serves and the path the client dials are the same.
/// </summary>
/// <remarks>
/// <para>
/// This test project is the only place both constants are visible — the plugin's tests cannot see
/// the relay, and the relay's own source cannot see a Dalamud-side default. That is why the
/// divergence survived from C3 until C7: neither side's tests could have caught it, and neither side
/// was wrong on its own.
/// </para>
/// <para>
/// <b>The value was ruled; this guards the class.</b> The relay's path stays configurable via
/// <c>DMX_RELAY_PATH_PREFIX</c>, so today's fix is one string and tomorrow's regression is one
/// environment variable. What fails here is the two defaults drifting apart again.
/// </para>
/// <para>
/// Deliberately not solved by deriving both from a single shared constant, which would have removed
/// the class outright and made this assertion unable to fail. That change belongs in
/// <c>RelayEndpoint</c>, which is another chunk's file with C5 in flight against it — and a shared
/// surface edited unilaterally is how the two halves disagreed in the first place. Worth doing;
/// not worth doing from here.
/// </para>
/// </remarks>
public sealed class RelayPathMatchesTheClientDefaultTests
{
    [Fact]
    public void TheRelayServesThePathTheClientDials()
    {
        Assert.True(RelayEndpoint.TryParse(RelayEndpoint.Default, out var clientDials));

        Assert.Equal(clientDials!.AbsolutePath, new RelayOptions().Path);
    }

    /// <summary>
    /// The assertion above compares two independently written values, so it can fail. Stated because
    /// a version of this that compared a constant with itself would pass forever and prove nothing —
    /// which is what deriving both from one source would have quietly turned it into.
    /// </summary>
    [Fact]
    public void TheTwoValuesAreIndependentlyWritten()
    {
        Assert.Contains("/session", RelayEndpoint.Default, System.StringComparison.Ordinal);
        Assert.Equal("/session", new RelayOptions().Path);
    }
}
