# Session layer: product spec

Describes the current product. Converted 2026-09-29 from `PRD-1-session-layer.md`, plus the
directives and resolved escalations that bear on it; the original is archived at
`~/archive/dungeonmasterxiv-agent-team-2026-09-29/claude/team/product/prd/PRD-1-session-layer.md`.

Principles are cited as `product-overview D-n` and are not restated here.

## Purpose

Nothing the group shares works without this. Rolls, initiative, HP and resume all assume there is a
session, a roster, and a host to be authoritative. It is also the highest-risk area of the product:
if the transport does not work in real players' hands, nothing above it matters.

It is one coherent transport-and-admission concern, and it is built in sequence rather than split
into parallel pieces, because splitting it produces collisions in the layer everything else stands
on.

This area carries the session, not its contents. Rolls, initiative, HP and status, the session log
and character sheets belong to their own areas. Moderation (blocklists, bans, reputation,
reporting) is out of scope: the DM's accept/deny is the whole trust model. The relay accumulates no
state of any kind (product-overview D-2).

## Requirements

### R-1.1 Hosting

- A DM can start a session from the plugin. Their client connects to the relay and registers the
  session at that moment, and not before.
- Ending the session, closing the plugin, or unloading it disconnects from the relay. The plugin
  never holds a relay connection while no session is running.
- The DM's client stays authoritative throughout (product-overview D-3). The relay forwards, decides
  nothing, and is never asked what the state is.
- A DM can see at a glance whether they are hosting and how many players are admitted.

**Acceptance criteria**
- **A-1.1** A DM starts a session and is shown a code they can read aloud.
- **A-1.8** When the DM ends the session or closes the plugin, every client shows the session ended
  and the DM's client has disconnected from the relay. The DM-side disconnect is observable at the
  relay. A player client showing "ended" is a self-report and does not by itself prove that client
  let go of its connection.

### R-1.2 Session codes: short and speakable

- Starting a session produces a **short, human-readable code that can be read aloud over voice chat**
  and typed without care. The decision is speakability over unguessability.
- **The code is not a secret and is never presented as one.** Security rests entirely on the DM's
  accept/deny (R-1.3). A DM who reads their code aloud on a public stream is still safe, because they
  control admission.
- A code is **reusable**: a DM may ask for the same code next week and will usually get it. It is a
  convenience and a habit, **not an identity** (R-1.2a).

### R-1.2a Session code parameters

- **Alphabet, 24 characters:** `B C D F G H J K M N P R T V W X Y` and `2 3 4 6 7 8 9`. The
  exclusions are the decision:
  - **All vowels (A E I O U).** A code that cannot form a word cannot form an offensive one, so no
    profanity filter is needed. This is the main reason.
  - **L, S, Z, Q:** confusable with 1, 5, 2 and O when written or spoken.
  - **0, 1, 5:** confusable with O, I/L and S.
- **Length 6**, displayed in two groups of three (`BKD-7RM` style): about 191 million combinations,
  chosen for speakability and low accidental collision. **Not chosen for guessing resistance**, since
  codes are not secret. Do not lengthen it to make it "safer"; that trades away the decided axis.
- **Collision is resolved at the relay, not the host.** The relay routes by code, so the namespace is
  relay-wide. The host requests a code, the relay refuses one already active, and the host
  regenerates and retries. A host cannot know on its own what is free.
- **A code identifies a live session, not a campaign.** Campaign identity is a locally generated UUID
  in the DM's store (product-overview D-8). If a returning DM's usual code is unavailable at resume,
  the host takes a new one and the DM shares it. **The campaign is unaffected**, and the UI says so
  rather than letting the DM think the campaign is lost.

### R-1.3 Join and admission

- A player enters a code and requests to join. This is a human action in the plugin; nothing is
  automatic.
- The DM sees a prompt showing the requester's **chosen display name**, with accept and deny. The
  request reaches the host encrypted (R-1.3m). The plugin reads no chat, so a display name is something the player chose to
  send; showing it lets the DM tell who is knocking. What the name may and may not do is R-1.3e.
- Nothing flows to a requesting client before the DM accepts. A denied or pending client receives no
  roster, no state and no events: not a filtered view, nothing (R-1.3b).
- The requesting player always sees which state they are in (waiting, admitted or denied) and is
  never left looking at an ambiguous spinner.
- The DM can remove an admitted player mid-session, and that client immediately stops receiving
  state.
- A session may have an **Assistant** co-DM. An Assistant can run combat, roll for DM-controlled
  combatants and reveal hidden rolls, but **cannot end the session, admit or remove participants, or
  approve a relink**. An Assistant runs the table; only the DM controls who is at it.

**Acceptance criteria**
- **A-1.2** A player entering the code causes the DM to see a prompt identifying the requester by
  their chosen display name, with accept and deny.
- **A-1.3** On accept, the player's client shows it is in the session. On its own this is a
  self-report, so it is bounded by A-1.3a.
- **A-1.3a** On accept, session state actually flows to that client: roster, events, the picture the
  host is authoritative for. Asserted on what the client **receives**, not on what it displays. A
  client showing "you are in the session" while nothing reaches it fails.
- **A-1.4** On deny, the player's client says so and receives no session state whatsoever.

### R-1.3m Keys first, then an encrypted join request

**The joiner and the host exchange keys before the join request is sent, so the request travels
encrypted.**

- The joining client sends a hello carrying a fresh public key, and the host answers with its own
  (product-overview D-11). Both derive the session key before anything identifying is sent.
- The join request, carrying the display name and any stored participant ID (R-1.5b), is sealed
  under that key. **The relay never sees a name or a participant ID.**
- The key exchange is not session traffic. An unadmitted client still receives none (R-1.3b).
- **Nothing is compared, and nothing claims to have been.** No fingerprint is shown and the prompt has
  no confirmation control. With no comparison, a relay that actively substitutes keys could read a
  session; a passive relay, a leaked log or a seized server cannot. That risk is accepted, and no
  copy implies otherwise (R-1.7).

**Acceptance criteria**
- **A-1.3m-1** No frame the relay receives during a join carries the display name or a participant ID
  in plaintext. Assessed over what the relay receives, with a recognisable name and a known stored
  participant ID.
- **A-1.3m-2** The DM's prompt and the joiner's screen show no fingerprint and no comparison or
  confirmation control.

### R-1.3b Denial is a message, and an unadmitted client receives nothing

- **Denial is an explicit message the denied client receives**: not a timeout, not silence, not an
  absence of acceptance. The player is told they were refused.
- **An unadmitted client receives no session traffic at all, including ciphertext it cannot read**
  (product-overview D-13). A denied client receiving undecryptable payloads would still learn that a
  session is live, its cadence, and roughly how much is happening.
- So **the relay routes a connection into a session's traffic only after the DM accepts.** Filtering
  at the client is the failure, and encryption does not substitute for not sending.
- After denial the connection is closed, not left open doing nothing.
- A refused player who got silence could not tell refusal from a broken relay, a wrong code, or a DM
  who has not looked yet. That is the ambiguity R-1.8 forbids.

The message vocabulary is an engineering choice. What is required is that denial is expressible,
delivered, and followed by no further session traffic.

**Acceptance criteria**
- **A-1.14** A client that is not admitted receives no roster at all and cannot infer its size or
  membership: absent from what it receives, not filtered in the UI. Assessed over what the client
  receives.

### R-1.3c No unbounded wait, anywhere

**Every wait a person can experience is bounded, and every bounded wait names what it was waiting for
when it ends.** "It didn't work" is not an answer a person can act on.

