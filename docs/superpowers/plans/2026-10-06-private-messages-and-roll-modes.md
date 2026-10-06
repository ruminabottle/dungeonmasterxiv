# Private Messages and Roll Modes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a player send a message or roll to the DM (or roll blind), let the DM send one to a
single player, and let the DM reveal a hidden roll. Nobody else's client receives private content.

**Architecture:** Sends carry an optional audience. The host settles it into a private entry with
the seats entitled to it, records it, and sends every member their own view of the record: private
messages dropped, private rolls swapped for a placeholder, numbered 1, 2, 3… for that member alone.
A reveal marks the entry and re-sends its line at each member's own number, which clients already
overwrite in place. The Chat tab gets an icon row that picks the audience.

**Tech Stack:** C# / .NET 10, Dalamud ImGui bindings, System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-06-private-messages-and-roll-modes-design.md`

## Global Constraints

- The wire format only grows (product-overview D-14): new optional fields only, nothing renamed or
  removed, no protocol version bump.
- A missing audience means Public.
- Copy never calls a blind roll secret, private, hidden or secure (rolls R-2.15). Placeholders never
  say private, secret or hidden.
- Private content never reaches FFXIV's chat log. Nothing writes to it today (no `IChatGui` use);
  this plan adds none.
- Smoke tests only: at most one end-to-end test for the feature, no per-case tests.
- Code style: every new type has one `/// <summary>` line; no requirement IDs in code or strings;
  match the surrounding file's idiom.
- Build: `dotnet build` from the repository root. Tests: `dotnet test` and check the `Total:` and
  `Skipped:` counts, not only "Passed!".

## Review Focus

1. **A whisper to a player who is not running the new plugin.** Expect the host to refuse it with
   "Eli's plugin needs updating for private messages", not deliver it as an ordinary line. Task 2's
   test asserts the refusal.
2. **The DM's own DM-only roll.** Expect members to get the placeholder "rolled" with no roll data.
   Task 2's test asserts it.
3. **A member who resumes after a reveal.** Expect the revealed roll to be re-sent so their
   placeholder fills in. Task 2 implements `MissedBy`; checked in game (Task 3, step 9).
4. **The whisper target leaves while To ▾ names them.** Expect Send disabled with "Eli is no longer
   in the session", never a silent switch of audience. Task 3 implements it; checked in game.
5. **A new session starts while DM only or Blind was chosen.** Expect the row back on Public.
   Task 3 implements it; checked in game.

---

## File structure

**Core (`src/DungeonMasterXIV.Core/Net/`)**

| File | Change | Responsibility |
| --- | --- | --- |
| `MessageAudience.cs` | create | `AudienceKind` and the audience a sender asks for |
| `EntryPrivacy.cs` | create | a recorded entry's audience, entitled seats, placeholder text, reveal |
| `SeatKey.cs` | create | the identity entitlement is kept under |
| `AudienceRules.cs` | create | settles a requested audience into `EntryPrivacy`, or refuses it |
| `MemberView.cs` | create | builds one member's numbered view of the record |
| `SessionContent.cs` | modify | `Audience` on sends, `Audiences` flag from the host |
| `SessionContentCodec.cs` | modify | keeps the two new fields when vetting |
| `JoinDetails.cs` | modify | `Audiences` flag from members |
| `StreamEntry.cs`, `StreamLine.cs` | modify | privacy on entries; audience fields on lines |
| `SessionStream.cs`, `SessionRecording.cs` | modify | record privacy; replace an entry on reveal |
| `HostStream.cs` | modify | per-member fan-out, reveal, resume lines |
| `RosterBroadcast.cs` | modify | announce support with the roster; drop `PublishEntry` |
| `SessionAudience.cs`, `PendingAdmission.cs`, `AdmissionControl.cs` | modify | remember which members announced support |
| `OutboundHandshake.cs` | modify | members announce support on join and resume |
| `InboundWiring.cs` | modify | enforce audiences on member sends; resume from views |
| `MemberMessage.cs`, `SessionMembership.cs` | modify | member sends carry an audience |
| `SessionWiring.cs`, `SessionCoordinator.cs` | modify | wiring; `CanAddress`, audience on `Say`/`ShareRoll`, `Reveal` |
| `src/DungeonMasterXIV.Core/Chat/MessageFault.cs` | modify | `AudienceUnavailable` |

**Plugin (`Windows/`)**

| File | Change | Responsibility |
| --- | --- | --- |
| `AudienceChoice.cs` | create | the chosen audience, shared by Chat and Session tabs |
| `Ui/Components/AudienceRow.cs` | create | the icon row and the To ▾ popup |
| `Ui/Components/AudienceMark.cs` | create | the audience line drawn under a card's speaker |
| `Ui/Components/PrivateCard.cs` | create | the placeholder card |
| `Ui/Components/MessageCard.cs`, `RollCard.cs` | modify | private surface, audience line, "?" total, reveal |
| `MessageComposeView.cs`, `ChatTab.cs`, `StreamView.cs`, `SessionTab.cs` | modify | wire it in |
| `Plugin.cs` | modify | create one `AudienceChoice` and pass it to both tabs |

**Tests:** `tests/DungeonMasterXIV.Tests/APrivateRollLeavesOthersAPlaceholderTests.cs` (create).

---

### Task 1: Audience on the wire, and both sides announce support

Adds the types and optional fields, makes the host and members announce support, and lets members
send with an audience. Nothing is private yet: the host still records every send as public, because
Task 2 adds the rules. The existing tests must stay green.

**Files:**
- Create: `src/DungeonMasterXIV.Core/Net/MessageAudience.cs`, `EntryPrivacy.cs`, `SeatKey.cs`
- Modify: `SessionContent.cs`, `SessionContentCodec.cs`, `JoinDetails.cs`, `StreamEntry.cs`,
  `StreamLine.cs`, `SessionAudience.cs`, `PendingAdmission.cs`, `AdmissionControl.cs`,
  `OutboundHandshake.cs`, `InboundWiring.cs`, `RosterBroadcast.cs`, `MemberMessage.cs`,
  `SessionMembership.cs`, `SessionCoordinator.cs` (all in `src/DungeonMasterXIV.Core/Net/`),
  `src/DungeonMasterXIV.Core/Chat/MessageFault.cs`

