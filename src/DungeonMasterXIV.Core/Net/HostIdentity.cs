using System;

namespace DungeonMasterXIV.Net;

/// <summary>Supplies the host's current keys, session code, display name and own peer code on demand.</summary>
internal sealed record HostIdentity(
    Func<SessionKeyExchange?> Keys,
    Func<SessionCode?> Code,
    Func<DisplayName> Name,
    Func<PeerCode?> OwnPeerCode)
{
    public static HostIdentity ForHost(
        Func<SessionKeyExchange?> keys,
        Func<SessionCode?> code,
        Func<DisplayName> name,
        Func<byte[], PeerCode> peerCodeFor) =>
        new(keys, code, name, () => keys() is { } hostKeys ? peerCodeFor(hostKeys.PublicKey) : null);
}
