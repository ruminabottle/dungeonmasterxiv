namespace DungeonMasterXIV.Data;

/// <summary>Decides whether a session's log is retained: only when hosting.</summary>
public static class LogRetention
{
    public static bool KeepsItsLog(bool isHosting) => isHosting;
}
