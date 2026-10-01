namespace DungeonMasterXIV.Net;

internal sealed class ReceivedClosing
{
    public SessionClosing? Notice { get; private set; }

    public void Apply(long? utcTicks)
    {
        if (utcTicks is { } ticks && SessionClosing.TryFromWire(ticks) is { } closing)
        {
            Notice = closing;
        }
    }

    public void Clear() => Notice = null;
}
