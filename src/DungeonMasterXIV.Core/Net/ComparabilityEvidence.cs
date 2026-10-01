namespace DungeonMasterXIV.Net;

/// <summary>What the host knows about whether a joiner's client can show a fingerprint to read back.</summary>
public enum ComparabilityEvidence
{
    NotEstablished = 0,

    EstablishedCapable = 1,

    EstablishedIncapable = 2,
}
