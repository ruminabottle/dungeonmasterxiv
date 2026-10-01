using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>Holds the join requests the host has not yet answered and removes them once decided or lapsed.</summary>
public sealed class AdmissionDesk
{
    private readonly List<PendingAdmission> _pending = new();

    public IReadOnlyList<PendingAdmission> Pending => _pending.AsReadOnly();

    public void Receive(PendingAdmission request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_pending.All(existing => existing.PeerCode != request.PeerCode))
        {
            _pending.Add(request);
        }
    }

    public PendingAdmission? Find(PeerCode peerCode) =>
        _pending.FirstOrDefault(request => request.PeerCode == peerCode);

    public PendingAdmission? Decide(PeerCode peerCode)
    {
        var request = Find(peerCode);
        if (request is not null)
        {
            _pending.Remove(request);
        }

        return request;
    }

    public IReadOnlyList<PendingAdmission> ExpireLapsed(DateTimeOffset now)
    {
        var lapsed = _pending.Where(request => request.HasLapsedAt(now)).ToList();
        foreach (var request in lapsed)
        {
            _pending.Remove(request);
        }

        return lapsed;
    }

    public TimeSpan? SoonestRemainingAt(DateTimeOffset now) =>
        _pending.Count == 0 ? null : _pending.Min(request => request.RemainingAt(now));

    public void Clear() => _pending.Clear();
}
