using System;
using DungeonMasterXIV.Chat;

namespace DungeonMasterXIV.Net;

/// <summary>A joiner's view of its session: its keys, messages to the host, leaving, and the closing notice.</summary>
public sealed class SessionMembership
{
    private readonly JoinRequester _joiner;
    private readonly ReceivedClosing _closing = new();
    private readonly MemberDeparture _departure;
    private readonly MemberMessage _message;

    internal SessionMembership(RelayLink link, JoinRequester joiner, Func<SessionCode?> code, Func<bool> pathDown)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(joiner);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(pathDown);

        _joiner = joiner;
        _departure = new MemberDeparture(link, code, () => joiner.SessionKey);
        _message = new MemberMessage(link, code, () => joiner.SessionKey, pathDown);
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

    public int Undelivered { get; internal set; }

    internal void FlushWaiting() => _message.Flush();

    internal void AbandonWaiting()
    {
        var abandoned = _message.Abandon();
        if (abandoned > 0)
        {
            Undelivered = abandoned;
        }
    }

    public void Leave()
    {
        AbandonWaiting();
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
