using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// A-1.14: a client that is not admitted receives no roster at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Assessed over what the client RECEIVES, which is why this test is here and not in the plugin
/// suite.</b> The criterion says <i>"absent from the payload, not filtered in the UI"</i>, so it can
/// only be settled by a real relay deciding what to forward to a real unadmitted connection. A test
/// that asked the roster type whether it would exclude somebody would be asking the wrong party.
/// </para>
/// <para>
/// <b>It crosses the seam on purpose.</b> The roster is built with the production
/// <see cref="SessionContentCodec"/> and sealed with the production <see cref="SessionCipher"/>,
/// then put on a real socket through a real relay. Two tests — one that the roster is a
/// <see cref="WireMessageType.SessionPayload"/>, another that payloads are gated — would both pass
/// while nothing joined them, which is the shape that has cost this team four defects.
/// </para>
/// <para>
/// <b>And it would have passed vacuously a day ago.</b> Nothing sent content, so "an unadmitted
/// client receives no roster" was true of a product that had no roster. The positive half —
/// <see cref="TheAdmittedParticipantDoesReceiveIt"/> — is what stops that: it fails if the roster
/// never travels, so the negative half cannot be satisfied by silence.
/// </para>
/// </remarks>
public sealed class AnUnadmittedClientReceivesNoRosterTests
{
    /// <summary>
    /// A peer code of the shape the product actually produces.
    /// </summary>
    /// <remarks>
    /// <b>Derived, not typed.</b> These fixtures used <c>"PRBCD2"</c>, which
    /// <c>AdmissionControl.PeerCodeFor</c> can never emit — <c>E</c>, <c>-</c> and <c>1</c> are not
    /// in <see cref="SpeakableAlphabet.Characters"/>. That was invisible while nothing checked, and
    /// BUG-57 added the check. Built from the same two constants the codec validates against, so it
    /// cannot become impossible again if the alphabet or the length ever moves.
    /// <para>
    /// <b>The TAIL of the alphabet, not the head, and that is load-bearing.</b> The head is
    /// <c>"BCDFGH"</c>, which is also the session code these fixtures use — and a session code
    /// travels in the CLEAR, because the relay has to read it to route. A peer code equal to it
    /// makes "the roster is ciphertext" fail for a reason that has nothing to do with the roster.
    /// </para>
    /// </remarks>
    private static readonly string PeerCode = SpeakableAlphabet.Characters[^SessionCode.Length..];

    /// <summary>Long enough that a real forward would have landed, short enough not to stall a run.</summary>
    private static readonly TimeSpan LongEnoughToHaveArrived = TimeSpan.FromMilliseconds(750);

    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    [Fact]
    public async Task APendingJoinerIsSentNoRoster()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var host = await relay.ConnectAsync();
        using var pending = await relay.ConnectAsync();
        using var hostKeys = new SessionKeyExchange();
        using var pendingKeys = new SessionKeyExchange();

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(Code));
        await RelayUnderTest.ReceiveAsync(host);

        // Asks to join and is NOT answered. This is D-13's None: the entry is absent from what they
        // receive, rather than present and hidden by their client.
        await RelayUnderTest.SendAsync(pending, WireEnvelope.ForJoinRequest(Code, pendingKeys.PublicKey));
        await RelayUnderTest.ReceiveAsync(host);

        await RelayUnderTest.SendAsync(host, Roster(hostKeys, pendingKeys));

        Assert.Null(await RelayUnderTest.TryReceiveAsync(pending, LongEnoughToHaveArrived));
    }

    /// <summary>
    /// The positive half, without which the negative one is satisfied by a product that sends nothing.
    /// </summary>
    [Fact]
    public async Task TheAdmittedParticipantDoesReceiveIt()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var host = await relay.ConnectAsync();
        using var joiner = await relay.ConnectAsync();
        using var hostKeys = new SessionKeyExchange();
        using var joinerKeys = new SessionKeyExchange();

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(Code));
        await RelayUnderTest.ReceiveAsync(host);

        await RelayUnderTest.SendAsync(joiner, WireEnvelope.ForJoinRequest(Code, joinerKeys.PublicKey));
        var (request, _) = await RelayUnderTest.ReceiveAsync(host);

        await RelayUnderTest.SendAsync(
            host,
            WireEnvelope.ForJoinAccepted(Code, request.PublicKey!, hostKeys.PublicKey));
        await RelayUnderTest.ReceiveAsync(joiner);

        await RelayUnderTest.SendAsync(host, Roster(hostKeys, joinerKeys));

        var (forwarded, _) = await RelayUnderTest.ReceiveAsync(joiner);
        Assert.Equal(WireMessageType.SessionPayload, forwarded.Type);

        // Opened rather than counted: a forwarded envelope proves routing, and only opening it
        // proves the participant can actually read who is in the session.
        var plaintext = SessionCipher.Open(
            joinerKeys.DeriveSharedKey(hostKeys.PublicKey, Code),
            forwarded.TryGetSealedPayload()!,
            forwarded.AssociatedData());

        Assert.True(SessionContentCodec.TryDecode(plaintext, out var content));
        Assert.Equal(PeerCode, Assert.Single(content!.Roster!).PeerCode);
    }

    /// <summary>
    /// The roster the host would send, built and sealed exactly as the product builds and seals it.
    /// </summary>
    private static WireEnvelope Roster(SessionKeyExchange hostKeys, SessionKeyExchange recipientKeys)
    {
        var plaintext = SessionContentCodec.Encode(new SessionContent
        {
            Roster = [new RosterEntry(PeerCode, "Ysera", SessionRole.Player)],
        });

        var sealedPayload = SessionCipher.Seal(
            hostKeys.DeriveSharedKey(recipientKeys.PublicKey, Code),
            plaintext,
            WireEnvelope.AssociatedDataFor(Code, WireMessageType.SessionPayload));

        return WireEnvelope.ForSessionPayload(Code, sealedPayload);
    }

    /// <summary>
    /// The roster is unreadable on the wire, so "receives nothing" is not the only thing protecting
    /// it — a relay operator reading every frame learns no names either (D-11).
    /// </summary>
    [Fact]
    public void TheRosterIsCiphertextOnTheWire()
    {
        using var hostKeys = new SessionKeyExchange();
        using var joinerKeys = new SessionKeyExchange();

        var envelope = Roster(hostKeys, joinerKeys);
        var onTheWire = Encoding.UTF8.GetString(EnvelopeCodec.Encode(envelope));

        Assert.DoesNotContain("Ysera", onTheWire, StringComparison.Ordinal);
        Assert.DoesNotContain(PeerCode, onTheWire, StringComparison.Ordinal);
    }
}
