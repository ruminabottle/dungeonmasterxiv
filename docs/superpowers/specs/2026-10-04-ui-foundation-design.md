# UI foundation: a Chronicle look, a launcher rail, the stream on screen, rolls on the wire

Date: 2026-10-04.

## Why

The plugin draws stock Dalamud ImGui: three windows of `TextUnformatted`, `Button` and `Separator`
with no shared styling, components or layout. Two gaps sit behind that look:

- **The session stream is recorded but never shown.** The host keeps `Recorded` and a player keeps
  `Received`, but the session window only offers the message box. A player cannot see what anyone
  said.
- **The stream is also incomplete.** The host records messages and departures, but not joins, lost
  connections or reconnections, and it sends departures to nobody.
- **The DM cannot send at all.** The message path is the member's, sealed to the host, so the host
  has no route of its own.
- **`/roll` never leaves the roller's machine.** It prints a total as a status line.

Every coming feature (initiative, combatants, private messages, roll modes, character sheets) needs a
window. Without a shared design language and component layer, each one hand-builds its own cards and
badges and the product drifts.

The goal: one visual identity, a small set of components every feature window is built from, the
current windows rebuilt on them as proof, the stream on screen, and public rolls reaching everyone.

## Decisions

### 1. Identity: Chronicle

- **Ink on dark vellum, with FFXIV in the details.** Dark warm surfaces, thin gold rules like the
  game's menus, engraved serif titles, a book-like serif for in-character speech, and the game's own
  Axis font for everything else. It is the plugin's own space, not a skin of the game's HUD.
- **Our windows only.** The theme applies while one of our windows draws and is undone after it.
  The player's Dalamud style and other plugins are never changed.
- **ImGui cannot draw textures cheaply,** so the identity lives in colour, type and rules, not
  images.

### 2. Design language

Every value has a role name. Components use roles, never raw values.

**Colour roles.** Ratios are WCAG contrast against each surface.

| Role | Value | Used for |
| --- | --- | --- |
| `Surface` | `#17140F` | window background |
| `SurfaceRaised` | `#221D15` | cards, title bar |
| `SurfaceSunk` | `#120F0B` | formula and total bars, inputs |
| `Rule` | `#6B5631` | decorative borders, card edges |
| `RuleSoft` | `#3A3020` | separators inside cards |
| `RuleStrong` | `#8A7040` | edges of interactive controls (≥ 3.6:1, meets the 3:1 non-text bar) |
| `Text` | `#E9DFC9` | body (≥ 12.6:1) |
| `TextMuted` | `#B5A888` | timestamps, hints, event lines (≥ 7.1:1) |
| `Gold` | `#C9A65A` | active state, focus, primary edge (≥ 7.2:1) |
| `GoldBright` | `#F2D891` | roll totals (≥ 12.0:1) |
| `GoldLabel` | `#D8BF86` | titles, section labels, badges (≥ 9.3:1) |
| `PrivateSurface` / `PrivateRule` / `PrivateText` | `#1F1A22` / `#6A5478` / `#CFC2DC` | private content, when built (text ≥ 9.9:1) |
| `Warning` | `#D9824A` | reconnecting, code changed (≥ 5.8:1) |
| `Danger` | `#D9665A` | errors, refusals, End session (≥ 4.8:1) |

- **Gold means "current or important" only,** never decoration.
- **Colour is never the only signal.** Away, private, DM and danger always carry a word, badge or
  icon too.
- `Rule` and `RuleSoft` are decorative and exempt from the 3:1 bar. Anything a person must find in
  order to operate it uses `RuleStrong` or `Gold`.

**Type roles.**

