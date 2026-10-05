# Docked panel: one window, a rail attached to it, tabs that switch

Date: 2026-10-05. Amends `2026-10-04-ui-foundation-design.md` §4 and core-skeleton R-0.3 and R-0.4.

## Why

The first look in game showed the window shape is not what was meant:

- **The rail floats on its own.** The UI spec made it a separate window that opens other windows,
  which sit beside it only the first time. The mockup that was approved showed the rail attached to
  the panel's edge, as in Foundry's sidebar. The spec's wording, not the request, caused the gap.
- **Session controls crowd the chat.** Hosting, joining, the code, the table and admission requests
  sit above the stream in one window, so the conversation gets what is left.

Three bugs showed up in the same look:

- **The settings name box cannot be cleared.** It refills every frame from an old global name, and
  the edit is refused, so a deletion snaps back.
- **A failed "Start session" leaves its campaign marked as current,** so settings behaves as if a
  campaign is open when none is.
- **Headings are smaller than the text under them.**

A fourth issue is not a bug but scolds the user: pressing Send on an empty box shows a red refusal.

## Decisions

### 1. One panel, with the rail on its edge

- **The plugin has one window, the panel,** titled "Dungeon Master XIV". It keeps the old main
  window's `###dmx-main` ID, so Dalamud's stored position carries over.
- **The rail is a column of `RailButton`s inside the panel, on its left edge,** full height. It is no
  longer a window of its own.
- **Each rail button selects a tab.** The panel shows that tab's content to the right of the rail,
  and the selected button is lit. The tabs are:
  - **Chat:** the stream and the message box;
  - **Session:** everything about hosting and joining;
  - **Settings:** today's settings sections, unchanged.
- **A button exists only for a tab that exists.** Initiative, when it ships, adds a tab and its
  button.
- **Collapsing.** ◂ at the bottom of the rail collapses the panel to the rail alone, and ▸ restores
  it. A collapsed panel takes no more room than the rail.
- **What is remembered.** The selected tab and the collapsed state are kept in the plugin's settings,
  as window-open state is today. Position and size stay Dalamud's to persist (R-0.3).
- **The separate Session and Settings windows are removed.** Their content moves into the tabs as it
  is, built from the same components.

### 2. What goes where

- **Chat tab:**
  - the stream, with everything spec §4 item 7 describes;
  - the message box;
  - the reconnecting banner, above the stream, so a person mid-conversation sees it;
  - the keep-or-lose offer, as a card at the end of the stream as now.
- **Session tab,** top to bottom:
  - not in a session: the empty state with the Host and Join paths;
  - hosting: the code, the code-changed warning, At the table with the returning-players choice,
    then End session;
  - joined: the status, At the table, then Leave session;
  - for the host: admission request cards;
  - the failure and undelivered banners.
- **Settings tab:** the four sections from spec §4, with the wording unchanged.
- **Session-state banners show on both tabs.** The reconnecting banner, the closing countdown
  ("The DM has ended this session…") and the undelivered-messages banner are not Chat-only or
  Session-only: each draws above the stream on Chat and at the top of the Session tab, since either
  tab can be the one a person is looking at when the state changes.

### 3. A join request reaches the DM on any tab

- **A badge on the Session button** shows how many requests are waiting: a small `Gold` count on
  the button's corner. It is gone when none wait.
- **A notice above the stream on the Chat tab** reads "Eli (Tuka) is asking to join" for one
  request, or "2 people are asking to join" for several, with a **Review** button that switches to
  the Session tab. It is a `Banner`, not a stream entry. Admission mechanics stay out of the stream
  (rolls R-2.3): the notice is drawn locally on the host and is never recorded, sent or exported.
- **Neither moves the panel.** The DM switches tabs when they choose.
- The admission cards themselves, with their countdown and buttons, stay on the Session tab.

### 4. Commands

- `/dmx` toggles the panel.
- `/dmx settings` opens the panel on the Settings tab.
- Dalamud's "open plugin" and "settings" buttons do the same two things.

### 5. The bugs

- **A failed start clears its campaign.** When hosting stops for any reason, after a failure or by
  the DM's choice, the hosting campaign is cleared, so settings sees no open campaign unless a
  session is live.
- **The name box can be cleared and edited.**
  - With no campaign open, the box shows the character name greyed out with the "a name is saved
    with a campaign" notice. It never shows the old global name it can't change.
  - With a campaign open, what is typed stays typed. Clearing the box clears the campaign's name, so
    the character name is used.
  - The stuck global value is not deleted from the user's settings file, because it is their data.
    It simply stops being shown where it cannot be edited.
