# Core skeleton: product spec

Describes the current product. Converted 2026-09-29 from `PRD-0-core-skeleton.md`; the original is
archived at `~/archive/dungeonmasterxiv-agent-team-2026-09-29/claude/team/product/prd/PRD-0-core-skeleton.md`.

Principles are cited as `product-overview D-n` and are not restated here.

## Purpose

Nothing else can be built until the plugin loads, draws windows, and has somewhere to put settings.
This area contains no product logic on purpose — no dice, no session, no initiative — so that
everything in it is determined by the platform rather than by decisions made elsewhere.

## Requirements

### R-0.1 Plugin identity

- The internal name (`AssemblyName`) is **`DungeonMasterXIV`**. This is permanent: Dalamud keys the
  config directory, log lines and the DLL from it, and its own documentation says it may not be
  changed once set. Getting this wrong is not a rename, it is a migration.
- The display name is **`Dungeon Master XIV`**. Punchline: "Dice, initiative and encounter tracking
  for in-game tabletop sessions." Description and tags follow the product overview. Display name,
  description and tags may change freely; only the internal name may not.

### R-0.2 Loads and unloads cleanly

- The project builds and produces a plugin artifact.
- One class implements `IDalamudPlugin` and implements `IDisposable`.
- Everything subscribed, registered or allocated during construction is unwound in `Dispose()`.
- The plugin can be disabled and re-enabled repeatedly in `/xlplugins` without leaking handlers,
  duplicating windows, or throwing.

**Acceptance criteria**
- **A-0.6** Disabling and re-enabling the plugin five times leaves exactly one of each window, no
  duplicate handlers, and no errors in `/xllog`. This is the criterion that matters most: it is the
  difference between a skeleton that can carry the rest of the product and one that has to be rebuilt.

### R-0.3 Window shell — one panel with a rail of tabs

The UI is **one panel with a rail of tabs attached to its edge**, Foundry's sidebar shape: each rail
button selects what the panel shows. This area delivers the panel and its rail, not the features'
tabs themselves (`2026-10-05-docked-panel-design.md`).

- The panel can be opened, closed, moved, resized and collapsed to the rail alone.
- **Window geometry is Dalamud's to persist, not ours.** Position and size are not stored in the
  plugin's own config. Two sources of truth that could disagree is a defect that would fail silently,
  so it is not created.
- The panel ships with its rail and a **Settings** tab. Feature tabs, such as Chat and Session, add
  their rail button when they ship. The selected button is lit.
- The look and the components every window is built from are set in
  `2026-10-04-ui-foundation-design.md`.
- No placeholder tab or rail button for a feature that does not exist yet. An empty feature tab
  teaches users the feature exists and is broken.

### R-0.4 Commands

- `/dmx` toggles the panel.
- `/dmx settings` opens the panel on its Settings tab.
- Both appear in `/xlhelp` with a useful help string.
- The Settings tab is also reachable from the plugin installer entry, as Dalamud expects.

**Acceptance criteria**
- **A-0.4** `/dmx` opens the panel and `/dmx settings` opens it on the Settings tab; both are
  listed in `/xlhelp`; the panel can be moved, resized, collapsed and closed. The movable-and-resizable
  half is R-0.3's; it is folded into this criterion rather than left living only in a script where a
  later revision could quietly drop it.
- **A-0.3** The plugin appears in `/xlplugins` under its display name (R-0.1) and loads without
  error.

### R-0.5 Settings persistence

- Settings persist across restarts through Dalamud's plugin config mechanism.
- A schema version field is persisted alongside genuine plugin-owned preferences — for example
  whether each window was open, and whether to restore that on load. Window **geometry** is excluded
  (R-0.3) and is the only thing excluded. Open/closed state is plugin-owned — Dalamud does not persist
  it — so persisting it creates no second authority.
- **A schema version is written from the first release.** Retrofitting one onto config already on
  users' disks is painful; adding it from the start is free.
- These are plugin-installation-scoped settings (window layout, preferences), stored once per
  installation. Campaign data is a different store, keyed by a locally generated campaign UUID
  (session-layer R-1.6), not by anything here. Per-character concerns are deferred to the character
  sheets area, where they actually bite.

**Acceptance criteria**
- **A-0.5** After the plugin has loaded once, its config file on disk exists and contains a schema
  version field. (This criterion originally asked whether windows returned to the same place after a
  restart; that property is Dalamud's `WindowSystem`, not this plugin's, so it tested nothing this
  plugin could fail. It now tests the field that is actually this plugin's to get right.)

### R-0.6 Logging

- Uses Dalamud's plugin log. No `Console.WriteLine`.
- Logs loading, unloading and failures. Logs nothing per-frame.
- **No log line ever contains a character name.** Enforced at this layer, because logging is written
  once and copied forever, and identity is never persisted with a character name attached
  (product-overview D-8). Log the session-scoped code instead.

**Acceptance criteria**
- **A-0.7** `/xllog` contains no character name after a load/unload cycle.

## Out of scope

- Any chat reading or `XivChatType` handling (product-overview non-goals; rolls A-2.7).
- Any networking, socket, listener or session concept — that belongs to the session layer.
- Dice notation, initiative, HP and status, the session log, and character sheets each belong to
  their own area.
- A tab for a feature that does not exist yet (R-0.3).
- Update checks or telemetry of any kind (product-overview D-2).
- Persisting window position or size in the plugin's own config (R-0.3).

## Open questions

- None blocking. The identity, window model and command surface are decided.

## Retired IDs

- A-0.1 (the project builds, producing a plugin artifact, with build output shown in the PR):
  the product-observable half is folded into R-0.2; "build output shown in the PR" is a process
  check, not a product property.
- A-0.2 (`dotnet build && dotnet test` reports a non-zero passed count, no skipped assembly, and
  every test assembly in the solution appears in the output): process check, not a product property,
  the same reason session-layer A-1.12b was retired.
