namespace DungeonMasterXIV.Net;

/// <summary>
/// One line of the session stream: what happened, who it was, and the host's place-and-time for it
/// (R-2.3, R-2.4).
/// </summary>
/// <remarks>
/// <b>A CLASS, NOT A RECORD STRUCT, AND THAT IS THE WHOLE POINT OF THE TYPE.</b> An entry without
/// the host's order and clock is not a stream entry — but <b>every struct has an implicit
/// parameterless constructor</b>, so as a record struct <c>new StreamEntry()</c> compiled, carried
/// <c>Sequence 0</c>, and <b>SORTED TO THE FRONT OF A POPULATED LOG</b>. Measured, not reasoned: the
/// first draft was a record struct and a probe confirmed it. The doc then claimed no such constructor
/// existed, which was false in the file asserting it.
///
/// A reference type has no implicit parameterless constructor, so <c>new StreamEntry()</c> no longer
/// compiles.
///
/// <b>THAT IS NOT THE SAME AS UNCONSTRUCTABLE, AND THE SENTENCE THAT SAID SO WAS THE THIRD OVER-CLAIM
/// IN THIS FILE (BUG-161).</b> <see cref="StreamStamp"/> is a readonly record struct, so
/// <c>default(StreamStamp)</c> exists and <c>new StreamEntry(default, kind, peer, text)</c> COMPILES,
/// carrying <c>Sequence 0</c>. Verified by compiling it, not by reasoning. The class fix moved the
/// hazard down one level rather than removing it.
///
/// <b>So the guarantee is enforced where it lands, not by the type system:</b>
/// <see cref="SessionStream.Record"/> REFUSES a stamp that was never minted. A host sequence starts
/// at 1, so <c>Sequence &lt; 1</c> is definitionally not host-issued — which is the invariant rather
/// than an arbitrary bound. The violating expression above is written down here so the next reader
/// can run it instead of trusting this paragraph.
/// </remarks>
/// <param name="Stamp">The host's order and time. See <see cref="StreamStamp"/>.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Peer">
/// Whose event it was. <b>The validated type, not a string</b> — <c>PeerCodeIsTheOnlyDoorTests</c>
/// caught the first draft of this file taking a bare one, which is the third time that guard has
/// fired on a new surface. A stream entry outlives the frame it arrived in, so an unvalidated code
/// here would be a bad value with a long life.
/// </param>
/// <param name="Text">What was said or rolled, empty for a membership change.</param>
public sealed record StreamEntry(
    StreamStamp Stamp,
    StreamEventKind Kind,
    PeerCode Peer,
    string Text);
