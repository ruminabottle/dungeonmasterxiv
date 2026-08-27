# Relay service policy

Written in plain language on purpose. The claims here about **what the software does** are checkable
in published source and provable from the test suite, which is a rarer position than most privacy
policies occupy. Some of what follows is a **promise about how the service is run** instead — those
are marked where they appear rather than blended in with the rest, because blending the two is how a
policy comes to be trusted instead of checked. A document written to limit liability would undersell
what is actually true.

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

This is not a promise you have to take on faith. The relay's source is public, and there is an
automated test that runs the relay, puts a full session through it, and **watches the disk while it
happens** — including the ordinary places a program writes by default, which is exactly where an
earlier version of this test was not looking. It fails if the relay writes a file.

The test is checked by deliberately breaking the relay: a version that writes one line per
connection makes the test fail, by name. That is how we know it is looking rather than merely
passing.

## What it cannot read

Session traffic is **end-to-end encrypted between the people in the session**. The relay carries
sealed messages and holds no key. It forwards your DM's bytes onward unchanged and cannot open them.

**One honest limit, which matters.** Encryption protects you from someone substituting a key only if
the humans involved can check the keys. Today that check is **one-directional, and you should know
which direction.**

**The DM is shown a short code** when someone asks to join, and can confirm it against what that
person reads out. That protects the DM against a substituted joiner.

**If you are joining, you are shown nothing to compare.** Your client does not receive the DM's key
until the moment you are admitted, so there is currently no step at which you could verify you are
talking to the DM you think you are. **A substituted host key is not something you can presently
detect**, and no amount of "end-to-end encrypted" changes that. We consider this a defect in the
product rather than an acceptable design, and it is recorded as one; this page will say so until it
is fixed rather than describing a check that does not exist.

## What it can see

A relay that forwards your messages inherently sees **that you are connected**: your network address,
when you connected, how much traffic and how often, and which session code you are on. It cannot see
what any of it says.

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
relay writes only to standard output and has no file of its own, which is exactly what makes the
"stores nothing" test above possible. Retention is therefore a property of how the service is run,
not of what the software does. It is a promise rather than a fact about the
code, and this page marks that difference wherever it matters.

## What we will do to keep it running

- We may **rate-limit** connections, and **refuse service** to a source that is abusing it.
- We do not read, moderate or police what happens inside a session. We cannot — see above. Who is in
  your game is your DM's decision, made through the accept/deny prompt, and it is the entire trust
  model.
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

## Changes

If this policy changes in a way that reduces what is promised, that is announced in the repository
before it takes effect, not after.
