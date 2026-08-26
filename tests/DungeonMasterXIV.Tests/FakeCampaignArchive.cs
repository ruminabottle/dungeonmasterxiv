using System.Collections.Generic;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// An in-memory <see cref="ICampaignArchive"/> that records what happened to it, so a test can
/// assert that an unreadable document was kept rather than written over.
/// </summary>
internal sealed class FakeCampaignArchive : ICampaignArchive
{
    public FakeCampaignArchive(string? initialContent = null) => Content = initialContent;

    /// <summary>What is currently stored, as the store would find it on a later load.</summary>
    public string? Content { get; private set; }

    /// <summary>Every write, in order. Empty means nothing was written at all.</summary>
    public List<string> Writes { get; } = new();

    /// <summary>What was moved aside, or null if nothing ever was.</summary>
    public string? Preserved { get; private set; }

    public string? Read() => Content;

    public void Write(string contents)
    {
        Content = contents;
        Writes.Add(contents);
    }

    public string PreserveUnreadable()
    {
        Preserved = Content;
        Content = null;
        return "campaigns.unreadable-test.json";
    }
}
