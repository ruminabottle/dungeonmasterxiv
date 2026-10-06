# Private messages and roll modes: one audience row, a view per member

Date: 2026-10-06. Amends rolls R-2.6 and R-2.15, and the UI foundation spec's `RollModeSwitch` and
`PrivateCard` rows.

## Why

Everything in a session today is public. The specs already promise more:

- **Private talk between a player and the DM, in either direction** (rolls R-2.6). There is no
  player-to-player privacy: FFXIV's `/tell` covers that, and R-2.10 depends on the host only ever
  holding what it is a party to.
- **Three roll modes:** Public, Private to the DM, and Blind (R-2.15). Foundry's Self mode was not
  taken.
- **A non-recipient never receives private content,** which is stronger than hiding it in their view
  (A-2.15, product-overview D-13).
- **Private content never reaches FFXIV's chat log** (R-2.14).
- **A reveal is a logged action,** so a hidden roll is never lost.

This spec decides how those work, modelled on Foundry core's chat-mode row.

## Decisions

### 1. One audience row sets who sees what you send

- **The same row governs messages and rolls,** as Foundry's does. It sits above the message box.
- **Players get three buttons:** Public, DM only, Blind.
- **The DM gets three:** Public, DM only, and To ▾, which lists the players in the session.
  Blind is meaningless when the DM is the one rolling, so the DM does not get it.
- **"The DM" means the DM side:** the DM plus any Assistants, as Foundry addresses all Gamemasters.
  Nothing can make anyone an Assistant yet, so today the DM side is the DM alone.
- **To ▾ always names one player,** as R-2.6 says. While set, messages and rolls both go to that
  player and the DM side.
- **The roster shortcut.** On the Session tab, each player's row has a "Message privately" button.
  It sets To ▾ to that player and switches to Chat.
- **Blind is for rolls.** Text sent while Blind is selected goes as DM only, and its sender can read
  it. The row stays on Blind for the next roll.
- **The choice stays set between sends** and resets to Public when a session starts. Staying
  private by mistake costs one message only the DM saw; slipping back to Public by mistake can spill
  a hidden roll.
- **The message box's placeholder always names the audience in words:** "Say something, or /roll
  1d20" for Public, "To the DM: say something, or /roll…", "Blind roll to the DM…", "To Eli: …".

### 2. Icons, as in Foundry

The row is icon buttons with tooltips, styled as `RailButton`s: the selected one is lit with the
`Gold` edge. Icons are Dalamud's FontAwesome set.

| Icon | Tooltip | Who gets it |
| --- | --- | --- |
| `Globe` | "Public: everyone sees it" | everyone |
| `UserSecret` | Players: "DM only: you and the DM". The DM: "DM side only" | everyone |
| `EyeSlash` | "Blind: the DM sees the roll, you don't" | players |
| `User` ▾ | "To one player". Once a player is picked, the button shows `User` and their name | the DM |

- **Cards use icons in place of words:**
  - the audience line under the speaker is `UserSecret` DM, or `User` Eli;
  - a blind roll carries `EyeSlash` beside it;
  - Reveal is an `Eye` icon button with the tooltip "Reveal to everyone";
  - a revealed card carries a small `Eye` and "Revealed by the DM";
  - the roster shortcut is a `User` icon button with the tooltip "Message Eli privately".
- **The placeholder card keeps its short text** (decision 3), because the text is all an onlooker
  learns.
- **Tooltip copy follows R-2.15:** Blind's says what it does and never calls it secret, private,
  hidden or secure.

### 3. What each person sees

**Someone entitled to the content** (the sender, the target, the DM side):

- a `PrivateCard` with the audience line from decision 2;
- the DM side sees a blind roll in full, marked with `EyeSlash`;
- **the roller of a blind roll** sees the expression they typed and "?" in place of the total. The
  value is on their client (A-2.13); the panel does not show it.

**Everyone else:**

- **A private or blind roll leaves a placeholder:** a `PrivateCard` with a "?" bar and one line of
  text naming the speaker and, where there is one, the target:
  - a player's roll: "Eli (Tuka) rolled for the DM";
  - the DM's own DM-only roll: "Renn (DM) rolled";
  - the DM rolling to a player: "Renn (DM) rolled for Eli";
  - an NPC speaker: "[DM] Goblin Archer (Ramon) rolled".

  Private and blind look the same, so the mode never shows. The copy never says private, secret or
  hidden.
- **A private message leaves nothing at all.**

Under D-13, an onlooker is at **Limited** for a private roll (knows it exists, sees no contents) and
at **None** for a private message.

### 4. Revealing a roll

- **The DM side has Reveal on every private or blind roll card.** Messages have no reveal.
- **A reveal always goes to everyone.** Every placeholder fills in with the full roll and "Revealed
  by the DM", including the blind roller's own card.
- **Once revealed, the button is gone,** and a repeated request changes nothing.
- **The record keeps the reveal:** who revealed it and when. Nothing changes silently.
- **A reveal works after the roller has left,** because the host holds the roll.

### 5. The FFXIV chat log

R-2.14 is unchanged:

