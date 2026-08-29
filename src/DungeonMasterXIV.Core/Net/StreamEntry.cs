namespace DungeonMasterXIV.Net;

/// <summary>
/// One line of the session stream: what happened, who it was, and the host's place-and-time for it
/// (R-2.3, R-2.4).
/// </summary>
/// <remarks>
/// <b>THERE IS NO CONSTRUCTOR THAT DOES NOT TAKE A STAMP.</b> An entry without the host's order and
/// clock is not a stream entry, and making one expressible is how a client's own clock would find its
/// way in. A-2.5 fails a build for exactly that, so the type refuses to represent it.
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
public readonly record struct StreamEntry(
    StreamStamp Stamp,
    StreamEventKind Kind,
    PeerCode Peer,
    string Text);
