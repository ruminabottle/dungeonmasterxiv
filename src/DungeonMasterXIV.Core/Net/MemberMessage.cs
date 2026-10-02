using System;
using System.Collections.Generic;
using DungeonMasterXIV.Chat;

namespace DungeonMasterXIV.Net;

/// <summary>Checks a member's chat message and sends it to the host sealed, holding it while the path is down.</summary>
internal sealed class MemberMessage
{
    private readonly RelayLink _link;
    private readonly Func<SessionCode?> _code;
    private readonly Func<byte[]?> _sessionKey;
    private readonly Func<bool> _pathDown;
    private readonly Queue<string> _waiting = new();

    public MemberMessage(RelayLink link, Func<SessionCode?> code, Func<byte[]?> sessionKey, Func<bool> pathDown)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(sessionKey);
        ArgumentNullException.ThrowIfNull(pathDown);

        _link = link;
        _code = code;
        _sessionKey = sessionKey;
        _pathDown = pathDown;
    }

    public int Waiting => _waiting.Count;

    public MessageDraft Say(string? text, MessageLimits limits)
    {
        var draft = MessageDraft.Compose(text, limits);
        if (!draft.IsAccepted)
        {
            return draft;
        }

        if (_code() is null || _sessionKey() is null)
        {
            return new MessageDraft(null, MessageFault.NotInASession, "This client is not in a session.");
        }

        _waiting.Enqueue(draft.Text!);
        Flush();
        return draft;
    }

    public void Flush()
    {
        while (_waiting.Count > 0 && !_pathDown() && _code() is { } code && _sessionKey() is { } key)
        {
            var plaintext = SessionContentCodec.Encode(new SessionContent { Saying = _waiting.Dequeue() });
            var sealedPayload = SessionCipher.Seal(
                key, plaintext, WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));
            _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForSessionPayload(code, sealedPayload)));
        }
    }

    public int Abandon()
    {
        var undelivered = _waiting.Count;
        _waiting.Clear();
        return undelivered;
    }
}
