# Client/server patterns from Lightless Sync

Product Owner research, 2026-08-26, at the human's direction. Source:
<https://git.lightless-sync.org> — a Gitea instance hosting an FFXIV Dalamud plugin with a real
client/server split. **MIT licensed**, `Copyright (c) 2022 Mare Synchronos` — Lightless Sync is a
fork of Mare Synchronos, so this is a lineage with years of production use behind it.

**Licence position:** MIT permits use, modification and redistribution provided the copyright notice
and licence text travel with any substantial portion we copy. If we reuse code rather than ideas,
attribution is mandatory and is engineering's to get right. Copying *patterns* carries no obligation;
copying *files* does.

This is the closest prior art we will find: same platform, same language, same problem shape —
a Dalamud plugin that has to talk to a server about who is in a group and what changed.

---

## The repository layout, which is itself the first finding

| Repo | Role |
| --- | --- |
| `LightlessAPI` | **The shared contract.** DTOs, enums, route constants, hub interfaces. Nothing else. |
| `LightlessClient` | The Dalamud plugin. Consumes `LightlessAPI` **as a git submodule**. |
| `LightlessServer` | The server. Consumes the same `LightlessAPI` **as a git submodule**. |

One contract project, vendored into both sides, so client and server cannot disagree about the wire
format without failing to compile.

**Why this matters to us specifically:** D-3 requires a PR introducing shared state to say what the
host sends and how a client rebuilds. A shared contract project turns that from a description
someone writes in a PR body into something the compiler enforces. That is a stronger version of a
directive we already have.

---

## Patterns worth taking

### 1. A shared contract project, consumed by both sides

Strongly recommended. It is the structural fix for the class of bug where the relay and the plugin
drift apart, and it makes D-3's requirement mechanical.

### 2. Strongly-typed bidirectional hub interfaces

Two interfaces, not one:

- `ILightlessHub` — what the **client calls on the server**
- `ILightlessHubClient` — what the **server calls on the client**

Both live in the shared contract. Adding a server→client event without the client implementing it
is a compile error rather than a silent no-op at runtime.

They use **SignalR** for this. Whether we use SignalR is an engineering decision and belongs to the
Deployment Manager and the Engineering Lead — I am endorsing the *shape*, not mandating the library.
What I will say is that hand-rolling a bidirectional wire protocol in C1 when a proven one exists on
the same platform, in the same language, in a plugin of this exact kind, deserves an explicit
justification rather than being the default.

### 3. Everything on the wire is a named DTO

No loose parameters. `GroupPairJoined(GroupPairFullInfoDto)`, not `GroupPairJoined(string, string,
int)`. Adding a field does not silently reorder an argument list, and a wire message is greppable by
type name.

They also use a **generic envelope** — `PairInboundDto<PairVisualDeltaDto>` — wrapping a payload with
its routing information. That is precisely what C1 is building as its "wire envelope", and it is
worth seeing that someone else arrived at the same shape.

### 4. Event naming: `On<Subject><Verb>`

`OnGroupPairJoined`, `OnGroupPairLeft`, `OnUserSendOnline`, `OnUserSendOffline`,
`OnGroupChangePermissions`. Consistent enough that you can guess the name of an event you have not
read. Cheap to adopt, expensive to retrofit.

### 5. An operator→user message channel

`OnReceiveServerMessage(MessageSeverity, string)`.

We need this and had not specified it. **The relay service policy requires a shutdown notice** —
this is how that notice reaches a person mid-session rather than in a Discord post they never see.
It also covers "the relay is restarting in five minutes" and degraded-service warnings.

**This should become a requirement in PRD-1.** It is small, it is needed by a commitment we have
already made, and nobody had connected the two.

### 6. Their `Group` is our session

`GroupPairJoined`, `GroupPairLeft`, `GroupChangePermissions`, `GroupSendFullInfo`,
`GroupPairChangeUserInfo`. A named group, members joining and leaving, per-member permissions, and
a full-state push for a client that needs to catch up.

That last one — `OnGroupSendFullInfo` versus `OnGroupSendInfo` — is the **reconnect problem we
specified in PRD-1 R-1.4 and PRD-3 R-3.7**, solved the same way: a full-state message distinct from
incremental updates. Worth knowing our design converges with a shipped one.

---

## What we must NOT take, and why

This is a **service with accounts**, and we are not building one.

| Theirs | Why it is forbidden for us |
| --- | --- |
| `Routes/LightlessAuth.cs`, user accounts, login | D-8: no cross-campaign identity, no accounts. Their model is a persistent user identity by design |
| User profiles, `OnUserUpdateProfile` | Same. Persistent per-user state on a server |
| Server-stored groups, pairings, permissions | **D-2: the relay stores nothing.** Theirs is authoritative and persistent; ours forwards and forgets |
| File upload/download, `LightlessFiles`, `OnDownloadReady` | Not our product, and it is most of their server's weight |
| `OnUserSendOnline` / presence tracking | Presence implies a persistent roster the server owns. Ours belongs to the DM's client (D-3) |

**The rule for reviewing any PR that cites this research: copy the shape, never the substance.**
Their server is the source of truth and remembers everything. Ours is a pipe that remembers nothing
(D-2, D-3, A-1.5e). A pattern lifted from a stateful server into a stateless relay must be checked
for assumptions it carried across.

**One thing needing engineering judgement, flagged rather than decided:** if SignalR is adopted, its
connection-group membership is in-memory routing rather than stored state, which I read as
compatible with A-1.5e. A **persistent backplane** (Redis or similar, usually added for scale-out)
would be a different matter and should come to me before it is added, not after.

---

## Recommended actions

1. **Adopt the shared-contract-project pattern.** Strong recommendation. It makes D-3 mechanical.
2. **Add the operator→user message channel to PRD-1** as a requirement. It is the delivery mechanism
   for a shutdown notice we have already committed to in the service policy.
3. **Take the DTO discipline and the `On<Subject><Verb>` naming.** Cheap now, expensive later.
4. **Have engineering justify hand-rolling a transport** if it chooses to, rather than defaulting to
   it. Not a mandate — SignalR may be wrong for us — but the alternative should be an argued choice.
5. **Do not import their auth, profile, presence or file-transfer model in any form.** Those are the
   parts that make theirs a service and would make ours one.
