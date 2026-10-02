using System;

namespace DungeonMasterXIV.Net;

/// <summary>The callbacks the host uses for a joiner's hello and its sealed join request.</summary>
public readonly record struct JoinerAdmission(
    Action<byte[]>? OnHello = null,
    Action<byte[], WireEnvelope>? OnJoinRequest = null);
