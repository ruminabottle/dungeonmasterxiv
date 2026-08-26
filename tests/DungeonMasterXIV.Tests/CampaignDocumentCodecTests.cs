using System;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class CampaignDocumentCodecTests
{
    [Fact]
    public void RoundTripPreservesEveryPersistedField()
    {
        var document = new CampaignDocument();
        var participant = new CampaignParticipant { ParticipantId = Guid.NewGuid(), Label = "Yshtola Rhul" };
        document.Campaigns.Add(new Campaign
        {
            CampaignId = Guid.NewGuid(),
            PreferredCode = "BKD7RM",
            CreatedUtc = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero),
            Participants = { participant },
        });

        Assert.True(CampaignDocumentCodec.TryDeserialize(CampaignDocumentCodec.Serialize(document), out var loaded));

        var campaign = Assert.Single(loaded!.Campaigns);
        Assert.Equal(document.Campaigns[0].CampaignId, campaign.CampaignId);
        Assert.Equal("BKD7RM", campaign.PreferredCode);
        Assert.Equal(document.Campaigns[0].CreatedUtc, campaign.CreatedUtc);
        Assert.Equal(participant.ParticipantId, Assert.Single(campaign.Participants).ParticipantId);
        Assert.Equal("Yshtola Rhul", Assert.Single(campaign.Participants).Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    public void TextThatIsNotACampaignDocumentIsRejected(string stored)
    {
        Assert.False(CampaignDocumentCodec.TryDeserialize(stored, out var document));
        Assert.Null(document);
    }

    [Fact]
    public void JsonNullIsRejectedRatherThanBecomingAnEmptyStore()
    {
        Assert.False(CampaignDocumentCodec.TryDeserialize("null", out var document));
        Assert.Null(document);
    }

    [Fact]
    public void ADocumentWrittenByANewerBuildIsRejected()
    {
        var fromTheFuture = $"{{\"Version\":{CampaignDocument.CurrentSchemaVersion + 1},\"Campaigns\":[]}}";

        Assert.False(CampaignDocumentCodec.TryDeserialize(fromTheFuture, out var document));
        Assert.Null(document);
    }

    [Fact]
    public void ADocumentPredatingAFieldKeepsThatFieldsDefault()
    {
        Assert.True(CampaignDocumentCodec.TryDeserialize("{\"Version\":1}", out var document));

        Assert.NotNull(document!.Campaigns);
        Assert.Empty(document.Campaigns);
    }

    [Fact]
    public void SerializingStampsTheCurrentVersionOverWhateverTheDocumentCarried()
    {
        // The version records the shape that was written, never the shape that was loaded.
        var document = new CampaignDocument { Version = 0 };

        Assert.True(CampaignDocumentCodec.TryDeserialize(CampaignDocumentCodec.Serialize(document), out var loaded));
        Assert.Equal(CampaignDocument.CurrentSchemaVersion, loaded!.Version);
    }
}