| Wait | Bounded by | When it ends without success, the player is told |
| --- | --- | --- |
| Reaching the relay | short, seconds | The relay could not be reached, distinct from the next two (R-1.8) |
| Looking up the session code | short, seconds | That code is not an active session. Not "denied", not "failed" |
| Waiting for the DM to answer | the window R-1.3l sets | The request lapsed. The DM never answered and may be mid-encounter. You can ask again |
| Host loss mid-session | the grace window R-1.4 sets | The session ended |
| DM closes the session deliberately | the closing window R-1.3g sets | The session ended |

- **The joining player sees that the wait is bounded while it is happening** (a countdown, a
  "waiting for the DM, up to N minutes" line), not only when it ends. N is R-1.3l's window.
- **The admission wait and R-1.3l's prompt expiry are one window seen from two sides**, and they must
  not drift apart. A player told the request lapsed while the DM still has a live prompt is worse than
  either timeout alone.
- **Lapsing is not denial** and is never reported as it. A busy DM refused nothing.
- Re-requesting after a lapse is always available, and **the player reuses the session code they
  already have.** A lapse costs them nothing but the wait.

**Acceptance criteria**
- **A-1.5g** No wait in the join flow is unbounded. Relay-reach, code-lookup and DM-answer each end
  within their bound with a distinct message, and the joining player can see the wait is bounded
  while it runs.
- **A-1.5h** A lapsed request is reported as lapsed, never as denied, and can be re-requested without
  a new code.

### R-1.3d-1 Telling requests apart

- Two concurrent requests, including two that send the same display name, are told apart on the DM's
  screen by a short per-request label (a peer code). The joiner need not see it.
- The label is session-scoped (the same person in two sessions does not present the same value), not
  derivable from a character name or account, not stable across campaigns, and absent from every
  export (A-1.11c).

**Acceptance criteria**
- **A-1.2a** No identifier the admission prompt or roster uses links a participant across two
  different session codes, and no display name appears in any export (product-overview D-8).

### R-1.3e The display name is shown, and never authenticates

- **The admission prompt shows the requester's chosen display name.** The name defaults to the
  character name and may be changed to an alias.
- **A participant can see and change the name they will send, before it is sent, pre-filled with
  their character name.** The default is allowed only because the alternative exists; with no way to
  change it, nothing is "chosen".
- **The choice lives in the join flow, not in settings.** A user who never opens settings never
  learns what is about to be sent on their behalf. Settings may hold a persistent default that
  pre-fills the join-flow field; they may not replace it. The general rule: *a control that protects
  against something the user does not know about must appear on the path they are already taking.*
- Persistence across sessions and per-campaign aliases belong to the rolls area (rolls R-2.17); the
  minimum here is see it, change it, before it is sent.
- **A name never admits anyone.** It is self-declared, unverified and trivially spoofable. The DM
  admits people they arranged to play with; a returning player is recognised by their stored
  participant ID (R-1.5f) or by the DM (R-1.5e), never by a name match.
- **Two requesters may send the same display name.** The prompt stays unambiguous when they do.
- **Refused and absent never render the same.** "Did not give a name" invites suspicion; "the name was
  rejected" invites a question. A name a person gave and the rules refused is never reported as a name
  not given, to the DM or anyone.
- **A name a person gave is never silently discarded**, whatever the reason it could not be kept
  (refused, no home to store it, no current campaign, a store that failed). The user is told. A control
  offered where it cannot work is itself a defect: a settings box that records nothing is worse than
  one that is absent or disabled with a reason.
- Names stay campaign-scoped and are barred from exports (product-overview D-8).

**Acceptance criteria**
- **A-1.2d** Two concurrent requesters sending the same display name remain distinguishable to the DM,
  and admitting one does not admit the other.
- **A-1.2g** A player can see and change the name they will send, before it is sent, pre-filled with
  their character name, and what reaches the session is what they set. Four failures, each separate: a
  value sent with no way to change it; a blank rather than pre-filled field; a change made after the
  name is on the wire; a settings value that never reaches the wire. Asserted on what leaves the
  client.
- **A-1.2n** The name that will be sent is shown, editable, in the join flow, on the screen the user
  is already on when they join. A build whose only name control is in settings fails.
- **A-1.2y** Refused and absent never render the same, anywhere. Supply a character name the rules
  refuse and assert the product does not render it as "gave no name". Falling back to a "no name"
  value fails. The pre-fill path and the typed path fail separately.
- **A-1.2z** A name a person gave is never silently discarded, whatever the reason it could not be
  kept, and the user is told.

### R-1.3f The roster, and who may see it

- **The DM sees the display names of everyone currently admitted.**
- **A joined player sees the DM's display name and those of the other participants.**
- **The host authors the roster** (product-overview D-3). A player client renders what the host sends
  and never originates it. **A client reconnecting mid-session rebuilds the current roster** rather
  than showing an empty list.
- **A role shown in the roster is not rendered in parentheses.** Parentheses mean one thing across the
  product: the person behind the speaker (rolls R-2.7). Badge, prefix, column: the form is an
  engineering choice. This is consistency work, not a defence; the reserved names (R-1.3j) and the
  session role (rolls R-2.7a) carry the guarantee against imitating the host.
- **Access levels** (product-overview D-13):
  - A participant is **Owner** of their own roster entry: it is their display name and they chose it.
  - Every **admitted** participant is **Observer** on every other entry: sees the name, changes
    nothing.
  - A client that is **not admitted is at None**: the entry is absent from what it receives, and it
    cannot infer the roster's size or membership (R-1.3b).
- No level above Observer is granted to anyone but the entry's owner.

**Acceptance criteria**
- **A-1.13** The DM sees the display names of everyone currently admitted; a joined player sees the
  DM's name and the other participants'.
- **A-1.13a** A client reconnecting mid-session rebuilds the current roster rather than showing an
  empty list.
- **A-1.13d** No role appears in parentheses anywhere in the roster. Render a roster with a Host, an
  Assistant and a Player; no entry contains a parenthesised role. The role may still be shown; this
  constrains the form.

### R-1.3g Leaving, and the deliberate asymmetry of disconnects

- **A player can leave a session they have joined, and the DM's roster reflects the departure.**
- **The two roles disconnect differently, on purpose.** When the **DM's** client quits, every
  participant sees that the session is closing **and how long remains**. When a **player's** client
  quits, that player is **removed from the roster immediately**. The session and its record live on
  the DM's machine, so a departed player costs the group nothing and a departed DM costs everything.
- **The countdown is a requirement.** "The session is closing" without "how long remains" is an
  unbounded wait (R-1.3c).
- **The closing window is sixty seconds and is not configurable.** Its job is *time to notice*: long
  enough to survive a normal glance away, short enough that nobody sits in a room that is over, and a
  round number a DM can say aloud ("you have a minute"). The DM already controls the length of the
  goodbye by choosing when to press close; the window only confirms the goodbye happened.
- **It is not R-1.4's five minutes.** That window is time for the host to come back; a deliberate quit
  has no coming back.
- **It is one value every participant agrees on**, set by the host and sent out, never decided per
  client.
- **"Quits" means a deliberate quit.** A deliberate exit (including closing FFXIV cleanly) removes the
  player at once. An ungraceful drop (crash, kill, network loss) is not a departure: R-1.5a holds the
  seat. A crash cannot send anything, so graceful exit is as much as can be achieved
  (product-overview D-17).
- What an ungraceful drop needs is for the host to **learn** of it (R-1.5a), not removal.

**Acceptance criteria**
- **A-1.15** A player leaves a session and the DM's roster reflects the departure.
- **A-1.16** When the DM's client quits, every participant sees the session is closing and how long
  remains. "Closing" with no remaining time fails. The window is sixty seconds.
- **A-1.16a** When a player's client quits deliberately, the host acts on that member-authored
  departure and removes the player from the roster immediately. A member that vanishes without a
  leave notice is **not** a gap here: R-1.5a holds its seat, and removing vanished members to "close"
  this row would break R-1.5a.
