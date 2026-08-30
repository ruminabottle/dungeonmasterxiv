using System;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Performs the act a player asks for when they accept the session-end offer (R-2.12, A-2.23a).
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS EXISTS SO THE ACCEPT PATH HAS A SEAM A TEST CAN REACH.</b> The click lives in an ImGui
/// view, which no unit test can drive — so without this, <b>"a build where a player accepts and no
/// export is produced FAILS"</b> would be asserted by reading the view rather than by running it,
/// and a later edit that dropped the write would pass everything.
/// </para>
/// <para>
/// <b>It takes the offer rather than being held by it, and that direction is the point.</b>
/// <see cref="SessionLogOffer"/> holds no store, no archive and no formatter, and a test asserts it
/// cannot acquire one — because a type that could write is a type that could reach
/// <see cref="RetainedLogFormat"/> and put a peer code into a genuine export (A-1.11c). Composing
/// here keeps that guarantee intact while still making the act testable.
/// </para>
/// </remarks>
public static class SessionExport
{
    /// <summary>
    /// Resolves <paramref name="offer"/> as kept and writes the export, returning where it went.
    /// </summary>
    /// <param name="offer">The open offer the player just accepted.</param>
    /// <param name="destination">Where the export goes.</param>
    public static string Produce(SessionLogOffer offer, ISessionExportDestination destination)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(destination);

        // REFUSED ON A RESOLVED OFFER, and the guard is here rather than on the offer.
        // WHEN THIS WAS WRITTEN, SessionLogOffer.Keep() did NOT refuse a second call -- it re-set
        // the outcome and handed the log back again -- so producing twice would have written two
        // files for one choice. BUG-182 closed that at the type in 2719162, so Keep() now refuses
        // too and this is no longer the only thing between a repeat call and a second file.
        // Nothing reaches it twice today either: the view stops drawing the buttons once the offer
        // closes. THE REASON FOR GUARDING HERE IS UNCHANGED: the SEAM is the thing a test and a
        // future caller can both reach, and guarding it needed no change to a fenced type.
        // The two refusals carry different messages, so a failure says which one answered.
        if (!offer.IsOpen)
        {
            throw new InvalidOperationException(
                "the session-end choice has already resolved; an export is one act per choice");
        }

        var kept = offer.Keep();

        return destination.Write(SessionExportFormat.Write(kept));
    }
}
