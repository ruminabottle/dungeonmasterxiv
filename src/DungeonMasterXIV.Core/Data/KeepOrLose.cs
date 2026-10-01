using System;

namespace DungeonMasterXIV.Data;

public sealed record KeepOrLose(Func<SessionLogOffer> Open, ISessionExportDestination Export);