- **A-1.16b** Every participant's countdown **follows the host's**: the host and every participant
  agree on **the same instant**, not a matching delta or a value that merely moves when the host's
  does. A demonstration must compare values the client could not have known, at two different
  settings (defeating a hardcoded constant), and must rule out the plausible wrong candidate by name:
  the raw value matches the deadline **and** differs from the instant the session ended, which is one
  window away. Any demonstration is shown failing on a build that violates the property before it is
  relied on.
- **A-1.30** A member that **vanishes** is not treated as one that **quit**. Kill a client without a
  leave notice and return with the same key **inside** the window: it resumes with no DM prompt. Kill
  another and return with the same key **after** the window: the DM is prompted. Send a leave notice:
  immediate removal. All three fail separately; a build that tracks nothing passes the first and
  fails the second. The host must record, per member, when that member's link dropped, and compare it
  on return. No ticking clock or expiry sweep is required, and showing a member as reconnecting is
  neither required nor forbidden.

### R-1.3h Hosting and joining are exclusive

- **A client that is hosting offers no way to join a session. A client that has joined offers no way
  to host one.** A live host is also never offered an action that restarts hosting
  (product-overview D-17).
- **"Offers no way" means the affordance is absent**, not disabled-and-explained. A greyed control
  invites the question the exclusivity removes.
- A client cannot coherently be both the author of shared state and a renderer of someone else's
  (product-overview D-3).
