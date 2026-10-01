using System;

namespace DungeonMasterXIV.Net;

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
