using System;

namespace DungeonMasterXIV.Data;

/// <summary>What a keep-or-discard choice needs: a way to open the session-log offer, and where to export it.</summary>
public sealed record KeepOrLose(Func<SessionLogOffer> Open, ISessionExportDestination Export);
