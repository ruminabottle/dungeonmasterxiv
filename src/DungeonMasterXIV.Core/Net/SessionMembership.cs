using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What this client is once it is <i>inside</i> somebody else's session: the key it derived on
/// being admitted, and its ability to tell the host it is leaving.
/// </summary>
/// <remarks>
/// <para>
/// <b>Being a member is not the same thing as asking to become one.</b> <see cref="JoinRequester"/>
/// owns the asking — the request, the retries, the failure modes of a join that never completes.
/// This type owns the state that exists only <i>after</i> the host says yes. The two were adjacent
/// on <c>SessionCoordinator</c> and read as one surface, which is why the boundary is worth stating
/// rather than leaving to the reader: <b>everything here presupposes admission.</b>
/// </para>
/// <para>
/// <b>THE PIECES ALREADY EXISTED; THE CONCEPT DID NOT.</b> <see cref="JoinRequester.SessionKey"/>
/// and <see cref="MemberDeparture"/> were two collaborators with no common owner, and the
/// coordinator was the only thing that knew they were one story — it held the key on one object and
/// the send on another and bridged them with a closure. That is the shape a missing type makes.
/// </para>
/// <para>
/// <b>AND THE BRIDGE WAS A <c>this</c>-CAPTURE, WHICH THIS REMOVES.</b> The coordinator built
/// <see cref="MemberDeparture"/> with <c>() =&gt; SessionKey</c> — a closure over the
/// <i>coordinator</i>, created part-way through its own constructor, because the key lived on an
/// object built forty lines further down. Here the same closure captures the
/// <see cref="JoinRequester"/> passed in, never <c>this</c>. <b>One fewer escaped reference in the
/// composition root</b>, which is the hazard DMXENG-45 built a detector for and the reason the
/// constructor itself was ruled unmovable.
/// </para>
/// <para>
/// <b>This is a move, not new behaviour.</b> Every member below forwards to the same collaborator
/// it forwarded to before, with the same nullability and the same meaning.
/// </para>
/// </remarks>
public sealed class SessionMembership
{
    private readonly JoinRequester _joiner;
    private readonly ReceivedClosing _closing = new();
    private readonly MemberDeparture _departure;

    /// <summary>Binds the member half to the joiner that owns its key material.</summary>
    /// <param name="link">The transport this client speaks to the relay over.</param>
    /// <param name="joiner">
    /// The join attempt whose key this membership reads. Captured directly rather than through the
    /// coordinator, which is what keeps the departure closure free of <c>this</c>.
    /// </param>
    /// <param name="code">
    /// The session code to seal against, read at send time rather than captured, because a client
    /// can leave one session and join another without this type being rebuilt.
    /// </param>
    /// <remarks><b>Internal because <see cref="JoinRequester"/> is.</b> The type is public — the
    /// coordinator hands it to callers — but only the composition root can build one.</remarks>
    internal SessionMembership(RelayLink link, JoinRequester joiner, Func<SessionCode?> code)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(joiner);
        ArgumentNullException.ThrowIfNull(code);