- **The exclusivity lasts while the client holds a seat that could still be resumed.** A dropped link
  does not end a session: inside R-1.5a's window the joiner is still a member. So exclusivity ends when
  the **seat** does (a deliberate quit, expiry of R-1.5a's window, or the session ending), never when
  the link drops. Otherwise a client could author one session while holding a place in another, or
  silently abandon a seat the host is keeping.
- Enforcing this requires the joining client to know how long its own seat lives.

**Acceptance criteria**
- **A-1.17** A hosting client offers no join affordance and a joined client offers no host
  affordance, for the life of the session. A disabled-but-present control fails. The in-game run
  includes a disconnect step.
- **A-1.17a** An admitted joiner whose link drops is **not** offered a host affordance while its seat
  could still be resumed. A build that offers it the instant the link falls fails.

### R-1.3i The session code can be copied in one action

- **The session code can be copied to the clipboard in a single action**, so it can be pasted rather
  than transcribed.
- The clipboard is an OS facility: not game input (product-overview D-1) and not a network
  destination (product-overview D-2).
- R-1.2a's grouping is for reading aloud. **What is copied is a code the join field accepts**; a
  formatting choice never makes the pasted value fail.

**Acceptance criteria**
- **A-1.18** The code copies in one action, and the copied value is accepted verbatim by the join
  field. A copy carrying grouping the join field rejects fails.

### R-1.3j What a display name may contain

These are properties, not a codepoint set; the allowlist that implements them is an engineering
choice. The line that organises them: **restrict what can attack the display; never restrict what
language a person speaks** (product-overview D-8).

1. **A name renders as something a person can see.** A name that renders as nothing leaves the prompt
   blank where a name is required. `U+3164 HANGUL FILLER` is a letter and `U+2800 BRAILLE BLANK` is a
   symbol, which is why this is an allowlist and not a denylist.
2. **A name cannot alter, forge or displace the text around it.** A line break (including `U+2028`) or
   a bidirectional override can push the request label or the controls out of the DM's eyeline, or
   reorder what they read, through the one field a requester controls.
3. **Bounded in length, so the prompt stays readable beside it.** The bound is **32 grapheme
   clusters**, counted in a unit that does not vary by script. Code units would refuse a Vietnamese or
   Devanagari name that a Japanese name of the same visible length passes. **The rule behind the
   number: the limit never rejects a name FFXIV itself permits**, because the character name is the
   pre-filled default. FFXIV allows a forename and a surname of 2 to 15 characters each and at most 20
   characters in all, the space included, so 32 accepts every character name with room for longer
   aliases. If the game's maximum ever rises past 32, the number rises and the rule does not change.
4. **Legitimate names are accepted.** Diacritics and combining marks, including decomposed forms, are
   ordinary. An allowlist that admits nothing refuses every hostile input and looks perfect, so this
   half fails silently.
5. **Any script, no restriction.** Japanese, Korean, Cyrillic, Arabic, any of them. FFXIV is global,
   and since the default is the character name, a script restriction would make the default invalid
   for exactly the players it excluded.
6. **Square brackets (`[`, `]`) are refused.** They are reserved to the host marker (rolls R-2.7a).
7. **`DM`, `GM`, `Dungeon Master` and `Game Master` are reserved to the host and may not be chosen as
   an alias**, matched case-insensitively after whitespace normalisation.
   - **The reservation reaches a chosen alias, never a player's actual character name.** A character
     name comes from the game and is true; a player genuinely called `Dungeon Master` is not imitating
     anyone. An alias is free and instant, and that is the whole attack surface. FFXIV needs a
     forename and a surname, so only `Dungeon Master` and `Game Master` can collide with a real name.
   - A blocklist leaks (`D.M.`, `the DM`, small caps, homoglyphs). Reserved names close the obvious
     case cheaply; the session role (rolls R-2.7a) carries the guarantee. Neither alone suffices.

- An unreadable or look-alike name cannot admit anyone, because a name never admits anyone. The
  worst case is a DM asking "who is this?", and the recourse is to deny, which costs nothing. A social
  product solves a social problem socially, not by refusing a script.
- **A refusal is visible, and nothing alters the user's name silently.** Refusing loudly beats
  truncating silently. This binds every layer that can discard what the user typed: a field that
  silently stops accepting keystrokes fails as surely as a silent truncation. Either blocking input or
  refusing at parse is fine, provided the outcome is visible. Combining marks are unbounded, so no
  buffer size is provably safe; the only conforming answer is to tell the user.

**Acceptance criteria**
- **A-1.2i** The check **accepts** each of these, each a separate failure: `Y'shtola Rhul`
  (apostrophe), `Jose` with a combining acute (decomposed form), and an ordinary-length name with a
  diacritic. A run refusing every listed input fails even if it also refuses every hostile input.
- **A-1.2j** The check refuses a name that renders as nothing, including `U+3164` and `U+2800`.
- **A-1.2k** The check refuses a name that can alter, forge or displace surrounding text: line
  separators including `U+2028`, and bidirectional overrides.
- **A-1.2l** A name long enough to displace the request label or the controls is refused, and both
  stay visible beside the longest accepted name.
- **A-1.2m** The check accepts a name in a non-Latin script: Japanese, Korean, Cyrillic and Arabic
  each pass, each a separate failure.
- **A-1.2s** Two names of the same perceived length are treated the same way whatever their script. A
  build accepting a Latin name at the limit and refusing a Japanese, Korean, Arabic or combining-mark
  name of the same perceived length fails. Only a length-matched pair exposes this; a short non-Latin
  name passes any code-unit limit.
- **A-1.2t** The longest name FFXIV itself permits (20 characters, the space included, for example a
  15-character forename and a 4-character surname) is accepted, pre-filled and unedited.
- **A-1.2u** The length boundary is exercised with a **non-ASCII** name. A boundary built from a
  repeated ASCII character cannot tell grapheme clusters from code units. The expected bound is stated
  independently of the production constant, so changing that constant makes the boundary check fail.
- **A-1.2v** A name refused for length is refused **visibly**: never silently truncated, never
  silently dropped, and no layer silently stops accepting input. The words are an engineering choice
  under R-1.7's constraints.
- **A-1.2v-1** "Silently" describes the **alteration**, not the result. Displaying a cut name does not
  satisfy A-1.2v: the user must be told an alteration happened.
- **A-1.2v-2** A stored display name is never rewritten without the user having changed it. Load a
  stored name longer than the current input buffer, open and close the settings window touching
  nothing, and the stored bytes are identical. Needs the real game client.
- **A-1.2w** The four reserved words are refused as a **chosen alias**, case-insensitively and after
  whitespace normalisation: `DM`, `dm`, `  Dungeon Master  `, `GAME MASTER` are each refused. Matching
  only the exact four strings fails.
- **A-1.2w-1** A player whose **actual character name** is a reserved word is accepted, and the
  pre-fill holds for them. Present `Dungeon Master` as it arrives from the game and assert it is
  accepted and offered as the pre-fill. A build refusing both provenances fails.
- **A-1.2x** No copy states or implies that the reserved names prevent the host from being imitated.
  Copy claiming the DM is verified, that only the host can be called DM, or that impersonation is
  prevented fails. This can fail while A-1.2w passes. It fails on copy, not on the session role being
  absent, which is the rolls area's criterion.

### R-1.3k The host reads member-authored events

Player clients send their own events (a roll they made, a join request) and the host turns events
into shared state (product-overview D-3). The model is one-author, not one-direction: **a host that
cannot read member-authored content cannot implement D-3 at all.**

- The host opens and acts on content from any admitted member, with no relay involvement. Per-peer key
  selection is an engineering choice.
- Every roll is member-authored content the host must read, so the rolls area stands on this.
- A member-authored leave notice covers a deliberate quit. A crash has nobody to send anything, so the
  host still learns of an ungraceful drop only from the relay (R-1.5a, A-1.28), and that serves the
  seat clock, not roster removal.

**Acceptance criteria**
- **A-1.13c** The host opens and acts on content authored by any admitted member. A build in which
  inbound member content is dropped unopened fails. Assert that the host **acted** on it, not that it
  arrived: routing is availability, decryption is capability.

### R-1.3l The admission prompt expires

**The admission prompt expires after fifteen minutes**, and the request lapses. The requester is told
it lapsed and can ask again; the DM sees that it expired rather than the prompt vanishing (R-1.3c).

- **It must survive one combat.** A DM mid-encounter may not look at a prompt for ten minutes, and
  bouncing a player because the DM was running the game is the wrong failure.
- **The asymmetry favours long.** Lapsing is cheap (the joiner asks again), so erring long costs little
  and erring short costs a real person a retry at the moment they are trying to join their friends.
- A session admitted at about five minutes has been observed, well inside the window.
- **If DMs routinely miss prompts, the fix is prompt visibility, not a longer deadline**
  (product-overview D-17). Lengthening the window would hide a visibility defect rather than fix it.

### R-1.4 Host loss: grace period, then end

Grace, then a clean end. Not an instant kick, and not an indefinite freeze.

- **The relay holds the session while the host is away.** When the host's connection closes, the
  relay keeps the session code reserved and the players connected for the grace window, tells the
  players the host is away, drops pending join requests with a message to ask again, and forwards no
  player payloads meanwhile. A hold that runs out ends the session as before.
- **Only the same running host can reclaim it.** On hosting, the host's client makes a random reclaim
  secret, held in memory only, and registers its hash with the code. The host's client redials on its
  own and presents the secret; a match reattaches it as host and tells the players it is back, and a
  mismatch is refused like a code already in use. A crash loses the secret, so a relaunched client
  cannot reclaim, and the campaign continues through autosave (R-1.6a).
- If the host becomes unreachable, player clients hold their last known state and show they are
  reconnecting after the silent window (R-1.4a), with the state visibly marked as no longer live.
- If the host returns within the grace window, clients resync **from the host** (product-overview
  D-3). Clients never reconcile with each other.
- If the grace window expires, the session ends and every client says so plainly. It never keeps
  showing stale data as though it were live.
- **The grace window is five minutes**, and it is a settable value, not a magic number. It measures
  the whole outage: a connection that flaps for five minutes ends the session as one long drop would,
  and each reconnect does not restart it. It is the same value as the seat window (R-1.5a): the two
  never tick together, so one number serves both. The relay's hold uses the same default.

**Acceptance criteria**
- **A-1.6** Killing the host's connection makes player clients show "reconnecting" with state
  visibly not live; restoring it within the grace window resyncs them from the host.
- **A-1.7** Letting the grace window expire ends the session on every client, stated plainly, with no
  stale data shown as live.
- **A-1.7f** Drop the host's connection and let its client redial within the window: it reclaims the
  same code, players resync from it, and nobody joins again. A client without the reclaim secret is
  refused.

### R-1.4a A blip is invisible

- **Every client redials on its own** after its connection drops, with growing waits, for as long as
  its window runs (the grace window for the host, the seat window for a player), keeping its keys.
- **The first ten seconds of any drop show nothing**: the client's own link, or a player hearing the
  host is away. After that, one line says who is reconnecting and how long is left. On return the line
  disappears, with no "reconnected" notice, so flapping produces no stream of messages.
- **Messages sent while the path is down wait on the sender's machine** and go out in order when it
  returns. After the silent window the message box is disabled until then. If the window runs out,
  waiting messages are dropped and the end message says how many were not delivered.
- **When the session ends**, the end message says the DM can resume the campaign. It states how the
  product works; it is not a warning (R-1.7).

**Acceptance criteria**
- **A-1.32** A drop shorter than ten seconds shows nothing on any client, and a message sent during it
  arrives afterwards, in order.
- **A-1.33** After ten seconds the line appears with the time left, and disappears on return with no
  further notice.

### R-1.5 Relink: DM-approved unless the DM lets returning players in

- A client's identifier is fresh on every plugin launch (product-overview D-8).
- A returning player in a known campaign is offered relink to their existing participant UUID.
- **The DM approves every relink, every session, unless they have turned on "Let them straight in"
  for that campaign (R-1.5f).** Relink is never inferred from a character name.
- Until relink is approved or automatically admitted, that client is not in the session.
- A build that has never delivered relink, and offers no control claiming it, claims nothing by
  shipping without it (product-overview D-18). That does not make the criteria below met.

**Acceptance criteria**
- **A-1.9** With "Ask me each time", a returning player is offered relink and receives no state until
  the DM approves it.
- **A-1.9a** A relink claim is **sent** by the joiner, **read** on arrival, **resolved**, and **reaches
  the DM's approval path** (or R-1.5f's automatic admission), and each of the four fails separately. A build that sends a claim nobody
  reads fails as surely as one that sends nothing, with nothing null, nothing thrown and no existing
  test red.

### R-1.5a Resuming is not relinking: the key says who, the window says how long

    same key, within 5 minutes   ->  resumes, no re-approval
    same key, after 5 minutes    ->  relink (product-overview D-8; approval per R-1.5f)
    different key, any time      ->  relink (approval per R-1.5f)
    deliberate quit              ->  removed immediately; returning means a new key, so relink

Two questions, two mechanisms (product-overview D-17):

- **The key answers who may resume**: only a client still holding the pair it was admitted under. A
  window alone would admit whoever turns up inside it.
- **The window answers how long the seat is held.** A key alone is unbounded: the host would hold a
  slot for someone never coming back.
- **Resumption is gated on proof of possession, never on presentation.** A returning client
  demonstrates it holds the private key: its resume carries a payload sealed with the key it shares
  with the host, naming the last stream line it received, and the host admits it silently only if that
  payload opens and the member is recorded as dropped. A refused resume tells the player their seat
  expired and the ordinary join applies. A public key used as a bearer token is a value that travels,
  and a value that travels is not a secret (product-overview D-11). The construction is an engineering
  choice. Only one client can prove possession, so "two clients claim the same key" cannot arise.
