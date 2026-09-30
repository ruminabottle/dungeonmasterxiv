# Fresh Summaries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove every comment from product C#, then give each type one fresh `/// <summary>` line written from the stripped code alone, and cut build and config file comments to one line per block. Compiled output does not change.

**Architecture:** Two throwaway tools live outside the repository:
- `comments.cs`, a .NET 10 file-based Roslyn app, strips comments (`strip`) and audits the result (`census`).
- `dll-hashes.sh` builds the four product assemblies deterministically without PDBs and prints each one's SHA-256.

The strip lands first, in its own commit, with its hashes equal to the base. Summaries are then written folder by folder over the already-stripped tree, so no writer ever sees old comment text.

**Tech Stack:** .NET SDK 10.0.400 (file-based apps with `#:package`), Microsoft.CodeAnalysis.CSharp 4.11.0, bash, `shasum`, git, `gh`.

**Spec:** `docs/superpowers/specs/2026-09-30-fresh-summaries-design.md`

## Global Constraints

- **Branch** `chore/fresh-summaries`, which already holds the spec. **Base** for every comparison: `dfc4b25`.
- **Tool directory**, outside the repo and never committed: `T=/private/tmp/claude-501/-Users-ramonmunoz-repositories-dungeonmasterxiv-1/d2bb6708-1b6f-4b17-a3e8-a1211dc995da/scratchpad/fresh-summaries`. Define `T` at the start of every task, and keep outputs (hash files, logs) there too.
- **Product C# file list**, the same everywhere:

  ```bash
  PRODUCT=$(git ls-files 'src/*.cs' 'Windows/*.cs' Plugin.cs 'Services/*.cs' 'Net/*.cs' 'Data/*.cs' 'tools/DungeonMasterXIV.Release/*.cs')
  ```

- **Compiled output is identical.** `$T/dll-hashes.sh` output must equal `$T/hashes-base.txt` after every commit.
- **No new build warnings.** `dotnet build DungeonMasterXIV.sln -nologo` shows 0 warnings, as the base does. A summary with a raw `<` or `&` raises CS1570, so a summary uses words, or `<c>…</c>`, instead.
- **Tests stay green.** `dotnet test DungeonMasterXIV.sln` reports Total 5 / 1 / 1 with Failed 0 and Skipped 0. Read the Total and Skipped fields, never the "Passed!" word.
- **The summary rule** (spec §2):
  - One line directly above each type declaration, and above its attributes if it has any, at the declaration's indentation: `/// <summary>…</summary>`.
  - Present tense, one sentence, at most about 100 characters of text, saying what the type is for.
  - Written from the stripped code and its uses only. Never run `git show`, `git diff` or `git log -p` against pre-strip commits, and never read the archive.
  - No spec or ticket IDs, no history ("was", "used to", "since"), no claims about tests, reviewers or roles.
  - If the purpose isn't clear, say what the type does.
- **Commit messages** go through `git commit -F -` with a quoted heredoc, ending with `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`. The PR body ends with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

## Review Focus

1. **A summary that says more than the code.** Guessing a purpose from a type name is how poisoning comes back. The reviewer checks each summary against the type body and its call sites; the census only counts.
2. **The strip damaging a string literal.** Blank-line collapsing inside a multi-line raw or verbatim string would change a literal. The DLL hash comparison catches this; if it ever mismatches, the fix is in the tool, never a hand-edit that hides it.
3. **A comment inside `#if`-disabled text.** Roslyn sees disabled regions as one trivia token, so the strip leaves any comment inside them. The census must report these as `EXTRA`, and they are then removed by hand.
4. **Doc-comment XML that breaks the build.** A summary containing `<`, `>` or `&` raises CS1570. The warnings check after each summary task catches it.
5. **Build-file comment edits that touch an element.** The comment-stripped diff in Task 6 guards `.csproj`, `Dockerfile` and `.dockerignore`.

---

### Task 1: The tools, and the base hashes

**Files:**
- Create (outside the repo): `$T/comments.cs`, `$T/dll-hashes.sh`
- Produce: `$T/hashes-base.txt`

