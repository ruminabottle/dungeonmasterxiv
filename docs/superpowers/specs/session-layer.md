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
- The DM sees a prompt showing the requester's **chosen display name alongside the key fingerprint**,
  with accept and deny. The plugin reads no chat, so a display name is something the player chose to
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
  their chosen display name alongside the key fingerprint, with accept and deny.
- **A-1.3** On accept, the player's client shows it is in the session. On its own this is a
  self-report, so it is bounded by A-1.3a.
- **A-1.3a** On accept, session state actually flows to that client: roster, events, the picture the
  host is authoritative for. Asserted on what the client **receives**, not on what it displays. A
  client showing "you are in the session" while nothing reaches it fails.
- **A-1.4** On deny, the player's client says so and receives no session state whatsoever.

### R-1.3a The fingerprint must be compared, not displayed

The key exchange (product-overview D-11) puts a fingerprint on the admission prompt. That fingerprint
**is the entire defence against interception**: admission protects a session only if the DM can tell
the right key from a substituted one. A forgeable or unread fingerprint does not make encryption
slightly weaker; it inverts the guarantee while the UI keeps claiming it holds.

- **Rendering:** the R-1.2a alphabet, in groups of three. One speakable alphabet for the whole
  product.
- **One fingerprint, derived from both public keys together, shown identically on both screens.** Not
  two fingerprints:
  - One thing to read aloud, not two. Two doubles the reading and halves the chance anyone finishes.
  - Nobody can verify one direction and forget the other.
  - It is symmetric: either party reads it out and the other confirms.
- **Interaction.** The admission prompt **requires the DM to actively confirm** that the fingerprint
  matches what the joining player told them: a deliberate act, not a displayed string and not a
  pre-ticked box.
- The comparison happens **out of band** (voice chat, Discord, whatever the group already uses). The
  plugin cannot carry it, because a channel an attacker controls cannot verify that attacker. The UI
  says this in plain words.
- A DM **may admit without comparing**; a session is not blocked on a step some groups will skip. That
  admission is recorded and shown as unverified, and the UI never describes that session as protected
  against interception.
- The ceiling that sized the fingerprint: **a DM will read aloud roughly eight to twelve characters
  before people start skipping the step**, and a skipped check is worse than an absent one because
  the UI records that it happened. If a security floor ever exceeds that ceiling, it is a product
  conflict to resolve with a different comparison mechanism or an honest statement of the defence's
  limits, never by picking a number in the middle.

**Acceptance criteria**
- **A-1.3f-2** The combined fingerprint differs if either public key is substituted, in both
  directions independently.
- **A-1.3f-4** The same fingerprint is visible on both screens at the moment of comparison, in the
  R-1.2a alphabet, grouped three-three-three-two.

### R-1.3a-i The joiner can compare before the DM decides

- The host's public key reaches the joining client **before** admission, and the joining client can
  compute and display the combined fingerprint before the decision is made, not as part of learning
  the outcome (product-overview D-11).
- **A confirmation control the counterparty cannot take part in is a false control**, worse than none
  because it records success. The DM's prompt never offers a "the code matched what they read to me"
  affirmation where the joiner has nothing to read.
- Whether the host key travels as a new field or a new message is an engineering choice. It is
  additive either way (product-overview D-14).
- **Failing safe is not silence.** If the host key is unavailable when the prompt is shown, the
  prompt says the check cannot be performed rather than presenting an unverifiable value. Admitting
  without comparing stays available on R-1.3a's terms.

**Acceptance criteria**
- **A-1.3f-1** The joining client can compute the combined fingerprint before it has been admitted. A
  run in which a joiner is admitted without the host's public key having reached that client
  beforehand fails.

### R-1.3a-ii A security control does not silently survive a peer that ignored it

