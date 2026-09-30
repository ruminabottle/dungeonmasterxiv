# Comment ID Rewrite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove every retired ticket ID, agent-role name and archive path from tracked files outside `docs/`, restate history-as-reason as the rule itself, and land a release test that keeps them out.

**Architecture:** A throwaway Roslyn console tool, `tools/DungeonMasterXIV.CommentPass`, provides four things: `scan` (find dead references), `strip` (mechanically delete bare tags), `verify` (prove only comments and string literals changed against a base commit) and `snapshot.sh` (normalized build warnings and test totals). Seven area PRs run the same procedure, and each merges before the next branches. The last PR moves the pattern set into a permanent test in `tools/DungeonMasterXIV.Release.Tests` and deletes the tool.

**Tech Stack:** C# / .NET SDK 10.0.400 (`global.json`), Microsoft.CodeAnalysis.CSharp 4.11.0 (already used by `tools/DungeonMasterXIV.Sizes`), xUnit 2.9.2, bash, git, `gh`.

**Spec:** `docs/superpowers/specs/2026-09-30-comment-id-rewrite-design.md`

## Global Constraints

- Spec IDs `R-n.n`, `A-n.n` and `D-n` stay everywhere. Never delete or renumber one.
- Scope is every tracked file outside `docs/`. Never edit anything under `docs/` except in Task 11.
- Compiled code does not change. Only comments, string/char/interpolated-text literals, XML comments in `.csproj`/`.targets`, `#` lines in `Dockerfile`/`.gitignore`/`.dockerignore`/`size-gate-baseline.txt`, and the header paragraph of `tests/DungeonMasterXIV.Tests/ClockFormsFixture.txt` may differ.
- A rewrite is never longer, in lines, than the text it replaces. The size gate counts lines inside member bodies and whole files: file block 450, class block 400, method block 60, and the flags at 300 / 250 / 40.
- New tool code stays under every size flag: files ≤ 300 lines, methods ≤ 40 lines, ≤ 4 parameters, nesting ≤ 3.
- `tools/DungeonMasterXIV.CommentPass` and `tools/DungeonMasterXIV.CommentPass.Tests` are **not** added to `DungeonMasterXIV.sln` and **not** added to `tools/DungeonMasterXIV.Release.Tests/size-gate-baseline.txt`. The baseline treats files as a floor, so a file listed there could not be deleted later.
- Every branch is created from a freshly fetched `origin/main`. Otherwise the size gate skips (`ContainsMainFact`) and the Release.Tests Skipped count is wrong.
- Compare `dotnet test` on Total, Passed and Skipped per assembly, never on the "Passed!" word.
- Commit messages go through `git commit -F` with a quoted heredoc, because backticks in `-m` strings get executed. End each one with `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`.
- PR bodies end with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- A human merges each PR. Do not start the next PR's task until the previous PR is merged and `main` is pulled.

## Review Focus

1. **A rewrite changes what a comment claims.** A reader expects the rewritten comment to assert exactly what the original asserted, minus the provenance. The per-file review step compares every judgment rewrite against its source record, and the PR body lists each rule recovered from the archive with its source.
2. **A dangling reference after removal.** "As that bug showed", "the ticket above", "this ruling" can be left pointing at nothing. The `scan --review` set includes `(the|that|this) (bug|ticket|ruling|escalation|brief)`, and every hit must be resolved or recorded as kept.
3. **A changed product string.** A literal under `src/` or `Windows/` or in `Plugin.cs` is text a person may see. `verify` marks those `[product]`, the PR body lists them, and `ShippedCopyMeetsItsConstraintsTests` runs in the snapshot.
4. **The mechanical strip damages prose**, leaving a double space, a space before punctuation, an empty `()` or an orphaned comma. The TagStripper tests pin each of these.
5. **A stale claim gets "fixed" in passing.** Some comments are wrong today. For example, `RollEvaluator.cs` says it has no caller, but #247 wired it in. Accuracy is out of scope, so a stale claim keeps its content and is listed in the PR body under "Stale, not fixed".

---

## Area pass procedure

Tasks 2–10 each run this procedure over the paths they name. It lives here once because it is identical for every area. Each task states its branch, pathspecs and PR title.

Define these at the start of the session and reuse them:

```bash
REPO=/Users/ramonmunoz/repositories/dungeonmasterxiv-1
SCRATCH="${SCRATCH:-$(mktemp -d)}"          # a directory outside the repo
CP="dotnet run --project $REPO/tools/DungeonMasterXIV.CommentPass --"
A=~/archive/dungeonmasterxiv-agent-team-2026-09-29/claude/team
```

**P1. Branch from fresh main.**

```bash
cd "$REPO" && git fetch origin && git switch main && git pull --ff-only && git switch -c <branch> origin/main
BASE=$(git rev-parse HEAD)
```

**P2. Take the base snapshot.**

```bash
tools/DungeonMasterXIV.CommentPass/snapshot.sh "$SCRATCH/<branch>-base.txt"
```

Expected: the script exits 0, and the `== tests` block shows three lines, with `DungeonMasterXIV.Release.Tests.dll` at `Skipped: 0` and `DungeonMasterXIV.Relay.Tests.dll` at `Skipped: 1` (the container smoke test). If Release.Tests shows `Skipped: 2`, the branch does not contain `origin/main`. Stop and redo P1.

**P3. Inventory.**

```bash
$CP scan <pathspecs> | tee "$SCRATCH/<branch>-inventory.txt"; echo "exit=${PIPESTATUS[0]}"
$CP scan --review <pathspecs> | tee "$SCRATCH/<branch>-review.txt"
```

Expected: `scan` lists `path:line: match` rows, ends with `N dead references in M files`, and exits 1. Record N and M for the PR body.

**P4. Mechanical pre-pass.**

```bash
$CP strip <pathspecs>
$CP verify "$BASE" <pathspecs>; echo "exit=$?"
git diff --stat
```

Expected: `verify` exits 0. Skim `git diff` for damage: a double space, `( )`, `,)`, or a sentence that no longer reads. Fix any damage by hand and re-run `verify`. Then commit:

