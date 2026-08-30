using System;
using System.Globalization;
using System.IO;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Writes exports as files in a directory the player can reach.
/// </summary>
/// <remarks>
/// <b>THE FILE NAME CARRIES NO IDENTIFIER EITHER (D-20, A-2.17a).</b> It is the instant of the
/// write, which is not assigned or stored by the system against a participant and cannot be joined
/// to another export except on time — the alignment A-2.17c already acknowledges and permits.
/// <b>Do not name these by campaign, session or participant</b>: the format's guarantee would be
/// defeated by the filing rather than by the bytes, and nothing in the format's own tests would see
/// it.
/// </remarks>
/// <param name="directory">Where exports are written. Created on first write.</param>
public sealed class SessionExportFileDestination(string directory) : ISessionExportDestination
{
    /// <summary>The extension every export carries.</summary>
    public const string Extension = ".log";

    /// <inheritdoc />
    public string Write(string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        Directory.CreateDirectory(directory);

        var name = "session-"
            + DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture)
            + Extension;
        var path = Path.Combine(directory, name);

        File.WriteAllText(path, contents);

        return path;
    }
}
