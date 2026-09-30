# Rewrite the dead IDs out of code comments

**Date:** 2026-09-30
**Status:** Approved for planning
**Scope:** Sub-project 2 of 2 from [2026-09-29-retire-agent-team-design.md](2026-09-29-retire-agent-team-design.md). Sub-project 1 (the switch to specs) landed in #250.

## Goal

Comments and string literals in the code point only at things that still exist. After the switch, the
ticket, bug and ruling IDs they cite (`BUG-n`, `DMXENG-n`, `SQ-n`, `E-n`, `PRD-n`) resolve only inside
the archive, and the roles they name (Spec Owner, Deployment Manager and others) no longer exist.

Success means:

- No tracked file outside `docs/` carries a dead ID, an agent-role name or a `.claude/` path, and a
  release test keeps it that way.
- Where a comment used history as its reason ("the collision BUG-43 was"), it now states the rule or
  the failure itself.
- Spec IDs (`R-`, `A-`, `D-`) stay, so code still traces to `docs/superpowers/specs/` by one grep.
- Compiled code is unchanged: only comments, string literals and non-code lines differ, and the
  build warnings and test counts are identical before and after every PR.

## Current state (measured 2026-09-30, main at `83c60b2`)

The first spec estimated about 866 IDs across 262 files. That count covered only part of the
pattern set. The full picture:

| Found | Occurrences | Files | Resolves to |
|---|---|---|---|
| `R-n.n` | 1,016 | 257 | live specs (kept) |
| `A-n.n` | 853 | 235 | live specs (kept) |
| `D-n` | 428 | 178 | live specs (kept) |
| `BUG-n` | 584 | 216 | archive `claude/team/bugs/` |
| `DMXENG-n` | 218 | 115 | archive `claude/team/tickets/`, Jira |
| `SQ-n` | 70 | 39 | archive `claude/team/product/prd/SQ-LEDGER.md` |
| `PRD-n` | 18 | 17 | renamed to area specs |
| `E-n` | 8 | 7 | retired |
| `T-n` (pre-Jira ticket IDs, e.g. `T-37`) | 15 | ~10 | archive `claude/team/tickets/` |
| Role names (Spec Owner 24, Deployment Manager 20, the HUMAN 14, QA 8, breakfix 2) | 68 | 57 | roles retired |
| More role names (Product Owner 21, Code Reviewer / code reviewer 13, Engineering Lead 3) | 37 | — | roles retired |
| Archived documents (`engineering-standards.md` 8, `brief.md` 1) | 9 | — | archive |
| "Decision n" | 7 | 4 | product-overview Session panel item n |
| `.claude/` paths | 3 | 3 | archive |
| PR numbers `#nn` / commit hashes | 82 / 10 | 52 / 9 | GitHub, git (live) |

- In `.cs` files, the dead IDs, role names, `.claude` paths, PR numbers and the word "ticket" come to 1,081 occurrences together.
- About 88 ID hits sit inside string literals rather than comments. Most are test assertion
  messages. A few are in product code (`MissedMessages.cs`, `MessageLine.cs`, `RelayApp.cs`).
  `ShippedCopyCorpus` scans product string literals.
- Non-`.cs` files with dead references: `DungeonMasterXIV.csproj` (9), `deploy/Dockerfile` (6),
  `Directory.Build.targets` (4), `size-gate-baseline.txt` (2), the Core and Sizes `.csproj` (2 each),
  the Relay `.csproj`, `.gitignore`, `.dockerignore` and `tests/DungeonMasterXIV.Tests/ClockFormsFixture.txt` (1 each).
- No build step, gate or test parses these IDs.
- The size gate counts lines inside member bodies and whole files. Doc comments above a declaration
  sit outside a member's span.
- Roslyn is already a dependency, in `tools/DungeonMasterXIV.Sizes`.

## Decisions

| Question | Decision |
|---|---|
| What a comment may point at | Spec IDs stay. Dead IDs go. |
| Role language | Goes. A ruling keeps its content and loses its author. |
| PR numbers and commit hashes | Kept only where they anchor a checkable fact. Otherwise restated as the rule. |
| String literals | Same rules as comments. |
| Where a lost rule is recovered from | The code, then the specs, then the archive and PR bodies. |
| How the pass runs | One PR per area, a mechanical pre-pass, then judgment per file. |
| Keeping it clean | A permanent release test, landed last. |

## §1 Rewrite rules

**Scope:** every tracked file outside `docs/`.

**Stays:**

- `R-n.n`, `A-n.n`, `D-n`.
- A PR or commit reference that anchors a fact a reader can check, for example
  "Measured at `2719162`: …".
- Emphatic ALL-CAPS headings and the rest of the house voice. Only IDs, roles and dead paths change.

**Goes:**

| Found | Rewrite |
|---|---|
| A pure tag: `(BUG-87)`, `(DMXENG-105)`, an ID in a citation list | Delete it and tidy the punctuation. |
| History as the reason: "which is the collision BUG-43 was" | State the failure: "which is the collision where one arm swallows the other's frame". |
| A ruling with provenance: "The Spec Owner's ruling (SQ-84) is that X", "ruled by the HUMAN" | "X". If a spec holds the ruling, cite its `R-`/`A-`/`D-` ID. |
| Process narrative: "DMXENG-69 gave the rule an owner", "a bug-lane ticket held on it", "DMXENG-107's brief warned me" | Delete it. Keep any fact inside it. |
| `PRD-n` | The area spec name, or the `R-`/`A-` ID alone. |
| "Decision n" | "product-overview Session panel item n". |
| A `.claude/…` path | Write the fact into the comment, or drop it if the path only pointed at a backlog. |
| A citation of an archived document: "in `engineering-standards.md`", "the brief" | The rule it cited, or its spec ID if a spec now holds it. |
| "PR #89's body is authoritative", "since #120" | The rule that PR established. |