**Interfaces:**
- Produces:
  - `enum AudienceKind { Public = 0, DmSide = 1, Blind = 2, Player = 3 }`
  - `sealed record MessageAudience(AudienceKind Kind, string? To = null)` with `Public`, `DmSide`,
    `Blind`, `ToPlayer(string peerCode)`, `MessageAudience? ForSending(bool isRoll)`
  - `sealed record EntryPrivacy(MessageAudience Audience, IReadOnlyCollection<string> Entitled, string Placeholder, string? RevealedBy = null)`
    with `bool IsRevealed`, `bool Entitles(string seat)`
  - `static class SeatKey { string For(AdmittedPeer peer) }`
  - `SessionContent.Audience` (`MessageAudience?`), `SessionContent.Audiences` (`bool?`)
  - `JoinDetails.Audiences` (`bool?`)
  - `StreamEntry(..., SharedRoll? Roll = null, EntryPrivacy? Privacy = null)`
  - `StreamLine(..., SharedRoll? Roll = null, AudienceKind? Audience = null, string? To = null, bool? Withheld = null, string? RevealedBy = null)`
  - `SessionAudience.NoteAudiences(PeerCode, bool)`, `SessionAudience.SupportsAudiences(PeerCode)`
  - `SessionCoordinator.CanAddress` (`bool`)
  - `SessionCoordinator.ReceiveJoinRequest(PeerCode, byte[], DateTimeOffset, RelinkClaim relink = default, DisplayName displayName = default, bool supportsAudiences = false)`
  - `SessionMembership.Say(string? text, MessageLimits? limits = null, MessageAudience? audience = null)`,
    `SessionMembership.ShareRoll(SharedRoll roll, MessageAudience? audience = null)`
  - `MessageFault.AudienceUnavailable`

- [ ] **Step 1: Create the audience type**

`src/DungeonMasterXIV.Core/Net/MessageAudience.cs`:

```csharp
namespace DungeonMasterXIV.Net;

/// <summary>Who a message or roll is for: everyone, the DM side, blind to the DM side, or one player and the DM side.</summary>
public enum AudienceKind
{
    Public = 0,

    DmSide = 1,

    Blind = 2,

    Player = 3,
}

/// <summary>The audience a sender asked for; <see cref="To"/> is the player's peer code when the kind is Player.</summary>
public sealed record MessageAudience(AudienceKind Kind, string? To = null)
{
    public static MessageAudience Public { get; } = new(AudienceKind.Public);

    public static MessageAudience DmSide { get; } = new(AudienceKind.DmSide);

    public static MessageAudience Blind { get; } = new(AudienceKind.Blind);

    public static MessageAudience ToPlayer(string peerCode) => new(AudienceKind.Player, peerCode);

    /// <summary>What goes on the wire: nothing for Public, and DM side for text sent while Blind is chosen.</summary>
    public MessageAudience? ForSending(bool isRoll) => Kind switch
    {
        AudienceKind.Public => null,
        AudienceKind.Blind when !isRoll => DmSide,
        _ => this,
    };
}
```

- [ ] **Step 2: Create the recorded privacy and the seat key**

`src/DungeonMasterXIV.Core/Net/EntryPrivacy.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>A private entry's audience, the seats entitled to it when it was sent, its placeholder text, and who revealed it.</summary>
public sealed record EntryPrivacy(
    MessageAudience Audience,
    IReadOnlyCollection<string> Entitled,
    string Placeholder,
    string? RevealedBy = null)
{
    public bool IsRevealed => RevealedBy is not null;

    public bool Entitles(string seat) => Entitled.Contains(seat);
}
```

`src/DungeonMasterXIV.Core/Net/SeatKey.cs`:

```csharp
namespace DungeonMasterXIV.Net;

/// <summary>The identity entitlement is kept under: a member's participant id, or its peer code when it has none.</summary>
public static class SeatKey
{
    public static string For(AdmittedPeer peer) => peer.ParticipantId?.ToString("D") ?? peer.PeerCode.Value;
}
```

- [ ] **Step 3: Add the optional fields**

In `SessionContent.cs`, add to `SessionContent` after `Rolling`:

```csharp
    public MessageAudience? Audience { get; init; }

    public bool? Audiences { get; init; }
```

In `SessionContentCodec.cs`, `Vetted` builds a new `SessionContent`; add the two fields to it,
after `Rolling = content.Rolling,`:

```csharp
            Audience = content.Audience,
            Audiences = content.Audiences,
```

Without this, the decoder silently drops both fields.

In `JoinDetails.cs`, add to `JoinDetails` after `LastSequence`:

```csharp
    public bool? Audiences { get; init; }
```

Replace `StreamEntry.cs` with:

```csharp
namespace DungeonMasterXIV.Net;

/// <summary>One event in a session stream: its stamp, kind, peer, text, roll, and privacy when it is not public.</summary>
public sealed record StreamEntry(
    StreamStamp Stamp,
    StreamEventKind Kind,
    PeerCode Peer,
    string Text,
    SharedRoll? Roll = null,
    EntryPrivacy? Privacy = null);
```

In `StreamLine.cs`, extend the record and `From` (leave `TryToEntry` as it is; it is only used to
vet lines):

```csharp
public readonly record struct StreamLine(
    long Sequence,
    long AtUtcTicks,
    StreamEventKind Kind,
    string Peer,
    string Text,
    SharedRoll? Roll = null,
    AudienceKind? Audience = null,
    string? To = null,
    bool? Withheld = null,
    string? RevealedBy = null)
{
    public static StreamLine From(StreamEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new StreamLine(
            entry.Stamp.Sequence,
            entry.Stamp.AtUtcTicks,
            entry.Kind,
            entry.Peer.Value,
            entry.Text,
            entry.Roll,
            entry.Privacy?.Audience.Kind,
            entry.Privacy?.Audience.To,
            RevealedBy: entry.Privacy?.RevealedBy);
    }
```

In `src/DungeonMasterXIV.Core/Chat/MessageFault.cs`, add after `NotInASession,`:

```csharp

    AudienceUnavailable,
```

- [ ] **Step 4: Remember which members announced support**

In `SessionAudience.cs`, add a field and two methods, and forget a member's support when it is
removed or cleared:

```csharp
    private readonly HashSet<PeerCode> _addressable = new();

    public void NoteAudiences(PeerCode peerCode, bool supported)
    {
        if (supported)
        {
            _addressable.Add(peerCode);
        }
        else
        {
            _addressable.Remove(peerCode);
        }
    }

    public bool SupportsAudiences(PeerCode peerCode) => _addressable.Contains(peerCode);
```

Change `Remove` to:

```csharp
    public bool Remove(PeerCode peerCode)
    {
        _addressable.Remove(peerCode);
        var peer = _admitted.FirstOrDefault(candidate => candidate.PeerCode == peerCode);
        return peer is not null && _admitted.Remove(peer);
    }
```

and `Clear` to:

```csharp
    public void Clear()
    {
        _admitted.Clear();
        _addressable.Clear();
    }
```

In `PendingAdmission.cs`, add a constructor parameter `bool supportsAudiences = false` after
`DisplayName displayName = default`, assign it, and expose it:

```csharp
    public bool SupportsAudiences { get; }
```

