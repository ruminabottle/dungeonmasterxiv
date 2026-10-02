using System;

namespace DungeonMasterXIV.Net;

/// <summary>Whether a join request matched a known campaign participant, with that participant's label and id.</summary>
public readonly record struct RelinkClaim(bool Matched, string? Label, Guid? ParticipantId = null)
{
    public static RelinkClaim None => default;
}
