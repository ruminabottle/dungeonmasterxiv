namespace DungeonMasterXIV.Net;

/// <summary>An answer the DM can give to a pending admission.</summary>
public enum AdmissionAction
{
    /// <summary>Neither. The prompt favours no answer and waits.</summary>
    None,

    /// <summary>Let them in.</summary>
    Admit,

    /// <summary>Refuse them.</summary>
    Deny,
}