In `AdmissionControl.cs`:
- `AdmitToTheQueue` gets a last parameter `bool supportsAudiences = false` and passes it on:
  `Receive(PeerCodeFor(joinerPublicKey), joinerPublicKey, now, relink, displayName, supportsAudiences)`.
- `Receive` gets a last parameter `bool supportsAudiences = false` and builds the request with
  `new PendingAdmission(peerCode, deadline, relink, joinerPublicKey, displayName, supportsAudiences)`.
- In `Admit`, right after `var peer = Audience.Admit(...)`, add
  `Audience.NoteAudiences(peerCode, request?.SupportsAudiences ?? false);`
- In `Resume`, right before `Drops.Forget(peer);`, add
  `Audience.NoteAudiences(peer, details.Audiences ?? false);`

In `InboundWiring.cs`, `OnJoinRequest` passes the flag:

```csharp
                    var request = admissions.AdmitToTheQueue(
                        key,
                        now,
                        DisplayName.OrNone(details.DisplayName),
                        resolveRelink(details.ParticipantId),
                        details.Audiences ?? false);
```

In `SessionCoordinator.cs`, the second `ReceiveJoinRequest` overload gets the parameter:

```csharp
    public PendingAdmission? ReceiveJoinRequest(
        PeerCode peerCode,
        byte[] joinerPublicKey,
        DateTimeOffset now,
        RelinkClaim relink = default,
        DisplayName displayName = default,
        bool supportsAudiences = false) =>
        _admissions.Receive(peerCode, joinerPublicKey, now, relink, displayName, supportsAudiences);
```

- [ ] **Step 5: Both sides announce support**

In `OutboundHandshake.cs`, the join request details become:

```csharp
        var details = new JoinDetails
        {
            DisplayName = _joinDisplayName.WasStated ? _joinDisplayName.Value : null,
            ParticipantId = _claimedParticipantId?.ToString("D"),
            Audiences = true,
        };
```

and the resume proof:

```csharp
        var proof = JoinDetailsCodec.Seal(
            key, new JoinDetails { LastSequence = _lastSequence(), Audiences = true }, code, WireMessageType.Resume);
```

In `RosterBroadcast.Publish`, the roster goes with the flag:

```csharp
        SealToEveryRecipient(new SessionContent { Roster = Current(), Audiences = true }, keys, code);
```

In `SessionCoordinator.cs`, remember what the host announced:

```csharp
    private bool _hostAnnouncedAudiences;

    /// <summary>True when sends can carry an audience: always when hosting, otherwise once the host has announced it.</summary>
    public bool CanAddress => InAHostedSession || (Join.Phase == JoinPhase.Admitted && _hostAnnouncedAudiences);
```

In `HeardFromTheHost`, first line:

```csharp
        if (content.Roster is not null)
        {
            _hostAnnouncedAudiences = content.Audiences == true;
        }
```

Set `_hostAnnouncedAudiences = false;` at the top of `StartHosting` and of the three-argument
`RequestJoin`.

- [ ] **Step 6: Member sends carry an audience**

In `MemberMessage.cs`, change the two send methods:

```csharp
    public MessageDraft Say(string? text, MessageLimits limits, MessageAudience? audience = null)
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

        _waiting.Enqueue(new SessionContent { Saying = draft.Text!, Audience = audience?.ForSending(isRoll: false) });
        Flush();
        return draft;
    }

    /// <summary>Queues a roll for the host; returns why it cannot be shared, or null once it is queued.</summary>
    public string? ShareRoll(SharedRoll roll, MessageAudience? audience = null)
    {
        ArgumentNullException.ThrowIfNull(roll);

        if (roll.RefusalToShare() is { } refusal)
        {
            return refusal;
        }

        if (_code() is null || _sessionKey() is null)
        {
            return "This client is not in a session.";
        }

        _waiting.Enqueue(new SessionContent { Rolling = roll, Audience = audience?.ForSending(isRoll: true) });
        Flush();
        return null;
    }
```

In `SessionMembership.cs`:

```csharp
    public MessageDraft Say(string? text, MessageLimits? limits = null, MessageAudience? audience = null) =>
        _message.Say(text, limits ?? MessageLimits.Default, audience);

    public string? ShareRoll(SharedRoll roll, MessageAudience? audience = null) => _message.ShareRoll(roll, audience);
```

- [ ] **Step 7: Build and run the existing tests**

Run: `dotnet build` then `dotnet test`
Expected: build succeeds with 0 warnings; 16 tests, 0 failed, 0 skipped.

- [ ] **Step 8: Commit**

```bash
git add src/DungeonMasterXIV.Core
git commit -F- <<'EOF'
feat(session): audiences on the wire, announced by host and members

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 2: The host settles audiences and sends each member their own view

**Files:**
- Create: `src/DungeonMasterXIV.Core/Net/AudienceRules.cs`, `MemberView.cs`
- Create: `tests/DungeonMasterXIV.Tests/APrivateRollLeavesOthersAPlaceholderTests.cs`
- Modify: `SessionStream.cs`, `SessionRecording.cs`, `HostStream.cs`, `RosterBroadcast.cs`,
  `SessionWiring.cs`, `InboundWiring.cs`, `SessionCoordinator.cs` (in `src/DungeonMasterXIV.Core/Net/`)

**Interfaces:**
- Consumes: everything Task 1 produces.
- Produces:
  - `SessionCoordinator.Say(string? text, DateTimeOffset now, MessageAudience? audience = null)`
  - `SessionCoordinator.ShareRoll(SharedRoll roll, DateTimeOffset now, MessageAudience? audience = null)`
  - `SessionCoordinator.Reveal(long sequence)` → `bool`; `sequence` is the record's sequence, which
    is what the host's own `LatestStreamLines` carries.
  - `AudienceRules.ForTheDm` = `"rolled for the DM"`, `AudienceRules.ByTheDm` = `"rolled"`

- [ ] **Step 1: Write the failing smoke test**

`tests/DungeonMasterXIV.Tests/APrivateRollLeavesOthersAPlaceholderTests.cs`:

```csharp
using System;
using System.Linq;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Rolls;
using Xunit;
using static DungeonMasterXIV.Tests.BaseChatFixture;

namespace DungeonMasterXIV.Tests;

