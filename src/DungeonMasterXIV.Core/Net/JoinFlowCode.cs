namespace DungeonMasterXIV.Net;

public static class JoinFlowCode
{
    public static bool Accepts(string typed, out SessionCode code) =>
        SessionCode.TryParse(typed, out code);
}
