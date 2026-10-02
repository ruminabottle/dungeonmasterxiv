# Reconnect Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A dropped connection comes back on its own:

- the relay holds a session while the DM is away;
- both clients redial;
- a player resumes without a prompt and catches up on what they missed;
- a blip shorter than ten seconds is invisible.

**Architecture:**

- **Relay.** When the host's connection closes, the relay keeps the session in memory, if the host registered a reclaim hash. It tells members `HostAway`, accepts a `Reclaim` carrying the secret, answers `Reclaimed`, and tells members `HostBack`. A background reaper ends holds older than the host-hold setting.
- **Clients.** Clients redial on a backoff schedule while their grace or seat window runs.
  - The host sends `Reclaim` and re-publishes the roster on `Reclaimed`.
  - A player sends `Resume`, sealed with its pairwise key and naming the last stream line it holds. The host admits it silently and re-sends the missed lines to that player alone.
- **Silent window.** Outgoing player messages wait in an outbox while the path is down, and a "reconnecting" line appears only after ten seconds.

**Tech Stack:** C# / .NET 10, ASP.NET Core (relay), Dalamud ImGui (plugin), xUnit, System.Security.Cryptography (SHA-256).

**Spec:** `docs/superpowers/specs/2026-10-01-reconnect-design.md`. The area requirements are:

- session-layer R-1.4, R-1.4a, R-1.5a;
- rolls R-2.10;
- product-overview D-2.

## Global Constraints

- **Grace and seat windows.** Both are 5 minutes (`GraceWindow.Default`). The window measures the whole outage: a reconnect never restarts it.
- **Relay hold.** The relay's host hold defaults to `GraceWindow.Default`. It is read from `DMX_RELAY_HOST_HOLD_SECONDS`. The relay keeps the hold in memory only and writes nothing.
- **Redial waits.** 1, 2, 4 and 8 seconds, then every 15 seconds. Keys are kept, never regenerated.
- **Silent window.** 10 seconds. After it the reconnecting lines read exactly:
  - "Reconnecting to the relay… the session ends in {m:ss} if it doesn't come back." (the DM)
  - "The DM is reconnecting… the session ends in {m:ss} if they don't come back." (players, while the DM is away)
  - "Reconnecting… your seat is held for {m:ss}." (a player whose own link is down)
- **Reclaim secret.** 32 random bytes, held only in memory. The relay stores and compares SHA-256(secret), in fixed time.
- **Wire format.** New message types are `HostAway = 14`, `HostBack = 15`, `Reclaim = 16`, `Reclaimed = 17` and `Resume = 18`. New optional fields are `ReclaimHash` and `ReclaimSecret`. All are additive. `ProtocolVersion.Current` stays `2`.
- **No security text in the plugin.** Forbidden: "anonymous", "private", "we can't see anything", "no one can see your session", relay-cannot-correlate claims, and verified or protected-against-interception claims.
- **Tests.** Smoke tests only: at most one smoke test per task, no narrative comments in tests.
- **Code comments.** Exactly one `/// <summary>` line per type, and no spec IDs in code.
- **Build:** `dotnet build DungeonMasterXIV.sln -c Release`.
- **Test:** `dotnet test DungeonMasterXIV.sln -c Release --no-build`. Quote each assembly's Total and Skipped. The baseline is Relay.Tests 3, Release.Tests 1 and Tests 5, all Skipped 0.
- **Commits:** `git commit -F -` with a quoted heredoc, ending `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`.
- **Branch:** `feat/reconnect` off `main`, with one PR at the end.

## Review Focus

Under the smoke-tests-only rule, the reviewer and a manual two-client run check these:

1. **The real socket drops without a close frame** (Wi-Fi off). The DM's client must treat it as a lost connection and redial. It must not report "relay unreachable" and end hosting. This is the `WebSocketSessionTransport` fix in Task 2, and the in-memory test harness cannot exercise it.
2. **A player sends a message in the instant after the DM drops,** before the player's client has processed `HostAway`. The relay drops that payload. Check the outbox is used whenever the player's grace or seat window runs. The residual race of a few milliseconds is acceptable and should be noted, not hidden.
3. **The DM and a player both drop at once.** The player's `Resume` gets `HostAway` back, and must be re-sent after `HostBack`, not given up.
4. **The hold runs out on the relay while a player is still connected.** The player's own countdown ends the session with the "DM did not come back" message. It never hangs on "reconnecting".
5. **The DM ends the session deliberately during a player's outage.** The player's resume is refused, and they see an ended or expired message rather than reconnecting forever.

**Not in this plan:**

- **Gap markers.** The host's `SessionRecording` keeps every entry for the whole session, so a missed line is always still held, and a gap cannot arise. Gap marking (rolls R-2.10) arrives with the first bound on what the host holds.
- **Entitlement filtering in catch-up.** No DM-private messages exist yet, and every recorded line went to everyone.

---

### Task 1: The relay holds and hands back a session

**Files:**
- Create: `src/DungeonMasterXIV.Relay/Transport/HeldSessionReaper.cs`
- Create: `tests/DungeonMasterXIV.Relay.Tests/AHeldSessionIsReclaimedTests.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/WireMessageType.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/WireShape.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/WireEnvelope.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/EnvelopeCodec.cs`
- Modify: `src/DungeonMasterXIV.Relay/Sessions/LiveSession.cs`
- Modify: `src/DungeonMasterXIV.Relay/Sessions/SessionRegistry.cs`
- Modify: `src/DungeonMasterXIV.Relay/Sessions/ConnectionRemoval.cs`
- Modify: `src/DungeonMasterXIV.Relay/Sessions/RelayDecision.cs`
- Modify: `src/DungeonMasterXIV.Relay/Sessions/RelayRouter.cs`
- Modify: `src/DungeonMasterXIV.Relay/Transport/RelayHub.cs`
- Modify: `src/DungeonMasterXIV.Relay/RelayOptions.cs`
- Modify: `src/DungeonMasterXIV.Relay/RelayApp.cs`
- Modify: `tests/DungeonMasterXIV.Relay.Tests/LoopbackRelay.cs`
- Modify: `RELAY-SERVICE-POLICY.md`

**Interfaces produced:**
- **Wire types:**
  - `WireMessageType.HostAway = 14`, `HostBack = 15`, `Reclaim = 16`, `Reclaimed = 17`, `Resume = 18`.
  - `WireEnvelope.ReclaimHash` and `WireEnvelope.ReclaimSecret`, both `byte[]?`.
- **Factories:**
  - `ForCodeRequest(SessionCode, byte[]? reclaimHash = null)`
  - `ForReclaim(SessionCode, byte[] reclaimSecret)`
  - `ForReclaimed(SessionCode)`
  - `ForHostAway(SessionCode)`
  - `ForHostBack(SessionCode)`
  - `ForResume(SessionCode, byte[] publicKey, SealedPayload proof)`
- **`SessionRegistry`:**
  - `SessionRegistry(TimeProvider? clock = null)`
  - `TryClaim(SessionCode, string, byte[]? reclaimHash = null)`
  - `TryReclaim(SessionCode, byte[] secret, string connectionId, out IReadOnlyList<string> members)`
  - `IsHostAway(string code)`
  - `ExpireHolds(TimeSpan hold) -> IReadOnlyList<SessionDeparture>`
- **`SessionDeparture.HeldMembers`:** `IReadOnlyList<string>?`, non-null when the session is held rather than ended.
- **`RelayDecision`:**
  - `RelayDecision.Notice` (`WireEnvelope?`) and `NoticeRecipients` (`IReadOnlyList<string>?`)
  - `RelayDecision.AlsoTelling(IReadOnlyList<string>, WireEnvelope)`
- **`RelayOutcome`:** `HostAway = 17`, `Reclaimed = 18`, `ReclaimRefused = 19`, `ResumeForwarded = 20`.
- **`RelayHub`:** `RelayHub.ExpireHoldsAsync(TimeSpan hold, CancellationToken)`.
- **`RelayOptions.HostHold`:** a `TimeSpan`.
- **Test harness:** `LoopbackRelay` now drives the real `RelayHub`. It adds:
  - `Drop(string id)`
  - `RunUntil(Func<bool> condition, TimeSpan? step = null)`

- [ ] **Step 1: Create the branch**

