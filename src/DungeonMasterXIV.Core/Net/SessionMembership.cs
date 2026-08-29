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
}
