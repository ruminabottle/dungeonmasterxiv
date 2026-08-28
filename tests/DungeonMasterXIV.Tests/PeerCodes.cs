using System;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Builds a <see cref="PeerCode"/> for a test that needs one.
/// </summary>
/// <remarks>
/// <para>
/// <b>It goes through <see cref="PeerCode.TryParse"/> like everything else, and throws rather than
/// degrading.</b> A helper that quietly produced <c>default(PeerCode)</c> for an unparseable value
/// would let a test arrange an empty code, pass, and prove nothing — which is the shape this type
/// exists to remove. If a fixture cannot build the code it asked for, the test should die at the
/// arrangement rather than assert against an absent one.
/// </para>
/// <para>
/// <b>Why the fixtures changed when this type landed.</b> Every test here used to identify
/// participants as <c>"PEER-1"</c>. That is six characters, so it passed the length half of BUG-57's
/// vet and failed the alphabet half — <c>E</c> is excluded as a vowel and <c>-</c> and <c>1</c> are
/// not in <see cref="SpeakableAlphabet.Characters"/> at all. <b>The suite was therefore asserting
/// admission and roster behaviour against codes this product can never emit</b>, and the half of the
/// rule that rejects them was never exercised by these fixtures. They now use real ones.
/// </para>
/// </remarks>
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
