using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The log, as this project can see it. Dalamud's <c>IPluginLog</c> cannot be named here, so the
/// plugin supplies a forwarding adapter and the transport's logging decisions stay testable.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately the same shape as <c>ICampaignStoreLog</c>, which already exists and is already
/// reviewed — one seam pattern in this codebase rather than two.
/// </para>
/// <para>
/// D-8 forbids a character name in any line we write, and the transport must go further: it also
/// never writes the relay address. A user's chosen relay is their business, and the log is the one
/// artifact most likely to be pasted into a bug report.
/// </para>
/// </remarks>
public interface ISessionTransportLog
{
    /// <summary>Records something expected.</summary>
    /// <param name="message">The line to write. No character name, no relay address.</param>
    void Information(string message);

    /// <summary>Records something the user or whoever supports them would want to know about.</summary>
    /// <param name="message">The line to write. No character name, no relay address.</param>
    void Warning(string message);

    /// <summary>Records a failure, with the exception that caused it.</summary>
    /// <param name="exception">What went wrong.</param>
    /// <param name="message">The line to write. No character name, no relay address.</param>
    void Warning(Exception exception, string message);
}
