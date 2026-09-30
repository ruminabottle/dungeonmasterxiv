# Smoke Tests Only Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut the three test projects down to 7 smoke tests, delete the size-gate and CommentPass tools, and trim the build-guard comments. Product code does not change.

**Architecture:** Deletion first. Every test file except the 7 kept ones goes, along with the `Sizes` and `CommentPass` tools. Helpers the kept files still need are restored from `main` by following the compile errors. Then each kept file is cut to one test, helpers with no remaining caller are pruned, comments are shortened, and the two build guards are probed to show they still fail a bad build. Spec, plan and change land together in one PR from `docs/smoke-tests-only`.

**Tech Stack:** C# / .NET SDK 10.0.400, xUnit 2.9.2, MSBuild, git, `gh`.

**Spec:** `docs/superpowers/specs/2026-09-30-smoke-tests-only-design.md`

## Global Constraints

- Product code is unchanged: `git diff --stat fc85981 -- src Windows Plugin.cs Services Net Data deploy tools/DungeonMasterXIV.Release` must be empty. The one exception is removing the `InternalsVisibleTo` line in `src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj`, and only if no kept plugin test uses an internal member.
- The kept tests are exactly these, one per file:

  | File | Kept test |
  |---|---|
  | `tests/DungeonMasterXIV.Tests/JoinOverASocketTests.cs` | `AJoinCompletesAcrossARealSocket` |
  | `tests/DungeonMasterXIV.Tests/BaseChatReachesEveryMemberTests.cs` | `AMessageFromOneMemberReachesADifferentMember` |
  | `tests/DungeonMasterXIV.Tests/ARollShowsItsDiceAndACorrectTotalTests.cs` | `FourSixSidedDiceAndAModifierTotalWhatTheTestItselfComputes` |
  | `tests/DungeonMasterXIV.Tests/EndingASessionAnnouncesItTests.cs` | `EndingTheSessionTellsAnAdmittedParticipant` |
  | `tests/DungeonMasterXIV.Tests/AnExportNamesNobodyTests.cs` | `TheCampaignIdDoesNotAppearInAnExport` |
  | `tests/DungeonMasterXIV.Relay.Tests/RelayRouterTests.cs` | `APayloadReachesTheOtherMembersOfItsSession` |
  | `tools/DungeonMasterXIV.Release.Tests/ManifestMatchesTheBuiltPluginTests.cs` | `TheManifestVersionIsTheVersionOfTheAssemblyItLinksTo` |

- The final `dotnet test DungeonMasterXIV.sln` gives Total 5 / 1 / 1 for `DungeonMasterXIV.Tests.dll`, `DungeonMasterXIV.Relay.Tests.dll` and `DungeonMasterXIV.Release.Tests.dll`, with Failed 0 and Skipped 0. Read the Total and Skipped fields, never the "Passed!" word.
- No new build warnings. Every warning at head, with line and column stripped, must already appear in the base run. Warnings from deleted files may disappear.
- The logic of the TLS fence (`NoTlsValidationBypass` in `Directory.Build.targets`) and of the release-tag targets (`ReleaseTag…` in `DungeonMasterXIV.csproj`) stays unchanged: same conditions, same `Text`. Only their XML comments shrink, to one or two lines per block.
- Commit messages go through `git commit -F -` with a quoted heredoc, ending with `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`.
- The PR body ends with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- Work on branch `docs/smoke-tests-only`, which already holds the spec. Base for every comparison: `fc85981`.
- Scratch directory for every task, since each task starts in a fresh shell: `S=/Users/ramonmunoz/repositories/dungeonmasterxiv-1/.superpowers/sdd/2026-09-30-smoke-tests-only/scratch`. It is git-ignored. Define it at the start of each task.

## Review Focus

1. **A kept test that became vacuous.** Removing its helpers or setup could leave a test that asserts nothing real. Each kept test must still exercise its path end to end, and the reviewer reads each one against the spec row it proves.
2. **A helper deleted while a kept test still reached it through reflection or file reads.** The build would not catch that, so the test run is the check. Task 2's final test run must show all 7 passing.
3. **A guard whose comment was trimmed and whose logic moved.** Task 3 probes both guards on the final tree.
4. **Silent solution drift.** A project dropped from the `.sln`, or a stale `ProjectReference`, can make `dotnet test` run fewer assemblies. The per-assembly Totals in the final check catch this.
5. **Dead references surviving in kept files.** A kept file may still say `BUG-n` or name a role. Task 2 rewrites these, and its check greps for them.

---

### Task 1: Delete the tools and every non-kept test file; restore the helpers the kept files need

