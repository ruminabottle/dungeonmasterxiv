using DungeonMasterXIV.Relay.Diagnostics;
using DungeonMasterXIV.Relay.Sessions;
using DungeonMasterXIV.Relay.Transport;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace DungeonMasterXIV.Relay;

/// <summary>
/// Builds the relay. Construction and registration only — every rule lives in
/// <see cref="RelayRouter"/>, <see cref="SessionRegistry"/> and <see cref="RelayHub"/>.
/// </summary>
/// <remarks>
/// A method rather than statements inside <c>Program</c> so the A-1.5e test can start <i>the relay
/// we ship</i> instead of a stand-in assembled to look like it. A test that built its own host
/// would prove something about the test's wiring, which is the failure mode A-1.5e was re-pointed
/// to avoid.
/// </remarks>
public static class RelayApp
{
    /// <summary>Wires up a relay listening as <paramref name="options"/> describes.</summary>
    public static WebApplication Build(RelayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = options.ContentRoot,
        });

        // Console only, and that is load-bearing rather than a default left in place: a file sink
        // here would write to disk and make both A-1.5e and R-1.7a's shipped copy false. See RelayLog.
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(console => console.SingleLine = true);

        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.ListenAnyIP(options.Port, listen =>
            {
                if (!options.UseTls)
                {
                    return;
                }

                if (string.IsNullOrEmpty(options.CertificatePath))
                {
                    throw new InvalidOperationException(
                        $"TLS is on but no certificate was given. Set {RelayOptions.EnvironmentPrefix}CERT_PATH, "
                        + $"or {RelayOptions.EnvironmentPrefix}USE_TLS=false for a loopback test. The relay "
                        + "terminates TLS itself; a proxy in front of it is a destination D-2 forbids.");
                }

                try
                {
                    listen.UseHttps(options.CertificatePath, options.CertificatePassword);
                }
                catch (Exception failure)
                {
                    // Every exception type, deliberately: the load failure surfaces as a different
                    // platform-specific crypto exception on each host, and the one that matters here
                    // is the one nobody would think to name. Nothing is swallowed — the original is
                    // the inner exception, and its text is quoted in the message. See BUG-15.
                    throw new InvalidOperationException(
                        CertificateLoadFailure.Describe(options.CertificatePath, failure.Message),
                        failure);
                }
            }));

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<SessionRegistry>();
        builder.Services.AddSingleton<RelayRouter>();
        builder.Services.AddSingleton<ConnectionDirectory>();
        builder.Services.AddSingleton<RelayLog>();
        builder.Services.AddSingleton<RelayHub>();
        builder.Services.AddSingleton<WebSocketRelayEndpoint>();
        builder.Services.AddSingleton<ProtocolVersionGate>();

        var app = builder.Build();

        // The keepalive is a contract with the plugin's connection adapter, not a tuning knob:
        // the timeout must stay strictly greater than the interval, or the mechanism that keeps a
        // quiet session alive becomes the mechanism that ends it. Guarded rather than commented.
        if (options.KeepAliveTimeout <= options.KeepAliveInterval)
        {
            throw new InvalidOperationException(
                $"KeepAliveTimeout ({options.KeepAliveTimeout}) must exceed KeepAliveInterval "
                + $"({options.KeepAliveInterval}); otherwise the relay reaps connections faster than "
                + "they can answer a ping.");
        }

        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = options.KeepAliveInterval,
            KeepAliveTimeout = options.KeepAliveTimeout,
        });
        app.Map(options.Path, UpgradeAsync);
        return app;
    }

    /// <summary>
    /// The port a started relay actually bound, which is how a test on an ephemeral port finds it.
    /// </summary>
    public static int BoundPort(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The server exposes no addresses; is it started?");

        var address = addresses.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("The server is bound to no address; is it started?");

        return new Uri(address).Port;
    }

    private static async Task UpgradeAsync(
        HttpContext context,
        WebSocketRelayEndpoint endpoint,
        ProtocolVersionGate versions)
    {
        // Before the upgrade, deliberately: R-1.7b refuses a mismatched client rather than
        // connecting it and then explaining. The gate writes its own refusal.
        if (!versions.Admits(context))
        {
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            // Nothing else is served here. The relay has no health page, no status page and no
            // index: anything a browser could read would be a surface describing live sessions.
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        await endpoint.ServeAsync(socket, context.RequestAborted).ConfigureAwait(false);
    }
}