```bash
git checkout main && git pull && git checkout -b feat/reconnect
```

- [ ] **Step 2: Write the failing relay smoke test**

`tests/DungeonMasterXIV.Relay.Tests/AHeldSessionIsReclaimedTests.cs`:

```csharp
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Sessions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A dropped host's session is held for its players and handed back only to the holder of the secret.</summary>
public sealed class AHeldSessionIsReclaimedTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");
    private static readonly byte[] JoinerKey = [1, 2, 3];

    [Fact]
    public void TheSessionWaitsForTheHostAndOnlyTheSecretReclaimsIt()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var registry = new SessionRegistry();
        var router = new RelayRouter(registry);
        router.Route(WireEnvelope.ForCodeRequest(Code, SHA256.HashData(secret)), "host-1");
        router.Route(WireEnvelope.ForJoinHello(Code, JoinerKey), "joiner-1");
        registry.TryAdmit(Code.Value, JoinerKey, out _);

        var departure = Assert.Single(registry.Remove("host-1").Departures);
        Assert.False(departure.EndedSession);
        Assert.Equal(["joiner-1"], departure.HeldMembers);

        var payload = WireEnvelope.ForSessionPayload(Code, SealedPayload.FromWire(new byte[12], [9]));
        Assert.Equal(RelayOutcome.HostAway, router.Route(payload, "joiner-1").Outcome);
        Assert.Equal(
            RelayOutcome.ReclaimRefused,
            router.Route(WireEnvelope.ForReclaim(Code, RandomNumberGenerator.GetBytes(32)), "host-2").Outcome);

        var back = router.Route(WireEnvelope.ForReclaim(Code, secret), "host-2");
        Assert.Equal(RelayOutcome.Reclaimed, back.Outcome);
        Assert.Equal(["joiner-1"], back.NoticeRecipients);
        Assert.Equal(["host-2"], router.Route(payload, "joiner-1").Recipients);
    }
}
```

- [ ] **Step 3: Run it and watch it fail**

Run: `dotnet build DungeonMasterXIV.sln -c Release`

Expected: build FAIL. `ForCodeRequest` takes no hash, and `ForReclaim`, `HeldMembers` and `RelayOutcome.HostAway` do not exist.

- [ ] **Step 4: Extend the wire contract**

**`WireMessageType.cs`:** add after `HostKey = 13,`:

```csharp

    HostAway = 14,

    HostBack = 15,

    Reclaim = 16,

    Reclaimed = 17,

    Resume = 18,
```

**`WireShape.cs`:** add the two properties below. Then make three more changes:
- **`EnvelopeCodec.Encode`:** copy the two fields, as `ReclaimHash = envelope.ReclaimHash,` and `ReclaimSecret = envelope.ReclaimSecret,`.
- **`WireEnvelope.FromWire`:** copy them back, as `ReclaimHash = wire.ReclaimHash, ReclaimSecret = wire.ReclaimSecret,`.
- **`WireEnvelope`:** add `public byte[]? ReclaimHash { get; private init; }` and `public byte[]? ReclaimSecret { get; private init; }`.

```csharp
    public byte[]? ReclaimHash { get; set; }

    public byte[]? ReclaimSecret { get; set; }
```

**`WireEnvelope.cs`:** replace `ForCodeRequest`, and add the new factories after it:

```csharp
    public static WireEnvelope ForCodeRequest(SessionCode code, byte[]? reclaimHash = null) =>
        new(WireMessageType.CodeRequest, code.Value) { ReclaimHash = reclaimHash };

    public static WireEnvelope ForReclaim(SessionCode code, byte[] reclaimSecret)
    {
        ArgumentNullException.ThrowIfNull(reclaimSecret);
        return new WireEnvelope(WireMessageType.Reclaim, code.Value) { ReclaimSecret = reclaimSecret };
    }

    public static WireEnvelope ForReclaimed(SessionCode code) => new(WireMessageType.Reclaimed, code.Value);

    public static WireEnvelope ForHostAway(SessionCode code) => new(WireMessageType.HostAway, code.Value);

    public static WireEnvelope ForHostBack(SessionCode code) => new(WireMessageType.HostBack, code.Value);

    public static WireEnvelope ForResume(SessionCode code, byte[] publicKey, SealedPayload proof)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(proof);
        return new WireEnvelope(WireMessageType.Resume, code.Value)
        {
            PublicKey = publicKey,
            Nonce = proof.Nonce,
            Payload = proof.Ciphertext,
        };
    }
```

- [ ] **Step 5: Hold the session in the registry**

**`LiveSession.cs`:** replace the class with:

```csharp
namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>One session on the relay: its host connection (absent while held), members, pending joiners and reclaim hash.</summary>
internal sealed class LiveSession(string hostConnectionId, byte[]? reclaimHash)
{
    public string? HostConnectionId { get; set; } = hostConnectionId;

    public byte[]? ReclaimHash { get; } = reclaimHash;

    public DateTimeOffset? HostAwaySince { get; set; }

    public bool HostAway => HostAwaySince is not null;

    public Dictionary<string, string> Members { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Pending { get; } = new(StringComparer.Ordinal);

    public bool IsHost(string connectionId) =>
        HostConnectionId is not null && string.Equals(HostConnectionId, connectionId, StringComparison.Ordinal);

    public bool IsMember(string connectionId) => IsHost(connectionId) || Members.ContainsKey(connectionId);

    public bool HasPending(string connectionId) =>
        Pending.Values.Contains(connectionId, StringComparer.Ordinal);

    public IEnumerable<string> Everyone() =>
        HostConnectionId is { } host ? Members.Keys.Prepend(host) : Members.Keys;

    public void ForgetAllPending(string connectionId)
    {
        var stale = Pending
            .Where(entry => string.Equals(entry.Value, connectionId, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var key in stale)
        {
            Pending.Remove(key);
        }
    }
}
```

**`ConnectionRemoval.cs`:** add a final parameter `IReadOnlyList<string>? HeldMembers = null` to `SessionDeparture`, and change its summary to `/// <summary>One session a removed connection left: if it ended, its orphans; if held, the members to tell; otherwise its host and member key.</summary>`.

**`SessionRegistry.cs`:**
- Add `using System.Security.Cryptography;`.
- Add a field `private readonly TimeProvider _clock;` and a constructor:

```csharp
    public SessionRegistry(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;
```

- Replace `TryClaim`:

```csharp
    public bool TryClaim(SessionCode code, string hostConnectionId, byte[]? reclaimHash = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostConnectionId);

        lock (_gate)
        {
            if (_byCode.ContainsKey(code.Value) || _roles.Hosts(hostConnectionId))
            {
                return false;
            }

            _byCode[code.Value] = new LiveSession(hostConnectionId, reclaimHash);
            _roles.AddHost(hostConnectionId, code.Value);
            return true;
        }
    }
```

- In `TryGetHost`, return the host only when present: replace the body's `if (_byCode.TryGetValue(code, out var session))` with `if (_byCode.TryGetValue(code, out var session) && session.HostConnectionId is { } host)`, and assign `hostConnectionId = host;`.
- Add these methods:

```csharp
    public bool IsHostAway(string code)
    {
        lock (_gate)
        {
            return _byCode.TryGetValue(code, out var session) && session.HostAway;
        }
    }

    public bool TryReclaim(SessionCode code, byte[] secret, string connectionId, out IReadOnlyList<string> members)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentException.ThrowIfNullOrEmpty(connectionId);

        lock (_gate)
        {
            members = [];
            if (!_byCode.TryGetValue(code.Value, out var session)
                || !session.HostAway
                || session.ReclaimHash is not { } expected
                || _roles.Hosts(connectionId)
                || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(secret), expected))
            {
                return false;
            }

            session.HostConnectionId = connectionId;
            session.HostAwaySince = null;
            _roles.AddHost(connectionId, code.Value);
            members = session.Members.Keys.ToArray();
            return true;
        }
    }

    public IReadOnlyList<SessionDeparture> ExpireHolds(TimeSpan hold)
    {
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            var expired = _byCode
                .Where(entry => entry.Value.HostAwaySince is { } since && now - since >= hold)
                .Select(entry => entry.Key)
                .ToArray();

            var departures = new List<SessionDeparture>(expired.Length);
            foreach (var code in expired)
            {
                var session = _byCode[code];
                _byCode.Remove(code);

                var orphaned = session.Members.Keys.ToArray();
                foreach (var orphan in orphaned)
                {
                    _roles.Remove(orphan, code);
                }

                departures.Add(new SessionDeparture(code, EndedSession: true, orphaned));
            }

            return departures;
        }
    }
```

