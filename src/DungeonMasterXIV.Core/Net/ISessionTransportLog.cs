using System;

namespace DungeonMasterXIV.Net;

/// <summary>Receives information and warning messages from the session networking code.</summary>
public interface ISessionTransportLog
{
    void Information(string message);

    void Warning(string message);

    void Warning(Exception exception, string message);
}
