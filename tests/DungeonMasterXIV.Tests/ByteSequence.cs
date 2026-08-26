using System.Linq;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Searches one byte array for another.
/// </summary>
/// <remarks>
/// Shared because the same assertion is needed wherever a test claims something is absent from an
/// output. Asserting that two arrays are merely unequal is a proxy for that and a weaker one: it is
/// satisfied by a difference in length alone, so it passes when the value under test is sitting in
/// the output untouched next to some extra bytes.
/// </remarks>
internal static class ByteSequence
{
    /// <summary>Whether <paramref name="needle"/> appears anywhere in <paramref name="haystack"/>.</summary>
    public static bool Contains(byte[] haystack, byte[] needle) =>
        needle.Length <= haystack.Length
        && Enumerable.Range(0, haystack.Length - needle.Length + 1)
            .Any(i => haystack.Skip(i).Take(needle.Length).SequenceEqual(needle));
}