Receivers ignore messages they do not recognise (product-overview D-14), which keeps old plugins
working. When an additive message **carries a security control**, the sender cannot tell "the peer
acted on this" from "the peer ignored it". So, for any control whose validity depends on a peer
having received an additive message:

- **The control does not present as performed unless the peer's participation has been
  established.** Presenting it on the strength of having *sent* the message is a false control,
  whatever the reason the peer could not take part.
- How participation is established is an engineering choice: suppressing or qualifying the control
  when participation is unknown satisfies this, and so does an acknowledgement round trip.
- This does not weaken the rule that unknown messages are ignored. A security-relevant additive
  message needs its degraded case designed rather than inherited.

**Fingerprint length and prompt expiry are a pair, and this requirement owns the pairing.**

- The fingerprint is **eleven characters (about 50.4 bits)**, in the R-1.2a alphabet, grouped
  three-three-three-two. The length is this requirement's; the alphabet is R-1.2a's.
- The admission prompt **expires** after the window R-1.3l sets. What is required here is that the
  window is bounded at all: a prompt left open while a client runs overnight must not exist. Against
  a prompt that expires, grinding a second preimage at this length is hopeless.
- **If the expiry is ever removed, the fingerprint must go to 14 characters** (64 bits) and the
  usability problem returns. Do not change one without the other.

**Acceptance criteria**
- **A-1.2f** The confirmation control is suppressed or qualified when the joining client **could not
  compare**. A build offering an unqualified confirmation against a client that received no host key
  fails. The criterion is conditioned on that fact, never on the presence of a receipt: absence of a
  receipt does not discharge it (R-1.3a-iii). Because no state-2 producer exists today (R-1.3a-iv),
  the "qualified" branch is the live one and suppression cannot legitimately fire.
- **A-1.3f-3** In no session is a confirmation control offered for a comparison the counterparty did
  not have the means to perform. Whether a joiner holds the key depends on the **peer's version at
  runtime**, so this is judged per session, not per build.

### R-1.3a-iii Capability may be signalled; human action may not

- **A signal that the joining client COULD compare is legitimate.** It is a protocol fact (the client
  received the host key and rendered a fingerprint), and it is how R-1.3a-ii distinguishes an old
  build that ignored the host key from one that did not.
- **A signal that the joining human DID compare is forbidden.** It would travel the same channel an
  attacker who substituted the host key controls, so it can be forged exactly when it matters.
- The DM's UI **may** suppress or qualify the confirmation control when the joiner's client could not
  compare.
- The DM's UI **must never** state or imply that the joining person compared, checked, confirmed or
  agreed. No copy, tick or icon that reads as it. A capability signal is never rendered as an action
  signal.
- What the DM confirms is their own out-of-band comparison, unchanged.

**Silence is not evidence, in either direction.** A missing receipt can mean an old client that sends
none (cannot compare), a relay that lost it (unknowable), or a fast admission that closed the send
window before the joiner processed the host key (can compare, and did). A host admitting 171ms before
the joiner processed the host key produced exactly the third case.

- The UI asserts neither direction it has not established: not that the joiner compared, and not that
  it could not. Suppressing the control *on the grounds that the joiner could not compare*, from
  silence alone, asserts the second.
- **A third state is required: not established**, distinct from both. Its look is an engineering
  choice.
- Suppression is not the safe default it appears to be: in the fast-admission case the joiner can
  compare, so suppression buys no security, and a control that is usually suppressed teaches DMs to
  ignore it.

**Acceptance criteria**
- **A-1.2e** No UI states or implies that the joining person compared, checked or confirmed the
  fingerprint. A capability signal rendered where a DM reads it as an action signal fails. Judged by a
  reader shown only the DM's screen and asked what the joiner did.
- **A-1.2o** Where the host has not established whether the joiner could compare, the UI says so and
  asserts neither direction. Implying they compared and implying they could not each fail separately.
