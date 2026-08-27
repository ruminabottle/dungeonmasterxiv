using System;
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
        var name = PreservedCampaignFile.NameFor(DateTimeOffset.UtcNow);
        Preserved = Content;
        Content = null;
        PreservedNames.Add(name);
        return name;
    }

    /// <summary>Preserved files still present, as the DM would see them listed.</summary>
    public List<string> PreservedNames { get; } = new();

    public IReadOnlyList<string> PreservedFiles() => PreservedNames;

    public bool DeletePreserved(string name) => PreservedNames.Remove(name);
}