```bash
git add -A -- <pathspecs>
git commit -F - <<'EOF'
chore(comments): strip bare retired tags in <area>

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

**P5. Judgment pass.** Work from `$CP scan <pathspecs>`, one file at a time. Re-run `scan` after every few files. For each hit, apply the first rule that fits:

| Found | Rewrite |
|---|---|
| History as the reason: "which is the collision BUG-43 was" | State the failure itself. |
| A ruling with provenance: "The Spec Owner's ruling (SQ-84) is that X", "ruled by the HUMAN" | "X". If an area spec holds the ruling, cite its `R-`/`A-`/`D-` ID. |
| Process narrative: "DMXENG-69 gave the rule an owner", "a bug-lane ticket held on it", "DMXENG-107's brief warned me" | Delete it. Keep any fact inside it. |
| A leading label: "BUG-118: the coordinator SUPPLIES…" | Drop the label: "The coordinator SUPPLIES…". |
| `PRD-n` | The area spec name (PRD-0 core-skeleton, PRD-1 session-layer, PRD-2 rolls, PRD-3 initiative, PRD-7 distribution), or the `R-`/`A-` ID alone. |
| "Decision n" | "product-overview Session panel item n". |
| A `.claude/…` path | Write the fact into the comment, or drop it if the path only pointed at a backlog. |
| "in `engineering-standards.md`", "the brief" | The rule it cited, or its spec ID. |
| A role name (Deployment Manager, Code Reviewer, …) as the author of a finding or ruling | Remove the author and keep the finding. |

To recover the rule behind an ID, read in this order and stop at the first source that settles it:
1. the surrounding code;
2. the area spec (`grep -n '<R-/A- ID>' docs/superpowers/specs/*.md`);
3. `$A/bugs/BUG-n.md`, `grep -rln 'DMXENG-n\b' $A`, `grep -n '^## SQ-n\b' -A20 $A/product/prd/SQ-LEDGER.md`, `$A/tickets/`, `$A/IN-GAME-BACKLOG.md`;
4. the PR body: `gh pr list --state all --search 'DMXENG-n' --json number,title` and then `gh pr view <n>`.

If none of them settles it, keep the claim and drop only the ID. Never guess.

Worked examples, taken from the tree:

- `tests/DungeonMasterXIV.Tests/EveryMessageTypeReachesAnArmTests.cs:231`
  - Before: `// swallow a frame meant for the other -- which is the collision BUG-43 was, and a probe`
  - Source: `$A/bugs/BUG-43.md`, "THE MECHANISM BELOW IS WRONG": the host arm consumed the joiner's envelope and reported it handled.
  - After: `// swallow a frame meant for the other -- the collision where the host arm consumed a joiner's frame and reported it handled -- and a probe`. That runs past the line width, so reflow within the same three comment lines. The line count must not grow.
- `src/DungeonMasterXIV.Core/Rolls/RollEvaluator.cs:12`
  - Before: `The Spec Owner's ruling (SQ-84) is that "base chat first" governs what a USER can do`
  - After: `"Base chat first" governs what a USER can do`. The claim that the evaluator has no caller is stale since #247. Leave it, and list it under "Stale, not fixed".
- `src/DungeonMasterXIV.Core/Data/PluginSettings.cs:139`
  - Before: `That question is recorded with its control in <c>.claude/team/IN-GAME-BACKLOG.md</c>, so it has a home rather than sitting in this remark as an indefinite hold.`
  - After: `That question needs an in-game check and is not answered here.` Read `$A/IN-GAME-BACKLOG.md` first. If the entry states the control, put the control in the sentence instead.
- `tools/DungeonMasterXIV.Release.Tests/SizeGate.cs:39`
  - Before: `The rows are split ABSOLUTE and DELTA, and the split is ruled rather than chosen here (DMXENG-70).`
  - After: the pre-pass already made it `…ruled rather than chosen here.`, which is fine. Later in that paragraph, `one of them <c>Drain</c> at −120 with a bug-lane ticket held on it` becomes `one of them <c>Drain</c> at −120`.

After each group of about ten files, run `$CP verify "$BASE" <pathspecs>`. It must exit 0. A code difference means a rewrite touched code, so undo that edit. When a concatenated string literal loses a piece, keep the same number of pieces and shorten the text inside them; the verify step compares token counts.

**P6. Review list.** Re-run `$CP scan --review <pathspecs>`. For each hit, either rewrite it by the P5 rules or keep it. Keep a PR or commit reference only when it anchors a checkable fact ("Measured at `2719162`: …"). Keep `QA`, "ticket", "the human" or a 7-hex word only when it isn't agent-era process (e.g. `the human reading the roster`). List every kept hit in the PR body with a few words on why.

**P7. Checks.**

```bash
$CP scan <pathspecs>; echo "scan exit=$?"                          # expect 0 dead references, exit 0
$CP verify "$BASE" <pathspecs> | tee "$SCRATCH/<branch>-verify.txt"; echo "verify exit=${PIPESTATUS[0]}"   # expect exit 0
tools/DungeonMasterXIV.CommentPass/snapshot.sh "$SCRATCH/<branch>-head.txt"
diff "$SCRATCH/<branch>-base.txt" "$SCRATCH/<branch>-head.txt" && echo SAME
```

Expected: scan exit 0, verify exit 0, and `SAME`. If the diff shows a new CS1570/CS1587 warning, a rewritten `///` block has broken XML, so fix that comment. If a test count moved, find the test that reads source text and restore what it reads.

**P8. Commit, push, PR.**

```bash
git add -A -- <pathspecs>
git commit -F - <<'EOF'
chore(comments): <area> cites specs, not retired tickets

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
git push -u origin <branch>
gh pr create --title "<PR title>" --body-file "$SCRATCH/<branch>-body.md"
```

The body file has these sections:
- **Area:** the pathspecs.
- **Dead references:** N in M files before, 0 after.
- **Rules recovered from the archive:** file:line and the source record, one line each.
- **Literal changes:** paste the `LITERAL` rows from `$SCRATCH/<branch>-verify.txt`, `[product]` rows first.
- **Kept review hits:** one line each.
- **Stale, not fixed:** one line each, or "None found".
- **Checks:** the scan, verify and snapshot `diff` outcomes, with the three test lines copied from the head snapshot.

Then stop until the human merges the PR, and run `git switch main && git pull --ff-only`.

---

### Task 1: The CommentPass tool

**Branch:** `chore/comment-pass-tools` (created in Step 1; Task 2 continues on it).

**Files:**
- Create: `tools/DungeonMasterXIV.CommentPass/DungeonMasterXIV.CommentPass.csproj`
- Create: `tools/DungeonMasterXIV.CommentPass/DeadPatterns.cs`
- Create: `tools/DungeonMasterXIV.CommentPass/TagStripper.cs`
- Create: `tools/DungeonMasterXIV.CommentPass/CodeComparison.cs`
- Create: `tools/DungeonMasterXIV.CommentPass/NonCodeComparison.cs`
- Create: `tools/DungeonMasterXIV.CommentPass/Git.cs`
- Create: `tools/DungeonMasterXIV.CommentPass/Program.cs`
- Create: `tools/DungeonMasterXIV.CommentPass/snapshot.sh`
- Create: `tools/DungeonMasterXIV.CommentPass.Tests/DungeonMasterXIV.CommentPass.Tests.csproj`
- Test: `tools/DungeonMasterXIV.CommentPass.Tests/DeadPatternsTests.cs`, `TagStripperTests.cs`, `CodeComparisonTests.cs`, `NonCodeComparisonTests.cs`

**Interfaces:**
- Produces (C#, namespace `DungeonMasterXIV.CommentPass`):
  - `DeadPatterns.Guard : IReadOnlyList<Regex>`, `DeadPatterns.Review : IReadOnlyList<Regex>`, `DeadPatterns.Find(string text, IReadOnlyList<Regex> set) : IReadOnlyList<(int Line, string Match)>`
  - `TagStripper.Strip(string text) : string`
  - `CodeComparison.Compare(string before, string after) : CodeComparisonResult`, with `record CodeComparisonResult(string? CodeDifference, IReadOnlyList<LiteralChange> Literals)` (property `bool CodeChanged`) and `record LiteralChange(int Line, string Before, string After)`
  - `NonCodeComparison.Difference(string path, string before, string after) : string?`, where null means only comments differ
- Produces (CLI): `scan [--review] [pathspec…]`, `strip [pathspec…]`, `verify <base-ref> [pathspec…]`, and `snapshot.sh <out-file>`, as used by the Area pass procedure.

- [ ] **Step 1: Branch and take the base snapshot by hand** (the script doesn't exist yet)

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1 && git fetch origin && git switch main && git pull --ff-only && git switch -c chore/comment-pass-tools origin/main
SCRATCH="${SCRATCH:-$(mktemp -d)}"; echo "$SCRATCH"
dotnet build DungeonMasterXIV.sln --no-incremental -nologo > "$SCRATCH/pr1-base-build.txt" 2>&1; echo "build exit=$?"
dotnet test DungeonMasterXIV.sln --no-build -nologo > "$SCRATCH/pr1-base-test.txt" 2>&1; echo "test exit=$?"
grep -E '^(Passed|Failed)!' "$SCRATCH/pr1-base-test.txt"
```

Expected: both exits 0, with three result lines. Release.Tests must show `Skipped: 0`. Keep these two files: Step 12 turns them into the PR 1 base snapshot.

- [ ] **Step 2: Create both project files**

`tools/DungeonMasterXIV.CommentPass/DungeonMasterXIV.CommentPass.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!--
    Throwaway: exists only while code comments are rewritten to cite specs instead of retired
    tickets. Not in the solution. Deleted by the PR that lands the permanent guard.
  -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>DungeonMasterXIV.CommentPass</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" />
  </ItemGroup>

</Project>
```

`tools/DungeonMasterXIV.CommentPass.Tests/DungeonMasterXIV.CommentPass.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\DungeonMasterXIV.CommentPass\DungeonMasterXIV.CommentPass.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write the failing tests for DeadPatterns and TagStripper**

The samples are built from fragments, so this test source never matches the guard. The same trick keeps Task 11's guard from matching itself.

`tools/DungeonMasterXIV.CommentPass.Tests/DeadPatternsTests.cs`:

```csharp
using Xunit;

namespace DungeonMasterXIV.CommentPass.Tests;

public class DeadPatternsTests
{
    private const string D = "-";

    public static TheoryData<string> Dead => new()
    {
        "BUG" + D + "87", "DMXENG" + D + "105", "SQ" + D + "84", "PRD" + D + "1", "(E" + D + "3)", "(T" + D + "37)",
        "The Spec" + " Owner ruled", "the Deployment" + " Manager", "the deployment" + " manager",
        "Product" + " Owner", "Engineering" + " Lead", "found by the code" + " reviewer",
        "the Code" + " Reviewer", "a b" + "reakfix engineer", "ruled by the " + "HUMAN",
        "in ." + "claude/team/X.md", "engineering" + D + "standards.md", "product" + D + "directives",
        "the " + "brief.md",
    };

    public static TheoryData<string> Alive => new()
    {
        "R" + D + "1.3h", "A" + D + "2.40", "D" + D + "11", "1E" + D + "10", "SHA" + D + "256", "UTF" + D + "8",
        "#89", "the human reading the roster", "a ticket", "QA",
    };

    public static TheoryData<string> Review => new()
    {
        "Decision 10", "QA", "since #120", "at 2719162", "the bug above", "that ticket", "this ruling",
        "dmx-bug17", "the human",
    };

    [Theory, MemberData(nameof(Dead))]
    public void TheGuardFindsEachDeadForm(string text) =>
        Assert.NotEmpty(DeadPatterns.Find(text, DeadPatterns.Guard));

    [Theory, MemberData(nameof(Alive))]
    public void TheGuardLeavesLiveFormsAlone(string text) =>
        Assert.Empty(DeadPatterns.Find(text, DeadPatterns.Guard));

    [Theory, MemberData(nameof(Review))]
    public void TheReviewSetFindsEachQuestionableForm(string text) =>
        Assert.NotEmpty(DeadPatterns.Find(text, DeadPatterns.Review));

    [Fact]
    public void FindReportsOneBasedLineNumbers()
    {
        var hits = DeadPatterns.Find("clean\nclean\nsee BUG" + D + "1 here", DeadPatterns.Guard);
        Assert.Equal(3, Assert.Single(hits).Line);
    }

    [Fact]
    public void ThePatternSourceDoesNotMatchItself()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "DungeonMasterXIV.CommentPass", "DeadPatterns.cs"));
        Assert.Empty(DeadPatterns.Find(source, DeadPatterns.Guard));
    }

    internal static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DungeonMasterXIV.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
```

`tools/DungeonMasterXIV.CommentPass.Tests/TagStripperTests.cs`:

```csharp
using Xunit;

namespace DungeonMasterXIV.CommentPass.Tests;

public class TagStripperTests
{
    private const string D = "-";
    private static readonly string Bug87 = "BUG" + D + "87";
    private static readonly string Bug115 = "BUG" + D + "115";
    private static readonly string Eng70 = "DMXENG" + D + "70";
    private static readonly string T37 = "T" + D + "37";

    [Fact]
    public void APureTagBeforePunctuationGoesWithItsSpace() =>
        Assert.Equal("TWO NULLS APART.</b>", TagStripper.Strip($"TWO NULLS APART ({Bug87}).</b>"));

    [Fact]
    public void APureTagMidSentenceLeavesOneSpace() =>
        Assert.Equal("ruled here and kept", TagStripper.Strip($"ruled here ({Eng70}) and kept"));

    [Fact]
    public void APureListOfTagsGoesWhole() =>
        Assert.Equal("measured.", TagStripper.Strip($"measured ({Bug87}, {Eng70})."));

    [Fact]
    public void ATrailingTagInACitationListGoes() =>
        Assert.Equal("(R" + D + "1.3h)", TagStripper.Strip($"(R{D}1.3h, {Bug115})"));

    [Fact]
    public void ALeadingTagInACitationListGoes() =>
        Assert.Equal("(R" + D + "1.5)", TagStripper.Strip($"({T37}, R{D}1.5)"));

    [Fact]
    public void TagsOnBothEndsOfAListGo() =>
        Assert.Equal("(A" + D + "1.2z)", TagStripper.Strip($"({Bug87}, A{D}1.2z, {Bug115})"));

    [Fact]
    public void ATagThatIsPartOfASentenceIsLeftForJudgment()
    {
        var text = $"which is the collision {Bug87} was";
        Assert.Equal(text, TagStripper.Strip(text));
    }

    [Fact]
    public void AnAmendedTagIsLeftForJudgment()
    {
        var text = $"(amended {Eng70} and later)";
        Assert.Equal(text, TagStripper.Strip(text));
    }

    [Fact]
    public void ADocCommentLineStartingWithATagKeepsItsMarker() =>
        Assert.Equal("/// the rule", TagStripper.Strip($"/// ({Bug87}) the rule"));

    [Fact]
    public void SpecIdsAreNeverTouched()
    {
        var text = $"(R{D}1.3h) and (A{D}2.40, D{D}11)";
        Assert.Equal(text, TagStripper.Strip(text));
    }
}
```

- [ ] **Step 4: Run them and watch them fail**

Run: `dotnet test tools/DungeonMasterXIV.CommentPass.Tests -nologo 2>&1 | tail -5`
Expected: a build failure, because `DeadPatterns` and `TagStripper` don't exist.

- [ ] **Step 5: Implement DeadPatterns and TagStripper**

`tools/DungeonMasterXIV.CommentPass/DeadPatterns.cs`:

```csharp
using System.Text.RegularExpressions;

namespace DungeonMasterXIV.CommentPass;

/// <summary>
/// References that point only into the retired agent system, and forms worth a second look.
/// Every pattern is assembled from pieces so this file never matches its own guard.
/// </summary>
public static class DeadPatterns
{
    private const string Dash = "-";
    private const string NotAfterWordChar = "(?<![A-Za-z0-9])";

    /// <summary>A hit here is a dead reference, full stop.</summary>
    public static readonly IReadOnlyList<Regex> Guard =
    [
        new(NotAfterWordChar + "(?:BUG|DMXENG|SQ|PRD|E|T)" + Dash + @"\d+"),
        new("[Ss]pec" + " [Oo]wner"),
        new("[Dd]eployment" + " [Mm]anager"),
        new("[Pp]roduct" + " [Oo]wner"),
        new("[Ee]ngineering" + " [Ll]ead"),
        new("[Cc]ode" + " [Rr]eviewer"),
        new("[Bb]" + "reakfix"),
        new("the " + "HUMAN"),
        new(@"\." + "claude/"),
        new("engineering" + Dash + "standards"),
        new("product" + Dash + "directives"),
        new(@"\bbrief" + @"\.md"),
    ];

    /// <summary>A hit here is rewritten or deliberately kept, and a kept one is listed in the PR.</summary>
    public static readonly IReadOnlyList<Regex> Review =
    [
        new(@"[Dd]ecision \d+"),
        new(@"\bQA\b"),
        new(@"\bthe human\b"),
        new(@"(?<![&\w])#\d{2,3}\b"),
        new(@"\b[0-9a-f]{7}\b"),
        new(@"\bticket"),
        new(@"\b(?:the|that|this) (?:bug|ticket|ruling|escalation|brief)\b"),
        new(@"[Bb]ug ?\d+"),
    ];

    /// <summary>Every match of any pattern in the set, with its one-based line.</summary>
    public static IReadOnlyList<(int Line, string Match)> Find(string text, IReadOnlyList<Regex> set)
    {
        var hits = new List<(int Line, string Match)>();
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            foreach (var pattern in set)
            {
                foreach (Match match in pattern.Matches(lines[index]))
                {
                    hits.Add((index + 1, match.Value));
                }
            }
        }

        return hits;
    }
}
```

`tools/DungeonMasterXIV.CommentPass/TagStripper.cs`:

```csharp
using System.Text.RegularExpressions;

namespace DungeonMasterXIV.CommentPass;

/// <summary>
/// Deletes retired ticket IDs only where nothing but citation surrounds them: a parenthesis holding
/// only such IDs, or one that opens or closes a list of citations. Anything inside a sentence is
/// left for a person.
/// </summary>
public static class TagStripper
{
    private const string Id = "(?<![A-Za-z0-9])(?:BUG|DMXENG|SQ|E|T)" + "-" + @"\d+";

    private static readonly Regex Pure = new($@" ?\({Id}(?:, ?{Id})*\)");
    private static readonly Regex Leading = new($@"\({Id}, ?");
    private static readonly Regex Trailing = new($@", ?{Id}(?=[,)])");

    public static string Strip(string text)
    {
        var result = Pure.Replace(text, string.Empty);
        string previous;
        do
        {
            previous = result;
            result = Trailing.Replace(Leading.Replace(result, "("), string.Empty);
        }
        while (result != previous);

        return result;
    }
}
```

- [ ] **Step 6: Run the tests and see them pass**

Run: `dotnet test tools/DungeonMasterXIV.CommentPass.Tests -nologo 2>&1 | grep -E '^(Passed|Failed)!|error'`
Expected: `Passed!` with `Failed: 0`, and Total equal to the number of test cases (every theory row counts once).

- [ ] **Step 7: Write the failing tests for the comparisons**

`tools/DungeonMasterXIV.CommentPass.Tests/CodeComparisonTests.cs`:

```csharp
using Xunit;

namespace DungeonMasterXIV.CommentPass.Tests;

public class CodeComparisonTests
{
    private const string Before = """
        /// <summary>Old words (tag).</summary>
        public class C
        {
            // an old comment
            public string M() => "old message";
        }
        """;

    [Fact]
    public void ACommentOnlyChangeIsNotACodeChange()
    {
        var after = Before.Replace("Old words (tag).", "New words.").Replace("an old comment", "new");
        var result = CodeComparison.Compare(Before, after);
        Assert.False(result.CodeChanged, result.CodeDifference);
        Assert.Empty(result.Literals);
    }

    [Fact]
    public void ALiteralChangeIsReportedWithItsLine()
    {
        var result = CodeComparison.Compare(Before, Before.Replace("old message", "new message"));
        Assert.False(result.CodeChanged, result.CodeDifference);
        var change = Assert.Single(result.Literals);
        Assert.Equal(5, change.Line);
        Assert.Equal("\"old message\"", change.Before);
        Assert.Equal("\"new message\"", change.After);
    }

    [Fact]
    public void AnInterpolatedTextChangeIsALiteralChange()
    {
        var before = "class C { string M(int n) => $\"old {n} words\"; }";
        var result = CodeComparison.Compare(before, before.Replace("old ", "new "));
        Assert.False(result.CodeChanged, result.CodeDifference);
        Assert.Single(result.Literals);
    }

    [Fact]
    public void AnIdentifierChangeIsACodeChange() =>
        Assert.True(CodeComparison.Compare(Before, Before.Replace("M()", "N()")).CodeChanged);

    [Fact]
    public void ARemovedTokenIsACodeChange() =>
        Assert.True(CodeComparison.Compare(Before, Before.Replace("public class", "class")).CodeChanged);

    [Fact]
    public void ADroppedConcatenationPieceIsACodeChange()
    {
        var before = "class C { string M() => \"a\" + \"b\"; }";
        Assert.True(CodeComparison.Compare(before, "class C { string M() => \"a\"; }").CodeChanged);
    }

    [Fact]
    public void APreprocessorDirectiveChangeIsACodeChange()
    {
        var before = "#if DEBUG\nclass C { }\n#endif\n";
        Assert.True(CodeComparison.Compare(before, before.Replace("DEBUG", "RELEASE")).CodeChanged);
    }

    [Fact]
    public void DisabledTextIsCode()
    {
        var before = "#if NEVER\nclass Old { }\n#endif\nclass C { }\n";
        Assert.True(CodeComparison.Compare(before, before.Replace("Old", "New")).CodeChanged);
    }
}
```

`tools/DungeonMasterXIV.CommentPass.Tests/NonCodeComparisonTests.cs`:

```csharp
using Xunit;

namespace DungeonMasterXIV.CommentPass.Tests;

public class NonCodeComparisonTests
{
    private const string Project = """
        <Project>
          <!--
            Old words (tag).
          -->
          <PropertyGroup>
            <OutputType>Exe</OutputType>
          </PropertyGroup>
        </Project>
        """;

    [Fact]
    public void AProjectCommentChangeIsNotADifference() =>
        Assert.Null(NonCodeComparison.Difference("x/A.csproj", Project, Project.Replace("Old words (tag).", "New.")));

    [Fact]
    public void AProjectCommentRemovedWholeIsNotADifference() =>
        Assert.Null(NonCodeComparison.Difference(
            "Directory.Build.targets", Project, Project.Replace("  <!--\n    Old words (tag).\n  -->\n", "")));

    [Fact]
    public void AProjectPropertyChangeIsADifference() =>
        Assert.NotNull(NonCodeComparison.Difference("x/A.csproj", Project, Project.Replace("Exe", "Library")));

    [Theory]
    [InlineData("deploy/Dockerfile")]
    [InlineData(".gitignore")]
    [InlineData(".dockerignore")]
    [InlineData("tools/DungeonMasterXIV.Release.Tests/size-gate-baseline.txt")]
    public void AHashLineChangeIsNotADifference(string path) =>
        Assert.Null(NonCodeComparison.Difference(path, "# old (tag)\nFROM x\n", "# new\nFROM x\n"));

    [Theory]
    [InlineData("deploy/Dockerfile")]
    [InlineData(".gitignore")]
    public void AnInstructionChangeIsADifference(string path) =>
        Assert.NotNull(NonCodeComparison.Difference(path, "# c\nFROM x\n", "# c\nFROM y\n"));

    [Fact]
    public void TheClockFixtureHeaderMayChangeButItsFormsMayNot()
    {
        const string before = "Title line.\n\nOld header (tag).\nMore header.\n\nDateTime.Now\nStopwatch\n";
        var path = "tests/DungeonMasterXIV.Tests/ClockFormsFixture.txt";
        Assert.Null(NonCodeComparison.Difference(path, before, before.Replace("Old header (tag).", "New header.")));
        Assert.NotNull(NonCodeComparison.Difference(path, before, before.Replace("Stopwatch", "Stopwatc")));
    }

    [Fact]
    public void AFileTypeWithNoRuleIsADifference() =>
        Assert.NotNull(NonCodeComparison.Difference("README.md", "a", "b"));
}
```

- [ ] **Step 8: Run them and watch them fail**

Run: `dotnet test tools/DungeonMasterXIV.CommentPass.Tests -nologo 2>&1 | tail -5`
Expected: a build failure, because `CodeComparison` and `NonCodeComparison` don't exist.

- [ ] **Step 9: Implement the comparisons**

`tools/DungeonMasterXIV.CommentPass/CodeComparison.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DungeonMasterXIV.CommentPass;

public sealed record LiteralChange(int Line, string Before, string After);

public sealed record CodeComparisonResult(string? CodeDifference, IReadOnlyList<LiteralChange> Literals)
{
    public bool CodeChanged => CodeDifference is not null;
}

/// <summary>
/// Two versions of a C# file are the same code when their tokens, preprocessor directives and
/// disabled text match in order, comments ignored. Literal text may differ and is reported.
/// </summary>
public static class CodeComparison
{
    private static readonly CSharpParseOptions Options = new(LanguageVersion.Preview);

    private static readonly HashSet<SyntaxKind> LiteralKinds =
    [
        SyntaxKind.StringLiteralToken, SyntaxKind.Utf8StringLiteralToken, SyntaxKind.CharacterLiteralToken,
        SyntaxKind.InterpolatedStringTextToken, SyntaxKind.SingleLineRawStringLiteralToken,
        SyntaxKind.MultiLineRawStringLiteralToken, SyntaxKind.Utf8SingleLineRawStringLiteralToken,
        SyntaxKind.Utf8MultiLineRawStringLiteralToken,
    ];

    private sealed record Item(SyntaxKind Kind, string Text, int Line);

    public static CodeComparisonResult Compare(string before, string after)
    {
        var (was, now) = (Items(before), Items(after));
        var literals = new List<LiteralChange>();
        if (was.Count != now.Count)
        {
            return new($"{was.Count} code items became {now.Count}", literals);
        }

        for (var index = 0; index < was.Count; index++)
        {
            var (b, a) = (was[index], now[index]);
            if (b.Kind != a.Kind || (b.Text != a.Text && !LiteralKinds.Contains(b.Kind)))
            {
                return new($"line {b.Line}: '{b.Text}' became '{a.Text}' (line {a.Line})", literals);
            }

            if (b.Text != a.Text)
            {
                literals.Add(new LiteralChange(b.Line, b.Text, a.Text));
            }
        }

        return new(null, literals);
    }

    private static List<Item> Items(string source)
    {
        var items = new List<Item>();
        foreach (var token in CSharpSyntaxTree.ParseText(source, Options).GetRoot().DescendantTokens())
        {
            AddCodeTrivia(token.LeadingTrivia, items);
            items.Add(new Item(token.Kind(), token.Text, LineOf(token.GetLocation())));
            AddCodeTrivia(token.TrailingTrivia, items);
        }

        return items;
    }

    private static void AddCodeTrivia(SyntaxTriviaList trivia, List<Item> items)
    {
        foreach (var piece in trivia)
        {
            if (piece.IsDirective || piece.IsKind(SyntaxKind.DisabledTextTrivia))
            {
                items.Add(new Item(piece.Kind(), piece.ToFullString().Trim(), LineOf(piece.GetLocation())));
            }
        }
    }

    private static int LineOf(Location location) => location.GetLineSpan().StartLinePosition.Line + 1;
}
```

`tools/DungeonMasterXIV.CommentPass/NonCodeComparison.cs`:

```csharp
using System.Text.RegularExpressions;

namespace DungeonMasterXIV.CommentPass;

/// <summary>
/// Two versions of a non-C# file are the same when they match once their comments are removed.
/// A file type with no rule here is always a difference, so an unexpected edit cannot pass.
/// </summary>
public static class NonCodeComparison
{
    private static readonly Regex XmlComment = new("<!--.*?-->", RegexOptions.Singleline);

    public static string? Difference(string path, string before, string after)
    {
        var meaningful = RuleFor(path);
        if (meaningful is null)
        {
            return $"no comparison rule for {path}";
        }

        var (was, now) = (meaningful(before), meaningful(after));
        for (var index = 0; index < Math.Max(was.Count, now.Count); index++)
        {
            var (b, a) = (index < was.Count ? was[index] : "<end>", index < now.Count ? now[index] : "<end>");
            if (b != a)
            {
                return $"'{b}' became '{a}'";
            }
        }

        return null;
    }

    private static Func<string, List<string>>? RuleFor(string path)
    {
        var name = Path.GetFileName(path);
        return Path.GetExtension(path) switch
        {
            ".csproj" or ".targets" or ".props" => text => Lines(XmlComment.Replace(text, string.Empty).Split('\n')),
            _ when name is "Dockerfile" or ".gitignore" or ".dockerignore" or "size-gate-baseline.txt" =>
                text => Lines(text.Split('\n').Where(line => !line.TrimStart().StartsWith('#'))),
            _ when name is "ClockFormsFixture.txt" => text => Lines(AfterHeader(text.Split('\n'))),
            _ => null,
        };
    }

    /// <summary>Everything after the second blank line: the title and header paragraph are prose.</summary>
    private static IEnumerable<string> AfterHeader(string[] lines)
    {
        var blanks = 0;
        var index = 0;
        while (index < lines.Length && blanks < 2)
        {
            blanks += lines[index].Trim().Length == 0 ? 1 : 0;
            index++;
        }

        return lines.Skip(index);
    }

    private static List<string> Lines(IEnumerable<string> lines) =>
        [.. lines.Select(line => line.TrimEnd()).Where(line => line.Length > 0)];
}
```

- [ ] **Step 10: Run all tool tests and see them pass**

Run: `dotnet test tools/DungeonMasterXIV.CommentPass.Tests -nologo 2>&1 | grep -E '^(Passed|Failed)!|error'`
Expected: `Passed!`, with `Failed: 0` and `Skipped: 0`.

- [ ] **Step 11: Add Git.cs, Program.cs and snapshot.sh**

`tools/DungeonMasterXIV.CommentPass/Git.cs`:

```csharp
using System.Diagnostics;

namespace DungeonMasterXIV.CommentPass;

/// <summary>git, run in the repository root. A non-zero exit throws, so nothing reads as empty.</summary>
public static class Git
{
    public static string Root { get; } = Run(Environment.CurrentDirectory, "rev-parse", "--show-toplevel").Trim();

    public static string Show(string baseRef, string path) => Run(Root, "show", $"{baseRef}:{path}");

    /// <summary>Files differing between <paramref name="baseRef"/> and the working tree, with a one-letter status.</summary>
    public static IReadOnlyList<(char Status, string Path)> Changed(string baseRef, IReadOnlyList<string> pathspecs) =>
        [.. Run(Root, ["diff", "--name-status", "--no-renames", baseRef, "--", .. pathspecs])
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => (line[0], line[(line.IndexOf('\t') + 1)..]))];

    public static IReadOnlyList<string> Tracked(IReadOnlyList<string> pathspecs) =>
        [.. Run(Root, ["ls-files", "-z", "--", .. pathspecs]).Split('\0', StringSplitOptions.RemoveEmptyEntries)];

    private static string Run(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start");
        var output = git.StandardOutput.ReadToEnd();
        var errors = git.StandardError.ReadToEnd();
        git.WaitForExit();
        return git.ExitCode == 0 ? output : throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {errors}");
    }
}
```

`tools/DungeonMasterXIV.CommentPass/Program.cs`:

```csharp
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
```

`tools/DungeonMasterXIV.CommentPass/snapshot.sh`:

```bash
#!/usr/bin/env bash
# Writes this tree's build warnings and test totals to $1 in a form two runs can diff.
# Warning locations lose their line and column: rewriting comments moves lines, not code.
set -uo pipefail
out="$1"
root="$(git rev-parse --show-toplevel)"
build="$(mktemp)"
tests="$(mktemp)"
cd "$root" || exit 1

if ! dotnet build DungeonMasterXIV.sln --no-incremental -nologo > "$build" 2>&1; then
  tail -40 "$build"; echo "BUILD FAILED"; exit 1
fi
if ! dotnet test DungeonMasterXIV.sln --no-build -nologo > "$tests" 2>&1; then
  tail -60 "$tests"; echo "TESTS FAILED"; exit 1
fi

{
  echo "== warnings"
  grep -E ': (warning|error) [A-Z]+[0-9]+' "$build" \
    | sed -E "s#${root}/##; s/\([0-9]+,[0-9]+\)//; s/ \[[^]]*\]$//" | sort -u
  echo "== tests"
  grep -E '^(Passed|Failed)!' "$tests" | sed -E 's/Duration: [^-]*- //' | sort
} > "$out"
cat "$out"
```

```bash
chmod +x tools/DungeonMasterXIV.CommentPass/snapshot.sh
```

- [ ] **Step 12: Smoke-test the CLI against this tree**

```bash
CP="dotnet run --project tools/DungeonMasterXIV.CommentPass --"
$CP scan | tail -1; echo "exit=${PIPESTATUS[0]}"
$CP scan --review | tail -1
$CP verify HEAD; echo "exit=$?"
tools/DungeonMasterXIV.CommentPass/snapshot.sh "$SCRATCH/pr1-base.txt"
grep -E '^(Passed|Failed)!' "$SCRATCH/pr1-base-test.txt" | sed -E 's/Duration: [^-]*- //' | sort | diff - <(sed -n '/== tests/,$p' "$SCRATCH/pr1-base.txt" | tail -n +2) && echo "TOTALS MATCH STEP 1"
```

Expected:
- `scan` reports roughly 1,000 dead references in roughly 300 files, and exits 1.
- `verify HEAD` reports `0 literal changes, 0 code changes` and exits 0, because the new files are excluded.
- The snapshot's test totals match Step 1 (`TOTALS MATCH STEP 1`). Adding the tool did not change the solution's build or tests, since it isn't in the `.sln`.

Then prove `verify` bites. Mutate one character of code in a tracked file and confirm the mutation landed:

```bash
sed -i '' 's/public static class CodeOnly/public static class CodeOnlyX/' tools/DungeonMasterXIV.Sizes/CodeOnly.cs
grep -c 'CodeOnlyX' tools/DungeonMasterXIV.Sizes/CodeOnly.cs   # expect 1
$CP verify HEAD; echo "exit=$?"                                 # expect CODE CHANGED …CodeOnly.cs, exit=1
git checkout -- tools/DungeonMasterXIV.Sizes/CodeOnly.cs
git status --short tools/DungeonMasterXIV.Sizes                 # expect nothing
```

- [ ] **Step 13: Commit**

```bash
git add tools/DungeonMasterXIV.CommentPass tools/DungeonMasterXIV.CommentPass.Tests
git commit -F - <<'EOF'
chore(tools): add a throwaway CommentPass tool to scan, strip and verify comment rewrites

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 2: tools/ (PR 1)

**Branch:** stays on `chore/comment-pass-tools`. Skip P1 and P2: `BASE` is the Task 1 base, `git merge-base HEAD origin/main`, and the base snapshot is `$SCRATCH/pr1-base.txt` from Task 1 Step 12.

**Pathspecs:** `tools/DungeonMasterXIV.Release tools/DungeonMasterXIV.Release.Tests tools/DungeonMasterXIV.Sizes`
About 155 dead references in 53 files, including `size-gate-baseline.txt` and `DungeonMasterXIV.Sizes.csproj`.

**PR title:** `chore(comments): tools cite specs, not retired tickets (1/7)`

**Interfaces:**
- Consumes: the CLI from Task 1.
- Produces: `tools/` free of dead references, and PR 1 merged.

- [ ] **Step 1:** `BASE=$(git merge-base HEAD origin/main)`, and confirm `$SCRATCH/pr1-base.txt` exists.
- [ ] **Step 2:** Run P3 (inventory) over the pathspecs.
- [ ] **Step 3:** Run P4 (pre-pass) and commit it.
- [ ] **Step 4:** Run P5 (judgment pass). `SizeGate.cs`, `ContainsMainFactAttribute.cs` and `size-gate-baseline.txt` carry most of the ruling narrative ("ruled by the Deployment Manager", "DMXENG-70"). Keep each rule and drop its author.
- [ ] **Step 5:** Run P6 (review list).
- [ ] **Step 6:** Run P7 (checks), using `$SCRATCH/pr1-base.txt` as the base file.
- [ ] **Step 7:** Run P8 (commit, push, PR). The PR body also says the CommentPass tool is included, stays out of the `.sln`, and is deleted in PR 7. Wait for the merge.

---

### Task 3: Relay and deploy (PR 2)

**Branch:** `chore/comments-relay`

**Pathspecs:** `src/DungeonMasterXIV.Relay tests/DungeonMasterXIV.Relay.Tests deploy .dockerignore`
About 55 dead references in 24 files.

**PR title:** `chore(comments): relay and deploy cite specs, not retired tickets (2/7)`

- [ ] **Step 1:** Run P1 and P2.
- [ ] **Step 2:** Run P3 and P4.
- [ ] **Step 3:** Run P5. `RelayApp.cs` holds operator-facing strings. Every literal change there is `[product]` and goes first in the PR body. The temp-directory prefixes `"dmx-bug15"` / `"dmx-bug17"` are review hits. Rename them to what they hold, e.g. `"dmx-cert-unreadable"`, keeping the count of literal pieces.
- [ ] **Step 4:** Run P6, P7 and P8. Wait for the merge.

---

### Task 4: Core outside Net (PR 3)

**Branch:** `chore/comments-core`

**Pathspecs:** `src/DungeonMasterXIV.Core/Data src/DungeonMasterXIV.Core/Chat src/DungeonMasterXIV.Core/Rolls src/DungeonMasterXIV.Core/Campaigns src/DungeonMasterXIV.Core/Services src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj`
About 54 dead references in 26 files.

**PR title:** `chore(comments): core data, chat, rolls and campaigns cite specs, not retired tickets (3/7)`

- [ ] **Step 1:** Run P1 and P2.
- [ ] **Step 2:** Run P3 and P4.
- [ ] **Step 3:** Run P5. This area holds two worked examples, `RollEvaluator.cs` and `PluginSettings.cs`, and the "found by the code reviewer" lines in `RetainedLogFileArchive.cs` and `RetainedLogFormat.cs`.
- [ ] **Step 4:** Run P6, P7 and P8. Wait for the merge.

---

### Task 5: Net, A–R (PR 4, first half)

**Branch:** `chore/comments-net` (Task 6 continues on it).

**Pathspecs:** `'src/DungeonMasterXIV.Core/Net/[A-R]*'`
About 140 dead references in 40 files.

- [ ] **Step 1:** Run P1 and P2 with the branch `chore/comments-net`.
- [ ] **Step 2:** Run P3 and P4 over **the whole of `src/DungeonMasterXIV.Core/Net`**, so the pre-pass commit covers both halves.
- [ ] **Step 3:** Run P5 over `'src/DungeonMasterXIV.Core/Net/[A-R]*'`. `AdmissionControl.cs`, `JoinAttempt.cs`, `InboundWiring.cs` and `HostRunner.cs` carry the densest narrative.
- [ ] **Step 4:** Run P6 over the same pathspec, then `$CP verify "$BASE" src/DungeonMasterXIV.Core/Net`. It must exit 0.
- [ ] **Step 5:** Commit with the message `chore(comments): net A–R cites specs, not retired tickets`. Don't push yet.

---

### Task 6: Net, S–Z (PR 4, second half)

**Branch:** stays on `chore/comments-net`. `BASE` and the base snapshot come from Task 5.

**Pathspecs:** `'src/DungeonMasterXIV.Core/Net/[S-Z]*'`
About 120 dead references in 29 files. `SessionCoordinator.cs` alone has 47.

**PR title:** `chore(comments): session network layer cites specs, not retired tickets (4/7)`

- [ ] **Step 1:** Run P5 and P6 over the pathspec.
- [ ] **Step 2:** Run P7 and P8 over **`src/DungeonMasterXIV.Core/Net`** (both halves). Wait for the merge.

---

### Task 7: Windows, plugin entry and root build files (PR 5)

**Branch:** `chore/comments-plugin`

**Pathspecs:** `Windows Plugin.cs Services Net Data DungeonMasterXIV.csproj Directory.Build.targets Directory.Build.props .gitignore`
About 58 dead references in 15 files.

**PR title:** `chore(comments): plugin windows and build files cite specs, not retired tickets (5/7)`

- [ ] **Step 1:** Run P1 and P2.
- [ ] **Step 2:** Run P3 and P4.
- [ ] **Step 3:** Run P5.
  - `Windows/` string literals are shipped copy. `ShippedCopyMeetsItsConstraintsTests` runs in the snapshot, and every literal change is `[product]`.
  - `ConfigWindow.cs:193` ("A-1.2z (BUG-141) and SQ-87 (#217) both live in this one control") keeps `A-1.2z` and restates SQ-87's rule from the ledger.
  - `JoinFlowView.cs:17` ("PR #89's, whose body is authoritative") restates the seam rule from `gh pr view 89`.
- [ ] **Step 4:** Run P6, P7 and P8. Wait for the merge.

---

### Task 8: Plugin tests, A–D (PR 6a, first half)

**Branch:** `chore/comments-tests-a-s` (Task 9 continues on it).

**Pathspecs:** `'tests/DungeonMasterXIV.Tests/[A-D]*'`
About 110 dead references in 39 files, including `ClockFormsFixture.txt` (edit only its header paragraph) and `BaseChatFixture.cs`.

- [ ] **Step 1:** Run P1 and P2 with the branch `chore/comments-tests-a-s`.
- [ ] **Step 2:** Run P3 and P4 over `'tests/DungeonMasterXIV.Tests/[A-S]*'`, so the pre-pass covers both halves.
- [ ] **Step 3:** Run P5 and P6 over `'tests/DungeonMasterXIV.Tests/[A-D]*'`. Assertion messages are string literals. Rewrite them by the same rules, keeping the count of concatenated pieces.
- [ ] **Step 4:** Run `$CP verify "$BASE" 'tests/DungeonMasterXIV.Tests/[A-S]*'`. It must exit 0.
- [ ] **Step 5:** Commit with the message `chore(comments): plugin tests A–D cite specs, not retired tickets`. Don't push yet.

---

### Task 9: Plugin tests, E–S (PR 6a, second half)

**Branch:** stays on `chore/comments-tests-a-s`.

**Pathspecs:** `'tests/DungeonMasterXIV.Tests/[E-S]*'`
About 125 dead references in 38 files.

**PR title:** `chore(comments): plugin tests A–S cite specs, not retired tickets (6a/7)`

- [ ] **Step 1:** Run P5 and P6 over the pathspec. In `EveryMessageTypeReachesAnArmTests.cs` the BUG-43 worked example applies.
- [ ] **Step 2:** Run P7 and P8 over `'tests/DungeonMasterXIV.Tests/[A-S]*'`. Wait for the merge.

---

### Task 10: Plugin tests, T–Z (PR 6b)

**Branch:** `chore/comments-tests-t-z`

**Pathspecs:** `'tests/DungeonMasterXIV.Tests/[T-Z]*'`
About 170 dead references in 53 files.

**PR title:** `chore(comments): plugin tests T–Z cite specs, not retired tickets (6b/7)`

- [ ] **Step 1:** Run P1 and P2.
- [ ] **Step 2:** Run P3 and P4.
- [ ] **Step 3:** Run P5 and P6.
- [ ] **Step 4:** Run P7 and P8, and add one more check before P8:

```bash
$CP scan; echo "whole-tree exit=$?"   # expect 0 dead references across every tracked file outside docs/
```

If that finds anything, it is in a path no task named. Fix it on this branch under the same rules and say so in the PR body. Wait for the merge.

---

### Task 11: The guard, and retiring the tool (PR 7)

**Branch:** `chore/dead-reference-guard`

**Files:**
- Create: `tools/DungeonMasterXIV.Release.Tests/DeadReferences.cs`
- Create: `tools/DungeonMasterXIV.Release.Tests/NothingCitesTheRetiredAgentSystemTests.cs`
- Delete: `tools/DungeonMasterXIV.CommentPass/`, `tools/DungeonMasterXIV.CommentPass.Tests/`
- Modify: `docs/superpowers/specs/README.md` (remove the "Decision n" bullet)
- Modify: `docs/superpowers/specs/2026-09-30-comment-id-rewrite-design.md` (Status line)

**Interfaces:**
- Consumes: the Guard patterns from `tools/DungeonMasterXIV.CommentPass/DeadPatterns.cs` (Task 1), moved here verbatim, and `TheBuild.RepositoryRoot() : DirectoryInfo` (existing, `tools/DungeonMasterXIV.Release.Tests/TheBuild.cs:123`).
- Produces: `DeadReferences.Patterns : IReadOnlyList<Regex>`, `DeadReferences.TrackedOutsideDocs(string root) : IReadOnlyList<string>` and `DeadReferences.In(string root) : IReadOnlyList<string>`, whose rows read `path:line: match`.

- [ ] **Step 1: Branch and take the base snapshot**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1 && git fetch origin && git switch main && git pull --ff-only && git switch -c chore/dead-reference-guard origin/main
tools/DungeonMasterXIV.CommentPass/snapshot.sh "$SCRATCH/pr7-base.txt"
```

- [ ] **Step 2: Write the test**

`tools/DungeonMasterXIV.Release.Tests/NothingCitesTheRetiredAgentSystemTests.cs`:

```csharp
using System.Linq;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// Code, tests, tools and build files cite the specs in <c>docs/superpowers/specs</c>, never the
/// retired ticket system, its roles, or its archived files. Those resolve nowhere a reader can follow.
/// </summary>
/// <remarks>
/// Every sample below is assembled from pieces, as the patterns are, so this file does not trip the
/// scan it defines.
/// </remarks>
public class NothingCitesTheRetiredAgentSystemTests
{
    private const string D = "-";

    public static TheoryData<string> Dead => new()
    {
        "BUG" + D + "87", "DMXENG" + D + "105", "SQ" + D + "84", "PRD" + D + "1", "(E" + D + "3)", "(T" + D + "37)",
        "Spec" + " Owner", "deployment" + " manager", "Product" + " Owner", "Engineering" + " Lead",
        "code" + " reviewer", "B" + "reakfix", "the " + "HUMAN", "." + "claude/x", "engineering" + D + "standards",
        "product" + D + "directives", "brief" + ".md",
    };

    public static TheoryData<string> Alive => new()
    {
        "R" + D + "1.3h", "A" + D + "2.40", "D" + D + "11", "1E" + D + "10", "SHA" + D + "256", "UTF" + D + "8", "#89",
    };

    [Fact]
    public void NoTrackedFileOutsideDocsCitesTheRetiredAgentSystem()
    {
        var hits = DeadReferences.In(TheBuild.RepositoryRoot().FullName);

        Assert.True(hits.Count == 0,
            "These cite the retired agent system. Cite the spec's R-/A-/D- ID or state the rule instead:\n  "
            + string.Join("\n  ", hits));
    }

    [Fact]
    public void TheScanReadsTheWholeTreeNotAnEmptyOne()
    {
        var files = DeadReferences.TrackedOutsideDocs(TheBuild.RepositoryRoot().FullName);

        Assert.True(files.Count > 400, $"only {files.Count} tracked files were scanned");
        Assert.Contains("Plugin.cs", files);
        Assert.Contains("deploy/Dockerfile", files);
        Assert.Contains("DungeonMasterXIV.csproj", files);
        Assert.DoesNotContain(files, file => file.StartsWith("docs/", System.StringComparison.Ordinal));
    }

    [Theory, MemberData(nameof(Dead))]
    public void EachRetiredFormIsCaught(string text) =>
        Assert.Contains(DeadReferences.Patterns, pattern => pattern.IsMatch(text));

    [Theory, MemberData(nameof(Alive))]
    public void SpecIdsAndOrdinaryTokensAreNot(string text) =>
        Assert.DoesNotContain(DeadReferences.Patterns, pattern => pattern.IsMatch(text));
}
```

- [ ] **Step 3: Run it and watch it fail**

Run: `dotnet test tools/DungeonMasterXIV.Release.Tests -nologo --filter NothingCitesTheRetiredAgentSystemTests 2>&1 | tail -5`
Expected: a build failure, because `DeadReferences` doesn't exist.

- [ ] **Step 4: Write DeadReferences**

`tools/DungeonMasterXIV.Release.Tests/DeadReferences.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// References that resolve only into the retired agent system's archive: its ticket, bug, ruling and
/// escalation IDs, its role names, its directory and its standards documents.
/// </summary>
/// <remarks>
/// Each pattern is assembled from pieces so this file never matches itself. Spec IDs (R-, A-, D-)
/// are not here: they resolve to <c>docs/superpowers/specs</c>.
/// </remarks>
internal static class DeadReferences
{
    private const string Dash = "-";

    public static readonly IReadOnlyList<Regex> Patterns =
    [
        new("(?<![A-Za-z0-9])(?:BUG|DMXENG|SQ|PRD|E|T)" + Dash + @"\d+"),
        new("[Ss]pec" + " [Oo]wner"),
        new("[Dd]eployment" + " [Mm]anager"),
        new("[Pp]roduct" + " [Oo]wner"),
        new("[Ee]ngineering" + " [Ll]ead"),
        new("[Cc]ode" + " [Rr]eviewer"),
        new("[Bb]" + "reakfix"),
        new("the " + "HUMAN"),
        new(@"\." + "claude/"),
        new("engineering" + Dash + "standards"),
        new("product" + Dash + "directives"),
        new(@"\bbrief" + @"\.md"),
    ];

    /// <summary>Every hit in the tree, as <c>path:line: match</c>.</summary>
    public static IReadOnlyList<string> In(string root) =>
        [.. TrackedOutsideDocs(root).SelectMany(path => Hits(path, File.ReadAllLines(Path.Combine(root, path))))];

    /// <summary>What git tracks, minus <c>docs/</c>. A failing git throws rather than scanning nothing.</summary>
    public static IReadOnlyList<string> TrackedOutsideDocs(string root)
    {
        using var git = Process.Start(new ProcessStartInfo("git", "ls-files -z")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("git did not start");

        var listed = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
        {
            throw new InvalidOperationException("git ls-files failed: " + git.StandardError.ReadToEnd());
        }

        return [.. listed.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => !path.StartsWith("docs/", StringComparison.Ordinal))];
    }

    private static IEnumerable<string> Hits(string path, string[] lines) =>
        lines.SelectMany((line, index) => Patterns
            .SelectMany(pattern => pattern.Matches(line))
            .Select(match => $"{path}:{index + 1}: {match.Value}"));
}
```

- [ ] **Step 5: Delete the tool, so its fixtures don't trip the guard**

```bash
git rm -r -q tools/DungeonMasterXIV.CommentPass tools/DungeonMasterXIV.CommentPass.Tests
rm -rf tools/DungeonMasterXIV.CommentPass tools/DungeonMasterXIV.CommentPass.Tests   # untracked bin/obj
```

- [ ] **Step 6: Run the guard tests and see them pass**

Run: `dotnet test tools/DungeonMasterXIV.Release.Tests -nologo --filter NothingCitesTheRetiredAgentSystemTests 2>&1 | grep -E '^(Passed|Failed)!'`
Expected: `Passed!` with `Failed: 0`, and Total equal to 2 facts plus the theory rows (17 Dead + 7 Alive), which is 26.

- [ ] **Step 7: Prove the guard bites**

```bash
F=src/DungeonMasterXIV.Core/Chat/MessageLimits.cs
printf '// seeded %s-%s\n' BUG 1 >> "$F"
tail -1 "$F"                                                    # expect: // seeded BUG-1
dotnet test tools/DungeonMasterXIV.Release.Tests -nologo --filter NothingCitesTheRetiredAgentSystemTests 2>&1 | grep -E 'MessageLimits.cs:[0-9]+: BUG|^(Passed|Failed)!'
# expect: a row naming MessageLimits.cs, then Failed! with Failed: 1
git checkout -- "$F" && git status --short "$F"                 # expect nothing
```

- [ ] **Step 8: Closing doc edits**

In `docs/superpowers/specs/README.md`, delete this line:

```
- **"Decision n"** in a code comment means product-overview "Session panel" item n.
```

In `docs/superpowers/specs/2026-09-30-comment-id-rewrite-design.md`, change `**Status:** Approved for planning` to `**Status:** Implemented <today's date, YYYY-MM-DD>`.

- [ ] **Step 9: Full check**

`snapshot.sh` was deleted with the tool, so run its commands inline:

```bash
dotnet build DungeonMasterXIV.sln --no-incremental -nologo 2>&1 | grep -E ': (warning|error) [A-Z]+[0-9]+' | sed -E "s#$PWD/##; s/\([0-9]+,[0-9]+\)//; s/ \[[^]]*\]$//" | sort -u > "$SCRATCH/pr7-warnings.txt"
diff <(sed -n '/== warnings/,/== tests/p' "$SCRATCH/pr7-base.txt" | sed '1d;$d') "$SCRATCH/pr7-warnings.txt" && echo "WARNINGS SAME"
dotnet test DungeonMasterXIV.sln --no-build -nologo 2>&1 | grep -E '^(Passed|Failed)!' | sed -E 's/Duration: [^-]*- //' | sort
```

Expected:
- `WARNINGS SAME`.
- Three test lines with Failed 0. `DungeonMasterXIV.Tests.dll` and `DungeonMasterXIV.Relay.Tests.dll` are unchanged from `pr7-base.txt`. `DungeonMasterXIV.Release.Tests.dll` shows Total and Passed higher by exactly 26, with Skipped 0.

- [ ] **Step 10: Commit, push, PR**

```bash
git add -A tools/DungeonMasterXIV.Release.Tests docs/superpowers/specs
git commit -F - <<'EOF'
test(release): nothing cites the retired agent system; retire the CommentPass tool

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
git push -u origin chore/dead-reference-guard
gh pr create --title "test(release): nothing cites the retired agent system (7/7)" --body-file "$SCRATCH/pr7-body.md"
```

The body covers:
- what the guard catches and what it leaves alone;
- the Step 7 probe output;
- the Step 9 counts;
- the tool deletion;
- the two doc edits;
- the closing line `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

Wait for the merge.