- **a recipient of a private message** gets only the existing contentless notice ("a private
  message from the DM");
- **a placeholder** echoes only its placeholder text, under the existing echo setting;
- **a revealed roll** echoes as a public roll does;
- no private content ever reaches the game's log.

### 6. On the wire: sending

All changes are additive under D-14. The protocol version does not change.

- **`Saying` and `Rolling` get an optional `Audience`:** Public, DM side, Blind, or To a player, with
  that player's peer code.
- **A missing `Audience` means Public,** so an older client keeps working as now.
- **The host enforces the rules** rather than trusting the sender:
  - a player cannot send To a player;
  - Blind on text is stored as DM side;
  - a target must be a player currently in the session.

  Anything else is dropped and logged on the host. Nothing is relayed or echoed.

### 7. The host keeps one record and sends a view per member

- **Each recorded entry keeps its audience.** A private entry also keeps **the participants entitled
  to it, settled when it was sent**: the sender, the target, and the DM side as it was then. Someone
  made an Assistant later does not gain what came before. Entitlement is by participant, not peer
  code, so a member who reconnects under a new code keeps theirs.
- **A member's view is a pure function of the record and the participant.** Line by line:
  - **public:** as is;
  - **private message:** only to someone entitled; everyone else gets nothing;
  - **private or blind roll:** in full to someone entitled; in full and flagged blind to the blind
    roller; as a placeholder to everyone else.
- **Each view is numbered 1, 2, 3… for that member alone.** A global number would leave a gap where
  a private message was, and D-13 forbids a gap in an ordering. The host stores nothing extra; it can
  rebuild any view from the record.
- **Live sending** passes each new entry through every member's view and seals what is left to each
  member separately, as `RosterBroadcast` does now.

### 8. On the wire: what members receive

- **A stream line gets optional fields:** its audience, its target's peer code, a blind flag, a
  placeholder flag, and who revealed it.
- **A placeholder carries no roll data.** Its kind is Message and its text is the placeholder line
  from decision 3, so an older client shows it as an ordinary line ("Eli (Tuka): rolled for the
  DM"), and a current client draws the placeholder card.
- **A reveal is the same line sent again.** Clients already store lines by number and overwrite a
  line that arrives with the same number. The host re-sends the placeholder's line, at each member's
  own number, now holding the full roll and the reveal. The card fills in where it was. No new
  message type.

### 9. Resume

- **A returning member asks for lines after its last number,** as now. The host rebuilds that
  member's view and sends the lines after it.
- **It also re-sends every revealed roll in the view.** Overwriting is harmless, and it catches
  reveals made while the member was away.

### 10. An older host

- **A new client talking to an older host would have its `Audience` ignored,** and a private roll
  would go to everyone.
- **So the host announces support:** session content gets an optional `Audiences` flag, sent with
  every roster broadcast, which each member receives on admission.
- **A client shows the row only when its host has announced it.** Otherwise the row is hidden and
  everything is Public.

### 10a. An older member

- **An older member client would show private content it was entitled to as an ordinary line,** with
  no private marking, and might echo its content to FFXIV's chat log, breaking R-2.14.
- **So members announce support too:** the join and resume details get an optional `Audiences`
  flag.
- **The host never sends private content to a member that has not announced it:**
  - in To ▾ and on the roster, that player is greyed out with "Eli's plugin needs updating for
    private messages";
  - as an onlooker, they get placeholders like anyone else, which is safe.

### 11. Edge cases

- **The whisper target leaves.** To ▾ stays on their name, greyed out, and Send is disabled with
  "Eli is no longer in the session". It never switches audience on its own: falling back to Public
  could spill a whisper, and falling back to DM only is a choice the DM did not make.
- **The whisper target drops and returns.** Their view catches them up on resume, whisper included.
- **Sends queued while the link is down** keep the audience they were sent with. The undelivered
  count works as now.
- **An older member client** never sends an audience, shows placeholders as their fallback text, and
  never receives private content (decision 10a).

## Spec changes

- **rolls R-2.6:** the two targets are reached through the audience row (decision 1). A private
  message is at None for a non-recipient. The DM picks the player with To ▾ or the roster shortcut.
- **rolls R-2.15:**
  - the modes are set by the same row as messages;
  - a private or blind roll is at Limited for everyone not entitled: a contentless placeholder
    (decision 3);
  - Blind applies to rolls; Blind text goes as DM only;
  - only the DM side reveals, always to everyone.
- **UI foundation spec:**
  - `RollModeSwitch` becomes the icon row of decision 2 and governs messages too. Its labels are
    Public, DM only, Blind; the DM's third button is To ▾;
  - `PrivateCard`'s placeholder text follows decision 3;
  - "All rolls are Public until roll modes are built" is removed when this ships.

## Out of scope

- **Message as character,** Foundry's fifth button. Speakers (R-2.7) already cover it.
- **Self mode** and **player-to-player privacy** (R-2.6, R-2.15).
- **The roll breakdown** (click a total to see each die), **modifier tags** and **message
  formatting.** Each can be its own spec.
- **Making someone an Assistant.** The audience rules are ready for it.
- **Holding messages for a dropped member** (R-2.10) and the half-open link loss gap. Resume
  already covers private content the same way as public.

## Verification

The smoke-tests-only rule applies.

- **One end-to-end test,** beside `ARollReachesEveryMemberWithItsDiceTests`: a DM, Eli and Mara in a
  loopback session.
  1. Eli sends a DM-only roll and a message to the DM.
  2. Mara's **received data**, not her view, holds a placeholder line with no roll data, and nothing
     for the message. Her line numbers have no gap. This is A-2.15 and the second half of A-2.13.
  3. The DM reveals the roll. Mara's line at the same number now holds the full roll.
- **Checked by eye in game:**
  - the icon row and its tooltips, for a player and for the DM;
  - a blind roll showing "?" to its roller and in full to the DM;
  - the FFXIV chat log getting only contentless notices (A-2.29);
  - the roster's "Message privately";
  - To ▾ greyed out after its player leaves;
  - a reveal filling in every placeholder.