/// <summary>A private roll reaches others only as a placeholder, a whisper not at all, and a reveal fills the placeholder in.</summary>
public sealed class APrivateRollLeavesOthersAPlaceholderTests
{
    [Fact]
    public void OthersGetAPlaceholderAndNoWhisperUntilTheDmReveals()
    {
        var host = Hosting(out var transport);
        using var eli = new SessionKeyExchange();
        using var mara = new SessionKeyExchange();
        using var old = new SessionKeyExchange();
        AdmittedWithAudiences(host, Speaker, eli);
        AdmittedWithAudiences(host, Listener, mara);
        var oldCode = Admitted(host, "MNPQRS", old);

        host.Say("before", Now);
        var outcome = new RollEvaluator(new ScriptedDieRoller(17)).Evaluate("1d20");
        transport.Deliver(SealedBy(eli, host, new SessionContent
        {
            Rolling = SharedRoll.From("1d20", outcome),
            Audience = MessageAudience.DmSide,
        }));
        transport.Deliver(SealedBy(eli, host, new SessionContent
        {
            Saying = "the key is under the mat",
            Audience = MessageAudience.DmSide,
        }));
        host.Tick(TimeSpan.Zero, Now);
        var dmRoll = new RollEvaluator(new ScriptedDieRoller(4)).Evaluate("1d6");
        Assert.Null(host.ShareRoll(SharedRoll.From("1d6", dmRoll), Now, MessageAudience.DmSide));
        host.Say("after", Now);

        Assert.False(host.Say("psst", Now, MessageAudience.ToPlayer(oldCode.Value)).IsAccepted);

        var seen = StampedLinesFor(mara, host, transport);
        var numbers = seen.Select(line => line.Sequence).ToList();
        Assert.Equal(Enumerable.Range((int)numbers[0], numbers.Count).Select(n => (long)n), numbers);
        Assert.DoesNotContain(seen, line => line.Text.Contains("mat", StringComparison.Ordinal));
        Assert.All(seen, line => Assert.Null(line.Roll));

        var placeholders = seen.Where(line => line.Withheld == true).ToList();
        Assert.Equal(2, placeholders.Count);
        Assert.Equal(AudienceRules.ForTheDm, placeholders[0].Text);
        Assert.Equal(AudienceRules.ByTheDm, placeholders[1].Text);

        var eliRoll = Assert.Single(StampedLinesFor(eli, host, transport), line => line.Roll is not null);
        Assert.Equal(AudienceKind.DmSide, eliRoll.Audience);

        var recorded = host.Recorded.First(entry => entry.Kind == StreamEventKind.Roll);
        Assert.True(host.Reveal(recorded.Stamp.Sequence));
        Assert.False(host.Reveal(recorded.Stamp.Sequence));

        var revealed = StampedLinesFor(mara, host, transport).Last(line => line.Sequence == placeholders[0].Sequence);
        Assert.Equal(17, revealed.Roll!.Total);
        Assert.NotNull(revealed.RevealedBy);
    }

    private static PeerCode AdmittedWithAudiences(SessionCoordinator host, string code, SessionKeyExchange keys)
    {
        var peerCode = PeerCodes.Of(code);
        host.ReceiveJoinRequest(peerCode, keys.PublicKey, Now, supportsAudiences: true);
        host.Admit(peerCode);
        return peerCode;
    }
}
```

`"MNPQRS"` must be a valid peer code for `PeerCodes.Of`. If it throws, pick six letters from
`SpeakableAlphabet.Characters`.

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --filter APrivateRollLeavesOthersAPlaceholderTests`
Expected: FAIL. It fails to compile (`AudienceRules`, `Reveal`, and the audience arguments to `Say`
and `ShareRoll` do not exist yet).

- [ ] **Step 3: The audience rules**

`src/DungeonMasterXIV.Core/Net/AudienceRules.cs`:

```csharp
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>Settles what a sender asked for into a private entry's audience and entitled seats, or refuses it.</summary>
internal static class AudienceRules
{
    public const string ForTheDm = "rolled for the DM";

    public const string ByTheDm = "rolled";

    /// <summary>False when the request must be dropped; a null privacy means public. A null sender is the host.</summary>
    public static bool TryResolve(
        MessageAudience? requested,
        bool isRoll,
        AdmittedPeer? sender,
        SessionAudience audience,
        out EntryPrivacy? privacy)
    {
        privacy = null;
        if (requested is null || requested.Kind == AudienceKind.Public)
        {
            return true;
        }

        var fromHost = sender is null;
        var seats = audience.Recipients
            .Where(peer => peer.Role == SessionRole.Assistant)
            .Select(SeatKey.For)
            .ToList();
        if (sender is not null)
        {
            seats.Add(SeatKey.For(sender));
        }

        switch (requested.Kind)
        {
            case AudienceKind.DmSide:
            case AudienceKind.Blind:
                var kind = requested.Kind == AudienceKind.Blind && isRoll && !fromHost
                    ? AudienceKind.Blind
                    : AudienceKind.DmSide;
                privacy = new EntryPrivacy(
                    new MessageAudience(kind), seats.Distinct().ToList(), fromHost ? ByTheDm : ForTheDm);
                return true;

            case AudienceKind.Player when fromHost
                && requested.To is { } to
                && PeerCode.TryParse(to, out var code)
                && audience.Find(code) is { Role: SessionRole.Player } target
                && audience.SupportsAudiences(code):
                seats.Add(SeatKey.For(target));
                privacy = new EntryPrivacy(
                    MessageAudience.ToPlayer(to), seats.Distinct().ToList(), $"rolled for {target.DisplayName.Value}");
                return true;

            default:
                return false;
        }
    }
}
```

- [ ] **Step 4: A member's view**

`src/DungeonMasterXIV.Core/Net/MemberView.cs`:

```csharp
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>One line of a member's view, with the sequence of the recorded entry it came from.</summary>
internal readonly record struct ViewedLine(long RecordSequence, StreamLine Line);

/// <summary>Builds the stream one member may receive from the host's record, numbered 1, 2, 3… for that member alone.</summary>
internal static class MemberView
{
    public static List<ViewedLine> Of(IReadOnlyList<StreamEntry> record, string seat, bool addressable)
    {
        var view = new List<ViewedLine>(record.Count);
        foreach (var entry in record)
        {
            if (Shape(entry, seat, addressable) is { } line)
            {
                view.Add(new ViewedLine(entry.Stamp.Sequence, line with { Sequence = view.Count + 1 }));
            }
        }

        return view;
    }

    private static StreamLine? Shape(StreamEntry entry, string seat, bool addressable)
    {
        if (entry.Privacy is not { } privacy || privacy.IsRevealed || (addressable && privacy.Entitles(seat)))
        {
            return StreamLine.From(entry);
        }

        if (entry.Kind != StreamEventKind.Roll)
        {
            return null;
        }

        return new StreamLine(
            0, entry.Stamp.AtUtcTicks, StreamEventKind.Message, entry.Peer.Value, privacy.Placeholder, Withheld: true);
    }
}
```

A placeholder's kind is Message on purpose: an older client draws it as an ordinary line.

- [ ] **Step 5: Record privacy and reveals**

In `SessionStream.cs`, add:

```csharp
    public bool Replace(StreamEntry entry)
    {
        var at = _entries.FindIndex(existing => existing.Stamp.Sequence == entry.Stamp.Sequence);
        if (at < 0)
        {
            return false;
        }

        _entries[at] = entry;
        return true;
    }
```