**Interfaces:**
- Produces `dotnet run --file $T/comments.cs -- strip <files…>`: rewrites files in place and prints `stripped N files`.
- Produces `dotnet run --file $T/comments.cs -- census [--strict] <files…>`:
  - prints `MISSING path:line TypeName` for each type without exactly one single-line summary;
  - prints `EXTRA path:line` for every other comment;
  - ends with a totals line `files=… lines=… commentLines=… types=… summaries=… extra=… missing=…`;
  - with `--strict`, exits 1 when `extra` or `missing` is above 0.
- Produces `$T/dll-hashes.sh <out-file>`: writes lines of the form `<AssemblyName> <sha256>`, one per product assembly, and exits 1 if a build fails.

- [ ] **Step 1: Write the tool**

`$T/comments.cs`:

```csharp
#:package Microsoft.CodeAnalysis.CSharp@4.11.0
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var mode = args.Length > 0 ? args[0] : "";
var strict = args.Contains("--strict");
var files = args.Skip(1).Where(a => a != "--strict").ToArray();
var options = new CSharpParseOptions(LanguageVersion.Preview);

if (mode == "strip")
{
    var changed = 0;
    foreach (var path in files)
    {
        var text = File.ReadAllText(path);
        var stripped = Strip(text, CSharpSyntaxTree.ParseText(text, options).GetRoot());
        if (stripped != text)
        {
            File.WriteAllText(path, stripped);
            changed++;
        }
    }

    Console.WriteLine($"stripped {changed} files");
    return 0;
}

if (mode == "census")
{
    var summaryLine = new Regex(@"^\s*/// <summary>.+</summary>\s*$");
    int lines = 0, commentLines = 0, types = 0, summaries = 0, extra = 0, missing = 0;
    foreach (var path in files)
    {
        var text = File.ReadAllText(path);
        var tree = CSharpSyntaxTree.ParseText(text, options);
        var root = tree.GetRoot();
        lines += text.Split('\n').Length;
        var allowed = new HashSet<int>();

        foreach (var type in root.DescendantNodes().Where(n => n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax))
        {
            types++;
            var docs = type.GetLeadingTrivia().Where(IsComment).ToList();
            var ok = docs.Count == 1
                && docs[0].IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && summaryLine.IsMatch(docs[0].ToFullString().TrimEnd('\r', '\n'))
                && !docs[0].ToFullString().TrimEnd('\r', '\n').Contains('\n');
            if (ok)
            {
                summaries++;
                allowed.Add(docs[0].SpanStart);
            }
            else
            {
                missing++;
                Console.WriteLine($"MISSING {path}:{LineOf(tree, type.SpanStart)} {Name(type)}");
            }
        }

        foreach (var trivia in root.DescendantTrivia().Where(IsComment))
        {
            commentLines += trivia.ToFullString().TrimEnd('\r', '\n').Split('\n').Length;
            if (!allowed.Contains(trivia.SpanStart))
            {
                extra++;
                Console.WriteLine($"EXTRA {path}:{LineOf(tree, trivia.SpanStart)}");
            }
        }

        foreach (var disabled in root.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.DisabledTextTrivia)))
        {
            if (disabled.ToFullString().Contains("//") || disabled.ToFullString().Contains("/*"))
            {
                extra++;
                Console.WriteLine($"EXTRA {path}:{LineOf(tree, disabled.SpanStart)} (inside disabled #if text)");
            }
        }
    }

    Console.WriteLine($"files={files.Length} lines={lines} commentLines={commentLines} types={types} summaries={summaries} extra={extra} missing={missing}");
    return strict && (extra > 0 || missing > 0) ? 1 : 0;
}

Console.Error.WriteLine("usage: strip <files…> | census [--strict] <files…>");
return 2;

static bool IsComment(SyntaxTrivia t) =>
    t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
    || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

static int LineOf(SyntaxTree tree, int position) => tree.GetLineSpan(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0)).StartLinePosition.Line + 1;

static string Name(SyntaxNode node) => node switch
{
    BaseTypeDeclarationSyntax t => t.Identifier.Text,
    DelegateDeclarationSyntax d => d.Identifier.Text,
    _ => "?",
};

// Masks every comment character, rebuilds the file line by line, drops lines that held only comment,
// and collapses a blank-line run only where a dropped line sat, so literals with blank lines are untouched.
static string Strip(string text, SyntaxNode root)
{
    var mask = new bool[text.Length];
    foreach (var trivia in root.DescendantTrivia().Where(IsComment))
    {
        for (var i = trivia.FullSpan.Start; i < trivia.FullSpan.End; i++)
        {
            mask[i] = true;
        }
    }

    var output = new StringBuilder();
    var start = 0;
    var droppedSinceEmit = false;
    string? lastEmitted = null;
    while (start < text.Length)
    {
        var newline = text.IndexOf('\n', start);
        var end = newline < 0 ? text.Length : newline + 1;
        var contentEnd = end;
        while (contentEnd > start && (text[contentEnd - 1] == '\n' || text[contentEnd - 1] == '\r'))
        {
            contentEnd--;
        }

        var hadComment = false;
        var kept = new StringBuilder();
        for (var i = start; i < contentEnd; i++)
        {
            if (mask[i]) { hadComment = true; } else { kept.Append(text[i]); }
        }

        var line = hadComment ? kept.ToString().TrimEnd() : text[start..contentEnd];
        var ending = text[contentEnd..end];
        start = end;

        if (hadComment && line.Trim().Length == 0)
        {
            droppedSinceEmit = true;
            continue;
        }

        var blank = line.Trim().Length == 0;
        if (blank && droppedSinceEmit && (lastEmitted is null || lastEmitted.Trim().Length == 0 || lastEmitted.TrimEnd().EndsWith('{')))
        {
            continue;
        }

        output.Append(line).Append(ending);
        lastEmitted = line;
        droppedSinceEmit = false;
    }

    return output.ToString();
}
```