**Files:**
- Delete: `tools/DungeonMasterXIV.Sizes/`, `tools/DungeonMasterXIV.CommentPass/`, `tools/DungeonMasterXIV.CommentPass.Tests/`, `tools/DungeonMasterXIV.Release.Tests/size-gate-baseline.txt`, `tests/DungeonMasterXIV.Tests/ClockFormsFixture.txt`
- Delete: every `.cs` under `tests/DungeonMasterXIV.Tests`, `tests/DungeonMasterXIV.Relay.Tests` and `tools/DungeonMasterXIV.Release.Tests` except the 7 kept files and the helpers restored in Step 4
- Modify: `tools/DungeonMasterXIV.Release.Tests/DungeonMasterXIV.Release.Tests.csproj` (remove the `Sizes` `ProjectReference`)

**Interfaces:**
- Produces: a tree that builds, whose three test assemblies contain only the 7 kept files (still holding all their original tests) plus the helpers they compile against; and `$S/base.txt`, the normalized base warnings and test totals.

- [ ] **Step 1: Branch state and the base snapshot**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
git switch docs/smoke-tests-only && git status --short          # expect clean
S=/Users/ramonmunoz/repositories/dungeonmasterxiv-1/.superpowers/sdd/2026-09-30-smoke-tests-only/scratch; mkdir -p "$S"
git stash list | head -1                                          # expect nothing relevant
dotnet build DungeonMasterXIV.sln --no-incremental -nologo > "$S/base-build.txt" 2>&1; echo "build exit=$?"
dotnet test DungeonMasterXIV.sln --no-build -nologo > "$S/base-test.txt" 2>&1; echo "test exit=$?"
grep -E ': (warning|error) [A-Z]+[0-9]+' "$S/base-build.txt" | sed -E "s#$PWD/##; s/\([0-9]+,[0-9]+\)//; s/ \[[^]]*\]$//" | sort -u > "$S/base-warnings.txt"
grep -E '^(Passed|Failed)!' "$S/base-test.txt"
```

Expected: both exits 0, and three result lines at the current totals (about 1560 / 111 / 339). The `docs/smoke-tests-only` branch holds only docs on top of `fc85981`, so this equals the `main` base. If the test run fails once with no code change, rerun it once and record both runs. A known flaky case exists.

- [ ] **Step 2: Delete the tools and the non-kept files**

```bash
git rm -r -q tools/DungeonMasterXIV.Sizes tools/DungeonMasterXIV.CommentPass tools/DungeonMasterXIV.CommentPass.Tests \
  tools/DungeonMasterXIV.Release.Tests/size-gate-baseline.txt tests/DungeonMasterXIV.Tests/ClockFormsFixture.txt
KEEP='^(tests/DungeonMasterXIV.Tests/(JoinOverASocketTests|BaseChatReachesEveryMemberTests|ARollShowsItsDiceAndACorrectTotalTests|EndingASessionAnnouncesItTests|AnExportNamesNobodyTests)\.cs|tests/DungeonMasterXIV.Relay.Tests/RelayRouterTests\.cs|tools/DungeonMasterXIV.Release.Tests/ManifestMatchesTheBuiltPluginTests\.cs)$'
git ls-files 'tests/*.cs' 'tools/DungeonMasterXIV.Release.Tests/*.cs' | grep -vE "$KEEP" | xargs git rm -q
git ls-files 'tests/*.cs' 'tools/DungeonMasterXIV.Release.Tests/*.cs'   # expect exactly the 7 kept files
rm -rf tools/DungeonMasterXIV.Sizes tools/DungeonMasterXIV.CommentPass tools/DungeonMasterXIV.CommentPass.Tests   # untracked bin/obj
```

- [ ] **Step 3: Drop the Sizes reference**

In `tools/DungeonMasterXIV.Release.Tests/DungeonMasterXIV.Release.Tests.csproj`, delete this line:

```xml
    <ProjectReference Include="..\DungeonMasterXIV.Sizes\DungeonMasterXIV.Sizes.csproj" />
