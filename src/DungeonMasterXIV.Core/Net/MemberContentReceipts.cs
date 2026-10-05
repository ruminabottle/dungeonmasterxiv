using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Chat;
using DungeonMasterXIV.Rolls;

namespace DungeonMasterXIV.Net;

/// <summary>The latest content received from one member, with its arrival order.</summary>
public readonly record struct MemberContentReceipt(PeerCode Peer, int Order, SessionContent Content);

/// <summary>Keeps the latest content from each member, dropping and counting parts members may not send.</summary>
public sealed class MemberContentReceipts
{
    private readonly Dictionary<string, MemberContentReceipt> _latest = new(StringComparer.Ordinal);
    private int _received;
    private int _refusedSayings;
    private int _refusedRolls;
    private int _refusedRosters;
    private int _refusedEntries;

    public int Received => _received;

    public int RefusedSayings => _refusedSayings;

    public int RefusedRolls => _refusedRolls;

    public int RefusedRosters => _refusedRosters;

    public int RefusedEntries => _refusedEntries;

    public IReadOnlyList<MemberContentReceipt> Latest =>
        _latest.Values.OrderBy(receipt => receipt.Order).ToList();

    internal void Record(PeerCode peer, SessionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        _received++;
        _latest[peer.Value] = new MemberContentReceipt(peer, _received, Retainable(content));
    }

    private SessionContent Retainable(SessionContent content)
    {
        string? saying = null;

        if (content.Saying is { } arrived)
        {
            if (MessageDraft.Compose(arrived, MessageLimits.Default).IsAccepted)
            {
                saying = arrived;
            }
            else
            {
                _refusedSayings = Counted(_refusedSayings);
            }
        }

        if (content.Rolling is { } roll && !roll.IsWithinBounds(RollLimits.Default))
        {
            _refusedRolls = Counted(_refusedRolls);
        }

        if (content.Roster is not null)
        {
            _refusedRosters = Counted(_refusedRosters);
        }

        if (content.Entries is not null)
        {
            _refusedEntries = Counted(_refusedEntries);
        }

        return new SessionContent
        {
            Leaving = content.Leaving,
            ClosingAtUtcTicks = content.ClosingAtUtcTicks,
            Saying = saying,
        };
    }

    private static int Counted(int refusals) => refusals == int.MaxValue ? refusals : refusals + 1;

    internal void Clear()
    {
        _latest.Clear();
        _received = 0;
        _refusedSayings = 0;
        _refusedRolls = 0;
        _refusedRosters = 0;
        _refusedEntries = 0;
    }
}