- In `Remove`, change the host branch from `session.IsHost(connectionId) ? EndSession(code, session, connectionId) : LeaveSession(code, session, connectionId)` to:

```csharp
                departures.Add(session.IsHost(connectionId)
                    ? session.ReclaimHash is not null
                        ? HoldSession(code, session)
                        : EndSession(code, session, connectionId)
                    : LeaveSession(code, session, connectionId));
```

- Add the method below.
- In `EndSession` and `LeaveSession`, replace `session.HostConnectionId` (used as the host id for `SessionDeparture`) with `session.HostConnectionId ?? string.Empty`.

```csharp
    private SessionDeparture HoldSession(string code, LiveSession session)
    {
        session.HostAwaySince = _clock.GetUtcNow();
        session.HostConnectionId = null;

        var waiting = session.Pending.Values.Distinct(StringComparer.Ordinal).ToArray();
        session.Pending.Clear();
        foreach (var joiner in waiting.Where(joiner => !session.Members.ContainsKey(joiner)))
        {
            _roles.Remove(joiner, code);
        }

        return new SessionDeparture(code, EndedSession: false, waiting, HeldMembers: session.Members.Keys.ToArray());
    }
```

- [ ] **Step 6: Route reclaim, resume and payloads while the host is away**

**`RelayDecision.cs`:**
- Add these outcomes after `HostKeyForwarded = 16,`:

```csharp

    HostAway = 17,

    Reclaimed = 18,

    ReclaimRefused = 19,

    ResumeForwarded = 20,
```

- Give the record two optional positional parameters, `WireEnvelope? Notice = null` and `IReadOnlyList<string>? NoticeRecipients = null`, after `CloseRecipients`.
- Add:

```csharp
    public RelayDecision AlsoTelling(IReadOnlyList<string> recipients, WireEnvelope notice) =>
        this with { Notice = notice, NoticeRecipients = recipients };
```

- Change the summary to `/// <summary>The router's verdict on a message: action, outcome, reply, recipients, whether to close them, and any notice for others.</summary>`.

**`RelayRouter.cs`:**
- Change the `CodeRequest` arm to `Arbitrate(code, senderConnectionId, envelope.ReclaimHash)`, and give `Arbitrate` that parameter, passing it on to `_registry.TryClaim(code, hostConnectionId, reclaimHash)`.
- Add these arms before the `CodeAccepted` arm:

```csharp
            WireMessageType.Reclaim => RouteReclaim(envelope, code, senderConnectionId),
            WireMessageType.Resume => RouteResume(code, senderConnectionId, envelope.PublicKey),

            WireMessageType.HostAway or WireMessageType.HostBack or WireMessageType.Reclaimed =>
                RelayDecision.Drop(RelayOutcome.RelayOnlyMessageFromClient),
```

- Add the two methods below.
- At the top of `ForwardPayload`, add:

```csharp
        if (_registry.IsHostAway(code.Value))
        {
            return RelayDecision.Drop(RelayOutcome.HostAway);
        }
```

The two methods:

```csharp
    private RelayDecision RouteReclaim(WireEnvelope envelope, SessionCode code, string senderConnectionId)
    {
        if (envelope.ReclaimSecret is not { } secret)
        {
            return RelayDecision.Drop(RelayOutcome.MalformedEnvelope);
        }

        return _registry.TryReclaim(code, secret, senderConnectionId, out var members)
            ? RelayDecision.Respond(RelayOutcome.Reclaimed, WireEnvelope.ForReclaimed(code))
                .AlsoTelling(members, WireEnvelope.ForHostBack(code))
            : RelayDecision.Respond(RelayOutcome.ReclaimRefused, WireEnvelope.ForCodeRefused(code));
    }

    private RelayDecision RouteResume(SessionCode code, string memberConnectionId, byte[]? publicKey)
    {
        if (_registry.IsHostAway(code.Value))
        {
            return RelayDecision.Respond(RelayOutcome.HostAway, WireEnvelope.ForHostAway(code));
        }

        if (!_registry.TryGetHost(code.Value, out var hostConnectionId))
        {
            return RelayDecision.Respond(RelayOutcome.SessionNotFound, WireEnvelope.ForCodeRefused(code));
        }

        if (publicKey is null)
        {
            return RelayDecision.Drop(RelayOutcome.MalformedEnvelope);
        }

        _registry.TryRegisterPending(code.Value, memberConnectionId, publicKey);
        return RelayDecision.Forward(RelayOutcome.ResumeForwarded, [hostConnectionId]);
    }
```

- [ ] **Step 7: The hub sends notices, tells members the host is away, and expires holds**

**`RelayHub.cs`:**
- At the end of `ReceiveAsync`, after the `switch`, add:

```csharp
        if (decision.Notice is { } notice && decision.NoticeRecipients is { } noticeRecipients)
        {
            await ForwardAsync(EnvelopeCodec.Encode(notice), noticeRecipients, cancellationToken).ConfigureAwait(false);
        }
```

- In `DisconnectAsync`, after `await TellHostsTheirMemberDroppedAsync(...)`, add `await TellMembersTheHostIsAwayAsync(removal, cancellationToken).ConfigureAwait(false);`.
- Add the methods below.
- Change the summary to `/// <summary>Decodes and routes incoming messages, holds sessions whose host dropped, and clears up on disconnect and expiry.</summary>`.

```csharp
    public async ValueTask ExpireHoldsAsync(TimeSpan hold, CancellationToken cancellationToken)
    {
        foreach (var departure in _registry.ExpireHolds(hold))
        {
            await CloseAsync(departure.OrphanedConnections, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask TellMembersTheHostIsAwayAsync(ConnectionRemoval removal, CancellationToken cancellationToken)
    {
        foreach (var departure in removal.Departures)
        {
            if (departure.HeldMembers is not { } members || !SessionCode.TryParse(departure.Code, out var code))
            {
                continue;
            }

            await ForwardAsync(EnvelopeCodec.Encode(WireEnvelope.ForHostAway(code)), members, cancellationToken)
                .ConfigureAwait(false);
            await CloseAsync(departure.OrphanedConnections, cancellationToken).ConfigureAwait(false);
        }
    }
```

**`src/DungeonMasterXIV.Relay/Transport/HeldSessionReaper.cs`:**

```csharp
namespace DungeonMasterXIV.Relay.Transport;

/// <summary>Every few seconds, ends the sessions whose host has been away longer than the hold.</summary>
public sealed class HeldSessionReaper(RelayHub hub, RelayOptions options) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await hub.ExpireHoldsAsync(options.HostHold, stoppingToken).ConfigureAwait(false);
        }
    }
}
```

**`RelayOptions.cs`:**
- Add:

```csharp
    public TimeSpan HostHold { get; init; } = GraceWindow.Default;
```

- In `FromEnvironment`, add `HostHold = ReadSeconds("HOST_HOLD_SECONDS") ?? GraceWindow.Default,`.

**`RelayApp.cs`:** after `builder.Services.AddSingleton<ProtocolVersionGate>();`, add `builder.Services.AddHostedService<HeldSessionReaper>();`.

- [ ] **Step 8: Drive the real hub from the in-memory test harness**

Replace `tests/DungeonMasterXIV.Relay.Tests/LoopbackRelay.cs` with the code below. Each client redial gets a fresh relay-side connection id, as a real socket would.