```

If the XML comment above that `ItemGroup` mentions `Sizes` or the size gate, cut the comment to the one line that still applies ("Its own project so the plugin's test assembly keeps no Dalamud reference.").

- [ ] **Step 4: Restore the helpers the kept files compile against**

Loop until the build succeeds:

```bash
dotnet build DungeonMasterXIV.sln -nologo 2>&1 | grep -E 'error CS(0246|0103|0117|1061)' | grep -oE "'[A-Za-z_][A-Za-z0-9_]*'" | sort -u
```

For each missing name `X` the build reports, find the deleted file that declared it and restore it from `main`:

```bash
git grep -lE "(class|record|struct|interface|enum) X\b" fc85981 -- tests tools/DungeonMasterXIV.Release.Tests | sed 's/^fc85981://'
git checkout fc85981 -- <that path>
```

Restore only declaring files: a helper that is itself a test class (it contains `[Fact]` or `[Theory]`) should not be needed. If one is, report it rather than restoring it. Repeat until:

```bash
dotnet build DungeonMasterXIV.sln -nologo 2>&1 | tail -3   # expect "Build succeeded" and 0 errors
```

- [ ] **Step 5: Run the tests; everything in the kept files still passes**

```bash
dotnet test DungeonMasterXIV.sln --no-build -nologo 2>&1 | grep -E '^(Passed|Failed)!'
```

Expected: three lines, Failed 0, Skipped 0. The totals are the tests the 7 files hold right now. That is about 34 plugin tests (4 + 4 + 10 + 4 + 12 methods), 9 relay and 4 release; theory rows can raise the counts. If a test fails because it read a deleted fixture or helper through a path rather than a type, restore that file too and note it.

- [ ] **Step 6: Commit**

```bash
git add -A tests tools
git commit -F - <<'EOF'
test: keep only the files that hold the smoke tests; drop the size gate and CommentPass

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 2: Cut each kept file to its smoke test, prune helpers, shorten comments

**Files:**
- Modify: the 7 kept files, and the helpers restored in Task 1
- Delete: restored helpers that no kept test uses after the cut
- Maybe modify: `src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj` (remove `InternalsVisibleTo` only if unused)

**Interfaces:**
- Consumes: Task 1's tree.
- Produces: the final test tree, with 7 tests passing.

- [ ] **Step 1: Delete every test method except the kept one in each file**

In each of the 7 files, delete every `[Fact]`/`[Theory]` method except the one named in Global Constraints. Also delete any `[InlineData]`/`MemberData` sources used only by deleted methods, and any private helper method or field in that file that no remaining method uses. Keep the class name and namespace.

- [ ] **Step 2: Build and prune restored helpers with no remaining caller**

```bash
dotnet build DungeonMasterXIV.sln -nologo 2>&1 | tail -2        # expect success
```

For each helper file restored in Task 1, try deleting it and rebuilding:

```bash
git rm -q <helper>; dotnet build DungeonMasterXIV.sln -nologo 2>&1 | grep -qE ' error ' && git checkout HEAD -- <helper>
```

A helper stays only if the build fails without it. Inside each surviving helper, delete members no kept test reaches. The build tells you: remove a member, rebuild, and restore it if the build breaks.

- [ ] **Step 3: InternalsVisibleTo**

Temporarily remove `<InternalsVisibleTo Include="DungeonMasterXIV.Tests" />` from `src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj` and rebuild. If the build succeeds, leave it removed. If it fails, restore the line exactly: `git checkout fc85981 -- src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj`.

- [ ] **Step 4: Shorten comments in the kept files and helpers**

- Each kept test class gets a one-line `/// <summary>` stating the path it proves, taken from the spec's §1 table. For example, `/// <summary>A host and a joiner connect through a relay; the joiner is admitted and sees the roster.</summary>`.
- Delete `<remarks>` blocks and multi-paragraph narrative. Keep an inline `//` comment only where a step would otherwise be unclear, one line each.
- Helpers get at most a one-line summary.
- No `BUG-n`, `DMXENG-n`, `SQ-n`, `T-n`, `PRD-n`, role name or `.claude/` may remain. Check with:

```bash
git grep -nEi '(^|[^A-Za-z0-9])(BUG|DMXENG|DMXHUM|SQ|PRD|E|T)-[0-9]+|spec owner|deployment manager|product owner|engineering lead|code reviewer|breakfix|\bqa-[0-9]|\.claude/' -- tests tools/DungeonMasterXIV.Release.Tests
```

Expected: no output.

- [ ] **Step 5: Run the tests**

```bash
dotnet test DungeonMasterXIV.sln -nologo 2>&1 | grep -E '^(Passed|Failed)!'
```

Expected:

```
Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, … - DungeonMasterXIV.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, … - DungeonMasterXIV.Relay.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, … - DungeonMasterXIV.Release.Tests.dll (net10.0)
```

The three lines may appear in any order.

- [ ] **Step 6: Commit**

