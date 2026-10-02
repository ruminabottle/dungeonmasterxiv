# Connection Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Joining exchanges keys first and sends the join request sealed, the fingerprint comparison is gone, the DM can let a campaign's returning players straight in, and the plugin carries no security text, with storage deletion only in settings.

**Architecture:**
- **Two new wire messages:**
  - `JoinHello`: joiner to host, carrying the joiner's public key.
  - `HostKey`: host to joiner, carrying the host's public key.
- **Sealed payloads:** after the hello exchange, `JoinRequest` and `JoinAccepted` carry a payload sealed with the existing pairwise key (ECDH P-256 + HKDF, AES-GCM). The relay never sees a name or a participant ID.
- **Returning players:** a per-campaign flag that `AdmissionControl` consults to admit a stored participant without a prompt.
- **UI:** settings gains Campaign storage and User storage sections and a policy link, and every security explanation string goes.

**Tech Stack:** C# / .NET 10, Dalamud (ImGui), xUnit, BouncyCastle (key agreement), System.Text.Json.

**Spec:** `docs/superpowers/specs/2026-10-01-connection-design.md`. The area requirements are session-layer R-1.3m, R-1.3d-1, R-1.5b, R-1.5f, R-1.6, R-1.7, R-1.7a and R-1.9.

## Global Constraints

- **Protocol:** `ProtocolVersion.Current` becomes `2`. This is a breaking change under product-overview D-14, so v1 plugins are refused at connect.
- **No security text:** no screen in the plugin carries a security or privacy explanation, or a warning about one.
- **Forbidden phrasings:**
  - "anonymous", "private", "we can't see anything", "no one can see your session"
  - any claim that the relay cannot correlate sessions
  - any claim that a session is verified or protected against interception
- **Smoke tests only.** Add at most one smoke test per feature. No regression, structure or text-scan tests, and no narrative comments in tests.
- **Code comments:** each type has exactly one `/// <summary>` line, and code cites no spec IDs.
- **Build:** `dotnet build DungeonMasterXIV.sln -c Release`
- **Test:** `dotnet test DungeonMasterXIV.sln -c Release --no-build`
  - Quote the `Total`/`Skipped` lines. The baseline is Relay.Tests 1, Release.Tests 1 and Tests 5, all with Skipped 0.
- **Commits:** write messages with `git commit -F -` and a quoted heredoc, never `-m` with backticks. End each with `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`.
- **Branch:** `feat/connection-redesign`, off `main`, with one PR at the end.

## Review Focus

The project rule is smoke tests only, so these are checked by the reviewer and in a manual two-client run, not pinned by new tests:

1. **An old (v1) plugin connects to a v2 relay.** It is refused with the "update your plugin" message (`SessionFailure.PluginBehindRelay`), not a hang or a generic failure.
2. **A join request the host cannot open** (wrong key, altered on the way). The host discards it and logs a warning. The joiner waits out the 10-second contact timeout. Check that the message it then sees (`RelayUnreachable`) is not false for that case. If it is, give it its own wording.
3. **Two clients present the same stored participant ID** (a copied settings file) with "Let them straight in" on. The first is admitted automatically and the second goes to the prompt. Neither takes the other's seat.
4. **No campaign is open while hosting.** The returning-players setting reads as "Ask me each time" and admitting still works.
5. **A joiner re-requests after a lapse.** A fresh hello goes out with fresh keys, the host sees a new request with a new peer code, and the old one is gone from the desk.

**Not in this plan:** session-layer R-1.5e, the DM mapping a joiner onto a *different* stored entry from a list. The prompt here offers only "as their claimed entry" or "as a new player". R-1.5e gets its own plan.

---

### Task 1: Keys-first join, sealed request and welcome, fingerprint removed

**Files:**
- Create: `src/DungeonMasterXIV.Core/Net/JoinDetails.cs`
- Create: `tests/DungeonMasterXIV.Relay.Tests/LoopbackRelay.cs`
- Create: `tests/DungeonMasterXIV.Relay.Tests/QuietLog.cs`
- Create: `tests/DungeonMasterXIV.Relay.Tests/AJoinTravelsSealedTests.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/WireMessageType.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/WireShape.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/WireEnvelope.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/EnvelopeCodec.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/WireEnvelopeReading.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/ProtocolVersion.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/JoinAttempt.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/OutboundHandshake.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/InboundFrame.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/ParticipantReceipt.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/JoinerAdmission.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/InboundWiring.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/AdmissionAnnouncer.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/AdmissionControl.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/PendingAdmission.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/AdmissionPrompt.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionAudience.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/AdmittedPeer.cs`
- Modify: `src/DungeonMasterXIV.Relay/Sessions/RelayRouter.cs`
- Modify: `src/DungeonMasterXIV.Relay/Sessions/RelayDecision.cs`
- Modify: `Windows/AdmissionPromptView.cs`
- Modify: `Windows/JoinFlowView.cs`
- Modify: `Windows/SessionWindow.cs`
- Modify: `tests/DungeonMasterXIV.Relay.Tests/RelayRouterTests.cs`
- Delete: `src/DungeonMasterXIV.Core/Net/KeyFingerprint.cs`
- Delete: `src/DungeonMasterXIV.Core/Net/AdmissionVerification.cs`
- Delete: `src/DungeonMasterXIV.Core/Net/ComparabilityEvidence.cs`
- Delete: `Windows/JoinComparisonView.cs`

**Interfaces produced:**
- **Wire messages:** `WireMessageType.JoinHello = 12` and `WireMessageType.HostKey = 13`. `JoinerHoldsFingerprint = 10` is removed, and 10 is never reused.
- **`WireEnvelope` factories:**
  - `ForJoinHello(SessionCode, byte[] publicKey)`
  - `ForHostKey(SessionCode, byte[] joinerPublicKey, byte[] hostPublicKey)`
  - `ForJoinRequest(SessionCode, byte[] publicKey, SealedPayload details)`
  - `ForJoinAccepted(SessionCode, byte[] joinerPublicKey, byte[] hostPublicKey, SealedPayload? welcome = null)`
- **`JoinDetails`:** a record with `string? DisplayName` and `string? ParticipantId`.
- **`JoinDetailsCodec`:**
  - `Seal(byte[] key, JoinDetails, SessionCode, WireMessageType) -> SealedPayload`
  - `TryOpen(byte[] key, WireEnvelope) -> JoinDetails?`
- **`JoinAttempt.HostPublicKey`:** a `byte[]?`.
- **`JoinerAdmission`:** `(Action<byte[]>? OnHello, Action<byte[], WireEnvelope>? OnJoinRequest)`.
- **`AdmissionControl`:**
  - `OfferHostKey(byte[] joinerPublicKey)`
  - `OpenJoinRequest(byte[] joinerPublicKey, WireEnvelope) -> JoinDetails?`
- **`SessionAudience.Admit`:** `Admit(PeerCode, SessionRole, byte[]? publicKey, DisplayName displayName)`, with no verification parameter.
- **`PendingAdmission`:** `(PeerCode, AdmissionDeadline, RelinkClaim = default, byte[]? joinerPublicKey = null, DisplayName displayName = default)`.
- **Relay:** `RelayOutcome.HostKeyForwarded = 16`.
- **Test helper `LoopbackRelay`:**
  - `Connect(string id, SessionCapabilities? capabilities = null) -> SessionCoordinator`
  - `RunUntil(Func<bool>)`
  - `Seen` (`List<byte[]>`)

