using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The host's side of an inbound payload: try the keys it shares with its members (R-1.3k).
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <see cref="AdmissionInbox"/> rather than written inside it, and the reason is a
/// measurement rather than taste.</b> Adding this to the inbox put that class at <b>428 lines
/// against a 400 block</b> — the engineering standards' hard limit, not its flag. The method needs
/// nothing the inbox holds: no queue, no lock, no drain state. A static helper that reads only its
/// arguments has no business inflating the class that happens to call it.
/// </para>
/// <para>
/// <b>It is the counterpart to <c>AdmissionInbox.ApplyContent</c>, not a replacement for it.</b>
/// That one opens HOST-authored content with the single key a joiner derived on admission; this
/// opens MEMBER-authored content with the keys a host shares with its peers. They stay apart
/// because their results go to different places — see
/// <see cref="InboundHandlers.OnMemberContent"/> for why merging them inverts D-3.
/// </para>
/// </remarks>
internal static class MemberContentReader
{
    /// <summary>
    /// Opens a payload a host can read from one of its members, and says who it was from (R-1.3k).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It stops at the first key that opens, and that is not just an early exit.</b> Keys are
    /// pairwise, so at most one candidate can succeed — carrying on would be asking whether a
    /// second peer also shares the key the first one does, which the key exchange already answers.
    /// </para>
    /// <para>
    /// <b>A payload no candidate opens is discarded in silence, for the reason the drain gives and
    /// one more.</b> A host receives every copy of its own roster broadcast that the relay forwards
    /// to other members, and it holds a key for each of them — so it will try, fail, and must say
    /// nothing. That traffic is ordinary and constant.
    /// </para>
    /// </remarks>
    public static void Apply(
        WireEnvelope envelope,
        InboundHandlers handlers,
        ISessionTransportLog? log)
    {
        if (handlers.OnMemberContent is not { } onMemberContent
            || handlers.OpenMemberContentWith is not { } candidates
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
                // Not this peer. Measured at ~4.9µs, which is why trying every candidate is cheaper
                // than deriving even one key on demand — see MemberContentKeys.
                continue;
            }

            // The AEAD authenticated under a key shared with exactly this peer, so a decode failure
            // after this point is version skew or an encoding defect rather than somebody else's
            // traffic — the same distinction ApplyContent draws, and it is knowable here for the
            // same reason. THE PEER CODE IS IN THE MESSAGE AND THE PAYLOAD IS NOT: the code is what
            // identifies which participant to ask about (A-1.2d), and D-8 keeps everything else out.
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