```csharp
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Diagnostics;
using DungeonMasterXIV.Relay.Sessions;
using DungeonMasterXIV.Relay.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>Connects session coordinators through the real relay hub in memory and keeps every frame the relay received.</summary>
internal sealed class LoopbackRelay
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 20, 0, 0, TimeSpan.Zero);

    private readonly RelayHub _hub;
    private readonly ConnectionDirectory _directory = new();
    private readonly Dictionary<string, Client> _clients = new(StringComparer.Ordinal);
    private readonly List<SessionCoordinator> _coordinators = new();
    private readonly Queue<(Client From, byte[] Frame)> _inFlight = new();

    public LoopbackRelay()
    {
        var registry = new SessionRegistry();
        _hub = new RelayHub(new RelayRouter(registry), registry, _directory, new RelayLog(NullLogger<RelayLog>.Instance));
    }

    public List<byte[]> Seen { get; } = new();

    public SessionCoordinator Connect(string id, SessionCapabilities? capabilities = null)
    {
        var client = new Client(this, id);
        _clients[id] = client;

        var coordinator = new SessionCoordinator(
            client,
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            log: QuietLog.Instance,
            capabilities: capabilities ?? SessionCapabilities.Default);

        _coordinators.Add(coordinator);
        return coordinator;
    }

    public void Drop(string id) => _clients[id].Drop();

    public void RunUntil(Func<bool> condition, TimeSpan? step = null)
    {
        for (var round = 0; round < 200 && !condition(); round++)
        {
            foreach (var coordinator in _coordinators)
            {
                coordinator.Tick(step ?? TimeSpan.Zero, Now);
            }

            Deliver();
        }

        Assert.True(condition(), "The session never reached the expected state.");
    }

    private void Deliver()
    {
        while (_inFlight.TryDequeue(out var sent))
        {
            if (sent.From.RelaySide is not { } relaySide)
            {
                continue;
            }

            Seen.Add(sent.Frame);
            _hub.ReceiveAsync(relaySide, sent.Frame, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
    }

    private sealed class Client(LoopbackRelay relay, string name) : ISessionTransport
    {
        private int _dials;

        public RelaySide? RelaySide { get; private set; }

        public bool IsConnected => RelaySide is not null;

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Connect(Uri relayAddress)
        {
            RelaySide = new RelaySide(this, $"{name}#{++_dials}");
            relay._directory.Add(RelaySide);
        }

        public void Disconnect() => Close(reportFailure: false);

        public void Send(byte[] envelope)
        {
            if (RelaySide is not null)
            {
                relay._inFlight.Enqueue((this, envelope));
            }
        }

        public void Drop() => Close(reportFailure: true);

        public void Deliver(byte[] frame) => Received?.Invoke(frame);

        public void Close(bool reportFailure)
        {
            if (RelaySide is not { } side)
            {
                return;
            }

            RelaySide = null;
            relay._hub.DisconnectAsync(side, "dropped", CancellationToken.None).AsTask().GetAwaiter().GetResult();
            if (reportFailure)
            {
                Failed?.Invoke(SessionFailure.ConnectionLost);
            }
        }
    }

    private sealed class RelaySide(Client client, string id) : IRelayConnection
    {
        public string Id { get; } = id;

        public ValueTask SendAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            client.Deliver(bytes);
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseAsync(CancellationToken cancellationToken)
        {
            client.Close(reportFailure: true);
            return ValueTask.CompletedTask;
        }
    }
}
```

`RelaySide` is a private nested class used as a public property type on another private nested class, which is fine. If the compiler objects to `Client.RelaySide`'s accessibility, make both nested classes `internal`.

- [ ] **Step 9: Tell the policy page about the hold**

In `RELAY-SERVICE-POLICY.md`, at the end of the `## What it stores` section, add this paragraph (wrap at about 100 characters):

```markdown
One exception, held in memory and never written: if your DM's connection drops, the relay keeps that
session open for up to five minutes so the DM can come back without anyone rejoining. It holds the
session code, a hash of a secret only the DM's running plugin knows, and a timer, and forgets all
three when the DM returns or the five minutes run out.
```

- [ ] **Step 10: Build and run every test**

Run: `dotnet build DungeonMasterXIV.sln -c Release && dotnet test DungeonMasterXIV.sln -c Release --no-build`

Expected: 0 errors. Relay.Tests Total 4, Release.Tests Total 1, Tests Total 5, all Skipped 0. The three earlier relay tests still pass through the hub-backed harness.

- [ ] **Step 11: Commit**

```bash
git add -A src tests RELAY-SERVICE-POLICY.md
git commit -F - <<'EOF'
feat(relay): hold a session while its host is away, and hand it back

A host that registered a reclaim hash keeps its session when its
connection drops: members are told HostAway, pending requests are closed,
and payloads are not forwarded. Reclaim with the matching secret
reattaches the host and tells members HostBack. A reaper ends holds older
than DMX_RELAY_HOST_HOLD_SECONDS (default 5 minutes). Adds the Resume
message type and routing for the player side. The test harness now
drives the real hub.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 2: Clients redial, and the host reclaims its session

**Files:**
- Create: `src/DungeonMasterXIV.Core/Net/ReconnectSchedule.cs`
- Create: `tests/DungeonMasterXIV.Relay.Tests/AHostBlipIsRecoveredTests.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/WebSocketSessionTransport.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionInterruption.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/HostRunner.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/OutboundHandshake.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionWiring.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/TransportNotices.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/InboundFrame.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/InboundWiring.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionCoordinator.cs`

**Interfaces:**
- **Consumes:** Task 1's wire factories and `LoopbackRelay.Drop` / `RunUntil(condition, step)`.
- **Produces:**
  - `ReconnectSchedule` with `Due(TimeSpan sinceLastTick) -> bool` and `Reset()`.
  - `HostRunner.ReclaimSecret` (`byte[]?`).
  - `SessionInterruption.Reconnecting` (`bool`).
  - `TransportNotices` gains `Action? OnReclaimed`, `Action? OnReclaimRefused`, `Action? OnHostAway` and `Action? OnHostBack`.
  - `InboundWiring` gains constructor parameter `Action onReclaimed`, `Action onReclaimRefused`, `Action onHostAway`, `Action onHostBack`.

Before editing, read `src/DungeonMasterXIV.Core/Net/TransportNotices.cs` and `src/DungeonMasterXIV.Core/Net/InboundFrame.cs` to see how `TransportNotices.Deliver` dispatches `ConnectionDropped`, and add the new notices in the same pattern.

- [ ] **Step 1: Write the failing smoke test**

`tests/DungeonMasterXIV.Relay.Tests/AHostBlipIsRecoveredTests.cs`:

```csharp
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A host whose connection drops redials, reclaims its session, and hears its players again with nobody rejoining.</summary>
public sealed class AHostBlipIsRecoveredTests
{
    [Fact]
    public void TheHostComesBackAndThePlayerStaysIn()
    {
        var relay = new LoopbackRelay();
        var host = relay.Connect("host");
        var player = relay.Connect("player");

        host.StartHosting();
        relay.RunUntil(() => host.Host.Phase == HostingPhase.Hosting);
        player.RequestJoin(host.Host.Code!.Value, DisplayName.OrNone("Tuka"), null);
        relay.RunUntil(() => host.Admissions.Pending.Count == 1);
        host.Admit(host.Admissions.Pending[0].PeerCode);
        relay.RunUntil(() => player.Join.Phase == JoinPhase.Admitted);

        relay.Drop("host");
        relay.RunUntil(() => host.Grace.IsRunning);
        relay.RunUntil(() => !host.Grace.IsRunning, TimeSpan.FromSeconds(1));

        Assert.Equal(HostingPhase.Hosting, host.Host.Phase);
        Assert.Equal(JoinPhase.Admitted, player.Join.Phase);

        player.Membership.Say("still here");
        relay.RunUntil(() => host.Recorded.Any(entry => entry.Text == "still here"));
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet build DungeonMasterXIV.sln -c Release && dotnet test tests/DungeonMasterXIV.Relay.Tests -c Release --no-build --filter AHostBlipIsRecoveredTests`

Expected: FAIL. "The session never reached the expected state" on `!host.Grace.IsRunning`, because nothing redials.

- [ ] **Step 3: Add the backoff schedule**

`src/DungeonMasterXIV.Core/Net/ReconnectSchedule.cs`:

```csharp
using System;

namespace DungeonMasterXIV.Net;

/// <summary>Spaces out redial attempts after a drop: about 1, 2, 4 and 8 seconds, then every 15.</summary>
internal sealed class ReconnectSchedule
{
    private static readonly TimeSpan[] Waits =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(15),
    ];

    private int _attempt;
    private TimeSpan _waited;

    public bool Due(TimeSpan sinceLastTick)
    {
        _waited += sinceLastTick;
        if (_waited < Waits[Math.Min(_attempt, Waits.Length - 1)])
        {
            return false;
        }

        _waited = TimeSpan.Zero;
        _attempt++;
        return true;
    }

    public void Reset()
    {
        _attempt = 0;
        _waited = TimeSpan.Zero;
    }
}
```

- [ ] **Step 4: A drop after the socket opened is a lost connection, not an unreachable relay**

In `WebSocketSessionTransport.ConnectAsync`, track whether the connection opened, and classify a later failure as `ConnectionLost`:

```csharp
    private async Task ConnectAsync(ClientWebSocket socket, Uri relay, CancellationToken token)
    {
        var opened = false;
        try
        {
            await socket.ConnectAsync(relay, token).ConfigureAwait(false);
            _connected = socket;
            opened = true;
            await ReceiveLoopAsync(socket, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is WebSocketException
                                              or ObjectDisposedException
                                              or InvalidOperationException)
        {
            var failure = opened ? SessionFailure.ConnectionLost : ClassifyRefusal(socket);
            _log.Warning(
                exception,
                failure switch
                {
                    SessionFailure.ConnectionLost => "The connection to the session relay dropped.",
                    SessionFailure.RelayUnreachable => "Could not reach the session relay.",
                    _ => "The session relay refused this build's protocol version.",
                });
            Failed?.Invoke(failure);
        }
        finally
        {
            if (ReferenceEquals(socket, _socket))
            {
                _connecting = false;
            }
        }
    }
```

- [ ] **Step 5: Keep the session through a drop and a failed redial**

**`SessionInterruption.cs`:** replace `Fail` with the code below, and add `Reconnecting`. A redial that fails while a window runs is not the end. The window decides.

```csharp
    public bool Reconnecting => Grace.IsRunning || Seat.IsRunning;

    public void Fail(SessionFailure failure)
    {
        var dropped = failure == SessionFailure.ConnectionLost
            || (failure == SessionFailure.RelayUnreachable && Reconnecting);

        if (dropped && _host.Phase == HostingPhase.Hosting)
        {
            Grace.HostLost();
            return;
        }

        if (dropped && _join.Phase == JoinPhase.Admitted)
        {
            Seat.HostLost();
            return;
        }

        if (_host.Phase is HostingPhase.Registering or HostingPhase.Hosting)
        {
            _host.Fail(failure);
        }

        if (_join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted)
        {
            _join.Fail(failure);
        }

        _synchronise();
    }
```

- [ ] **Step 6: The host registers a reclaim hash and reclaims after a redial**

**`HostRunner.cs`:**
- Add `using System.Security.Cryptography;`.
- Add the property below.
- In `Start`, after `Keys = hostKeys;`, add `ReclaimSecret = RandomNumberGenerator.GetBytes(32);`.
- In `Stop`, before `Keys?.Dispose();`, add:

```csharp
        if (ReclaimSecret is { } secret)
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        ReclaimSecret = null;
```

The property:

```csharp
    public byte[]? ReclaimSecret { get; private set; }
```

**`OutboundHandshake.cs`:**
- Add constructor parameters `Func<byte[]?> reclaimSecret` and `Func<bool> hostAway`, stored in fields, plus a field `private bool _reclaimSentOnThisLink;`.
- In `RegisterWithRelayWhenReady`, send the hash:

```csharp
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForCodeRequest(
            code, _reclaimSecret() is { } secret ? SHA256.HashData(secret) : null)));
