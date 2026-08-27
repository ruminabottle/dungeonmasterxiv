using System.Net.Security;
using System.Net.WebSockets;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// C15: the relay, as the container we deploy, reached over TLS from outside that container.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every other test in this suite starts the relay in-process</b> (see <see cref="RelayUnderTest"/>),
/// which proves the code and says nothing about the image. This one proves the opposite half and
/// only that half: that <c>deploy/Dockerfile</c> produces something that starts, binds, terminates
/// TLS and speaks the protocol. It is the only test here that does not build its own relay.
/// </para>
/// <para>
/// <b>What a pass does NOT mean.</b> It is not evidence that a plugin can join: a real client
/// validates the certificate and this one does not. It is not evidence that self-hosting works. And
/// a pass against a loopback address is satisfiable by a stale container or a host process, which is
/// why it ships with a negative control run by hand — stop the container, watch this fail, restart
/// it, watch it pass again. The restart is the half that proves the stop caused the failure.
/// </para>
/// <para>
/// Opt-in rather than skipped-by-default-forever: it needs a relay already running somewhere, which
/// no ordinary <c>dotnet test</c> has. Absent the variable it reports as skipped with the reason,
/// rather than passing quietly and being counted as coverage it did not provide.
/// </para>
/// </remarks>
public sealed class ContainerSmokeTests
{
    /// <summary>The environment variable naming the running relay, e.g. <c>wss://localhost/session</c>.</summary>
    public const string EndpointVariable = "DMX_SMOKE_RELAY_URL";

    /// <summary>Long enough for a round trip over a real socket, short enough to fail a run rather than hang it.</summary>
    private static readonly TimeSpan RoundTrip = TimeSpan.FromSeconds(10);

    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    /// <summary>The relay under test, or null when this run has not been pointed at one.</summary>
    internal static string? Endpoint =>
        Environment.GetEnvironmentVariable(EndpointVariable) is { Length: > 0 } value ? value : null;

    /// <summary>
    /// A code request crosses TLS into the container and comes back accepted.
    /// </summary>
    /// <remarks>
    /// A protocol round trip rather than a liveness check, deliberately. "The port answers" is
    /// satisfied by anything at all listening there, including a container that started and then
    /// failed to wire the endpoint up; "the relay serves" is what this criterion is about, and
    /// <see cref="WireMessageType.CodeAccepted"/> can only come from the session registry.
    /// </remarks>
    [ContainerSmokeFact]
    public async Task TheContainerServesTheProtocolOverTls()
    {
        using var client = new ClientWebSocket { Options = { CollectHttpResponseDetails = true } };

        // The one place in this repository that accepts an unverified certificate. That is enforced
        // as far as a text scan can enforce it: NoTlsValidationBypass in Directory.Build.targets
        // fails the build of every other project if one of the NAMES it lists appears in its source.
        //
        // Read that as what it is. It catches the copy-paste route, which is the one that actually
        // happens. It does NOT catch a positional callback — an SslStream handed a validation
        // delegate as an argument rather than assigned to a named property — and closing that needs
        // a Roslyn analyser with the semantic model, not more tokens. A guard trusted further than
        // it reaches is worse than the gap in it.
        //
        // Why it is here at all: the certificate this dials is minted on the box at smoke-test time
        // and destroyed afterwards, because a real one for the deployed name requires the host to be
        // publicly reachable — and making that reachable is the separate decision D-12 keeps apart
        // from verifying the container. Validating it would test the certificate; skipping
        // validation tests the thing this criterion is about, which is that Kestrel loaded a PKCS#12
        // from the mounted secret and is serving the protocol behind it.
        client.Options.RemoteCertificateValidationCallback =
            (_, _, _, _) => true;

        var relay = ProtocolVersion.AppendTo(new Uri(Endpoint!));

        using var deadline = new CancellationTokenSource(RoundTrip);
        await client.ConnectAsync(relay, deadline.Token);

        Assert.Equal(WebSocketState.Open, client.State);

        await RelayUnderTest.SendAsync(client, WireEnvelope.ForCodeRequest(Code));
        var (accepted, _) = await RelayUnderTest.ReceiveAsync(client, RoundTrip);

        Assert.Equal(WireMessageType.CodeAccepted, accepted.Type);
        Assert.Equal(Code.Value, accepted.SessionCode);

        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
    }
}

/// <summary>
/// A fact that runs only when <see cref="ContainerSmokeTests.EndpointVariable"/> names a relay.
/// </summary>
/// <remarks>
/// xunit takes <c>Skip</c> as a constant, so the decision has to be made in the attribute's
/// constructor rather than in the test body. Made here rather than by returning early from the
/// test, because an early return is a green tick for work that did not happen — the shape that put
/// a false pass in front of this team once already.
/// </remarks>
public sealed class ContainerSmokeFactAttribute : FactAttribute
{
    /// <summary>Skips the test, with the reason, unless a relay has been named.</summary>
    public ContainerSmokeFactAttribute()
    {
        if (ContainerSmokeTests.Endpoint is null)
        {
            Skip = $"Set {ContainerSmokeTests.EndpointVariable} to a running relay's wss:// address "
                 + "(deploy/compose.yaml brings one up) to run the container smoke test.";
        }
    }
}