- **A relink supersedes a stale seat; it never sits alongside one.** A relink under a new key clears
  any seat still held for that player. The harm being prevented is a player **locked out by their own
  ghost**, as in Foundry VTT's defect foundryvtt#13161 (a disconnected user "active" and unable to
  rejoin for over ten minutes). The case is common: a crash-and-relaunch produces a fresh key, so it
  is a relink, not a resume.
- **A crash means a relink, not a resume.** A fresh launch means a fresh key, and a fresh key means a
  relink: the DM approves it, or R-1.5f admits it. Persisting a key across launches to turn it into a
  resume would build the portable identifier product-overview D-8 refuses.
- **The host must learn that a connection dropped, and the relay may tell it.** The seat clock cannot
  start, and a member cannot be shown as reconnecting, unless the host learns the link is gone; it
  must not infer this from silence. A dropped connection is a transport fact, and the relay is the only
  party that can know its own socket closed (it already authors transport messages, R-1.7b), so the
  notice reveals nothing R-1.9 does not already disclose.
- **The relay reports that a connection dropped. It never says "remove this participant"**, and
  nothing treats its notice as a roster instruction. The host decides what a drop means, and per this
  requirement it means **hold the seat**. The carrier is an engineering choice.
- **The seat window is five minutes and is a setting, not a constant**, changeable without a protocol
  decision. Five is the top of the documented two-to-five-minute band, because relaunching FFXIV takes
  minutes. The key is the mechanism; the number is a bound.
- **The seat clock advances only while reconnecting was possible.** It pauses whenever the host is
  unreachable. A clock that runs while reconnection is impossible measures the host's outage and
  evicts people for it. This is an instance of the general rule for every timer (product-overview
  D-17): *a timer that infers intent from inaction may only run while the action was possible.*
- **The two windows are complementary.** R-1.4's grace runs only while the host is unreachable; the
  seat clock runs only while the host is reachable. They never tick at the same time, which is why one
  number safely serves both.

**Acceptance criteria**
- **A-1.19** A client holding the same key pair, returning within the window, resumes with no DM
  re-approval, and the DM is not prompted at all.
- **A-1.20** The same client returning after the window relinks, under R-1.5f's setting. Resuming silently
  after the window fails. The window must be settable for this to be testable.
- **A-1.21** A client presenting a different key relinks at any time, however soon it returns.
- **A-1.22** A client that presents the public key but cannot prove possession does **not** resume.
- **A-1.23** The reconnect window is changeable without a protocol decision. A value changeable only by
  altering the wire format fails.
- **A-1.24** A player whose seat clock would have expired during a host outage still resumes on
  return. Drop a player and take the host unreachable at essentially the same moment; restore the host
  inside the last moments of R-1.4's grace, before it expires. The player resumes with the same key and
  no re-approval; being relinked fails. Do not keep the host away longer than the grace: the session
  then ends and there is nothing to resume into. A harness that cannot place the host's return inside
  the final moments of the grace cannot run this and says so rather than approximating.
- **A-1.25** No timer in the product advances while the action it infers intent from was impossible.
  A standing property over every timer, not a list: a timer added later that measures wall time
  through an outage fails it. A reworded message alone does not satisfy it, and a paused timer alone
  does not satisfy A-1.5j; a fix for a timer-driven wrong message needs both.
- **A-1.26** A relink under a new key clears any seat still held for that player. Drop a player,
  relaunch so they present a fresh key, and relink them while the old seat is inside the window: they
  are admitted and no ghost remains. Being refused, queued, or shown beside a stale entry fails.
- **A-1.27** Both windows read from one settable value and neither is a literal in the code path.
  Changing it moves the host-loss grace and the seat window together, without a protocol decision.
  The last hop (the composition root supplying the one value to both) is verified by reading, because
  that layer cannot be referenced from tests.
- **A-1.28** The host learns that a member's connection dropped without inferring it from silence. A
  vanished member shown as present, or a seat clock started from an absence of traffic, fails.
- **A-1.29** A relay notice that a connection dropped does not itself change the roster. Removing a
  member on the relay's say-so fails twice: it lets the relay originate shared state and it contradicts
  this requirement.

### R-1.5b The joiner's stored UUID: unbounded, and the player's to delete

- **The joining client stores its participant UUID, under the session code it was admitted on**
  (product-overview D-8). This is what makes a later relink possible.
- **Retention is unbounded: no expiry, no timer.** The rule guards against linkage across campaigns,
  not duration within one; a clock protects nothing and breaks relink for a campaign that meets
  monthly. A number appearing in this requirement means something has gone wrong.
- **The unit is the stored entry, not the campaign.** The joiner cannot know a campaign's identity,
  and telling it one would hand it an identifier linking the player across codes (product-overview
  D-8, A-1.11). A campaign whose code changed at resume (R-1.2a) therefore leaves one entry per code,
  and the player sees and deletes each on its own.
- **The player may delete their own participant UUID, per stored entry, without the DM's involvement
  and without the DM being told.** The DM can delete a campaign outright (R-1.6); the subject of an
  identifier needs the same control over their own copy.
- **No notification to the DM.** One would manufacture a signal linking a deletion to a player. The DM
  learns when relink is not offered.
- **Deleting ends the possibility of relink, and the player is told so before the deletion**: they
  will rejoin as a new participant needing fresh approval. The less a user understands what they are
  destroying, the more friction the destruction gets, so this is never a one-click delete.
- **A player can see what they are storing, per stored entry, before deleting it.** Each entry shows
  the session code it was stored under and when. You cannot meaningfully delete what you cannot see.
- **Deleting lives in settings, under "User storage", and nowhere else.** The session panel, the join
  flow and the roster never offer it. It is kept separate from the DM's campaign storage (R-1.6).
- **Relink is offered only under the code it was stored under.** A returning player whose DM's code
  changed has no matching entry and is not offered relink; the DM recognises them instead (R-1.5e).

**Acceptance criteria**
- **A-1.9b** A player can list what their client stores, one row per stored entry showing its
  session code and when it was stored, and delete one entry's participant UUID without the DM's
  involvement. Afterwards no file on their disk contains that UUID. Offering deletion
  without first showing what is stored fails.
- **A-1.9b-1** No control that deletes stored participant IDs is reachable from the session panel,
  the join flow or the roster. It is in settings, separate from campaign storage.
- **A-1.9c** The player is told, before the deletion completes, that relink will no longer be possible
  and that they will rejoin as a new participant needing fresh DM approval. A one-click irreversible
  delete fails.
- **A-1.9d** No participant UUID has an expiry. Discarding or ageing out a stored UUID on any timer
  fails.
- **A-1.9e** Deleting a participant UUID sends nothing to the DM or the relay. Assessed over what
  leaves the machine.

### R-1.5c The host creates the participant, and tells the joiner which one it is

The joiner stores its participant UUID (R-1.5b), the DM's machine stores participant UUIDs (R-1.6),
and a player client never originates shared state (product-overview D-3). So **the host mints the
participant, and the only way the joiner can store it is to be told.** Two halves, which fail
separately:

1. **A participant is actually created** when a joiner is admitted.
2. **The admitted joiner is told which participant it is**, at a point where it can store it.

Constraints on the conveyance:

- **Only to the joiner it belongs to.** The UUID is the relink claim; a participant holding another's
  could present it and the DM would see a plausible returning player (product-overview D-13, R-1.3f).
- **After admission, never before** (R-1.3b).
- **It is a credential when the DM lets returning players in (R-1.5f).** It is random (no less than
  122 bits), only ever sent encrypted (R-1.3m), and only ever told to its owner. Anyone who copies a
  player's stored copy can get in as that player while the setting is on; turning the setting off
  ends that. Resumption's proof of possession (R-1.5a) governs a different path.
