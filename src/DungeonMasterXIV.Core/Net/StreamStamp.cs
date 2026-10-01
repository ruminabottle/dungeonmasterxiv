using System;

namespace DungeonMasterXIV.Net;

/// <summary>The sequence number and UTC time the host gives a stream entry.</summary>
public readonly record struct StreamStamp(long Sequence, long AtUtcTicks);