```bash
git add -A tests tools src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj
git commit -F - <<'EOF'
test: one smoke test per path; prune helpers and prose

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 3: Shorten the build-guard comments and prove both guards still bite

**Files:**
- Modify: `Directory.Build.targets` (XML comments around `NoTlsValidationBypass` and its property group)
- Modify: `DungeonMasterXIV.csproj` (XML comments around the `ReleaseTag…` targets and properties)

**Interfaces:**
- Consumes: Task 2's tree.
- Produces: the final build files.

- [ ] **Step 1: Shorten the comments**

- In `Directory.Build.targets`, cut each `<!-- … -->` block to one or two lines saying what the next element does. For example, `<!-- Only DungeonMasterXIV.Relay.Tests may set a TLS certificate validation callback; any other project fails the build. -->`.
- Drop mentions of deleted test files (`ConnectionRolesAreASetTests.cs`, `EveryFieldTheManifestCarriesIsComparedTests.cs` and others).
- Do the same in `DungeonMasterXIV.csproj` for the release-tag comments. For example, `<!-- The release tag is the only author of the version: -p:ReleaseTag=v1.2.3. -->`.
- Leave every element, attribute, `Condition` and `Text` exactly as it is.

- [ ] **Step 2: Confirm only comments changed**

```bash
for f in Directory.Build.targets DungeonMasterXIV.csproj; do
  diff <(git show fc85981:$f | perl -0pe 's/<!--.*?-->//gs' | sed 's/[[:space:]]*$//' | grep -v '^$') \
       <(perl -0pe 's/<!--.*?-->//gs' $f | sed 's/[[:space:]]*$//' | grep -v '^$') && echo "$f: only comments changed"
done
```

Expected: both files print "only comments changed".

- [ ] **Step 3: Probe the TLS fence**

```bash
F=src/DungeonMasterXIV.Core/Net/RelayLink.cs
printf '\nstatic class FenceProbe { static System.Net.Security.RemoteCertificateValidationCallback P = (a, b, c, d) => true; }\n' >> "$F"
tail -1 "$F"                                                         # expect the probe line
dotnet build src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj -nologo 2>&1 | grep -iE 'error.*(tls|certificate|validation)' | head -2   # expect the fence's error
git checkout -- "$F" && git status --short "$F"                      # expect nothing
```

If `RelayLink.cs` doesn't exist, use any other `.cs` file under `src/DungeonMasterXIV.Core/Net`.

- [ ] **Step 4: Probe the release-tag check**

```bash
dotnet build DungeonMasterXIV.csproj -nologo -p:ReleaseTag=V1 2>&1 | grep -E 'must begin with a lowercase' | head -1   # expect the message
```

- [ ] **Step 5: Commit**

```bash
git add Directory.Build.targets DungeonMasterXIV.csproj
git commit -F - <<'EOF'
chore(build): shorten the TLS-fence and release-tag comments

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 4: Mark the specs, run the final checks, open the PR

**Files:**
- Modify: `docs/superpowers/specs/2026-09-30-comment-id-rewrite-design.md` (Status line)
- Modify: `docs/superpowers/specs/2026-09-30-smoke-tests-only-design.md` (Status line)

- [ ] **Step 1: Spec status lines**

- In `2026-09-30-comment-id-rewrite-design.md`, replace the `**Status:**` line with: `**Status:** Superseded after PR 6a (tests A–S); Tasks 10–11 cancelled by [2026-09-30-smoke-tests-only-design.md](2026-09-30-smoke-tests-only-design.md)`
- In `2026-09-30-smoke-tests-only-design.md`, change `**Status:** Approved for planning` to `**Status:** Implemented 2026-09-30`.

- [ ] **Step 2: Final checks**

```bash
git diff --stat fc85981 -- src Windows Plugin.cs Services Net Data deploy tools/DungeonMasterXIV.Release   # expect empty, or only the Core csproj InternalsVisibleTo line
dotnet build DungeonMasterXIV.sln --no-incremental -nologo > "$S/head-build.txt" 2>&1; echo "build exit=$?"
grep -E ': (warning|error) [A-Z]+[0-9]+' "$S/head-build.txt" | sed -E "s#$PWD/##; s/\([0-9]+,[0-9]+\)//; s/ \[[^]]*\]$//" | sort -u > "$S/head-warnings.txt"
comm -13 "$S/base-warnings.txt" "$S/head-warnings.txt"                # expect no output: no new warnings
dotnet test DungeonMasterXIV.sln --no-build -nologo 2>&1 | grep -E '^(Passed|Failed)!'   # expect 5 / 1 / 1, Failed 0, Skipped 0
git ls-files 'tests/*.cs' 'tools/DungeonMasterXIV.Release.Tests/*.cs' | wc -l                # report the count
```

- [ ] **Step 3: Commit, push, PR**

```bash
git add docs/superpowers/specs
git commit -F - <<'EOF'
docs(spec): smoke tests only implemented; comment plan superseded

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
git push -u origin docs/smoke-tests-only
gh pr create --title "test: cut the suite to smoke tests; drop the size gate" --body-file "$S/pr-body.md"
```

The body covers:
- the spec and plan paths;
- the before/after table: files, lines and tests per project;
- the 7 kept tests;
- the helpers kept, and why;
- whether `InternalsVisibleTo` stayed;
- the probe outputs from Task 3;
- the final check outputs from Step 2;
- the closing line `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

Stop after the PR is open. A human merges it.
