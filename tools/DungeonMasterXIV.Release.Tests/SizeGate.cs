using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Sizes;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>One unit measured over one row, and the capacity it is being held to.</summary>
/// <param name="File">Repository-relative path, so a breach names where it lives.</param>
/// <param name="Row">Which of the five rows: file, class, method, parameters, nesting.</param>
/// <param name="Unit">The type or member name. Empty for the file row.</param>
/// <param name="Value">What was measured.</param>
/// <param name="Capacity">The block figure it may reach and may not exceed.</param>
internal sealed record Breach(string File, string Row, string Unit, int Value, int Capacity)
{
    /// <summary>What remains. Negative is the breach.</summary>
    public int Margin => Capacity - Value;

    /// <summary>Identity for delta comparison — a breach is "the same one" by file, row and unit.</summary>
    public string Key => $"{File}|{Row}|{Unit}";

    public override string ToString() => $"{File}  {Row} {Unit} = {Value} (capacity {Capacity}, margin {Margin})";
}

/// <summary>
/// The size gate: which measurements of a tree are refusals, and why.
/// </summary>
/// <remarks>
/// <para>
/// <b>PURE OVER MEASUREMENTS, AND THAT IS WHAT MAKES IT TESTABLE AT ALL.</b> Every one of this
/// repository's seven block breaches is a method <i>length</i> breach — zero class, zero file, zero
/// parameter, zero nesting. So a gate exercised only against real code has <b>four arms that no input
/// can fire</b>, and a version of it that silently ignored those rows would be green on main, green on
/// every branch, and green in any test written against this tree. Taking measurements as arguments
/// lets the tests construct the cases the repository cannot supply.
/// </para>
/// <para>
/// <b>The rows are split ABSOLUTE and DELTA, and the split is ruled rather than chosen here
/// (DMXENG-70).</b> <c>main</c> carries zero class and zero file breaches, so absolute costs nothing
/// at those scopes and is strictly stronger — a delta would pass a class breach that arrived by a
/// route nobody anticipated. At method scope absolute is unaffordable: seven breaches exist, one of
/// them <c>Drain</c> at −120 with a bug-lane ticket held on it. So method rows fail on a NEW breach or
/// on an existing one getting WORSE, and the seven pass at their recorded margins.
/// </para>
/// </remarks>
/// <summary>
/// What one file yielded: the breaches found, and anything the readers could not measure.
/// </summary>
/// <remarks>
/// <b>REFUSALS ARE CARRIED, NOT FILTERED, AND THIS TYPE EXISTS BECAUSE THE FIRST DRAFT FILTERED
/// THEM.</b> A span the reader refuses has not been measured, so it has not been found compliant —
/// and dropping it leaves a gate that reports no breaches for a file it never read. That is
/// <i>could-not-evaluate</i> collapsing into <i>pass</i>, which is BUG-121's shape one layer down:
/// <c>dotnet test</c> printing "Passed!" with a truncated total when the host aborts. Three outcomes,
/// and the third must not wear the first one's face.
/// </remarks>
internal sealed record Measured(IReadOnlyList<Breach> Breaches, IReadOnlyList<string> Unmeasured);

/// <summary>One capacity per row — the block set or the flag set.</summary>
/// <remarks>
/// <b>Init-only rather than positional, and the reason is this ticket's own subject.</b> Five
/// positional parameters would sit at the PARAMETER FLAG of 4 that DMXENG-107 teaches the gate to
/// report. A type introduced to measure the flag row should not cross it.
/// </remarks>
internal sealed record Thresholds
{
    /// <summary>Class span.</summary>
    internal required int Class { get; init; }

    /// <summary>File length.</summary>
    internal required int File { get; init; }

    /// <summary>Method length.</summary>
    internal required int Method { get; init; }

    /// <summary>Parameter count.</summary>
    internal required int Parameter { get; init; }

    /// <summary>Nesting depth.</summary>
    internal required int Nesting { get; init; }
}

internal static class SizeGate
{
    // THE LIMITS ARE DUPLICATED FROM Program.cs AND THAT DUPLICATION IS POLICED, NOT TOLERATED.
    // They are top-level `const`s in a Program.cs that compiles to an entry point, so nothing outside
    // that file can reference them, and the ticket's boundary is to use the sizes tool AS-IS rather
    // than restructure it. TheGateHoldsTheSameLimitTheToolDoes reads Program.cs and fails if these
    // ever disagree, which turns a silent drift into a red test.
    internal const int ClassBlock = 400;
    internal const int FileBlock = 450;
    internal const int MethodBlock = 60;
    internal const int ParameterBlock = 6;
    internal const int NestingBlock = 4;

    // THE FLAG ROW, ADDED BY DMXENG-107, AND IT IS NOT A SECOND SET OF BLOCKS.
    // engineering-standards.md:1140 -- "Blocking limits are a denial on their own. FLAGS ARE A
    // CONVERSATION" -- and the DM has confirmed that sentence governs the GATE as well as the
    // standard. A gate that refused here would not be stricter; it would implement a different rule.
    // Nothing in Refusals reads these, and TheFlagReportCannotMakeTheGateRefuse pins that.
    //
    // Duplicated from Program.cs for the same reason the blocks are -- top-level consts in an
    // entry-point file cannot be referenced -- and policed by the same guard.
    internal const int ClassFlag = 250;
    internal const int FileFlag = 300;
    internal const int MethodFlag = 40;
    internal const int ParameterFlag = 4;
    internal const int NestingFlag = 3;

    internal const string FileRow = "file";
    internal const string ClassRow = "class";
    internal const string MethodRow = "method";
    internal const string ParameterRow = "parameters";
    internal const string NestingRow = "nesting";