- The carrier is an engineering choice, and additive (product-overview D-14).
- **The player's deletion does not propagate to the host.** The player deletes their copy; the host
  keeps its own under R-1.6. With no copy to present, the player cannot relink, which is what deletion
  means. A propagating delete would be a notification by construction, and the host retaining an id no
  one can claim is the intended end state.

**Acceptance criteria**
- **A-1.9f** A participant is created in production when a joiner is admitted.
- **A-1.9g** The admitted joiner is told which participant it is and **retains it across a plugin
  restart**. An in-memory receipt that is forgotten on exit fails, since relink is by definition
  across launches.
- **A-1.9h** No participant learns another participant's UUID. A conveyance broadcasting the roster's
  ids fails. Assessed over what each client receives.

### R-1.5d A session always has a campaign, and hosting never asks first

(product-overview D-17 states the rule; the details that make it checkable are here.)

- **Hosting never blocks on a campaign choice and never refuses.** A DM who does nothing gets a
  working session; not choosing always means a new campaign. **Hosting stays one action for a DM with
  nothing to resume.** The widget is an engineering choice.
- **A campaign exists before any participant is created**, in every case.
- **When prior campaigns exist, resuming one is reachable from the host flow without navigating
  away.** Pure auto-create would silently give a DM resuming last week's game a new campaign and lose
  the roster, which is the failure the product exists to remove.
- **The picker never claims continuity the build cannot deliver** (product-overview D-18). It need
  not explain what it cannot do; it must not say it restores players when it does not.
- **A resumed roster must not grow by one duplicate entry per join.** A participant appended with a
  fresh id on every admission makes a four-person weekly game hold eight entries after a fortnight and
  sixteen after a month, all bearing the same labels. That replaces an empty roster with one that is
  wrong, gets wronger, and cannot be seen to be false. Repeats are recognised by the stored
  participant ID (R-1.5f) or by the DM mapping an arriving joiner onto an existing entry (R-1.5e).
