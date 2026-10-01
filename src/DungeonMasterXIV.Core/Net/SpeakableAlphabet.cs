using System;
using System.Text;

namespace DungeonMasterXIV.Net;

/// <summary>The characters used in spoken codes, and grouping of codes into dash-separated threes.</summary>
public static class SpeakableAlphabet
{
    public const string Characters = "BCDFGHJKMNPRTVWXY2346789";

    public const int GroupSize = 3;

    public static int Length => Characters.Length;

    public static string Group(string rendered)
    {
        ArgumentNullException.ThrowIfNull(rendered);

        var grouped = new StringBuilder(rendered.Length + (rendered.Length / GroupSize));

        for (var start = 0; start < rendered.Length; start += GroupSize)
        {
            if (start > 0)
            {
                grouped.Append('-');
            }

            grouped.Append(rendered.AsSpan(start, Math.Min(GroupSize, rendered.Length - start)));
        }

        return grouped.ToString();
    }
}
