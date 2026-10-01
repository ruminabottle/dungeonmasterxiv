# Connection: fewer steps, same encryption

Date: 2026-10-01. Background: `research/reports/Game session security norms.md`.

## Why

The research compared this product with virtual tabletops, game networking libraries and networked
Dalamud plugins. The controls nobody sees (end-to-end encryption, a relay that stores nothing, no
identifier that crosses campaigns, DM approval of each joiner) are above the norm and cost players
nothing, so they stay. The visible ceremony is what goes:

- **The read-aloud fingerprint comparison.** No comparable product requires it, and studies of
  messenger verification find most people skip it. It defends against one threat: a relay operator
  actively swapping keys. For a hobby RP relay that the project or the DM runs, that threat is low.
- **In-app security text.** The norm is a policy page linked from the app, which is also what GDPR
  transparency guidance accepts. Explanation moves out of the plugin.

The goal is a join that is easier for players, at the cost of one stated risk.

## Decisions

### 1. Keys first, then an encrypted join request

1. The player enters the code. Their client connects and sends a **hello** carrying a fresh public
   key.
2. The host answers with its public key.
3. Both derive the session key, with the D-11 construction unchanged (ECDH P-256, HKDF-SHA256).
4. The joiner sends its **join request encrypted**: the display name, and the stored participant ID
   if it has one for this code.
5. The DM's prompt shows the name, with Admit and Deny. There is no fingerprint and no confirmation
   control.

- The relay then sees a connection, its network address, timing and size, and the session code. **It
  no longer sees any name or participant ID.**
- The key exchange is not session traffic. An unadmitted client still receives none (R-1.3b).
- Two concurrent requests with the same name are told apart by a short per-request label on the
  DM's screen.
- **Accepted risk:** with no comparison, a relay that actively substitutes keys can read a session.
  Passive reading, a leaked log and a seized server cannot. A "verify" option can be added later if
  anyone asks for it.

### 2. Returning players: a per-campaign DM setting

The setting is **Returning players: Ask me each time** (the default) **or Let them straight in**. It
sits on the host's session window for the current campaign.

- A returning joiner presents a participant ID the campaign store holds for this campaign, for an
  entry not already seated.
- **Ask me each time:** the prompt names the entry they claim. The DM admits them as that entry, as
  new, or as another entry (R-1.5e), or denies them.
- **Let them straight in:** the joiner is admitted as their entry with no prompt and appears as an
  ordinary join. New people are still prompted.
- With the setting on, the participant ID is a credential: random, only ever sent encrypted, only
  ever told to its owner. **Accepted risk:** anyone who copies a player's stored file can get in as
  that player while the setting is on.
- Resume (same key within the seat window) is unchanged, and so is the Assistant's lack of admission
  rights.

### 3. No security text in the plugin

- R-1.7a's quoted strings are removed (session window, admission prompt, the settings paragraph),
  along with every requirement that the UI explain security, and the campaign picker's
  not-yet-built notice.
- **Kept: say nothing rather than something false.** The forbidden phrasings stay, with one added:
  no copy claims a session is verified or protected against interception.
- Settings carries one link to the published relay and privacy page, and the plugin listing links it
  too. That page is `RELAY-SERVICE-POLICY.md` until a security spec writes the FAQ.
- R-1.9 stays the single source for what the relay can see, minus the display name.

### 4. Storage in settings, two sections

- **Campaign storage** (the DM's): list campaigns and delete one.
- **User storage** (the player's): list stored participant IDs, one row per stored entry showing its
  session code and when it was stored, and delete one.
- Neither is reachable from the session panel, the join flow, the roster, the host flow or the
  campaign picker. Both keep a deliberate confirmation that states the consequence.

## Spec changes

- **session-layer:**
  - R-1.3, R-1.3e and R-1.3j lose the fingerprint.
  - R-1.3a through R-1.3a-iv are retired, and R-1.3d is replaced by R-1.3d-1 (telling requests
    apart).
  - The key-first join is R-1.3m.
  - R-1.5, R-1.5a, R-1.5c and R-1.5d change for the returning-players setting, and that setting is
    R-1.5f.
  - R-1.5b and R-1.6 gain the settings placement.
  - R-1.7 and R-1.7a are rewritten as "no false claims".
  - R-1.9 drops the display name and its UI obligation.
- **product-overview:** D-8, D-11 and the acceptance list.
- **rolls:** references to R-1.7a and the fingerprint.
- **distribution:** the relay policy is R-1.9's one public statement.

## Rollout

- **This is a breaking wire change:** the join request is encrypted and `JoinerHoldsFingerprint` is
  removed. Under D-14 the protocol version is bumped, and older plugins are refused with an "update"
  message (R-1.7b). The release notes say every player needs the new version.
- **Code plan, after this spec:**
  1. The protocol change, and removing the fingerprint (`KeyFingerprint`,
     `AdmissionVerification`, `ComparabilityEvidence`, `JoinComparisonView`, the prompt
     confirmation, the relay's fingerprint route). `RELAY-SERVICE-POLICY.md` changes in the same
     release, because until then the name does travel unencrypted.
  2. The returning-players setting.
  3. The settings layout, the link, and removing the copy.
- **Tests:** smoke tests only.

## Deferred

- A security spec and its online FAQ.
- An optional "verify" comparison.
