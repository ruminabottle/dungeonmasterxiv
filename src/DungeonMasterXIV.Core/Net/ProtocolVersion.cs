using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The version of the wire contract this build speaks (R-1.7b).
/// </summary>
/// <remarks>
/// <para>
/// <b>This number should almost never change, and D-14 is why.</b> The wire format only grows:
/// new message types and new optional fields are additive, a receiver ignores what it does not
/// recognise, and an old plugin therefore keeps working across almost every change we make. A bump
/// is for a change that genuinely cannot be expressed additively — and D-14 says reaching for one to
/// avoid thinking about whether the change could have been additive is itself a denial.
/// </para>
/// <para>
/// <b>What it is actually for.</b> Client and relay ship independently: the relay is updated while
/// old plugins are in the wild, with no way to make anyone update, and with each side able to pin a
/// different commit of this contract. Without a version, a genuine incompatibility surfaces as
/// whatever the protocol change happens to break, and the user sees a bug rather than "update me".
/// This turns that into a refusal at connect, which is the whole of R-1.7b.
/// </para>
/// <para>
/// <b>It travels on the connect request, not in an envelope.</b> A version carried as a message
/// would have to be read after a socket exists, so a mismatched client would be connected before
/// being refused — and R-1.7b forbids a partial connection, not merely a confusing one. On the
/// upgrade request the refusal happens before there is a WebSocket at all.
/// </para>
/// </remarks>
public static class ProtocolVersion
{
    /// <summary>
    /// The version this build speaks. Bump only for a change that cannot be made additively (D-14).
    /// </summary>
    public const int Current = 1;

    /// <summary>Query parameter the client states its version in, on the connect request.</summary>
    public const string QueryParameter = "v";

    /// <summary>Response header the relay states its own version in when it refuses.</summary>
    /// <remarks>
    /// The relay reports <i>its own</i> version rather than a verdict, and the client works out
    /// which side is behind by comparing. That keeps the user-facing sentence on the side that has a
    /// user, and it means a relay never has to describe a client build it has never heard of.
    /// </remarks>
    public const string Header = "X-DMX-Protocol-Version";

    /// <summary>
    /// Adds this build's version to <paramref name="relay"/> as a query parameter.
    /// </summary>
    /// <remarks>
    /// Preserves any query the user's own relay address already carries, because R-1.8 makes the
    /// address user-settable and silently discarding part of it would be a bug they could not see.
    /// </remarks>
    public static Uri AppendTo(Uri relay)
    {
        ArgumentNullException.ThrowIfNull(relay);

        var existing = relay.Query.TrimStart('?');
        var query = existing.Length == 0
            ? $"{QueryParameter}={Current}"
            : $"{existing}&{QueryParameter}={Current}";

        return new UriBuilder(relay) { Query = query }.Uri;
    }

    /// <summary>
    /// Reads a version a client stated, or <c>null</c> if it stated none or stated nonsense.
    /// </summary>
    /// <remarks>
    /// A client that states no version cannot be shown to be compatible, so it is refused like any
    /// other mismatch. That is the honest treatment of a build older than this requirement: it
    /// predates the check and cannot render the refusal either, so it will report the connection as
    /// failed. The check informs every build from this one onward and can only refuse those before
    /// it — a limitation of arriving second, not something to paper over.
    /// </remarks>
    public static int? Parse(string? stated) =>
        int.TryParse(stated, out var version) && version > 0 ? version : null;

    /// <summary>
    /// Reads a refused connect and works out which side is behind (R-1.7b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lives here rather than in the transport so it is testable without a socket or a Dalamud type
    /// — A-1.5i is machine-verifiable, and a classification that could only be exercised in-game
    /// would make it a criterion nobody could check.
    /// </para>
    /// <para>
    /// Anything that is not a well-formed refusal stays <see cref="SessionFailure.RelayUnreachable"/>.
    /// A relay that answered oddly is one this build cannot talk to, and inventing a version story
    /// for it would be worse than the honest answer.
    /// </para>
    /// </remarks>
    /// <param name="upgradeRefused">Whether the relay answered the upgrade with 426.</param>
    /// <param name="statedByRelay">The relay's own version, from <see cref="Header"/>.</param>
    public static SessionFailure ClassifyRefusal(bool upgradeRefused, string? statedByRelay)
    {
        if (!upgradeRefused || Parse(statedByRelay) is not { } relayVersion)
        {
            return SessionFailure.RelayUnreachable;
        }

        if (relayVersion > Current)
        {
            return SessionFailure.PluginBehindRelay;
        }

        return relayVersion < Current ? SessionFailure.RelayBehindPlugin : SessionFailure.RelayUnreachable;
    }
}
