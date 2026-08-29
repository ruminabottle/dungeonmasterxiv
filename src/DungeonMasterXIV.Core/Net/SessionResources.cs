using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What a hosted session holds, and the one place that releases all of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS NAMES A SET THAT HAD NO NAME, AND ITS NAMELESSNESS WAS THE ROOT OF BUG-90.</b> Until
/// now, "the things a hosted session owns" existed only as the body of
/// <see cref="HostRunner.Stop"/> — so every method that needed the set was trusted to remember it,
/// and <see cref="HostRunner.Start"/> did not: it resets two of them where <c>Stop</c> releases
/// five. That asymmetry is not a missing line, it is what happens to a list nobody wrote down.
/// </para>
/// <para>
/// <b>Why it exists NOW rather than as tidying.</b> <see cref="HostRunner"/>'s constructor reached
/// <b>seven parameters against a block of six</b> — the only such breach in production code — and
/// DMXENG-50's two teardown calls would have taken it to nine. <b>This codebase has already made
/// exactly this move for exactly this row:</b> <see cref="InboundHandlers"/> exists because
/// <c>AdmissionInbox.Drain</c> "had reached six parameters — the block row in the engineering
/// standards". Same row, same remedy, cited rather than invented.
/// </para>
/// <para>
/// <b>It bundles references; it does not take ownership.</b> The coordinator still hands
/// <see cref="AdmissionControl"/> and <see cref="AdmissionInbox"/> to other collaborators, and
/// nothing here changes who may use them. What this type owns is <i>the question of what must be
/// let go when a session ends</i>, which is a different thing from owning the objects — and it is
/// the question that was previously answered by whoever last edited a method body.
/// </para>
/// <para>
/// <b>The breach was invisible because the instrument that would have caught it does not measure
/// that row.</b> <c>tools/DungeonMasterXIV.Sizes</c> reads type spans and file spans — two of the
/// five rows in the size table. Method length, parameter counts and nesting depth are measured by
/// nothing, and the one production breach in the repository sat in a row the tool cannot see, on a
/// PR reviewed with unusual attention to size. <b>An instrument that covers part of a rule quietly
/// redefines the rule as the part it covers</b>, and the reason that is hard to notice is that the
/// part it does cover keeps coming back clean.
/// </para>
/// </remarks>
internal sealed class SessionResources
{
    private readonly AdmissionControl _admissions;
    private readonly AdmissionInbox _inbox;
    private readonly Func<GraceWindow> _grace;

    /// <param name="admissions">Who is admitted and who is waiting.</param>
    /// <param name="inbox">Frames that arrived and have not been applied.</param>
    /// <param name="grace">
    /// The seat clock, read at use time rather than captured, because it belongs to
    /// <c>SessionInterruption</c> and is reached through the coordinator.
    /// </param>
    /// <param name="memberKeys">Keys derived to open member-authored content (R-1.3k).</param>
    /// <param name="memberContent">What the host has heard from its members (A-1.13c).</param>
    public SessionResources(
        AdmissionControl admissions,
        AdmissionInbox inbox,
        Func<GraceWindow> grace,
        MemberContentKeys memberKeys,
        MemberContentReceipts memberContent)
    {
        // DMXENG-45's rule. Every argument here arrives from a field assigned earlier in
        // SessionCoordinator's constructor, so building this type too early passes a null nothing
        // would refuse -- the assignment succeeds and the failure surfaces on a hosting path, or
        // never in a test that does not host.
        ArgumentNullException.ThrowIfNull(admissions);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(grace);
        ArgumentNullException.ThrowIfNull(memberKeys);
        ArgumentNullException.ThrowIfNull(memberContent);

        _admissions = admissions;
        _inbox = inbox;
        _grace = grace;
        MemberKeys = memberKeys;
        MemberContent = memberContent;
    }

    /// <summary>Keys this host can open member-authored content with (R-1.3k).</summary>
    /// <remarks>
    /// Exposed because the drain needs the candidates every tick, not only at teardown. The other
    /// three are release-only and stay private, which is the difference between a bundle and a bag.
    /// </remarks>
    public MemberContentKeys MemberKeys { get; }

    /// <summary>What this host has heard from its members (A-1.13c).</summary>
    public MemberContentReceipts MemberContent { get; }

    /// <summary>
    /// Lets go of everything the session was holding. Called when hosting ends, and nowhere else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The order is preserved from the method this was cut out of, deliberately.</b> Nothing
    /// here depends on the order today — each step touches a different object — but this arrived as
    /// a pure extraction of an existing body, and reordering on the way past is how a refactor
    /// stops being reviewable as one.
    /// </para>
    /// <para>
    /// <b>The keys are ZEROED rather than dropped.</b> The host's key pair is disposed by
    /// <see cref="HostRunner.Stop"/> immediately before this runs, and keys derived from it left
    /// live in the heap would undo that at one remove (D-8).
    /// </para>
    /// </remarks>
    public void Release()
    {
        _admissions.Clear();
        _inbox.Clear();
        MemberKeys.Forget();
        MemberContent.Clear();
        _grace().Reset();
    }
}
