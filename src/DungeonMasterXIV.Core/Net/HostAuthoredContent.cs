using System;

namespace DungeonMasterXIV.Net;

public readonly record struct HostAuthoredContent(
    byte[]? OpenWith = null,
    Action<SessionContent>? OnContent = null);
