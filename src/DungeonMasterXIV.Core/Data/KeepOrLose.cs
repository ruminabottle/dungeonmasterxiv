using System;

namespace DungeonMasterXIV.Data;

/// <summary>
/// The session-end choice's two collaborators: how to open the offer, and where an accepted export
/// goes (R-2.12, A-2.23a).
/// </summary>
/// <remarks>
/// <b>A record rather than two parameters, for the reason <c>SessionCapabilities</c> gives:
/// the NEXT thing this choice needs costs a member here instead of another argument.</b> It is not
/// only tidiness — <c>SessionWindow</c> already takes five parameters against a block of six, so
/// threading the destination as its own argument would have put a window constructor at margin 0 to
/// deliver a data-privacy feature. The two travel together because neither is useful alone: an
/// offer that cannot write is the A-2.23a defect, and a destination nothing opens is dead.
/// </remarks>
/// <param name="Open">
/// Opens the keep-or-lose choice over this client's log. Supplied rather than built in a window: the
/// campaign, the clock and the length of the window are the composition root's.
/// </param>
/// <param name="Export">
/// Where an accepted log is written. <b>Reached only from the player's accept</b> — an export is an
/// act, never a default and never teardown (A-2.17).
/// </param>
public sealed record KeepOrLose(Func<SessionLogOffer> Open, ISessionExportDestination Export);
