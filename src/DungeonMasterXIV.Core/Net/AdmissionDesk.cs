using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The requests waiting on the DM. Holds them, decides them, and expires the ones nobody answered.
/// </summary>
/// <remarks>
/// Separate from <see cref="SessionCoordinator"/> because it has its own reason to change — who is
/// waiting and what happened to them — and because the coordinator is already the plugin's busiest
/// type.
/// </remarks>
public sealed class AdmissionDesk
{
    private readonly List<PendingAdmission> _pending = new();

    /// <summary>
    /// Everyone awaiting a decision. A list rather than one slot: four players clicking join at the
    /// start of a session is the ordinary case, and a single slot strands all but the newest on
    /// "waiting for the DM", which looks to them exactly like a DM ignoring them.
    /// </summary>
    public IReadOnlyList<PendingAdmission> Pending => _pending.AsReadOnly();

    /// <summary>Records a request. A repeat from the same participant does not stack a second prompt.</summary>
    public void Receive(PendingAdmission request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_pending.All(existing => existing.PeerCode != request.PeerCode))
        {
            _pending.Add(request);
        }
    }

    /// <summary>The request from this participant, or null.</summary>
    public PendingAdmission? Find(string peerCode) =>
        _pending.FirstOrDefault(request => request.PeerCode == peerCode);

    /// <summary>
    /// Removes a decided request and hands it back, so the caller can record how it was verified
    /// (R-1.3a) without re-deriving it.
    /// </summary>
    public PendingAdmission? Decide(string peerCode)
    {
        var request = Find(peerCode);
        if (request is not null)
        {
            _pending.Remove(request);
        }

        return request;
    }

    /// <summary>
    /// Removes every request whose window has closed and returns them.
    /// </summary>
    /// <remarks>
    /// These lapsed, they were not denied, and R-1.3c requires the difference to reach the player:
    /// nobody looked, so asking again is reasonable. Reporting a lapse as a refusal tells someone
    /// they were turned away when in fact the DM was mid-encounter.
    /// </remarks>
    public IReadOnlyList<PendingAdmission> ExpireLapsed(DateTimeOffset now)
    {
        var lapsed = _pending.Where(request => request.HasLapsedAt(now)).ToList();
        foreach (var request in lapsed)
        {
            _pending.Remove(request);
        }

        return lapsed;
    }

    /// <summary>The soonest deadline among waiting requests, for the host's own display.</summary>
    public TimeSpan? SoonestRemainingAt(DateTimeOffset now) =>
        _pending.Count == 0 ? null : _pending.Min(request => request.RemainingAt(now));

    /// <summary>Drops everything, for the end of a session.</summary>
    public void Clear() => _pending.Clear();
}
