namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// The log, as this project can see it. Dalamud's <c>IPluginLog</c> cannot be named here, so the
/// plugin supplies a forwarding adapter and the store's logging decisions stay testable.
/// </summary>
/// <remarks>
/// D-8 forbids a character name in any line we write. Everything in this namespace logs campaign
/// UUIDs, counts and schema versions, and never a participant label — which is a property the
/// tests assert rather than a convention this comment asks for.
/// </remarks>
public interface ICampaignStoreLog
{
    /// <summary>Records something expected.</summary>
    /// <param name="message">The line to write. Must contain no participant label.</param>
    void Information(string message);

    /// <summary>Records something the DM or whoever supports them would want to know about.</summary>
    /// <param name="message">The line to write. Must contain no participant label.</param>
    void Warning(string message);
}
