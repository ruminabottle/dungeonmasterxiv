using System.Text;

namespace DungeonMasterXIV.Net;

public static class NameInputCapacity
{
    private const int LargestCodePointBytes = 4;

    public static bool IsFull(string typed) =>
        typed is not null
        && Encoding.UTF8.GetByteCount(typed) + LargestCodePointBytes >= DisplayName.MaxUtf8Bytes;
}
