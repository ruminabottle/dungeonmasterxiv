# Retire the Agent Team Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the PRDs, brief and product directives into versioned Superpowers specs, then archive and remove the `.claude/` multi-role agent system and rebuild project memory, with no change to product code or test results.

**Architecture:** Content work first, while the sources are still in place: six area specs under `docs/superpowers/specs/`, each checked by an ID-coverage script. Then irreversible operations, each gated on a check: freeze and tag the nested repo, bundle and remove the worktrees, move `.claude/` to an archive, and archive and rebuild memory. The archive lives outside the project at one fixed path.

**Tech Stack:** Markdown, bash, git (worktrees, bundles, tags), .NET SDK (`dotnet test`, baseline comparison only), `gh` (read-only PR listing).

**Spec:** `docs/superpowers/specs/2026-09-29-retire-agent-team-design.md`

## Global Constraints

- Archive destination: `~/archive/dungeonmasterxiv-agent-team-2026-09-29/` (written `$A` below). Repo root: `/Users/ramonmunoz/repositories/dungeonmasterxiv-1` (written `$R`).
- Work on branch `docs/retire-agent-team` in `$R`. No product-code or test file (`src/`, `tests/`, `tools/`, `Windows/`, `Net/`, `Services/`, `Data/`, `Plugin.cs`) is modified.
- Area specs have stable, undated names: `product-overview.md`, `core-skeleton.md`, `session-layer.md`, `rolls.md`, `initiative.md`, `distribution.md`, plus `research/`.
- Requirement IDs (`R-n.m…`), criterion IDs (`A-n.m…`) and directive IDs (`D-n`) are kept verbatim as anchors. Ticket IDs (`DMXENG-*`, `BUG-*`, `SQ-*`) and role names do not appear in area specs.
- Every area spec ends with `## Retired IDs`: one bullet per dropped ID, `- <ID>: <one-line reason>`. Write `None.` if there are none.
- Uncertain requirements get an `Open question:` line. Never guess.
- Where a PRD and a later directive disagree, the directive wins.
- Not touched: Jira, GitHub PRs, remote branches, local branches (none are deleted).
- Nothing is killed by the implementer. Running sessions are stopped by the human.
- Commit messages go through `git commit -F -` with a quoted heredoc (`<<'EOF'`). Never use `-m` with backticks.
- Commit trailer: `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`
- **Checkpoint tasks (0, 7, 10) are run by the controlling session, not a subagent,** because they need the human.

## Review Focus

1. **ID boundary:** `R-1.2` must not count as present just because `R-1.2a` is. Otherwise a dropped parent requirement reads as covered. Pinned in Task 1, Step 2 (fixture `boundary`).
2. **Zero-extraction pass:** once the sources move to `$A`, a stale path extracts no IDs, and "0 missing" would read as full coverage. The checker must exit 2 on an empty extraction. Pinned in Task 1, Step 2 (fixture `empty`).
3. **Self-coverage:** the dated design spec quotes `R-1.7a`, so the checker must ignore `2026-*` files or the design doc would "cover" IDs the area specs dropped. Pinned in Task 1, Step 2 (fixture `dated`).
4. **Late writes to `.claude/`:** a session still running between the freeze tag and the move would leave changes outside the tag. Task 9, Step 1 refuses to move unless `git -C .claude status --porcelain` is empty.
5. **Losing memory or worktree work:** deleting before the copy is verified. Task 8 refuses to remove a worktree with any `status --porcelain` output, and Task 10 runs `diff -r` against the archive before deleting anything.

---

### Task 0: Checkpoint: nothing running, test baseline (controller + human)

**Files:**
- Create: `$A/baseline-test.txt`, `$A/sessions-before.txt`

**Interfaces:**
- Produces: `$A/` exists; `$A/baseline-test.txt` holds the full `dotnet test` output from `main` at `ea669fa`.

- [ ] **Step 1: Create the archive directory**

```bash
A=~/archive/dungeonmasterxiv-agent-team-2026-09-29
mkdir -p "$A" && ls -ld "$A"
```

- [ ] **Step 2: List running Claude sessions and their working directories**

```bash
A=~/archive/dungeonmasterxiv-agent-team-2026-09-29
for p in $(pgrep -f claude); do
  printf '%s\t%s\t' "$p" "$(ps -o command= -p "$p" | cut -c1-80)"
  lsof -a -p "$p" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p'
done | tee "$A/sessions-before.txt"
cat /Users/ramonmunoz/repositories/dungeonmasterxiv-1/.claude/scheduled_tasks.lock 2>/dev/null
```

Also call the `CronList` tool and record any jobs that belong to agent roles.

- [ ] **Step 3: Human stops the role sessions**

Show the user `sessions-before.txt` and the cron list. Ask them to stop every session except this one, and to delete role crons. Wait. Re-run Step 2 and continue only when the one remaining process is this session.

- [ ] **Step 4: Take the test baseline on `main`**

