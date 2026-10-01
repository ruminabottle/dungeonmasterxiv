namespace DungeonMasterXIV.Data;

public static class RelinkDisclosure
{
    public const string WhatIsStored =
        "When a DM admits you, they create a participant for you in their campaign and tell your "
        + "client which one it is. Your client keeps that here, so the same DM can recognise you "
        + "when you join again with the same code.";

    public const string BeginForgetting = "Forget this";

    public const string KeepIt = "Keep it";

    public const string ConfirmForget = "Forget it permanently";

    public static string BeforeForgetting(string sessionCode) =>
        $"Forget {sessionCode}?\n\n"
        + "This DM will no longer recognise you under this code. The next time you join it you "
        + "arrive as a new player, and the DM approves you the same way they did the first time.\n\n"
        + "Nothing is sent to the DM about this. They find out when you next join, because you "
        + "arrive as someone new.\n\n"
        + "This cannot be undone.";
}
