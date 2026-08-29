using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// When each member's connection was last seen to drop, so the host can decide what a returning
/// client gets (R-1.5a, A-1.28).
/// </summary>
/// <remarks>
/// <para>
/// <b>A RECORDED INSTANT, NOT A RUNNING CLOCK, and the Spec Owner ruled the difference.</b> R-1.5a
/// constrains the decision taken WHEN A CLIENT RETURNS — same key within five minutes resumes, same
/// key after five minutes needs full approval. It says nothing about the roster's state while
/// nobody is looking. So there is <b>no ticking clock, no expiry sweep, and no seat that visibly
/// lapses</b>: a recorded instant compared on return satisfies it in full.
/// </para>
/// <para>
/// <b>Why the HOST has to be the one holding it.</b> Both outcomes are the host's to produce, and
/// <i>"a joiner's own claim about how long it has been away is precisely the assertion D-3 says the
/// host does not take."</i> A build that records nothing cannot produce both rows — it either
/// always resumes or always re-approves, violating one of them whichever it picks.
/// </para>
/// <para>
/// <b>Keyed by peer code rather than by public key, because the peer code IS the host's name for a
/// member.</b> The relay names a dropped member by the key it joined with; the host turns that into
/// a peer code through the one derivation it already uses for everything else, and from there the
/// member is the same subject in the roster, the prompt and here.
/// </para>
/// <para>
/// <b>Recording a drop changes nothing about membership.</b> A-1.29: a relay notice does not itself
/// change the roster, and R-1.5a HOLDS the seat rather than removing it. Nothing in this type
/// removes anybody, and that is the point rather than an omission.
/// </para>
/// </remarks>
public sealed class MemberDrops
{
    private readonly Dictionary<PeerCode, DateTimeOffset> _dropped = new();

    /// <summary>How many members are currently recorded as having dropped.</summary>
    public int Count => _dropped.Count;

    /// <summary>
    /// Records that <paramref name="peerCode"/>'s connection went away at <paramref name="when"/>.
    /// </summary>
    /// <remarks>
    /// <b>The LATEST drop wins, and that is deliberate rather than incidental.</b> A member that
    /// drops, returns and drops again has a new clock to be measured against; keeping the first
    /// instant would age them out on the strength of an absence they have already come back from.
    /// </remarks>
    public void Record(PeerCode peerCode, DateTimeOffset when) => _dropped[peerCode] = when;

    /// <summary>
    /// Forgets any recorded drop for <paramref name="peerCode"/>, because they are back.
    /// </summary>
    /// <remarks>
    /// <b>Called when a member is admitted, not when a frame arrives from them.</b> Admission is the
    /// decision R-1.5a is about; traffic is not, and treating it as such would be the
    /// infer-from-silence defect A-1.28 forbids, arriving from its cheerful side.
    /// </remarks>
    public bool Forget(PeerCode peerCode) => _dropped.Remove(peerCode);

    /// <summary>
    /// When <paramref name="peerCode"/> was last seen to drop, or null if they are not recorded as
    /// having dropped at all.
    /// </summary>
    public DateTimeOffset? WhenDropped(PeerCode peerCode) =>
        _dropped.TryGetValue(peerCode, out var when) ? when : null;

    /// <summary>Forgets every recorded drop, because the session is over.</summary>
    public void Clear() => _dropped.Clear();
}
