using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The host's place-and-time for one stream entry: its order and its clock, inseparably (R-2.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>ONE PAIR, NOT TWO FIELDS, AND THAT IS THE REQUIREMENT RATHER THAN TIDINESS.</b> R-2.4 says
/// <i>one order, one clock, identical logs</i>. A type carrying the sequence and letting the time
/// arrive separately would make it constructable to have the host's order beside somebody else's
/// clock — and that combination is exactly what A-2.5 fails a build for. They travel together or the
/// divergence becomes expressible.
/// </para>
/// <para>
/// <b>Ticks rather than <see cref="DateTimeOffset"/>, matching <c>SessionContent.ClosingAtUtcTicks</c>.</b>
/// The wire already carries UTC ticks for the closing notice; a second representation for the same
/// kind of value is a second thing that can disagree.
/// </para>
/// <para>
/// <b>This type does not validate the time and deliberately so.</b> It is not this type's business
/// whether the host's clock is sensible — it is this type's business that the value came from the
/// host. Range-checking here would invite the reading that a locally-sourced-but-plausible stamp is
/// acceptable, which is the opposite of the rule.
/// </para>
/// </remarks>
/// <param name="Sequence">The host's ordinal. Strictly increasing within one session.</param>
/// <param name="AtUtcTicks">The host's clock at the moment it sequenced this entry.</param>
public readonly record struct StreamStamp(long Sequence, long AtUtcTicks);
