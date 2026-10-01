# Relay service policy

Written in plain language on purpose. The claims here about **what the software does** are checkable
in published source, which is a rarer position than most privacy policies occupy. Some of what
follows is a **promise about how the service is run** instead — those are marked where they appear
rather than blended in with the rest, because blending the two is how a policy comes to be trusted
instead of checked. A document written to limit liability would undersell what is actually true.

---

## What the relay is

Dungeon Master XIV sessions pass through a **relay**: a program that forwards messages between the
people in a session. It is not a server that runs your game. It holds nothing, decides nothing, and
is not asked what the state of your session is — your DM's client is the authority for that.

The default relay is operated by **Rum in a Bottle**, in Atlanta, Georgia, United States.
The server itself is hosted in Germany.

## What it stores

**Nothing.**

Not your session, not your campaign, not your character names, not your messages, not an account —
there are no accounts. When your session ends there is nothing left on the relay to delete, because
nothing was written.

You can check this rather than take it on trust: the relay's source is public, and it writes only to
standard output. It has no file of its own.

## What it cannot read

Session traffic is **end-to-end encrypted between the people in the session**. The relay carries
sealed messages and holds no key. It forwards your DM's bytes onward unchanged and cannot open them.

**That includes the name you join with.** [changed with protocol version 2] Your client and your DM's
exchange keys before you ask to join, so your name, and anything that would let your DM recognise you
as a returning player, travel sealed.

**One honest limit.** Nobody compares keys. A relay that only forwards traffic, a log that leaks, or a
server that is seized reveals nothing you say. A relay operator who set out to swap keys during a join
could read that session. We think that risk is small for a tabletop relay, and we would rather say it
plainly than ask every group to read codes aloud. Earlier versions had you compare a short code; this
one does not.

## What it can see

A relay that forwards your messages inherently sees **that you are connected**: your network address,
when you connected, how much traffic and how often, and which session code you are on.

**"Not stored" is not "not observed."** Those are different claims and we are making the first one.
An operator who wanted to watch traffic in real time could see the shape of your session — when you
are playing, roughly how busy it is, and who else is connected at the same time.

**The relay never records a network address. Ever — not even hashed.** This has been verified
against the source: the relay does not read your address anywhere, for any purpose. The only address
in the whole project is the server's own, used to report which port it is listening on.

Its logs do contain **session codes**, because an operator who cannot tell which session an error
belongs to cannot fix anything. A session code identifies a *session*, not a person — it stops
existing when the session does, and two people sharing one were in a game together and already knew
it. Logs never contain message contents; that is structurally impossible, since the relay holds no
key.

**Retention: whatever is kept is discarded within seven days.**

Two of those three are facts about the code and you can check them. **The seven days is not** — the
relay writes only to standard output and has no file of its own, which is what makes "stores
nothing" checkable in the source. Retention is therefore a property of how the service is run,
not of what the software does. It is a promise rather than a fact about the
code, and this page marks that difference wherever it matters.

## What we will do to keep it running

- We may **rate-limit** connections, and **refuse service** to a source that is abusing it.
- We do not read, moderate or police what happens inside a session. We cannot — see above. Who is in
  your game is your DM's decision, made through the accept/deny prompt or by letting that campaign's
  returning players straight in, and it is the entire trust model.
- We collect **no telemetry, no analytics, and no usage measurement**, anywhere in the plugin or the
  relay. We do not know how many people use this.

## If it goes away

This relay is funded by optional community support and run by a person, not a company. It may
eventually stop.

**If it is going to shut down, we will say so at least 30 days in advance**, in the repository and
through an in-plugin notice, so groups have time to move. That is a promise about how the service is
run, not something the code enforces.

**If this relay stops, expect the plugin to stop working with it.** That is the honest position and it
follows from a deliberate choice: making the plugin outlive this service would mean testing that it
can, and we do not test it.

There is an escape hatch, and it is real but unproven. The plugin lets you point at a **different
relay** — the setting is in the plugin, not buried — and the relay's source is public, so someone
technical could run one. We have never tested that path and do not promise it works. It is worth
knowing about; it is not a reason to assume this service ending is survivable.

## What is stored on your own machine

Your campaign history — participants, saved encounters, the names your DM uses for people — lives on
**the DM's computer** and nowhere else. It is never uploaded. Your DM can list every campaign their
machine holds and delete any of them.

Nothing exported from the plugin contains an identifier that links a person across two different
campaigns. That is deliberate: this plugin is not a way to find out where someone else plays.

If your DM lets returning players straight in, the participant id your client keeps for that
campaign is what lets you back in without being asked. Anyone who copied that file from your
computer could join that campaign as you while the setting is on. Forgetting the id in settings, or
your DM switching the setting off, ends that.

## Changes

If this policy changes in a way that reduces what is promised, that is announced in the repository
before it takes effect, not after.