```

- Add `using System.Security.Cryptography;` if it is not already present.
- At the start of `SendWhatIsDue`, add:

```csharp
        if (!_link.IsReadyToSend)
        {
            _reclaimSentOnThisLink = false;
        }

        ReclaimWhenReconnected();
```

- Add:

```csharp
    private void ReclaimWhenReconnected()
    {
        if (_host.Phase != HostingPhase.Hosting
            || !_hostAway()
            || _host.Code is not { } code
            || _reclaimSecret() is not { } secret
            || _reclaimSentOnThisLink
            || !_link.IsReadyToSend)
        {
            return;
        }

        _reclaimSentOnThisLink = true;
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForReclaim(code, secret)));
    }
```

**`SessionWiring.cs`:** construct the handshake as `new OutboundHandshake(Link, Host, Join, () => Joiner?.Keys, () => Hosting?.ReclaimSecret, () => Interruption?.Grace.IsRunning == true)`. The lambdas read properties assigned later in the constructor, which is fine at call time.

- [ ] **Step 7: Deliver the new notices inbound**

**`TransportNotices.cs`:** replace the record with:

```csharp
using System;

namespace DungeonMasterXIV.Net;

/// <summary>Passes the relay's transport notices on: a member dropped, the host's session reclaimed, the host away or back.</summary>
public readonly record struct TransportNotices(
    Action<byte[]>? OnConnectionDropped = null,
    Action? OnReclaimed = null,
    Action? OnReclaimRefused = null,
    Action? OnHostAway = null,
    Action? OnHostBack = null)
{
    public void Deliver(WireEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        switch (envelope.Type)
        {
            case WireMessageType.ConnectionDropped
                when OnConnectionDropped is { } onDropped && envelope.PublicKey is { } memberKey:
                onDropped(memberKey);
                break;
            case WireMessageType.Reclaimed:
                OnReclaimed?.Invoke();
                break;
            case WireMessageType.HostAway:
                OnHostAway?.Invoke();
                break;
            case WireMessageType.HostBack:
                OnHostBack?.Invoke();
                break;
        }
    }
}
```

**`InboundFrame.cs`:**
- Widen `TryConnectionDropped`'s check to `envelope.Type is WireMessageType.ConnectionDropped or WireMessageType.Reclaimed or WireMessageType.HostAway or WireMessageType.HostBack`, still calling `handlers.Transport.Deliver(envelope)` and returning `true`.
- Rename it `TryTransportNotice`.
- In `TryCodeRefused`, before the existing joiner check, add:

```csharp
        if (envelope.Type == WireMessageType.CodeRefused && Host is { Phase: HostingPhase.Hosting })
        {
            Handlers.Transport.OnReclaimRefused?.Invoke();
            return true;
        }
```

**`InboundWiring.cs`:**
- Add constructor parameters `Action onReclaimed, Action onReclaimRefused, Action onHostAway, Action onHostBack`.
- Pass them into `new TransportNotices(OnConnectionDropped: ..., OnReclaimed: onReclaimed, OnReclaimRefused: onReclaimRefused, OnHostAway: onHostAway, OnHostBack: onHostBack)`.

- [ ] **Step 8: The coordinator redials, and handles reclaim**

**`SessionCoordinator.cs`:**
- Add a field `private readonly ReconnectSchedule _reconnect = new();`.
- Pass the four callbacks when building `InboundWiring` in `Tick`: `new InboundWiring(_admissions, _resources, _resolveRelink, _roster, Reclaimed, ReclaimRefused, HostWentAway, HostCameBack)`.
- Add the methods below.
- In `Tick`, directly after the `_interruption.Tick(...)` block and before `_timeouts.Advance`, add:

```csharp
        if (_interruption.Reconnecting && !_link.IsReadyToSend)
        {
            if (_reconnect.Due(sinceLastTick))
            {
                SynchroniseTransport();
            }
        }
        else
        {
            _reconnect.Reset();
        }
```

The methods:

```csharp
    private void Reclaimed()
    {
        _interruption.HostReconnected();
        _roster.Publish();
    }

    private void ReclaimRefused()
    {
        _hosting.Stop();
        _parts.Host.Fail(SessionFailure.ConnectionLost);
    }

    private void HostWentAway() => _interruption.Grace.HostLost();

    private void HostCameBack() => _interruption.Grace.HostReturned();
```

`HostWentAway` and `HostCameBack` are wired now, and Task 3 gives them their player behaviour. On a player client, `Grace` is the "DM away" window.

- [ ] **Step 9: Run the smoke test, then everything**

Run: `dotnet build DungeonMasterXIV.sln -c Release && dotnet test DungeonMasterXIV.sln -c Release --no-build`

Expected: 0 errors. Relay.Tests Total 5, Release.Tests Total 1, Tests Total 5, all Skipped 0.

- [ ] **Step 10: Commit**

```bash
git add -A src tests
git commit -F - <<'EOF'
feat: clients redial after a drop and the host reclaims its session

