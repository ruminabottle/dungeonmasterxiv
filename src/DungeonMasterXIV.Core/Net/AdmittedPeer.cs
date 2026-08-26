namespace DungeonMasterXIV.Net;

/// <summary>
/// A participant the DM has admitted. The only thing session state can be addressed to.
/// </summary>
/// <remarks>
/// Obtainable only from <see cref="SessionAudience.Admit"/>, so a caller cannot construct one for a
/// client that was never admitted. That is D-13's None level made structural: a client at None is
/// absent from the payload because there is no way to put it in one, rather than being filtered out
/// of a payload that was built for everyone.
/// </remarks>
public sealed class AdmittedPeer
{
    internal AdmittedPeer(string peerCode) => PeerCode = peerCode;

    /// <summary>
    /// The session-scoped code identifying this participant. Never a character name — R-1.3 requires
    /// the DM's prompt to identify a requester by code, and D-8 forbids the name reaching a log, a
    /// file or an export.
    /// </summary>
    public string PeerCode { get; }
}
