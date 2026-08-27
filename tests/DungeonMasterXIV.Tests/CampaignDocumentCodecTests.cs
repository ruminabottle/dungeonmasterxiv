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

    // Fails if System.Text.Json is left to drop properties it does not recognise. Under D-12 a
    // tester rolling back from a newer build is the EXPECTED case, not a corruption scenario: the
    // newer build's fields arrive, are not understood, and — without JsonExtensionData — vanish on
    // the next save with no error and nothing in the log.
    //
    // This does not replace the schema version gate and is not meant to. A document from a higher
    // schema version is still refused outright; this covers fields added without a version bump,
    // which is the case the gate cannot see.
    [Fact]
    public void PropertiesThisBuildDoesNotUnderstandSurviveALoadAndSave()
    {
        const string FromANewerBuild =
            "{\"Version\":1,\"SomethingNew\":\"keep me\",\"Campaigns\":[" +
            "{\"CampaignId\":\"2f1d5b8e-0000-4000-8000-000000000001\",\"CampaignColour\":\"blue\"," +
            "\"Participants\":[{\"ParticipantId\":\"2f1d5b8e-0000-4000-8000-000000000002\"," +
            "\"Label\":\"Yshtola Rhul\",\"Pronouns\":\"they/them\"}]}]}";

        Assert.True(CampaignDocumentCodec.TryDeserialize(FromANewerBuild, out var loaded));
        var rewritten = CampaignDocumentCodec.Serialize(loaded!);

        Assert.Contains("SomethingNew", rewritten);
        Assert.Contains("keep me", rewritten);
        Assert.Contains("CampaignColour", rewritten);
        Assert.Contains("blue", rewritten);
        Assert.Contains("Pronouns", rewritten);
        Assert.Contains("they/them", rewritten);
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