In `SessionRecording.cs`, add `using System.Linq;`, give `StampAsHost` a last parameter
`EntryPrivacy? privacy = null` and build the entry with
`new StreamEntry(_sequencer.Next(), kind, peer, text, roll, privacy)`. Then add:

```csharp
    /// <summary>Marks a private roll revealed; returns the revealed entry, or null when there is nothing to reveal.</summary>
    public StreamEntry? Reveal(long sequence, string revealedBy)
    {
        var entry = _stream.Entries.FirstOrDefault(candidate => candidate.Stamp.Sequence == sequence);
        if (entry is not { Kind: StreamEventKind.Roll, Privacy: { IsRevealed: false } privacy })
        {
            return null;
        }

        var revealed = entry with { Privacy = privacy with { RevealedBy = revealedBy } };
        return _stream.Replace(revealed) ? revealed : null;
    }
```

- [ ] **Step 6: Per-member fan-out**

Replace `HostStream.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>Stamps a host stream entry and sends each admitted member its line in their own view, in the same step.</summary>
internal sealed class HostStream(SessionRecording recording, RosterBroadcast roster, SessionAudience audience)
{
    public StreamEntry? Announce(
        StreamEventKind kind,
        PeerCode peer,
        string text,
        DateTimeOffset at,
        SharedRoll? roll = null,
        EntryPrivacy? privacy = null)
    {
        if (recording.StampAsHost(kind, peer, text, at, roll, privacy) is not { } entry)
        {
            return null;
        }

        SendToEachMember(entry.Stamp.Sequence);
        return entry;
    }

    public bool Reveal(long sequence, PeerCode revealedBy)
    {
        if (recording.Reveal(sequence, revealedBy.Value) is null)
        {
            return false;
        }

        SendToEachMember(sequence);
        return true;
    }

    /// <summary>The lines a returning member missed, plus every revealed roll, so a reveal made while away fills in.</summary>
    public IReadOnlyList<StreamLine> MissedBy(AdmittedPeer peer, long lastSequence) =>
        ViewFor(peer)
            .Where(viewed => viewed.Line.Sequence > lastSequence || viewed.Line.RevealedBy is not null)
            .Select(viewed => viewed.Line)
            .ToList();

    private void SendToEachMember(long recordSequence)
    {
        foreach (var peer in audience.Recipients)
        {
            var viewed = ViewFor(peer).Find(candidate => candidate.RecordSequence == recordSequence);
            if (viewed.RecordSequence == recordSequence)
            {
                roster.PublishEntriesTo(peer.PeerCode, new[] { viewed.Line });
            }
        }
    }

    private List<ViewedLine> ViewFor(AdmittedPeer peer) =>
        MemberView.Of(recording.Entries, SeatKey.For(peer), audience.SupportsAudiences(peer.PeerCode));
}
```

In `RosterBroadcast.cs`, delete `PublishEntry`: nothing calls it now.

In `SessionWiring.cs`, construct it with the audience:

```csharp
        Stream = new HostStream(Resources.Recording, Roster, Admissions.Audience);
```

- [ ] **Step 7: Enforce audiences on member sends, and resume from views**

In `InboundWiring.cs`, add a constructor parameter `ISessionTransportLog log` after
`HostStream stream`. Replace the resume body's `missed` computation:

```csharp
                    var missed = admissions.Audience.Find(resumed.Peer) is { } member
                        ? stream.MissedBy(member, resumed.LastSequence)
                        : Array.Empty<StreamLine>();
```

Replace `Said` and `Rolled`:

```csharp
    private void Said(PeerCode peer, SessionContent content, DateTimeOffset now)
    {
        if (content.Saying is not { } text)
        {
            return;
        }

        var draft = MessageDraft.Compose(text, MessageLimits.Default);

        if (!draft.IsAccepted || draft.Text is not { } said || !Addressed(peer, content, isRoll: false, out var privacy))
        {
            return;
        }

        stream.Announce(StreamEventKind.Message, peer, said, now, privacy: privacy);
    }

    private void Rolled(PeerCode peer, SessionContent content, DateTimeOffset now)
    {
        if (content.Rolling is not { } roll
            || !roll.IsWithinBounds(RollLimits.Default)
            || !Addressed(peer, content, isRoll: true, out var privacy))
        {
            return;
        }

        stream.Announce(StreamEventKind.Roll, peer, roll.Summary(), now, roll, privacy);
    }

    private bool Addressed(PeerCode peer, SessionContent content, bool isRoll, out EntryPrivacy? privacy)
    {
        privacy = null;
        if (admissions.Audience.Find(peer) is not { } sender)
        {
            return false;
        }

        if (AudienceRules.TryResolve(content.Audience, isRoll, sender, admissions.Audience, out privacy))
        {
            return true;
        }

        log.Warning(
            $"Dropped a send from {peer.Value} addressed to an audience it may not use. Nothing was "
            + "relayed. The content is deliberately not recorded here.");
        return false;
    }
```

In `SessionCoordinator.TickSession`, pass the log when building the wiring:

```csharp
            new InboundWiring(_admissions, _resources, _resolveRelink, _roster, _parts.Stream, _log, Reclaimed, ReclaimRefused, HostWentAway, HostCameBack)
```

- [ ] **Step 8: Audiences on the coordinator's sends, and Reveal**

In `SessionCoordinator.cs`, replace `Say` and `ShareRoll`, and add `Reveal` and two helpers:

```csharp
    /// <summary>Sends a message to the session: stamped directly when hosting, sealed to the host otherwise.</summary>
    public MessageDraft Say(string? text, DateTimeOffset now, MessageAudience? audience = null)
    {
        var addressed = audience?.ForSending(isRoll: false);
        if (!InAHostedSession)
        {
            return addressed is not null && !CanAddress
                ? new MessageDraft(null, MessageFault.AudienceUnavailable, HostNeedsUpdating)
                : Membership.Say(text, audience: addressed);
        }

        var draft = MessageDraft.Compose(text, MessageLimits.Default);
        if (!draft.IsAccepted)
        {
            return draft;
        }

        if (_parts.HostIdentity.OwnPeerCode() is not { } own)
        {
            return new MessageDraft(null, MessageFault.NotInASession, "This client is not in a session.");
        }

        if (!AudienceRules.TryResolve(addressed, isRoll: false, null, Audience, out var privacy))
        {
            return new MessageDraft(null, MessageFault.AudienceUnavailable, AudienceRefusal(addressed));
        }

        _parts.Stream.Announce(StreamEventKind.Message, own, draft.Text!, now, privacy: privacy);
        return draft;
    }

    /// <summary>Shares a roll this client made; returns why it was not shared, or null.</summary>
    public string? ShareRoll(SharedRoll roll, DateTimeOffset now, MessageAudience? audience = null)
    {
        ArgumentNullException.ThrowIfNull(roll);

        var addressed = audience?.ForSending(isRoll: true);
        if (!InAHostedSession)
        {
            return addressed is not null && !CanAddress ? HostNeedsUpdating : Membership.ShareRoll(roll, addressed);
        }

        if (roll.RefusalToShare() is { } refusal)
        {
            return refusal;
        }

        if (_parts.HostIdentity.OwnPeerCode() is not { } own)
        {
            return "This client is not in a session.";
        }

        if (!AudienceRules.TryResolve(addressed, isRoll: true, null, Audience, out var privacy))
        {
            return AudienceRefusal(addressed);
        }

        _parts.Stream.Announce(StreamEventKind.Roll, own, roll.Summary(), now, roll, privacy);
        return null;
    }

    /// <summary>Reveals a private or blind roll to everyone; only the host can, and only once.</summary>
    public bool Reveal(long sequence) =>
        InAHostedSession
        && _parts.HostIdentity.OwnPeerCode() is { } own
        && _parts.Stream.Reveal(sequence, own);

    private const string HostNeedsUpdating = "Your DM's plugin needs updating for private messages.";

    private string AudienceRefusal(MessageAudience? audience) =>
        audience?.To is { } to && PeerCode.TryParse(to, out var code) && Audience.Find(code) is { } target
            ? $"{target.DisplayName.Value}'s plugin needs updating for private messages."
            : "That player is no longer in the session.";
```

