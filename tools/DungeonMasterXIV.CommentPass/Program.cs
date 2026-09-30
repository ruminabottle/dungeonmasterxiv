using DungeonMasterXIV.CommentPass;

var verb = args.FirstOrDefault();
var rest = args.Skip(1).ToList();
return verb switch
{
    "scan" => Scan(rest),
    "strip" => Strip(rest),
    "verify" when rest.Count > 0 => Verify(rest[0], rest.Skip(1).ToList()),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: scan [--review] [pathspec...] | strip [pathspec...] | verify <base-ref> [pathspec...]");
    return 2;
}

static bool Excluded(string path) =>
    path.StartsWith("docs/", StringComparison.Ordinal)
    || path.StartsWith("tools/DungeonMasterXIV.CommentPass", StringComparison.Ordinal);

static IReadOnlyList<string> Files(List<string> pathspecs) =>
    [.. Git.Tracked(pathspecs.Count == 0 ? ["."] : pathspecs).Where(path => !Excluded(path))];

static string Read(string path) => File.ReadAllText(Path.Combine(Git.Root, path));

static int Scan(List<string> rest)
{
    var review = rest.Remove("--review");
    var set = review ? DeadPatterns.Review : DeadPatterns.Guard;
    var (count, files) = (0, 0);
    foreach (var path in Files(rest))
    {
        var hits = DeadPatterns.Find(Read(path), set);
        files += hits.Count > 0 ? 1 : 0;
        count += hits.Count;
        foreach (var (line, match) in hits)
        {
            Console.WriteLine($"{path}:{line}: {match}");
        }
    }

    Console.WriteLine($"{count} {(review ? "review hits" : "dead references")} in {files} files");
    return review || count == 0 ? 0 : 1;
}

static int Strip(List<string> pathspecs)
{
    var changed = 0;
    foreach (var path in Files(pathspecs))
    {
        var text = Read(path);
        var stripped = TagStripper.Strip(text);
        if (stripped != text)
        {
            File.WriteAllText(Path.Combine(Git.Root, path), stripped);
            changed++;
        }
    }

    Console.WriteLine($"stripped tags in {changed} files");
    return 0;
}

static int Verify(string baseRef, List<string> pathspecs)
{
    var (errors, literals) = (0, 0);
    foreach (var (status, path) in Git.Changed(baseRef, pathspecs.Count == 0 ? ["."] : pathspecs).Where(c => !Excluded(c.Path)))
    {
        var problem = status != 'M' ? $"status {status}: files may only be modified" : Compare(path, baseRef, ref literals);
        if (problem is not null)
        {
            Console.WriteLine($"CODE CHANGED {path}: {problem}");
            errors++;
        }
    }

    Console.WriteLine($"{literals} literal changes, {errors} code changes");
    return errors == 0 ? 0 : 1;
}

static string? Compare(string path, string baseRef, ref int literals)
{
    var (before, after) = (Git.Show(baseRef, path), Read(path));
    if (!path.EndsWith(".cs", StringComparison.Ordinal))
    {
        return NonCodeComparison.Difference(path, before, after);
    }

    var result = CodeComparison.Compare(before, after);
    var product = !path.StartsWith("tests/", StringComparison.Ordinal) && !path.StartsWith("tools/", StringComparison.Ordinal);
    foreach (var change in result.Literals)
    {
        Console.WriteLine($"LITERAL {path}:{change.Line}{(product ? " [product]" : "")}\n  - {change.Before}\n  + {change.After}");
        literals++;
    }

    return result.CodeDifference;
}
