using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// D-14: the wire format only grows, so a receiver ignores what it does not recognise.
/// </summary>
/// <remarks>
/// The relay is the party that gets updated; players' plugins are not, and there is no way to make
/// anyone update. So the failure this guards is the relay meeting a message from a client it does
/// not know and treating it as an attack or a fault — dropping the connection, and turning every
/// additive protocol change into a breaking one for whoever upgraded first.
/// </remarks>
public sealed class UnknownMessagesAreIgnoredTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    /// <summary>
    /// A message type from the future is ignored and the connection survives it — asserted by the
    /// connection still working afterwards, which is the part that would actually break.
    /// </summary>
    [Fact]
    public async Task AMessageTypeFromANewerClientDoesNotBreakTheConnection()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var host = await relay.ConnectAsync();

        var fromTheFuture = Encoding.UTF8.GetBytes("""{"Type":9999,"SessionCode":"BCDFGH"}""");
        await RelayUnderTest.SendRawAsync(host, fromTheFuture);
        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(Code));

        // Asserting on what arrives FIRST proves both halves at once: the unknown type drew no
        // reply, and the connection carried on well enough to answer the message after it. Waiting
        // on a timeout to prove the silence would abort the socket and destroy the second half —
        // see the remark on TryReceiveAsync.
        var (first, _) = await RelayUnderTest.ReceiveAsync(host);

        Assert.Equal(WireMessageType.CodeAccepted, first.Type);
    }

    /// <summary>
    /// A field the relay has never heard of is ignored by the deserializer rather than by anything
    /// remembering to, which is the half of D-14 that is a property rather than a habit.
    /// </summary>
    [Fact]
    public async Task AFieldFromANewerClientIsIgnoredAndTheMessageStillWorks()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var host = await relay.ConnectAsync();

        var withExtraField = Encoding.UTF8.GetBytes(
            """{"Type":1,"SessionCode":"BCDFGH","SomethingAddedLater":{"nested":[1,2,3]}}""");

        await RelayUnderTest.SendRawAsync(host, withExtraField);
        var (accepted, _) = await RelayUnderTest.ReceiveAsync(host);

        Assert.Equal(WireMessageType.CodeAccepted, accepted.Type);
    }

    /// <summary>
    /// Genuine rubbish draws no reply. Without this, "ignore what you do not recognise" would be
    /// indistinguishable from "accept anything", and the test above would pass on a relay that had
    /// simply stopped parsing.
    /// </summary>
    [Fact]
    public async Task BytesThatAreNotAnEnvelopeAreStillDropped()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var host = await relay.ConnectAsync();

        await RelayUnderTest.SendRawAsync(host, [0xFF, 0xFE, 0xFD, 0x00, 0x01]);
        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(Code));

        var (first, _) = await RelayUnderTest.ReceiveAsync(host);

        Assert.Equal(WireMessageType.CodeAccepted, first.Type);
    }
}