- [ ] **Step 9: Run the smoke test**

Run: `dotnet test --filter APrivateRollLeavesOthersAPlaceholderTests`
Expected: PASS.

- [ ] **Step 10: Run everything**

Run: `dotnet build` then `dotnet test`
Expected: 0 warnings; 17 tests, 0 failed, 0 skipped.

- [ ] **Step 11: Commit**

```bash
git add src/DungeonMasterXIV.Core tests/DungeonMasterXIV.Tests/APrivateRollLeavesOthersAPlaceholderTests.cs
git commit -F- <<'EOF'
feat(session): the host settles audiences and sends each member their own view

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 3: The audience row, private cards, reveal, and the roster shortcut

UI only, so it is checked by eye in game (smoke-tests-only rule).

**Files:**
- Create: `Windows/AudienceChoice.cs`, `Windows/Ui/Components/AudienceRow.cs`,
  `Windows/Ui/Components/AudienceMark.cs`, `Windows/Ui/Components/PrivateCard.cs`
- Modify: `Windows/Ui/Components/MessageCard.cs`, `Windows/Ui/Components/RollCard.cs`,
  `Windows/MessageComposeView.cs`, `Windows/ChatTab.cs`, `Windows/StreamView.cs`,
  `Windows/SessionTab.cs`, `Plugin.cs`, `docs/superpowers/specs/2026-10-04-ui-foundation-design.md`

**Interfaces:**
- Consumes: `SessionCoordinator.CanAddress`, `Say(text, now, audience)`,
  `ShareRoll(roll, now, audience)`, `Reveal(long)`, `CurrentRoster`, `Speakers`,
  `Audience.SupportsAudiences(PeerCode)`; `StreamLine.Audience`, `To`, `Withheld`, `RevealedBy`.
- Produces: `AudienceChoice` (`Kind`, `To`, `Current`, `Choose`, `ChoosePlayer`, `Reset`).

- [ ] **Step 1: The shared choice**

`Windows/AudienceChoice.cs`:

```csharp
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>The audience chosen above the message box, shared so the roster can pick a player to message.</summary>
internal sealed class AudienceChoice
{
    public AudienceKind Kind { get; private set; } = AudienceKind.Public;

    public string? To { get; private set; }

    public MessageAudience Current =>
        Kind == AudienceKind.Player && To is { } to ? MessageAudience.ToPlayer(to) : new MessageAudience(Kind);

    public void Choose(AudienceKind kind)
    {
        Kind = kind;
        To = null;
    }

    public void ChoosePlayer(string peerCode)
    {
        Kind = AudienceKind.Player;
        To = peerCode;
    }

    public void Reset() => Choose(AudienceKind.Public);
}
```

- [ ] **Step 2: The audience mark**

`Windows/Ui/Components/AudienceMark.cs`:

```csharp
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>The line under a private card's speaker: who it is for, blind, and revealed, as icons with short words.</summary>
internal sealed record AudienceMark(FontAwesomeIcon Icon, string Label, bool Blind, bool Revealed)
{
    public const string RevealedByTheDm = "Revealed by the DM";

    public void Draw(UiFonts fonts)
    {
        using var meta = fonts.Meta.Push();
        Glyph(fonts, Icon);
        ImGui.SameLine();
        ImGui.TextColored(Palette.PrivateText, Label);

        if (Blind)
        {
            ImGui.SameLine();
            Glyph(fonts, FontAwesomeIcon.EyeSlash);
        }

        if (Revealed)
        {
            ImGui.SameLine();
            Glyph(fonts, FontAwesomeIcon.Eye);
            ImGui.SameLine();
            ImGui.TextColored(Palette.TextMuted, RevealedByTheDm);
        }
    }

    private static void Glyph(UiFonts fonts, FontAwesomeIcon icon)
    {
        using var font = fonts.Icon.Push();
        ImGui.TextColored(Palette.PrivateText, icon.ToIconString());
    }
}
```

- [ ] **Step 3: Private surfaces on message and roll cards**

`MessageCard.Draw` gains an optional mark; an unrevealed mark means the private surface:

```csharp
    public static void Draw(UiFonts fonts, SpeakerName speaker, long atUtcTicks, string text, AudienceMark? mark = null)
    {
        using var card = mark is { Revealed: false }
            ? Card.Begin(Palette.PrivateSurface, Palette.PrivateRule)
            : Card.Begin(Palette.SurfaceRaised, Palette.Rule);
        Speaker.DrawWithTime(fonts, speaker, atUtcTicks);
        mark?.Draw(fonts);
        ImGui.TextWrapped(text);
    }