    /// <summary>Rows held to an ABSOLUTE standard: any breach fails, no grandfathering.</summary>
    internal static readonly string[] AbsoluteRows = [FileRow, ClassRow];

    /// <summary>Every block breach in one file's source.</summary>
    /// <remarks>
    /// Structured values from <see cref="MemberReader"/> and <see cref="ClassSpanReader"/> rather
    /// than the tool's printed prose. The prose cannot be matched safely anyway: <c>Program.cs</c>
    /// emits a BARE "OVER THE BLOCK" as the true arm of three separate ternaries — method length,
    /// parameters and nesting — so a grep for that string cannot tell which row it came from.
    /// </remarks>
    internal static Measured BreachesIn(string path, string source) =>
        MeasureAgainst(path, source, Blocks);

    /// <summary>
    /// Every FLAG crossing in one file's source. <b>Not refusals — see <see cref="ClassFlag"/>.</b>
    /// </summary>
    /// <remarks>
    /// Same measurement, different capacities. <b>The measurement is shared rather than copied</b>,
    /// because two readers of the same source that could disagree are the drift this file already
    /// polices between the gate and the tool — rebuilding it one scope down would be the same defect
    /// with a shorter travel time.
    /// </remarks>
    internal static Measured FlagCrossingsIn(string path, string source) =>
        MeasureAgainst(path, source, Flags);

    /// <summary>The five block capacities.</summary>
    internal static readonly Thresholds Blocks = new()
    {
        Class = ClassBlock,
        File = FileBlock,
        Method = MethodBlock,
        Parameter = ParameterBlock,
        Nesting = NestingBlock,
    };

    /// <summary>The five flag capacities.</summary>
    internal static readonly Thresholds Flags = new()
    {
        Class = ClassFlag,
        File = FileFlag,
        Method = MethodFlag,
        Parameter = ParameterFlag,
        Nesting = NestingFlag,
    };

    private static Measured MeasureAgainst(string path, string source, Thresholds capacities)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(capacities);

        var found = new List<Breach>();
        var unmeasured = new List<string>();
        var lines = source.Split('\n');

        if (lines.Length > capacities.File)
        {
            found.Add(new Breach(path, FileRow, string.Empty, lines.Length, capacities.File));
        }

        foreach (var type in ClassSpanReader.Read(lines))
        {
            if (type.Refusal is not null)
            {
                unmeasured.Add($"{path}: type {type.Name} REFUSED — {type.Refusal}");
                continue;
            }

            var span = type.ClosingBraceLine - type.DeclarationLine + 1;
            if (span > capacities.Class)
            {
                found.Add(new Breach(path, ClassRow, type.Name, span, capacities.Class));
            }
        }

        foreach (var member in MemberReader.Read(source))
        {
            if (!member.IsMeasured)
            {
                unmeasured.Add($"{path}: member {member.Name} REFUSED — {member.Refusal}");
                continue;
            }

            if (member.Lines > capacities.Method)
            {
                found.Add(new Breach(path, MethodRow, member.Name, member.Lines, capacities.Method));
            }

            if (member.Parameters > capacities.Parameter)
            {
                found.Add(new Breach(path, ParameterRow, member.Name, member.Parameters, capacities.Parameter));
            }

            if (member.Depth > capacities.Nesting)
            {
                found.Add(new Breach(path, NestingRow, member.Name, member.Depth, capacities.Nesting));
            }
        }

        return new Measured(found, unmeasured);
    }

    /// <summary>Why the gate refuses, or empty if it does not.</summary>
    /// <param name="baseline">The breaches recorded as grandfathered, with their margins.</param>
    /// <param name="current">What the tree measures now.</param>
    /// <param name="baselineIntake">The files the baseline was measured over.</param>
    /// <param name="currentIntake">The files measured now.</param>
    internal static IReadOnlyList<string> Refusals(
        IReadOnlyList<Breach> baseline,
        IReadOnlyList<Breach> current,
        IReadOnlyList<string> baselineIntake,
        IReadOnlyList<string> currentIntake)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        var refusals = new List<string>();
        var recorded = baseline.ToDictionary(breach => breach.Key, breach => breach);

        // INTAKE FIRST, because every other arm below is a delta and a delta over a file that left
        // measurement reads as ZERO -- indistinguishable from compliant. This is the one failure a
        // delta gate cannot see from its own results, so it is checked before them.
        foreach (var missing in baselineIntake.Except(currentIntake, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            refusals.Add($"INTAKE: {missing} was measured before and is not measured now. A file "
                + "outside intake has no baseline and therefore no delta, so a regression in it "
                + "would read as zero. Restore it to intake or record its removal in the baseline.");
        }

        foreach (var breach in current.Order(Comparer<Breach>.Create((a, b) => string.CompareOrdinal(a.Key, b.Key))))
        {
            if (AbsoluteRows.Contains(breach.Row, StringComparer.Ordinal))
            {
                refusals.Add($"{breach.Row.ToUpperInvariant()} BLOCK: {breach}. This row is absolute — "
                    + "main carries no breach of it, so there is nothing to grandfather.");
                continue;
            }

            if (!recorded.TryGetValue(breach.Key, out var was))
            {
                refusals.Add($"NEW {breach.Row.ToUpperInvariant()} BREACH: {breach}.");
                continue;
            }

            if (breach.Margin < was.Margin)
            {
                refusals.Add($"WORSENED: {breach}. Was margin {was.Margin}, now {breach.Margin}. "
                    + "Grandfathered breaches may stay where they are; they may not grow.");
            }
        }

        return refusals;
    }
}
