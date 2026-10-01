# Replace product comments with fresh one-line summaries

**Date:** 2026-09-30
**Status:** Implemented 2026-09-30
**Follows:** [2026-09-30-smoke-tests-only-design.md](2026-09-30-smoke-tests-only-design.md) (landed in #258)

## Goal

Product code carries only what an agent needs in order to navigate it: one fresh line per type, saying what the type is for. Every existing comment goes.

The old comments are not rewritten, because they are part of the problem:

- **Volume.** 12,691 of the 23,168 lines in product C# are comments (55%), including 522 `<remarks>` essays.
- **Poison.** Many comments carry claims from the agent era: ruling history, stale statements, ticket narrative and role voice. A summary written while the old comment is in view inherits its claims. So the old text is removed before any new text is written, and new text is written from the code alone.

Success means:

- Product C# is at most about 2% comments: one `/// <summary>` line per type and nothing else.
- Compiled output is byte-identical before and after.
- Every summary is supported by the code it describes.

## Current state (measured 2026-09-30, main at `dfc4b25`)

- 205 product C# files: 23,168 lines, 12,691 comment lines, about 222 type declarations and about 960 public-member lines.
- Every product project sets `GenerateDocumentationFile` and suppresses CS1591, so a missing summary raises no warning.
- Build and config files with comment blocks: `DungeonMasterXIV.csproj` (9), `deploy/Dockerfile` (44), `.dockerignore` (24), and the Core, Relay, Release and three test `.csproj` files (1–4 each).
- The smoke tests (7 tests, 14 files) were already trimmed in #258.

## Decisions

| Question | Decision |
|---|---|
| How far | Near zero: summaries on types only |
| Where summaries come from | Written fresh from the stripped code, never from old comments |
| Spec IDs in code | Dropped; the specs are where rules live |
| Build and config files | One line per comment block |
| Proof that only comments changed | Deterministic, PDB-less builds give byte-identical DLLs |

## §1 Scope

**Stripped completely, then summarized:** every C# file under `src/`, `Windows/`, `Services/`, `Net/`, `Data/` and `tools/DungeonMasterXIV.Release/`, plus `Plugin.cs`.

- All `//`, `///` and `/* */` comments are removed, including existing summaries, `<remarks>`, inline notes, commented-out code and spec IDs.
- Preprocessor directives (`#if`, `#pragma`, `#region`, `#nullable`) stay. They are not comments.

**Each comment block cut to one line:** every `.csproj`, `deploy/Dockerfile` and `.dockerignore`.

- The line says what the next element or instruction does.
- The TLS-fence and release-tag comments in `Directory.Build.targets` and `DungeonMasterXIV.csproj` stay as #258 left them.
- Elements, attributes, conditions, `Text=` messages and Docker instructions do not change.

**Unchanged:**

- the smoke tests and their helpers;
- `docs/`;
- `Directory.Build.targets` and `Directory.Build.props`;
- every string literal, shipped copy included;
- all code.

## §2 The summary rule

Every type declaration gets exactly one line directly above it, `/// <summary>…</summary>`. This covers classes, records, structs, interfaces, enums and delegates, nested ones included.

- **Content:** what the type is for, in present tense, one sentence, and at most about 100 characters of text.
- **Source:** the type's own code and its uses, read from the stripped tree. The writer never opens the pre-strip version of a file: no `git show`/`git diff` of pre-strip content, and no archive.
- **Never:** spec or ticket IDs, history ("was", "used to", "since"), claims about tests, reviewers or roles, or anything the code does not show.
- **When the purpose isn't clear from the code:** describe what the type does, not why it exists. A plain accurate line beats a speculative one.

Members, parameters and statements get no comments.

## §3 Landing and verification

One PR, from branch `chore/fresh-summaries`. It holds the strip as its own commit, then the summaries, then the build and config file comments.

**Strip:**

- A throwaway Roslyn script, kept outside the repository and never committed, removes comment trivia from each file in §1. It leaves no blank-line residue: consecutive blank lines collapse to one, and a comment-only line leaves nothing.
- **Build identity.** Before the strip and after each commit, every product assembly is built with `-p:Deterministic=true -p:DebugType=none -p:ContinuousIntegrationBuild=true`. The assemblies are `DungeonMasterXIV.dll`, `DungeonMasterXIV.Core.dll`, `DungeonMasterXIV.Relay.dll` and `DungeonMasterXIV.Release.dll`. The SHA-256 of each DLL must equal the base. A mismatch means code changed; that file is found and fixed. This is sound only while no product code bakes line numbers into IL: there is no `CallerLineNumber`, `CallerFilePath` or `#line` today (checked at `dfc4b25`).

**Summaries:**

- Written folder by folder over the stripped tree.
- After each folder: a comment census over that folder, which must show exactly one summary line per type and no other comment line; plus the build identity check.

**Build and config files:**

- The comment-stripped content of each file is identical before and after: XML comments removed for `.csproj`, `#` lines removed for `Dockerfile` and `.dockerignore`.

**Final checks:**

- The whole-tree census reports comment lines of at most about 2% of product C# lines, and zero non-summary comment lines.
- `dotnet build DungeonMasterXIV.sln` has 0 warnings.
- `dotnet test` reports 5 / 1 / 1, Failed 0, Skipped 0.
- DLL hashes equal the base.

**Review:** a fresh reviewer reads every type next to its summary and flags any summary the code does not support, or any that breaks §2.

## Out of scope

- Renaming types or members to be more self-describing.
- Summaries on members.
- The smoke tests.
- The deferred minors from #258 (the TLS fence's `Text` and its Relay.Tests exemption).
