using System;

namespace DungeonMasterXIV.Net;

public readonly record struct StreamStamp(long Sequence, long AtUtcTicks);
