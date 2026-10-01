using DungeonMasterXIV.Relay.Diagnostics;
using DungeonMasterXIV.Relay.Sessions;
using DungeonMasterXIV.Relay.Transport;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace DungeonMasterXIV.Relay;

/// <summary>Builds the relay web server, with optional TLS, and maps its version-gated WebSocket endpoint.</summary>
public static class RelayApp
{
    public static WebApplication Build(RelayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = options.ContentRoot,
        });

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
        if (!versions.Admits(context))
        {
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        await endpoint.ServeAsync(socket, context.RequestAborted).ConfigureAwait(false);
    }
}
