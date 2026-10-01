# Product overview: product spec

Describes the current product. Converted 2026-09-29 from `brief.md` and `product-directives.md`;
the originals are archived at
`~/archive/dungeonmasterxiv-agent-team-2026-09-29/claude/team/product/brief.md` and
`~/archive/dungeonmasterxiv-agent-team-2026-09-29/claude/skills/deployment-manager/product-directives.md`.

Area specs cite the principles below as `product-overview D-n`.

Platform reference: <https://dalamud.dev/>. Internal name (permanent): `DungeonMasterXIV`.

## Purpose

A Dalamud plugin for FFXIV that tracks dice rolls, initiative order, combatant HP and status,
character sheets, and a session log for players running tabletop-style RP campaigns in game.

**Everyone playing installs it. Nobody can take part without it.** A person without the plugin sees
nothing, because the session travels over the relay rather than through the game. A **DM hosts a
session**. Players join with a session code, and the DM admits each one individually. While the
session runs, the DM's client is the single source of truth for everything the group shares, and it
pushes state to the admitted players. When the session ends, it is over.

Traffic passes through a **relay**, and the word is deliberate. It is not a server, and its
directory is not named like one, because a name argues for a shape: something called "server" invites
storage. The relay is a dumb pipe. It forwards messages between session members and stores nothing:
no accounts, no session state, no campaign data, no content at rest. The plugin ships pointing at a
default relay, and anyone can run their own and point the plugin at that instead. The relay is never
authoritative. Campaign data lives on the DM's machine and only there.

The plugin observes the game and displays. It never sends input into the game: no chat, no commands,
no actions. The one thing it writes into the game client is a **local echo line**, shown in the
player's own chat log and visible to nobody else. That lets DM narration and session events read
naturally in chat without the plugin ever transmitting through the game.

## Platforms

FFXIV with Dalamud runs natively on Windows, and on macOS and Linux through XIVLauncher. **All three
are supported.** Windows has the largest userbase. That is a fact about the audience, not a limit on
who the product serves.

The macOS/Linux gap was never a missing macOS feature. The failing runtime was Windows .NET running
under the wrapper, which could not create an ECDH key through the Windows key-storage backend. Linux
failed through the same path. The fix is a managed crypto implementation used on every platform
(see D-19). It has no protocol break, because an ECDH shared secret does not depend on the
implementation, and no per-platform code, because per-platform code would stop Windows and macOS
players from sessioning together.

## Who it is for

The FFXIV RP community running tabletop campaigns inside the game. Two roles, both on the same build:

- **The DM.** Hosts the session, calls for rolls, keeps turn order, tracks who is hurt, and remembers
  what happened last week. Today they do this with a second monitor: chat in one window, a
  spreadsheet or Discord message for initiative, paper or notes for HP.
- **The players.** Roll dice however they can (the game's `/dice`, a Discord bot, a physical set on the
  desk). They spend the rest of the session scrolling chat to work out whose turn it is and whether
  the roll that just went past was theirs.

What breaks for them today:

- Roll results scroll away, or happen somewhere the group cannot see. In a busy party chat a result
  is off screen by the time anyone reads it. The game cannot roll `4d6+2` at all, so anything beyond a
  single die happens outside the game.
- Initiative lives outside the game, is kept by hand, drifts from what actually happened, and only
  the DM can see it.
- Nothing lasts. When the session ends, its only record is a chat log nobody will read.
- Picking a campaign back up means rebuilding from memory where everyone was.

## Problems it solves

Most important first.

1. **Rolls stop scrolling away.** Every roll in the session is captured, attributed and kept in
   order, on every admitted client.
2. **Everyone can see whose turn it is.** Turn order is on every participant's screen, not just the
   DM's, and stays correct without anyone keeping it up by hand.
3. **Combat state is shown instead of remembered.** HP and status for the current encounter are on
   screen and update as the DM changes them.
4. **A campaign can be picked up where it stopped.** A session that ended mid-combat resumes next week
   with the same roster and the same state.
5. **The session leaves a record** on the DM's machine that outlives the chat buffer.
6. **Character detail is at hand when a roll lands**, so nobody has to ask "what's your modifier?"

## How state is shared

This constrains every feature.

- **The DM's client is authoritative** (D-3). It owns the shared picture: roster, initiative, HP, the
  encounter record. Player clients send their own events, such as a roll they made or a join request,
  and render what the host sends back. A player client never originates shared state and never
  trusts another player's client for it.
- **The transport is a relay** (D-2). It is connected only while a session is running, stores nothing,
  and can be swapped for one you host yourself. It forwards. It is not a source of truth.
- **The game is not an input at all.** The plugin **reads no chat**. Rolls are generated in the
  plugin, and session events travel over the relay. The only thing the plugin does to the game client
  is write local echo lines to show events. That is display, and nobody else sees it.
- **Local state** (your character sheets, your notes, your window layout, the DM's own campaign
  history) lives on one machine and is never assumed to exist on anyone else's.

## Identity and privacy

A deliberate, load-bearing design choice; see D-8.

- A client's identifier is generated fresh **every time the plugin launches**.
- It may bind to a persistent UUID stored **under a session code**, so a returning player can relink to
  their character in that campaign. **The DM approves every relink, every session.** Relinking is
  never silent and never automatic.
