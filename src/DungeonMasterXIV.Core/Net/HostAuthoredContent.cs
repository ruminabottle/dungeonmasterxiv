using System;

namespace DungeonMasterXIV.Net;

/// <summary>The key and callback for opening and handling session content the host sealed.</summary>
public readonly record struct HostAuthoredContent(
    byte[]? OpenWith = null,
    Action<SessionContent>? OnContent = null);
