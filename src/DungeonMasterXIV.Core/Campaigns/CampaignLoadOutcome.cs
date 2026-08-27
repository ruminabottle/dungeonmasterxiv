namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// How the campaign document arrived. First run and failed-to-load are separate values because
/// the standards require a user who lost everything to get a different signal from one who never
/// had anything.
/// </summary>
public enum CampaignLoadOutcome
{
    /// <summary>Nothing was stored. This machine has never saved a campaign.</summary>
    FirstRun,

    /// <summary>A document was stored and was read.</summary>
    Loaded,

    /// <summary>
    /// A document was stored and could not be read — malformed, or written by a newer build whose
    /// shape this one does not know. It has been preserved, not replaced.
    /// </summary>
    Unreadable,
}
