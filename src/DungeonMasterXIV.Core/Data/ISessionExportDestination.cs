using System;

namespace DungeonMasterXIV.Data;

public interface ISessionExportDestination
{
    string Write(string contents);
}