        _joiner = joiner;
        _departure = new MemberDeparture(link, code, () => joiner.SessionKey);
    }

    /// <summary>This client's key pair when joining somebody else's session, or null.</summary>
    /// <remarks>Owned by <see cref="JoinRequester"/>, which is the only thing that creates it.</remarks>
    public SessionKeyExchange? Keys => _joiner.Keys;

    /// <summary>
    /// The key this client derived on being admitted, or null. Present only once the host's key has
    /// arrived — which is why the acceptance has to carry it.
    /// </summary>
    /// <remarks>
    /// <b>Settable because the inbound drain produces it, not this type.</b> The write stays
    /// internal for the same reason it always was: exactly two places produce this value, the
    /// request and the acceptance, and neither is a caller outside this assembly.
    /// </remarks>
    public byte[]? SessionKey
    {
        get => _joiner.SessionKey;
        internal set => _joiner.SessionKey = value;
    }

    /// <summary>
    /// Tells the host this client is leaving, so it is removed at once (R-1.3g, A-1.16a). Returns
    /// whether anything was sent.
    /// </summary>
    /// <remarks>
    /// <b>False and silent when there is nothing to leave</b> — no code, or no shared key because
    /// this client was never admitted. Quitting the join screen has nobody to tell.
    /// </remarks>
    public bool AnnounceDeparture() => _departure.Announce();

    /// <summary>
    /// What the host has said about this session ending, or null if it has said nothing (R-1.3g).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The joiner's half of a notice the product has published since DMXENG-58 and nobody read.</b>
    /// Measured before this: <c>SessionClosing</c> had zero occurrences under <c>Windows/</c> or
    /// <c>Plugin.cs</c>, so the host sealed a closing instant to every participant and every
    /// participant discarded it. R-1.3g requires them to see both THAT it is closing and HOW LONG
    /// REMAINS, and the countdown is a requirement rather than a courtesy — "the session is closing"
    /// without a duration is the indefinite wait R-1.3c and R-1.8 forbid.
    /// </para>
    /// <para>
    /// <b>Null on the host, by design.</b> D-3 makes the host the author; a host reading its own
    /// broadcast back would be believing a copy of what it already decided. See
    /// <see cref="ReceivedClosing"/> for the two ways a notice can be LOST after it arrives.
    /// </para>
    /// </remarks>
    public SessionClosing? Closing => _closing.Notice;

    /// <summary>
    /// Leaves this session because the player asked to (R-1.3g, A-1.16a).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The notice is best effort and the departure is not.</b> The host is told first, so its
    /// roster can drop this client at once, and the teardown then runs WHETHER OR NOT that send
    /// succeeded. A leave conditional on an acknowledgement is a player held in a session by a host
    /// that never answers, which is the state this exists to remove.
    /// </para>
    /// <para>
    /// <b>An undelivered notice is not a defect, and R-1.5a is why.</b> From the host's side a joiner
    /// whose notice never arrived is a client that VANISHED, and holding its seat for five minutes is
    /// then CORRECT. PRD-1:733 says in terms that removing vanished members to close that apparent
    /// gap files a false gap and breaks R-1.5a.
    /// </para>
    /// <para>
    /// <b>Announcing stays its own member rather than being folded in.</b> It is a distinct wire act
    /// with its own coverage — the first member-authored send in the product — and hiding it behind
    /// this would delete that coverage to save a line.
    /// </para>
    /// </remarks>
    public void Leave()
    {
        AnnounceDeparture();
        _closing.Clear();
        _joiner.Left();
    }

    /// <summary>
    /// Releases what this client held for a session that has now closed (R-1.3g).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A KEY-LIFETIME PROPERTY, and deliberately not argued as a D-8 breach.</b> The key derived
    /// at admission belongs to one session; when the host's closing instant passes, that session is
    /// over and the key should go with it. <b>The cross-session exposure is NOT reachable</b> —
    /// <see cref="JoinRequester.Request"/> releases before minting, so no key survives into a
    /// different session code. Naming this as D-8 would be a right conclusion with a wrong reason,
    /// and the next reader would look for cross-session leakage, find none, and conclude the release
    /// was unnecessary. <b>The real residual is narrower: the key outlives its own session</b>, from
    /// close-expiry until the next join or shutdown.
    /// </para>
    /// <para>
    /// <b>Nothing was watching this instant at all.</b> Before this, <see cref="SessionClosing.HasClosedAt"/>
    /// had zero production callers — the countdown was rendered and its expiry acted on by nobody.
    /// </para>
    /// <para>
    /// <b>No departure is announced, and that is the difference from <see cref="Leave"/>.</b> The
    /// host ended this; telling it we are leaving a session it has already closed asserts something
    /// D-3 gives it and not us. The teardown is shared because it is the same teardown; only the
    /// notice differs.
    /// </para>
    /// <para>
    /// <b>The notice is cleared so this fires ONCE.</b> Left standing it would re-run every frame,
    /// and the teardown synchronises the transport — a per-frame reconnect for a session that no
    /// longer exists.
    /// </para>
    /// </remarks>
    /// <param name="now">The current instant, supplied by the frame rather than read.</param>
    internal void ExpireIfTheSessionHasClosed(DateTimeOffset now)
    {
        if (_closing.Notice is { } notice && notice.HasClosedAt(now))
        {
            _closing.Clear();
            _joiner.Left();
        }
    }

    /// <summary>
    /// Records a closing instant that arrived from the host, if the payload carried one (R-1.3g).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE LINE THIS WHOLE CHUNK EXISTS FOR.</b> The closing instant was already arriving at the
    /// client and being discarded: the frame handler read <c>content.Roster</c> and nothing else, so
    /// a participant of a session the DM had ended saw a roster that never changed and was told
    /// nothing at all. The notice had been sent since DMXENG-58 and read by no one.
    /// </para>
    /// <para>
    /// <b>Both halves come off the SAME <see cref="SessionContent"/></b>, which is why the caller
    /// applies them together rather than through two independent lambdas that could disagree about
    /// which payload they were looking at.
    /// </para>
    /// <para>
    /// <b>What a MISSING field means is decided in <see cref="ReceivedClosing"/>, not here.</b> Most
    /// payloads carry no closing, so "absent" must not read as "no longer closing" — and a malformed
    /// value must not read as a retraction either, because under D-3 only the host decides.
    /// </para>
    /// </remarks>
    /// <param name="utcTicks">The instant from the payload, or null.</param>
    internal void HeardFromTheHost(long? utcTicks) => _closing.Apply(utcTicks);
}
