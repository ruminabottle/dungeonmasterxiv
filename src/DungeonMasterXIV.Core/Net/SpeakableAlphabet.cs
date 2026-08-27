using System;
using System.Text;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The one alphabet and grouping this product uses for anything a person reads aloud: session
/// codes (R-1.2a) and key fingerprints (R-1.3a).
/// </summary>
/// <remarks>
/// <para>
/// R-1.2a's reasoning is the reason this is one type rather than a constant on each of them.
/// "One speakable alphabet for the whole product... no second thing for a DM to learn" is a claim
/// about the product, not about session codes, and a second copy of these values would be two
/// things that must agree and eventually will not.
/// </para>
/// <para>
/// The exclusions are the decision, not an afterthought. All five vowels are absent so a code
/// cannot spell a word and therefore cannot spell an offensive one, which removes the need for a
/// profanity filter we would otherwise have to build and maintain. L, S, Z, Q, 0, 1 and 5 are
/// absent because they are confusable spoken or written with 1, 5, 2, O, I/L and S.
/// </para>
/// </remarks>
public static class SpeakableAlphabet
{
    /// <summary>The 24 permitted characters. See the remarks for why each exclusion is there.</summary>
    public const string Characters = "BCDFGHJKMNPRTVWXY2346789";

    /// <summary>Characters per displayed group, as in <c>BKD-7RM</c>.</summary>
    public const int GroupSize = 3;

    /// <summary>How many symbols the alphabet holds. The base of any value rendered in it.</summary>
    public static int Length => Characters.Length;

    /// <summary>
    /// Hyphenates <paramref name="rendered"/> into groups of <see cref="GroupSize"/>. A trailing
    /// partial group is kept as it is, which is what makes an 11-character fingerprint read as
    /// three-three-three-two.
    /// </summary>
    /// <param name="rendered">Characters already drawn from this alphabet.</param>
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
