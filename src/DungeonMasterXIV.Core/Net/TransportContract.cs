using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The parts of the transport that the plugin and the relay must agree on. Neither side may change
/// one of these alone — a mismatch here does not fail at connect time, it fails during play.
/// </summary>
/// <remarks>
/// <para>
/// Clause 1 is framing: WebSocket binary frames over TLS, one <see cref="WireEnvelope"/> per
/// message. Clause 2 is the keepalive below.
/// </para>
/// <para>
/// <b>Why keepalive is a requirement rather than a nicety.</b> RP sessions sit quiet between rolls,
/// and idle connections get reaped by NAT tables and middleboxes. A session that dies silently
/// during a lull and reveals it at the next roll is indistinguishable from the product being
/// broken, and it surfaces during play rather than at connect time.
/// </para>
/// <para>
/// The values are <b>reasoned, not measured</b>. The relationship between them is not: see
/// <see cref="IsKeepAliveSafeFor"/>.
/// </para>
/// </remarks>
public static class TransportContract
{
    /// <summary>
    /// How often this client pings. The client initiates; it does not rely on the relay to.
    /// </summary>
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long this client waits for a pong before treating the connection as dead. Must exceed
    /// <see cref="KeepAliveInterval"/> so a single missed pong is not a disconnection.
    /// </summary>
    public static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// The smallest multiple of the keepalive interval that must still fit inside the grace window.
    /// Three, so a lull has to survive two lost pings before host-loss detection is even reached.
    /// </summary>
    public const int RequiredGraceMargin = 3;

    /// <summary>
    /// Whether the keepalive interval is safe against a given grace window (R-1.4).
    /// </summary>
    /// <remarks>
    /// The hard bound, and the reason it is a function rather than a comment: R-1.4's grace window
    /// is <b>settable</b>, deliberately, because the right length is an empirical question. So the
    /// dangerous edit is not someone raising this interval — it is someone lowering the grace
    /// window in settings until an ordinary lull trips host-loss detection and ends a session
    /// mid-play. Whoever builds that setting can call this and refuse the value.
    /// </remarks>
    /// <param name="graceWindow">The configured host-loss grace window.</param>
    public static bool IsKeepAliveSafeFor(TimeSpan graceWindow) =>
        graceWindow >= KeepAliveInterval * RequiredGraceMargin;
}
