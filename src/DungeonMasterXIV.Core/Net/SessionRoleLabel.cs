using System;

namespace DungeonMasterXIV.Net;

public static class SessionRoleLabel
{
    public static string? For(SessionRole role) => role switch
    {
        SessionRole.Player => "Player",
        SessionRole.Assistant => "Assistant",
        SessionRole.DungeonMaster => "DM",

        _ => null,
    };

    public static bool IsKnown(SessionRole role) => For(role) is not null;
}
