using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Data;

public sealed class RememberedParticipant
{
    public string SessionCode { get; set; } = string.Empty;

    public Guid ParticipantId { get; set; }
}

public sealed class RelinkMemory
{
    public List<RememberedParticipant> Remembered { get; set; } = new();

    public IReadOnlyList<RememberedParticipant> All() => Remembered;

    public Guid? IdFor(SessionCode code) =>
        Remembered.FirstOrDefault(entry => Matches(entry, code))?.ParticipantId;

    public bool Remember(SessionCode code, Guid participantId)
    {
        var existing = Remembered.FirstOrDefault(entry => Matches(entry, code));

        if (existing is not null)
        {
            if (existing.ParticipantId == participantId)
            {
                return false;
            }

            existing.ParticipantId = participantId;
            return true;
        }

        Remembered.Add(new RememberedParticipant
        {
            SessionCode = code.Value,
            ParticipantId = participantId,
        });

        return true;
    }

    public bool Forget(SessionCode code) =>
        Remembered.RemoveAll(entry => Matches(entry, code)) > 0;

    private static bool Matches(RememberedParticipant entry, SessionCode code) =>
        string.Equals(entry.SessionCode, code.Value, StringComparison.Ordinal);
}
