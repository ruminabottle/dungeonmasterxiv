namespace DungeonMasterXIV.Sizes;

/// <summary>
/// The line that says how much of what was looked at actually got a number.
/// </summary>
/// <remarks>
/// <para>
/// <b>A refusal is safe about the NUMBER and unsafe about the CENSUS.</b> It never lies about what
/// it measured; it lies by omission about what it LOOKED AT. A list of results reads as a clean
/// sweep to anyone not counting the lines twice, and over eighty files nobody counts twice — which
/// is how a tool reporting "68 measured" was taken for a complete survey while 34 types were
/// invisible to it.
/// </para>
/// <para>
/// <b>Extracted from the program so it can be tested.</b> The Deployment Manager made this an
/// obligation on any refusing tool, and an obligation defended by a comment is a comment.
/// </para>
/// </remarks>
public static class Census
{
    /// <summary>Describes the coverage of one run.</summary>
    /// <remarks>
    /// <b>It renders on EVERY run, including when nothing was refused.</b> If the line only appeared
    /// when something was refused, its ABSENCE would be ambiguous between "nothing was refused" and
    /// "this build does not report it" — the same reassuring-direction failure the line exists to
    /// close.
    /// </remarks>
    public static string Describe(int measured, int refused, int files)
    {
        var types = measured + refused;
        var scope = $"{types} type(s) across {files} file(s)";

        return refused == 0
            ? $"{scope}: {measured} measured, 0 refused."
            : $"{scope}: {measured} measured, {refused} NOT MEASURED — the counts above cover "
              + $"{measured} of {types}.";
    }
}