- [ ] **Step 1: Create the branch**

```bash
git checkout main && git pull && git checkout -b feat/connection-redesign
```

- [ ] **Step 2: Write the in-memory relay helper and its quiet log**

`tests/DungeonMasterXIV.Relay.Tests/QuietLog.cs`:

```csharp
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A session log that records nothing.</summary>
internal sealed class QuietLog : ISessionTransportLog
{
    public static readonly QuietLog Instance = new();

    public void Information(string message)
    {
    }

    public void Warning(string message)
    {
    }

    public void Warning(Exception exception, string message)
    {
    }
}
```

`tests/DungeonMasterXIV.Relay.Tests/LoopbackRelay.cs`:

```csharp
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Sessions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>Connects session coordinators through the real relay router in memory and keeps every frame the relay saw.</summary>
internal sealed class LoopbackRelay
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 20, 0, 0, TimeSpan.Zero);

    private readonly RelayRouter _router = new(new SessionRegistry());
    private readonly Dictionary<string, Connection> _connections = new(StringComparer.Ordinal);
    private readonly List<SessionCoordinator> _coordinators = new();
    private readonly Queue<(string From, byte[] Frame)> _inFlight = new();

    public List<byte[]> Seen { get; } = new();

    public SessionCoordinator Connect(string id, SessionCapabilities? capabilities = null)
    {
        var connection = new Connection(this, id);
        _connections[id] = connection;

        var coordinator = new SessionCoordinator(
            connection,
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            log: QuietLog.Instance,
            capabilities: capabilities ?? SessionCapabilities.Default);

        _coordinators.Add(coordinator);
        return coordinator;
    }

    public void RunUntil(Func<bool> condition)
    {
        for (var round = 0; round < 50 && !condition(); round++)
        {
            foreach (var coordinator in _coordinators)
            {
                coordinator.Tick(TimeSpan.Zero, Now);
            }

            Deliver();
        }

        Assert.True(condition(), "The session never reached the expected state.");
    }

    private void Deliver()
    {
        while (_inFlight.TryDequeue(out var sent))
        {
            Seen.Add(sent.Frame);

            if (!EnvelopeCodec.TryDecode(sent.Frame, out var envelope) || envelope is null)
            {
                continue;
            }

            var decision = _router.Route(envelope, sent.From);

            if (decision.Action == RelayAction.ReplyToSender && decision.Reply is not null)
            {
                _connections[sent.From].Receive(EnvelopeCodec.Encode(decision.Reply));
            }
            else if (decision.Action == RelayAction.Forward)
            {
                foreach (var recipient in decision.Recipients)
                {
                    _connections[recipient].Receive(sent.Frame);
                }
            }
        }
    }

    private sealed class Connection(LoopbackRelay relay, string id) : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void Connect(Uri relayAddress) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => relay._inFlight.Enqueue((id, envelope));

        public void Receive(byte[] frame) => Received?.Invoke(frame);
    }
}
```

- [ ] **Step 3: Write the failing smoke test**

`tests/DungeonMasterXIV.Relay.Tests/AJoinTravelsSealedTests.cs`:

```csharp
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A join crosses the relay with the name and participant ids sealed, and the DM admits by name.</summary>
public sealed class AJoinTravelsSealedTests
{
    [Fact]
    public void TheRelayNeverSeesTheNameOrTheParticipantIds()
    {
        var minted = Guid.NewGuid();
        var claimed = Guid.NewGuid();
        var relay = new LoopbackRelay();
        var host = relay.Connect("host", new SessionCapabilities(MintParticipant: _ => minted));
        var joiner = relay.Connect("joiner");

        host.StartHosting();
        relay.RunUntil(() => host.Host.Phase == HostingPhase.Hosting);

        joiner.RequestJoin(host.Host.Code!.Value, DisplayName.OrNone("Ysera Moonfall"), claimed);
        relay.RunUntil(() => host.Admissions.Pending.Count == 1);

        var request = host.Admissions.Pending[0];
        Assert.Equal("Ysera Moonfall", request.DisplayName.Value);

        host.Admit(request.PeerCode);
        relay.RunUntil(() => joiner.Join.Phase == JoinPhase.Admitted);

        Assert.Equal(minted, joiner.Join.ParticipantId);

        var seen = string.Join("\n", relay.Seen.Select(Encoding.UTF8.GetString));
        Assert.DoesNotContain("Ysera", seen);
        Assert.DoesNotContain(claimed.ToString("D"), seen);
        Assert.DoesNotContain(minted.ToString("D"), seen);
    }
}
```

- [ ] **Step 4: Run it and watch it fail**

Run: `dotnet build DungeonMasterXIV.sln -c Release && dotnet test tests/DungeonMasterXIV.Relay.Tests -c Release --no-build --filter AJoinTravelsSealedTests`

Expected: FAIL on `Assert.DoesNotContain("Ysera", seen)`, because today the join request carries `DisplayName` in plaintext.

- [ ] **Step 5: Add the sealed join details**

`src/DungeonMasterXIV.Core/Net/JoinDetails.cs`:

```csharp
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Net;

/// <summary>What a joiner tells the host, and what the host tells it back, sealed under their shared key.</summary>
public sealed record JoinDetails
{
    public string? DisplayName { get; init; }

    public string? ParticipantId { get; init; }
}

/// <summary>Seals join details for one message type and opens them again, refusing anything that does not authenticate.</summary>
public static class JoinDetailsCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static SealedPayload Seal(byte[] key, JoinDetails details, SessionCode code, WireMessageType type)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(details);

        return SessionCipher.Seal(
            key,
            JsonSerializer.SerializeToUtf8Bytes(details, Options),
            WireEnvelope.AssociatedDataFor(code, type));
    }

    public static JoinDetails? TryOpen(byte[] key, WireEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Nonce is null || envelope.Payload is null)
        {
            return null;
        }

        try
        {
            var plaintext = SessionCipher.Open(
                key, SealedPayload.FromWire(envelope.Nonce, envelope.Payload), envelope.AssociatedData());
            return JsonSerializer.Deserialize<JoinDetails>(plaintext, Options);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 6: Change the wire contract**

**`WireMessageType.cs`:** delete `JoinerHoldsFingerprint = 10,` and its blank line, then add after `ConnectionDropped = 11,`:

```csharp

    JoinHello = 12,

    HostKey = 13,
```

**`ProtocolVersion.cs`:** change `public const int Current = 1;` to `public const int Current = 2;`.

**`WireShape.cs`:** delete the `DisplayName`, `ClaimedParticipantId` and `ParticipantId` properties.

**`EnvelopeCodec.cs`:** in `Encode`, delete these three lines:

```csharp
            DisplayName = envelope.DisplayName,
            ClaimedParticipantId = envelope.ClaimedParticipantId,
            ParticipantId = envelope.ParticipantId,