- No identifier is stable across campaigns, derivable from a character name or account, or present
  in any export.
- The DM's local history may contain real character names. Exports never may (D-20).

**Two honest limitations. The UI must state them, and nobody may paper over them:**

1. The plugin knows the display name each participant's client **sent to the session**. It harvests
   no names from chat, because it reads no chat. What it knows is what you chose to send, which
   defaults to your character name and can be changed to an alias. The copy should say this plainly
   and not apologise for a problem that no longer exists.
2. **The relay observes every session by construction.** End-to-end encryption (D-11) makes "no
   identifier links a player across two session codes" a property of the architecture rather than a
   promise about our logging. The relay carries ciphertext. What it can still observe must be stated
   plainly; session-layer R-1.9 is the single statement of that list. Encryption hides content, not
   the fact of a conversation.

## Principles

### D-1 The plugin never sends anything into the game

The plugin sends nothing into the game: no actions, no macros, no keystrokes, no input of any kind,
and **no chat**. The only thing it writes to the game client is a local echo line in that player's own
chat log, which nobody else can see. The plugin never hooks, detours or otherwise drives the game's
chat-send path, whatever triggers it.

The plugin also **does not write the player's game UI configuration**, including chat-tab names and
filters: not by default, not on first run, and not yet as an opt-in, because an opt-in for a
mechanism nobody has executed is a promise the product cannot keep. An opt-in may follow once the
mechanism has been run in the game (rolls R-2.13). The echo prints to a filterable channel. A player
who wants a dedicated tab builds it with the game's own UI.

Reason: anything that acts for the player risks their account and stops this being a tracker.
Dalamud has no supported API for sending chat to other players, so any "send chat" feature would
have to hook the game's chat-send function, which is exactly the automation this rule forbids.

### D-2 The relay is a dumb pipe, and it is the only service

The only network destinations allowed are (a) a **session relay** (the shipped default, or any relay
the user configures instead) and (b) other clients in the same session. The plugin must let a user
point it at a different relay, and the setting is in the plugin, not buried.

The relay **stores nothing**: no accounts, no session state, no campaign data, no message content at
rest, no content logging. It keeps no session identifier beyond the life of a connection and never
writes one to durable storage. It forwards traffic between session members and forgets it. It is not
authoritative for anything (D-3).

A TLS-terminating proxy in front of the relay (a CDN, a proxied DNS record, any service the client
authenticates to and hands bytes to) **counts as a destination** and is not permitted. The test is
where the client's TLS session ends, not who eventually receives the traffic.

Everything else is forbidden: telemetry, analytics, update or version checks, webhooks, Discord
integration, cloud storage, crash reporting, and any vendor service that is not a relay. A custom
Dalamud plugin repository does not breach this rule, because Dalamud fetches the manifest, not the
plugin.

The relay code lives in this project and deploys as an ordinary container, so a third party *could*
run one. That is an **escape hatch, not a guarantee, and not a tested requirement.** If the default
relay stops, the plugin stops, and that risk is accepted knowingly. A deployment artefact that cannot
start without a prerequisite names that prerequisite in its failure message. That is the artefact
working correctly, not self-hosting documentation.

Reason: peer-to-peer fails behind carrier-grade or symmetric NAT, so a real share of users could never
connect. A relay removes that whole category. Keeping the relay this narrow keeps the original
promise: a pipe that stores nothing is not a database, and campaign data still lives only on the DM's
disk.

### D-3 The DM's client is authoritative

Shared session state has exactly one author: the DM's client. Player clients send their own events
(a roll they made, a join request) and render what the host sends back. They never originate shared
state or trust another player's client for it. Every piece of shared state has two defined answers:
what the host sends, and how a client that reconnects mid-session rebuilds the current picture
instead of showing an empty window.

The host sequencing and timestamping messages is not the relay deciding anything. The host is a
participant the table already trusts to run the game.

Reason: everyone agrees because everyone derives from one authority. It also makes the DM's
accept/deny meaningful, because an unadmitted client has no route into anyone's state.

### D-4 No rules engine

The plugin contains no rules content for any tabletop system and enforces no legality of any action.
It does not compute the outcome of a roll beyond evaluating the dice expression a participant wrote.
It records what the humans decided. Success/failure resolution was raised and deferred: it is not
built yet, and it is not a non-goal.