- **A-1.2p** A fast admission does not by itself degrade the reported verification state. Admit
  before the joiner processes the host key and assert the DM is not told the joiner could not compare.
  A build in which clicking faster degrades the reported state fails. This is a forced-timing case.

### R-1.3a-iv What the three states name

The state is **what the host has established about the joiner's capability at the moment of the
decision**: not what is true of the joiner, but what the host has grounds to assert. How it is
represented is an engineering choice.

1. **Established capable:** positive evidence the joiner held the host key and could render the
   fingerprint.
2. **Established incapable:** positive evidence it could not.
3. **Not established:** neither. **This is the default and initial state.**

- A representation whose zero value means *incapable* fails by construction: absence and incapability
  would share one value.
- A state is entered only on positive evidence: never from silence, a timeout, or elapsed time.
- Nothing reaches state 2 by exhausting state 3. "We waited and heard nothing" is state 3 held longer.
- **State 2 has no producer today, and that is correct.** The protocol version cannot distinguish the
  case: an old client that ignores the additive message carries the same version and connects
  normally (R-1.7b). So the reachable states are two, and a build that suppresses the control is
  asserting state 2 without a producer for it.

**Acceptance criteria**
- **A-1.2q** The initial and default state is not established. Asserted before any evidence arrives,
  which is where a two-valued type gives itself away.
- **A-1.2r** No path reaches established incapable without positive evidence. Silence, a timeout and
  elapsed time each fail, including in a run where the wait is long.

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
| Reaching the relay | short, seconds | The relay could not be reached, distinct from the next rows (R-1.8) |
| Looking up the session code | short, seconds | That code is not an active session. Not "denied", not "failed" |
| Version check at connect | immediate | Which side is out of date (R-1.7b) |
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

### R-1.3d The fingerprint is the one security-bearing identifier

- The key fingerprint is the single security-bearing identifier at admission, and **there is exactly
  one value both parties read aloud** (R-1.3a). A second mutual read-aloud value is not added.
- The prompt names a requester by display name plus fingerprint (R-1.3e), not by a session-scoped
  code. The fingerprint is per key, so it already tells two concurrent requests apart, including two
  that send the same display name.
- If a per-requester **peer code** exists, its job is only to tell concurrent pending requests apart on
  the DM's screen. The joiner need not see it. It is session-scoped (the same person in two sessions
  does not present the same value), not derivable from a character name or account, not stable across
  campaigns, and absent from every export (A-1.11c). Whether one exists is an engineering choice.
- Any future need for the joiner to see a shared label is a new product decision.

**Acceptance criteria**
- **A-1.2a** No identifier the admission prompt or roster uses links a participant across two
  different session codes, and no display name appears in any export (product-overview D-8).

### R-1.3e The display name is shown, and never authenticates

- **The admission prompt shows the requester's chosen display name alongside the key fingerprint.**
  Both, together. The name defaults to the character name and may be changed to an alias.
- **A participant can see and change the name they will send, before it is sent, pre-filled with
  their character name.** The default is allowed only because the alternative exists; with no way to
  change it, nothing is "chosen", and the product's disclosure copy (R-1.7) becomes false.
- **The choice lives in the join flow, not in settings.** A user who never opens settings never
  learns what is about to be sent on their behalf. Settings may hold a persistent default that
  pre-fills the join-flow field; they may not replace it. The general rule: *a control that protects
  against something the user does not know about must appear on the path they are already taking.*
- Persistence across sessions and per-campaign aliases belong to the rolls area (rolls R-2.5); the
  minimum here is see it, change it, before it is sent.
- **The limitation is stated in the UI** and never papered over (product-overview D-8).
- **The name is never what the DM authenticates on.** It is self-declared, unverified and trivially
  spoofable; the fingerprint is the security-bearing element. A prompt showing a name and hiding the
  fingerprint fails, and so does one showing the fingerprint so quietly that the name is what the DM
  acts on.
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
- **A-1.2b** The admission prompt shows the display name **and** the key fingerprint. A prompt
  rendering the name without the fingerprint fails.