```bash
A=~/archive/dungeonmasterxiv-agent-team-2026-09-29
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
git status --porcelain   # expect: no output (the plan and spec are committed)
git switch main
dotnet test DungeonMasterXIV.sln > "$A/baseline-test.txt" 2>&1; echo "exit=$?"
grep -E '^(Passed|Failed)!|Total tests|Skipped' "$A/baseline-test.txt"
git switch docs/retire-agent-team
```

Expected: one `Passed!` (or `Failed!`) summary line per test project, **with Total, Failed, Passed and Skipped counts**. Record them. If there are no summary lines, the run was truncated. Stop and investigate. A pre-existing failure is fine, since this is a baseline, but write it down.

---

### Task 1: ID-coverage checker

**Files:**
- Create: `$A/check-ids.sh`
- Create: `$A/checker-fixtures/` (test fixtures)

**Interfaces:**
- Produces: `check-ids.sh <target-dir> <id-regex> <source-file>...`, which prints `MISSING <id>` for each absent ID and then `ids=<n> missing=<m>`. Exits 0 if all are present, 1 if any are missing, 2 if the sources yield zero IDs or a source is unreadable. It ignores files named `2026-*` under the target.

- [ ] **Step 1: Write the fixtures**

```bash
F=~/archive/dungeonmasterxiv-agent-team-2026-09-29/checker-fixtures
mkdir -p "$F"/{boundary,empty,dated,ok}/specs
# boundary: source has R-1.2 and R-1.2a; target has only R-1.2a
printf '### R-1.2 Codes\n### R-1.2a Params\n- **A-1.2a** x\n' > "$F/boundary/src.md"
printf '### R-1.2a Params\n- **A-1.2a** x\n' > "$F/boundary/specs/s.md"
# empty: source contains no IDs at all
printf 'no ids here\n' > "$F/empty/src.md"
# dated: the ID exists only in a dated design spec
printf '### R-1.7a Copy\n' > "$F/dated/src.md"
printf 'cites R-1.7a\n' > "$F/dated/specs/2026-09-29-x-design.md"
# ok: everything present, one via Retired IDs
printf '### R-1.1 Host\n### D-3 Auth\n' > "$F/ok/src.md"
printf '### R-1.1 Host\n## Retired IDs\n- D-3: process rule\n' > "$F/ok/specs/s.md"
```

