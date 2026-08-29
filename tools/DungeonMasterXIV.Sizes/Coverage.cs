namespace DungeonMasterXIV.Sizes;

/// <summary>
/// What this run covers, and what it does not, printed before any number.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS IS THE ACTUAL FIX FOR DMXENG-55 AND NOT A NICETY.</b> The tool never lied: it measured
/// two of the five rows in the size table and its banner named exactly those two. What failed was
/// the practice that grew around it — every size conversation ran it and read a clean result as
/// "the size limits are met". <b>An instrument that covers part of a rule quietly redefines the
/// rule as the part it covers</b>, and that is invisible precisely because the covered part keeps
/// coming back clean.
/// </para>
/// <para>
/// <b>What it cost:</b> the only parameter-block breach in production code — a seven-parameter
/// constructor against a block of six — merged through the one review conducted with unusual
/// attention to size, because it sat in a row the instrument could not see.
/// </para>
/// <para>
/// <b>So coverage is stated per run, positively AND negatively.</b> Listing only what is measured
/// leaves the reader to notice an absence, which is the thing nobody did for months. Naming the
/// unmeasured rows explicitly means a future gap has to be read past rather than merely overlooked.
/// </para>
/// <para>
/// <b>It renders on EVERY run</b>, for the reason <see cref="Census"/> gives: a line that appears
/// only sometimes has an ambiguous absence.
/// </para>
/// </remarks>
public static class Coverage
{
    /// <summary>The five rows, each marked measured or not, with the limits this build applies.</summary>
    /// <param name="file">The file row.</param>
    /// <param name="type">The class row — records, structs, interfaces and enums included.</param>
    /// <param name="method">The method row.</param>
    /// <param name="parameters">The parameter row.</param>
    /// <param name="nesting">The nesting-depth row.</param>
    /// <remarks>
    /// <b>Five parameters rather than ten, and the tool found that itself.</b> The first version
    /// took each flag and block separately and came out at ten against a block of six — reported by
    /// this very run, in the commit that added the row. See <see cref="SizeLimits"/>.
    /// </remarks>
    public static string Describe(
        SizeLimits file,
        SizeLimits type,
        SizeLimits method,
        SizeLimits parameters,
        SizeLimits nesting) =>
        $"""
        COVERAGE OF THIS RUN — all five rows of the size table, and which ones carry a number.

          Unit            Flag   Block   Measured here
          File            {file.Flag,4}   {file.Block,5}   yes
          Class           {type.Flag,4}   {type.Block,5}   yes — records, structs, interfaces and enums count as classes
          Method          {method.Flag,4}   {method.Block,5}   yes — methods, constructors, operators, finalizers
          Parameters      {parameters.Flag,4}   {parameters.Block,5}   yes — including primary constructors and delegates
          Nesting depth   {nesting.Flag,4}   {nesting.Block,5}   yes — control flow only; a lambda resets the baseline

        NOT MEASURED, AND SAYING SO IS THE POINT: property, indexer and event accessors are outside
        the method and nesting rows, because whether an accessor body is a "method" is unruled. A
        local function is refused BY NAME for the same reason rather than folded into its container.

        Class and file spans come from ClassSpanReader; the other three are parsed. The two original
        rows were deliberately not moved onto the parser — they are ruled, tested and already quoted,
        and re-deriving them would move numbers nobody asked to have moved.

        Ruled by the Deployment Manager; see engineering-standards.md "## Size limits", "HOW TO COUNT
        A CLASS" and "THE SHAPES A REAL FILE HAS". This tool cites those rulings; it does not make one.
        """;
}