- **A-1.2c** A prompt that de-emphasises the fingerprint relative to the name fails. Judged by a person
  who was not told which value matters.
- **A-1.2d** Two concurrent requesters sending the same display name remain distinguishable to the DM,
  and admitting one does not admit the other.
- **A-1.2g** A player can see and change the name they will send, before it is sent, pre-filled with
  their character name, and what reaches the session is what they set. Four failures, each separate: a
  value sent with no way to change it; a blank rather than pre-filled field; a change made after the
  name is on the wire; a settings value that never reaches the wire. Asserted on what leaves the
  client.
- **A-1.2h** The UI states that the name sent is the player's choice and defaults to their character
  name.
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
   a bidirectional override can push the fingerprint out of the DM's eyeline or reorder what they read.
   That is the substitution attack arriving through the one field an attacker controls.
3. **Bounded in length, so the fingerprint stays visible beside it.** The bound is **32 grapheme
   clusters**, counted in a unit that does not vary by script. Code units would refuse a Vietnamese or
   Devanagari name that a Japanese name of the same visible length passes. **The rule behind the
   number: the limit never rejects a name FFXIV itself permits**, because the character name is the
   pre-filled default. 32 assumes the game's maximum is 31 (15 + space + 15); if the real maximum is
   higher, the number rises and the rule does not change.
4. **Legitimate names are accepted.** Diacritics and combining marks, including decomposed forms, are
   ordinary. An allowlist that admits nothing refuses every hostile input and looks perfect, so this
   half fails silently.
5. **Any script, no restriction.** Japanese, Korean, Cyrillic, Arabic, any of them. FFXIV is global,
   and since the default is the character name, a script restriction would make the default invalid
   for exactly the players it excluded.
6. **`DM`, `GM`, `Dungeon Master` and `Game Master` are reserved to the host and may not be chosen as
   an alias**, matched case-insensitively after whitespace normalisation.
   - **The reservation reaches a chosen alias, never a player's actual character name.** A character
     name comes from the game and is true; a player genuinely called `Dungeon Master` is not imitating
     anyone. An alias is free and instant, and that is the whole attack surface. FFXIV needs a
     forename and a surname, so only `Dungeon Master` and `Game Master` can collide with a real name.
   - A blocklist leaks (`D.M.`, `the DM`, small caps, homoglyphs). Reserved names close the obvious
     case cheaply; the session role (rolls R-2.7a) carries the guarantee. Neither alone suffices.

- An unreadable or look-alike name cannot admit anyone, because the fingerprint authenticates. The
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
- **A-1.2l** A name long enough to displace the fingerprint is refused, and the fingerprint stays
  visible beside the longest accepted name.
- **A-1.2m** The check accepts a name in a non-Latin script: Japanese, Korean, Cyrillic and Arabic
  each pass, each a separate failure.
- **A-1.2s** Two names of the same perceived length are treated the same way whatever their script. A
  build accepting a Latin name at the limit and refusing a Japanese, Korean, Arabic or combining-mark
  name of the same perceived length fails. Only a length-matched pair exposes this; a short non-Latin
  name passes any code-unit limit.
- **A-1.2t** The longest name FFXIV itself permits is accepted, pre-filled and unedited. Needs the
  real game, since its maximum is not recorded anywhere.
- **A-1.2u** The length boundary is exercised with a **non-ASCII** name. A boundary built from a
  repeated ASCII character cannot tell grapheme clusters from code units. The expected bound is stated
  independently of the production constant, so changing that constant makes the boundary check fail.
- **A-1.2v** A name refused for length is refused **visibly**: never silently truncated, never
  silently dropped, and no layer silently stops accepting input. The words are an engineering choice
  under R-1.7a's constraints.
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

- If the host becomes unreachable, player clients hold their last known state and clearly show they
  are reconnecting, with the state visibly marked as no longer live.