- **Do not "fix" the empty roster by minting participants at the picker.** No durable joiner identity
  exists (joiner keys are per request, a peer code is per session, a display name is not an identity,
  and the participant id is only as durable as the joiner's retained copy, A-1.9g). Minting produces
  one phantom per join, not per person, and it persists: nothing records which duplicates were one
  person, so no later migration can repair the file. **An empty, honest roster is the correct state**,
  not a gap. What is required here is the campaign half; recognising the same person automatically is
  relink (R-1.5).
- **An auto-created campaign is recognisable to its own DM a week later without them having typed
  anything, and is renameable.** A GUID or an empty label fails.
- **Its name is the creation date, then the clock time**, rendered in the reader's culture at the
  moment they read it:
  - The time is **local**, never UTC; a DM recognises the hour they sat at the table.
  - "Order" means date then time. The order inside the date is the culture's.
  - **No weekday**, and its absence is pinned, because some cultures inject one into the long-date
    pattern. For a weekly game every campaign would show the same weekday.
  - **No "Session of" prefix.** A campaign is not a session (R-1.2a), and the prefix becomes a
    misnomer the moment the campaign is resumed, which is when the feature has succeeded.
  - **A clock time, not "evening".** Where evening begins is a ruling nobody asked for; a clock time
    needs none and formats per culture for free. The time is a tiebreaker; the date does the
    recognising.
  - Example, holding the instant and timezone fixed and varying only culture: a DM at UTC-4 whose
    table sat at 16:14 local sees en-US `August 28, 2026, 4:14 PM` or en-GB `28 August 2026, 16:14`.
    The components and their order are load-bearing, not the punctuation.
- **A campaign carries a persisted name of its own.** Renameable needs somewhere for the rename to
  live, and the name is never synthesised from the session code.
- Campaign identity is never keyed on the session code (R-1.2a, R-1.6).
- **Accepted cost:** DMs who never resume accumulate auto-named campaigns. R-1.6's list-and-delete is
  the mitigation and it is sufficient; this is not to be "fixed" with a prompt.

**Acceptance criteria**
- **A-1.9i** Hosting succeeds with no campaign choice made, and the session has a campaign anyway. A
  build that blocks, prompts-and-waits or refuses fails. Hosting is one action.
- **A-1.9j** With prior campaigns present, resuming one is reachable from the host flow without
  navigating away.
- **A-1.9k** An auto-created campaign is identifiable by its own DM a week later with nothing typed at
  creation, and renameable afterwards. A GUID fails, and so does an empty label. Judged by a tester
  shown the list cold.
- **A-1.9k-1** Distinctness is not required. Two campaigns auto-created the same day may share a label,
  and renaming is the escape hatch; forcing uniqueness pushes toward an id-like suffix. The label still
  carries enough temporal detail that a DM's sessions do not routinely collide.
- **A-1.9k-2** The label a DM reads is correct in **their** culture at the time they read it. A stored
  label frozen in one culture's format fails. A user-supplied rename is stored text and is exempt.
- **A-1.9k-3** A campaign is shown by its **own name**, never by its session code. A list entry built
  from the code (`BCD-FGH`, or `(no code yet)`) fails.
- **A-1.9k-4** A campaign's displayed name does not change when its session code changes, and never
  comes to name a different campaign. Move a code from one campaign to another and assert neither name
  moved.
- **A-1.9k-5** The auto-created name is the creation date followed by the local clock time, with no
  weekday and no prefix naming it a session, rendered in the reader's culture. A `Session of…` prefix
  fails; a coarse period such as "evening" fails. Verified against the shipped build, not against this
  document.
- **A-1.9m** No participant is minted to populate a resumed campaign's roster; a build that creates
  participants at the picker fails. This is about **where** a participant is minted, not whether:
  A-1.9f requires admission to mint one. The two are a complementary pair over the same operation.

### R-1.5e The DM maps a returning player onto their existing entry

Admission mints a participant (R-1.5c) and relink is the player's claim to an old one (R-1.5), so
without either working a returning person would arrive as someone new every session. **The DM
recognises them instead, at the admission prompt.**

- **The prompt offers the campaign's stored participants.** In a campaign with stored participants,
  the DM can admit a joiner as a new player or as one of the stored entries. Choosing an entry mints
  nothing: the joiner becomes that participant and is told its existing UUID, under the same
  constraints as R-1.5c (only to that joiner, after admission, never before).
- **The product never suggests a match.** Nothing is preselected, ranked or highlighted by display
  name, character name or anything else; matching is never inferred (R-1.5). The default is a new
  player. The DM recognises the person, and the product only lists the entries.
- **Admitting stays one action.** A DM who ignores the list admits a new player exactly as before, and
  the prompt keeps its expiry and every other property (R-1.3l).
- **An entry holds one seat at a time.** An entry already held in this session is not offered, so two
  joiners never become the same participant.
- **A duplicate the DM chooses is accepted.** A DM who admits a returning person as new has made that
  choice, and R-1.6's list-and-delete is the remedy. The rule against duplicate growth (R-1.5d) is met
  by the product offering the match, not by forcing it.

**Acceptance criteria**
- **A-1.9n** Resume a campaign and map a returning player onto their stored entry: the stored roster
  has the same number of entries afterwards, and the joiner holds that entry's UUID.
- **A-1.9o** An entry already held in the session is not offered to a second joiner. A build that lets
  two joiners map onto one entry fails.
- **A-1.9p** A joiner whose display name equals a stored entry's label is shown with nothing
  preselected or ranked. A build that suggests the match fails.
- **A-1.9q** Admitting a joiner as a new player is still one action in a campaign with stored
  participants. A build that requires a mapping choice first fails.

### R-1.5f Returning players: the DM chooses, per campaign

- **Each campaign has a "Returning players" setting: "Ask me each time" (the default) or "Let them
  straight in".** It is reachable from the host's session window for the current campaign and can be
  changed mid-session. Its exact place is an engineering choice.
- **A returning joiner** is one whose encrypted request (R-1.3m) carries a participant ID the
  campaign store holds for this campaign, for an entry not already seated this session.
- **Ask me each time:** the prompt names the stored entry the joiner claims. The DM admits them as
  that entry, as a new player, or as another entry (R-1.5e), or denies them.
- **Let them straight in:** a returning joiner is admitted as their entry with no prompt, and the DM
  sees an ordinary join. A joiner with no matching claim is prompted as usual. The DM can still remove
  anyone.
- An Assistant cannot change the setting (R-1.3).

**Acceptance criteria**
- **A-1.9r** With the default setting, a returning joiner is prompted. A build that admits them
  unprompted by default fails.
- **A-1.9s** With "Let them straight in", a returning joiner is admitted as their stored entry without
  a prompt, and a joiner with no matching claim is still prompted.
- **A-1.9t** An entry already seated is never admitted a second time automatically; the second claim
  falls to the prompt.

### R-1.6 The DM's campaign store

- The DM's machine stores, **keyed by a campaign UUID generated locally**: participant UUIDs, the
  local labels or character names the DM has seen, and saved campaign state. The session code is
  stored against the campaign as its **most recent code, never as its key**, and never as its display
  name (R-1.5d). Keying by code would orphan a campaign whose code was taken at resume.
- This store is what makes resume possible. It is local, it is the DM's, and it never leaves the
  machine.
- **The DM can list every campaign their machine holds and delete one outright.** Afterwards no trace
  of its participants, UUIDs or state remains on disk.
- **This lives in settings, under "Campaign storage", and nowhere else.** The host flow and the
  campaign picker never offer deletion. It is kept separate from the player's own storage (R-1.5b).
  Deleting asks for a deliberate confirmation that states what is lost.
- Nothing in this store, and nothing exported from it, links a participant across two different session
  codes (product-overview D-8).
- **What leaves the machine is what the promise is about.** The DM's own local history may hold real
  character names (product-overview D-8); exports and relay traffic may not carry cross-code
  identifiers. Splitting into one file per campaign is not a justification for that promise, since two
  files in one folder link a person as well as one does, but it is kept because it **bounds blast
  radius**: people zip a folder into a bug report.

**Acceptance criteria**
- **A-1.10** The DM can list every campaign the machine holds, **including files the plugin cannot read
  or parse**, and delete any of them; afterwards no file on disk contains its participants, UUIDs or
  state.
- **A-1.10a** Campaign deletion is reachable only from settings. A delete control in the host flow or
  the campaign picker fails.
- **A-1.11** No export, and nothing sent over the relay, contains an identifier linking a player across
  two different session codes.
- **A-1.11a** **No exported artefact contains a participant identifier at all**, not one, not for one
  session (product-overview D-8's third clause, which A-1.11 and A-1.2a do not reach alone). Export a
  session's artefacts and assert no participant id appears in any of them, however scoped. "The export
  contains only what its owner could see" does not cover this, because the owner can see their own id.
  Reusing the campaign codec to build an export would emit participant ids and fail this. An
  identifier is a value that can be **joined**; a label computed at write time from the file's own
  ordering is not one, and authorship in an export is carried that way (product-overview D-20).
- **A-1.11a-note** A retained log is not an export, and the discriminator is not the directory. An
  export is an act someone asks for, never a setting and never a default; a retained log is written
  automatically. Local history may hold names (product-overview D-8), so it may hold a peer code. A
  write that leaves the machine is not an export either: it is a transmission, and is forbidden.
- **A-1.11b** No single campaign file contains more than one session code, so sharing one campaign's
  file does not disclose that a participant appears in another.
- **A-1.11c** A **peer code** is a participant identifier for A-1.11a and must not appear in an export.
  Session-scoped does not exempt it. This does not reach a retained log (A-1.11a-note).

### R-1.6a Campaign state saves itself

- **The DM never saves and is never asked to.** Every change to the shared state (roster, encounter,
  initiative, current turn and round, HP, statuses) is written to the campaign store shortly after it
  happens. A burst of changes may be gathered into one write.
- **A crash loses seconds, not the session.** A crash sends nothing and runs no shutdown (R-1.3g), so a
  save that happens only at a clean end is no save at all for the case that matters. How long a change
  may wait before it is written is engineering's; the rule is relative: a crash moments after an edit
  may lose that edit, and a crash a minute after it must not.
- **A clean end saves too**, so ending a session normally never loses the last changes.
- **An encounter is saved as it stands and resumes as it stands**: its combatants, order, current turn,
  round and HP. Saving never ends or discards an encounter; ending one stays explicit (initiative
  R-3.1).
- **An interrupted write never costs the last good save.** A crash during a write leaves the previous
  save readable.

**Acceptance criteria**
- **A-1.11d** Kill the DM's client mid-encounter a few seconds after an HP change, relaunch and resume
  the campaign: the HP change, the order and the current turn are all there. A build that saves only
  on a clean exit fails.
- **A-1.11e** Nothing in the host flow asks the DM whether to save. A save prompt, a save button the
  state depends on, or a "save before closing?" dialog fails.
- **A-1.11f** Kill the DM's client while a save is being written: on relaunch the campaign opens with
  either the new state or the previous one, never as unreadable.

### R-1.7 Say nothing false, and explain elsewhere

- **The plugin carries no security or privacy explanation and no warnings.** How the session is
  protected, and what the relay can see, is explained on the published relay policy page (R-1.9), not
  in the plugin.
- **Settings carries one link to that page**, and the plugin listing links it too, so it is never
  more than a couple of steps away.
- What copy the plugin does have is held to R-1.7a.

### R-1.7a No false claims

**Forbidden phrasings:** "anonymous", "private", "we can't see anything", "no one can see your
session", any claim that the relay cannot correlate sessions, or any claim that a session is verified
or protected against interception (R-1.3m). Each is false under product-overview D-8 or R-1.3m.

- **Assert a property, not a list.** Every user-facing string is engineering-authored under this
  constraint. A property binds strings that do not exist yet; a list binds only the ones someone
  wrote down.
- **A reversal sweeps the shipped strings before it is declared done.**

**Acceptance criteria**
- **A-1.7d** After any decision that reverses previously specified behaviour, no shipped string still
  describes the old behaviour.
- **A-1.7e** Every user-facing string meets R-1.7a: no phrasing from the forbidden list, and no claim
  that a session is verified or protected. A string asserting protection fails, however well written.
- **A-1.7f** Settings links to the published relay policy page, and no screen in the plugin carries
  a security or privacy explanation.

### R-1.7b Protocol version is checked at connect, and mismatch is a clear refusal

- The shared contract carries a **protocol version**. The client sends it when connecting; the relay
  compares it with its own.
- **On mismatch the connection is refused with a message naming the problem and which side is
  behind** ("this plugin is too old for this relay, update it" or "this relay is older than your
  plugin"), never a generic failure and never a silent degradation into partial behaviour.
- The version is **in the contract**, so it cannot change on one side without the other seeing it.
- Two reasons: it turns a silent runtime disagreement between client and relay built from different
  contract versions into a refusal at connect; and the relay will be updated while old plugins are
  still in the wild, which is the normal state of any deployed client/server pair. Without it an
  outdated plugin fails in whatever way the change happens to break it.
- This is a fourth ending under R-1.3c: a connection attempt that fails on version is neither
  relay-unreachable nor code-not-active, and must say so in its own words.
- The contract only grows (product-overview D-14): an old build keeps working against a new one, which
  is why D-14 is a product property and not only a coding standard.

**Acceptance criteria**
- **A-1.5i** A client whose protocol version differs from the relay's is refused at connect with a
  message naming the mismatch and which side is behind: not a generic failure, never a partial
  connection.
- **A-1.12a** For every action the protocol requires a client to send a message for, the message is
  actually sent, and a check fails when it is not. The check must tell two actions apart: for any two
  distinct actions, a run where one is absent reddens it while the other is present. It is stated over
  every action, not a list, so an action added later is covered. The distinguishing key is not fixed
  here; keying on message type or on send site has already collapsed relink into join.
- **A-1.31** A field once on the wire is never removed, renamed, repurposed or made required, and no
  message type changes meaning. Compared against the contract's previous shape, each fails separately;
  a change of meaning fails even where name and shape did not change.
- **A-1.31a** An old build still works against a new one across a contract change: the previous
  release's client runs against the current host. Passing A-1.31 while failing this satisfies only the
  letter.

### R-1.8 The relay is swappable, and unreachability is stated plainly

- A user can point the plugin at a **different relay**, and the setting is discoverable. The default
  relay is a default, not a dependency.
- The relay implementation is part of this project and deploys as an ordinary container, so a third
  party *could* run one. This is an escape hatch, **not a tested guarantee**. The abandonment risk is
  accepted: if the default relay stops, the plugin stops.
- When the relay is unreachable, the plugin says so promptly and distinguishes "the relay is down" from
  "your connection is broken" from "that session code is not active". Not a spinner, not "connection
  failed".
- The relay never becomes authoritative and is never asked for state. If it restarts, sessions
  re-establish from the DM's client (product-overview D-3).
- **The relay we ship writes nothing to durable storage during a session** (product-overview D-2). It
  forwards and forgets. "The relay stores nothing" is the basis for telling users their data stays on
  the DM's machine. This proves the relay we build, not that a deployed instance runs that build;
  deploying from the repository, not by hand, is the mitigation, and the published service policy
  states retention explicitly.

**Acceptance criteria**
- **A-1.5a** Two FFXIV clients on **two genuinely distinct network paths** (a phone hotspot counts, and
  is a good test) can join the same session through the default relay, with neither person
  configuring anything on their router. Both clients dial outbound.
- **A-1.5a-r** The relay's own logs, read after an A-1.5a attempt, show the connection outcome and the
  reason for any failure.
- **A-1.5b** With the relay unreachable, both sides are told promptly, and the message distinguishes
  relay-down from connection-broken from code-not-active, never an indefinite spinner. **Tested against
  the network condition, never the plugin's own classification:** a refused port, a *dropped* port, and
  a live relay asked for a code it never claimed each yield their own correct message. Asking the
  plugin which category it chose tests the classifier against itself.
- **A-1.5c** A user can point the plugin at a different relay and a session works through it.
- **A-1.5e** The relay implementation we ship writes nothing to durable storage during a session. The
  detector is itself measured: a probe writes to each realistic destination (the working directory,
  `AppContext.BaseDirectory`, the temp roots) and the test fails if any goes undetected. A bare relative
  filename is the default write path, not an exotic one.
- **A-1.5j** Every failure message is true of the state that produced it. A message asserting a fact
  the client has not established ("the relay is reachable, so this is not your network" on a path with
  no connection check) fails, even if promptly shown and correctly categorised. A message may say less
  than it knows, never more. A-1.5b supplies the conditions that exercise this.

### R-1.9 Encrypt it, and publish what the relay can still see

- Session payloads are end-to-end encrypted between members (product-overview D-11). The relay carries
  ciphertext.
- The published relay policy page (R-1.7) states what the relay **can** still observe even so:
  - that a connection exists;
  - roughly when, and how much;
  - the network address it came from;
  - **which session code a client is on.**
- **The session code is plaintext to the relay by necessity: the relay routes on it.** Do not "fix"
  this by encrypting the code; that breaks routing and falsifies nothing.
- **This requirement is the single source for what the relay can observe, and the published relay
  policy is its one public statement.** The policy is updated whenever this list changes. Every other
  document, README, release note or plugin screen links to the policy rather than restating the
  list. A restatement is a copy that will not be updated when this one is; the same false claim once
  lived in three documents that did not cite each other.
- No copy states or implies that the relay cannot read anything a client sends, and no copy overstates
  the guarantee in either direction. Encryption hides content, not the fact of a conversation.

**Acceptance criteria**
- **A-1.5f** Session payloads reaching the relay are ciphertext; the relay holds no key and can decrypt
  nothing. Bounded by A-1.5f-a, because the session code is plaintext by design.
- **A-1.5f-a** The ciphertext assertion **discriminates**: in one run, a deliberately leaked roster
  field makes it fail, and the session code present in the frame (including when a peer code
  legitimately equals it) leaves it passing.
- **A-1.5f-b** Fixture values are ones the product can actually generate. A fixture chosen so it cannot
  collide with a real value is chosen so the test cannot discriminate.
- **A-1.13b** Outside this spec, only the published relay policy states what the relay can observe,
  and it matches R-1.9. Every other mention links to it. A second independent statement fails even if
  currently accurate.

## Open questions

- Open question: a version refusal is a fourth ending under R-1.3c (R-1.7b); its timing bound is not
  specified, so it has no row in R-1.3c's table.

## Retired IDs

- Fingerprint comparison, retired by the connection redesign (2026-10-01-connection-design.md); keys
  are now exchanged first and nothing is compared (R-1.3m): R-1.3a, R-1.3a-i, R-1.3a-ii,
  R-1.3a-iii, R-1.3a-iv and R-1.3d; A-1.2b, A-1.2c, A-1.2e, A-1.2f, A-1.2o, A-1.2p, A-1.2q, A-1.2r,
  A-1.3f-1, A-1.3f-2, A-1.3f-3 and A-1.3f-4. R-1.3d's per-request label survives as R-1.3d-1.
- In-plugin disclosure copy, retired by the same redesign; explanation moved to the published relay
  policy (R-1.7): R-1.7a's quoted strings, A-1.2h, A-1.7c and A-1.9l (the picker's literal notice).
- A-1.3f: superseded by A-1.3f-1, A-1.3f-2, A-1.3f-3 and A-1.3f-4, themselves since retired. It said
  what both parties see but not when.
- A-1.5: the NAT-traversal criterion of the abandoned peer-to-peer design, superseded by A-1.5a. No
  traversal technique works behind carrier-grade or symmetric NAT, so the transport became a relay.
- A-1.5d: withdrawn. A self-hosted relay is an escape hatch, not a tested guarantee (R-1.8).
- A-1.12: superseded by A-1.12a. It was a list of three things to test, and the list omitted the join
  request.
- A-1.12b: process check, not a product property (the test run reports a non-zero passed count with no
  skipped assembly).
- A-1.13b-collision, A-1.11b-collision: bookkeeping for two reused criterion numbers, not a product
  property. A-1.13b and A-1.11b keep their original meanings; the reused rows are A-1.13d and A-1.11c.
- Annotation rows folded into the criterion they annotate, with their product content kept there and
  their history dropped: A-1.2u-oracle and A-1.2v-note (into A-1.2u and R-1.3j), A-1.2v-2-note (a
  status record), A-1.2x-note (into A-1.2x), A-1.5b-note (into A-1.5j), A-1.7c-note (process check,
  not a product property), A-1.9k-5-note (into A-1.9k-5), A-1.9m-note and A-1.9m-vacuity (into
  A-1.9m), A-1.12a-note and A-1.12a-note-2 (into A-1.12a), A-1.16a-note (into A-1.16a), A-1.24-note
  (into A-1.24), A-1.27-r (into A-1.27), A-1.30-note (into A-1.30).
- A-1.2w-note: became A-1.2x.
