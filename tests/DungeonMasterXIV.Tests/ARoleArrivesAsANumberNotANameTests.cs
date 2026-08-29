using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// <c>SessionRole</c> crosses the wire as a number, and a name is refused (BUG-104).
/// </summary>
/// <remarks>
/// <para>
/// <b>This pins a DEFAULT, which is why it exists at all.</b> <c>SessionContentCodec.Options</c>
/// declares no <c>Converters</c>, so <c>System.Text.Json</c> reads enums as numbers — and that
/// default is the whole basis for the codec's recorded finding that <c>Role</c> "cannot carry text
/// and cannot forge a line". Adding a <c>JsonStringEnumConverter</c> is one line, in a different
/// part of the file, and plausibly done for an unrelated reason. Without this test that edit makes
/// the finding false with nothing failing and nobody re-reading it.
/// </para>
/// <para>
/// <b>The question that produced it is worth more than the answer.</b> qa-1 checked four recorded
/// decisions and all four were TRUE; what they found is that the four are not equally DURABLE.
/// Three describe code in the same method as the decision, so the sentence and its subject travel
/// together and it cannot rot unread. This one's truth lives in a declaration elsewhere and is a
/// claim about a third-party library's default. So the question to ask of a recorded decision is not
/// "is it true" but WHAT WOULD HAVE TO CHANGE FOR IT TO STOP BEING TRUE, AND WOULD ANYBODY NOTICE.
/// </para>
/// <para>
/// <b>A behaviour test, not a prose checker.</b> It decodes a document and asserts an outcome. It
/// does not read the vetting comment, and it does not read <c>Options</c> — checking prose against
/// code is a separate design question that was deliberately deferred, and this needs none of it.
/// </para>
/// </remarks>
public class ARoleArrivesAsANumberNotANameTests
{
    // A peer code the product could actually produce, derived rather than typed -- the same idiom
    // ADroppedRosterEntryIsObservableTests uses, and the reason the control below can be trusted:
    // a hand-typed code would be dropped by the vetting and the document would decode empty.
    private static readonly string Usable = SpeakableAlphabet.Characters[^SessionCode.Length..];

    // Names that are real members, a name that is not, and the number written as text. All four are
    // string forms, and the codec must refuse the document rather than coerce any of them.
    [Theory]
    [InlineData("\"Player\"")]
    [InlineData("\"DungeonMaster\"")]
    [InlineData("\"Assistant\"")]
    [InlineData("\"0\"")]
    public void ARoleWrittenAsAStringIsRefused(string role)
    {
        Assert.False(
            SessionContentCodec.TryDecode(Encoding.UTF8.GetBytes(Document(role)), out _),
            $"A Role of {role} was accepted. SessionContentCodec.Options declares no Converters, so "
            + "enums must arrive as numbers -- if a JsonStringEnumConverter has been added, the "
            + "codec's finding that Role cannot carry text is now false and needs rewriting, not "
            + "this test relaxing (BUG-104).");
    }

    // THE VACUITY CONTROL, and without it the theory above proves nothing: every one of those
    // documents could be refused for some reason having nothing to do with the Role field, and a
    // codec that refused everything would satisfy it perfectly. This is the same document with the
    // only difference being the form of the one value under test.
    [Fact]
    public void TheSameDocumentWithANumericRoleIsAccepted()
    {
        Assert.True(
            SessionContentCodec.TryDecode(Encoding.UTF8.GetBytes(Document("0")), out var content),
            "The numeric form was refused too, so the theory above is not measuring the Role field.");

        Assert.Equal(SessionRole.Player, Assert.Single(content!.Roster!).Role);
    }

    private static string Document(string role) =>
        $$"""
        { "Roster": [ { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": {{role}} } ] }
        """;
}