```

**`WireEnvelope.cs`:**
- **Delete these properties:** `ClaimedParticipantId`, `ParticipantId` and `DisplayName`.
- **Delete these factories:** both `ForJoinRequest` overloads, `ForRelinkRequest` and `ForJoinerHoldsFingerprint`.
- **In `FromWire`,** delete the three lines that copy `DisplayName`, `ClaimedParticipantId` and `ParticipantId`.
- **Replace `ForJoinAccepted`** with the version below, and add the new factories after `ForCodeRefused`:

```csharp
    public static WireEnvelope ForJoinHello(SessionCode code, byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return new WireEnvelope(WireMessageType.JoinHello, code.Value) { PublicKey = publicKey };
    }

    public static WireEnvelope ForHostKey(SessionCode code, byte[] joinerPublicKey, byte[] hostPublicKey)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        ArgumentNullException.ThrowIfNull(hostPublicKey);
        return new WireEnvelope(WireMessageType.HostKey, code.Value)
        {
            PublicKey = joinerPublicKey,
            HostPublicKey = hostPublicKey,
        };
    }

    public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey, SealedPayload details)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(details);
        return new WireEnvelope(WireMessageType.JoinRequest, code.Value)
        {
            PublicKey = publicKey,
            Nonce = details.Nonce,
            Payload = details.Ciphertext,
        };
    }
```

```csharp
    public static WireEnvelope ForJoinAccepted(
        SessionCode code,
        byte[] joinerPublicKey,
        byte[] hostPublicKey,
        SealedPayload? welcome = null)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        ArgumentNullException.ThrowIfNull(hostPublicKey);
        return new WireEnvelope(WireMessageType.JoinAccepted, code.Value)
        {
            PublicKey = joinerPublicKey,
            HostPublicKey = hostPublicKey,
            Nonce = welcome?.Nonce,
            Payload = welcome?.Ciphertext,
        };
    }
```

**`WireEnvelopeReading.cs`:** delete `TryGetFingerprintReceiptKey`.

- [ ] **Step 7: Joiner side: hold the host key, send hello, then send the sealed request**

**`JoinAttempt.cs`:**
- Delete the `Fingerprint` and `FingerprintWasComparableAtDecision` properties, and every assignment to them in `Request`, `Left` and `Admitted`.
- Add `HostPublicKey = null;` to `Request` and `Left`.
- Replace `HostKeyOffered` with:

```csharp
    public byte[]? HostPublicKey { get; private set; }

    public void HostKeyOffered(byte[] hostPublicKey)
    {
        ArgumentNullException.ThrowIfNull(hostPublicKey);

        if (Phase != JoinPhase.Contacting)
        {
            return;
        }

        HostPublicKey = (byte[])hostPublicKey.Clone();
    }
```

**`OutboundHandshake.cs`:** replace the whole file with:

```csharp
using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Sends the relay registration, the join hello and the sealed join request once each is due and the link is ready.</summary>
internal sealed class OutboundHandshake
{
    private readonly RelayLink _link;
    private readonly HostSession _host;
    private readonly JoinAttempt _join;
    private readonly Func<SessionKeyExchange?> _joinerKeys;

    private string? _requestedCode;
    private string? _helloSentFor;
    private string? _requestedJoinCode;
    private DisplayName _joinDisplayName;
    private Guid? _claimedParticipantId;

    public OutboundHandshake(
        RelayLink link,
        HostSession host,
        JoinAttempt join,
        Func<SessionKeyExchange?> joinerKeys)
    {
        _link = link;
        _host = host;
        _join = join;
        _joinerKeys = joinerKeys;
    }

    public bool RegistrationWasSent => _requestedCode is not null;

    public void JoiningAs(DisplayName name, Guid? claimedParticipantId)
    {
        _joinDisplayName = name;
        _claimedParticipantId = claimedParticipantId;
    }

    public void ForgetHostRegistration() => _requestedCode = null;

    public void ForgetJoinRequest()
    {
        _helloSentFor = null;
        _requestedJoinCode = null;
    }

    public void SendWhatIsDue()
    {
        RegisterWithRelayWhenReady();
        SendHelloWhenReady();
        SendJoinRequestWhenReady();
    }

    private void RegisterWithRelayWhenReady()
    {
        if (_host.Phase != HostingPhase.Registering
            || _host.Code is not { } code
            || string.Equals(_requestedCode, code.Value, StringComparison.Ordinal)
            || !_link.IsReadyToSend)
        {
            return;
        }

        _requestedCode = code.Value;
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForCodeRequest(code)));
    }

    private void SendHelloWhenReady()
    {
        if (_join.Phase != JoinPhase.Contacting
            || _join.Code is not { } code
            || _joinerKeys() is not { } keys
            || string.Equals(_helloSentFor, code.Value, StringComparison.Ordinal)
            || !_link.IsReadyToSend)
        {
            return;
        }

        _helloSentFor = code.Value;
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForJoinHello(code, keys.PublicKey)));
    }

    private void SendJoinRequestWhenReady()
    {
        if (_join.Phase != JoinPhase.Contacting
            || _join.Code is not { } code
            || _join.HostPublicKey is not { } hostPublicKey
            || _joinerKeys() is not { } keys
            || string.Equals(_requestedJoinCode, code.Value, StringComparison.Ordinal)
            || !_link.IsReadyToSend)
        {
            return;
        }

        _requestedJoinCode = code.Value;

        byte[] key;
        try
        {
            key = keys.DeriveSharedKey(hostPublicKey, code);
        }
        catch (CryptographicException)
        {
            _join.Fail(SessionFailure.HostKeyUnusable);
            return;
        }

        var details = new JoinDetails
        {
            DisplayName = _joinDisplayName.WasStated ? _joinDisplayName.Value : null,
            ParticipantId = _claimedParticipantId?.ToString("D"),
        };

        var sealedDetails = JoinDetailsCodec.Seal(key, details, code, WireMessageType.JoinRequest);
        CryptographicOperations.ZeroMemory(key);

        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForJoinRequest(code, keys.PublicKey, sealedDetails)));
    }
}
```

**`ParticipantReceipt.cs`:** replace the whole file with:

```csharp
using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Opens the participant id sealed into a join acceptance addressed to this client.</summary>
public static class ParticipantReceipt
{
    public static Guid? TryOpen(WireEnvelope? envelope, SessionKeyExchange? keys, SessionCode? code)
    {
        if (envelope is not { Type: WireMessageType.JoinAccepted, PublicKey: { } addressee, HostPublicKey: { } hostPublicKey }
            || keys is null
            || code is not { } sessionCode
            || !CryptographicOperations.FixedTimeEquals(addressee, keys.PublicKey)
            || !SessionKeyExchange.CanAgreeWith(hostPublicKey))
        {
            return null;
        }

        var key = keys.DeriveSharedKey(hostPublicKey, sessionCode);
        var details = JoinDetailsCodec.TryOpen(key, envelope);
        CryptographicOperations.ZeroMemory(key);

        return Guid.TryParseExact(details?.ParticipantId, "D", out var id) ? id : null;
    }
}
```

- [ ] **Step 8: Route hello, host key and the sealed request inbound**

**`JoinerAdmission.cs`:** replace the record with:

```csharp
using System;