Hosting registers a reclaim hash; after a drop, both clients redial on a
1/2/4/8/15-second schedule while their window runs, and the host sends
Reclaim and re-publishes the roster on Reclaimed. A drop after the
socket opened is now a lost connection, not an unreachable relay, and a
failed redial during the window no longer ends the session.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 3: A player resumes without a prompt and catches up

**Files:**
- Create: `src/DungeonMasterXIV.Core/Net/ReceivedStream.cs`
- Create: `tests/DungeonMasterXIV.Relay.Tests/APlayerResumesAndCatchesUpTests.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/JoinDetails.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/JoinAttempt.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionFailure.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionInterruption.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/OutboundHandshake.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionWiring.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/JoinerAdmission.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/InboundFrame.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/InboundApplication.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/InboundWiring.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/AdmissionControl.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/RosterBroadcast.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionCoordinator.cs`

**Interfaces:**
- **Consumes:**
  - Task 1: `WireEnvelope.ForResume` and Resume routing.
  - Task 2: `SessionInterruption.Reconnecting`, the redial in `Tick`, and the `HostWentAway`/`HostCameBack` callbacks.
- **Produces:**
  - `JoinDetails.LastSequence` (`long?`).
  - `ReceivedStream` with `Lines`, `LastSequence`, `Add(IReadOnlyList<StreamLine>?)` and `Clear()`.
  - `SessionCoordinator.Received` (`IReadOnlyList<StreamLine>`).
  - `JoinAttempt.Resuming`, plus `StartResuming()`, `ResumeConfirmed()` and `SeatExpired()`.
  - `SessionFailure.SeatExpired = 12` and `SessionFailure.HostGone = 13`.
  - `AdmissionControl.Resume(byte[], WireEnvelope) -> (PeerCode Peer, long LastSequence)?`.
  - `RosterBroadcast.PublishEntriesTo(PeerCode, IReadOnlyList<StreamLine>)`.
  - `JoinerAdmission.OnResume` (`Action<byte[], WireEnvelope>?`).

- [ ] **Step 1: Write the failing smoke test**

`tests/DungeonMasterXIV.Relay.Tests/APlayerResumesAndCatchesUpTests.cs`:

```csharp
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A player who drops resumes without a prompt and receives the lines said while they were away.</summary>
public sealed class APlayerResumesAndCatchesUpTests
{
    [Fact]
    public void TheReturningPlayerGetsWhatTheyMissed()
    {
        var relay = new LoopbackRelay();
        var host = relay.Connect("host");
        var alice = relay.Connect("alice");
        var bob = relay.Connect("bob");

        host.StartHosting();
        relay.RunUntil(() => host.Host.Phase == HostingPhase.Hosting);
        var code = host.Host.Code!.Value;
        alice.RequestJoin(code, DisplayName.OrNone("Alice"), null);
        bob.RequestJoin(code, DisplayName.OrNone("Bob"), null);
        relay.RunUntil(() => host.Admissions.Pending.Count == 2);
        foreach (var request in host.Admissions.Pending.ToArray())
        {
            host.Admit(request.PeerCode);
        }

        relay.RunUntil(() => alice.Join.Phase == JoinPhase.Admitted && bob.Join.Phase == JoinPhase.Admitted);

        relay.Drop("bob");
        relay.RunUntil(() => host.Drops.Count == 1);
        alice.Membership.Say("one");
        alice.Membership.Say("two");
        relay.RunUntil(() => host.Recorded.Count(entry => entry.Kind == StreamEventKind.Message) == 2);

        relay.RunUntil(
            () => !bob.Join.Resuming && bob.Received.Any(line => line.Text == "two"),
            TimeSpan.FromSeconds(1));

        Assert.Equal(JoinPhase.Admitted, bob.Join.Phase);
        Assert.Empty(host.Admissions.Pending);
        Assert.Contains(bob.Received, line => line.Text == "one");
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet build DungeonMasterXIV.sln -c Release`

Expected: build FAIL. `JoinAttempt.Resuming` and `SessionCoordinator.Received` do not exist.

- [ ] **Step 3: Keep what the member received, and say where it got to**

`src/DungeonMasterXIV.Core/Net/ReceivedStream.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>Keeps the stream lines a member has received from the host, in sequence order.</summary>
internal sealed class ReceivedStream
{
    private readonly SortedDictionary<long, StreamLine> _lines = new();

    public IReadOnlyList<StreamLine> Lines => _lines.Values.ToList();

    public long LastSequence => _lines.Count == 0 ? 0 : _lines.Keys.Last();

    public void Add(IReadOnlyList<StreamLine>? lines)
    {
        if (lines is null)
        {
            return;
        }

        foreach (var line in lines)
        {
            _lines[line.Sequence] = line;
        }
    }

    public void Clear() => _lines.Clear();
}
```

**`JoinDetails.cs`:** add `public long? LastSequence { get; init; }` to `JoinDetails`.

**`SessionCoordinator.cs`:**
- Add a field `private readonly ReceivedStream _stream = new();` and `public IReadOnlyList<StreamLine> Received => _stream.Lines;`.
- In `HeardFromTheHost`, add `_stream.Add(content.Entries);`.
- In `RequestJoin(SessionCode, DisplayName, Guid?)`, clear it first: `_stream.Clear();` before `_joiner.Request(...)`.

- [ ] **Step 4: Join state for resuming and the two new endings**

**`SessionFailure.cs`:** add `SeatExpired = 12,` and `HostGone = 13,`, and their messages in `SessionFailureMessage.For`:

```csharp
        SessionFailure.SeatExpired =>
            "You were away too long to rejoin automatically. Ask to join again.",
        SessionFailure.HostGone =>
            "The DM did not come back in time, so the session ended. The DM can resume the campaign, "
            + "and you can join again when they do.",
```

**`JoinAttempt.cs`:**
- Add the members below.
- Set `Resuming = false;` in `Request` and `Left`.

```csharp
    public bool Resuming { get; private set; }

    public void StartResuming()
    {
        if (Phase == JoinPhase.Admitted)
        {
            Resuming = true;
        }
    }

    public void ResumeConfirmed() => Resuming = false;

    public void SeatExpired()
    {
        Resuming = false;
        Fail(SessionFailure.SeatExpired);
    }
```

**`SessionInterruption.cs`:**
- In `Fail`, in the `dropped && _join.Phase == JoinPhase.Admitted` branch, add `_join.StartResuming();` after `Seat.HostLost();`.
- Replace `Tick` with:

```csharp
    public bool Tick(TimeSpan sinceLastTick)
    {
        if (Seat.IsRunning && _join.Phase == JoinPhase.Admitted && !_join.Resuming)
        {
            Seat.HostReturned();
        }

        if (Seat.Tick(sinceLastTick))
        {
            _join.SeatExpired();
            _synchronise();
        }

        return Grace.Tick(sinceLastTick);
    }
```

**`SessionCoordinator.Tick`:** the existing `if (_interruption.Tick(sinceLastTick)) { StopHosting(now); return; }` ends hosting when the grace expires. On a player client the same `Grace` is the "DM away" window, so branch:

```csharp
        if (_interruption.Tick(sinceLastTick))
        {
            if (InAHostedSession)
            {
                StopHosting(now);
            }
            else if (Join.Phase == JoinPhase.Admitted)
            {
                Join.Fail(SessionFailure.HostGone);
                SynchroniseTransport();
            }

            return;
        }
```

- [ ] **Step 5: The player sends Resume after a redial, and again after the DM is back**

**`OutboundHandshake.cs`:**
- Add constructor parameters `Func<byte[]?> sessionKey` and `Func<long> lastSequence`, stored in fields, plus a field `private bool _resumeSentOnThisLink;`.
- In `SendWhatIsDue`, reset `_resumeSentOnThisLink = false;` in the same `!_link.IsReadyToSend` block as the reclaim flag, and call `ResumeWhenReconnected();`.
- Add `public void ResendResume() => _resumeSentOnThisLink = false;` and:

