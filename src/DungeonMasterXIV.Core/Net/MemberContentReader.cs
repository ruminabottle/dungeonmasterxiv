using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

internal static class MemberContentReader
{
    public static void Apply(
        WireEnvelope envelope,
        InboundHandlers handlers,
        ISessionTransportLog? log)
    {
        if (handlers.MemberAuthored.OnContent is not { } onMemberContent
            || handlers.MemberAuthored.OpenWith is not { } candidates
            || envelope.TryGetSealedPayload() is not { } sealedPayload)
        {
            return;
        }

        var associatedData = envelope.AssociatedData();

        foreach (var candidate in candidates())
        {
            byte[] plaintext;
            try
            {
                plaintext = SessionCipher.Open(candidate.Key, sealedPayload, associatedData);
            }
            catch (CryptographicException)
            {
                continue;
            }

            if (!SessionContentCodec.TryDecode(plaintext, out var content, log) || content is null)
            {
                log?.Warning(
                    $"Content from participant {candidate.Peer.Value} authenticated and then failed "
                    + "to decode. It was sealed with the key this host shares with them, so this is "
                    + "version skew or an encoding defect rather than traffic for somebody else. "
                    + "The payload was discarded.");
                return;
            }

            onMemberContent(candidate.Peer, content);
            return;
        }
    }
}