| Role | Font | Used for |
| --- | --- | --- |
| `Body` | Axis (the game's), 14 | everything by default |
| `Meta` | Axis, 12 | timestamps, tags. Nothing is drawn smaller. |
| `Title` | Cinzel, 15 | window titles, card headings, section labels |
| `Total` | Cinzel bold, 24 | roll results |
| `Voice` | Spectral, 16 | in-character speech and narration |

- OOC renders as `Body` italic in `TextMuted`. An emote renders as `Voice` italic. Both wait for
  those kinds to be built (rolls R-2.5).
- Every size follows Dalamud's global font scale.

**Spacing and shape.**

- 4px grid, using steps 4, 8, 12 and 16.
- Cards have 10px padding and an 8px gap between them.
- Controls are rounded 4px and windows 6px.

**States.** Every interactive component defines default, hover, active, focused and disabled:

- hover raises the surface one step;
- active uses `Gold`;
- focus draws a `Gold` outline;
- disabled draws at 50% and has a tooltip saying why.

### 3. Components

Each component is a small static draw function. It takes plain data (strings, a role, a time) and
draws itself in the theme. It never reads session state. Behaviour stays in the calling view.

**Built now.** The rebuilt windows use these:

| Component | Shape |
| --- | --- |
| `Speaker` | One line. `Eli (Tuka)`, or the person alone when speaking as themselves (rolls R-2.7). The parenthetical is never dropped, at any width. A host line starts with the `[DM]` marker from the session role (rolls R-2.7a), drawn as a `RoleBadge` whose text is literally `[DM]`. |
| `RoleBadge` | A small outlined `GoldLabel` tag. Host: `[DM]`, the only bracketed text in the product. Assistant: `Assistant`, no brackets, never styled as the host (A-2.24b). Player: no badge. Never in parentheses (session-layer A-1.13d). |
| `MessageCard` | `Speaker` and time on the top line, then the text in `Body`. |
| `EventLine` | A membership event as one compact `TextMuted` line, not a card: "Eli (Tuka) lost connection · 21:06". A gap draws as a `RuleSoft` rule reading "Some messages were not held." |
| `RollCard` | `Speaker` and time, then the label as a `Title` heading (when given), the expression on a `SurfaceSunk` bar, every die (kept dice plain, set-aside dice struck through and muted), and the total in `Total` on its own bar. The "no dice survived" notice is written in words (A-2.3b). A local roll adds "Only you saw this." |
| `Banner` | Info, warning or danger: a 3px left edge in `Gold`, `Warning` or `Danger`, an icon, and wrapped text. |
| `ActionRow` | Buttons in a row. Primary has a `Gold` edge, secondary a `RuleStrong` edge. Danger has `Danger` text and edge and asks for confirmation on the first press. |
| `RosterRow` | `Speaker`, `RoleBadge`, and the word "away" in `TextMuted` while disconnected. |
| `CodeDisplay` | The session code in `Title` at 20, with a Copy button (session-layer R-1.3i). |
| `EmptyState` | What will appear here and how to make it appear, in one or two lines. |
| `RailButton` | An icon with a tooltip. Lit (`SurfaceRaised` with a `Gold` edge) while its window is open. |
| `Section` | A `Title` small-caps label over a `RuleSoft` rule, optionally collapsible. |

**Specified only.** These are designed here and built with their feature, so feature specs cite them
instead of designing their own:

| Component | Shape | Built with |
| --- | --- | --- |
| `PrivateCard` | A `MessageCard` or `RollCard` on `PrivateSurface` with a `PrivateRule` edge. For someone not entitled to a private roll: a placeholder line ("Mara (Copy) rolled for the DM") and a "?" bar; a private message leaves nothing (`2026-10-06-private-messages-and-roll-modes-design.md` decision 3). | private messages and roll modes |
| `RollModeSwitch` | The audience row above the composer, for messages and rolls: icon buttons with tooltips, styled as `RailButton`s. Players: Public, DM only, Blind. The DM: Public, DM only, To ▾. Copy follows rolls R-2.15: never "secret", "hidden" or "secure" (`2026-10-06-private-messages-and-roll-modes-design.md` decision 2). | private messages and roll modes |
| `RollButton` | A `[[/roll]]` button inside a message. Greyed, with "for Eli, Mara", when addressed to others (product-overview Session panel item 9). | clickable rolls |
| `RemovedMarker` | An `EventLine` reading "Message removed by the DM" in the deleted message's place (product-overview Session panel item 8). | moderation |
| `InitiativeRow` | `Speaker`, initiative value, and the current turn marked in `Gold` with a ▸. | initiative |

### 4. Windows

> **Superseded by `2026-10-05-docked-panel-design.md`:** the rail is now attached to one panel and
> switches its tabs (Chat, Session, Settings). The content each window held below moves into those
> tabs; the components and behaviour stand.

**The rail replaces the main window.**

- A small borderless window: a column of `RailButton`s and a grip to drag it by. Dalamud keeps its
  position (R-0.3).
- It holds **Session** and **Settings**, with Settings pinned to the bottom, and ◂ to collapse it to
  one button. A button exists only for a window that exists, so Initiative appears when initiative
  ships.
- Collapsed or expanded is remembered in our settings, as window-open state is today.
- `/dmx` and Dalamud's open-main-UI button toggle the rail. `/dmx settings` opens Settings.
- The first time the rail opens, the session window opens beside it.
- core-skeleton R-0.3 and R-0.4 change to match.

**Session window, top to bottom.**

1. **Title:** the campaign name while hosting one, otherwise "Session".
2. **Not in a session:** an `EmptyState` with two paths, side by side when the window is wide enough
   and stacked when it is not.
   - **Host:** the campaign picker and Start.
   - **Join:** the code and name form.

   This replaces today's stacked "Hosting: not hosting" and "Joining: not in a session" lines.
3. **Hosting:**
   - `CodeDisplay`;
   - a collapsible **At the table** `Section` with `RosterRow`s and the returning-players choice;
   - **End session** as a danger action.
4. **Joined:** the status "In the session" and the same **At the table** section, without host
   controls.
5. **Banners:** reconnecting, code changed, failures and undelivered messages. They stack under the
   header and never cover the stream. Existing wording is kept.
6. **Admission requests (host only):** a card per request, pinned above the stream, with the name,
   the countdown and an `ActionRow`. The favoured action (`AdmissionPrompt.Favoured`) is the primary
   button and keeps default focus.
7. **Stream:** a scrolling region of `MessageCard`s, `RollCard`s and `EventLine`s.
   - It holds only what this client was sent live (log history is never sent).
   - It draws the latest 300 entries, with a note when there are more. The log keeps them all.
   - It stays pinned to the newest entry. Scrolled up, it holds still and shows "New below ↓".
8. **Composer:** one input and Send.
   - `/roll` in a session sends a public roll (decision 5).
   - `/roll` outside a session rolls locally and adds a local `RollCard`.
   - A refusal shows as a `Danger` line under the input.
   - The input is disabled while reconnecting, as today.

**Session ending.** The keep-or-lose and export offer becomes a card at the end of the stream. It
stays offered at session end and is never buried (product-overview Session panel item 4).

**Settings window.**

- The contents and behaviour are unchanged.
- Grouped into `Section`s: **You** (display name, window restore), **Connection** (relay address,
  policy link), **Campaigns** (storage), **Stored data** (relink memory).
- Warnings become `Banner`s.
- **No copy changes.** Existing wording carries spec obligations (session-layer R-1.7a and the honest
  limitations in product-overview).

### 5. The stream on the wire: membership, the DM's own lines, public rolls

- **Membership events are announced.** The host stamps joined, left, lost connection and reconnected
  as stream entries and sends each to every admitted member, as it does a message.
- **The DM speaks and rolls directly.** A host message or roll is checked by the same rules as a
  member's and stamped straight into the host's stream, then sent to every member.
- **Catch-up never nears the relay's frame limit.** The relay closes a connection that sends more than
  64 KiB in one frame. The missed lines a reconnecting member is owed go out in frames of at most
  24 KiB of content.

- **The roller rolls** (rolls R-2.2). In a session, `/roll` evaluates locally as today. A new
  optional `Rolling` field on `SessionContent` then goes to the host on the existing sealed member
  path, queued like a message while the path is down. It carries:
  - the expression and label;
  - every individual die, each with its sides, value and whether it was kept;
  - the total;
  - the no-survivor notice.

  The host's own roll goes straight to its sequencer.
- **Malformed or over-bound input never leaves.** A refused expression shows its message under the
  input, and nothing is sent (A-2.3).
- **A shared roll carries at most 500 dice.** That keeps its frame well under 64 KiB. A larger roll is
  refused with a message giving the dice count and the cap, and nothing is sent. Outside a session the
  cap does not apply.
- **The host checks bounds and never re-rolls.** It caps the die count and sides at `RollLimits`, and
  the expression and label at a length cap of its own. A payload over a bound is refused under
  product-overview D-22, as the inbound message bound is today (A-2.36 to A-2.40):
  - recorded locally;
  - never attributed;
  - nothing in the stream;
  - the content not kept.
- **The host sequences a valid roll** into a `Roll` stream entry. The roll data travels on the
  `StreamLine` alongside the existing fields, and `Text` carries a plain summary
  (`1d20+4 = 17 [13]`), so the log and export projection keep working unchanged.
- **Receivers draw the dice exactly as received** and never re-evaluate the expression. That is
  A-2.18, and it means no client evaluates a stranger's formula.
- **Version skew is accepted pre-release.** The JSON codec ignores unknown fields, so an older host
  drops a newer member's roll without telling anyone. There is no protocol version bump; everyone
  updates from the same repository.

### 6. Code shape

- `Windows/Ui/Theme.cs`: the roles from decision 2, applied as a scope that undoes itself on every
  exit path. A window that throws mid-draw must not leak our style into other plugins.
- `Windows/Ui/UiFonts.cs`: font handles from `UiBuilder.FontAtlas`.
  - Axis comes from the game.
  - Cinzel Regular and Bold and Spectral Regular (about 400 KB) are bundled in `Data/Fonts/` with
    their SIL Open Font Licence files and shipped in the release zip. The player's language glyphs
    are merged into each, so Japanese and other scripts still render.
  - **A font that fails to load** falls back to Dalamud's default for that role, logged once. The UI
    still works.
- `Windows/Ui/ThemedWindow.cs`: a `Window` base that applies theme and fonts in `PreDraw`
  (so the title bar is themed) and undoes them in `PostDraw`. Every plugin window derives from it.
  A window that throws while drawing logs once and shows a one-line notice instead of its content.
- `ImRaii`'s colour and style pushes need the `Dalamud.Bindings.ImPlot` reference, supplied by
  Dalamud and never copied into the plugin output.
- `Windows/Ui/Components/`: one file per component in decision 3's built-now table.
- `Windows/RailWindow.cs` replaces `MainWindow.cs`. `SessionWindow` and `ConfigWindow` are rebuilt
  from components. Their sub-views (`JoinFlowView`, `AdmissionPromptView`, `MessageComposeView`,
  `SessionEndingView`, `CampaignStorageView`, `RelinkMemoryView`, `HostCampaignPicker`,
  `JoinRequestForm`) keep their behaviour and draw through components. `RosterView` becomes
  `RosterRow`.
- Core: the `Rolling` content and its codec, the member send path, host bounds and sequencing, the
  roll fields on `StreamLine`, membership announcements, the host's own send path, and batched
  catch-up.

## Out of scope

- Private messages and roll modes: the next spec, built on `PrivateCard` and `RollModeSwitch`.
- `/ooc` and `/me` message kinds (rolls R-2.5).
- The game-chat echo (rolls R-2.13).
- Clickable `[[/roll]]` buttons and inline `[[2d6]]` (rolls R-2.8, R-2.9).
- Registering `/roll` as a game-wide command (rolls R-2.18).
- NPC speakers (rolls R-2.7).
- Portraits, dice animation and any textured art.
- Native game UI through KamiToolKit. It was considered and rejected for the foundation: it is
  harder for complex layouts, breaks on game patches, and gives up the Chronicle identity.

## Verification

The smoke-tests-only rule applies: one new smoke test for the new path, nothing else.

- **The new smoke test:** a member's roll reaches a different member with every die. The chat smoke
  test now filters to message lines, because members also receive join lines.
- The build passes with no new warnings, and the smoke set passes.
- In the game, by eye:
  - every session-window state: not in a session, hosting, joined, reconnecting, admission pending,
    ended;
  - the rail lighting and collapsing;
  - settings in each group;
  - Dalamud font scale at 1.0 and 1.5;
  - a missing font file falling back.
- Two clients through a relay:
  - a player's `/roll 4d6kh3+2` appears for the host and for a third client with the same individual
    dice (A-2.8, A-2.18, A-2.1);
  - the DM's message and roll appear for every player;
  - `/roll 600d6` in a session is refused with the cap named, and nothing is sent;
  - a membership change appears timed in the stream (A-2.27);
  - a host line carries `[DM]`, and an Assistant does not (A-2.24a-1, A-2.24b).
- Disable and re-enable the plugin five times: exactly one of each window and no style leaking into
  other plugins (A-0.6).
- The release zip contains the fonts and their licences, and the manifest smoke test passes.
