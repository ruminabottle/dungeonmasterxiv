using System;
using System.Globalization;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// What a campaign is called on screen (A-1.9k, A-1.9k-3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Rendered at READ TIME, not stored as text, and that is the ruling rather than a preference
/// (SQ-54).</b> The name is shown "in the DM's own culture at the moment they read it", and a
/// culture-formatted string written into a file cannot do that — it would be frozen in whatever
/// culture the machine had when the campaign was created. So the auto name is composed here, from
/// the instant, every time it is displayed.
/// </para>
/// <para>
/// <b>That is also why there is no migration.</b> <see cref="Campaign.CreatedUtc"/> already exists
/// on every stored campaign, so a campaign written by an older build gets a correct name the first
/// time this build shows it. Nothing is backfilled and no schema version moves.
/// </para>
/// <para>
/// <b>NEVER the session code (A-1.9k-3, and R-1.6 as corrected).</b> R-1.6 used to call the stored
/// code the campaign's "preferred label", and <c>CampaignListView</c> faithfully displayed it — the
/// Spec Owner's words were <i>"the implementation is faithful and my requirement was wrong"</i>.
/// A code fails three ways: it is as unrecognisable as a GUID, its absence renders an empty label,
/// and R-1.2a lets it change while the campaign does not — so it goes stale and can come to name a
/// DIFFERENT campaign. That last one is why <see cref="Campaign.CreatedUtc"/> is the right source: an instant
/// that never changes cannot migrate to another campaign (A-1.9k-4).
/// </para>
/// <para>
/// <b>Distinctness is NOT required (A-1.9k-1).</b> Two campaigns created in the same minute share a
/// name, and that is allowed: the criterion asks for IDENTIFIABLE, not unique, and renaming is the
/// escape hatch. A disambiguating suffix would push this back toward the id-shaped thing A-1.9k
/// rejects.
/// </para>
/// </remarks>
public static class CampaignName
{
    /// <summary>What to show for <paramref name="campaign"/>.</summary>
    /// <param name="campaign">The campaign being displayed.</param>
    /// <returns>Its own name if it has one, otherwise the auto name.</returns>
    /// <remarks>
    /// A stored name wins outright, because R-1.5d makes an auto-created campaign renameable and a
    /// rename that the display could override would not be one.
    /// </remarks>
    public static string For(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        return string.IsNullOrWhiteSpace(campaign.Name)
            ? Auto(campaign.CreatedUtc)
            : campaign.Name!;
    }

    /// <summary>
    /// The name a campaign has when nobody typed one: its creation date, then the clock time.
    /// </summary>
    /// <param name="createdUtc">When the campaign was created.</param>
    /// <param name="culture">Whose conventions to render in. Defaults to the reader's.</param>
    /// <remarks>
    /// <para>
    /// <b>No "Session of" prefix, and the reason is not brevity (SQ-54).</b> A campaign is not a
    /// session — this product spends real effort keeping them apart — so the prefix would be the one
    /// place the product calls a campaign a session. And it is accurate only at creation: it becomes
    /// a misnomer the moment the campaign is RESUMED, which is exactly when the feature has worked.
    /// </para>
    /// <para>
    /// <b>A clock time rather than "evening".</b> Whoever writes the boundary between afternoon and
    /// evening makes a ruling nobody asked for, silently, and it varies by person and culture in a
    /// way a clock does not.
    /// </para>
    /// <para>
    /// <b>The weekday is dropped, and this is the one judgement call in here.</b> The ruling drafted
    /// <c>28 August 2026, 8:14 PM</c> and said the COMPONENTS AND THEIR ORDER are load-bearing while
    /// punctuation is not. That draft carries no weekday, and several cultures put one in their long
    /// date pattern, so it is removed rather than allowed in for some readers and not others.
    /// </para>
    /// </remarks>
    public static string Auto(DateTimeOffset createdUtc, CultureInfo? culture = null)
    {
        var reader = culture ?? CultureInfo.CurrentCulture;
        var local = createdUtc.ToLocalTime();

        return $"{local.ToString(DatePatternWithoutWeekday(reader), reader)}, {local.ToString("t", reader)}";
    }

    /// <summary>The culture's long date, with any weekday token and its leftover separator removed.</summary>
    private static string DatePatternWithoutWeekday(CultureInfo culture) =>
        culture.DateTimeFormat.LongDatePattern
            .Replace("dddd", string.Empty, StringComparison.Ordinal)
            .Replace("ddd", string.Empty, StringComparison.Ordinal)
            .Trim(' ', ',', '،', '、')
            .Replace("  ", " ", StringComparison.Ordinal);
}
