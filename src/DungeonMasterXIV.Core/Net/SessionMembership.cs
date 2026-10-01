using System;
using DungeonMasterXIV.Chat;

namespace DungeonMasterXIV.Net;

public sealed class SessionMembership
{
    private readonly JoinRequester _joiner;
    private readonly ReceivedClosing _closing = new();
    private readonly MemberDeparture _departure;
    private readonly MemberMessage _message;

    internal SessionMembership(RelayLink link, JoinRequester joiner, Func<SessionCode?> code)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(joiner);
        ArgumentNullException.ThrowIfNull(code);

        _joiner = joiner;
        _departure = new MemberDeparture(link, code, () => joiner.SessionKey);
        _message = new MemberMessage(link, code, () => joiner.SessionKey);
    }

    public SessionKeyExchange? Keys => _joiner.Keys;

    public byte[]? SessionKey
    {
        get => _joiner.SessionKey;
        internal set => _joiner.SessionKey = value;
    }

    public bool AnnounceDeparture() => _departure.Announce();

    public MessageDraft Say(string? text, MessageLimits? limits = null) =>
        _message.Say(text, limits ?? MessageLimits.Default);

    public SessionClosing? Closing => _closing.Notice;

    public void Leave()
    {
        AnnounceDeparture();
        _closing.Clear();
        _joiner.Left();
    }

    internal void ExpireIfTheSessionHasClosed(DateTimeOffset now)
    {
        if (_closing.Notice is { } notice && notice.HasClosedAt(now))
        {
            _closing.Clear();
            _joiner.Left();
        }
    }

    internal void HeardFromTheHost(long? utcTicks) => _closing.Apply(utcTicks);
}
