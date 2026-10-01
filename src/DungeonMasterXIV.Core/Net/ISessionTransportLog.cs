using System;

namespace DungeonMasterXIV.Net;

public interface ISessionTransportLog
{
    void Information(string message);

    void Warning(string message);

    void Warning(Exception exception, string message);
}
