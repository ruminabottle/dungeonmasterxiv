using System;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.5's non-negotiable: <b>a matching participant ID must not auto-admit.</b>
/// </summary>
/// <remarks>
/// <para>
/// Every test here uses a <b>GOOD</b> claim — one that resolves. That is deliberate and it is the
/// point of the file. A relink that auto-admits on a valid match passes every test about forged and
/// mismatched ids, because those never reach the admission path at all. The rejection tests are in
/// <c>RelinkResolutionTests</c>; the dangerous case is this one.
/// </para>
/// <para>
/// The DM approves every relink, every session. A match changes what the prompt says and never the
/// number of steps to get past it — the same shape as D-11's fingerprint, where a check the user is
/// nudged past is worse than no check, because the UI then records that verification happened.
/// </para>
/// </remarks>
public class RelinkRequiresApprovalTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 3, 0, 0, TimeSpan.Zero);

    private static (SessionCoordinator Coordinator, RelinkClaim Claim) HostWithAReturningPlayer()
    {
        var coordinator = new SessionCoordinator(new SilentTransport(), () => RelayEndpoint.Default, GraceWindow.Default);
        coordinator.StartHosting();
        coordinator.Host.Registered();

        var campaign = new Campaign { CampaignId = Guid.NewGuid() };
        var participant = new CampaignParticipant { ParticipantId = Guid.NewGuid(), Label = "Ysera" };
        campaign.Participants.Add(participant);

        var claim = CampaignRelink.Resolve(campaign, participant.ParticipantId.ToString("D"));
        Assert.True(claim.Matched, "fixture must present a claim that genuinely resolves");

        return (coordinator, claim);
    }

    // THE test. A resolved relink is still a PENDING request: nobody is admitted by resolving one.
    [Fact]
    public void AMatchingParticipantIdAdmitsNobody()
    {
        var (coordinator, claim) = HostWithAReturningPlayer();

        var request = coordinator.ReceiveJoinRequest("PEER-3", new byte[] { 1, 2, 3 }, Now, claim);

        Assert.NotNull(request);
        Assert.True(request!.IsRelink);
        Assert.Empty(coordinator.Audience.Recipients);
        Assert.Single(coordinator.Admissions.Pending);
    }

    // A relink must not arrive pre-confirmed. R-1.3a forbids a pre-ticked box, and a match is
    // exactly the excuse someone would use to tick it.
    [Fact]
    public void AMatchingParticipantIdDoesNotPreConfirmTheFingerprint()
    {
        var (coordinator, claim) = HostWithAReturningPlayer();

        var request = coordinator.ReceiveJoinRequest("PEER-3", new byte[] { 1, 2, 3 }, Now, claim);

        Assert.False(request!.FingerprintConfirmed);
        Assert.Equal(AdmissionVerification.NotCompared, request.Verification);
    }

    // A relink takes exactly the same steps as a first-time join. Fails if a match ever shortens
    // the path -- which is the failure mode that looks finished.
    [Fact]
    public void ARelinkRequiresTheSameStepsAsAFirstTimeJoin()
    {
        var (relinking, claim) = HostWithAReturningPlayer();
        var (joining, _) = HostWithAReturningPlayer();

        var relink = relinking.ReceiveJoinRequest("PEER-3", new byte[] { 1, 2, 3 }, Now, claim);
        var join = joining.ReceiveJoinRequest("PEER-4", new byte[] { 1, 2, 3 }, Now, RelinkClaim.None);

        // Same starting state on every field that governs whether admission may proceed.
        Assert.Equal(join!.FingerprintConfirmed, relink!.FingerprintConfirmed);
        Assert.Equal(join.Verification, relink.Verification);
        Assert.Equal(join.Deadline.UtcTicks, relink.Deadline.UtcTicks);
        Assert.Equal(joining.Audience.Count, relinking.Audience.Count);
        Assert.Equal(joining.Admissions.Pending.Count, relinking.Admissions.Pending.Count);
    }

    // The sharper failing input. A prompt that opens with Accept selected STILL REQUIRES APPROVAL
    // and still records that approval happened -- so it passes every test above. It is forbidden for
    // the same reason a nudged-past fingerprint check is worse than no check: the DM who pressed
    // Enter on a focused button leaves the record of a DM who compared out of band, and R-1.3a's
    // design rests on those two being distinguishable.
    [Fact]
    public void ThePromptFavoursNeitherAnswerOnAResolvedRelink()
    {
        var (coordinator, claim) = HostWithAReturningPlayer();

        var request = coordinator.ReceiveJoinRequest("PEER-3", new byte[] { 1, 2, 3 }, Now, claim);

        Assert.Equal(AdmissionAction.None, AdmissionPrompt.Favoured(request!));
    }

    // And a relink is favoured no differently from a first-time join, so "helpful for people we
    // recognise" cannot creep in as an asymmetry between the two paths.
    [Fact]
    public void ARelinkPromptFavoursTheSameAnswerAsAFirstTimeJoinPrompt()
    {
        var (relinking, claim) = HostWithAReturningPlayer();
        var (joining, _) = HostWithAReturningPlayer();

        var relink = relinking.ReceiveJoinRequest("PEER-3", new byte[] { 1, 2, 3 }, Now, claim);
        var join = joining.ReceiveJoinRequest("PEER-4", new byte[] { 1, 2, 3 }, Now, RelinkClaim.None);

        Assert.Equal(AdmissionPrompt.Favoured(join!), AdmissionPrompt.Favoured(relink!));
        Assert.Equal(AdmissionAction.None, AdmissionPrompt.Favoured(relink!));
    }

    // Denying a relink denies it. Fails if a resolved claim survives a denial in any form.
    [Fact]
    public void AResolvedRelinkCanStillBeDenied()
    {
        var (coordinator, claim) = HostWithAReturningPlayer();
        coordinator.ReceiveJoinRequest("PEER-3", new byte[] { 1, 2, 3 }, Now, claim);

        coordinator.Deny("PEER-3");

        Assert.Empty(coordinator.Audience.Recipients);
        Assert.Empty(coordinator.Admissions.Pending);
    }

    // And when the DM does approve, it is the approval that admits -- not the match.
    [Fact]
    public void OnlyTheDmsApprovalAdmitsARelinkingParticipant()
    {
        var (coordinator, claim) = HostWithAReturningPlayer();
        coordinator.ReceiveJoinRequest("PEER-3", new byte[] { 1, 2, 3 }, Now, claim);
        Assert.Empty(coordinator.Audience.Recipients);

        coordinator.Admit("PEER-3");

        Assert.Single(coordinator.Audience.Recipients);
    }

    // These tests drive the coordinator directly rather than through the wire, so this stub never
    // raises either event. They are declared because the interface requires them, and the compiler
    // is right that nothing here uses them.
#pragma warning disable CS0067
    private sealed class SilentTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public bool IsConnected { get; private set; }

        // A fake socket is open the instant it connects, so readiness follows connection here.
        // The real WebSocket does not (BUG-36), which is why the coordinator asks this and not
        // IsConnected -- and why TheHostRegistersItsCodeTests drives the two apart deliberately.
        public bool IsReadyToSend => IsConnected;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] frame)
        {
        }

        public void Dispose() => Disconnect();
    }
#pragma warning restore CS0067
}
