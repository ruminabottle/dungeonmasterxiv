using System;

namespace DungeonMasterXIV.Net;

/// <summary>The callbacks the host uses for incoming join requests and fingerprint comparability receipts.</summary>
public readonly record struct JoinerAdmission(
    Action<byte[], DisplayName, string?>? OnJoinRequest = null,
    Action<byte[]>? OnComparabilityReceipt = null);
