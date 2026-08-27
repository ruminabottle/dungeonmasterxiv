using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay;

/// <summary>
/// How this relay instance listens. Everything here is set by the operator at start-up; the relay
/// keeps no configuration of its own and writes none (D-2).
/// </summary>
public sealed class RelayOptions
{
    private readonly TimeSpan? _keepAliveTimeout;

    /// <summary>Environment variable prefix for every setting below.</summary>
    public const string EnvironmentPrefix = "DMX_RELAY_";

    /// <summary>
    /// The port to listen on. 443 by default.
    /// </summary>
    /// <remarks>
    /// Reasoned, not measured — no relay has run yet, and A-1.5a is what will confirm it. With a
    /// relay both clients dial outbound, which removes the inbound-acceptance failure that
    /// carrier-grade and symmetric NAT cause. What is left is networks that block outbound on
    /// unusual ports while permitting 443, and those are typically the ones running an inspecting
    /// proxy — which is also why the framing is WebSocket over real TLS rather than raw bytes on a
    /// port that happens to be 443.
    /// </remarks>
    public int Port { get; init; } = 443;

    /// <summary>
    /// Whether to terminate TLS here. On in production; off only for a test listening on loopback.
    /// </summary>
    /// <remarks>
    /// <b>The client's TLS session must terminate against the relay itself.</b> D-2's clarification
    /// of 2026-08-26 makes a TLS-terminating proxy — a CDN, an orange-cloud DNS record, anything
    /// the client authenticates and hands bytes to — a network destination, and destinations other
    /// than a relay or a session peer are approve-blocking. So this is not a deployment preference
    /// that a reverse proxy could satisfy just as well: putting one in front makes D-2's
    /// permitted-destination list untrue and moves retention outside what A-1.5e covers.
    /// </remarks>
    public bool UseTls { get; init; } = true;

    /// <summary>PKCS#12 certificate file for <see cref="UseTls"/>. Supplied by the operator.</summary>
    public string? CertificatePath { get; init; }

    /// <summary>Password for <see cref="CertificatePath"/>, if it has one.</summary>
    public string? CertificatePassword { get; init; }

    /// <summary>
    /// Directory the host treats as its content root. The container points this at a directory the
    /// relay does not need to write to; the A-1.5e test points it at an empty sandbox it can then
    /// assert stayed empty, which is what makes "the relay wrote nothing" checkable rather than
    /// asserted.
    /// </summary>
    public string? ContentRoot { get; init; }

    /// <summary>
    /// The request path the WebSocket upgrade is accepted on.
    /// </summary>
    /// <remarks>
    /// Taken from <see cref="RelayEndpoint.SessionPath"/> rather than restated, because a shared
    /// type is what makes two halves agree and two matching literals is what makes them drift.
    /// <para>
    /// It stays configurable via <c>DMX_RELAY_PATH_PREFIX</c> — an operator behind a reverse proxy
    /// may need a different prefix. What is no longer possible is the two <i>defaults</i> differing.
    /// </para>
    /// </remarks>
    public string Path { get; init; } = RelayEndpoint.SessionPath;

    /// <summary>
    /// Largest single envelope accepted. A client that could send an unbounded message could make
    /// the relay accumulate memory, and accumulated state is the thing D-2 forbids.
    /// </summary>
    public int MaxMessageBytes { get; init; } = 64 * 1024;

    /// <summary>
    /// How often the relay sends a WebSocket ping. Taken from
    /// <see cref="TransportContract.KeepAliveInterval"/> rather than restated, because a shared
    /// type is what makes two halves agree and two matching literals is what makes them drift.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; init; } = TransportContract.KeepAliveInterval;

    /// <summary>
    /// How many pings a connection may miss before the relay drops it. The relay's own margin, not
    /// the plugin's — see <see cref="KeepAliveTimeout"/>. There is no environment variable for the
    /// resulting timeout, deliberately; see <see cref="FromEnvironment"/>.
    /// </summary>
    public const int MissedPingsTolerated = 3;

    /// <summary>
    /// How long a connection may go without answering a ping before the relay drops it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Derived from <see cref="KeepAliveInterval"/> rather than written down.</b> It is a
    /// different number from <see cref="TransportContract.KeepAliveTimeout"/>, which is the
    /// <i>client's</i> tolerance for a missing pong; the two happen to coincide today and must not
    /// be conflated. Deriving it means it cannot drift below the interval no matter what the
    /// contract's interval becomes, which is the failure that would turn the mechanism keeping a
    /// quiet session alive into the mechanism that ends it.
    /// </para>
    /// <para>
    /// <b>Measured on ping/pong liveness, never on message traffic.</b> A relay reaping on "no
    /// envelope for ninety seconds" would kill precisely the quiet sessions the keepalive exists to
    /// protect, and it would look like a NAT problem when it was ours. It cannot be built on
    /// received messages in any case: .NET does not surface pong frames to <c>ReceiveAsync</c>, so a
    /// healthy client that pings and says nothing else is indistinguishable there from a dead one.
    /// </para>
    /// <para>
    /// <b>It must not assume clients close cleanly, and some will not.</b> A process can always die
    /// without a close frame, and BUG-5 is a live instance of a client that disposes its socket
    /// without one. This reaper is the thing that has to be right when the other end is not — the
    /// same reason it does not reap on traffic.
    /// </para>
    /// </remarks>
    public TimeSpan KeepAliveTimeout
    {
        get => _keepAliveTimeout ?? KeepAliveInterval * MissedPingsTolerated;
        init => _keepAliveTimeout = value;
    }

    /// <summary>
    /// How many envelopes may be queued for one connection before it is dropped for falling behind.
    /// </summary>
    /// <remarks>
    /// Bounded on purpose. An unbounded queue would turn a slow reader from a stall into memory that
    /// accumulates, which is the thing D-2 forbids wearing a different hat. The session outliving a
    /// participant who stopped reading is the trade being made.
    /// </remarks>
    public int OutboundQueueCapacity { get; init; } = 256;

    /// <summary>Read buffer size. A message larger than this arrives in several chunks.</summary>
    public int ReceiveChunkBytes { get; init; } = 4 * 1024;

    /// <summary>
    /// Reads the options from <c>DMX_RELAY_*</c> environment variables, falling back to the
    /// defaults above. Environment rather than a config file because the container should be
    /// configurable without a writable filesystem.
    /// </summary>
    /// <remarks>
    /// <see cref="KeepAliveTimeout"/> is deliberately absent: it is derived from
    /// <see cref="KeepAliveInterval"/> and has no variable of its own, so an operator cannot set a
    /// reaper below the interval the clients are pinging at. The bound couples two numbers, and the
    /// cheapest guard is to leave only one of them settable.
    /// </remarks>
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
