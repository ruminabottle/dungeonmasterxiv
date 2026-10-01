using System;
using System.Globalization;
using System.IO;

namespace DungeonMasterXIV.Data;

/// <summary>Writes a session export to a new session-{ticks}.log file in a directory and returns its path.</summary>
public sealed class SessionExportFileDestination(string directory) : ISessionExportDestination
{
    public const string Extension = ".log";

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
