# Initiative: product spec

Describes the current product. Converted 2026-09-29 from `PRD-3-initiative.md`, plus the resolved
escalations that bear on it; the original is archived at
`~/archive/dungeonmasterxiv-agent-team-2026-09-29/claude/team/product/prd/PRD-3-initiative.md`.

Principles are cited as `product-overview D-n`, session requirements as `session-layer R-1.x`, and
roll requirements as `rolls R-2.x`; none is restated here.

## Purpose

Initiative order is the second problem the product exists to solve: everyone can see whose turn it
is, on every admitted client, without anyone keeping it up by hand (product-overview "Problems it
solves"). An encounter is an explicit object the DM creates, populates, starts and ends, with a
roster of combatants each holding a numeric initiative.

## The model: Foundry VTT's combat tracker

Deliberately modelled on Foundry's, per product-overview D-4 and its "not a Foundry system" framing.
Reference: <https://foundryvtt.com/article/combat/>.

**Taken:** an encounter as an explicit object the DM creates, populates, starts and ends. A roster of
combatants each holding a numeric initiative. Roll-all versus roll-for-NPCs, so players roll their
own. A distinct "begin encounter" step separate from adding people. Next-turn and round advancement.
Hidden combatants the DM can see and players cannot. Defeated markers. The DM able to edit any
initiative value directly.

**Not taken, because there is no map:** tokens, scenes, and selecting combatants by clicking them on
a canvas. Combatants here are session participants plus DM-created entries.

**Not taken, because of product-overview D-4:** Foundry derives initiative from the game system's
formula — Pathfinder's Perception, 5e's Dexterity. This product encodes no system; see below. Scenes,
tokens, canvas, vision, lighting, walls and movement are also not taken, for the same reason: FFXIV is
the map.

## How initiative is determined

**A flat `d20`, plus an optional modifier the participant types themselves.** The plugin never knows
what a Perception score is, never reads a sheet, and never computes a bonus (product-overview D-4).

The modifier is a field where a human types a number they already know, not a rules engine — the
plugin still knows nothing about where the number came from. Nearly every tabletop system gives
initiative a bonus, and a tracker that cannot express `d20+4` forces arithmetic in the player's head
and a correction request to the DM.

## Requirements

### R-3.1 The encounter

- The DM creates an encounter. It exists in the session and is visible to every admitted participant
  (the DM's client is authoritative for it: product-overview D-3).
- Creating an encounter is separate from **starting** it. A DM assembles the roster, gets initiative
  in, and then begins.
- The DM ends an encounter. Ending is explicit and announced, never implicit.
- One active encounter per session. Sequential encounters are fine; concurrent ones are not.

**Acceptance criteria**
- **A-3.6** The DM creates an encounter and it appears on every admitted client.

### R-3.2 The roster

- Participants in the session can be added as combatants.
- The DM can add **DM-controlled combatants** — monsters, NPCs — which are entries with a name and no
  participant behind them. A DM running six goblins adds six entries.
- Each combatant shows: display name, initiative value, and status markers. No portraits or
  thumbnails.
- The DM can remove a combatant mid-encounter.

### R-3.3 Rolling initiative

- A participant rolls their own initiative — `d20` plus their typed modifier — and it lands in the
  roster.
- The DM can roll for all DM-controlled combatants at once ("roll NPCs"), and can roll for everyone
  if the table prefers speed over ceremony.
- The DM can set or edit any initiative value directly, including a participant's. Tables do this
  constantly, and refusing it would make the tool an obstacle.
- An edited value is visibly marked as DM-set rather than rolled, so the table can see why a number is
  not what the dice said.

**Acceptance criteria**
- **A-3.7** A player rolls initiative and their position updates on every client without the DM
  editing anything.
- **A-3.9** A DM-edited initiative value is visibly marked as DM-set rather than rolled.
- **A-3.9a** A rolled value is **not** marked DM-set. A-3.9 alone tests one direction, so a build that
  marks every value DM-set satisfies it; both directions fail separately, and this needs a rolled
  value present in the same encounter as an edited one.

### R-3.4 Order and ties

- Combatants order by initiative, highest first.
- Ties hold their tied value and order by which roll the host received first (product-overview D-3).
  Deterministic on every client, no coin flips, no divergence.
- The DM can reorder tied combatants manually. Most tables resolve ties by GM fiat, so that is the
  affordance rather than an automatic rule nobody would agree with.

**Acceptance criteria**
- **A-3.1** Combatants order by initiative descending; tied values order by host receipt order,
  identically on two clients fed the same events in different local timing.

### R-3.5 Turns and rounds

- A current combatant is highlighted on every client.
- Next turn advances. Passing the last combatant increments the round and returns to the top.
- The round number is visible.
- A player may end their own turn. DM-controlled combatants' turns are ended by the DM. This keeps
  players moving without letting anyone advance past someone else.
- The DM can jump to any combatant, and can step back a turn.

**Acceptance criteria**
- **A-3.2** Next turn advances one combatant; advancing past the last increments the round and
  returns to the first.
- **A-3.8** Advancing the turn moves the highlight on every client.
- **A-3.10** A player can end their own turn; a player cannot end a DM-controlled combatant's turn.

### R-3.6 Hidden and defeated

- **Hidden combatants** appear on the DM's tracker and not on players'. This is what makes the
  tracker usable for an ambush or an unrevealed enemy.
  - This is not a boolean; it is product-overview D-13's four-level access model. A hidden combatant
    is one on which players are at **None**; revealing it raises them to **Observer**. **Limited** is
    also available: the party knows *something* is in the initiative order without knowing what.
  - None means absent from the payload (product-overview D-13). An unrevealed combatant must not be
    inferable from the roster's length, the round order, a gap in the turn sequence, or any other
    channel a client's received stream carries.
- **Defeated** is a marker the DM sets. A defeated combatant stays visible, marked, and by default is
  skipped when advancing turns.
- Neither marker means anything mechanically. The plugin records what the DM decided
  (product-overview D-4).

**A-3.4 and A-3.11a are the pair that matters, and they are deliberately separate.** A-3.4 tests the
wire; A-3.11a tests what a client's received stream can be used to infer. A hidden combatant filtered
client-side passes any UI inspection and leaks to anyone reading the traffic — and the whole point of
the feature is that the players do not know the thing is there.

**Acceptance criteria**
- **A-3.3** A defeated combatant is skipped by default when advancing.
- **A-3.4** A combatant on which players are at None is absent from the payload sent to their
  clients — not merely hidden in their UI. Verified against what crosses the wire (product-overview
  D-13).
- **A-3.4b** A combatant at Limited appears in the players' order without its name or details;
  raising it to Observer reveals it.
- **A-3.11a** Nothing a client at None receives lets it infer the hidden combatant exists — not a
  count, not a gap in an ordering, not a timing, and not any other channel. Assessed over what the
  client receives, not what it displays. The list above is illustrative, not exhaustive, and this
  criterion is written over the whole received stream so a further channel is covered without
  amending it.
- **A-3.11b** With a hidden combatant present, nothing on a player's screen reveals its existence.
  This is the rendering half; A-3.11a is the one that can catch client-side filtering.

### R-3.7 Reconnect and late arrival

- A participant joining or reconnecting mid-encounter receives the current roster, initiative values,
  current turn and round — not an empty tracker (session-layer R-1.4, product-overview D-3).
- Hidden combatants are not sent to player clients on reconnect either. The obvious way to leak this
  is to send everything and filter in the UI; that is not permitted.

**Acceptance criteria**
- **A-3.5** A client joining mid-encounter reconstructs roster, initiative, current turn and round,
  with hidden combatants still absent.

## Out of scope

- Any system's initiative formula (product-overview D-4). No Perception, no Dexterity, no ability
  scores. A number the human types is fine; a number the plugin derives is not.
- Conditions, statuses and HP belong to the HP-and-status area.
- Maps, tokens, positioning, distance and movement (product-overview D-4: FFXIV is the map).
- Automatic turn timers, reminders or nudges are not built. This is a scope choice, not a technical
  limitation: a tracker that nags is a tracker people close. It remains open to reconsideration.
- Reading game chat, in any form (product-overview non-goals; rolls A-2.7).

## Open questions

- Open question: should a participant control more than one combatant — a player running a companion
  or summon? Common in Pathfinder. Not decided; blocks nothing built so far.
- Open question: does initiative persist between encounters in the same session, or reset? Assume
  reset until decided.
- Open question: does a DM-controlled combatant need to be attributable to a specific character the
  DM is playing, for a session-log entry to read well? Also raised in the rolls area. Blocks the
  session-log area.

## Retired IDs

- A-3.11 (with a hidden combatant in the encounter, nothing on a player's screen reveals its
  existence): superseded by A-3.11a and A-3.11b. It enumerated three inference channels and omitted
  timing, and it was scoped to a player's screen (UI inspection), which passes exactly the
  client-side filtering that leaks to anyone reading the traffic.
