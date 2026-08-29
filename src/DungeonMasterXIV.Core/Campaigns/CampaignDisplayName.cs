using System;
using System.Text;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// The name a player sends, remembered against ONE campaign and never outside it
/// (R-2.17, A-2.30, A-2.31, D-8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Campaign-scoped is the requirement, not a convenience.</b> D-8 is titled <i>"Identity is
/// campaign-scoped and never portable"</i>. A name that followed the player into a different
/// campaign would be a portable identifier arriving through a convenience rather than through a
/// design, so there is deliberately no global store, no "remember my name" setting, and nowhere on
/// <c>PluginSettings</c> for a name to live. <b>A-2.31 is about what the product CAN do</b> — a
/// portable identifier behind a toggle is still a portable identifier.
/// </para>
/// <para>
/// <b>This behaviour previously lived on <see cref="Data.PluginSettings"/> as one global alias</b>,
/// which is precisely the shape A-2.31 forbids. It predates the requirement that now forbids it, so
/// it was not a regression — but persistence existed and was built the forbidden way, and this type
/// is the re-scoping rather than a new feature. <b>The rules themselves are unchanged and were moved
/// deliberately intact</b>, because each of them records a defect somebody already paid for.
/// </para>
/// <para>
/// <b>A null campaign means "no campaign is current", and it reads as no stored name.</b> That is
/// the same answer the empty alias always gave, so the read path degrades to today's default rather
/// than to a new behaviour. The WRITE path cannot degrade the same way — with no campaign there is
/// nowhere scoped to put a name, and putting it anywhere else is the forbidden shape — so
/// <see cref="Record"/> reports that nothing changed. <b>Stated here rather than discovered at a
/// call site.</b>
/// </para>
/// </remarks>
public static class CampaignDisplayName
{
    /// <summary>
    /// The raw stored alias for <paramref name="campaign"/>, or empty when there is none.
    /// </summary>
    /// <remarks>
    /// <b>Stored as the raw string and validated at the point of use.</b> <c>DisplayName.TryParse</c>
    /// decides, not this type. Repairing it here would make what is on disk disagree with what the
    /// user typed, and the validation rules belong with the thing that renders it next to a
    /// fingerprint.
    /// </remarks>
    /// <param name="campaign">The campaign whose name is in question, or null when none is current.</param>
    public static string Stored(Campaign? campaign) => campaign?.DisplayNameAlias ?? string.Empty;