- **Headings are at least as tall as the text under them.** The type roles become:
  - `Title` (Cinzel): 18;
  - `Code`: 24;
  - `Total`: 28;
  - Body and Meta (Axis 14 and 12) stay as they are.

  A heading must not render shorter than Body. That is checked by eye in game, at font scale 1.0.
- **An empty send does nothing.** Send, or Enter, on an empty or whitespace-only box changes nothing
  and shows nothing. Other refusals (too long, not in a session, a roll the session can't carry)
  still show as today.

## Spec changes

- **core-skeleton R-0.3:** "several small windows" becomes "one panel with a rail of tabs". The
  amended rule:
  - the panel is the plugin's window;
  - feature tabs add their rail button when they ship;
  - no placeholder tab or button exists for a feature that does not;
  - window geometry stays Dalamud's to persist.
- **core-skeleton R-0.4:** `/dmx` toggles the panel; `/dmx settings` opens it on the Settings tab.
  **A-0.4** becomes:
  - `/dmx` opens the panel and `/dmx settings` opens it on the Settings tab;
  - both are listed in `/xlhelp`;
  - the panel can be moved, resized, collapsed and closed.
- **UI foundation spec §4** is superseded by decisions 1–3 here. Its components, design language and
  stream behaviour are unchanged, apart from the type sizes in decision 5.
- **The product-overview Session panel item "The panel is our own window, and it is primary"**
  still holds: the panel is that window.

## Out of scope

- Popping a tab out into its own window, Foundry's pop-out. It was considered; one panel covers the
  need for now.
- Moving the rail to the other edge or making it configurable.
- Any change to what the session, stream or settings do. This is layout, plus the four fixes above.

## Verification

The smoke-tests-only rule applies:

- **The two Core bugs get covered by the smallest tests that fail before their fix:** the hosting
  campaign cleared after a failed start, and a cleared name saved as empty.
- **Everything else is checked by eye in game:**
  - `/dmx` and `/dmx settings`;
  - each tab;
  - collapse and expand;
  - the selected tab remembered across a reload;
  - the badge and Chat notice with a second client asking to join, and both gone after Admit;
  - headings no shorter than body text at scale 1.0 and 1.5;
  - an empty Send showing nothing;
  - the name box clearing;
  - five disable/re-enable cycles (A-0.6).

## Status and what's next

Status on 2026-10-05: built and shipped in v0.1.9 (PRs #283 and #284). The final review's
fixes are in: the keep-or-lose offer shows on Session, and the session-state banners show on both
tabs (§2).

**Next for this spec**

1. **The in-game checks** listed under Verification. Nothing has been checked in game yet. Also
   check two things by eye: whether a one-digit badge looks smaller than "9+", and whether the
   collapsed panel's title bar is cramped by the close and pin buttons. If it is, hide the title
   bar while collapsed and bring back a grip.
2. **Deferred minors**, each small:
   - The very first open, and a reload while collapsed, don't give the remembered size.
     `FirstSize` should apply with `ImGuiCond.FirstUseEver`.
   - The rail column is about 24px wider than its buttons.
   - An empty Send clears an earlier refusal message.
   - A campaign whose name was cleared under the old code offers the carried-over name once more.
     One more clear makes it stick.
3. **Copy:**
   - The admission prompt's headline reads "Name (ABCD)", which puts a peer code in parentheses.
     Parentheses are reserved for the person behind a speaker (R-2.7). This was there before this
     spec and needs its own fix.
   - "Reopen windows where I left them" is plural, but there is now one panel.

**Elsewhere**

- **The relay's TLS certificate expires on 25 November 2026**, and nothing renews it yet. Do this
  first.
- **Release pipeline:**
  - download-artifact v8 first runs in the next real release (v0.1.10). A digest mismatch now
    fails the publish.
  - The runners are pinned to `ubuntu-24.04`. Moving to Ubuntu 26 is a deliberate change.
- **Reconnect gap:** a message sent into a half-open link (up to 90s) can be lost. A follow-up
  plan needs message ids and an echo from the host.
- **Next feature spec:** private messages and roll modes.
- **Housekeeping:**
  - remove the unused `/home/ramon/deploy-v012` on the relay VM;
  - drop the extra key from the ssh agent (`ssh-add -d ~/.ssh/id_ed25519`);
  - set git `user.name` and `user.email`.
