using System;

namespace DungeonMasterXIV.Data;

/// <summary>Keeps an open session-log offer's log and writes it, formatted for export, to a destination.</summary>
public static class SessionExport
{
    public static string Produce(SessionLogOffer offer, ISessionExportDestination destination)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(destination);

        if (!offer.IsOpen)
        {
            throw new InvalidOperationException(
                "the session-end choice has already resolved; an export is one act per choice");
        }

        var kept = offer.Keep();

        return destination.Write(SessionExportFormat.Write(kept));
    }
}
