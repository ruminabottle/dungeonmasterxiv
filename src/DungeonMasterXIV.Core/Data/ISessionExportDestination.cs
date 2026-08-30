using System;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Where an export goes when a player asks for one (R-2.12, A-2.17).
/// </summary>
/// <remarks>
/// <para>
/// <b>DELIBERATELY NOT <see cref="IRetainedLogArchive"/>, and the reason is D-20 rather than
/// layering taste.</b> That archive is keyed by campaign id — <c>Write(Guid campaignId, string)</c>
/// — so an export written through it would carry a campaign id in its FILE NAME even though
/// <see cref="SessionExportFormat"/> keeps it out of the contents. <b>A campaign id is a value that
/// persists across DIFFERENT sessions, which is exactly the joinable neighbour D-20 names</b>: two
/// exports sharing one would be joinable on it and their file-local labels aligned by combination.
/// Keeping the identifier out of the bytes and putting it in the name would be the guarantee
/// defeated by its own filing.
/// </para>
/// <para>
/// <b>An export is AN ACT (A-2.17), so this is reached only from a player's choice</b> — never from
/// teardown, never from a default, never on a timer. <see cref="IRetainedLogArchive"/> is the
/// automatic path and is a different obligation.
/// </para>
/// </remarks>
public interface ISessionExportDestination
{
    /// <summary>
    /// Writes one export and returns a description of where it went, for the caller to show.
    /// </summary>
    /// <param name="contents">The export, already formatted by <see cref="SessionExportFormat"/>.</param>
    string Write(string contents);
}
