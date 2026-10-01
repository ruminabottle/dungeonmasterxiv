using System;

namespace DungeonMasterXIV.Net;

public readonly record struct JoinerAdmission(
    Action<byte[], DisplayName, string?>? OnJoinRequest = null,
    Action<byte[]>? OnComparabilityReceipt = null);
