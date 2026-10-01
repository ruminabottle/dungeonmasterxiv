using System.Text;

namespace DungeonMasterXIV.Net;

/// <summary>Tells whether a name input box is too full in UTF-8 bytes to take another character.</summary>
public static class NameInputCapacity
{
    private const int LargestCodePointBytes = 4;

    public static bool IsFull(string typed) =>
        typed is not null
        && Encoding.UTF8.GetByteCount(typed) + LargestCodePointBytes >= DisplayName.MaxUtf8Bytes;
}