namespace DungeonMasterXIV.Net;

/// <summary>The callbacks the host uses for a joiner's hello and its sealed join request.</summary>
public readonly record struct JoinerAdmission(
    Action<byte[]>? OnHello = null,
    Action<byte[], WireEnvelope>? OnJoinRequest = null);
```

**`InboundFrame.cs`:**
1. Add `using System.Security.Cryptography;` at the top.
2. Replace the chain in `Apply` with:

```csharp
        if (TryContent(envelope, sessionKey)
            || TryJoinHello(envelope)
            || TryJoinRequest(envelope)
            || TryConnectionDropped(envelope)
            || TryHostKey(envelope)
            || TryCodeRefused(envelope)
            || TryPendingNotice(envelope))
        {
            return sessionKey;
        }
```

3. Replace `TryJoinRequest` with the two methods below.
4. Delete `TryFingerprintReceipt`.
5. Add `TryHostKey`.
6. Replace `TryPendingNotice`.
7. In `ApplyOutcome`, replace `ParticipantReceipt.TryRead(envelope, keys?.PublicKey)` with `ParticipantReceipt.TryOpen(envelope, keys, attempt.Code)`.

```csharp
    private bool TryJoinHello(WireEnvelope envelope)
    {
        if (envelope.Type != WireMessageType.JoinHello)
        {
            return false;
        }

        if (Handlers.Admission.OnHello is { } onHello
            && envelope.PublicKey is { } joinerPublicKey
            && SessionKeyExchange.CanAgreeWith(joinerPublicKey))
        {
            onHello(joinerPublicKey);
        }

        return true;
    }

    private bool TryJoinRequest(WireEnvelope envelope)
    {
        if (envelope.Type != WireMessageType.JoinRequest)
        {
            return false;
        }

        if (Handlers.Admission.OnJoinRequest is { } onJoinRequest
            && envelope.PublicKey is { } joinerPublicKey
            && SessionKeyExchange.CanAgreeWith(joinerPublicKey))
        {
            onJoinRequest(joinerPublicKey, envelope);
        }

        return true;
    }

    private bool TryHostKey(WireEnvelope envelope)
    {
        if (envelope.Type != WireMessageType.HostKey)
        {
            return false;
        }

        var attempt = Attempt;

        if (Keys is { } keys
            && envelope.PublicKey is { } addressee
            && CryptographicOperations.FixedTimeEquals(addressee, keys.PublicKey)
            && envelope.HostPublicKey is { } hostPublicKey)
        {
            if (SessionKeyExchange.CanAgreeWith(hostPublicKey))
            {
                attempt.HostKeyOffered(hostPublicKey);
            }
            else if (attempt.Phase == JoinPhase.Contacting)
            {
                attempt.Fail(SessionFailure.HostKeyUnusable);
            }
        }

        return true;
    }

    private bool TryPendingNotice(WireEnvelope envelope)
    {
        if (envelope.TryGetPendingHostKey() is null)
        {
            return false;
        }

        Attempt.AwaitDecision(envelope.TryGetDeadline());
        return true;
    }
```

**`InboundWiring.cs`:** replace the `Admission:` argument with:

```csharp
            Admission: new JoinerAdmission(
                OnHello: admissions.OfferHostKey,
                OnJoinRequest: (key, envelope) =>
                {
                    if (admissions.OpenJoinRequest(key, envelope) is { } details)
                    {
                        admissions.AdmitToTheQueue(
                            key, now, DisplayName.OrNone(details.DisplayName), resolveRelink(details.ParticipantId));
                    }
                }),
```

- [ ] **Step 9: Host side: answer hello, open the request, and seal the welcome**

**`AdmissionAnnouncer.cs`:** add the method below, and change `Accepted` so its last parameter is `SealedPayload? welcome = null`, passed as `ForJoinAccepted(code, joinerPublicKey, hostPublicKey, welcome)`.

```csharp
    public void HostKey(SessionCode code, byte[] joinerPublicKey, byte[] hostPublicKey) =>
        Send(WireEnvelope.ForHostKey(code, joinerPublicKey, hostPublicKey));
```

**`PendingAdmission.cs`:**
- Remove the `fingerprint` constructor parameter and the `Fingerprint` property.
- Remove `FingerprintConfirmed`, `Comparability`, `JoinerReportedItCanCompare`, `Verification` and `ConfirmFingerprintMatched`.
- Change the summary to `/// <summary>A join request awaiting the host's answer, with its deadline, relink claim and name.</summary>`.

**`AdmissionPrompt.cs`:** keep only `Favoured` and `Headline`. Delete `OffersConfirmation`, `ComparabilityNote` and both constants, and change the summary to `/// <summary>Supplies the headline and favoured choice the host sees for a join request.</summary>`.

**`AdmittedPeer.cs`:**
- Remove the `verification` constructor parameter and the `Verification` property.
- Change the summary to `/// <summary>A peer the host has admitted, with its peer code, role, public key and display name.</summary>`.

**`SessionAudience.cs`:**
- Change `Admit` to `public AdmittedPeer Admit(PeerCode peerCode, SessionRole role = SessionRole.Player, byte[]? publicKey = null, DisplayName displayName = default)` and construct with `new AdmittedPeer(peerCode, role, publicKey, displayName)`.
- Delete `ConfirmedCount`.

**`AdmissionControl.cs`:**
- Add `using System.Security.Cryptography;`.
- In `Receive`, build the request with `new PendingAdmission(peerCode, deadline, relink, joinerPublicKey, displayName)`.
- Delete `RecordComparabilityReceipt`.
- Replace `Admit` and add the methods below:

```csharp
    public void OfferHostKey(byte[] joinerPublicKey)
    {
        if (_hostCode() is { } code && _hostKeys() is { } hostKeys)
        {
            _announcer.HostKey(code, joinerPublicKey, hostKeys.PublicKey);
        }
    }

    public JoinDetails? OpenJoinRequest(byte[] joinerPublicKey, WireEnvelope envelope)
    {
        if (_hostCode() is not { } code || _hostKeys() is not { } hostKeys)
        {
            return null;
        }

        byte[] key;
        try
        {
            key = hostKeys.DeriveSharedKey(joinerPublicKey, code);
        }
        catch (CryptographicException)
        {
            return null;
        }

        var details = JoinDetailsCodec.TryOpen(key, envelope);
        CryptographicOperations.ZeroMemory(key);

        if (details is null)
        {
            _log.Warning(
                "A join request arrived that could not be opened with the key agreed for it, so it was "
                + "discarded. A joiner on a different build and something altering the request on the "
                + "way look the same from here.");
        }

        return details;
    }

    public AdmittedPeer Admit(PeerCode peerCode, SessionRole role = SessionRole.Player)
    {
        var request = Desk.Decide(peerCode);
        var displayName = request?.DisplayName ?? DisplayName.None;
        var participantId = _mintParticipant(displayName);

        var peer = Audience.Admit(peerCode, role, request?.JoinerPublicKey, displayName);

        Drops.Forget(peerCode);
        AnnounceAccepted(request?.JoinerPublicKey, participantId);

        if (participantId is null)
        {
            _log.Warning(
                $"Admitted {peerCode} without creating a participant, so this session has no "
                + "campaign to record them in. They will not be able to relink and will be a new "
                + "request next time.");
        }

        return peer;
    }

    private void AnnounceAccepted(byte[]? joinerPublicKey, Guid? participantId)
    {
        if (_hostCode() is not { } code || _hostKeys() is not { } hostKeys || joinerPublicKey is null)
        {
            return;
        }

        SealedPayload? welcome = null;
        if (participantId is { } id)
        {
            var key = hostKeys.DeriveSharedKey(joinerPublicKey, code);
            welcome = JoinDetailsCodec.Seal(
                key, new JoinDetails { ParticipantId = id.ToString("D") }, code, WireMessageType.JoinAccepted);
            CryptographicOperations.ZeroMemory(key);
        }

        _announcer.Accepted(code, joinerPublicKey, hostKeys.PublicKey, welcome);
    }
```

