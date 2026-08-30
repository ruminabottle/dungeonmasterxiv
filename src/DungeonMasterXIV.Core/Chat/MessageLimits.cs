namespace DungeonMasterXIV.Chat;

/// <summary>
/// What a chat message may not exceed. <b>A message is untrusted input</b> (R-2.19) — it is typed by
/// a person, but it arrives from a peer who may be hostile or merely careless.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE VALUES HERE ARE ENGINEERING'S; THAT BOUNDS EXIST IS NOT.</b> R-2.19 says so in those
/// terms, and A-2.35 asserts no number. So these may be tuned by anyone with a reason, and the
/// <i>presence</i> of either bound may not be removed without going back to the product. This is
/// R-2.1a's shape, one layer over, and <see cref="DungeonMasterXIV.Rolls.RollLimits"/> is the
/// sibling worth reading beside it.
/// </para>
/// <para>
/// <b>A refusal is the required outcome — a freeze is a FAILED requirement, not a slow one.</b>
/// A-2.35 puts an out-of-memory in the same category as a wrong answer, which is why the byte
/// ceiling below is not decoration: it is the bound that holds when the character bound cannot.
/// </para>
/// <para>
/// <b>TWO BOUNDS, BECAUSE ONE OF THEM CANNOT BE MADE TO HOLD.</b> R-2.19 requires length
/// <i>at minimum</i>, so a second is permitted rather than scope creep. They measure different
/// things and only one is a defence — see <see cref="MaxUtf8Bytes"/>.
/// </para>
/// </remarks>
public sealed record MessageLimits
{
    /// <summary>The bounds applied when a caller does not choose its own.</summary>
    public static MessageLimits Default { get; } = new();

    /// <summary>
    /// The longest message accepted, counted as a reader counts characters.
    /// </summary>
    /// <remarks>
    /// <b>Generous on purpose.</b> This is conversation at a table, so the bound exists to stop a
    /// peer pasting a novel into the log, not to make people ration a sentence. A paragraph is
    /// ordinary and must not be refused; the number is chosen to sit well clear of anything a person
    /// types deliberately, which is what keeps the refusal rare enough to be informative when it
    /// does fire.
    /// </remarks>
    public int MaxLength { get; init; } = 2000;

    /// <summary>
    /// The most bytes the message may occupy once encoded as UTF-8.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THIS IS THE BOUND <see cref="MaxLength"/> CANNOT EXPRESS, AND IT IS THE DEFENCE.</b> A
    /// grapheme cluster may carry arbitrarily many combining marks, so a string of
    /// <see cref="MaxLength"/> characters has <b>no finite byte ceiling</b>. That is measured rather
    /// than supposed: <see cref="DungeonMasterXIV.Net.DisplayName"/> records exactly this for names
    /// and sizes its input buffer around it. <b>A character bound alone therefore bounds what a
    /// person perceives and leaves what the wire carries unbounded</b>, which is the hostile case
    /// R-2.19 names.
    /// </para>
    /// <para>
    /// <b>Eight bytes per character, matching <c>DisplayName.MaxUtf8Bytes</c>'s reasoning rather
    /// than inventing a second convention.</b> The heaviest realistic script is about six; this is
    /// generous so that no legitimate message written in any writing system is refused by this bound
    /// while passing the character one. <b>A message that trips this and not
    /// <see cref="MaxLength"/> is constructed, not typed.</b>
    /// </para>
    /// <para>
    /// <b>DERIVED FROM <see cref="MaxLength"/> RATHER THAN SET BESIDE IT, so the two cannot drift.</b>
    /// As its own initialised property this read <c>2000 * 8</c> — the same number today, and one
    /// that silently stops tracking the moment anybody tunes the character bound, leaving a byte
    /// ceiling corresponding to no character count at all. Move this independently by tuning
    /// <see cref="BytesPerCharacter"/>.
    /// </para>
    /// </remarks>
    public int MaxUtf8Bytes => MaxLength * BytesPerCharacter;

    /// <summary>
    /// How many UTF-8 bytes one accepted character may cost before <see cref="MaxUtf8Bytes"/>
    /// refuses. Generous rather than exact; see that property for why no exact answer exists.
    /// </summary>
    public int BytesPerCharacter { get; init; } = 8;
}
