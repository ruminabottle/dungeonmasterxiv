namespace DungeonMasterXIV.Net;

/// <summary>
/// What a host worked out about a returning client's claim to be an existing participant (R-1.5).
/// </summary>
/// <remarks>
/// <para>
/// <b>This carries information and no authority.</b> It cannot admit anybody. A resolved claim
/// changes what the DM's prompt <i>says</i> and nothing else — the DM approves every relink, every
/// session, and a match must never shorten the path (R-1.5, D-8). The type is shaped so there is
/// nothing here to act on: no token, no permission, no "already verified" flag.
/// </para>
/// <para>
/// <see cref="Label"/> comes from the participant that was <b>found in the store</b>, never from
/// anything the requesting client sent. A prompt built from what was claimed rather than from what
/// resolved would let a stranger choose the name the DM reads.
/// </para>
/// <para>
/// The two fields travel together so they cannot disagree. A claim that matched always has the
/// label of what it matched, and one that did not match has none — "relink with no label" and
/// "label but not a relink" are both unrepresentable rather than merely avoided.
/// </para>
/// </remarks>
/// <param name="Matched">Whether the claim resolved to a participant this campaign already knows.</param>
/// <param name="Label">That participant's local label, taken from the store. Null when nothing matched.</param>
public readonly record struct RelinkClaim(bool Matched, string? Label)
{
    /// <summary>No claim was made, or none resolved. The ordinary join case.</summary>
    public static RelinkClaim None => default;
}