**Delete** `KeyFingerprint.cs`, `AdmissionVerification.cs` and `ComparabilityEvidence.cs`.

- [ ] **Step 10: Relay routing for hello, host key and the sealed request**

**`RelayDecision.cs`:** add after `PendingNoticeForwarded = 15,`:

```csharp

    HostKeyForwarded = 16,
```

**`RelayRouter.cs`:**
1. Replace the `JoinRequest` arm and the `JoinerHoldsFingerprint` arm in the `switch`. The `JoinPending` arm now calls `RouteToPendingJoiner(envelope, code, senderConnectionId, RelayOutcome.PendingNoticeForwarded)`.
2. Rename the old `RouteJoinRequest` to `RouteJoinHello`, keeping its body.
3. Add the new `RouteJoinRequest`.
4. Rename `RouteJoinPending` to `RouteToPendingJoiner` with an extra `RelayOutcome forwarded` parameter, used in its success branch.
5. Delete `RouteFingerprintReceipt`.

The new arms:

```csharp
            WireMessageType.JoinHello => RouteJoinHello(code, senderConnectionId, envelope.PublicKey),
            WireMessageType.JoinRequest => RouteJoinRequest(code, senderConnectionId, envelope.PublicKey),
            WireMessageType.HostKey =>
                RouteToPendingJoiner(envelope, code, senderConnectionId, RelayOutcome.HostKeyForwarded),
```

The new `RouteJoinRequest`:

```csharp
    private RelayDecision RouteJoinRequest(SessionCode code, string joinerConnectionId, byte[]? envelopePublicKey)
    {
        if (!_registry.TryGetHost(code.Value, out var hostConnectionId))
        {
            return RelayDecision.Respond(RelayOutcome.SessionNotFound, WireEnvelope.ForCodeRefused(code));
        }

        if (envelopePublicKey is null)
        {
            return RelayDecision.Drop(RelayOutcome.MalformedEnvelope);
        }

        if (!_registry.TryGetPending(code.Value, envelopePublicKey, out var waiting)
            || !string.Equals(waiting, joinerConnectionId, StringComparison.Ordinal))
        {
            return RelayDecision.Drop(RelayOutcome.UnknownJoiner);
        }

        return RelayDecision.Forward(RelayOutcome.JoinForwardedToHost, [hostConnectionId]);
    }
```

**`RelayRouterTests.cs`:** change `WireEnvelope.ForJoinRequest(Code, [1, 2, 3])` to `WireEnvelope.ForJoinHello(Code, [1, 2, 3])`.

- [ ] **Step 11: Remove the fingerprint from the windows**

**Delete** `Windows/JoinComparisonView.cs`.

**`Windows/JoinFlowView.cs`:**
- Delete the `_comparison` field and the `_comparison.Draw(join);` line.
- Change the summary to `/// <summary>Draws the joiner's side of the session window: status, roster, leaving and request form.</summary>`.

**`Windows/SessionWindow.cs`:** delete the `if (audience.Count > audience.ConfirmedCount) { ... }` block.

**`Windows/AdmissionPromptView.cs`:**
- Delete the three constants and the `ImGui.TextWrapped(AdmissionDisclosure);` line.
- Inside the loop, keep only the headline, the lapse line and the Admit/Deny buttons:

```csharp
        foreach (var request in pending.ToArray())
        {
            ImGui.Separator();
            ImGui.TextUnformatted(AdmissionPrompt.Headline(request));

            var remaining = request.RemainingAt(now);
            ImGui.TextUnformatted($"This request lapses in {remaining:mm\\:ss}");

            if (ImGui.Button($"Admit##{request.PeerCode}"))
            {
                _coordinator.Admit(request.PeerCode);
            }

            if (AdmissionPrompt.Favoured(request) == AdmissionAction.Admit)
            {
                ImGui.SetItemDefaultFocus();
            }

            ImGui.SameLine();
            if (ImGui.Button($"Deny##{request.PeerCode}"))
            {
                _coordinator.Deny(request.PeerCode);
            }
        }
```

Change its summary to `/// <summary>Shows the host each pending join request with Admit and Deny buttons.</summary>`.

- [ ] **Step 12: Build and run every test**

Run: `dotnet build DungeonMasterXIV.sln -c Release && dotnet test DungeonMasterXIV.sln -c Release --no-build`

Expected:
- 0 build errors.
- Relay.Tests Total 2, Release.Tests Total 1, Tests Total 5, all with Skipped 0.
- `AJoinTravelsSealedTests` passes.

If the build lists any other reference to a removed member, remove that usage the same way and rebuild.

- [ ] **Step 13: Confirm nothing still names the fingerprint**

Run: `grep -rn -E "Fingerprint|Comparability|AdmissionVerification|ConfirmedCount" --include='*.cs' src Windows Plugin.cs tests | grep -v "/bin/\|/obj/"`

Expected: no output.

- [ ] **Step 14: Commit**

