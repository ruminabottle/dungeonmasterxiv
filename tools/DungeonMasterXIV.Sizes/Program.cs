using DungeonMasterXIV.Sizes;

// Reports class spans under the ruled procedure. Reports; does not enforce.
//
//     dotnet run --project tools/DungeonMasterXIV.Sizes -- <path> [<path> ...]
//
// Measure from the ref, not the working tree, when the number will be quoted to anyone -- a working
// tree sits behind and files grow, so a stale read always reports MORE headroom than exists:
//
//     git show origin/main:<path> > /tmp/x.cs && dotnet run --project tools/DungeonMasterXIV.Sizes -- /tmp/x.cs

const int ClassFlag = 250;
const int ClassBlock = 400;
const int FileFlag = 300;
const int FileBlock = 450;
// DMXENG-55: the three rows of the table that nothing measured. The values are the table's, and
// they live beside the other four so a reader can see at a glance that there are now FIVE rows.
const int MethodFlag = 40;
const int MethodBlock = 60;
const int ParameterFlag = 4;
const int ParameterBlock = 6;
const int NestingFlag = 3;
const int NestingBlock = 4;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: DungeonMasterXIV.Sizes <file.cs> [<file.cs> ...]");
    return 2;
}

var missing = args.Where(path => !File.Exists(path)).ToList();

if (missing.Count > 0)
{
    // Named rather than skipped: a run that silently measured four of five files and printed a
    // clean list would read as "nothing is over" when the fifth was never opened.
    Console.Error.WriteLine("no such file: " + string.Join(", ", missing));
    return 2;
}

// COVERAGE FIRST, BEFORE ANY NUMBER. This tool measured two of five rows and its banner named
// exactly those two -- it never lied, and we read its silence as breadth anyway. A banner that
// states all five and marks each is the thing that stops a clean run being heard as a clean rule.
Console.WriteLine(Coverage.Describe(
    new SizeLimits(FileFlag, FileBlock),
    new SizeLimits(ClassFlag, ClassBlock),
    new SizeLimits(MethodFlag, MethodBlock),
    new SizeLimits(ParameterFlag, ParameterBlock),
    new SizeLimits(NestingFlag, NestingBlock)));
Console.WriteLine();
Console.WriteLine("Type span: first line of the declaration to its closing brace, inclusive, nothing excluded.");
Console.WriteLine("Member span: the same procedure applied to a member. File: every line, first to last.");
Console.WriteLine();

var measured = 0;
var refused = 0;
// SEPARATE COUNTERS FOR MEMBERS. Folding them into the type census made a run over two files
// report "22 type(s)" for two types and twenty members -- a census that quietly changed its own
// population, which is this tool's own failure mode committed inside the tool.
var membersMeasured = 0;
var membersRefused = 0;

foreach (var path in args)
{
    var lines = File.ReadAllLines(path);

    // RULED: a file is every line in it, first to last, including a licence header.
    var fileStanding = lines.Length > FileBlock ? "OVER THE BLOCK"
        : lines.Length > FileFlag ? "over the flag"
        : "under the flag";

    Console.WriteLine($"{path}  —  {lines.Length} lines, {fileStanding}, margin {FileBlock - lines.Length}");

    var spans = ClassSpanReader.Read(lines);

    if (spans.Count == 0)
    {
        Console.WriteLine("  no type declaration found");
        continue;
    }

    foreach (var span in spans)
    {
        if (!span.IsMeasured)
        {
            refused++;
            Console.WriteLine($"  {span.Name,-34} NOT MEASURED — {span.Refusal}");
            continue;
        }

        measured++;

        var margin = ClassBlock - span.Lines;
        var standing = span.Lines > ClassBlock ? "OVER THE BLOCK"
            : span.Lines > ClassFlag ? "over the flag"
            : "under the flag";

        Console.WriteLine(
            $"  {span.Name,-34} {span.Lines,4} lines  ({span.DeclarationLine}-{span.ClosingBraceLine})"
            + $"  {standing}, margin {margin}");
    }

    foreach (var member in MemberReader.Read(File.ReadAllText(path)))
    {
        if (!member.IsMeasured)
        {
            membersRefused++;
            Console.WriteLine($"  {member.Name,-34} NOT MEASURED — {member.Refusal}");
            continue;
        }

        membersMeasured++;

        // ONE LINE PER MEMBER, AND ONLY WHEN A ROW HAS SOMETHING TO SAY. Printing every member of
        // every file would bury the class and file lines that callers came for, and a reader who
        // scrolls past a breach has not been told about it. Silence here means every row is under
        // its flag -- which the coverage banner above has already said is a statement about five
        // rows rather than about the two this tool used to measure.
        var notes = new List<string>();

        if (member.Lines > MethodFlag)
        {
            notes.Add($"{member.Lines} lines "
                + (member.Lines > MethodBlock ? "OVER THE BLOCK" : "over the flag")
                + $", margin {MethodBlock - member.Lines}");
        }

        if (member.Parameters > ParameterFlag)
        {
            notes.Add($"{member.Parameters} parameters "
                + (member.Parameters > ParameterBlock ? "OVER THE BLOCK" : "over the flag")
                + $", margin {ParameterBlock - member.Parameters}");
        }

        if (member.Depth > NestingFlag)
        {
            notes.Add($"nesting {member.Depth} "
                + (member.Depth > NestingBlock ? "OVER THE BLOCK" : "over the flag")
                + $", margin {NestingBlock - member.Depth}");
        }

        if (notes.Count > 0)
        {
            Console.WriteLine($"  {member.Name,-34} line {member.Line,4}  {string.Join("; ", notes)}");
        }
    }

    Console.WriteLine();
}

// THE CENSUS, and it prints on EVERY run including when nothing was refused.
//
// A refusal is safe about the NUMBER and unsafe about the CENSUS: it never lies about what it
// measured, it lies by omission about what it LOOKED AT. A list of results reads as a clean sweep
// to anyone not counting the lines twice, and over eighty files nobody counts twice.
//
// Printed even when refused == 0 on purpose. If it only appeared when something was refused, its
// ABSENCE would be ambiguous between "nothing was refused" and "this build does not report it" --
// which is the same reassuring-direction failure the line exists to close.
Console.WriteLine(Census.Describe(measured, refused, args.Length));
Console.WriteLine(Census.DescribeMembers(membersMeasured, membersRefused));

// Always 0 on a successful measurement, even for a breach. Whether a breach fails a build is a
// policy question the standards do not answer -- they say a blocking limit is "a denial on its
// own", which is about review. Returning non-zero here would answer it by implementation, which is
// the exact move that made writing this tool unsafe before the convention was written down.
return 0;
