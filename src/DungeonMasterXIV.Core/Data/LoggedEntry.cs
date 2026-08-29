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
/// <b>No display name appears here, ever.</b> The governing directive is <b>D-8, and it is
/// UNQUALIFIED</b>: <i>"No identifier may be stable across campaigns, derivable from a character
/// name or account, or present in any exported artifact. Local history on the DM's own machine may
/// hold real character names; exports may not."</i> <b>A-1.2a already implements it</b> — <i>"no
/// display name appears in any export"</i>.
/// <para>
/// <b>This citation is corrected, and the correction matters more than the claim.</b> It first read
/// "(A-2.31, D-8)" — but A-2.31's <i>"outside a campaign"</i> qualifier is the one clause that does
/// NOT govern here, and I had written a contested reading as though it were settled. Raising it
/// found a genuine conflict: <b>A-2.24 required the <c>Character (Player)</c> parenthetical IN an
/// export while R-2.7 states that parenthetical IS a display name.</b> The Spec Owner ruled that
/// A-2.24's export clause <b>could not be satisfied by any conforming build, and struck it.</b> The
/// outcome here was right; the reason given for it was half wrong.
/// </para>
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
