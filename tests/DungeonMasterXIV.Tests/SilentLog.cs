using System;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A log that records nothing, for the tests that construct a <see cref="SessionCoordinator"/> and
/// are not about logging.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shared, because DMXENG-13 made the log required and thirty-four test sites suddenly needed
/// one.</b> The alternative was a private no-op in each file, which is the duplication this project
/// keeps finding in other forms — and there were already two copies of a recording log before this.
/// </para>
/// <para>
/// <b>Deliberately NOT in Core, and that is the point of the ticket rather than a layering
/// preference.</b> A public no-op log shipped beside the interface is a supported way to construct a
/// coordinator that logs nothing — which is the silence the required parameter exists to prevent,
/// re-introduced under a respectable name. Production has exactly one construction site and it
/// passes the real adapter; nothing in the product should be able to opt out that easily.
/// </para>
/// <para>
/// A test that cares what was logged uses its own recording double instead. This one exists to say
/// "not what this test is about", and it says it once.
/// </para>
/// </remarks>
internal sealed class SilentLog : ISessionTransportLog
{
    /// <summary>The shared instance. Stateless, so one is enough.</summary>
    public static readonly SilentLog Instance = new();

    /// <inheritdoc />
    public void Information(string message)
    {
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
    }

    /// <inheritdoc />
    public void Warning(Exception exception, string message)
    {
    }
}