- [ ] **Step 2: Write the fixture test and run it (it fails, since the script doesn't exist yet)**

```bash
cat > ~/archive/dungeonmasterxiv-agent-team-2026-09-29/checker-fixtures/run.sh <<'EOF'
#!/usr/bin/env bash
# Each case: expected exit code, and a string the output must contain.
F="$(cd "$(dirname "$0")" && pwd)"; C="$F/../check-ids.sh"; RE='([RA]-[0-9]+\.[0-9]+[a-z]*|D-[0-9]+)'
fail=0
t() { local name=$1 want=$2 needle=$3 out code
  out=$("$C" "$F/$name/specs" "$RE" "$F/$name/src.md" 2>&1); code=$?
  if [ "$code" -ne "$want" ] || ! grep -qF -- "$needle" <<<"$out"; then
    echo "FAIL $name: exit=$code want=$want; output: $out"; fail=1
  else echo "ok   $name"; fi; }
t boundary 1 "MISSING R-1.2"
t empty    2 "no ids"
t dated    1 "MISSING R-1.7a"
t ok       0 "ids=2 missing=0"
exit $fail
EOF
chmod +x ~/archive/dungeonmasterxiv-agent-team-2026-09-29/checker-fixtures/run.sh
~/archive/dungeonmasterxiv-agent-team-2026-09-29/checker-fixtures/run.sh; echo "exit=$?"
```

Expected: all four `FAIL` (the script is missing), and `exit=1`.

- [ ] **Step 3: Write the checker**

```bash
cat > ~/archive/dungeonmasterxiv-agent-team-2026-09-29/check-ids.sh <<'EOF'
#!/usr/bin/env bash
# check-ids.sh <target-dir> <id-regex> <source-file>...
# Every ID matching <id-regex> in the sources must appear, as a whole token,
# in some .md file under <target-dir> that is not a dated (2026-*) design spec.
set -uo pipefail
target=$1 re=$2; shift 2
for s in "$@"; do [ -r "$s" ] || { echo "unreadable source: $s" >&2; exit 2; }; done
ids=$(grep -hoE "\\b${re}\\b" "$@" | sort -u)
[ -n "$ids" ] || { echo "no ids extracted from: $*" >&2; exit 2; }
total=0 missing=0
while read -r id; do
  total=$((total+1))
  # -w: R-1.2 does not match inside R-1.2a
  grep -rqwF --include='*.md' --exclude='2026-*' -- "$id" "$target" \
    || { echo "MISSING $id"; missing=$((missing+1)); }
done <<<"$ids"
echo "ids=$total missing=$missing"
[ "$missing" -eq 0 ]
EOF
chmod +x ~/archive/dungeonmasterxiv-agent-team-2026-09-29/check-ids.sh
```

- [ ] **Step 4: Run the fixture test. It passes.**

Run: `~/archive/dungeonmasterxiv-agent-team-2026-09-29/checker-fixtures/run.sh; echo "exit=$?"`
Expected: four `ok` lines, `exit=0`.

- [ ] **Step 5: Mutation check. Show the boundary fixture detects the `-w` removal.**

```bash
C=~/archive/dungeonmasterxiv-agent-team-2026-09-29/check-ids.sh
sed -i '' 's/grep -rqwF/grep -rqF/' "$C" && grep -c 'grep -rqF' "$C"   # expect 1: mutation landed
~/archive/dungeonmasterxiv-agent-team-2026-09-29/checker-fixtures/run.sh   # expect: FAIL boundary
sed -i '' 's/grep -rqF/grep -rqwF/' "$C" && grep -c 'grep -rqwF' "$C"  # expect 1: restored
~/archive/dungeonmasterxiv-agent-team-2026-09-29/checker-fixtures/run.sh   # expect: four ok
```

- [ ] **Step 6: Record a baseline against the real sources (it fails, since no specs exist yet)**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
~/archive/dungeonmasterxiv-agent-team-2026-09-29/check-ids.sh docs/superpowers/specs \
  '([RA]-[0-9]+\.[0-9]+[a-z]*|D-[0-9]+)' \
  .claude/team/product/prd/PRD-*.md .claude/team/product/brief.md \
  .claude/skills/deployment-manager/product-directives.md | tail -1
```

Expected: `ids=289 missing=289` (or close to it, if sources changed since 2026-09-29), exit 1. Nothing to commit, since the checker lives in the archive.

---

### How to convert a source (applies to Tasks 2–6)

Each conversion task follows this procedure. It's repeated here so it can be read on its own.

1. **Read the whole source file**, top to bottom, not by grep. For the directives, also read the `## Resolved escalations` section (line ~1689 on) and `## Out of scope` (line ~2356 on) of `.claude/skills/deployment-manager/product-directives.md`, and fold in every item that bears on your area.
2. Write the spec in this shape:

```markdown
# <Area>: product spec

Describes the current product. Converted 2026-09-29 from <source file name>; the
original is archived at ~/archive/dungeonmasterxiv-agent-team-2026-09-29/claude/<path>.

## Purpose
<why this area exists, 1–3 paragraphs, from the source's "Why this exists">

## Requirements

### R-1.1 Hosting
<the requirement statement, as rules about the product>

**Acceptance criteria**
- **A-1.1a** <observable, checkable statement>

## Open questions
- Open question: <what is undecided, and what it blocks>   (or "None.")

## Retired IDs
- R-1.2x: superseded by R-1.5a   (or "None.")
```

3. **Headings:** keep the ID and title. Remove bracketed provenance suffixes such as `[decided 2026-08-26, answers SQ-2]`.
4. **Remove process:** ticket numbers, `SQ-*`, role names ("the Spec Owner rules…", "the Deployment Manager…"), tiers, dispatch notes, escalation history, dates of rulings. Keep what was ruled.
5. **Supersession:** if a later ID replaces an earlier one, keep the later one and list the earlier one under Retired IDs as `superseded by <ID>`.
6. **Process-only criteria** (e.g. "QA confirms in game", "a human signs off"): rewrite as a product-observable statement if one exists. Otherwise retire it with the reason `process check, not a product property`.
7. **Uncertain status:** spot-check against the code with `git grep -n '<ID>' -- src tests` and read the hit. If it's still unclear, write an `Open question:`.
8. Run the area's coverage check (given in each task) until it reports `missing=0`, then run `grep -nE 'TBD|TODO|DMXENG|SQ-[0-9]|BUG-[0-9]|Spec Owner|Deployment Manager|Product Owner|QA ' <spec>` and expect no output.

---

### Task 2: `product-overview.md` (brief + directives)

**Files:**
- Create: `docs/superpowers/specs/product-overview.md`
- Read: `.claude/team/product/brief.md` (962 lines), `.claude/skills/deployment-manager/product-directives.md` (2,386 lines)

**Interfaces:**
- Produces: `D-*` anchors (all 46+ directives, as principles or retired), used by later area specs as `see product-overview D-11`.

Sections, in order: `Purpose` (brief "What it is"), `Platforms`, `Who it is for`, `Problems it solves`, `How state is shared`, `Identity and privacy`, `Principles` (the product directives, each as `### D-n Title`), `Non-goals and out of scope` (brief "Non-goals" plus the directives' `## Out of scope`), `Cross-cutting acceptance criteria` (brief "Acceptance criteria"), `Session panel` (brief "The session panel" section, as product rules), `Default relay`, `Repository layout`, `Open questions` (brief "Open questions", only the ones still open), `Retired IDs`.

Drop (process, not product): brief "Priority", "The first in-game confirmation", "Current focus", "The published policy has accuracy triggers, and they are mine", "Directive history". Directives about how the team worked go under Retired IDs with the reason `team process rule`. Examples from the list: D-5 "Tier order is not negotiable", D-6 "Nothing with logic merges unbuilt", D-7 "In-game criteria need a human", D-10 "Structure is reviewed before code is layered on it". Decide each directive by whether it describes the product or the team.

- [ ] **Step 1: Run the coverage check. It fails.**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
~/archive/dungeonmasterxiv-agent-team-2026-09-29/check-ids.sh docs/superpowers/specs 'D-[0-9]+' \
  .claude/skills/deployment-manager/product-directives.md .claude/team/product/brief.md | tail -1
```

Expected: `missing=` equal to `ids=`, exit 1.

- [ ] **Step 2: Write `product-overview.md` following "How to convert a source"**
- [ ] **Step 3: Re-run Step 1's command.** Expected: `missing=0`, exit 0.
- [ ] **Step 4: Run the process-leak grep.** Expected: no output.

```bash
grep -nE 'TBD|TODO|DMXENG|SQ-[0-9]|BUG-[0-9]|Spec Owner|Deployment Manager|Product Owner|QA ' docs/superpowers/specs/product-overview.md
```

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/product-overview.md
git commit -q -F - <<'EOF'
docs(spec): product overview from the brief and product directives

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 3: `session-layer.md` (PRD-1)

**Files:**
- Create: `docs/superpowers/specs/session-layer.md`
- Read: `.claude/team/product/prd/PRD-1-session-layer.md` (1,769 lines, 153 unique IDs), plus the directives' Resolved escalations (see "How to convert a source")

**Interfaces:**
- Consumes: `D-*` anchors from Task 2 (cite them as `product-overview D-n`; don't restate them).

Keep the IDs in numeric order under `## Requirements` (R-1.1, R-1.2, R-1.2a, R-1.3, R-1.3d…), not in the PRD's file order.

- [ ] **Step 1: Run the coverage check. It fails.**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
~/archive/dungeonmasterxiv-agent-team-2026-09-29/check-ids.sh docs/superpowers/specs '[RA]-1\.[0-9]+[a-z]*' \
  .claude/team/product/prd/PRD-*.md .claude/team/product/brief.md \
  .claude/skills/deployment-manager/product-directives.md | tail -1
```

Expected: `missing=` equal to `ids=` (about 150), exit 1.

- [ ] **Step 2: Write `session-layer.md` following "How to convert a source"**
- [ ] **Step 3: Re-run Step 1's command.** Expected: `missing=0`, exit 0.
- [ ] **Step 4: Run the process-leak grep.** Expected: no output.

```bash
grep -nE 'TBD|TODO|DMXENG|SQ-[0-9]|BUG-[0-9]|Spec Owner|Deployment Manager|Product Owner|QA ' docs/superpowers/specs/session-layer.md
```

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/session-layer.md
git commit -q -F - <<'EOF'
docs(spec): session layer spec from PRD-1

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 4: `rolls.md` (PRD-2)

**Files:**
- Create: `docs/superpowers/specs/rolls.md`
- Read: `.claude/team/product/prd/PRD-2-rolls.md` (896 lines, 115 unique IDs), the brief's "Reference: Foundry VTT chat, for PRD-2" section, and the directives' Resolved escalations

**Interfaces:**
- Consumes: `D-*` from Task 2 and `R-1.*` from Task 3 (cite them, e.g. `session-layer R-1.3e`).

- [ ] **Step 1: Run the coverage check. It fails.**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
~/archive/dungeonmasterxiv-agent-team-2026-09-29/check-ids.sh docs/superpowers/specs '[RA]-2\.[0-9]+[a-z]*' \
  .claude/team/product/prd/PRD-*.md .claude/team/product/brief.md \
  .claude/skills/deployment-manager/product-directives.md | tail -1
```

Expected: `missing=` equal to `ids=`, exit 1.

- [ ] **Step 2: Write `rolls.md` following "How to convert a source"**
- [ ] **Step 3: Re-run Step 1's command.** Expected: `missing=0`, exit 0.
- [ ] **Step 4: Run the process-leak grep.** Expected: no output.

```bash
grep -nE 'TBD|TODO|DMXENG|SQ-[0-9]|BUG-[0-9]|Spec Owner|Deployment Manager|Product Owner|QA ' docs/superpowers/specs/rolls.md
```

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/rolls.md
git commit -q -F - <<'EOF'
docs(spec): rolls spec from PRD-2

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 5: `core-skeleton.md` and `initiative.md` (PRD-0, PRD-3)

**Files:**
- Create: `docs/superpowers/specs/core-skeleton.md`, `docs/superpowers/specs/initiative.md`
- Read: `.claude/team/product/prd/PRD-0-core-skeleton.md` (145 lines, 22 IDs), `.claude/team/product/prd/PRD-3-initiative.md` (151 lines, 33 IDs)

**Interfaces:**
- Consumes: `D-*` from Task 2 and `R-1.*` from Task 3 (cite them, don't restate them).

- [ ] **Step 1: Run both coverage checks. Both fail.**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
S=".claude/team/product/prd/PRD-*.md .claude/team/product/brief.md .claude/skills/deployment-manager/product-directives.md"
for n in 0 3; do ~/archive/dungeonmasterxiv-agent-team-2026-09-29/check-ids.sh docs/superpowers/specs "[RA]-$n\.[0-9]+[a-z]*" $S | tail -1; done
```

Expected: two lines, each with `missing=` equal to `ids=`.

- [ ] **Step 2: Write `core-skeleton.md` following "How to convert a source"**
- [ ] **Step 3: Write `initiative.md` following "How to convert a source"**
- [ ] **Step 4: Re-run Step 1's command.** Expected: both lines report `missing=0`.
- [ ] **Step 5: Run the process-leak grep.** Expected: no output.

```bash
grep -nE 'TBD|TODO|DMXENG|SQ-[0-9]|BUG-[0-9]|Spec Owner|Deployment Manager|Product Owner|QA ' docs/superpowers/specs/core-skeleton.md docs/superpowers/specs/initiative.md
```

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/specs/core-skeleton.md docs/superpowers/specs/initiative.md
git commit -q -F - <<'EOF'
docs(spec): core skeleton and initiative specs from PRD-0 and PRD-3

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 6: `distribution.md` (PRD-7 + relay policy) and research notes

**Files:**
- Create: `docs/superpowers/specs/distribution.md`
- Create: `docs/superpowers/specs/research/foundry-vtt-model.md`, `docs/superpowers/specs/research/lightless-sync-client-server.md` (copied unchanged)
- Read: `.claude/team/product/prd/PRD-7-distribution.md` (200 lines, 29 IDs), `.claude/team/product/policy/relay-service-policy.md` (180 lines)
- Do not modify: `RELAY-SERVICE-POLICY.md` at the repo root (the README links to it)

**Interfaces:**
- Consumes: `D-*` from Task 2. `distribution.md` links to `../../../RELAY-SERVICE-POLICY.md` for the published policy rather than duplicating it. It includes only rules in the internal `policy/relay-service-policy.md` that aren't in the published one. Check with `diff .claude/team/product/policy/relay-service-policy.md RELAY-SERVICE-POLICY.md`.

- [ ] **Step 1: Run the coverage check. It fails.**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
~/archive/dungeonmasterxiv-agent-team-2026-09-29/check-ids.sh docs/superpowers/specs '[RA]-7\.[0-9]+[a-z]*' \
  .claude/team/product/prd/PRD-*.md .claude/team/product/brief.md \
  .claude/skills/deployment-manager/product-directives.md | tail -1
```

- [ ] **Step 2: Write `distribution.md` following "How to convert a source"**
- [ ] **Step 3: Copy the research notes**

```bash
mkdir -p docs/superpowers/specs/research
cp .claude/team/product/research/foundry-vtt-model.md .claude/team/product/research/lightless-sync-client-server.md docs/superpowers/specs/research/
diff -r .claude/team/product/research docs/superpowers/specs/research && echo identical
```

- [ ] **Step 4: Re-run Step 1's command.** Expected: `missing=0`, exit 0.
- [ ] **Step 5: Run the process-leak grep.** Expected: no output.

```bash
grep -nE 'TBD|TODO|DMXENG|SQ-[0-9]|BUG-[0-9]|Spec Owner|Deployment Manager|Product Owner|QA ' docs/superpowers/specs/distribution.md
```

- [ ] **Step 6: Full coverage run across all sources**

```bash
~/archive/dungeonmasterxiv-agent-team-2026-09-29/check-ids.sh docs/superpowers/specs \
  '([RA]-[0-9]+\.[0-9]+[a-z]*|D-[0-9]+)' \
  .claude/team/product/prd/PRD-*.md .claude/team/product/brief.md \
  .claude/skills/deployment-manager/product-directives.md | tail -3
```

Expected: `ids=289 missing=0` (the `ids` figure matches Task 1, Step 6), exit 0.

- [ ] **Step 7: Commit**

```bash
git add docs/superpowers/specs/distribution.md docs/superpowers/specs/research
git commit -q -F - <<'EOF'
docs(spec): distribution spec from PRD-7 and the relay policy; research notes

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 7: Checkpoint: freeze and tag the nested repo (controller + human)

**Files:**
- Modify: the nested repo at `$R/.claude` (commit + tag only)

**Interfaces:**
- Produces: tag `agent-era-final` in `$R/.claude`. After this, `git -C .claude status --porcelain` is empty.

- [ ] **Step 1: Show the human what will be committed**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
git -C .claude status --porcelain | wc -l
git -C .claude status --porcelain | cut -c4- | sed 's#/[^/]*$##' | sort | uniq -c
git -C .claude diff --stat | tail -1
```

Look for secrets (tokens, `.env`, credentials). If there are any, stop and ask. Get the user's go-ahead.

- [ ] **Step 2: Commit and tag**

```bash
git -C .claude add -A
git -C .claude commit -q -F - <<'EOF'
chore: final state before retiring the agent team

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
git -C .claude tag agent-era-final
git -C .claude status --porcelain | wc -l      # expect 0
git -C .claude log --oneline -1 agent-era-final
```

---

### Task 8: Bundle and remove worktrees

**Files:**
- Create: `$A/worktrees.bundle`, `$A/worktree-list-before.txt`
- Modify: the outer repo's worktree registry. Creates refs `refs/archive/<name>`.

**Interfaces:**
- Consumes: Task 7 (the `.claude` repo is frozen).
- Produces: no `.claude/worktrees/*` registered with the outer repo. A verified bundle holds each worktree's HEAD.

- [ ] **Step 1: Snapshot the registry and pin each worktree HEAD to a ref**

```bash
A=~/archive/dungeonmasterxiv-agent-team-2026-09-29
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
git worktree list --porcelain > "$A/worktree-list-before.txt"
for w in .claude/worktrees/*/; do w=${w%/}
  if [ "$(git -C "$w" rev-parse --show-toplevel 2>/dev/null)" != "$PWD/$w" ]; then echo "skip $w (not a worktree)"; continue; fi
  git update-ref "refs/archive/$(basename "$w")" "$(git -C "$w" rev-parse HEAD)"
done
git for-each-ref --format='%(refname) %(objectname:short)' refs/archive
```

Expected: `skip .claude/worktrees/sim (not a worktree)` and 10 `refs/archive/*` lines, matching the SHAs in `worktree-list-before.txt`.

- [ ] **Step 2: Bundle and verify**

```bash
A=~/archive/dungeonmasterxiv-agent-team-2026-09-29
git bundle create "$A/worktrees.bundle" --glob=refs/archive
git bundle verify "$A/worktrees.bundle" && git bundle list-heads "$A/worktrees.bundle" | wc -l   # expect: "is okay", 10
```

- [ ] **Step 3: Refuse on dirty worktrees, then remove the clean ones**

```bash
for w in .claude/worktrees/*/; do w=${w%/}
  [ "$(git -C "$w" rev-parse --show-toplevel 2>/dev/null)" = "$PWD/$w" ] || continue
  d=$(git -C "$w" status --porcelain | wc -l | tr -d ' ')
  if [ "$d" != 0 ]; then echo "DIRTY $w ($d): not removed"; else git worktree remove "$w" && echo "removed $w"; fi
done
```

Expected: 10 `removed` lines. Any `DIRTY` line means stop and show the user the `git -C <w> status`.

- [ ] **Step 4: Prune stale entries and report what's left**

```bash
git worktree prune -v
git worktree list
```

For each remaining entry other than `$R` itself: if its directory exists and `git -C <dir> status --porcelain` is empty, run `git worktree remove <dir>`. If it's dirty, list it for the user and leave it. Expected end state: `git worktree list` shows only `$R`, or only `$R` plus entries reported as dirty.

(Nothing to commit in the outer repo. Worktree registrations aren't tracked.)

---

### Task 9: Move `.claude/` to the archive; tidy `.gitignore`

**Files:**
- Move: `$R/.claude` → `$A/claude`
- Create: `$R/.claude/settings.local.json` (copied back)
- Modify: `$R/.gitignore:1-8`

**Interfaces:**
- Consumes: Task 7 tag and Task 8 (no worktrees inside `.claude/`).
- Produces: `$A/claude` (the whole nested repo). `$R/.claude/` contains only `settings.local.json`.

- [ ] **Step 1: Guard. Nothing has changed since the tag.**

```bash
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
test -z "$(git -C .claude status --porcelain)" && git -C .claude describe --exact-match --tags HEAD
```

Expected: `agent-era-final`. Any other output (a late write, or HEAD moved) means stop: re-run Task 0, Step 2 to find the writer, then redo Task 7.

- [ ] **Step 2: Move and restore settings**

```bash
A=~/archive/dungeonmasterxiv-agent-team-2026-09-29
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
mv .claude "$A/claude"
mkdir .claude && cp "$A/claude/settings.local.json" .claude/
ls -A .claude                                                      # expect: settings.local.json
git -C "$A/claude" status --porcelain | wc -l                       # expect 0
git -C "$A/claude" tag -l agent-era-final                           # expect agent-era-final
find .claude -name SKILL.md | wc -l                                 # expect 0
```

- [ ] **Step 3: Replace the harness comment in `.gitignore`**

Replace lines 1–8 (from `# Claude Code multi-role team harness` through `# .claude/team/queues/*/done/*.md`) with:

```gitignore
# Local Claude Code settings; not part of the plugin.
.claude/
```

Check: `head -3 .gitignore` shows the two new lines followed by a blank line, and `git diff --stat` shows only `.gitignore`.

- [ ] **Step 4: Commit**

```bash
git add .gitignore
git commit -q -F - <<'EOF'
chore: drop the team-harness notes from .gitignore

The multi-role agent system is archived outside the repo.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 10: Checkpoint: archive and rebuild memory (controller + human)

**Files:**
- Copy: `~/.claude/projects/-Users-ramonmunoz-repositories-dungeonmasterxiv-1/memory/` → `$A/memory/`
- Replace: every file in the live memory directory with the 7 files below plus `MEMORY.md`

**Interfaces:**
- Produces: a live `MEMORY.md` with exactly 7 entries.

- [ ] **Step 1: Copy and verify**

```bash
M=~/.claude/projects/-Users-ramonmunoz-repositories-dungeonmasterxiv-1/memory
A=~/archive/dungeonmasterxiv-agent-team-2026-09-29
cp -Rp "$M" "$A/memory"
diff -r "$M" "$A/memory" && echo identical; ls "$A/memory" | wc -l
```

Expected: `identical`, and a count equal to `ls "$M" | wc -l`. If it isn't identical, stop.

- [ ] **Step 2: Show the user the new index (below) and wait for approval**

- [ ] **Step 3: Clear the live directory and write the new files**

```bash
M=~/.claude/projects/-Users-ramonmunoz-repositories-dungeonmasterxiv-1/memory
diff -r "$M" ~/archive/dungeonmasterxiv-agent-team-2026-09-29/memory >/dev/null && find "$M" -mindepth 1 -delete && ls -A "$M" | wc -l   # expect 0
```

Then write each file with the Write tool:

`dotnet-test-summary-can-lie.md`
```markdown
---
name: dotnet-test-summary-can-lie
description: dotnet test prints "Passed!" on a truncated run or with a skipped gate; gate on Total and Skipped
metadata:
  type: feedback
---
`dotnet test` prints `Passed!` on a truncated suite and when a gate test is skipped (`Failed: 0, Passed: 298, Skipped: 1` hid the size gate). Compare Total and Skipped against a known baseline, not just the verdict word.
**Why:** a green verdict was reported on runs that never executed part of the suite.
**How to apply:** when reporting test results, quote the Total/Passed/Skipped line and compare it with the previous run.
```

`backticks-in-shell-strings.md`
```markdown
---
name: backticks-in-shell-strings
description: backticks inside git commit -m and other double-quoted shell args are executed and vanish
metadata:
  type: feedback
---
Backtick code spans inside a double-quoted shell argument (`git commit -m "..."`, any CLI message) are command substitution: they run, and the text silently disappears.
**Why:** this destroyed commit and message text several times.
**How to apply:** pass messages via `-F -` with a quoted heredoc (`<<'EOF'`).
```

`mutation-probe-hygiene.md`
```markdown
---
name: mutation-probe-hygiene
description: confirm a mutation landed; mv-restoring a file keeps the mutant mtime so incremental builds skip it
metadata:
  type: feedback
---
When mutating code to prove a test guards it: grep that the mutation actually landed before running the tests (a no-op edit gives a green run that reads as "the test is blind"). Restore with `git checkout -- <file>` or an edit, not `mv` from a backup, because `mv` preserves the backup's mtime and the incremental build may keep the mutant.
**Why:** both failure modes produced wrong conclusions about test coverage.
**How to apply:** mutate, grep, build, test, restore via git, grep again.
```

`squash-merge-reads-unmerged.md`
```markdown
---
name: squash-merge-reads-unmerged
description: squash-merged branches look unmerged ("N behind", git branch --no-merged) though the work landed
metadata:
  type: project
---
The repo squash-merges PRs, so a landed branch still shows as unmerged and a worktree on it reads as "N behind".
**Why:** landed work was mistaken for pending work.
**How to apply:** check `gh pr list --state merged --head <branch>` before treating a branch as unlanded or deleting it.
```

`read-origin-not-checkout.md`
```markdown
---
name: read-origin-not-checkout
description: the main checkout can lag origin; read current files with git show origin/main:<path>
metadata:
  type: feedback
---
The local `main` checkout may be behind `origin/main`. For reading the current state of a file, `git fetch` and then `git show origin/main:<path>`.
**Why:** stale local files were quoted as current.
**How to apply:** any claim about what main contains names the ref it was read from.
```

`bash-exit-in-subshell.md`
```markdown
---
name: bash-exit-in-subshell
description: exit/die inside $( ) ends only the subshell; the caller keeps running
metadata:
  type: feedback
---
In bash, `exit` (or a `die` helper) called inside `$( ... )` terminates only the command-substitution subshell; the calling script continues with an empty value.
**Why:** a guard written this way never stopped its caller.
**How to apply:** check the substitution's exit status (`x=$(f) || exit`), or don't guard from inside `$( )`.
```

`merge-check-diffs-warnings.md`
```markdown
---
name: merge-check-diffs-warnings
description: a merge check that counts only errors passes a change that adds warnings; diff warnings against the base
metadata:
  type: feedback
---
Build verification before merge must compare the warning set against the merge target, not only require zero errors.
**Why:** a change adding warnings passed an errors-only check.
**How to apply:** capture warnings on the base and on the branch and diff them. Confirm each capture actually ran (the build output is non-empty) before trusting an empty diff.
```

`MEMORY.md`
```markdown
- [dotnet test summary can lie](dotnet-test-summary-can-lie.md) — "Passed!" on truncated/skipped runs; gate on Total and Skipped
- [Backticks in shell strings](backticks-in-shell-strings.md) — executed inside commit -m; use -F with a quoted heredoc
- [Mutation probe hygiene](mutation-probe-hygiene.md) — confirm the mutation landed; restore via git, not mv
- [Squash-merge reads unmerged](squash-merge-reads-unmerged.md) — landed branches look unmerged; check merged PRs
- [Read origin, not the checkout](read-origin-not-checkout.md) — git show origin/main:<path>
- [Bash exit in subshell](bash-exit-in-subshell.md) — exit inside $( ) does not stop the caller
- [Merge check diffs warnings](merge-check-diffs-warnings.md) — errors-only checks pass added warnings
```

- [ ] **Step 4: Verify**

```bash
M=~/.claude/projects/-Users-ramonmunoz-repositories-dungeonmasterxiv-1/memory
ls "$M" | wc -l                                            # expect 8
grep -c '^- \[' "$M/MEMORY.md"                             # expect 7
grep -rilE 'Q_ROLE|queue|jira|qa-[0-9]|breakfix|deployment manager|board' "$M" || echo clean
```

(Nothing to commit. Memory is outside the repo.)

---

### Task 11: Final verification and hand-off

**Files:** none modified (read-only checks).

- [ ] **Step 1: Spec coverage from the archived sources**

```bash
A=~/archive/dungeonmasterxiv-agent-team-2026-09-29
cd /Users/ramonmunoz/repositories/dungeonmasterxiv-1
"$A/check-ids.sh" docs/superpowers/specs '([RA]-[0-9]+\.[0-9]+[a-z]*|D-[0-9]+)' \
  "$A"/claude/team/product/prd/PRD-*.md "$A/claude/team/product/brief.md" \
  "$A/claude/skills/deployment-manager/product-directives.md" | tail -1
grep -rnE 'TBD|TODO' docs/superpowers/specs --include='*.md' --exclude='2026-*' || echo "no TBDs"
```

Expected: `ids=289 missing=0` (the same `ids` as Task 1, Step 6. A smaller number means the paths are wrong), and `no TBDs`.

- [ ] **Step 2: Archive integrity**

```bash
git -C "$A/claude" status --porcelain | wc -l      # 0
git -C "$A/claude" tag -l agent-era-final          # agent-era-final
git bundle verify "$A/worktrees.bundle"            # "is okay"
```

- [ ] **Step 3: Project clean**

```bash
ls -A .claude                  # settings.local.json
git worktree list              # only $R (plus any user-acknowledged dirty entries from Task 8)
git diff --stat main -- src tests tools Windows Net Services Data Plugin.cs | tail -1   # empty: no product change
```

- [ ] **Step 4: Tests match the baseline**

```bash
dotnet test DungeonMasterXIV.sln > "$A/after-test.txt" 2>&1; echo "exit=$?"
diff <(grep -E '^(Passed|Failed)!' "$A/baseline-test.txt" | sed 's/, Duration.*//') \
     <(grep -E '^(Passed|Failed)!' "$A/after-test.txt"    | sed 's/, Duration.*//') && echo "same counts"
grep -cE '^(Passed|Failed)!' "$A/baseline-test.txt" "$A/after-test.txt"   # both non-zero and equal
```

Expected: `same counts`, and non-zero, equal line counts. That guards against an empty-versus-empty "match".

- [ ] **Step 5: Open agent-era PRs, for the user (read-only)**

```bash
gh pr list --state open --json number,title,headRefName --jq '.[] | "#\(.number) \(.headRefName) — \(.title)"'
```

Report the list. Don't close or comment.

- [ ] **Step 6: Human confirms in a fresh session** that none of the 9 role skills (`breakfix-engineer`, `code-reviewer`, `deployment-manager`, `engineering-lead`, `facilitator`, `feature-engineer`, `product-owner`, `qa`, `spec-owner`) are listed.

- [ ] **Step 7: Finish the branch.** Use superpowers:finishing-a-development-branch. The branch holds the specs, this plan and the `.gitignore` change.
