using System;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Builds a <see cref="PeerCode"/> for a test that needs one.
/// </summary>
internal static class PeerCodes
{
    /// <summary>A code, or a failed test — never a silently absent one.</summary>
    internal static PeerCode Of(string value) =>
        PeerCode.TryParse(value, out var peerCode)
            ? peerCode
            : throw new ArgumentException(
                $"'{value}' is not a peer code this product generates, so no fixture can use it.",
                nameof(value));
}
