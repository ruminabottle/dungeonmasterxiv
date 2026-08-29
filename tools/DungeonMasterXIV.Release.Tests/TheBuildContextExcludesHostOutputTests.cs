using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-77: host build output is kept out of the Docker build context by a <c>.dockerignore</c> at
/// the repository root.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS IS A DECLARED PROXY. It checks PRESENCE AND SHAPE, not EFFECT</b> — the same limit
/// <see cref="TheSdkIsPinnedTests"/> states about <c>global.json</c>, and for the same reason. Only
/// Docker can say whether a <c>.dockerignore</c> was honoured, and asking it means building an image,
/// which needs a daemon this suite does not require and should not start.
/// </para>
/// <para>
/// <b>The effect was verified by mutation instead, and the record is BUG-77's PR.</b> From a context
/// that had been built in locally, <c>docker build -f deploy/Dockerfile .</c> failed with
/// <c>NETSDK1064</c> (BouncyCastle not found) before the fix, succeeded after it, and failed
/// identically again when the <c>.dockerignore</c> was moved away and nothing else changed. That last
/// arm is what proves the file is load-bearing: <b>a <c>.dockerignore</c> that is present and inert
/// looks exactly like one that works, because the clean-context case passes either way.</b>
/// </para>
/// <para>
/// So what these tests are actually for: <b>deletion, relocation and hollowing-out.</b> That is
/// narrower than the class name suggests, which is why the limit is written here rather than left
/// for a reader to infer from a green run.
/// </para>
/// </remarks>
public class TheBuildContextExcludesHostOutputTests
{
    /// <summary>
    /// The paths <c>deploy/Dockerfile</c> copies. If the ignore file ever excludes one of these, the
    /// image build breaks in the same shape as the bug this guards — a failure that reads as broken
    /// source rather than as a context problem.
    /// </summary>
    private static readonly string[] RequiredByTheImage =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj",
        "src/DungeonMasterXIV.Relay/DungeonMasterXIV.Relay.csproj",
        "src/DungeonMasterXIV.Core/Net/SessionKeyExchange.cs",
        "src/DungeonMasterXIV.Relay/Program.cs",
    ];

    // THE LOCATION IS LOAD-BEARING, not a convention. Docker reads .dockerignore from the root of the
    // BUILD CONTEXT. Both ways this image is produced root their context at the repository:
    // `docker build -f deploy/Dockerfile .`, and deploy/compose.yaml which sets `context: ..`. A copy
    // placed next to the Dockerfile in deploy/ would be silently inert -- present, reassuring, and
    // doing nothing.
    [Fact]
    public void TheIgnoreFileIsAtTheContextRootAndNotBesideTheDockerfile()
    {
        Assert.True(
            File.Exists(Path.Combine(Root().FullName, ".dockerignore")),
            "No .dockerignore at the repository root, which is the build context for both "
            + "`docker build -f deploy/Dockerfile .` and deploy/compose.yaml's `context: ..`. "
            + "Host obj/ will overwrite the container's restore (BUG-77).");

        Assert.False(
            File.Exists(Path.Combine(Root().FullName, "deploy", ".dockerignore")),
            "There is a .dockerignore in deploy/. Docker reads it from the context root, not from "
            + "beside the Dockerfile, so this one does nothing and reads as though it does.");
    }

    // Fails if: the patterns are removed or hollowed out. bin/ AND obj/ both, because obj/ carries
    // project.assets.json -- the file that actually causes BUG-77 -- while bin/ is the same class of
    // host output and excluding only one would leave the pattern looking deliberate and half-done.
    [Theory]
    [InlineData("obj")]
    [InlineData("bin")]
    public void HostBuildOutputIsExcluded(string directory)
    {
        Assert.Contains(
            Patterns(),
            pattern => pattern.Trim('/') == directory || pattern.Trim('/') == $"**/{directory}");
    }

    // THE MIRROR FAILURE, and the reason this is not just a two-line presence check. Over-excluding
    // breaks the image in the SAME confusing shape as BUG-77 itself: the build fails inside the
    // container naming source code, while the host builds fine. A future `*` or `src/` added here to
    // "shrink the context" would do exactly that, and nothing else in the suite would notice.
    [Fact]
    public void NothingTheImageNeedsIsExcluded()
    {
        foreach (var pattern in Patterns())
        {
            foreach (var required in RequiredByTheImage)
            {
                Assert.False(
                    Excludes(pattern, required),
                    $"The .dockerignore pattern '{pattern}' excludes '{required}', which "
                    + "deploy/Dockerfile copies. The image build will fail naming the source code.");
            }
        }
    }

    /// <summary>
    /// Whether a pattern excludes a path, for the narrow pattern vocabulary this file uses.
    /// </summary>
    /// <remarks>
    /// <b>Throws rather than returning false on anything it does not understand.</b> Returning false
    /// would make this check pass for every pattern shape it cannot read — the check would go quiet
    /// exactly when the ignore file got complicated enough to be worth checking. If this throws,
    /// the answer is to widen it deliberately, not to catch it.
    /// </remarks>
    private static bool Excludes(string pattern, string path)
    {
        var bare = pattern.Trim('/');

        if (bare.StartsWith("**/", StringComparison.Ordinal))
        {
            var segment = bare[3..];
            Assert.DoesNotContain('*', segment);
            return path.Split('/').Contains(segment);
        }

        Assert.DoesNotContain('*', bare);
        return path == bare || path.StartsWith(bare + "/", StringComparison.Ordinal);
    }

    private static string[] Patterns() =>
        File.ReadAllLines(Path.Combine(Root().FullName, ".dockerignore"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

    private static DirectoryInfo Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "DungeonMasterXIV.sln")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        return root!;
    }
}