```bash
git add -A src Windows tests
git commit -F - <<'EOF'
feat: exchange keys before the join request and seal it

The joiner sends a hello, the host answers with its key, and the join
request (name and any claimed participant id) and the acceptance (the
told participant id) travel sealed. The fingerprint comparison is gone.
Protocol version 2; v1 plugins are refused at connect.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 2: Returning players setting

**Files:**
- Create: `tests/DungeonMasterXIV.Relay.Tests/AReturningPlayerIsLetStraightInTests.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/RelinkClaim.cs`
- Modify: `src/DungeonMasterXIV.Core/Campaigns/CampaignRelink.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/AdmittedPeer.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionAudience.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionCapabilities.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionWiring.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/AdmissionControl.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/InboundWiring.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/SessionCoordinator.cs`
- Modify: `src/DungeonMasterXIV.Core/Campaigns/Campaign.cs`
- Modify: `src/DungeonMasterXIV.Core/Campaigns/HostingCampaign.cs`
- Modify: `Plugin.cs`
- Modify: `Windows/SessionWindow.cs`
- Modify: `Windows/AdmissionPromptView.cs`

**Interfaces:**
- **Consumes:** Task 1's `AdmissionControl.OpenJoinRequest`, `SessionAudience.Admit(PeerCode, SessionRole, byte[]?, DisplayName)` and the `LoopbackRelay` helper.
- **Produces:**
  - `RelinkClaim(bool Matched, string? Label, Guid? ParticipantId = null)`
  - `SessionCapabilities.LetReturningPlayersIn` (`Func<bool>?`) and `ReturningPlayersSource`
  - `AdmissionControl.LetsInAutomatically(PendingAdmission) -> bool`
  - `AdmissionControl.Admit(PeerCode, SessionRole = Player, bool asClaimed = false)` and `SessionCoordinator.Admit(PeerCode, SessionRole = Player, bool asClaimed = false)`
  - `AdmissionControl.AdmitToTheQueue(...) -> PendingAdmission?`
  - `SessionAudience.HoldsParticipant(Guid) -> bool` and `AdmittedPeer.ParticipantId` (`Guid?`)
  - `Campaign.LetReturningPlayersIn` (`bool`)
  - `HostingCampaign.LetsReturningPlayersIn` (`bool`) and `HostingCampaign.SetReturningPlayers(bool letIn)`

- [ ] **Step 1: Write the failing smoke test**

`tests/DungeonMasterXIV.Relay.Tests/AReturningPlayerIsLetStraightInTests.cs`:

```csharp
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>With the DM's say-so, a returning player is admitted as their entry while a newcomer is still prompted.</summary>
public sealed class AReturningPlayerIsLetStraightInTests
{
    [Fact]
    public void AReturningPlayerSkipsThePromptAndANewcomerDoesNot()
    {
        var known = Guid.NewGuid();
        var relay = new LoopbackRelay();
        var host = relay.Connect("host", new SessionCapabilities(
            ResolveRelink: claimed => claimed == known.ToString("D")
                ? new RelinkClaim(true, "Ysera", known)
                : RelinkClaim.None,
            LetReturningPlayersIn: () => true));
        var returning = relay.Connect("returning");
        var newcomer = relay.Connect("newcomer");

        host.StartHosting();
        relay.RunUntil(() => host.Host.Phase == HostingPhase.Hosting);

        var code = host.Host.Code!.Value;
        returning.RequestJoin(code, DisplayName.OrNone("Ysera"), known);
        newcomer.RequestJoin(code, DisplayName.OrNone("Tuka"), null);
        relay.RunUntil(() => returning.Join.Phase == JoinPhase.Admitted && host.Admissions.Pending.Count == 1);

        Assert.Equal(known, returning.Join.ParticipantId);
        Assert.Equal("Tuka", host.Admissions.Pending[0].DisplayName.Value);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet build DungeonMasterXIV.sln -c Release`

Expected: build FAIL. `RelinkClaim` has no three-argument constructor, and `SessionCapabilities` has no `LetReturningPlayersIn`.

- [ ] **Step 3: Carry the claimed participant id and track seats**

**`RelinkClaim.cs`:**

```csharp
using System;

namespace DungeonMasterXIV.Net;

/// <summary>Whether a join request matched a known campaign participant, with that participant's label and id.</summary>
public readonly record struct RelinkClaim(bool Matched, string? Label, Guid? ParticipantId = null)
{
    public static RelinkClaim None => default;
}
```

**`CampaignRelink.cs`:** return `new RelinkClaim(true, participant.Label, participant.ParticipantId)`.

**`AdmittedPeer.cs`:** add a constructor parameter `Guid? participantId = null` after `displayName`, store it, and expose it:

```csharp
    public Guid? ParticipantId { get; }
```

Add `using System;`, and change the summary to `/// <summary>A peer the host has admitted, with its peer code, role, public key, display name and participant id.</summary>`.

**`SessionAudience.cs`:** give `Admit` a final `Guid? participantId = null` parameter, passed to `new AdmittedPeer(peerCode, role, publicKey, displayName, participantId)`. Add `using System;` and:

```csharp
    public bool HoldsParticipant(Guid participantId) =>
        _admitted.Any(peer => peer.ParticipantId == participantId);
```

- [ ] **Step 4: Add the capability and the automatic admission**

**`SessionCapabilities.cs`:** add the parameter `Func<bool>? LetReturningPlayersIn = null` after `ResolveRelink`, and:

```csharp
    public Func<bool> ReturningPlayersSource => LetReturningPlayersIn ?? (static () => false);
```

**`AdmissionControl.cs`:**
- Add a field `private readonly Func<bool> _letReturningPlayersIn;`.
- Add a constructor parameter `Func<bool> letReturningPlayersIn` after `mintParticipant`, assigned after a `ThrowIfNull`.
- Change `AdmitToTheQueue` to return `PendingAdmission?`: `public PendingAdmission? AdmitToTheQueue(...) => Receive(...);`.
- Replace `Admit` and add the helpers:

```csharp
    public bool LetsInAutomatically(PendingAdmission request) =>
        _letReturningPlayersIn() && ClaimedAndFree(request) is not null;

    public AdmittedPeer Admit(PeerCode peerCode, SessionRole role = SessionRole.Player, bool asClaimed = false)
    {
        var request = Desk.Decide(peerCode);
        var displayName = request?.DisplayName ?? DisplayName.None;
        var participantId = asClaimed && ClaimedAndFree(request) is { } claimed
            ? claimed
            : _mintParticipant(displayName);

        var peer = Audience.Admit(peerCode, role, request?.JoinerPublicKey, displayName, participantId);

        Drops.Forget(peerCode);
        AnnounceAccepted(request?.JoinerPublicKey, participantId);

        if (participantId is null)
        {
            _log.Warning(
                $"Admitted {peerCode} without creating a participant, so this session has no "
                + "campaign to record them in. They will not be able to relink and will be a new "
                + "request next time.");
        }

        return peer;
    }

    private Guid? ClaimedAndFree(PendingAdmission? request) =>
        request?.Relink is { Matched: true, ParticipantId: { } claimed } && !Audience.HoldsParticipant(claimed)
            ? claimed
            : null;
```

**`SessionWiring.cs`:** pass `capabilities.ReturningPlayersSource` as the new argument after `capabilities.ParticipantSource`.

**`SessionCoordinator.cs`:** change `Admit` to:

```csharp
    public AdmittedPeer Admit(PeerCode peerCode, SessionRole role = SessionRole.Player, bool asClaimed = false)
    {
        var peer = _admissions.Admit(peerCode, role, asClaimed);

        _roster.Publish();
        return peer;
    }
```

**`InboundWiring.cs`:** replace the `OnJoinRequest` lambda with:

```csharp
                OnJoinRequest: (key, envelope) =>
                {
                    if (admissions.OpenJoinRequest(key, envelope) is not { } details)
                    {
                        return;
                    }

                    var request = admissions.AdmitToTheQueue(
                        key, now, DisplayName.OrNone(details.DisplayName), resolveRelink(details.ParticipantId));

                    if (request is not null && admissions.LetsInAutomatically(request))
                    {
                        admissions.Admit(request.PeerCode, asClaimed: true);
                        roster.Publish();
                    }
                }),
```

- [ ] **Step 5: Run the smoke test**

Run: `dotnet build DungeonMasterXIV.sln -c Release && dotnet test tests/DungeonMasterXIV.Relay.Tests -c Release --no-build`

Expected: Total 3, Skipped 0, all passed.

- [ ] **Step 6: Store the setting on the campaign and show it to the DM**

**`Campaign.cs`:** add after `DisplayNameAlias`:

```csharp
    public bool LetReturningPlayersIn { get; set; }
```

**`HostingCampaign.cs`:** add:

```csharp
    public bool LetsReturningPlayersIn => Current?.LetReturningPlayersIn == true;

    public void SetReturningPlayers(bool letIn)
    {
        if (Current is not { } campaign || campaign.LetReturningPlayersIn == letIn)
        {
            return;
        }

        campaign.LetReturningPlayersIn = letIn;
        _store.Save(campaign);
    }
```

**`Plugin.cs`:** in the `SessionCapabilities` constructor call, add after `ResolveRelink: ...`:

```csharp
                LetReturningPlayersIn: () => _hostingCampaign.LetsReturningPlayersIn
```

Add a comma after the `ResolveRelink` argument.

**`Windows/SessionWindow.cs`:** directly after the `RosterView.Draw(...)` line in `DrawHosting`, add:

```csharp
            ImGui.TextUnformatted("Returning players");
            var letIn = _hosting.LetsReturningPlayersIn;
            if (ImGui.RadioButton("Ask me each time", !letIn))
            {
                _hosting.SetReturningPlayers(false);
            }

            ImGui.SameLine();
            if (ImGui.RadioButton("Let them straight in", letIn))
            {
                _hosting.SetReturningPlayers(true);
            }
```

**`Windows/AdmissionPromptView.cs`:** replace the single Admit button with:

```csharp
            if (request.IsRelink)
            {
                if (ImGui.Button($"Admit as {request.RelinkLabel}##{request.PeerCode}"))
                {
                    _coordinator.Admit(request.PeerCode, asClaimed: true);
                }

                ImGui.SameLine();
                if (ImGui.Button($"Admit as a new player##{request.PeerCode}"))
                {
                    _coordinator.Admit(request.PeerCode);
                }
            }
            else if (ImGui.Button($"Admit##{request.PeerCode}"))
            {
                _coordinator.Admit(request.PeerCode);
            }
```

- [ ] **Step 7: Build and run every test**

Run: `dotnet build DungeonMasterXIV.sln -c Release && dotnet test DungeonMasterXIV.sln -c Release --no-build`

Expected:
- 0 build errors.
- Relay.Tests Total 3, Release.Tests Total 1, Tests Total 5, all with Skipped 0.

- [ ] **Step 8: Commit**

```bash
git add -A src Windows tests Plugin.cs
git commit -F - <<'EOF'
feat: let a campaign's returning players straight in

A per-campaign "Returning players" setting, Ask me each time by default.
With Let them straight in, a joiner whose sealed request claims a stored
participant not already seated is admitted as that participant without a
prompt. The prompt can admit a claimed joiner as their entry or as new.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 3: Settings layout, policy link, and no security text

**Files:**
- Create: `Windows/CampaignStorageView.cs`
- Modify: `Windows/ConfigWindow.cs`
- Modify: `Windows/RelinkMemoryView.cs`
- Modify: `Windows/SessionWindow.cs`
- Modify: `Windows/HostCampaignPicker.cs`
- Modify: `src/DungeonMasterXIV.Core/Data/RelinkMemory.cs`
- Modify: `src/DungeonMasterXIV.Core/Data/RelinkDisclosure.cs`
- Modify: `Plugin.cs`
- Modify: `DungeonMasterXIV.json`
- Delete: `Windows/CampaignListWindow.cs`

**Interfaces:**
- **Consumes:** nothing from Tasks 1 and 2 beyond a compiling tree.
- **Produces:**
  - `CampaignStorageView(CampaignStore store, CampaignDeletion deletion)` with `Draw()`
  - `RememberedParticipant.StoredUtc` (`DateTimeOffset?`)
  - `ConfigWindow(ConfigurationStore, Func<DisplayName>, Func<Campaign?>, Action<Campaign>, CampaignStorageView)`

This task is UI and copy. It adds no smoke test, because ImGui drawing is not reachable from the test projects. Verification is the build, the existing suite, a string check, and a look in game.

- [ ] **Step 1: Record when a participant id was stored**

**`RelinkMemory.cs`:** add to `RememberedParticipant`:

```csharp
    public DateTimeOffset? StoredUtc { get; set; }
```

In `Remember`, set `existing.StoredUtc = DateTimeOffset.UtcNow;` where `existing.ParticipantId` is updated. Add `StoredUtc = DateTimeOffset.UtcNow,` to the new `RememberedParticipant` initialiser.

- [ ] **Step 2: Move the campaign list into a settings section**

Create `Windows/CampaignStorageView.cs` with the body of `CampaignListWindow`, with these changes:
- It is no longer a `Window`.
- It has no intro paragraph.
- Its constructor takes `(CampaignStore store, CampaignDeletion deletion)`.

```csharp
using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;

namespace DungeonMasterXIV.Windows;

/// <summary>Lists stored campaigns and unreadable campaign files in settings, each with a delete that asks to confirm.</summary>
public sealed class CampaignStorageView
{
    private readonly CampaignStore _store;

    private readonly DeletionPrompt _prompt;

    private IReadOnlyList<CampaignRow> _rows = Array.Empty<CampaignRow>();
    private IReadOnlyList<UnreadableRow> _unreadable = Array.Empty<UnreadableRow>();
    private int _rowsBuiltAtRevision = -1;

    public CampaignStorageView(CampaignStore store, CampaignDeletion deletion)
    {
        _store = store;
        _prompt = new DeletionPrompt(id => deletion.Delete(id), name => _store.DeleteUnreadable(name));
    }

    public void Draw()
    {
        RefreshRowsIfStale();

        if (_rows.Count == 0)
        {
            ImGui.TextDisabled("No campaigns stored yet.");
        }

        foreach (var row in _rows)
        {
            DrawRow(row);
        }

        DrawUnreadable();
    }
}
```

Copy `RefreshRowsIfStale`, `DrawUnreadable`, `DrawRow` and `DrawConfirmation` from `CampaignListWindow.cs` unchanged, below `Draw`. Then delete `Windows/CampaignListWindow.cs`.

- [ ] **Step 3: Rebuild settings around the two storage sections and the link**

**`Windows/ConfigWindow.cs`:**
- Change the summary to `/// <summary>The settings window: window restore, display name, relay address, the relay policy link, and campaign and user storage.</summary>`.
- Delete `WhatThisPluginKnows`, `NameIsNotIdentity`, `DrawWhatThisPluginKnows` and the `ImGui.TextWrapped(NameIsNotIdentity);` line.
- Replace `UnusableAliasWarning` with:

```csharp
    private static readonly string UnusableAliasWarning =
        $"This name cannot be used, so your character name will be sent instead. Names are limited "
        + $"to {DisplayName.MaxLength} characters and cannot contain line breaks or invisible "
        + "formatting characters.";

    private const string RelayPolicyUrl =
        "https://github.com/ruminabottle/dungeonmasterxiv/blob/main/RELAY-SERVICE-POLICY.md";
```

- Add a field `private readonly CampaignStorageView _campaignStorage;`.
- Add a constructor parameter `CampaignStorageView campaignStorage`, last, assigned to it.
- Replace the part of `Draw` after `DrawRelaySetting(settings);` with:

```csharp
        if (ImGui.Button("Relay and privacy"))
        {
            Dalamud.Utility.Util.OpenLink(RelayPolicyUrl);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Campaign storage");
        _campaignStorage.Draw();

        ImGui.Separator();
        ImGui.TextUnformatted("User storage");
        _relinkMemory.Draw();

        ImGui.Separator();
        ImGui.TextDisabled(_schemaVersionLabel);
```

**`Windows/RelinkMemoryView.cs`:**
- Delete `ImGui.TextWrapped(RelinkDisclosure.WhatIsStored);`.
- After the `Participant` line, add:

```csharp
            ImGui.TextUnformatted(entry.StoredUtc is { } stored
                ? $"Stored {stored.ToLocalTime():d}"
                : "Stored before dates were kept");
```

**`src/DungeonMasterXIV.Core/Data/RelinkDisclosure.cs`:**
- Delete `WhatIsStored`.
- Change the summary to `/// <summary>The wording that confirms forgetting a stored participant id.</summary>`.

- [ ] **Step 4: Remove the remaining in-plugin security text**

**`Windows/SessionWindow.cs`:** delete `CodeDisclosure` and the `ImGui.TextWrapped(CodeDisclosure);` line.

**`Windows/HostCampaignPicker.cs`:** delete `ResumeDisclosure` and the `ImGui.TextWrapped(ResumeDisclosure);` line.

- [ ] **Step 5: Wire settings and drop the campaigns window and command**

**`Plugin.cs`:**
- Delete:
  - `CampaignsCommandName`
  - the `_campaignListWindow` field and its assignment
  - `CampaignListWindowFor`
  - the window's `AddWindow`/`Push` pair
  - the `/dmxcampaigns` `AddHandler`/`Push` pair
  - `OnCampaignsCommand`
- `SettingsWindowFor` needs the config directory. Change the call site to `_configWindow = SettingsWindowFor(characterName, pluginInterface.ConfigDirectory);` and the method to:

```csharp
    private ConfigWindow SettingsWindowFor(Func<DisplayName> characterName, DirectoryInfo configDirectory)
    {
        var retainedLogs = new RetainedLogStore(
            new RetainedLogFileArchive(Path.Combine(configDirectory.FullName, "logs")));

        return new ConfigWindow(
            _configurationStore,
            characterName,
            () => _hostingCampaign.Current,
            _campaignStore.Save,
            new CampaignStorageView(_campaignStore, new CampaignDeletion(_campaignStore, retainedLogs)));
    }
```

**`DungeonMasterXIV.json`:** append this to the end of the `Description` string, before its closing quote:

```
\n\nWhat the relay can see and what it stores: https://github.com/ruminabottle/dungeonmasterxiv/blob/main/RELAY-SERVICE-POLICY.md
```

- [ ] **Step 6: Build, test and check the strings**

Run: `dotnet build DungeonMasterXIV.sln -c Release && dotnet test DungeonMasterXIV.sln -c Release --no-build`

Expected:
- 0 build errors.
- Relay.Tests Total 3, Release.Tests Total 1, Tests Total 5, all with Skipped 0.

Run: `grep -rn -i -E "tamper|in the middle|to compare|not a secret|end to end|relay can|not proof|anonymous|verified|dmxcampaigns" --include='*.cs' Windows Plugin.cs src | grep -v "/bin/\|/obj/"`

Expected: no output.

- [ ] **Step 7: Look at it in game**

Load the dev plugin and open `/dmx settings`. Check each of these:
- The order is restore, display name, relay address, then a "Relay and privacy" button.
- The "Relay and privacy" button opens the policy page in the browser.
- "Campaign storage" lists campaigns, each with a Delete that asks to confirm.
- "User storage" lists stored entries, each with its code, "Stored <date>" and a Forget that asks to confirm.
- No paragraph explains security.

Open the session window and start hosting. Check each of these:
- There is no "not a secret" line.
- The "Returning players" radios are present and switch.

- [ ] **Step 8: Commit**

```bash
git add -A Windows src Plugin.cs DungeonMasterXIV.json
git commit -F - <<'EOF'
feat: storage sections and a policy link in settings, no security text

Settings gains Campaign storage (moved from its own window and command)
and User storage (now showing when each id was stored), plus a link to
the relay policy. Every in-plugin security explanation is removed.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 4: Relay policy and release

**Files:**
- Modify: `RELAY-SERVICE-POLICY.md`

**Interfaces:**
- **Consumes:** the shipped behaviour of Tasks 1 to 3.
- **Produces:** the public statement of session-layer R-1.9.

The policy's own "Changes" section says that a change which reduces what is promised is announced in the repository before it takes effect. Removing the code comparison is such a change. So this commit merges with the code, and the release notes say it.

- [ ] **Step 1: Correct "What it stores"**

Replace the two paragraphs beginning "This is not a promise you have to take on faith." and "The test is checked by deliberately breaking the relay" with:

```markdown
You can check this rather than take it on trust: the relay's source is public, and it writes only to
standard output. It has no file of its own.
```

In the "What it can see" section, replace "which is exactly what makes the "stores nothing" test above possible" with "which is what makes "stores nothing" checkable in the source".

- [ ] **Step 2: Rewrite "What it cannot read"**

Replace everything under `## What it cannot read`, up to the next heading, with:

```markdown
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
```

- [ ] **Step 3: Remove the name from "What it can see"**

Delete everything from "**And it sees your character name when you ask to join a session.**" through "...than describe a control you do not have." The first paragraph of the section (connection, address, when, how much, which session code) stays as it is.

- [ ] **Step 4: Update the trust model line**

In "What we will do to keep it running", replace "made through the accept/deny prompt, and it is the entire trust model." with "made through the accept/deny prompt or by letting that campaign's returning players straight in, and it is the entire trust model."

- [ ] **Step 5: Check the page against session-layer R-1.9**

Run: `grep -n -i "character name\|compare\|test" RELAY-SERVICE-POLICY.md`

Expected:
- No line claims the relay sees your character name.
- No line describes a disk-watching test.
- The only mention of comparing is the "Earlier versions" sentence.
- The "What it can see" list is exactly: connection, network address, when, how much and how often, and which session code.

- [ ] **Step 6: Commit and open the PR**

```bash
git add RELAY-SERVICE-POLICY.md
git commit -F - <<'EOF'
docs: relay policy for sealed joins and no code comparison

The joining name is sealed now, nobody compares keys and the residual
risk is stated, and the claim about a disk-watching relay test is gone
because that test no longer exists.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
git push -u origin feat/connection-redesign
```

Open the PR. Its body summarises the four commits and must include this release note, to carry into the GitHub release:

> **Everyone needs this version.** The join protocol changed (protocol version 2), so older plugins are refused by the relay with an "update your plugin" message. Joining no longer asks you to compare a code. Your name now travels encrypted. DMs can let a campaign's returning players straight in. Settings now holds campaign and user storage and a link to the relay policy.

End the PR body with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

- [ ] **Step 7: Deploy order at release**

Deploy the v2 relay and publish the v2 plugin together. Between the two, every client on the other version is refused at connect, which is the designed behaviour.
