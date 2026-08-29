using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// T-37: a relink claim arriving on the wire reaches the DM's prompt as a resolved relink — and
/// grants nothing (R-1.5, completing A-1.9a).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CLAIM WAS ARRIVING AND BEING DROPPED.</b> DMXENG-1 made the joiner send one and
/// <c>CampaignRelink.Resolve</c> had been correct and well tested since it was written — with
/// <b>zero production callers, all eight in <c>tests/</c></b>. So <c>Receive</c> was only ever
/// reached with <see cref="RelinkClaim.None"/> and every relink branch took the not-a-relink path.
/// Both sides existed; the middle did not.
/// </para>
/// <para>
/// <b>THIS IS WHY A GREEN SUITE PROVED NOTHING BEFORE THIS CHUNK.</b> The capability defaults to
/// <see cref="RelinkClaim.None"/>, which is exactly the old behaviour — so every test here supplies
/// a resolver, and the one that does not is asserting the default rather than the feature.
/// </para>
/// <para>
/// <b>A CLAIM IS NOT A CREDENTIAL.</b> It is unauthenticated text from a stranger and nothing is
/// granted on the strength of it: a matched relink still waits for the DM, every session (R-1.5,
/// D-8). <see cref="AMatchedRelinkStillWaitsForTheDM"/> is the assertion that says so, and it is the
/// one a "helpful" future change would break first.
/// </para>
/// </remarks>
public sealed class TheHostActsOnAnInboundClaimTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 4, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");
    private static readonly Guid Known = Guid.Parse("11111111-2222-3333-4444-555555555555");

    // THE WHOLE HOP. Fails if the claim is not carried to the handler, not resolved, or not passed
    // to the desk -- each of which leaves a returning player indistinguishable from a stranger while
    // every component involved stays individually correct and individually green.
    [Fact]
    public void AClaimOnTheWireReachesTheDMsPromptAsARelink()
    {
        var host = Hosting(WithParticipant("Ysera"));

        var pending = Request(host, claiming: Known.ToString("D"));

        Assert.True(pending.IsRelink);
        Assert.Equal("Ysera", pending.RelinkLabel);
        Assert.Contains("relink as Ysera", AdmissionPrompt.Headline(pending), StringComparison.Ordinal);
    }

    // R-1.5, AND IT IS THE LINE THAT MATTERS MOST. Relink is DM-approved EVERY TIME, so a matched
    // claim changes what the prompt SAYS and nothing else. Fails if a recognised participant is
    // admitted, pre-approved, or given any shortcut -- the "reasonable wrong move" the Spec Owner
    // named on the outbound half, arriving from the other side.
    [Fact]
    public void AMatchedRelinkStillWaitsForTheDM()
    {
        var host = Hosting(WithParticipant("Ysera"));

        var pending = Request(host, claiming: Known.ToString("D"));

        Assert.True(pending.IsRelink);
        Assert.Single(host.Coordinator.Admissions.Pending);
        Assert.Empty(host.Coordinator.Audience.Recipients);
        Assert.False(pending.FingerprintConfirmed);
    }

    // A STRANGER CLAIMING A PARTICIPANT THIS CAMPAIGN DOES NOT KNOW. Fails if an unmatched claim
    // produces a relink -- which would let anyone who guesses or replays a UUID appear to the DM as
    // a returning player, and the UUID is exactly the value D-13 keeps out of other participants'
    // reach for that reason.
    [Fact]
    public void AClaimThisCampaignDoesNotKnowIsNotARelink()
    {
        var host = Hosting(WithParticipant("Ysera"));

        var pending = Request(host, claiming: Guid.NewGuid().ToString("D"));

        Assert.False(pending.IsRelink);
        Assert.Null(pending.RelinkLabel);
    }

    // PARSED RATHER THAN TRUSTED. A joiner controls these characters. Fails if a value that is not a
    // GUID reaches anything that assumes it is one -- BUG-56's shape on the inbound claim.
    [Fact]
    public void AClaimThatIsNotAGuidIsNotARelink()
    {
        var host = Hosting(WithParticipant("Ysera"));

        Assert.False(Request(host, claiming: "not-a-guid").IsRelink);
    }

    // THE ORDINARY PATH, and the negative half that keeps the rest honest: a first-time join carries
    // no claim and must produce a plain prompt. Fails if the absence of a claim is resolved into
    // something -- a match against the first participant, say, which would pass every test above.
    [Fact]
    public void AJoinWithNoClaimIsAnOrdinaryJoin()
    {
        var host = Hosting(WithParticipant("Ysera"));

        var pending = Request(host, claiming: null);

        Assert.False(pending.IsRelink);
        Assert.Contains("asking to join", AdmissionPrompt.Headline(pending), StringComparison.Ordinal);
    }

    // THE LABEL COMES OFF THE PARTICIPANT, NEVER OFF THE REQUEST. The joiner also sends a DISPLAY
    // NAME, which is self-declared and collides freely (A-1.2d, R-1.3e). Fails if the DM's relink
    // line can be driven by what the caller called itself -- at which point "relink as Ysera" is a
    // sentence a stranger can compose.
    [Fact]
    public void TheRelinkLabelIsWhatTheCampaignKnowsNotWhatTheCallerSent()
    {
        var host = Hosting(WithParticipant("Ysera"));

        var pending = Request(host, claiming: Known.ToString("D"), callingItself: "Totally Ysera");

        Assert.Equal("Ysera", pending.RelinkLabel);
        Assert.Equal("Totally Ysera", pending.DisplayName.Value);
    }

    // NO CAMPAIGN, NO RELINK. A host that has not opened a campaign resolves nothing, and that is
    // the DEFAULT capability rather than a special case -- so this also pins that the default is
    // today's behaviour exactly and cannot quietly become something else.
    [Fact]
    public void AHostWithNoCampaignResolvesNothing()
    {
        var host = Hosting(SessionCapabilities.Default);

        Assert.False(Request(host, claiming: Known.ToString("D")).IsRelink);
    }

    private static SessionCapabilities WithParticipant(string label)
    {
        var campaign = new Campaign
        {
            CampaignId = Guid.NewGuid(),
            Participants = [new CampaignParticipant { ParticipantId = Known, Label = label }],
        };

        return new SessionCapabilities(
            ResolveRelink: claimed => CampaignRelink.Resolve(campaign, claimed));
    }

    // Drives the REAL wire: a JoinRequest envelope carrying the claim, decoded by the real codec and
    // dispatched by the real Drain. Building the PendingAdmission by hand would prove the desk works
    // and say nothing about whether a claim survives the journey, which is the entire defect.
    private static PendingAdmission Request(
        Hosted host,
        string? claiming,
        string callingItself = "Bob")
    {
        using var joiner = new SessionKeyExchange();

        var request = claiming is null
            ? WireEnvelope.ForJoinRequest(Code, joiner.PublicKey, DisplayName.OrNone(callingItself))
            : Claiming(joiner.PublicKey, claiming, callingItself);

        host.Transport.Deliver(request);
        host.Coordinator.Tick(TimeSpan.Zero, Now);

        return Assert.Single(host.Coordinator.Admissions.Pending);
    }

    // Built through the codec's own shape because ForRelinkRequest takes a Guid and half these cases
    // are values it would refuse to construct -- which is correct of the factory and exactly why a
    // hostile joiner has to be modelled at the wire.
    private static WireEnvelope Claiming(byte[] joinerPublicKey, string claimed, string callingItself) =>
        WireEnvelope.FromWire(
            WireMessageType.JoinRequest,
            Code.Value,
            new WireShape
            {
                PublicKey = joinerPublicKey,
                DisplayName = callingItself,
                ClaimedParticipantId = claimed,
            });

    /// <summary>A hosting coordinator and the transport frames arrive on.</summary>
    private sealed record Hosted(SessionCoordinator Coordinator, DeliveringTransport Transport);

    private static Hosted Hosting(SessionCapabilities capabilities)
    {
        var transport = new DeliveringTransport();
        var host = new SessionCoordinator(
            transport,
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            SilentLog.Instance,
            capabilities);

        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();
        return new Hosted(host, transport);
    }

    private sealed class DeliveringTransport : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
        }

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));
    }
}