**Recovering a rule.** When a comment's reason is only its ID, read, in order: the surrounding code,
the area spec, then the archive at `~/archive/dungeonmasterxiv-agent-team-2026-09-29/claude/team/`
(`bugs/BUG-n.md`, `tickets/`, `product/prd/SQ-LEDGER.md`, `IN-GAME-BACKLOG.md`) and the PR body
(`gh pr view n`).

**Guard rails:**

- Never change what a comment claims. A comment that needs its ticket to make sense is rewritten to
  state the rule, not deleted.
- Never guess. If no source settles the rule, keep the claim and drop only the ID.
- A rewrite is no longer than the text it replaces, so file and member line counts do not grow
  against the size gate.

## §2 Execution

Each PR is a branch off `main` that merges before the next one starts, so every PR measures against
a known clean base.

| PR | Area | Files |
|---|---|---|
| 1 | `tools/` (Release, Release.Tests, Sizes) and `size-gate-baseline.txt` | ~51 |
| 2 | `src/DungeonMasterXIV.Relay`, `tests/DungeonMasterXIV.Relay.Tests`, `deploy/`, `.dockerignore` | ~22 |
| 3 | `src/DungeonMasterXIV.Core` except `Net/` (Data, Chat, Rolls, Campaigns), Core `.csproj` | ~24 |
| 4 | `src/DungeonMasterXIV.Core/Net` | ~66 |
| 5 | `Windows/`, `Plugin.cs`, root `.csproj`, `Directory.Build.targets`, `.gitignore` | ~15 |
| 6 | `tests/DungeonMasterXIV.Tests` and its fixture | ~132 |
| 7 | The guard, closing edits | new test |

`tools/` goes first because it holds the checking machinery. A mangled tool comment shows up before
that machinery is trusted anywhere else. PR 6 splits alphabetically into 6a and 6b if its diff
passes about 1,500 lines.

Inside each PR:

1. **Mechanical pre-pass.** A script deletes only standalone parenthetical tags matching
   `\s?\((BUG|DMXENG|SQ|E|T)-\d+(, (BUG|DMXENG|SQ|E|T)-\d+)*\)` and dead IDs that open or close a
   citation list inside parentheses, such as `(R-1.3h, BUG-115)`.
   Its diff is its own commit, so review can skim it.
2. **Judgment pass.** Every remaining hit is rewritten by the §1 rules, one file group at a time.
3. **Checks** (§3), review, merge.

## §3 Verification

**Per PR, against that PR's base on `main`:**

1. **The code did not change.** `tools/DungeonMasterXIV.CommentPass`, a small Roslyn console tool
   added in PR 1, parses each changed `.cs` file at base and head and compares token streams with
   trivia ignored.
   - A difference in any token other than a string, char or interpolated-string-text literal fails.
   - Every changed literal is printed, product-code ones marked, and the list goes in the PR
     description.
   - Non-`.cs` files: with XML comments stripped (`.csproj`, `.targets`) or `#` lines stripped
     (`Dockerfile`, `.gitignore`, `.dockerignore`, `size-gate-baseline.txt`), the rest matches
     exactly.
2. **Nothing dead remains in the area.** The guard's pattern set over the PR's paths finds nothing.
3. **Same build.** `dotnet build` reports the same errors and the same warnings as base. Rewritten
   `///` XML that breaks raises CS1570 and shows here.
4. **Same tests.** `dotnet test` reports the same Total, Passed and Skipped as base. The numbers are
   compared, not the "Passed!" line. The size gate runs in this set.

**The guard (PR 7).** A test in `tools/DungeonMasterXIV.Release.Tests`:

- Scans every file `git ls-files` lists outside `docs/`.
- Fails on `BUG-n`, `DMXENG-n`, `SQ-n`, `E-n`, `T-n`, `PRD-n`; the role names Spec Owner,
  Deployment Manager, Product Owner, Engineering Lead and Code Reviewer (either capitalisation),
  `[Bb]reakfix` and `the HUMAN`; `.claude/`; and the archived documents `engineering-standards`,
  `product-directives` and `brief.md`.
- Does not police `R-`/`A-`/`D-` IDs, PR or commit anchors, or the word "ticket".
- Names each file, line and match on failure, and says to cite the spec ID instead.
- Builds its patterns from fragments so its own source does not match.
- Is shown to fail: a probe seeds `BUG-1` into a comment, confirms the seed is in the file, sees the
  test go red, and restores the file with `git checkout`.

**Closing edits (PR 7):**

- Drop the "Decision n" line from `docs/superpowers/specs/README.md`.
- Delete `tools/DungeonMasterXIV.CommentPass`.
- Mark this spec Implemented.

## Out of scope

- Spec IDs in comments, and any change to the specs themselves beyond the README line.
- Comment style, length or accuracy beyond what the rewrite rules require.
- Product-code and test behaviour.
- Commit messages, PR history and branch names.
