namespace DungeonMasterXIV.Net;

/// <summary>Checks whether a typed session code is valid for the join form.</summary>
public static class JoinFlowCode
{
    public static bool Accepts(string typed, out SessionCode code) =>
        SessionCode.TryParse(typed, out code);
}