- If the host returns within the grace window, clients resync **from the host** (product-overview
  D-3). Clients never reconcile with each other.
- If the grace window expires, the session ends and every client says so plainly. It never keeps
  showing stale data as though it were live.
- **The grace window is five minutes**, and it is a settable value, not a magic number, because the
  right length is empirical. Relaunching FFXIV takes minutes, so a shorter grace would end the session
  on any DM crash; five gives one relaunch attempt. It is the same value as the seat window (R-1.5a):
  the two never tick together, so one number serves both.

**Acceptance criteria**
- **A-1.6** Killing the host's connection makes player clients show "reconnecting" with state
  visibly not live; restoring it within the grace window resyncs them from the host.
- **A-1.7** Letting the grace window expire ends the session on every client, stated plainly, with no
  stale data shown as live.

### R-1.5 Relink: every session, DM-approved

- A client's identifier is fresh on every plugin launch (product-overview D-8).
- A returning player in a known campaign is offered relink to their existing participant UUID.
- **The DM approves every relink, every session.** Relink is never silent, never automatic, and never
  inferred from a character name.
- Until relink is approved, that client is not in the session.
- **While relink cannot be completed end to end, that is a known limitation and it is stated.** A
  build that has never delivered relink, and offers no control claiming it, regresses nothing and
  claims nothing by shipping without it, provided the limitation is disclosed (release notes, and the
  host-flow picker per R-1.5d; product-overview D-18). Disclosure does not make the criteria below
  met.

**Acceptance criteria**
- **A-1.9** A returning player is offered relink and receives no state until the DM approves it.
- **A-1.9a** A relink claim is **sent** by the joiner, **read** on arrival, **resolved**, and **reaches
  the DM's approval path**, and each of the four fails separately. A build that sends a claim nobody
  reads fails as surely as one that sends nothing, with nothing null, nothing thrown and no existing
  test red.

### R-1.5a Resuming is not relinking: the key says who, the window says how long

    same key, within 5 minutes   ->  resumes, no re-approval
    same key, after 5 minutes    ->  relink, full approval (product-overview D-8)
    different key, any time      ->  relink, full approval
    deliberate quit              ->  removed immediately; returning means a new key, so relink

Two questions, two mechanisms (product-overview D-17):

- **The key answers who may resume**: only a client still holding the pair it was admitted under. A
  window alone would admit whoever turns up inside it.
- **The window answers how long the seat is held.** A key alone is unbounded: the host would hold a
  slot for someone never coming back.
- **Resumption is gated on proof of possession, never on presentation.** A returning client
  demonstrates it holds the private key. A public key used as a bearer token is a value that travels,
  and a value that travels is not a secret (product-overview D-11). The construction is an engineering
  choice. Only one client can prove possession, so "two clients claim the same key" cannot arise.
- **A relink supersedes a stale seat; it never sits alongside one.** A relink under a new key clears
  any seat still held for that player. The harm being prevented is a player **locked out by their own
  ghost**, as in Foundry VTT's defect foundryvtt#13161 (a disconnected user "active" and unable to
  rejoin for over ten minutes). The case is common: a crash-and-relaunch produces a fresh key, so it
  is a relink, not a resume.
- **A crash costs a DM re-approval, and that is the design working.** A fresh launch means a fresh
  key, a fresh key means a relink, and a relink means the DM approves. That is the price of refusing
  a portable identifier (product-overview D-8). Persisting an identifier across launches to smooth
  this would undo that decision.
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
- **A-1.20** The same client returning after the window relinks, with full approval. Resuming silently
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
- **The player may delete their own participant UUID, per campaign, without the DM's involvement and
  without the DM being told.** The DM can delete a campaign outright (R-1.6); the subject of an
  identifier needs the same control over their own copy.
- **No notification to the DM.** One would manufacture a signal linking a deletion to a player. The DM
  learns when relink is not offered.