```

In `RollCard.cs`, make `Bar` `internal` (the placeholder uses it) and extend `Draw`:

```csharp
    public const string RevealTooltip = "Reveal to everyone";

    /// <summary>Draws the card; returns true when the host pressed Reveal on it.</summary>
    public static bool Draw(
        UiFonts fonts,
        SpeakerName speaker,
        long atUtcTicks,
        SharedRoll roll,
        bool local = false,
        AudienceMark? mark = null,
        bool hideTotal = false,
        bool canReveal = false)
    {
        using var card = mark is { Revealed: false }
            ? Card.Begin(Palette.PrivateSurface, Palette.PrivateRule)
            : Card.Begin(Palette.SurfaceRaised, Palette.Rule);
        Speaker.DrawWithTime(fonts, speaker, atUtcTicks);
        mark?.Draw(fonts);

        if (!string.IsNullOrWhiteSpace(roll.Label))
        {
            using var title = fonts.Title.Push();
            ImGui.TextColored(Palette.GoldLabel, roll.Label);
        }

        Bar(fonts.Body, roll.Expression, Palette.Text);
        if (hideTotal)
        {
            Bar(fonts.Total, "?", Palette.GoldBright);
        }
        else
        {
            DrawDice(fonts, roll);
            Bar(fonts.Total, roll.Total.ToString(CultureInfo.InvariantCulture), Palette.GoldBright);
        }

        if (RollSurvival.NoticeFor(roll.Dice) is { } notice && !hideTotal)
        {
            ImGui.TextColored(Palette.Warning, notice);
        }

        if (local)
        {
            using var meta = fonts.Meta.Push();
            ImGui.TextColored(Palette.TextMuted, OnlyYou);
        }

        return canReveal && RailButton.Draw(fonts, FontAwesomeIcon.Eye, RevealTooltip, lit: false);
    }
```

Add `using Dalamud.Interface;` for `FontAwesomeIcon`. The existing callers ignore the return value
and keep compiling.

`Windows/Ui/Components/PrivateCard.cs`:

```csharp
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>What someone not entitled to a private roll sees: the speaker, one line of text, and a "?" bar.</summary>
internal static class PrivateCard
{
    public static void Placeholder(UiFonts fonts, SpeakerName speaker, long atUtcTicks, string text)
    {
        using var card = Card.Begin(Palette.PrivateSurface, Palette.PrivateRule);
        Speaker.DrawWithTime(fonts, speaker, atUtcTicks);
        ImGui.TextColored(Palette.PrivateText, text);
        RollCard.Bar(fonts.Total, "?", Palette.GoldBright);
    }
}
```

- [ ] **Step 4: Draw lines by audience**

In `StreamView.cs`, add `using Dalamud.Interface;` and replace `DrawLine`:

```csharp
    private void DrawLine(StreamLine line)
    {
        var speaker = _coordinator.Speakers.For(line.Peer);

        if (line.Withheld == true)
        {
            PrivateCard.Placeholder(_fonts, speaker, line.AtUtcTicks, line.Text);
            return;
        }

        var mark = MarkFor(line);

        switch (line.Kind)
        {
            case StreamEventKind.Message:
                MessageCard.Draw(_fonts, speaker, line.AtUtcTicks, line.Text, mark);
                break;
            case StreamEventKind.Roll when line.Roll is { } roll:
                var hideTotal = line.Audience == AudienceKind.Blind
                    && line.RevealedBy is null
                    && !_coordinator.InAHostedSession;
                var canReveal = _coordinator.InAHostedSession && mark is { Revealed: false };
                if (RollCard.Draw(_fonts, speaker, line.AtUtcTicks, roll, mark: mark, hideTotal: hideTotal, canReveal: canReveal))
                {
                    _coordinator.Reveal(line.Sequence);
                }

                break;
            case StreamEventKind.Roll:
                MessageCard.Draw(_fonts, speaker, line.AtUtcTicks, line.Text, mark);
                break;
            default:
                EventLine.Draw(_fonts, line.Kind, speaker, line.AtUtcTicks);
                break;
        }
    }

    private AudienceMark? MarkFor(StreamLine line)
    {
        if (line.Audience is not { } kind || kind == AudienceKind.Public)
        {
            return null;
        }

        return kind == AudienceKind.Player && line.To is { } to
            ? new AudienceMark(FontAwesomeIcon.User, _coordinator.Speakers.For(to).Name, false, line.RevealedBy is not null)
            : new AudienceMark(FontAwesomeIcon.UserSecret, "DM", kind == AudienceKind.Blind, line.RevealedBy is not null);
    }
```

On the host, `line.Sequence` is the record's sequence, which is what `Reveal` takes. Members never
get `canReveal`.

- [ ] **Step 5: The audience row**

`Windows/Ui/Components/AudienceRow.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>The icon row above the message box that picks who sees the next message or roll.</summary>
internal static class AudienceRow
{
    public const string PublicTooltip = "Public: everyone sees it";
    public const string DmOnlyTooltip = "DM only: you and the DM";
    public const string DmSideTooltip = "DM side only";
    public const string BlindTooltip = "Blind: the DM sees the roll, you don't";
    public const string ToPlayerTooltip = "To one player";

    public static float Height => RailButton.Size + ImGui.GetStyle().ItemSpacing.Y;

    public static void Draw(
        UiFonts fonts,
        AudienceChoice choice,
        bool host,
        IReadOnlyList<RosterEntry> roster,
        Func<string, bool> supports)
    {
        if (RailButton.Draw(fonts, FontAwesomeIcon.Globe, PublicTooltip, choice.Kind == AudienceKind.Public))
        {
            choice.Choose(AudienceKind.Public);
        }

        ImGui.SameLine();
        if (RailButton.Draw(fonts, FontAwesomeIcon.UserSecret, host ? DmSideTooltip : DmOnlyTooltip, choice.Kind == AudienceKind.DmSide))
        {
            choice.Choose(AudienceKind.DmSide);
        }

        ImGui.SameLine();
        if (!host)
        {
            if (RailButton.Draw(fonts, FontAwesomeIcon.EyeSlash, BlindTooltip, choice.Kind == AudienceKind.Blind))
            {
                choice.Choose(AudienceKind.Blind);
            }

            return;
        }

        if (RailButton.Draw(fonts, FontAwesomeIcon.User, ToPlayerTooltip, choice.Kind == AudienceKind.Player))
        {
            ImGui.OpenPopup("##to-player");
        }

        if (choice.Kind == AudienceKind.Player && choice.To is { } to)
        {
            ImGui.SameLine();
            ImGui.TextColored(Palette.PrivateText, NameOf(roster, to));
        }

        using var popup = ImRaii.Popup("##to-player");
        if (!popup.Success)
        {
            return;
        }

        foreach (var player in roster.Where(entry => entry.Role == SessionRole.Player))
        {
            var ready = supports(player.PeerCode);
            using (ImRaii.Disabled(!ready))
            {
                if (ImGui.Selectable($"{NameOf(roster, player.PeerCode)}##{player.PeerCode}"))
                {
                    choice.ChoosePlayer(player.PeerCode);
                }
            }

            if (!ready && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(NeedsUpdating(NameOf(roster, player.PeerCode)));
            }
        }
    }

    public static string NeedsUpdating(string name) => $"{name}'s plugin needs updating for private messages";

