# Modelling on Foundry VTT

Product Owner research, 2026-08-26, at the human's direction: *"I love the way Foundry VTT runs.
I would love to model everything after how it is."*

Sources: [Combat](https://foundryvtt.com/article/combat/) · [Dice](https://foundryvtt.com/article/dice/) ·
[Actors](https://foundryvtt.com/article/actors/) · [Journal](https://foundryvtt.com/article/journal/) ·
[Users](https://foundryvtt.com/article/users/)

---

## The framing that makes this safe: we model Foundry *core*, never a system module

Foundry itself contains **no game rules**. It ships a core — actors, combat tracker, dice, journals,
permissions — and *game systems* (Pathfinder 2e, D&D 5e) are separate packages layered on top that
supply formulas, sheets and mechanics.

That split is exactly D-4's line, drawn by someone else and proven over years. **Everything we take
from Foundry comes from core. Nothing comes from a system.** When a Foundry feature seems appealing
and turns out to live in a system module, that is the signal to stop, not to reimplement it.

This resolves what otherwise looks like a contradiction between "model everything after Foundry" and
"no rules engine". Foundry's own architecture already made that decision.

---

## What maps, and where it lands

| Foundry concept | Our equivalent | Status |
| --- | --- | --- |
| Combat tracker | PRD-3 | **Already modelled** — encounter object, roster, roll-NPCs, next turn, rounds, hidden, defeated |
| Dice roller | PRD-2 | Partly — we have `XdY+Z` and labels. **Roll modes are missing; see below** |
| Actor | PRD-6 character sheets | Not written. Foundry's ownership model is the piece to take |
| Journal | PRD-5 session log | Not written. **Different from ours in an important way; see below** |
| Users and roles | PRD-1 admission | Partly. Foundry's role tiers answer our open question on co-DMs |
| Document ownership | Cross-cutting | Not modelled at all. **The biggest single idea available to us** |
| Scenes, tokens, canvas, maps | — | **Cannot take.** No map. This is the boundary of the analogy |
| System packages, modules, compendia | — | **Must not take.** D-4 |

---

## The three findings worth acting on

### 1. Roll modes — take these, they answer an open question

Foundry has four, and they are a solved design:

| Mode | Who sees the result |
| --- | --- |
| **Public** | Everyone |
| **GM** | The roller and the DM. Either may reveal it |
| **Blind** | The DM only — **the roller does not see their own result.** Only the DM may reveal |
| **Self** | The roller only. Only they may reveal |

PRD-2 has an open question: *"Should a DM be able to roll on behalf of a participant, or privately?
Common at real tables (hidden perception checks)."* **Blind is the answer to that**, and it is
better than what I would have invented. A DM asks for a Perception check, the player rolls, and
neither the player nor the table learns whether they succeeded — which is the entire point of a
hidden check and cannot be faked by rolling in secret yourself.

Note what makes Blind work: **the reveal is an action someone takes later**, not an absence. A
result that is hidden forever is just a lost roll.

**Recommendation:** add all four modes to PRD-2. They are cheap next to the transport, they are
orthogonal to dice notation, and the alternative is a DM whispering in Discord.

### 2. Document ownership — the single biggest idea, and we have nothing like it

Foundry gives every document four access levels **per user**: **None / Limited / Observer / Owner**.
A GM grants a player Observer on an NPC sheet and they can read it but not change it. Limited shows
the entry exists without its contents.

We currently have exactly two states — in the session or not — and we have already hit the gap three
times without recognising it as one gap:

- PRD-1 open question: *may a player share their character sheet with the DM?* → grant the DM
  Observer on it.
- PRD-3's hidden combatants → the players' level on that combatant is **None**.
- PRD-6's sheets, unwritten, will need exactly this and would otherwise invent a worse version.

**Recommendation:** adopt the four levels as a cross-cutting concept before PRD-6 is written, rather
than accreting three incompatible visibility mechanisms. This is a genuine architecture decision and
it should be a directive, not a per-PRD choice. **It is also the last cheap moment** — PRD-3 will
ship a hidden/visible boolean if nobody says otherwise.

### 3. Roles — Foundry answers our co-DM question

Foundry has **Gamemaster**, **Assistant** (most in-game powers, no world settings), **Trusted**,
**Player**, and **None** (blocks a user entirely).

PRD-1 asks: *may a session have more than one DM?* Foundry's answer is **Assistant** — a second
person who can run combat, roll for NPCs and reveal blind rolls, but cannot end the session or
change who is admitted. That is a better answer than "yes" or "no", and it matches how a co-DM
actually behaves at a table.

**None** is also worth noting: Foundry uses it to block a user. Ours is admission-refusal, so we
have this already under a different name.

---

## What we take from the Journal — and the one place ours must differ

Foundry's Journal is **authored notes**: handouts, lore, NPC descriptions, written by the GM ahead of
time, with a **"Show Players"** action that pushes a page onto everyone's screen mid-session.

PRD-5's session log is an **automatic record** of what happened — rolls, turns, HP changes. **These
are different products and should not be merged.** A journal you can edit is not a record of events,
and a record of events you can edit is not a record.

**But "Show Players" is worth stealing on its own.** A DM pushing a piece of text onto the table's
screens at a dramatic moment is exactly the kind of thing this plugin exists for, and it costs
little on top of the transport we are already building.

**Recommendation:** keep PRD-5 as the immutable record. Consider a small separate "handouts" feature
later, and do not let it creep into PRD-5.

---

## Where the analogy stops, and saying so matters

Foundry is a **virtual tabletop**: its centre of gravity is a **map with tokens on it**. Scenes,
token vision, lighting, walls, measured templates, drag-to-move — that is most of what Foundry *is*,
and none of it applies to us. Our players are already standing in a shared 3D world; FFXIV is the
map.

So "model everything after Foundry" means: **take its non-map half.** The combat tracker, dice,
permissions, roles, journals — the parts a GM uses that happen to be adjacent to the canvas rather
than on it. That half is well-designed, battle-tested, and almost entirely applicable.

The map half is not a gap in our product. It is the reason our product can exist inside a game.

---

## Recommended decisions

Ordered by how expensive they are to defer.

1. **Adopt the four ownership levels as a cross-cutting concept, now, before PRD-6.** Cheapest
   today; three features will otherwise each invent their own visibility rule. Should be a directive.
2. **Add the four roll modes to PRD-2.** Closes an open question with a proven answer.
3. **Adopt Assistant as the co-DM answer in PRD-1.** Closes another open question.
4. **Keep PRD-5 as an immutable record**, and treat "Show Players" handouts as a separate, later,
   optional feature.
5. **Write PRD-6 against Foundry's Actor model** — a sheet with per-user access — rather than
   inventing sheet sharing from scratch.

## What this does not change

D-1, D-2, D-3, D-8 and D-11 are untouched. Nothing here needs the plugin to read chat, send into the
game, store anything on the relay, or learn a game system. Foundry's core is system-agnostic, which
is precisely why it is safe to copy.