- **Deleting ends the possibility of relink, and the player is told so before the deletion**: they
  will rejoin as a new participant needing fresh approval. The less a user understands what they are
  destroying, the more friction the destruction gets, so this is never a one-click delete.
- **A player can see what they are storing, per campaign, before deleting it.** You cannot meaningfully
  delete what you cannot see.

**Acceptance criteria**
- **A-1.9b** A player can list what their client stores per campaign, and delete one campaign's
  participant UUID without the DM's involvement. Afterwards no file on their disk contains that UUID. Offering deletion
  without first showing what is stored fails.
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
- **It need not be unforgeable.** Relink is DM-approved every time, so the DM is the check and the UUID
  is not a bearer token. Resumption's proof of possession (R-1.5a) governs a different path.
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
- **The picker never offers continuity the build cannot deliver** (product-overview D-18). Any of three
  answers is acceptable, and the choice is an engineering one:
  1. ship participant creation that gives a resumed campaign a real roster;
  2. have the host flow not offer resumption while it cannot honour it; or
  3. **disclose it at the picker itself**, where the false belief would form. A release note is not
     sufficient.
- **A resumed roster must not grow by one duplicate entry per join.** A participant appended with a
  fresh id on every admission makes a four-person weekly game hold eight entries after a fortnight and
  sixteen after a month, all bearing the same labels. That replaces an empty roster with one that is
  wrong, gets wronger, and cannot be seen to be false. Answer 1 does not discharge the rule unless
  repeats are recognised (the DM mapping an arriving joiner onto an existing entry, or the roster
  distinguishing "has played before" from "arrived this session").