The product is modelled on **Foundry VTT core, never a Foundry system.** Foundry core ships no game
rules; Pathfinder 2e, D&D 5e and the rest are separate packages layered on top. That split is this
rule's line. Taken from core: the four-level access model (D-13), the combat tracker's shape, the
roll modes (Public, GM, Blind; Foundry's fourth, Self, was not taken, see rolls R-2.15), and the
Assistant co-DM role. Not taken, and it is most
of what Foundry is: scenes, tokens, canvas, vision, lighting, walls, movement. FFXIV is the map.

Reason: encoding one system's rules picks a game for our users and takes on its content licensing.
Every group runs something slightly different, so the tool has to be indifferent to which.

### No defence against a participant cheating their own table

No anti-cheat, no tamper detection, no host-authoritative rolls, and no cryptography meant to stop a
player learning something they could learn by reading their own client. **A blind roll hides a number
in the UI and sends it to the DM. That is the whole mechanism.** A roller's client may compute a blind
roll, send it, and not keep it; that is tidiness, not a guarantee.

Copy may not **claim** a blind roll is secret, and it need not advertise that it can be defeated.
Say what the feature does, not what it withstands.

**This scope is narrow.** It covers only the people the table invited. D-11 still assumes a hostile
relay, and D-8 still keeps the operator out. Citing this principle to weaken session encryption is a
misreading.

Reason: manipulating rolls takes real effort, and at that point the campaign has failed, not the
plugin. Foundry has the same property.

### D-8 Identity is campaign-scoped and never portable

A client's identifier is generated fresh on every plugin launch. It may be bound to a persistent UUID
stored under a session code, so a returning player can relink to their character in that campaign. The DM
approves every relink, every session, and an approved relink is never silent or automatic. No
identifier may be stable across campaigns, derivable from a character name or account, or present in
any exported artefact. Local history on the DM's own machine may hold real character names; exports
may not.

Reason: the plugin must never become a way to identify or track a character across contexts,
including as a way around someone's blocklist. A per-campaign UUID gives a DM enough continuity to
resume mid-combat next week; a portable one would build exactly the cross-context profile the
product refuses to build.

**Display names.**

- A display name is **what the client chose to send**. It defaults to the character name and may be
  changed to an alias. That default is allowed *only because* the player can change it: any build
  that ships a display name ships the means to change it.
- **The choice is in the join flow, not only in settings.** A participant sees and can change the
  name before it is sent, pre-filled. Settings may hold a persistent default that pre-fills the field;
  they may not replace it. A control that protects against something the user does not know about
  has to be on the path they are already taking.
- **The display name is sent in the clear at join**, beside the ciphertext, because the DM needs it
  to decide and no keys have been exchanged yet. So the alias is a privacy control. The screen where
  the name is chosen says the relay operator can see it, and the published policy says so too.
- **Any script is allowed.** Restrict what can attack the display; never restrict what language a
  person speaks. The constraints that do apply are all attacks. A name must render visibly and be
  length-bounded, and it must not forge or displace surrounding text: a bidirectional override or line
  separator that pushes the fingerprint out of the DM's view is the D-11 substitution attack.
  Diacritics and combining marks are accepted.
- **Length is counted in grapheme clusters**, so the limit does not vary by script. The limit is 32,
  and the rule behind the number is that it accepts any name FFXIV itself permits, because the
  character name is the default.
- **Reserved names:** `DM`, `GM`, `Dungeon Master` and `Game Master` may not be chosen as an alias,
  matched case-insensitively after whitespace normalisation. The reservation reaches only a chosen
  alias: a player whose actual character name is one of them is accepted, because a character name
  comes from the game and imitates nobody (session-layer R-1.3j). A blocklist
  leaks (`D.M.`, homoglyphs), so reserved names cover the obvious case cheaply, and the structural
  host marker (the session role, assigned by the session and never by the sender) carries the
  guarantee: in every rendering, including the one-line echo, a member cannot produce a line that
  looks host-authored. Two *players* can still look alike; that is the DM's to catch at admission.
- **A display name may be shown and may never authenticate.** It may appear at the admission prompt
  and in the roster. It is self-declared, unverified and trivially spoofable. The **key fingerprint**
  is the only security-bearing identifier at admission. A UI that shows a name while omitting or
  de-emphasising the fingerprint is not permitted.

**The relay observes every session by construction.** Without E2E, "no identifier links a player
across two session codes" would be only a promise about our logging. Copy implying that participants
are anonymous to the plugin, or that the relay cannot correlate sessions, is false and not
permitted. Choosing an alias hides you from other participants, not from your own client.

**The participant UUID does not expire, and the player can delete it.**

- Retention is unbounded: no expiry, no timer. This rule guards against linking across campaigns, not
  against time within one, and a timer would break relink for a campaign that meets monthly.
- The player may delete their own participant UUID for a campaign **without the DM's involvement
  and without the DM being told.** Notifying the DM would create a signal tying a deletion to a
  player. The DM learns when relink is not offered.
- Before deleting, the player can **see what their client stores for each campaign**, and the UI
  says that relink will no longer be possible and they will rejoin as a new participant.

### D-11 Session traffic is end-to-end encrypted

All session payloads are encrypted between session members. The relay carries ciphertext and **never
holds a key**. Keys are **not** derived from the session code, because codes are short, speakable and
not secret. No hand-rolled protocol or primitive: use a vetted standard construction and library. The
construction is ECDH on P-256 with HKDF-SHA256. Changing the curve, the KDF or the salt breaks the
protocol.

The key exchange rides the existing join flow. The joiner presents an ephemeral public key with its
request, and the host's public key reaches the joiner **before** admission. **Both parties are shown
the same fingerprint** and have something to compare. This adds no user-facing step, because the
human decision point is already there.

No UI offers a confirmation control for something the other party cannot take part in: a checkbox
affirming a comparison only one side could make is a false control that records success.

Reason: without encryption the relay could read session content, so it could link a player across
two codes using none of our identifiers. Without E2E, D-8 protects a
user from every other participant and from nobody else.

### D-12 A testing channel is not a release

The plugin is distributed through a **custom Dalamud plugin repository for testing**, to people who
were invited. It is **not** listed, advertised, submitted to the official Dalamud plugin repository,
or described anywhere as generally available. Official listing is not being pursued, and no work is
done whose only justification is "the official repo will want this".

The repository manifest is **testing-exclusive** (`IsTestingExclusive`), so Dalamud offers the
plugin only to users who have turned on testing builds. That makes two independent gates: an
unadvertised URL, and a flag Dalamud enforces. A URL leaks, so neither is enough alone. **Moving to
the stable channel is the act that makes this a release.** Anyone given the repository URL must
also be told to turn on *Get plugin testing builds*, because without it Dalamud shows an empty
repository and no error.

The default relay is operated as a public service only with a published service policy covering
what is rate-limited, what is refused, how it is funded, and what notice is given before shutdown.

Reason: a custom repository URL is a real distribution channel, since anyone holding it can
install. A testing channel is not presented as a launch.

### D-13 Access is per-user and has four levels

Anything one participant can see and another might not (a character sheet, a combatant, a note, a
handout) carries a **per-user access level**. Each kind of shareable state defines which levels apply
to it and what each one shows.

- **None:** cannot see it and cannot tell it exists.
- **Limited:** knows it exists; sees no contents.
- **Observer:** sees the contents; changes nothing.
- **Owner:** sees and edits.

**None means absent from the payload, not hidden in the UI.** A client at None never receives the
data and cannot infer that it exists from what it does receive: no count, no gap in an ordering, no
timing. Filtering on the client passes every UI inspection and leaks to anyone reading the traffic,
so it is not permitted.

Reason: taken from Foundry VTT core's document-ownership model. Without it, sheet sharing, hidden
combatants and character sheets would each have invented a visibility rule of their own, and the
three would not fit together.

### D-14 The wire format only grows

Changes to the shared contract are **additive**. New message types and new optional fields are
allowed. **No field is ever removed, renamed, repurposed, or made required**, and no message type
ever changes meaning. A receiver **ignores message types and fields it does not recognise**, and the
deserializer does that ignoring, not each handler separately.

A change that genuinely cannot be additive is a **breaking change**. It bumps the protocol version,
and the relay then refuses mismatched clients with a message naming which side is behind (see
session-layer R-1.7b). Breaking changes are rare and deliberate. A breaking change without a version
bump is not permitted, and neither is a version bump for a change that could have been additive.

Every message type in the contract is either wired on both sides or carries a note saying what will
wire it. A contract that defines a message nothing sends claims the product can do something it
cannot.

Reason: our own builds can be kept in step, but what is installed on players' machines cannot. The
relay will be updated while old plugins are still in use. Additive-only means an old plugin keeps
working through almost every change, and version refusal becomes a rare backstop. Strict lockstep
(refusing any version difference) was rejected because it would turn every relay update into an
outage for everyone who has not updated.

### D-15 The relay stays extractable

The relay lives in the plugin repository and must stay **mechanically extractable into its own
repository at any time**. The relay project depends on the **shared contract only**. Dependencies run
plugin → contract and relay → contract, and **never relay → plugin**. Neither the contract nor the
relay references any Dalamud assembly, any plugin-facing project, or any type that exists only
because the plugin exists. Shared code goes in the contract, not in a plugin project.

A reference that resolves to a file inside a Dalamud installation breaches this rule even when the
library itself is harmless (for example, `Newtonsoft.Json` by `HintPath` into the Dalamud lib
directory). The same library from NuGet does not breach it. What matters is where the file comes
from, not which library it is.

**This is enforced by a check that can fail, not by review.** The contract and relay projects fail
their build on a Dalamud reference. Known gap: the current check matches on reference and package
identity only, so a `HintPath` pointing into a Dalamud installation passes it. The requirement is
that any reference resolving into a Dalamud installation fails the build in both projects.

`BouncyCastle.Cryptography` ships into other people's game client, so it is pinned to an exact
version and scoped to the key-exchange path; a second use of it is a second decision (D-19).

Reason: the option to split the repository is real only while the coupling stays absent, and coupling
arrives one convenient reference at a time. Keeping the option is what would make an independent relay
release cadence, or handing the relay to another operator, a decision rather than a project.

### D-16 A build describes itself; nobody types what it is

Every value that tells a user or Dalamud **what a build is** (its version, its API level, the
artefact it resolves to) has exactly **one place where a human writes it**. Every other appearance
of that value is computed from that place. A release in which two such values are written
independently, and so could disagree, **fails rather than proceeds**. On the release path, the tag and
the assembly version are not both typed.

Builds are reproducible from their tag. The plugin build pins its SDK (`global.json`), and the relay
image pins its SDK and runtime versions.

Reason: the failure is **silent**. Dalamud does not reject a build that advertises the wrong version;
it just never offers it, nothing is logged, and the symptom is a tester who goes quiet. One authored
source removes the disagreement, where a "check that they match" is only a rule someone can later
bypass.

### D-17 Session continuity: reconnect, relink, and the clocks

This principle covers how a participant comes back, and every timer involved. Identity itself is D-8.
Relink is defined there and applied here.

**The key decides, and the window bounds it.**

- **Same key pair, session live, inside the seat window:** the client resumes. This is not a relink
  and needs no fresh approval.
- **Same key but after the window, or a different key at any time:** this is a **relink**, and all of
  D-8 applies.
- A plugin that unloads (game exit, Dalamud disable/enable) cannot keep its key pair, because nothing
  saves one. So it always comes back as a relink, never a resume.
- A relink under a new key **replaces and clears** any stale seat for that player. A player is never
  locked out by their own ghost.

**Durations.** The player seat window and the host-loss grace window are both **five minutes**, and
one settable value serves both (session-layer A-1.27). Five minutes gives a DM one FFXIV relaunch,
and "you have five minutes" is easy to hold in mind mid-combat. The admission prompt lapses after
**fifteen minutes**, long enough to survive one combat. If DMs routinely miss prompts, the fix is
making the prompt more visible, not a longer deadline. A closing session gives participants **sixty
seconds**, not configurable. The host sets the value and sends it, and every participant's displayed
countdown comes from what was sent.

**A timer that infers intent from inaction runs only while the action was possible.** The seat clock
pauses while the host is unreachable. Otherwise it would be measuring the host's outage and evicting
people for it. This applies to every timer the product adds.

**A deliberate exit is not a link failure.** A player who quits (including closing FFXIV cleanly)
leaves the roster at once. A crash, power cut or force-quit cannot send anything, so it looks like a
link failure and the seat is held. Graceful exit is as much as can be achieved.

**A session always has a campaign, and hosting never asks first.**

- Hosting never blocks on a campaign choice and never refuses. Doing nothing means a new campaign.
- A campaign exists before any participant is created, with no path where it does not.
- When earlier campaigns exist, resuming one is reachable from the host flow without navigating away.
  Otherwise a DM silently starts a new campaign and loses the roster.
- An auto-created campaign is labelled with the date and then the local time it was created, rendered
  in the DM's own culture, with no weekday and no "Session of" prefix. A campaign is not a session.
  The DM can rename it. Labels need not be unique.
- Campaign identity is never keyed on the session code.
- The host flow never offers continuity the build cannot deliver. Where resuming cannot restore
  participants, the picker says so where the choice is made.

**A live host is never offered an action that restarts hosting.** A rehost mid-session would either
re-key an audience that was admitted under a different key or eject everyone, and both are the
result of a mis-click. The exclusions are host/join, join/host, and host/host.

### D-18 A shipped build claims nothing it cannot deliver

A release must not make anyone worse off, and must not claim something the build does not do. A
known limitation that is **stated** is acceptable. The same limitation **unstated** is a defect,
because the release is then offering something it silently does not do. Something unbuilt is a
limitation; something that regressed, or that the product offers and does not deliver, is a defect.
Unshipped work is disclosed, not hidden.

**Where the limitation is stated depends on what a user would otherwise wrongly believe:** in the
product at the point of the claim, when the false belief forms inside the product (an affordance
offering something the build cannot do); in the manifest description, when a user could install on
a false belief; and in release notes when nothing in the product claims the missing thing. A
disclosure has to be present in the **built artefact**; being in the source is not enough.

A disclosure discharges a limitation, not the obligation to fix it. A disclosure that outlives the
work it describes has become a description of the product.

### D-19 macOS and Linux are supported

macOS and Linux (FFXIV through XIVLauncher) are supported alongside Windows.

- **One implementation for everyone, never a per-platform branch.** A branch would stop Windows and
  macOS clients sessioning together.
- Key exchange uses the managed **BouncyCastle.Cryptography**, pinned to an exact version (2.4.0) and
  scoped to the key-exchange path. The platform's native ECDH fails under the wrapper for creation,
  import and agreement alike. A managed implementation produces a byte-identical shared secret, so
  mixed clients interoperate.
- Only key material produced by a vetted implementation is imported. **Generating EC parameters
  ourselves is hand-rolled cryptography and is forbidden** (D-11).
- An exported public key carries a **named-curve** OID, not explicit curve parameters, because .NET
  refuses the explicit form. A test enforces this.
- The larger download (about 4.8MB, up from 128KB) is accepted. For comparison, Lightless Sync is
  15.2MB. **The package is not trimmed.** Trimming can silently break crypto and reflection, and
  nothing would catch a silently broken key exchange. Trimming would need a real user signal and,
  first, a test shown to detect a deliberately broken crypto path.
- A dependency's cost to the user (download size, startup cost, new permissions) is recorded when the
  dependency is accepted.

### D-20 An export names nobody, and its labels are local to the one file

An exported file carries no participant name and no participant identifier, in any field, however
scoped (D-8). **Authorship inside an export is carried by a label local to that one file.** The label
is assigned when the file is written, from the file's own contents, and derived from no stored or
transmitted value. The file describes it as meaningless outside itself. **The plugin never writes a
name into an export and offers no way for anyone else to put one there.**

- **An identifier is a value that can be joined:** held by two artefacts and used to say "the same
  person". System-assigned values (participant ids, peer codes) are identifiers. A label computed at
  write time exists only inside its file and is not one.
- **No legend, ever.** No field for names, and no offer to fill one in: not at export time, not
  afterwards, not as a setting.
- **The file says its labels mean nothing outside it.** It does not say the files cannot be related:
  two exports of one session can be aligned on host time, so the stronger sentence would be false.
- The DM's retained log is not an export and may hold names (D-8).
- An export format that gains a field persisting across *different* sessions (a campaign label, a
  stable code) makes the labels joinable in combination. Adding any field to an export format means
  re-checking it against this rule.

Reason: the display name defaults to the character name, so an export carrying display names would
carry FFXIV character names tied to a table, a campaign and a date. That is the portable
cross-context artefact D-8 refuses.

### D-21 A version rollback never silently destroys what the user wrote

Any file the plugin saves that holds **user-written or user-chosen state** must survive being read and
rewritten by a build that does not know all its fields. Unrecognised keys are **kept across a
read-modify-write cycle**, not dropped. This covers the settings file as well as campaign documents,
character sheets and session logs. A file does not have to travel between machines to meet a build
that does not understand it; **it only has to outlive one.** A key may be dropped only where another
principle requires it: identity is not portable (D-8), and key material is never saved (D-11).

Reason: testing builds (D-12) make version movement on one machine routine, backwards as well as
forwards. An older build rewriting a newer file drops what it did not recognise, with no error and
nothing the user could have done. Nobody reports a setting that quietly reverted; they re-enter it.
This is narrower than D-14, which governs the wire contract between two parties.

### D-22 A refusal is recorded, never attributed, never rendered

When a client or the relay refuses input at a boundary (a frame over the length bound, a malformed
or undecryptable message, a document it will not load), it **records locally, in a bounded form, that
the refusal happened.** That record:

- **(a)** is **never rendered** in the message stream, the roster, or any session surface;
- **(b)** **never names or identifies the participant the input came from**, in anything a person can
  read;
- **(c)** **cannot be grown by whoever caused it**: it stays bounded on screen, in logs and on disk
  however many refusals arrive.

The refused content itself is never displayed, stored, or echoed, in whole or in part. The test: a
record of **the user's own client's behaviour** is a diagnostic and is required; a record that
**accuses another participant** is moderation and is not permitted.

Reason: under D-14, version skew is expected, so the likeliest cause of a refused frame is
compatibility, not hostility. With no record, "I never see Bob's messages" cannot be answered on any
machine. Attribution, though, is the first brick of the moderation infrastructure the non-goals
refuse. A peer flooding refused frames is a matter for the relay operator's rate-limiting.

## Non-goals and out of scope

What the plugin will not do, and why.

- **No automation of play.** It never issues game actions, macros, or input of any kind, chat
  included. Local echo is display, not sending (D-1).
- **No network beyond the relay and session peers.** No cloud storage, telemetry, analytics, update
  or version check, crash reporting, Discord or webhook integration (D-2).
- **No relay that remembers.** A relay accumulating accounts, session history, campaign data or
  content logs is a different product, and it would make our privacy claims false (D-2).
- **No cross-campaign identity, ever.** No account system, no friends list, no reputation, no portable
  player ID. No identifier that is derivable from a character or account, or present in an export
  (D-8).
- **Not a rules engine.** No system-specific rules content, no computing outcomes, no enforcing
  legality (D-4).
- **Not a replacement for FFXIV's combat or party UI.** It is for RP sessions, not duty content. It
  does not read combat log, target, or party HP data from the game.
- **Not a chat logger, by construction.** The plugin reads game chat in no form and for no purpose,
  and subscribes to no chat-read event. A feature that seems to need one is a product decision, not
  an implementation detail (see rolls A-2.7).
- **No moderation infrastructure in the product.** No blocklists, bans, reporting or reputation. The
  DM's accept/deny is the entire trust model for a session. Running a relay does make us an
  operator: the operator may rate-limit and refuse service to a source, under a written policy. That
  is operations, not product moderation, and it does not bring social moderation into scope.
- **No monetisation surface in the plugin, and no accounts.** No donation prompts, support buttons,
  sponsor credits, paid tiers, nag screens, or links whose purpose is to ask for money. The relay is
  funded by optional community support that lives entirely outside the plugin. The relay's costs
  never stop, so this promise erodes one reasonable-sounding button at a time unless it is
  defended. Nothing guarantees the product outlives its funding. The service policy's
  shutdown-notice clause is the protection users have.
- **No third-party identity or authentication, including XIVAuth.** A verified Lodestone identity is
  exactly the durable, cross-context, character-derived identifier D-8 refuses. Impersonation is
  handled socially: the DM admits people they arranged with, using a code they gave out.
- **No user accounts, no cloud storage.**

## Cross-cutting acceptance criteria

Observable statements. Area specs hold the numbered criteria; these are the product-level ones.

### Session layer

- A DM can start a session and is shown a session code they can read aloud or paste to someone.
- The session code can be copied to the clipboard in a single action.
- Two players on different networks can join the same session without either of them configuring
  anything on their router. (Behind carrier-grade NAT this is inferred from the architecture and not
  yet observed.)
- A user can point the plugin at a relay they run themselves, and a session works through it.
- A player who enters the code causes the DM to see a prompt identifying the requester by their
  **chosen display name** alongside the key fingerprint, with accept and deny.
- **A participant can see and change the name they will send, before it is sent,** pre-filled with
  their character name, **in the join flow.** A settings page alone does not satisfy this.
- **The display name is never what the DM authenticates on.** The prompt must not present the name in
  a way that pushes the fingerprint aside. A prompt showing a name and hiding the fingerprint fails.
- A hosting client offers no way to join a session, a joined client offers no way to host one, and a
  live host is offered no way to restart hosting.
- The DM can see the display names of everyone currently admitted. A player who has joined can see
  the DM's display name and those of the other participants.
- A player can leave a session they have joined, and the DM's roster shows the departure.
- **The two roles are treated differently on disconnect, on purpose.** When the DM's client quits,
  every participant sees that the session is closing and how long remains. When a player's client
  quits, that player leaves the roster immediately.
- On accept, the player's client shows it is in the session. On deny, it shows it was refused, and it
  receives no session state at all.
- When the relay is unreachable, the plugin says so promptly, and it distinguishes relay-down from a
  broken connection from an inactive code. Silence or a hung spinner fails this. **This is tested
  against the network condition, never against the plugin's own classification.** Three conditions are
  produced (a refused port, a *dropped* port, and a live relay asked for a code it never claimed), and
  each must produce its own correct message.
- After a session runs, the relay holds no session state, no campaign data and no message content.
- When the DM ends the session or closes the plugin, their client disconnects from the relay, and
  every player client shows the session has ended rather than still appearing connected.
- A returning player in a known campaign is offered relink, and the DM must approve it before any
  state flows. Relink never happens silently.
- **No export contains a participant identifier or a participant name at all**, not even for a single
  session (D-8, D-20). No file the plugin writes contains an identifier that links a player across
  two different session codes.
- The DM can see every campaign their machine is storing and can delete one outright, after which no
  trace of its participants, UUIDs or state remains on disk.
- A player can see what their own client stores for each campaign and can delete their own
  participant UUID for one, without the DM's involvement and without the DM being told. Before
  deleting, the UI says relink will no longer be possible.

### Rolls and the roll log

- A participant writes a roll in `XdY+Z` notation, and an entry appears in the roll log showing who
  rolled, the individual dice, the total, and any label they attached. **The total is checked against
  something that is not the roller:** the displayed dice plus the modifier sum to the displayed total,
  asserted independently of the code that produced either, and there are exactly `X` dice, each in
  `1..Y`. Showing the individual dice is what makes a roll auditable at all.
- The roll appears on the DM's client and on every admitted player's client.
- The log is ordered by the host's receipt order, so two clients never disagree about what happened
  first.
- A participant who reconnects mid-session receives the rolls that happened while they held their
  seat. (A late joiner receives none: log history is never sent; see Session panel.)
- A denied or unadmitted client's rolls never appear in anyone's log.
- No copy anywhere describes rolls as verified, fair or tamper-proof. They are generated locally, and
  the product is honest about that.

### Initiative order, shared

- The DM can start an encounter, and the initiative list appears on every admitted client.
- A posted initiative roll takes its place in the order on every client without the DM editing.
- Advancing the turn moves the highlight on every client.
- A player who disconnects and rejoins mid-encounter rebuilds the current order rather than showing
  an empty list.

### HP and status tracking

- The DM changes a combatant's HP, and every client's display of that combatant updates to match.
- A combatant at zero is visibly distinguished from one merely hurt. **Both directions fail
  separately:** a combatant at zero carries the mark, **and a combatant merely hurt does not.**
- Statuses the DM applies are visible on every client with the duration the DM stated.

### Session log and resume

- After an encounter, a participant can open a record showing rolls, turn order and HP changes in the
  order they happened.
- The DM's record survives a game restart.
- A session that ended mid-combat can be resumed next week with the same roster and the same
  initiative and HP state, after each returning player is re-approved.
- That state survives a crash of the DM's client, not only a clean end: it saves itself as it changes,
  and the DM is never asked to save (session-layer R-1.6a).
- A participant can export the record to a file they can open outside the game. **It names nobody:**
  no participant name and no participant identifier, however scoped. Entries are attributed by a
  label local to that one file, and the file says the label means nothing outside it (D-20).

### Character sheets

- A player can create a sheet and see it on their own client.
- When that player rolls, their sheet is one click away from the roll entry.
- A player's sheet is visible to another participant only at the access level its owner granted
  (D-13): None by default, Observer if shared. A client at None never receives it at all.

## Session panel

The roll-and-message panel. Base chat comes first, then `/roll` on top of it.

**The panel is our own window, and it is primary.** Inline clickable rolls cannot exist inside FFXIV's
chat, which renders only text, so the panel cannot live there. Game chat is an echo of the panel.

1. **Targeting: everyone, plus private to or from the DM.** There is one shared room; a player may
   send privately to the DM, and the DM to a player. **Player-to-player privacy is deliberately not
   built.** FFXIV already has `/tell`, party chat and linkshells. What only this panel offers is
   private talk that is part of the session record and can carry a roll.
2. **The stream carries messages, rolls and membership events** (joined, left, lost connection,
   reconnected), timed and in order. The roster shows who is here now; the stream shows when that
   changed. **Admission mechanics stay out**: code registration, denials and fingerprint state are DM
   machinery and would bury the conversation.
3. **Three message kinds: in-character, out-of-character, emote.** One field plus `/ooc` and `/me`.
   Without them, nobody can tell whether a character or its player said "I don't think that's a good
   idea".
4. **Retention.** The DM's client keeps the log automatically, on the DM's machine, and it can be
   deleted from the plugin's settings, whose copy says there is nothing to delete anywhere else. A
   player's log lasts only as long as the session unless they **export** it. Export is the same
   deliberate act for both, and it is offered at session end rather than buried in a menu.
   The asymmetry is deliberate: the DM needs continuity between sessions and already holds the
   campaign; a player usually does not.
5. **`/roll` implements the full Foundry dice grammar:** pools, success counting, comparisons,
   keep/drop, exploding, rerolls, labelled terms, nested rolls. **Evaluation is bounded**, because a
   formula from the wire is untrusted input: `999999d999999` from a peer must not hang or exhaust
   memory. Dice count, sides, nesting depth and total work are bounded, and evaluation fails with a
   message rather than freezing. This is a product safety constraint.
6. **Rolls can be embedded both ways.** `[[2d6]]` resolves inline when the message is sent.
   `[[/roll 1d20]]` renders a button others press, and each person's result is attached to that
   message. So a message can carry a roll that has not happened yet.
7. **Messages sent while a member was briefly away are delivered on reconnect, and gaps that were not
   held are marked,** within the seat window (D-17). This is not history sent to a newcomer: a member
   holding a seat was there, and the seat is the entitlement. It adds no new exposure, because every
   message is public or player-to-DM, so the host is a legitimate party to everything it queues.
   Player-to-player privacy would break this; the two decisions depend on each other.
8. **The DM may delete any message, and a marker stays where it was,** so the log never silently
   disagrees with what people watched happen.
9. **Clickable rolls are open by default, and the DM may address one to named players.**
   Non-addressees see the button and who it is for, greyed out. Targeting is a protocol field.
10. **The echo is on by default, for rolls and membership events only.** Narration and conversation
    stay in the panel unless switched on. The echo prints to a filterable channel; the plugin never
    rewrites the player's chat tabs (D-1). **The echo is never the only place a roll appears.** The
    panel is the source of truth, and the echo is a view.
11. **Private content never echoes its contents to game chat**, only a notice that it arrived ("a
    private message from the DM", "your blind roll was sent"). FFXIV's chat log is outside our
    control: the game may keep it and other plugins can read it.
12. **Anyone may set a speaker label, and the real sender is always shown.** The format is
    `Character Name (Player Name)`, for example `Eli (Tuka)`. Someone speaking as themselves with no
    character renders alone (`Tuka`, never `Tuka (Tuka)`). The echo carries the same form, for
    example `[DM] Eli (Tuka): Hey`. **Parentheses mean exactly one thing across the product: the
    person behind the speaker.** Roles (Host, Assistant, Player) are therefore rendered some other way
    (a badge, prefix or column), including in the roster. The host is rendered distinctly by session
    role, so a member cannot produce a line that looks host-authored (D-8). Assistant is not Host.
    The parenthetical is never dropped from a rendered surface: not in a compact view, not in a
    narrow window. An export carries no names at all (D-20).
13. **The host sequences and timestamps every message.** One order and one clock give everyone the
    same log, so two exports of a session agree on order and time, and a "message removed" marker
    lands in the same place for every reader. If the DM is unreachable, conversation stops; that is
    true of the whole architecture already.

**Log history is never sent.** A client's log is its own and holds only what it was sent live:
nothing goes to a late joiner, a relinking client, or anyone who asks. A reconnecting client keeps
its log because it never stopped running (and receives what decision 7 owes it). A relink is a new
admission on a new key and starts empty. **An export contains only what that person could see:** a
player's export never contains the DM's private rolls, and no export rebuilds a view its owner never
had.

## Default relay

**`wss://relay.ruminabottle.com/session`** is the relay the plugin ships pointing at (D-2). It is a
**default, not a dependency**: every user can point elsewhere, and the setting is in the plugin.
Anything a shipped build points at is a product fact and is recorded here.

## Repository layout

The plugin, the relay and the shared contract live in **one repository**. The relay stays
mechanically extractable (D-15): it depends on the shared contract only, and a check enforces that.
The option to split later is kept on purpose. The former `dungeonmasterxiv-protocol` and
`dungeonmasterxiv-relay` repositories are empty and vestigial; nothing points at them.

**The relay is deployed only from tagged commits.** In one repository, the protocol coupling between
relay and plugin is implicit: a relay could be built from a commit whose contract is ahead of the
released plugin. Tagging makes "which protocol is this relay speaking?" answerable. The version check
(see session-layer R-1.7b) catches the failure; tagging prevents it.

## Open questions

- Open question: other game systems use other words for the host (Keeper, Storyteller, Referee, MC,
  Judge). Only `DM`, `GM`, `Dungeon Master` and `Game Master` are reserved (D-8). Whether to extend
  the list is undecided; it blocks nothing, because the host marker carries the guarantee.
- Open question: the 32-grapheme display-name limit assumes FFXIV allows at most 31 characters
  (15 + space + 15). That has not been checked against the game. If the real maximum is higher, the
  limit goes up (D-8).

## Retired IDs

- D-5 (Tier order is not negotiable): team process rule.
- D-6 (Nothing with logic merges unbuilt): team process rule.
- D-7 (In-game criteria need a human): team process rule. The product half (criteria that need
  something outside the thing under test, such as a produced network condition or an independent
  recomputation) is kept in the criteria themselves.
- D-9 (Hold: dispatch no new work): team process rule. It was removed on the day it was added, and
  its number was never reused.
- D-10 (Structure is reviewed before code is layered on it): team process rule.
