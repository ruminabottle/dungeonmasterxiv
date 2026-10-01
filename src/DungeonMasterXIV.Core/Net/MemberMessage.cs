using System;
using DungeonMasterXIV.Chat;

namespace DungeonMasterXIV.Net;

internal sealed class MemberMessage
{
    private readonly RelayLink _link;
    private readonly Func<SessionCode?> _code;
    private readonly Func<byte[]?> _sessionKey;

    public MemberMessage(RelayLink link, Func<SessionCode?> code, Func<byte[]?> sessionKey)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(sessionKey);

        _link = link;
        _code = code;
        _sessionKey = sessionKey;
    }

    public MessageDraft Say(string? text, MessageLimits limits)
    {
        var draft = MessageDraft.Compose(text, limits);

        if (!draft.IsAccepted)
        {
            return draft;
        }

        if (_code() is not { } code || _sessionKey() is not { } key)
        {
            return new MessageDraft(null, MessageFault.NotInASession, "This client is not in a session.");
        }

        var plaintext = SessionContentCodec.Encode(new SessionContent { Saying = draft.Text });
        var sealedPayload = SessionCipher.Seal(
            key, plaintext, WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForSessionPayload(code, sealedPayload)));
        return draft;
    }
}
