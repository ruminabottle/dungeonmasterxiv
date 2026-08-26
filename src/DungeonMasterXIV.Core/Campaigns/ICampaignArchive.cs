namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Where the campaign document is kept. Implemented outside this project by a file adapter that
/// knows the plugin's config directory; implemented in tests by an in-memory fake.
/// </summary>
/// <remarks>
/// This port carries no policy. <i>Whether</i> an unreadable document should be preserved is a
/// decision and lives in <see cref="CampaignStore"/>; <see cref="PreserveUnreadable"/> only
/// performs it.
/// </remarks>
public interface ICampaignArchive
{
    /// <summary>
    /// The stored document as text, or <c>null</c> when nothing has ever been written. Null means
    /// first run and must not be conflated with a document that exists and will not parse.
    /// </summary>
    string? Read();

    /// <summary>Overwrites the stored document with <paramref name="contents"/>.</summary>
    /// <param name="contents">The serialized document.</param>
    void Write(string contents);

    /// <summary>
    /// Moves the current stored document aside, keeping it, so the next <see cref="Write"/> starts
    /// a fresh one without destroying what could not be read.
    /// </summary>
    /// <returns>A description of where it was kept, for the log. Never contains a participant label.</returns>
    string PreserveUnreadable();
}
