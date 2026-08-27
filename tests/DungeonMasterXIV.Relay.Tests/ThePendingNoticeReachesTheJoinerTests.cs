using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Sessions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// The relay's half of A-1.3f-1: a <see cref="WireMessageType.JoinPending"/> carrying the host's key
/// must actually reach the waiting joiner.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file exists because the first version of the fix was correct and unreachable.</b> The
/// plugin sent the notice and the router had no arm for it, so it fell to the D-14 catch-all and was
/// dropped. Every plugin-side test passed: they were written against a socket double, which proves
/// what the host <i>puts on a wire</i> and nothing about what a relay <i>does with it</i>. Tests on
/// both sides of a seam, each written against its own idea of the agreement, both pass and together
/// cover nothing.
/// </para>
/// <para>
/// <b>Nor could probing by removal have found it.</b> Suppressing the host's send proved the ordering
/// tests depend on that send. It could not show whether a recipient exists downstream: a probe by
/// removal tests the code you removed, never the code you never wrote.
/// </para>
/// <para>
/// So every case here carries a <b>positive control on the same registry, router and session</b> — a
/// message that must be forwarded — so a green result cannot come from a harness in which nothing
/// would have been forwarded anyway.
/// </para>
/// </remarks>
public sealed class ThePendingNoticeReachesTheJoinerTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");
    private static readonly byte[] JoinerKey = [1, 2, 3];
    private static readonly byte[] HostKey = [4, 5, 6];

    private readonly SessionRegistry _registry = new();
    private readonly RelayRouter _router;

    public ThePendingNoticeReachesTheJoinerTests() => _router = new RelayRouter(_registry);

    // THE DEFECT THIS FILE WAS WRITTEN FOR. Fails if: JoinPending has no arm in the router and falls
    // to the D-14 catch-all, which is exactly what shipped in the first version of PR #53.
    [Fact]
    public void APendingNoticeIsForwardedToTheWaitingJoiner()
    {
        AJoinerIsWaiting();

        var decision = RoutePendingNotice(from: "host-1");

        Assert.Equal(RelayAction.Forward, decision.Action);
        Assert.Equal(RelayOutcome.PendingNoticeForwarded, decision.Outcome);
        Assert.Equal(["joiner-1"], decision.Recipients);
    }

    // POSITIVE CONTROL for the test above, on the same registry, router and session. If this were to
    // drop as well, the harness would be the explanation and the test above would prove nothing.
    [Fact]
    public void AnAcceptanceOnTheSamePathIsForwarded()
    {
        AJoinerIsWaiting();

        var decision = _router.Route(
            WireEnvelope.ForJoinAccepted(Code, JoinerKey, HostKey), "host-1");

        Assert.Equal(RelayAction.Forward, decision.Action);
        Assert.Equal(["joiner-1"], decision.Recipients);
    }

    // The gate must be exactly where it was. Fails if: the notice is routed through the admission
    // path, which resolves the pending entry — reusing RouteAdmission with admit:false would deliver
    // this and deny the joiner in the same call, and reusing it with admit:true would admit someone
    // the DM has not answered about.
    [Fact]
    public void ForwardingANoticeAdmitsNobodyAndStrandsNobody()
    {
        AJoinerIsWaiting();

        RoutePendingNotice(from: "host-1");

        // Still pending: not yet a member, and an acceptance afterwards still finds them. If the
        // notice had resolved the entry, this acceptance would drop as an unknown joiner.
        Assert.False(_registry.IsMember(Code.Value, "joiner-1"));

        var acceptance = _router.Route(
            WireEnvelope.ForJoinAccepted(Code, JoinerKey, HostKey), "host-1");

        Assert.Equal(RelayOutcome.JoinerAdmitted, acceptance.Outcome);
        Assert.True(_registry.IsMember(Code.Value, "joiner-1"));
    }

    // Only the host may name the key a joiner should expect. Fails if: the arm forwards from any
    // connection — which would let a third party post a key of their own choosing to the joiner, who
    // would compare its fingerprint and find it matches. That inverts the defence rather than
    // weakening it, so it is refused rather than obeyed.
    [Fact]
    public void APendingNoticeFromSomebodyOtherThanTheHostIsRefused()
    {
        AJoinerIsWaiting();

        var decision = RoutePendingNotice(from: "interloper-1");

        Assert.Equal(RelayAction.Drop, decision.Action);
        Assert.Equal(RelayOutcome.AdmissionFromNonHost, decision.Outcome);
    }

    // Fails if: a notice for somebody who is not waiting resolves to whoever happens to be pending,
    // the same reasoning TryAdmit already applies to a stale or invented decision.
    [Fact]
    public void APendingNoticeNamingNobodyWaitingIsDropped()
    {
        AJoinerIsWaiting();

        var decision = _router.Route(
            WireEnvelope.ForJoinPending(Code, [9, 9, 9], HostKey, Deadline()), "host-1");

        Assert.Equal(RelayAction.Drop, decision.Action);
        Assert.Equal(RelayOutcome.UnknownJoiner, decision.Outcome);
    }

    private void AJoinerIsWaiting()
    {
        _router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");
        _router.Route(WireEnvelope.ForJoinRequest(Code, JoinerKey), "joiner-1");
    }

    private RelayDecision RoutePendingNotice(string from) =>
        _router.Route(WireEnvelope.ForJoinPending(Code, JoinerKey, HostKey, Deadline()), from);

    private static AdmissionDeadline Deadline() =>
        AdmissionDeadline.DecidedByHost(new DateTimeOffset(2026, 8, 27, 19, 0, 0, TimeSpan.Zero));
}