```csharp
    private void ResumeWhenReconnected()
    {
        if (_join.Phase != JoinPhase.Admitted
            || !_join.Resuming
            || _join.Code is not { } code
            || _joinerKeys() is not { } keys
            || _sessionKey() is not { } key
            || _resumeSentOnThisLink
            || !_link.IsReadyToSend)
        {
            return;
        }

        _resumeSentOnThisLink = true;
        var proof = JoinDetailsCodec.Seal(
            key, new JoinDetails { LastSequence = _lastSequence() }, code, WireMessageType.Resume);
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForResume(code, keys.PublicKey, proof)));
    }
```

**`SessionWiring.cs`:**
- Pass `() => Joiner?.SessionKey` and a `Func<long>` for the last sequence into the handshake.
- The received stream lives on the coordinator. Add a constructor parameter `Func<long> lastSequence` to `SessionWiring`, passed by `SessionCoordinator` as `() => _stream.LastSequence`. Declare `_stream` before constructing `_parts`, or initialise it at its field declaration, which it already is.

**`SessionCoordinator.HostCameBack`:** make it also re-send a pending resume:

```csharp
    private void HostCameBack()
    {
        _interruption.Grace.HostReturned();
        _handshake.ResendResume();
    }
```

- [ ] **Step 6: The player handles the host's answer**

**`InboundApplication.Apply`:**
- In `onAccepted`, before `attempt.Admitted();`, add:

```csharp
                if (attempt.Phase == JoinPhase.Admitted && attempt.Resuming)
                {
                    attempt.ResumeConfirmed();
                    return keys is not null && attempt.Code is { } resumedCode
                        ? keys.DeriveSharedKey(hostPublicKey, resumedCode)
                        : null;
                }
```

- In `onDenied`, replace `attempt.Denied();` with:

```csharp
                if (attempt.Resuming)
                {
                    attempt.SeatExpired();
                }
                else
                {
                    attempt.Denied();
                }
```

**`InboundFrame.TryCodeRefused`:** after the host branch from Task 2, add:

```csharp
        if (envelope.Type == WireMessageType.CodeRefused && attempt.Phase == JoinPhase.Admitted && attempt.Resuming)
        {
            attempt.SeatExpired();
            return true;
        }
```

Here `attempt` is `Attempt`. Declare `var attempt = Attempt;` at the top of the method if it is not already.

`HostAway` arriving as the relay's answer to a `Resume` needs no extra code: it reaches `HostWentAway`, and `HostCameBack` re-sends the resume.

- [ ] **Step 7: The host verifies a resume, admits silently and sends the catch-up**

**`JoinerAdmission.cs`:** add `Action<byte[], WireEnvelope>? OnResume = null` as a third member, and update the summary to `/// <summary>The callbacks the host uses for a joiner's hello, its sealed join request, and a member's resume.</summary>`.

**`InboundFrame.cs`:** add `TryResume`, in the `Apply` chain after `TryJoinRequest`:

```csharp
    private bool TryResume(WireEnvelope envelope)
    {
        if (envelope.Type != WireMessageType.Resume)
        {
            return false;
        }

        if (Handlers.Admission.OnResume is { } onResume
            && envelope.PublicKey is { } memberPublicKey
            && SessionKeyExchange.CanAgreeWith(memberPublicKey))
        {
            onResume(memberPublicKey, envelope);
        }

        return true;
    }
```

**`AdmissionControl.cs`:** add:

```csharp
    public (PeerCode Peer, long LastSequence)? Resume(byte[] memberPublicKey, WireEnvelope envelope)
    {
        if (_hostCode() is not { } code || _hostKeys() is not { } hostKeys)
        {
            return null;
        }

        var peer = PeerCodeFor(memberPublicKey);
        JoinDetails? details = null;
        if (Audience.IsAdmitted(peer) && Drops.WhenDropped(peer) is not null)
        {
            byte[] key;
            try
            {
                key = hostKeys.DeriveSharedKey(memberPublicKey, code);
            }
            catch (CryptographicException)
            {
                key = [];
            }

            if (key.Length > 0)
            {
                details = JoinDetailsCodec.TryOpen(key, envelope);
                CryptographicOperations.ZeroMemory(key);
            }
        }

        if (details is null)
        {
            _announcer.Denied(code, memberPublicKey);
            return null;
        }

        Drops.Forget(peer);
        _announcer.Accepted(code, memberPublicKey, hostKeys.PublicKey);
        return (peer, details.LastSequence ?? 0);
    }
```

**`RosterBroadcast.cs`:**
- Refactor `SealToEveryRecipient` so the per-peer body is a private method `SealTo(AdmittedPeer peer, byte[] plaintext, byte[] associatedData, SessionKeyExchange keys, SessionCode code)`. `SealToEveryRecipient` then loops over `_audience.Recipients` calling it.
- Add:

```csharp
    public void PublishEntriesTo(PeerCode recipient, IReadOnlyList<StreamLine> lines)
    {
        if (lines.Count == 0
            || _host.Keys() is not { } keys
            || _host.Code() is not { } code
            || !_link.IsReadyToSend
            || _audience.Find(recipient) is not { } peer)
        {
            return;
        }

        var plaintext = SessionContentCodec.Encode(new SessionContent { Entries = lines });
        SealTo(peer, plaintext, WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload), keys, code);
    }
```

- Add `using System.Collections.Generic;` if missing.

**`InboundWiring.cs`:** add `OnResume` to the `JoinerAdmission`:

```csharp
                OnResume: (key, envelope) =>
                {
                    if (admissions.Resume(key, envelope) is not { } resumed)
                    {
                        return;
                    }

                    var missed = resources.Recording.Entries
                        .Where(entry => entry.Stamp.Sequence > resumed.LastSequence)
                        .Select(entry => new StreamLine(
                            entry.Stamp.Sequence, entry.Stamp.AtUtcTicks, entry.Kind, entry.Peer.Value, entry.Text))
                        .ToList();

                    roster.Publish();
                    roster.PublishEntriesTo(resumed.Peer, missed);
                }),
```

Add `using System.Linq;` to `InboundWiring.cs` if missing.

- [ ] **Step 8: Run the smoke test, then everything**

Run: `dotnet build DungeonMasterXIV.sln -c Release && dotnet test DungeonMasterXIV.sln -c Release --no-build`

Expected: 0 errors. Relay.Tests Total 6, Release.Tests Total 1, Tests Total 5, all Skipped 0.

- [ ] **Step 9: Commit**

