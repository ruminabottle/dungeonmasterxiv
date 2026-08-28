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

Console.WriteLine($"Class limits: flag {ClassFlag}, block {ClassBlock}.   File limits: flag {FileFlag}, block {FileBlock}.");
Console.WriteLine("Type span: first line of the declaration to its closing brace, inclusive, nothing excluded.");
Console.WriteLine("File: every line, first to last. Records, structs, interfaces and enums count as classes.");
Console.WriteLine("Ruled by the Deployment Manager; see engineering-standards.md, \"HOW TO COUNT A CLASS\"");
Console.WriteLine("and \"THE SHAPES A REAL FILE HAS\". This tool cites that ruling; it does not make one.");
Console.WriteLine();

var measured = 0;
var refused = 0;

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

// Always 0 on a successful measurement, even for a breach. Whether a breach fails a build is a
// policy question the standards do not answer -- they say a blocking limit is "a denial on its
// own", which is about review. Returning non-zero here would answer it by implementation, which is
// the exact move that made writing this tool unsafe before the convention was written down.
return 0;
