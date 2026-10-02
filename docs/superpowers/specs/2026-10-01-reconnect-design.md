# Reconnect: a dropped connection comes back on its own

Date: 2026-10-01.

## Why

A session can die three ways:

- the DM's or a player's internet connection drops or flaps;
- the FFXIV client crashes;
- the PC crashes.

Nothing can save a session through a crash. The relaunched client has new keys, and the campaign survives through autosave (session-layer R-1.6a).

A network drop is the case to fix, and today it is fatal:

- the relay ends the session the moment the DM's connection closes;
- neither client redials;
- a player's resume (R-1.5a) is not built.

A two-second blip therefore ends the session after the grace window.

The goal: a blip is invisible, a longer drop recovers by itself within the grace window, and only a crash or a drop longer than the window ends the session.

## Decisions

### 1. The relay holds the session while the DM is away

- **Registration.** When hosting starts, the DM's client makes a random reclaim secret, holds it only in memory, and sends its hash with the code request. A crash loses the secret, so a relaunched client cannot reclaim.
- **The DM drops.** The relay does not end the session. It marks the host absent and holds the session for the grace window: a relay setting, defaulting to the plugin's 5 minutes.
  - Players get a new `HostAway` notice.
  - Pending join requests are dropped, with a message telling the joiner to ask again.
  - Player payloads are not forwarded while the DM is away.
- **The DM returns.** The DM's client sends `Reclaim` with the code and the secret. If the hash matches, the relay attaches the new connection as host, answers `Reclaimed`, and sends players `HostBack`. A wrong secret is refused like a code already in use.
- **The hold expires.** The relay ends the session as it does today.
- **Wire format.** These messages and the hash field are additive (product-overview D-14). Protocol version 2 is unreleased, so there is no further bump.
- **Storage.** The relay still writes nothing. It keeps a hash and a timer in memory, for at most the grace window after the host's connection closes. product-overview D-2 changes to allow exactly that.

### 2. Both clients redial on their own

- **Redial.** After a drop, a client redials with growing waits (about 1, 2, 4 and 8 seconds, then every 15 seconds) for as long as its window runs: the grace window for the DM, the seat window for a player. Both are 5 minutes. Keys are kept.
- **The DM reconnects.** The DM's client sends `Reclaim`. On `Reclaimed`, the grace clock stops and the client re-sends the roster and current state to every player, so they resync from the host. Seat clocks stay paused while the host is away.
- **A player reconnects.** The player's client sends `Resume`: its public key, plus a payload sealed with its shared key that carries the last stream line it received. Opening the payload is the proof of possession R-1.5a asks for.
  - The relay forwards it to the DM's client.
  - If that member is admitted, recorded as dropped, and the payload opens, the DM's client admits them silently with the existing acceptance and sends the catch-up.
  - Otherwise the resume is refused, the player is told their seat expired, and the ordinary join applies.
- **Flapping.** Each drop restarts the backoff but not the window. The window measures the whole outage.

### 3. What people see, and held messages

- **The first 10 seconds of any drop show nothing.** That covers the DM's own link, a player's own link, and a player receiving `HostAway`.
- **After 10 seconds:**
  - **The DM sees:** "Reconnecting to the relay… the session ends in m:ss if it doesn't come back."
  - **Players see**, while the DM is away: "The DM is reconnecting… the session ends in m:ss if they don't come back."
  - **A player whose own link is down sees:** "Reconnecting… your seat is held for m:ss."
- **On return the line disappears.** There is no "reconnected" notice.
- **Messages sent while away:**
  - During the first 10 seconds, anything sent waits on the sender's machine and goes out in order once the path is back.
  - After that, the message box is disabled. Anything already waiting still goes out.
  - If the window runs out, waiting messages are dropped, and the end message says "N messages you sent were not delivered."
- **The session ends.** It ends as today, and the end message adds that the DM can resume the campaign. This is plain how-it-works text, not a warning.

### 4. Catch-up for a resuming player

- **What is re-sent.** After admitting a resumed player, the DM's client re-sends every stream line after the one the player last received. The lines are sealed to that player only, in order, and include only lines that player was entitled to at the time.
- **Gaps.** A line the DM's client no longer holds is marked as a gap in the player's stream, never silently skipped. The DM's client keeps the session's stream in memory for the whole session, so a gap should be rare. The bound is engineering's.
- **When the DM drops.** Nothing happens without the DM, so there is nothing to catch up.

## Spec changes

- **session-layer:**
  - R-1.4 changes to the relay's hold, the DM's reclaim, and `HostAway`/`HostBack`. The grace and seat window question is answered: 5 minutes measures the whole outage.
  - New R-1.4a: the silent window, the reconnecting lines and the held messages.
  - R-1.5a: resume is built on the sealed `Resume` proof, with a failed resume falling back to the ordinary join.
- **rolls R-2.10:** the catch-up replaces "not built".
- **product-overview D-2:** the relay may hold a held session's hash and timer in memory for at most the grace window.

## Testing

Smoke tests only:

1. **The DM's connection blips.** The in-memory relay test harness gains a step that drops a connection. A player's message sent during the blip arrives afterwards, with no re-join.
2. **A player drops, misses two lines and resumes.** They receive both, with no prompt.

## Deferred

- **Surviving a crash.** It would need keys kept across launches, which product-overview D-8 refuses.