```bash
git add -A src tests
git commit -F - <<'EOF'
feat: a dropped player resumes without a prompt and catches up

After a redial the player sends Resume, sealed with its shared key and
naming the last line it holds. The host admits a dropped member whose
proof opens, with no prompt, and re-sends the lines it missed to that
member alone. A refused resume or an expired seat says the seat expired;
the DM away for the whole window ends the session with a resume hint.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 4: The silent window, held messages, and what people see

**Files:**
- Create: `src/DungeonMasterXIV.Core/Net/ReconnectNotice.cs`
- Create: `tests/DungeonMasterXIV.Relay.Tests/AMessageSentDuringABlipArrivesTests.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/GraceWindow.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/MemberMessage.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionMembership.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionWiring.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionCoordinator.cs`
- Modify: `Windows/MessageComposeView.cs`
- Modify: `Windows/SessionWindow.cs`
- Modify: `Windows/JoinFlowView.cs`

**Interfaces:**
- **Consumes:** Tasks 2 and 3: `SessionInterruption.Grace`/`Seat`, `JoinAttempt.Resuming`, and `SessionFailure.HostGone`/`SeatExpired`.
- **Produces:**
  - `GraceWindow.Elapsed` (`TimeSpan`).
  - `ReconnectNotice.Silent` (`TimeSpan`, 10 seconds).
  - `ReconnectNotice.For(bool hosting, GraceWindow grace, GraceWindow seat) -> string?`.
  - `SessionCoordinator.ReconnectingLine` (`string?`).
  - `SessionMembership.Undelivered` (`int`).
  - `MemberMessage.Flush()`.

- [ ] **Step 1: Write the failing smoke test**

`tests/DungeonMasterXIV.Relay.Tests/AMessageSentDuringABlipArrivesTests.cs`:

```csharp
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A message a player sends while the DM is briefly away waits and arrives once the DM is back.</summary>
public sealed class AMessageSentDuringABlipArrivesTests
{
    [Fact]
    public void TheHeldMessageIsDeliveredAfterTheBlip()
    {
        var relay = new LoopbackRelay();
        var host = relay.Connect("host");
        var player = relay.Connect("player");

        host.StartHosting();
        relay.RunUntil(() => host.Host.Phase == HostingPhase.Hosting);
        player.RequestJoin(host.Host.Code!.Value, DisplayName.OrNone("Tuka"), null);
        relay.RunUntil(() => host.Admissions.Pending.Count == 1);
        host.Admit(host.Admissions.Pending[0].PeerCode);
        relay.RunUntil(() => player.Join.Phase == JoinPhase.Admitted);

        relay.Drop("host");
        relay.RunUntil(() => player.Grace.IsRunning);
        Assert.Null(player.ReconnectingLine);

        player.Membership.Say("held for you");
        relay.RunUntil(() => host.Recorded.Any(entry => entry.Text == "held for you"), TimeSpan.FromSeconds(1));
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet build DungeonMasterXIV.sln -c Release`

Expected: build FAIL. `SessionCoordinator.ReconnectingLine` does not exist. Without the outbox, the message would also be sent into the relay's host-away drop and lost.

- [ ] **Step 3: Elapsed time and the notice text**

**`GraceWindow.cs`:** add `public TimeSpan Elapsed => _elapsed;`.

`src/DungeonMasterXIV.Core/Net/ReconnectNotice.cs`:

```csharp
using System;
using System.Globalization;

namespace DungeonMasterXIV.Net;

/// <summary>The one reconnecting line a client shows, only once a drop has outlasted the silent window.</summary>
public static class ReconnectNotice
{
    public static readonly TimeSpan Silent = TimeSpan.FromSeconds(10);

    public static string? For(bool hosting, GraceWindow grace, GraceWindow seat)
    {
        ArgumentNullException.ThrowIfNull(grace);
        ArgumentNullException.ThrowIfNull(seat);

        if (seat.IsRunning && seat.Elapsed >= Silent)
        {
            return $"Reconnecting… your seat is held for {Clock(seat.Remaining)}.";
        }

        if (!grace.IsRunning || grace.Elapsed < Silent)
        {
            return null;
        }

        return hosting
            ? $"Reconnecting to the relay… the session ends in {Clock(grace.Remaining)} if it doesn't come back."
            : $"The DM is reconnecting… the session ends in {Clock(grace.Remaining)} if they don't come back.";
    }

    private static string Clock(TimeSpan remaining) =>
        remaining.ToString(@"m\:ss", CultureInfo.InvariantCulture);
}
```

**`SessionCoordinator.cs`:** add:

```csharp
    public string? ReconnectingLine =>
        ReconnectNotice.For(InAHostedSession, _interruption.Grace, _interruption.Seat);
```

- [ ] **Step 4: Hold outgoing messages while the path is down**

**`MemberMessage.cs`:**
- Add a constructor parameter `Func<bool> pathDown` (stored).
- Add a field `private readonly Queue<string> _waiting = new();`.
- Add `using System.Collections.Generic;`.
- Change the summary to `/// <summary>Checks a member's chat message and sends it to the host sealed, holding it while the path is down.</summary>`.
- Replace `Say`, and add the members below:

```csharp
    public int Waiting => _waiting.Count;

    public MessageDraft Say(string? text, MessageLimits limits)
    {
        var draft = MessageDraft.Compose(text, limits);
        if (!draft.IsAccepted)
        {
            return draft;
        }

        if (_code() is null || _sessionKey() is null)
        {
            return new MessageDraft(null, MessageFault.NotInASession, "This client is not in a session.");
        }

        _waiting.Enqueue(draft.Text!);
        Flush();
        return draft;
    }

    public void Flush()
    {
        while (_waiting.Count > 0 && !_pathDown() && _code() is { } code && _sessionKey() is { } key)
        {
            var plaintext = SessionContentCodec.Encode(new SessionContent { Saying = _waiting.Dequeue() });
            var sealedPayload = SessionCipher.Seal(
                key, plaintext, WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));
            _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForSessionPayload(code, sealedPayload)));
        }
    }

    public int Abandon()
    {
        var undelivered = _waiting.Count;
        _waiting.Clear();
        return undelivered;
    }
```

**`SessionMembership.cs`:**
- Add a constructor parameter `Func<bool> pathDown`, passed to `new MemberMessage(link, code, () => joiner.SessionKey, pathDown)`.
- Add:

```csharp
    public int Undelivered { get; private set; }

    internal void FlushWaiting() => _message.Flush();

    internal void AbandonWaiting() => Undelivered = _message.Abandon();
```

- In `Leave()`, call `AbandonWaiting();` first.

**`SessionWiring.cs`:** construct membership as `new SessionMembership(Link, Joiner, () => Join.Code, () => !Link.IsReadyToSend || Interruption.Reconnecting)`.

**`SessionCoordinator.Tick`:** after `_handshake.SendWhatIsDue();`, add:

```csharp
        Membership.FlushWaiting();
        if (Join.Phase is JoinPhase.Failed or JoinPhase.Idle)
        {
            Membership.AbandonWaiting();
        }
```

Keep `Undelivered` from being reset to zero by a later idle tick. In `AbandonWaiting`, only overwrite when something was abandoned:

```csharp
    internal void AbandonWaiting()
    {
        var abandoned = _message.Abandon();
        if (abandoned > 0)
        {
            Undelivered = abandoned;
        }
    }
```

In `SessionCoordinator.RequestJoin(SessionCode, DisplayName, Guid?)`, clear it for a new session. Make `Undelivered`'s setter `internal`, and set `Membership.Undelivered = 0;` before `_joiner.Request(...)`.

- [ ] **Step 5: Show the line, and disable the box after the silent window**

**`Windows/MessageComposeView.cs`:** at the start of `Draw`, add the block below, and at the end of `Draw` add `ImGui.EndDisabled();`.

```csharp
        var reconnecting = _coordinator.ReconnectingLine;
        if (reconnecting is not null)
        {
            ImGui.TextWrapped(reconnecting);
        }

        ImGui.BeginDisabled(reconnecting is not null);
```

**`Windows/SessionWindow.cs`:** replace the `if (_coordinator.Grace.IsRunning) { ... }` block with:

```csharp
            if (_coordinator.ReconnectingLine is { } reconnecting)
            {
                ImGui.TextWrapped(reconnecting);
            }
```

**`Windows/JoinFlowView.cs`:** after the `join.Failure != SessionFailure.None` block, add:

```csharp
        if (_coordinator.Membership.Undelivered > 0)
        {
            ImGui.TextWrapped(
                $"{_coordinator.Membership.Undelivered} messages you sent were not delivered.");
        }
```

- [ ] **Step 6: Build and run every test**

Run: `dotnet build DungeonMasterXIV.sln -c Release && dotnet test DungeonMasterXIV.sln -c Release --no-build`

Expected: 0 errors. Relay.Tests Total 7, Release.Tests Total 1, Tests Total 5, all Skipped 0.

- [ ] **Step 7: Check the user-facing text**

Run: `grep -rn -i -E "anonymous|we can't see|no one can see|verified|protected|tamper" --include='*.cs' src/DungeonMasterXIV.Core/Net/ReconnectNotice.cs src/DungeonMasterXIV.Core/Net/SessionFailure.cs Windows | grep -v "/bin/\|/obj/"`

Expected: no output.

- [ ] **Step 8: Commit**

```bash
git add -A src Windows tests
git commit -F - <<'EOF'
feat: a blip is invisible, and messages wait for the path to return

A player's messages wait on their machine while the path to the DM is
down and go out in order when it returns. Nothing is shown for the first
ten seconds of a drop; after that one reconnecting line with the time
left appears, and the message box is disabled until the path is back.
Messages still waiting when the session ends are counted for the player.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

- [ ] **Step 9: Push and open the PR** (the controller does this at finish, on the user's choice)

The PR body summarises the four commits and carries this release note:

> **A dropped connection now comes back on its own.** If the DM's or a player's internet drops, their plugin reconnects automatically, and nothing shows for the first ten seconds. The session waits up to five minutes for the DM. A returning player gets back in without being asked, and catches up on what they missed. **Deploy the relay and the plugin together**, because the relay change is what keeps a session alive while the DM is away.
