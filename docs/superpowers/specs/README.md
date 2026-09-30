# Product specs

These files describe the current product, one area each. `product-overview.md` holds the principles
every other spec cites; the rest hold requirements (`R-`) and acceptance criteria (`A-`).

| ID prefix | Spec |
| --- | --- |
| `D-n` (principles) and "Session panel" items | [product-overview.md](product-overview.md) |
| `R-0.*`, `A-0.*` | [core-skeleton.md](core-skeleton.md) |
| `R-1.*`, `A-1.*` | [session-layer.md](session-layer.md) |
| `R-2.*`, `A-2.*` | [rolls.md](rolls.md) |
| `R-3.*`, `A-3.*` | [initiative.md](initiative.md) |
| `R-7.*`, `A-7.*` | [distribution.md](distribution.md) |

- **Old PRD names:** PRD-0 is core-skeleton, PRD-1 session-layer, PRD-2 rolls, PRD-3 initiative,
  PRD-7 distribution.
- **"Decision n"** in a code comment means product-overview "Session panel" item n.
- **Dropped on purpose:** the resolved-escalation series (E-n) and the spec-question ledger (SQ-n)
  were history. Their rulings live in the specs; each spec's `## Retired IDs` lists IDs that no
  longer name a live requirement.
- **History** (tickets, bug reports, the original PRDs) is archived at
  `~/archive/dungeonmasterxiv-agent-team-2026-09-29/`.
- **New changes** get a design spec named `YYYY-MM-DD-<topic>-design.md` in this directory, citing the
  area specs by ID. `research/` holds background notes, not requirements.
