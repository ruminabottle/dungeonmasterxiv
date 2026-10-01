using System;

namespace DungeonMasterXIV.Data;

/// <summary>Writes a session export's text somewhere and returns where it was written.</summary>
public interface ISessionExportDestination
{
    string Write(string contents);
}
