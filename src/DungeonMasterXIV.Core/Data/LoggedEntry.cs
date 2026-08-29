namespace DungeonMasterXIV.Data;

/// <summary>
/// One line of a retained or exported log, in the form it is written to disk.
/// </summary>
/// <param name="Stamp">The host's order and instant for this line, copied, not re-derived.</param>
/// <param name="Kind">What happened, as text. See the remarks: this is deliberately not the enum.</param>
/// <param name="Peer">Whose event it was, by peer code.</param>
/// <param name="Text">What was said or rolled. Empty for a membership change.</param>
/// <remarks>
/// <para>
/// <b>THIS IS A PROJECTION, NOT THE STREAM'S OWN TYPE, AND THE REASON IS MEASURED.</b> An exporter
/// must read the stream, and <c>StreamEventKind</c> is <b>six members on main and seven on an open
/// PR</b> — #210 adds <c>Gap</c>. A retained log outlives the release that wrote it, so binding this
/// file's format to a live enum means a log written today is read by a different set tomorrow.
/// </para>
/// <para>
/// <b>Kind is a string here for that reason and not from laziness.</b> A log is an archival record;
/// its vocabulary may only grow, and an unknown kind read back from an old file must remain
/// readable rather than becoming an unmapped enum value. The enum is the live model; this is the
/// written one, and they are allowed to drift.
/// </para>
/// <para>
/// <b>No display name appears here, ever</b> (A-2.31, D-8). The peer code is the identifier the
/// session already uses, and a name written into an export would be a portable identifier leaving
/// the campaign that holds it.
/// </para>
/// </remarks>
public readonly record struct LoggedEntry(LoggedStamp Stamp, string Kind, string Peer, string Text);

/// <summary>The host's sequence number and instant for one line, as written to disk.</summary>
/// <param name="Sequence">The host's order. One order for every client (R-2.4).</param>
/// <param name="AtUtcTicks">The host's instant. <b>No client's local clock ever reaches a log.</b></param>
/// <remarks>
/// <b>Written as two numbers rather than a rendered string</b>, because a log is read back by
/// machines as well as people and a formatted date is a lossy, locale-dependent record of an
/// instant. And <b>copied from the stream rather than re-derived</b> — A-2.5 fails a build in which
/// any client's local clock reaches the log, and re-stamping at write time would be exactly that.
/// </remarks>
public readonly record struct LoggedStamp(long Sequence, long AtUtcTicks);
