using System;

namespace DungeonMasterXIV.Data;

/// <summary>Accepts an open session-log offer and writes its log, formatted for export, to a destination.</summary>
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
