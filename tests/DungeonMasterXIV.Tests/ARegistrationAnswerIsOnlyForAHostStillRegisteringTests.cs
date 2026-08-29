using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-116: the phase guard in <c>ApplyRegistration</c> — a host that is no longer registering must
/// not have a registration answer applied, and must not have the frame CONSUMED.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CODE MUST MATCH IN EVERY TEST HERE, AND THAT IS THE WHOLE DESIGN.</b> There are two guards
/// in this function: the phase check and BUG-89's code check. A test using a MIS-matched code is
/// refused by the code guard, so deleting the phase guard changes nothing and the test passes against
/// both. Every case below therefore hands the host <b>its own outstanding code</b>, leaving the phase
/// check as the only thing that can refuse it.
/// </para>
/// <para>
/// <b>Why it looked covered: its neighbour is pinned.</b> Six tests redden when
/// <c>ApplyRegistration</c> is made to always report not-handled — but that is a WHOLE-FUNCTION
/// mutation, and it cannot say which of the two guards is load-bearing because it disables both. The
/// block-level mutation reddened six; the line-level deletion of these four lines reddened none. The
/// tested guard was supplying the confidence for the untested one.
/// </para>
/// <para>
/// <b>What the guard prevents is CONSUMPTION, not action.</b> <c>Registered()</c> and
/// <c>CodeAlreadyLive()</c> each carry their own phase check, so without this guard they would no-op
/// — and <c>ApplyRegistration</c> would still <c>return true</c>. The frame is then eaten by a branch
/// that did nothing with it, which is BUG-43 exactly: a joiner's <c>CodeRefused</c> consumed before
/// it could reach a joiner arm. So these assert the RETURN VALUE, because that is the harm.
/// </para>
/// </remarks>
public class ARegistrationAnswerIsOnlyForAHostStillRegisteringTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // THE PREMISE. If the host were not holding this exact code, BUG-89's guard would refuse the
    // envelope and every case below would pass with the phase check deleted.
    [Fact]
    public void AHostThatIsHostingStillHoldsTheCodeItRegistered()
    {
        var host = AHostThatFinishedRegistering();

        Assert.Equal(HostingPhase.Hosting, host.Phase);
        Assert.Equal(Code, host.Code!.Value);
    }

    // THE DEFECT. Fails if: the phase check is removed. The code matches, so BUG-89's guard lets it
    // through; Registered() then no-ops on its own phase check and the function still reports
    // HANDLED, eating a frame it did nothing with.
    [Fact]
    public void AnAcceptanceArrivingAfterRegistrationIsNotConsumed()
    {
        var host = AHostThatFinishedRegistering();

        var handled = InboundApplication.ApplyRegistration(WireEnvelope.ForCodeAccepted(Code), host);

        Assert.False(
            handled,
            "A host that is already Hosting reported an acceptance as HANDLED. The frame is consumed "
            + "and can reach no other arm -- BUG-43. The clause under test is the phase check, not "
            + "the code check: the code matches deliberately so only the phase can refuse it.");
        Assert.Equal(HostingPhase.Hosting, host.Phase);
    }

    // The refusal arm needs it for the same reason, and this is the arm BUG-43 actually travelled:
    // a CodeRefused consumed by a branch that did nothing before a joiner could be told.
    [Fact]
    public void ARefusalArrivingAfterRegistrationIsNotConsumed()
    {
        var host = AHostThatFinishedRegistering();

        var handled = InboundApplication.ApplyRegistration(WireEnvelope.ForCodeRefused(Code), host);

        Assert.False(
            handled,
            "A host that is already Hosting reported a refusal as HANDLED, so it could never reach a "
            + "joiner arm -- BUG-43 exactly. The clause under test is the phase check.");
        Assert.Equal(Code, host.Code!.Value);
    }

    // A host that never started is the other side of the same line, and its code is null -- so this
    // one IS also refused by BUG-89's guard. Asserted anyway because the phase check is what SHOULD
    // refuse it, and stating that keeps the two clauses' jobs distinct.
    [Fact]
    public void AnAnswerToAClientThatIsNotHostingIsNotConsumed()
    {
        var host = new HostSession();

        Assert.False(InboundApplication.ApplyRegistration(WireEnvelope.ForCodeAccepted(Code), host));
        Assert.Equal(HostingPhase.NotHosting, host.Phase);
    }

    // THE OTHER DIRECTION, and the half a too-broad guard would break: a host that IS registering,
    // holding this code, must still be registered by it.
    [Fact]
    public void AHostStillRegisteringIsStillRegisteredByItsOwnAnswer()
    {
        var host = new HostSession();
        host.Start(Code);

        var handled = InboundApplication.ApplyRegistration(WireEnvelope.ForCodeAccepted(Code), host);

        Assert.True(handled);
        Assert.Equal(HostingPhase.Hosting, host.Phase);
    }

    /// <summary>Registering finished: phase is <c>Hosting</c> and the code is still held.</summary>
    private static HostSession AHostThatFinishedRegistering()
    {
        var host = new HostSession();
        host.Start(Code);
        host.Registered();
        return host;
    }
}