- [ ] **Step 2: Write the hash script**

`$T/dll-hashes.sh`:

```bash
#!/usr/bin/env bash
# Builds each product assembly deterministically with no PDB and prints its SHA-256.
set -uo pipefail
out="$1"
root="$(git rev-parse --show-toplevel)"
tmp="$(mktemp -d)"
: > "$out"
for project in DungeonMasterXIV.csproj src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj \
               src/DungeonMasterXIV.Relay/DungeonMasterXIV.Relay.csproj tools/DungeonMasterXIV.Release/DungeonMasterXIV.Release.csproj; do
  name="$(basename "$project" .csproj)"
  if ! dotnet build "$root/$project" -c Release -nologo -o "$tmp/$name" \
        -p:Deterministic=true -p:DebugType=none -p:ContinuousIntegrationBuild=true \
        -p:IncludeSourceRevisionInInformationalVersion=false > "$tmp/$name.log" 2>&1; then
    tail -20 "$tmp/$name.log"; echo "BUILD FAILED: $project"; exit 1
  fi
  echo "$name $(shasum -a 256 "$tmp/$name/$name.dll" | cut -d' ' -f1)" >> "$out"
done
cat "$out"
```

```bash
chmod +x "$T/dll-hashes.sh"
```

- [ ] **Step 3: Prove the tools on the base tree**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1 && git status --short   # expect clean, on chore/fresh-summaries
"$T/dll-hashes.sh" "$T/hashes-base.txt"
"$T/dll-hashes.sh" "$T/hashes-base-again.txt"
diff "$T/hashes-base.txt" "$T/hashes-base-again.txt" && echo REPRODUCIBLE
dotnet run --file "$T/comments.cs" -- census $PRODUCT | tail -1
```

Expected:
- 4 hash lines, and `REPRODUCIBLE`.
- The census totals line shows `commentLines` of about 12,700 and `types` of about 226.

If the two hash runs differ, the build is not deterministic yet. Find the varying input, such as a timestamp or a commit hash in `InformationalVersion`, and add the property that pins it to `dll-hashes.sh`, before going on.

- [ ] **Step 4: Prove the strip tool on a scratch copy**

```bash
mkdir -p "$T/probe" && cp src/DungeonMasterXIV.Core/Rolls/RollEvaluator.cs "$T/probe/"
dotnet run --file "$T/comments.cs" -- strip "$T/probe/RollEvaluator.cs"
grep -cE '^\s*(//|/\*|\*)' "$T/probe/RollEvaluator.cs"            # expect 0
dotnet run --file "$T/comments.cs" -- census "$T/probe/RollEvaluator.cs" | tail -1   # expect extra=0, missing = its type count
```

Nothing to commit: the tools live outside the repository.

---

### Task 2: Strip every product comment

**Files:** modify every file in `$PRODUCT`.

- [ ] **Step 1: Strip**

```bash
dotnet run --file "$T/comments.cs" -- strip $PRODUCT
dotnet run --file "$T/comments.cs" -- census $PRODUCT | grep -c '^EXTRA'   # expect 0, except comments inside disabled #if text
```

Remove any `EXTRA … (inside disabled #if text)` comment by hand.

- [ ] **Step 2: Compiled output unchanged**

```bash
"$T/dll-hashes.sh" "$T/hashes-strip.txt" && diff "$T/hashes-base.txt" "$T/hashes-strip.txt" && echo IDENTICAL
```

Expected: `IDENTICAL`. If an assembly differs, find the file with `git diff --stat`, spot-check its literals, fix the tool (never the output), `git checkout -- .`, and re-strip.

- [ ] **Step 3: Build, tests, eyeball**

```bash
dotnet build DungeonMasterXIV.sln -nologo 2>&1 | grep -cE 'warning (CS|NU|MSB)'   # expect 0
dotnet test DungeonMasterXIV.sln -nologo 2>&1 | grep -E '^(Passed|Failed)!'       # expect 5 / 1 / 1, Failed 0, Skipped 0
git diff --stat | tail -1
git diff src/DungeonMasterXIV.Core/Rolls/RollEvaluator.cs | head -60             # blank-line layout reads cleanly
```

- [ ] **Step 4: Commit**

```bash
git add -A -- $PRODUCT
git commit -F - <<'EOF'
chore(comments): strip every comment from product code

Compiled output is byte-identical: deterministic, PDB-less builds of the four product assemblies
hash the same before and after.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Tasks 3–5: Write the summaries

Each of the three tasks runs the same steps over its own folder set. The writer's only inputs are the task brief, the Global Constraints and the stripped files in the working tree. **Do not read git history, old commits or the archive.**

| Task | Folder set (`SET`) | Types |
|---|---|---|
| 3 | `src/DungeonMasterXIV.Core/Net` | about 103 |
| 4 | `src/DungeonMasterXIV.Core/Campaigns src/DungeonMasterXIV.Core/Data src/DungeonMasterXIV.Core/Rolls src/DungeonMasterXIV.Core/Chat src/DungeonMasterXIV.Core/Services` | about 79 |
| 5 | `src/DungeonMasterXIV.Relay tools/DungeonMasterXIV.Release Windows Plugin.cs Services Net Data` | about 45 |

- [ ] **Step 1: List the types that need a summary**

```bash
FILES=$(git ls-files $(for d in $SET; do [ -f "$d" ] && echo "$d" || echo "$d/*.cs"; done))
dotnet run --file "$T/comments.cs" -- census $FILES | grep '^MISSING' > "$T/todo.txt"; wc -l < "$T/todo.txt"
```

- [ ] **Step 2: Write one summary per `MISSING` line**

For each type:
1. Read its declaration and body in the stripped file.
2. Read its uses: `git grep -n '\bTypeName\b' -- src Windows Plugin.cs Services Net Data tools/DungeonMasterXIV.Release`. This searches the current tree only.
3. Insert one line directly above the declaration (above any attributes), at its indentation, following the summary rule in Global Constraints.

Good shapes:

```csharp
/// <summary>Evaluates a dice expression such as "4d6+2" into dice and a total, using an injected die roller.</summary>
/// <summary>The side a connection plays in a session: host, joiner, or not yet known.</summary>
/// <summary>Holds the admission requests the host has not yet answered.</summary>
```

Bad shapes: a purpose guessed from the name; "Handles X" when X isn't in the code; any "used to", "now", "so that the tests…"; any ID.

- [ ] **Step 3: Census, build, hashes, tests**

```bash
dotnet run --file "$T/comments.cs" -- census --strict $FILES | tail -1; echo "exit=$?"   # expect extra=0 missing=0
dotnet build DungeonMasterXIV.sln -nologo 2>&1 | grep -cE 'warning (CS|NU|MSB)'        # expect 0
"$T/dll-hashes.sh" "$T/hashes-now.txt" && diff "$T/hashes-base.txt" "$T/hashes-now.txt" && echo IDENTICAL
dotnet test DungeonMasterXIV.sln -nologo 2>&1 | grep -E '^(Passed|Failed)!'            # expect 5 / 1 / 1
```

The `exit=$?` above reports `tail`'s exit code. Read `extra` and `missing` from the totals line.

- [ ] **Step 4: Commit**

Use this message, with `<set name>` replaced by `net`, `core`, or `relay, release tool and plugin`:

```bash
git add -A -- $FILES
git commit -F - <<'EOF'
docs(code): one-line summaries for <set name> types, written from the code

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 6: Build and config file comments to one line per block

**Files:**
- Modify: every `.csproj` (`git ls-files '*.csproj'`), `deploy/Dockerfile` and `.dockerignore`
- Do not touch: the TLS-fence and release-tag comments `#258` already shortened (in `Directory.Build.targets`, and the five release-tag comments in `DungeonMasterXIV.csproj`)

- [ ] **Step 1: Rewrite each comment block as one line saying what the next element or instruction does**
  - A `.csproj` block becomes `<!-- … -->` on one line.
  - A `Dockerfile` or `.dockerignore` run of `#` lines becomes one `#` line.
  - A block that says nothing the element doesn't already say is deleted.
  - Elements, attributes, conditions, `Text=` and instructions stay unchanged.

- [ ] **Step 2: Prove only comments changed**

```bash
for f in $(git ls-files '*.csproj'); do
  diff <(git show dfc4b25:$f | perl -0pe 's/<!--.*?-->//gs' | sed 's/[[:space:]]*$//' | grep -v '^$') \
       <(perl -0pe 's/<!--.*?-->//gs' $f | sed 's/[[:space:]]*$//' | grep -v '^$') > /dev/null && echo "ok $f" || echo "CHANGED $f"
done
for f in deploy/Dockerfile .dockerignore; do
  diff <(git show dfc4b25:$f | grep -v '^[[:space:]]*#' | sed 's/[[:space:]]*$//' | grep -v '^$') \
       <(grep -v '^[[:space:]]*#' $f | sed 's/[[:space:]]*$//' | grep -v '^$') > /dev/null && echo "ok $f" || echo "CHANGED $f"
done
```

Expected: every line reads `ok`.

- [ ] **Step 3: Build, hashes, tests** — use the same commands as Task 3–5 Step 3, minus the census. Expect 0 warnings, `IDENTICAL`, and 5 / 1 / 1.

- [ ] **Step 4: Commit**

```bash
git add -A -- $(git ls-files '*.csproj') deploy/Dockerfile .dockerignore
git commit -F - <<'EOF'
chore(build): one line per comment block in project and container files

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 7: Final checks, spec status, PR

- [ ] **Step 1: Whole-tree census and checks**

```bash
dotnet run --file "$T/comments.cs" -- census --strict $PRODUCT | tail -1    # expect extra=0 missing=0; commentLines ≈ types
dotnet build DungeonMasterXIV.sln -nologo 2>&1 | grep -cE 'warning (CS|NU|MSB)'   # expect 0
"$T/dll-hashes.sh" "$T/hashes-final.txt" && diff "$T/hashes-base.txt" "$T/hashes-final.txt" && echo IDENTICAL
dotnet test DungeonMasterXIV.sln -nologo 2>&1 | grep -E '^(Passed|Failed)!'        # expect 5 / 1 / 1
echo "$PRODUCT" | xargs cat | wc -l                                               # report the new line total
```

Expected: `commentLines` is at most about 2% of the line total.

- [ ] **Step 2: Spec status**

In `docs/superpowers/specs/2026-09-30-fresh-summaries-design.md`, change `**Status:** Approved for planning` to `**Status:** Implemented 2026-09-30`. Commit it (`docs(spec): fresh summaries implemented`).

- [ ] **Step 3: Push and open the PR**

```bash
git push -u origin chore/fresh-summaries
gh pr create --title "chore: replace product comments with fresh one-line summaries" --body-file "$T/pr-body.md"
```

The body covers:
- the spec and plan paths;
- before and after for lines, comment lines and types;
- the four DLL hashes, identical at base and head;
- the census totals line;
- build warnings and test totals;
- any `EXTRA` found inside disabled `#if` text and how it was handled;
- the closing line `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

Stop after the PR is open. A human merges it.