    /// <summary>
    /// Records a new alias against a campaign, reporting whether that changed anything, so a caller
    /// does not rewrite an identical file on every keystroke that changes nothing.
    /// </summary>
    /// <remarks>
    /// <b>With no current campaign there is nothing to record and nothing changes.</b> The name has
    /// no campaign to be scoped to, and the only other place to put it is the global store A-2.31
    /// forbids — so this reports false rather than finding somewhere for it to go.
    /// </remarks>
    /// <param name="campaign">The campaign to remember it against, or null when none is current.</param>
    /// <param name="alias">What the user typed. Whitespace-only is stored as empty.</param>
    public static bool Record(Campaign? campaign, string? alias)
    {
        if (campaign is null)
        {
            return false;
        }

        var trimmed = string.IsNullOrWhiteSpace(alias) ? string.Empty : alias.Trim();

        if (string.Equals(Stored(campaign), trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        campaign.DisplayNameAlias = trimmed.Length > 0 ? trimmed : null;
        return true;
    }

    /// <summary>
    /// The name this client will actually send in <paramref name="campaign"/>: the alias if there is
    /// a usable one, otherwise <paramref name="characterName"/> (R-1.3e — "defaults to the character
    /// name and may be changed to an alias"). A-1.2g asserts this on what leaves the client, not on
    /// what settings shows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rule lives here rather than at the wiring point.</b> Putting it in the plugin's
    /// composition root would make it Dalamud-side and untestable, and it is a rule about what the
    /// product sends.
    /// </para>
    /// <para>
    /// <b>An unusable alias falls back rather than failing.</b> A name <c>DisplayName</c> refuses to
    /// render beside a fingerprint — control characters, overlong, bidi overrides — falls back to the
    /// character name, not to nothing. Sending nothing would show the DM "a player who gave no name"
    /// and make a typo look like deliberate anonymity. The settings window says the alias is
    /// unusable; the join does not silently become nameless because of it.
    /// </para>
    /// <para>
    /// <b>THERE IS DELIBERATELY NO OVERLOAD OF THIS METHOD TAKING THE SQ-87 CARRIED-OVER
    /// DEFAULT, AND THE ABSENCE IS LOAD-BEARING.</b> A-2.31 permits exactly one globally
    /// stored name <i>"whose ONLY permitted reader is the pre-fill path"</i>, and A-2.32 fails
    /// any build that sends it unaccepted. Both hold here because this method — the send path —
    /// cannot be handed that value, so sending it is unrepresentable rather than merely
    /// forbidden. <b>If a future caller wants to pass it in, that is the criterion failing, not
    /// a missing convenience.</b> The carried value reaches the wire only after
    /// <see cref="RecordChosen"/> has stored it against a campaign, which is the player
    /// accepting it.
    /// </para>
    /// </remarks>
    /// <param name="campaign">The campaign being played, or null when none is current.</param>
    /// <param name="characterName">What the game says this player is called.</param>
    public static Net.DisplayName Or(Campaign? campaign, Net.DisplayName characterName) =>
        Net.DisplayName.TryParse(Stored(campaign), out var alias) ? alias : characterName;

    /// <summary>
    /// What the settings box starts out showing (R-1.3e — "pre-filled with their character name").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An empty box does not satisfy "pre-filled", and that is a citation rather than a
    /// preference.</b> The user opens the control and sees the name that will be sent already in it,
    /// then edits or leaves it.
    /// </para>
    /// <para>
    /// <b>Deliberately the raw alias rather than <see cref="Or"/>.</b> When an alias is stored but
    /// unusable the effective name is the character name — showing that here would replace what the
    /// user typed with something they did not, while the warning beside it tells them to fix a value
    /// the box no longer contains.
    /// </para>
    /// </remarks>
    /// <param name="campaign">The campaign being played, or null when none is current.</param>
    /// <param name="characterName">What the game says this player is called.</param>
    public static string ToEdit(Campaign? campaign, Net.DisplayName characterName) =>
        ToEdit(campaign, carriedOverDefault: null, characterName);

    /// <summary>
    /// What the settings box starts out showing when this client carries a display name stored
    /// BEFORE names were campaign-scoped (SQ-87): the campaign's own alias, else that carried-over
    /// default, else the character name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE CARRIED-OVER VALUE IS OFFERED, NEVER APPLIED.</b> SQ-87 rules that a name stored
    /// before campaign-scoping <i>"becomes a LOCAL PRE-FILL DEFAULT — not migrated, not dropped,
    /// not asked about at upgrade"</i>, because an upgrade-time prompt would ask the player to
    /// decide about campaign-scoped names before they have met the concept. It reaches the player
    /// here, in the box, at the point where the decision is legible.
    /// </para>
    /// <para>
    /// <b>THE CAMPAIGN'S OWN ALIAS WINS, AND THE ORDER IS THE REQUIREMENT RATHER THAN A
    /// PREFERENCE.</b> A name the player accepted IN THIS CAMPAIGN is a decision; the carried-over
    /// value is a leftover. Reversing them would let a pre-campaign default overwrite a considered
    /// choice every time the box was opened.
    /// </para>
    /// <para>
    /// <b>THIS OVERLOAD EXISTS AND <see cref="Or"/> HAS NO COUNTERPART, WHICH IS THE WHOLE
    /// SEPARATION.</b> <see cref="Or"/> is what the client SENDS; this is what the client SHOWS.
    /// A-2.32 is over PROVENANCE rather than over the string — <i>"a build that sends the stored
    /// default fails, even though the same bytes would be correct had the player accepted
    /// them"</i> — so the carried value must be unable to reach the send path at all. <b>It is
    /// unable to because no method on the send path takes it</b>, not because a check refuses it.
    /// The route from here to the wire runs through <see cref="RecordChosen"/>, which is the
    /// player's act (A-2.33).
    /// </para>
    /// <para>
    /// <b>Stated because it would otherwise look like an oversight:</b> a caller with no carried
    /// value passes null and gets exactly the previous behaviour, which is why the two-argument
    /// form delegates here rather than keeping a second copy of the rule. Two expressions meant to
    /// agree drift; one that is shared cannot disagree with itself.
    /// </para>
    /// </remarks>
    /// <param name="campaign">The campaign being played, or null when none is current.</param>
    /// <param name="carriedOverDefault">
    /// <c>Data.PluginSettings.DisplayNameAlias</c> — a name stored before campaign-scoping. Null or
    /// empty when there is none, which is every client that never ran v0.1.5.
    /// </param>
    /// <param name="characterName">What the game says this player is called.</param>
    public static string ToEdit(
        Campaign? campaign, string? carriedOverDefault, Net.DisplayName characterName) =>
        Stored(campaign) is { Length: > 0 } stored
            ? stored
            : carriedOverDefault is { Length: > 0 } carried
                ? carried
                : characterName.Value;

    /// <summary>
    /// Records what the user left in the settings box, reporting whether anything changed.
    /// </summary>
    /// <remarks>
    /// <b>Typing your own character name means "use my character name", not "freeze this string".</b>
    /// The box is pre-filled with it, so the commonest edit is no edit at all — and storing it as an
    /// alias would pin today's name, so a player who is renamed would keep sending the old one with
    /// nothing on screen explaining why. Matching it clears the alias instead, which keeps the
    /// default tracking rather than snapshotting it.
    /// </remarks>
    /// <param name="campaign">The campaign to remember it against, or null when none is current.</param>
    /// <param name="typed">What is in the box.</param>
    /// <param name="characterName">What the game says this player is called.</param>
    public static bool RecordChosen(Campaign? campaign, string? typed, Net.DisplayName characterName)
    {
        var trimmed = string.IsNullOrWhiteSpace(typed) ? string.Empty : typed.Trim();

        if (WouldShortenANameTheFieldCouldNotShow(Stored(campaign), trimmed))
        {
            return false;
        }

        return Record(
            campaign,
            string.Equals(trimmed, characterName.Value, StringComparison.Ordinal)
                ? string.Empty
                : trimmed);
    }

    /// <summary>
    /// Whether recording <paramref name="incoming"/> would silently shorten a stored name the
    /// settings field could not display whole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The settings box RE-SAVES what it shows.</b> It loads the stored alias, and if the widget
    /// reports a change it writes the box's contents back. A valid name can be larger than that box —
    /// <see cref="Net.DisplayName.TryParse"/> counts characters and the field counts bytes, and a
    /// grapheme cluster carries unboundedly many combining marks — so a stored name can arrive back
    /// shortened having never been edited. That is stored-data mutation with no user action, and it
    /// outlives the session that caused it.
    /// </para>
    /// <para>
    /// <b>THIS DELIBERATELY MAKES NO CLAIM ABOUT THE WIDGET.</b> Whether it truncates or refuses at
    /// the boundary, and whether it reports a change when IT shortened rather than when the user
    /// typed, are both unmeasured — and reasoning about them is what produced the wrong description of
    /// this defect the first time. The only property used here is one that needs no observation:
    /// <b>shortening cannot lengthen</b>. Make the loss unpersistable and what the widget does stops
    /// mattering.
    /// </para>
    /// <para>
    /// <b>Scoped by <see cref="Net.NameInputCapacity.IsFull"/> rather than a second threshold of its
    /// own.</b> That is the same expression the window uses to decide the field is full, already
    /// reviewed and already deliberately conservative — two expressions meant to agree drift, one
    /// that is shared cannot disagree with itself. When the stored alias is not near the field's
    /// capacity the field could show it whole, nothing could have shortened it, and this returns false
    /// for every ordinary edit.
    /// </para>
    /// <para>
    /// <b>Clearing is always honoured, and that is required rather than a nicety.</b> Without it a
    /// user whose stored alias is too large to display could never change it from this box again —
    /// the guard would have replaced silent corruption with a silent dead end. An empty box is
    /// unambiguously the user's act.
    /// </para>
    /// <para>
    /// <b>What this does NOT do:</b> it refuses only a SHORTENING. A replacement of equal or greater
    /// length is recorded normally, so the name remains editable. The residual cost is narrow and
    /// worth stating: a user whose alias exceeds the field cannot shorten it in place, and must clear
    /// it or replace it outright.
    /// </para>
    /// </remarks>
    /// <param name="stored">The alias currently held against the campaign.</param>
    /// <param name="incoming">The trimmed contents of the box.</param>
    private static bool WouldShortenANameTheFieldCouldNotShow(string stored, string incoming) =>
        incoming.Length > 0
        && Net.NameInputCapacity.IsFull(stored)
        && Encoding.UTF8.GetByteCount(incoming) < Encoding.UTF8.GetByteCount(stored);
}
