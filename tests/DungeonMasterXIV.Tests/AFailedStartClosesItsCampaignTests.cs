using System.Collections.Generic;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>A campaign started for hosting stops being current as soon as hosting is not running.</summary>
public sealed class AFailedStartClosesItsCampaignTests
{
    [Fact]
    public void HostingThatStopsAfterAFailureLeavesNoCampaignCurrent()
    {
        var hosting = new HostingCampaign(new CampaignStore(new MemoryArchive(), new SilentStoreLog()));
        hosting.StartFor();

        hosting.Follow(hosting: false);

        Assert.Null(hosting.Current);
    }

    private sealed class MemoryArchive : ICampaignArchive
    {
        private readonly Dictionary<string, string> _files = new();

        public IReadOnlyList<string> CampaignFiles() => new List<string>(_files.Keys);

        public string? ReadCampaign(string name) => _files.TryGetValue(name, out var contents) ? contents : null;

        public void WriteCampaign(string name, string contents) => _files[name] = contents;

        public bool Delete(string name) => _files.Remove(name);

        public string? ReadLegacy() => null;

        public IReadOnlyList<string> OtherOwnedFiles() => new List<string>();
    }

    private sealed class SilentStoreLog : ICampaignStoreLog
    {
        public void Information(string message)
        {
        }

        public void Warning(string message)
        {
        }
    }
}
