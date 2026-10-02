using System;

namespace DungeonMasterXIV.Net;

/// <summary>The callbacks the host uses for a joiner's hello, its sealed join request, and a member's resume.</summary>
public readonly record struct JoinerAdmission(
    Action<byte[]>? OnHello = null,
    Action<byte[], WireEnvelope>? OnJoinRequest = null,
    Action<byte[], WireEnvelope>? OnResume = null);
