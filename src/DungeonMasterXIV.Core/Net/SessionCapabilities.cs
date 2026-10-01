using System;

namespace DungeonMasterXIV.Net;

public sealed record SessionCapabilities(
    Func<SessionKeyExchange>? NewKeys = null,
    Func<DisplayName>? HostDisplayName = null,
    Func<DisplayName, Guid?>? MintParticipant = null,
    Func<string?, RelinkClaim>? ResolveRelink = null)
{
    public static SessionCapabilities Default { get; } = new();

    public Func<SessionKeyExchange> KeySource => NewKeys ?? (static () => new SessionKeyExchange());

    public Func<DisplayName, Guid?> ParticipantSource => MintParticipant ?? (static _ => null);

    public Func<string?, RelinkClaim> RelinkSource =>
        ResolveRelink ?? (static _ => RelinkClaim.None);

    public Func<DisplayName> HostNameSource => HostDisplayName ?? (static () => DisplayName.None);
}