- **Do not "fix" the empty roster by minting participants at the picker.** No durable joiner identity
  exists (joiner keys are per request, a peer code is per session, a display name is not an identity,
  and the participant id is only as durable as the joiner's retained copy, A-1.9g). Minting produces
  one phantom per join, not per person, and it persists: nothing records which duplicates were one
  person, so no later migration can repair the file. **An empty, honest roster is the correct state**,
  not a gap. What is required here is the campaign half; recognising the same person automatically is
  relink (R-1.5).
- **The picker's disclosure, literal**, shown at the control:

  > Resuming keeps this campaign, but not its players. Everyone arrives as someone new, and the
  > roster stays empty until recognising returning players is built. Nothing has been lost.

  Punctuation is not load-bearing; the three claims are, one per sentence: resumption will not restore
  participants; the state is empty **and** temporary; the campaign is intact (a missing feature, not
  lost data). Do not add "...and you will admit them again": the DM admits every joiner every session
  regardless, so naming it misdescribes what is missing.
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
- **A-1.9l** The picker states, at the control, that a resumed campaign's roster is empty until relink
  exists, and **its absence fails**. Presence and placement are checked mechanically, so a later change
  cannot quietly remove it.
- **A-1.9m** No participant is minted to populate a resumed campaign's roster; a build that creates
  participants at the picker fails. This is about **where** a participant is minted, not whether:
  A-1.9f requires admission to mint one. The two are a complementary pair over the same operation.

### R-1.6 The DM's campaign store

- The DM's machine stores, **keyed by a campaign UUID generated locally**: participant UUIDs, the
  local labels or character names the DM has seen, and saved campaign state. The session code is
  stored against the campaign as its **most recent code, never as its key**, and never as its display
  name (R-1.5d). Keying by code would orphan a campaign whose code was taken at resume.
- This store is what makes resume possible. It is local, it is the DM's, and it never leaves the
  machine.
- **The DM can list every campaign their machine holds and delete one outright.** Afterwards no trace
  of its participants, UUIDs or state remains on disk.
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

### R-1.7 Say what this is, honestly

- The UI states that participants are known to the session by the **display name their client sent**,
  which defaults to their character name and can be changed to an alias. No copy implies anonymity from
  other participants, and none implies the choice hides you from your own client.
- The UI states that the session code is not a secret and admission is what protects the session.
- Both are requirements, not documentation tasks (product-overview D-8).

### R-1.7a Disclosure copy: the literal strings

Wording here is a product claim. **A string this requirement quotes is used verbatim, and the shipped
string is byte-identical to it.** Byte identity is what makes a disagreement between this document and
the build decidable instead of arguable.

**On the session window, where the code is shown:**

> Your session code is not a secret. Anyone who has it can ask to join — you decide who gets in.

**On the admission prompt:**

> The name shown is chosen by the requester, not proof of who they are - the code is. Only admit
> people you arranged to play with.

It denies the name any authority in the same breath as admitting one is shown. It is two sentences on
purpose: the prompt's order (headline, fingerprint, out-of-band instruction, unticked confirmation) is
load-bearing and nothing may push the fingerprint down. "Chosen by the requester" is accurate whether
they took the default or set an alias.

**In settings, under the heading "What this plugin knows":**

> During a session, this plugin knows who is in the room. The game gives it character names, and
> nothing can change that. What it does with them is the part we control: names are never written to
> a log, never included in an export, and never linked between one campaign and another.
>
> Your session is encrypted end to end, and the relay cannot read what you say inside it. One field
> is different: the name sent when you ask to join a session travels in the clear, because you and
> the DM have not yet exchanged keys at that moment. Today that name is your character name, and
> changing it is not yet built - so asking to join tells the relay operator who you are. The relay
> can also see that a connection exists, roughly when and how much, and the network address it came
> from. Encryption hides what you say, not that you are talking.
>
> Campaign history stays on the DM's machine. There is no account, no server storing your sessions,
> and nothing to delete anywhere but here.

Hyphen style is the codebase's, not an em dash. The sentence "changing it is not yet built" dates
itself on purpose and must be updated when the join-flow name choice ships (see Open questions).

**Forbidden phrasings:** "anonymous", "private", "we can't see anything", "no one can see your
session", or any claim that the relay cannot correlate sessions. Each is false under product-overview
D-8, the last even with encryption.

- **A ruled string may also live outside this requirement** (the picker disclosure in R-1.5d). This
  requirement governs exactly the strings it quotes; the test is the quotation, not a list.
- **Byte-pin where the words are the decision; assert a property where the class is the decision.**
  Every other user-facing string is engineering-authored under this requirement's constraints: no
  forbidden phrasing, and no claim that a session is protected when nobody checked. An enumeration
  binds the strings someone listed and is silent on the next one; a property binds strings that do not
  exist yet.
- **A reversal sweeps the shipped strings before it is declared done.** Protecting copy from casual
  substitution also protects it from correction.

**Acceptance criteria**
- **A-1.7c** Every string R-1.7a quotes is byte-identical to the shipped string, compared mechanically.
  A mismatch fails without adjudicating which side is right.
- **A-1.7d** After any decision that reverses previously specified behaviour, no shipped string still
  describes the old behaviour.
- **A-1.7e** Every engineering-authored user-facing string meets R-1.7a's constraints: no phrasing from
  the forbidden list, and no claim that a session is protected when nobody checked. A string asserting
  protection that was never verified fails, however well written. This covers strings R-1.7a does not
  quote (the out-of-band instruction, the unverified warning, the read-your-code-aloud prompt, the
  no-code-to-compare notice, the admitted-uncompared notice, the code-changed warning, and any new
  one).

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
- A version refusal is its own ending under R-1.3c: neither relay-unreachable nor code-not-active.
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

### R-1.9 Encrypt it, and say what the relay can still see

- Session payloads are end-to-end encrypted between members (product-overview D-11). The relay carries
  ciphertext.
- The UI states plainly what the relay **can** still observe even so:
  - that a connection exists;
  - roughly when, and how much;
  - the network address it came from;
  - **the display name a client sends at join**, which by default is the real character name;
  - **which session code a client is on.**
- **The session code is plaintext to the relay by necessity: the relay routes on it.** Do not "fix"
  this by encrypting the code; that breaks routing and falsifies nothing.
- **This requirement is the single source for what the relay can observe.** Any document, policy,
  README, release note or UI string describing it references R-1.9 rather than restating the list. A
  restatement is a copy that will not be updated when this one is; the same false claim once lived in
  three documents that did not cite each other.
- No copy states or implies that the relay cannot read anything a client sends, and no copy overstates
  the guarantee in either direction. Encryption hides content, not the fact of a conversation.
- This sits alongside R-1.7's disclosures. Both are requirements, not documentation tasks.

**Acceptance criteria**
- **A-1.5f** Session payloads reaching the relay are ciphertext; the relay holds no key and can decrypt
  nothing. Bounded by A-1.5f-a, because the session code is plaintext by design.
- **A-1.5f-a** The ciphertext assertion **discriminates**: in one run, a deliberately leaked roster
  field makes it fail, and the session code present in the frame (including when a peer code
  legitimately equals it) leaves it passing.
- **A-1.5f-b** Fixture values are ones the product can actually generate. A fixture chosen so it cannot
  collide with a real value is chosen so the test cannot discriminate.
- **A-1.13b** Exactly one place states what the relay can observe, and every other mention is a
  reference. A second independent statement fails even if currently accurate.

## Open questions

- Open question: what happens to an in-progress encounter when the session ends: is it saved for resume
  automatically, or does the DM choose? It blocks the session log and resume area.
- Open question: whether five minutes is the right grace and seat window in play is empirical and
  unmeasured. The value is settable (R-1.4, R-1.5a); this blocks nothing.
- Open question: the settings copy in R-1.7a says "Today that name is your character name, and changing
  it is not yet built". The join flow now lets a player see and change the name (R-1.3e), so that
  sentence has reached its own expiry. Replacement wording is a product decision; until it is made,
  A-1.7c and A-1.7e pull in different directions for that paragraph. The same paragraph also
  restates what the relay can see ("a connection exists, roughly when and how much, and the network
  address it came from") and omits the session code, while R-1.9 is the single source for that list
  and A-1.13b fails any second statement of it. So A-1.7c (the shipped paragraph is byte-identical to
  the ruled one) and A-1.13b cannot both pass for this paragraph. Whether the copy should reference
  R-1.9 instead, or carry the full list, is a product decision; it blocks A-1.7c and A-1.13b holding
  together, and any settings copy change.
- Open question: admission creates a participant for every admitted joiner (A-1.9f), and without
  relink (A-1.9g) a returning person arrives as a new participant, so a campaign's stored roster grows
  by one entry per join. That is the duplicate growth R-1.5d forbids for a resumed roster. Which answer
  applies (the DM mapping a joiner onto an existing entry, the roster marking repeats, or deferring
  admission-time creation until relink works) is undecided. It blocks A-1.9f and R-1.5d holding
  together.
- Open question: the see-and-delete right is per campaign (R-1.5b, A-1.9b), but the joiner cannot
  know a campaign's identity and stores its participant UUID under the session code it was admitted on
  (R-1.5b); the code keys this store by session code, one entry per code. A campaign's code can change
  at resume (R-1.2a), so one campaign may leave several entries under different codes, and an entry
  is not the same unit as a campaign. How a per-campaign listing and deletion map onto per-code
  storage, and whether a returning player is still offered relink when the DM's code has changed, are
  undecided. It blocks A-1.9b's per-campaign unit and A-1.9 for campaigns whose code moved.

## Retired IDs

- A-1.3f: superseded by A-1.3f-1, A-1.3f-2, A-1.3f-3 and A-1.3f-4. It said what both parties see but
  not when, so a build showing the fingerprint only after admission met it.
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
