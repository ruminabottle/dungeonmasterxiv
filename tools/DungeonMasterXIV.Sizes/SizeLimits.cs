namespace DungeonMasterXIV.Sizes;

/// <summary>
/// One row of the size table: where it flags, and where it blocks.
/// </summary>
/// <remarks>
/// <b>THIS EXISTS BECAUSE THE TOOL CAUGHT ITS OWN NEW CODE.</b> The first version of
/// <see cref="Coverage.Describe"/> took the ten numbers as ten parameters — <b>over the parameter
/// block, in the same commit that taught the tool to measure that row</b>. The first sweep of the
/// codebase reported it alongside four production breaches.
/// <para>
/// Reported rather than quietly rewritten would have been the wrong answer here: the Deployment
/// Manager's instruction is not to change production code to satisfy a newly-measured row, and this
/// is not production code — it is the row's own instrument, added by this change, and shipping a
/// measuring tool that fails its own new measurement is not a finding, it is a defect.
/// </para>
/// </remarks>
/// <param name="Flag">Where the standards raise it for discussion.</param>
/// <param name="Block">Where the standards make it a denial on its own.</param>
public readonly record struct SizeLimits(int Flag, int Block);
