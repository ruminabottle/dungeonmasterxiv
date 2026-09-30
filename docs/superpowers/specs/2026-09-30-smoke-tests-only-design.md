# Cut the test suite to smoke tests

**Date:** 2026-09-30
**Status:** Implemented 2026-09-30
**Supersedes:** Tasks 10 and 11 of [2026-09-30-comment-id-rewrite-design.md](2026-09-30-comment-id-rewrite-design.md)

## Goal

Before the first major release, the repository carries only smoke tests: one test per path a user or a release depends on. Coverage comes back feature by feature, as each one stabilises.

The reasons:

- **Context.** The suite is larger than the product: about 40,000 test lines against about 20,000 product lines, and a third of the plugin test lines are comments. An agent changing product code reads the tests around it, which crowds out the product code itself.
- **False constraints.** Tests that pin how code is written fail on changes no user would notice. This covers source-text scans, reflection checks, and the size gate with its baseline.
- **Premature locking.** Requirements are still moving. Every test that fixes today's behaviour becomes a conflict the day it changes.
- **Agent-era evidence.** Many tests exist as proof for a reviewer role that no longer exists.

Success means:

- `dotnet test` runs the smoke set, about 7 tests: 5 plugin, 1 relay, 1 release. All pass and none is skipped.
- Product code is unchanged.
- The two build-time guards still fail a build that breaks them.

## Current state (measured 2026-09-30, main at `fc85981`)

| Project | Files | C# lines | Tests |
|---|---|---|---|
| `tests/DungeonMasterXIV.Tests` | 175 | 29,827 | 1,560 |
| `tests/DungeonMasterXIV.Relay.Tests` | 29 | 3,612 | 111 (1 skipped) |
| `tools/DungeonMasterXIV.Release.Tests` | 43 | 6,185 | 339 |
| `tools/DungeonMasterXIV.Sizes` (size-gate tool) | 10 | 945 | — |
| `tools/DungeonMasterXIV.CommentPass` and `.Tests` (throwaway) | 13 | 663 | 89 |

- There is no CI. Nothing outside the repository runs these tests.
- `tools/DungeonMasterXIV.Release` builds the release manifest and assets. Releases v0.1.3–v0.1.6 went out through it.
- The build-time guards are not tests:
  - `NoTlsValidationBypass` in `Directory.Build.targets` fails any project except `DungeonMasterXIV.Relay.Tests` that sets a certificate validation callback.
  - The `ReleaseTag…` targets in `DungeonMasterXIV.csproj` fail a malformed release tag.
- `src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj` has `InternalsVisibleTo` for `DungeonMasterXIV.Tests`.

## Decisions

| Question | Decision |
|---|---|
| How far to cut | Smoke tests only |
| Size gate and `Sizes` tool | Delete |
| TLS fence and release-tag checks | Keep, with comments trimmed |
| Building the smoke set | Cut down existing tests; do not write new ones |
| Test projects | Keep all three; their dependency boundaries differ |
| Comment-rewrite plan Tasks 10–11 | Cancelled; the dead-reference guard is dropped |

## §1 The smoke set

| # | Path | Project | Kept from |
|---|---|---|---|
| 1 | A host and a joiner connect through a relay; the joiner is admitted and sees the roster | Tests | `JoinOverASocketTests.cs` |
| 2 | A chat message reaches every member | Tests | `BaseChatReachesEveryMemberTests.cs` |
| 3 | A roll such as `2d6+1` evaluates to the right total with scripted dice | Tests | `ARollShowsItsDiceAndACorrectTotalTests.cs` |
| 4 | Ending a session announces it to members | Tests | `EndingASessionAnnouncesItTests.cs` |
| 7 | An exported session log names nobody | Tests | `AnExportNamesNobodyTests.cs` |
| 5 | The relay routes a frame between two connections | Relay.Tests | `RelayRouterTests.cs` |
| 6 | The release manifest matches the built plugin | Release.Tests | `ManifestMatchesTheBuiltPluginTests.cs` |

Each kept file is cut to the one test that proves its path. If a path truly needs two tests to be proven, keep two and record why in the PR. Helpers stay only while a kept test still compiles against them. Each file keeps a one-line summary of the rule it pins, and inline comments stay only where a step would otherwise be unclear. A dead reference in a kept file (`BUG-n`, `DMXENG-n`, a role name) is rewritten by the comment spec's §1 rules.

## §2 What is removed

- Every other test file and helper in the three test projects, including `ContainerSmokeTests.cs`.
- `tools/DungeonMasterXIV.Sizes`, every size-gate test and `size-gate-baseline.txt`.
- `tools/DungeonMasterXIV.CommentPass` and `tools/DungeonMasterXIV.CommentPass.Tests`.
- Project references and `.sln` entries that only served removed code, such as `Release.Tests` → `Sizes`.
- The comment blocks around the TLS fence and the release-tag targets, cut to one or two lines each. Their `Text` messages and conditions stay unchanged.

**Stays unchanged:**
- product code (`src/`, `Windows/`, `Plugin.cs`, `Services/`, `Net/`, `Data/`);
- `tools/DungeonMasterXIV.Release`;
- `deploy/`;
- the area specs (only the paused comment spec's status changes, per §3).

`InternalsVisibleTo` stays while any kept plugin test uses an internal member. If none does, it goes too.

**Follow-ups outside this spec:** trimming the long remarks in product code, and rebuilding coverage as features stabilise.

## §3 Landing and verification

**One PR, in this order:**
1. Remove the tools and projects that go entirely.
2. Remove the non-smoke test files.
3. Cut the kept files to their smoke tests.
4. Remove helpers with no remaining caller.
5. Trim the build-file comments.
6. Update the paused comment spec: set its status to "Superseded after PR 6a (tests A–S); Tasks 10–11 cancelled", and link this spec.

**Checks:**
- **Build:** `dotnet build DungeonMasterXIV.sln --no-incremental` succeeds. Its warnings match a base run from `main`, compared with line numbers stripped. Any new warning is fixed or explained in the PR.
- **Tests:** `dotnet test DungeonMasterXIV.sln` reports, per assembly, a Total equal to the kept count the PR lists, with Failed 0 and Skipped 0. Read these from the Total and Skipped fields, not from the "Passed!" line. The expected counts are:
  - `DungeonMasterXIV.Tests.dll` 5
  - `DungeonMasterXIV.Relay.Tests.dll` 1
  - `DungeonMasterXIV.Release.Tests.dll` 1

  A higher count is allowed only under §1's two-test exception.
- **Product untouched:** `git diff --stat main -- src Windows Plugin.cs Services Net Data deploy tools/DungeonMasterXIV.Release` is empty. The one exception is removing `InternalsVisibleTo` under the rule in §2.
- **TLS fence still bites:** add a `RemoteCertificateValidationCallback` assignment to a Core source file, confirm it is in the file, and confirm `dotnet build` fails naming the fence. Then restore with `git checkout`.
- **Release-tag check still bites:** `dotnet build DungeonMasterXIV.csproj -p:ReleaseTag=V1` fails with the lowercase-`v` message.

## Out of scope

- Product code and its comments.
- New tests beyond the seven.
- CI.