    public static string NameOf(IReadOnlyList<RosterEntry> roster, string peerCode) =>
        roster.FirstOrDefault(entry => entry.PeerCode == peerCode) is { PeerCode: not null } entry
            ? DisplayName.OrNone(entry.DisplayName).Value
            : DisplayName.Unstated;
}
```

- [ ] **Step 6: Wire the row into the message box**

In `MessageComposeView.cs`:
- the constructor takes `AudienceChoice choice` as a second parameter and keeps it in
  `private readonly AudienceChoice _choice;`
- add `private bool _wasInSession;`
- `Height` adds the row when it is shown:

```csharp
    public float Height =>
        (_coordinator.CanAddress ? AudienceRow.Height : 0f)
        + ImGui.GetFrameHeightWithSpacing()
        + (_refusal is null ? 0f : ImGui.GetTextLineHeightWithSpacing() * 2f);
```

- at the top of `Draw`, after the local-rolls clearing:

```csharp
        if (_coordinator.InASession && !_wasInSession)
        {
            _choice.Reset();
        }

        _wasInSession = _coordinator.InASession;

        if (_coordinator.CanAddress)
        {
            AudienceRow.Draw(_fonts, _choice, _coordinator.InAHostedSession, _coordinator.CurrentRoster, Supports);
        }

        var problem = TargetProblem();
```

  `MessageComposeView` needs `UiFonts`: take it as a third constructor parameter and keep it in
  `_fonts`.
- the input's hint becomes `Hint()` instead of the literal.
- disable Send while there is a target problem, and show why. Wrap the Send button:

```csharp
            ImGui.SameLine();
            bool pressed;
            using (ImRaii.Disabled(problem is not null))
            {
                pressed = ActionRow.Primary("Send");
            }

            if (problem is not null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(problem);
            }

            if ((pressed || entered) && problem is null)
            {
                Submit();
                _refocus = true;
            }
```

- `Submit` and `Roll` pass the audience:
  `_coordinator.Say(_entry, DateTimeOffset.UtcNow, Addressed)` and
  `_coordinator.ShareRoll(roll, now, Addressed)`.
- add the helpers:

```csharp
    private MessageAudience? Addressed => _coordinator.CanAddress ? _choice.Current : null;

    private bool Supports(string peerCode) =>
        PeerCode.TryParse(peerCode, out var peer) && _coordinator.Audience.SupportsAudiences(peer);

    /// <summary>Why the chosen player cannot be messaged now, or null. Never switches the audience on its own.</summary>
    private string? TargetProblem()
    {
        if (!_coordinator.InAHostedSession || _choice.Kind != AudienceKind.Player || _choice.To is not { } to)
        {
            return null;
        }

        var roster = _coordinator.CurrentRoster;
        var name = _coordinator.Speakers.For(to).Name;
        if (roster.All(entry => entry.PeerCode != to))
        {
            return $"{name} is no longer in the session";
        }

        return Supports(to) ? null : AudienceRow.NeedsUpdating(name);
    }

    private string Hint()
    {
        if (!_coordinator.CanAddress)
        {
            return "Say something, or /roll 1d20";
        }

        return _choice.Kind switch
        {
            AudienceKind.DmSide when _coordinator.InAHostedSession => "DM side only: say something, or /roll…",
            AudienceKind.DmSide => "To the DM: say something, or /roll…",
            AudienceKind.Blind => "Blind roll to the DM: /roll 1d20",
            AudienceKind.Player when _choice.To is { } to => $"To {_coordinator.Speakers.For(to).Name}: say something, or /roll…",
            _ => "Say something, or /roll 1d20",
        };
    }
```

  Add `using System.Linq;` and `using DungeonMasterXIV.Windows.Ui;`.

In `ChatTab.cs`, the constructor takes `AudienceChoice audience` after `Action showSession`, and
builds the box with `new MessageComposeView(coordinator, audience, fonts)`.

- [ ] **Step 7: The roster shortcut**

In `SessionTab.cs`, the constructor takes `AudienceChoice audience` and `Action showChat` after
`JoinFlowView joinFlow`, kept in `_audience` and `_showChat`. In `DrawPeople`, after
`RosterRow.Draw(...)`:

```csharp
            if (host && entry.Role == SessionRole.Player)
            {
                var name = DisplayName.OrNone(entry.DisplayName).Value;
                var ready = PeerCode.TryParse(entry.PeerCode, out var code) && _coordinator.Audience.SupportsAudiences(code);
                ImGui.SameLine();
                using (ImRaii.Disabled(!ready))
                {
                    if (RailButton.Draw(_fonts, FontAwesomeIcon.User, $"Message {name} privately", lit: false))
                    {
                        _audience.ChoosePlayer(entry.PeerCode);
                        _showChat();
                    }
                }

                if (!ready && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip(AudienceRow.NeedsUpdating(name));
                }
            }
```

Add `using Dalamud.Interface;` and `using Dalamud.Interface.Utility.Raii;` if missing.

In `Plugin.cs`, create one choice and pass it to both tabs:

```csharp
        var audience = new AudienceChoice();
        _panel.Attach(
            new ChatTab(_sessionCoordinator, _fonts, NameWeSendAs(characterName), joinFlow, () => _panel.Select(PanelTab.Session), audience),
            new SessionTab(_sessionCoordinator, _fonts, _hostingCampaign, joinFlow, audience, () => _panel.Select(PanelTab.Chat)),
```

(keep the existing third argument to `Attach`).

- [ ] **Step 8: Update the UI foundation spec**

In `docs/superpowers/specs/2026-10-04-ui-foundation-design.md`, delete the line
`- **All rolls are Public** until roll modes are built (rolls R-2.15 default).`

- [ ] **Step 9: Build, test, and check in game**

Run: `dotnet build` then `dotnet test`
Expected: 0 warnings; 17 tests, 0 failed, 0 skipped.

Then check in game, with a second client joined as a player:
1. The player sees Globe, UserSecret, EyeSlash; the DM sees Globe, UserSecret, User ▾. Each tooltip
   matches the spec's table.
2. The message box placeholder names the audience for each choice.
3. A player's DM-only roll: the DM and the roller see the full card with `UserSecret` DM; a third
   client sees "rolled for the DM" and a "?" bar.
4. A blind roll: the roller sees the expression and "?"; the DM sees it in full with `EyeSlash`.
5. A DM-only message from a player: the third client sees nothing.
6. Reveal on the DM's card: every placeholder fills in with "Revealed by the DM", including the
   blind roller's; the button is gone.
7. The roster's `User` button sets To ▾ and switches to Chat.
8. Disconnect the whispered player: To ▾ still names them, and Send is disabled with "Eli is no
   longer in the session".
9. Reveal a roll while a client is disconnected, then let it reconnect: its placeholder fills in.
10. End the session with DM only chosen, start a new one: the row is back on Public.
11. Nothing appears in FFXIV's chat log.

- [ ] **Step 10: Commit**

```bash
git add Windows Plugin.cs docs/superpowers/specs/2026-10-04-ui-foundation-design.md
git commit -F- <<'EOF'
feat(ui): audience row, private cards, reveal, and the roster shortcut

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```
