using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay;

public sealed class RelayOptions
{
    private readonly TimeSpan? _keepAliveTimeout;

    public const string EnvironmentPrefix = "DMX_RELAY_";

    public int Port { get; init; } = 443;

    public bool UseTls { get; init; } = true;

    public string? CertificatePath { get; init; }

    public string? CertificatePassword { get; init; }

    public string? ContentRoot { get; init; }

    public string Path { get; init; } = RelayEndpoint.SessionPath;

    public int MaxMessageBytes { get; init; } = 64 * 1024;

    public TimeSpan KeepAliveInterval { get; init; } = TransportContract.KeepAliveInterval;

    public const int MissedPingsTolerated = 3;

    public TimeSpan KeepAliveTimeout
    {
        get => _keepAliveTimeout ?? KeepAliveInterval * MissedPingsTolerated;
        init => _keepAliveTimeout = value;
    }

    public int OutboundQueueCapacity { get; init; } = 256;

    public int ReceiveChunkBytes { get; init; } = 4 * 1024;

    public static RelayOptions FromEnvironment() => new()
    {
        Port = ReadInt("PORT") ?? 443,
        UseTls = ReadBool("USE_TLS") ?? true,
        CertificatePath = Read("CERT_PATH"),
        CertificatePassword = Read("CERT_PASSWORD"),
        Path = Read("PATH_PREFIX") ?? RelayEndpoint.SessionPath,
        ContentRoot = Read("CONTENT_ROOT"),
        KeepAliveInterval = ReadSeconds("KEEPALIVE_INTERVAL_SECONDS") ?? TransportContract.KeepAliveInterval,
        MaxMessageBytes = ReadInt("MAX_MESSAGE_BYTES") ?? 64 * 1024,
        OutboundQueueCapacity = ReadInt("OUTBOUND_QUEUE_CAPACITY") ?? 256,
    };

    private static string? Read(string name) =>
        Environment.GetEnvironmentVariable(EnvironmentPrefix + name) is { Length: > 0 } value ? value : null;

    private static int? ReadInt(string name) =>
        int.TryParse(Read(name), out var value) ? value : null;

    private static bool? ReadBool(string name) =>
        bool.TryParse(Read(name), out var value) ? value : null;

    private static TimeSpan? ReadSeconds(string name) =>
        int.TryParse(Read(name), out var value) ? TimeSpan.FromSeconds(value) : null;
}
