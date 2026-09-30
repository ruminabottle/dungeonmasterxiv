# Retire the agent team; move from PRDs to specs

**Date:** 2026-09-29
**Status:** Implemented 2026-09-30
**Scope:** Sub-project 1 of 2 ("the switch"). Sub-project 2, the code-comment rewrite, gets its own spec later.

## Goal

The project stops running as a multi-role agent team (deployment manager, spec owner, QA, feature and breakfix engineers, queues, board, Jira dispatch). Work from now on is one human plus a Superpowers session: brainstorm, write a spec in `docs/superpowers/specs/`, plan, implement.

Success means:

- The product knowledge in the PRDs, the brief and the product directives lives on as versioned specs in the main repo.
- The agent system is archived intact, outside the project, and nothing of it stays loaded into new sessions: no role skills, no role worktrees, no agent-era memory.
- Product code and tests are unchanged, and the build and test results are identical before and after.

## Current state (measured 2026-09-29, main at `ea669fa`)

- `.claude/` is gitignored by the outer repo and is **its own git repo with no remote**. It holds uncommitted changes (brief, directives, bugs 178/180, several handoffs).
- `.claude/skills/`: 9 role skills. `deployment-manager/` also holds `engineering-standards.md` (26,154 lines) and `product-directives.md` (2,386 lines).
- `.claude/team/product/`: `brief.md`, `prd/` (PRD-0, 1, 2, 3, 7, `README.md`, `SQ-LEDGER.md`), `policy/relay-service-policy.md`, `research/` (2 notes).
- `.claude/worktrees/`: 10 role worktrees registered with the outer repo (3 on detached HEADs), plus `sim/`, an empty plain directory. The nested repo's pending changes (52 `git status` entries) are what step 1 commits.
- The outer repo has 72 registered worktrees (most are prunable scratchpad checkouts) and 225 local branches not merged into `origin/main`.
- Nothing in the build, tests or tools reads `.claude/`. Three code comments mention it.
- Code comments carry about 866 requirement and ticket IDs across 262 files. These are **out of scope** here (see sub-project 2).

## Decisions

| Question | Decision |
|---|---|
| Existing PRDs and brief | Turn them into Superpowers specs, keeping the product and dropping the process |
| IDs in code comments | Rewrite them in a separate follow-up sub-project |
| The agent system | Archive, then remove from the project |
| Engineering standards | Drop them. Superpowers skills and the `tools/` gates replace them |
| Project memory | Archive it, then rebuild with only codebase and tooling facts |

## §1 Product specs

Location: `docs/superpowers/specs/`, versioned in the main repo. Area specs describe the current product, so they have stable, undated names. New change specs keep the Superpowers default `YYYY-MM-DD-<topic>-design.md` and cite the area specs.

| New spec | Built from |
|---|---|
| `product-overview.md` | `brief.md`, plus the out-of-scope list and cross-cutting standing directives from `product-directives.md` |
| `core-skeleton.md` | PRD-0 |
| `session-layer.md` | PRD-1, plus the directives and resolved escalations that bear on it |
| `rolls.md` | PRD-2, plus the relevant directives |
| `initiative.md` | PRD-3, plus the relevant directives |
| `distribution.md` | PRD-7, plus `policy/relay-service-policy.md` |
| `research/foundry-vtt-model.md`, `research/lightless-sync-client-server.md` | copied unchanged |

The root `RELAY-SERVICE-POLICY.md` stays where it is, since the README links to it.

**Keep:** requirement statements, acceptance criteria and product rulings that are still true. Requirement IDs (e.g. `R-1.7a`, `A-1.16a`) stay as section anchors, so existing code comments remain traceable until sub-project 2.

**Drop:** tiers, `SQ-LEDGER.md`, role routing, "who rules on what", ticket numbers and escalation history. A ruling keeps its content and loses its provenance.

**Conflicts:** where a PRD and a later directive disagree, the directive wins. Where it isn't clear whether a requirement still holds, spot-check it against the code. Anything that can't be settled is written into the spec as an explicit `Open question:` line, never guessed.

## §2 Archive and removal

Archive destination: `~/archive/dungeonmasterxiv-agent-team-2026-09-29/`.

0. **Nothing running.** List the running `claude` processes with their working directories, plus scheduled tasks and crons, and present them to the user. The user stops the role sessions. Nothing is killed by the implementer, and no step below starts until only the implementing session remains.
1. **Freeze.** In the nested `.claude` repo, commit all pending changes (`chore: final state before retiring the agent team`) and tag `agent-era-final`.
2. **Keep the code work safe.**
   - Keep all local branches. None are deleted.
   - Write a `git bundle` of every commit reachable from a `.claude/worktrees/*` HEAD that is on no local branch and not on `origin`. That covers at least the 3 detached-HEAD QA worktrees.
   - Then `git worktree remove` each `.claude/worktrees/*` entry and `git worktree prune` the stale scratchpad entries.
3. **Move.** Move `.claude/` (nested `.git`, `team/`, `skills/`, `roles/`, and the bundle) to the archive destination. Recreate `.claude/` in the project containing only `settings.local.json`.
4. **Outer repo tidy.** Replace the "multi-role team harness" comment block in `.gitignore`. `.claude/` stays ignored.

**Not touched:** Jira (DMXENG), GitHub PRs and remote branches. Open agent-era PRs are listed for the user to decide on.

**Undo:** move the archive back and `git worktree add` from the bundle or branches.

## §3 Memory

1. Copy the whole project memory directory (`~/.claude/projects/-Users-ramonmunoz-repositories-dungeonmasterxiv-1/memory/`) into the archive destination as `memory/`.
2. Clear the live directory and write back only facts about this codebase and its tooling, reworded to remove role, queue and Jira references. Starting set:
   - `dotnet test` prints `Passed!` on a truncated run or with a skipped gate. Check Total and Skipped.
   - Backticks in `git commit -m` and other shell strings are executed. Use `-F` or a heredoc.
   - Mutation probes: `mv`-restoring a file keeps the mutant's mtime, so the incremental build skips it. Confirm the mutation landed.
   - Squash-merge makes landed branches read as unmerged.
   - The main checkout can lag `origin`. Read with `git show origin/main:<path>`.
   - Bash: `exit` inside `$( )` ends only the subshell.
   - A merge check must compare warnings, not only count errors.
3. Show the proposed `MEMORY.md` to the user before writing it.

## §4 Verification

- **Spec coverage:** a script extracts every requirement, criterion and directive ID (`R-`, `A-`, `D-`) from `prd/PRD-*.md`, `brief.md` and `product-directives.md`, and checks that each one appears in an area spec, either in its body or in that spec's closing `## Retired IDs` list with a one-line reason. There are no `TBD`s in the spec files.
- **Archive integrity:** the tag `agent-era-final` exists, `git -C <archive> status` is clean, and `git bundle verify` passes.
- **Project clean:** `.claude/` contains only `settings.local.json`, and `git worktree list` shows only the main checkout.
- **Build unaffected:** `dotnet build` and `dotnet test` on the branch report the same Total, passed and skipped counts as the pre-change baseline taken on `main` at the start.
- **Skills gone:** a fresh session lists none of the 9 role skills.

## Out of scope

- Rewriting requirement and ticket IDs in code comments (sub-project 2), including the three comments that mention `.claude/` and the two tests that copy PRD text because the PRD was untracked.
- Any product-code or test change.
- Jira, GitHub PRs and remote branches.
