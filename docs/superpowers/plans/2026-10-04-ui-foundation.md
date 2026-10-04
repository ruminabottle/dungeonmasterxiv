# UI Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the plugin one Chronicle look and a component layer, replace the main window with a launcher rail, show the session stream as cards, and send public rolls, membership events and the DM's own lines through the host.

**Architecture:**
- **Core.** Core (`src/DungeonMasterXIV.Core`) gains a `SharedRoll` that travels on the existing sealed member path. `HostStream` stamps every host-side entry and sends it in one step. The coordinator gets one `Say`/`ShareRoll` pair that works for host and member alike.
- **Plugin.** The plugin's `Windows/Ui/` layer holds:
  - colour and spacing roles (`Palette`, `Metrics`);
  - fonts (`UiFonts`);
  - a scoped theme (`Theme`);
  - a `ThemedWindow` base;
  - static draw components that take plain data.
- **Windows.** The rail, session and settings windows are rebuilt on that layer, one task each.

**Tech Stack:** C# / .NET 10, Dalamud (ImGui bindings, `ImRaii`, managed font atlas), xUnit smoke tests.

**Spec:** `docs/superpowers/specs/2026-10-04-ui-foundation-design.md`. Read it before starting. Its decision numbers are cited below as "spec §n".

**How this plan was checked:** every code block below was built and tested in a scratch worktree, one commit per task, in this order. Each task left the solution building with 0 warnings and 0 errors, and all smoke tests passing. Full files are given for new code. Changed files are given as exact unified diffs against the state the previous task leaves. Apply a diff by hand, or save it and run `git apply`.

## Global Constraints

- **Smoke tests only, pre-release.** Add no tests beyond the one in Task 1: no regression, structure, reflection or tooling tests. Keep test comments to one line.
- **Build gate.** `dotnet build DungeonMasterXIV.sln` must report `0 Warning(s)` and `0 Error(s)`. Warnings are compared against `main`, not just errors.
- **Test gate.** Run `dotnet build` before `dotnet test`: the release test needs a built `DungeonMasterXIV.dll`. Expected totals after Task 1: Release.Tests 1, Tests 6, Relay.Tests 7, all passed, 0 skipped. `Passed!` alone proves nothing; compare Total and Skipped.
- **Copy is unchanged** except where a task shows new text. Existing wording carries spec obligations (session-layer R-1.7a, product-overview's honest limitations).
- **Parentheses mean only "the person behind the speaker"** (product-overview Session panel item 12). No role, count or label is ever drawn in parentheses.
- **Square brackets belong to the host's `[DM]` marker only** (rolls R-2.7a).
- **The theme applies to our windows only.** It is pushed in `PreDraw` and popped in `PostDraw`, never left on the global ImGui style.
- **Colour is never the only signal.** Away, private, DM and danger always carry a word, badge or icon.
- **No text below the `Meta` role (Axis 12).**
- **Commit messages** use `-F` with a quoted heredoc, because backticks inside `-m "..."` are executed by the shell. End each with: `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`

## Review Focus

These are the inputs most likely to hurt someone, which no test in this plan exercises. The project's smoke-tests-only rule forbids adding tests for them, so each is an explicit check in Task 7's in-game pass, and the owning task's reviewer reads its code for it:

1. **A huge roll in a session** (`/roll 600d6`): refused with the 500-dice cap named, and nothing sent. A frame over 64 KiB would make the relay drop that player's connection. *Owner: Task 1 (`SharedRoll.RefusalToShare`, `MemberMessage.ShareRoll`, `SessionCoordinator.ShareRoll`).*
2. **Reconnecting after many long messages:** catch-up goes out in frames of at most 24 KiB, never one giant frame. *Owner: Task 1 (`RosterBroadcast.Batches`).*
3. **A narrow window (420px) or Dalamud font scale 1.5:**
   - Host and Join stack;
   - the speaker line's time doesn't overlap the name;
   - `Eli (Tuka)` is never cut short.

   *Owner: Tasks 3 and 5.*
4. **A missing font file:** that role falls back to Dalamud's default and logs one warning. No crash, no blank text. *Owner: Task 2 (`UiFonts.Bundled`).*
5. **A window that throws while drawing:** one log line, a one-line notice in that window, and no colour or style leaking into other plugins. *Owner: Task 2 (`ThemedWindow.Draw`).*

## File map

| Path | Task | Responsibility |
| --- | --- | --- |
| `src/DungeonMasterXIV.Core/Net/SharedRoll.cs` | 1 | A roll as it travels: expression, label, every die, total, notice. Bounds, share cap, log summary. |
| `src/DungeonMasterXIV.Core/Net/HostStream.cs` | 1 | Stamp a host entry and send it to every member in one step. |
| `src/DungeonMasterXIV.Core/Net/SpeakerBook.cs` | 1 | `SpeakerName`, plus remembering each peer's name and role from rosters seen. |
| `Net/StreamLine.cs`, `Net/StreamEntry.cs`, `Net/SessionContent.cs`, `Net/SessionContentCodec.cs`, `Net/SessionRecording.cs` | 1 | Carry an optional `SharedRoll`. |
| `Net/MemberMessage.cs`, `Net/SessionMembership.cs`, `Net/MemberContentReceipts.cs` | 1 | Member roll path, plus a refused-roll counter. |
| `Net/InboundWiring.cs`, `Net/SessionWiring.cs`, `Net/RosterBroadcast.cs`, `Net/SessionCoordinator.cs` | 1 | Host handling of rolls; membership announcements; batched catch-up; host `Say`/`ShareRoll`; what the UI reads. |
| `tests/DungeonMasterXIV.Tests/ARollReachesEveryMemberWithItsDiceTests.cs` | 1 | The one new smoke test. |
| `DungeonMasterXIV.csproj`, `Data/Fonts/*` | 2 | ImPlot reference; bundled fonts and licences copied to output. |
| `Windows/Ui/{Palette,Metrics,UiFonts,Theme,ThemedWindow}.cs` | 2 | Design language and the themed window base. |
| `Windows/Ui/Components/*.cs` | 3 | `Card`, `Speaker`, `RoleBadge`, `MessageCard`, `EventLine`, `RollCard`, `Banner`, `ActionRow` + `DangerAction`, `RosterRow`, `CodeDisplay`, `EmptyState`, `RailButton`, `Section`. |
| `Windows/RailWindow.cs`, `Data/PluginSettings.cs`, `Plugin.cs`; delete `Windows/MainWindow.cs` | 4 | The rail replaces the main window. |
| `Windows/{SessionWindow,StreamView,MessageComposeView,AdmissionPromptView,JoinFlowView,SessionEndingView,JoinRequestForm}.cs`, `Plugin.cs`; delete `Windows/RosterView.cs` | 5 | Session window rebuilt; the stream on screen. |
| `Windows/{ConfigWindow,CampaignStorageView,RelinkMemoryView}.cs`, `Plugin.cs` | 6 | Settings window rebuilt. |
| (no files) | 7 | Whole-branch gates and the in-game pass. |

---

### Task 1: The stream carries rolls, membership events and the DM's own lines

Implements spec §5, plus the Core half of §6. Afterwards:
- a member's `/roll` reaches every member with each die;
- joins, departures, drops and reconnects are announced;
- the DM can send messages and rolls;
- catch-up is batched;
- the coordinator exposes what the session window will read.

**Files:**
- Create: `src/DungeonMasterXIV.Core/Net/SharedRoll.cs`, `src/DungeonMasterXIV.Core/Net/HostStream.cs`, `src/DungeonMasterXIV.Core/Net/SpeakerBook.cs`
- Modify: `src/DungeonMasterXIV.Core/Net/{StreamLine,StreamEntry,SessionContent,SessionContentCodec,SessionRecording,MemberMessage,SessionMembership,MemberContentReceipts,SessionWiring,RosterBroadcast,InboundWiring,SessionCoordinator}.cs`
- Test: create `tests/DungeonMasterXIV.Tests/ARollReachesEveryMemberWithItsDiceTests.cs`; modify `tests/DungeonMasterXIV.Tests/BaseChatReachesEveryMemberTests.cs`

**Interfaces:**
- Consumes: `RollOutcome` (`Total`, `Dice`, `Label`, `Notice`), `RolledDie(int Sides, int Value, bool Kept)`, `RollLimits.Default`, `MessageDraft.Compose`, `RosterBroadcast.PublishEntry`, `SessionRecording.StampAsHost`.
- Produces (later tasks rely on these exact names):
  - `public sealed record SharedRoll(string Expression, string? Label, IReadOnlyList<RolledDie> Dice, int Total, string? Notice)`, with:
    - `MaxDice = 500`;
    - `static SharedRoll From(string expression, RollOutcome outcome)`;
    - `string? RefusalToShare()`;
    - `bool IsWithinBounds(RollLimits)`;
    - `string Summary()`.
  - `StreamLine(..., string Text, SharedRoll? Roll = null)` and `static StreamLine From(StreamEntry)`. `StreamEntry(..., string Text, SharedRoll? Roll = null)`.
  - `SessionContent.Rolling` (`SharedRoll?`).
  - `public readonly record struct SpeakerName(string Name, SessionRole Role)`, and `SpeakerBook`, with `Learn(IEnumerable<RosterEntry>)` and `SpeakerName For(string peer)`.
  - On `SessionCoordinator`:
    - `IReadOnlyList<StreamLine> StreamLines`;
    - `IReadOnlyList<RosterEntry> CurrentRoster`;
    - `bool InASession`;
    - `MessageDraft Say(string? text, DateTimeOffset now)`;
    - `string? ShareRoll(SharedRoll roll, DateTimeOffset now)`, which returns the refusal text, or null once the roll is shared.
  - `MemberContentReceipts.RefusedRolls`.

- [ ] **Step 1: Write the failing smoke test**

Create `tests/DungeonMasterXIV.Tests/ARollReachesEveryMemberWithItsDiceTests.cs`:

`tests/DungeonMasterXIV.Tests/ARollReachesEveryMemberWithItsDiceTests.cs` (new file, complete):

```csharp
using System;
using System.Linq;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Rolls;
using Xunit;
using static DungeonMasterXIV.Tests.BaseChatFixture;

namespace DungeonMasterXIV.Tests;

/// <summary>A member's roll reaches a different member with every die the roller rolled.</summary>
public sealed class ARollReachesEveryMemberWithItsDiceTests
{
    [Fact]
    public void ARollFromOneMemberReachesADifferentMemberWithItsDice()
    {
        var host = Hosting(out var transport);
        using var roller = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        var rollerCode = Admitted(host, Speaker, roller);
        Admitted(host, Listener, listener);

        var outcome = new RollEvaluator(new ScriptedDieRoller(3, 6, 1, 4)).Evaluate("4d6+2");
        transport.Deliver(SealedBy(roller, host, new SessionContent { Rolling = SharedRoll.From("4d6+2", outcome) }));
        host.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(
            StampedLinesFor(listener, host, transport), sent => sent.Kind == StreamEventKind.Roll);

        Assert.Equal(rollerCode.Value, line.Peer);
        Assert.NotNull(line.Roll);
        Assert.Equal(new[] { 3, 6, 1, 4 }, line.Roll!.Dice.Select(die => die.Value));
        Assert.Equal(3 + 6 + 1 + 4 + 2, line.Roll.Total);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet build DungeonMasterXIV.sln 2>&1 | grep -E 'error|Build succeeded' | sort -u`

Expected: compile errors naming `SharedRoll` and `Rolling`/`Roll`, which don't exist yet.

- [ ] **Step 3: Add `SharedRoll`**

Create `src/DungeonMasterXIV.Core/Net/SharedRoll.cs`:

`src/DungeonMasterXIV.Core/Net/SharedRoll.cs` (new file, complete):

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Rolls;

namespace DungeonMasterXIV.Net;

/// <summary>A roll as it travels to the session: what was typed, every die, the total and any notice.</summary>
public sealed record SharedRoll(
    string Expression,
    string? Label,
    IReadOnlyList<RolledDie> Dice,
    int Total,
    string? Notice)
{
    /// <summary>The most dice one shared roll carries, keeping its frame well under the relay's 64 KiB limit.</summary>
    public const int MaxDice = 500;

    public const int MaxLabelLength = 200;

    public const int MaxNoticeLength = 200;

    private const int SummaryExpressionLength = 100;

    private const int SummaryDice = 20;

    public static SharedRoll From(string expression, RollOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return new SharedRoll(expression, outcome.Label, outcome.Dice, outcome.Total, outcome.Notice);
    }

    /// <summary>Why this roll cannot be shared, or null when it can.</summary>
    public string? RefusalToShare() =>
        Dice.Count > MaxDice
            ? $"That roll used {Dice.Count} dice. A roll shared with the session can carry up to {MaxDice}."
            : null;

    public bool IsWithinBounds(RollLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        return Expression is not null
            && Expression.Length <= limits.MaxLength
            && (Label is null || Label.Length <= MaxLabelLength)
            && (Notice is null || Notice.Length <= MaxNoticeLength)
            && Dice is not null
            && Dice.Count <= MaxDice
            && Dice.All(die => die.Sides >= 1
                && die.Sides <= limits.MaxDieSize
                && Math.Abs(die.Value) <= limits.MaxDieSize);
    }

    /// <summary>A short plain-text form for the session log, such as "1d20+4 = 17 [13]".</summary>
    public string Summary()
    {
        var expression = Expression.Length > SummaryExpressionLength
            ? Expression[..SummaryExpressionLength] + "…"
            : Expression;

        var dice = string.Join(", ", Dice.Take(SummaryDice).Select(die => die.Kept ? $"{die.Value}" : $"{die.Value}*"));
        var more = Dice.Count > SummaryDice ? ", …" : string.Empty;

        return Dice.Count == 0 ? $"{expression} = {Total}" : $"{expression} = {Total} [{dice}{more}]";
    }
}
```

- [ ] **Step 4: Let stream lines, entries, content and the recording carry a roll**

`src/DungeonMasterXIV.Core/Net/StreamLine.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/StreamLine.cs b/src/DungeonMasterXIV.Core/Net/StreamLine.cs
index 6628149..303c326 100644
--- a/src/DungeonMasterXIV.Core/Net/StreamLine.cs
+++ b/src/DungeonMasterXIV.Core/Net/StreamLine.cs
@@ -1,3 +1,6 @@
+using System;
+using DungeonMasterXIV.Rolls;
+
 namespace DungeonMasterXIV.Net;
 
 /// <summary>A stream entry in the plain form sent inside session content, convertible back to an entry.</summary>
@@ -6,18 +9,29 @@ public readonly record struct StreamLine(
     long AtUtcTicks,
     StreamEventKind Kind,
     string Peer,
-    string Text)
+    string Text,
+    SharedRoll? Roll = null)
 {
+    public static StreamLine From(StreamEntry entry)
+    {
+        ArgumentNullException.ThrowIfNull(entry);
+
+        return new StreamLine(
+            entry.Stamp.Sequence, entry.Stamp.AtUtcTicks, entry.Kind, entry.Peer.Value, entry.Text, entry.Roll);
+    }
+
     public bool TryToEntry(out StreamEntry entry)
     {
         entry = default!;
 
-        if (Sequence < 1 || !PeerCode.TryParse(Peer, out var peer))
+        if (Sequence < 1
+            || !PeerCode.TryParse(Peer, out var peer)
+            || (Roll is not null && !Roll.IsWithinBounds(RollLimits.Default)))
         {
             return false;
         }
 
-        entry = new StreamEntry(new StreamStamp(Sequence, AtUtcTicks), Kind, peer, Text ?? string.Empty);
+        entry = new StreamEntry(new StreamStamp(Sequence, AtUtcTicks), Kind, peer, Text ?? string.Empty, Roll);
         return true;
     }
 }
```

`src/DungeonMasterXIV.Core/Net/StreamEntry.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/StreamEntry.cs b/src/DungeonMasterXIV.Core/Net/StreamEntry.cs
index c0e9985..4e6a9ab 100644
--- a/src/DungeonMasterXIV.Core/Net/StreamEntry.cs
+++ b/src/DungeonMasterXIV.Core/Net/StreamEntry.cs
@@ -5,4 +5,5 @@ public sealed record StreamEntry(
     StreamStamp Stamp,
     StreamEventKind Kind,
     PeerCode Peer,
-    string Text);
+    string Text,
+    SharedRoll? Roll = null);
```

`src/DungeonMasterXIV.Core/Net/SessionContent.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/SessionContent.cs b/src/DungeonMasterXIV.Core/Net/SessionContent.cs
index 27ab31d..962c993 100644
--- a/src/DungeonMasterXIV.Core/Net/SessionContent.cs
+++ b/src/DungeonMasterXIV.Core/Net/SessionContent.cs
@@ -13,6 +13,8 @@ public sealed class SessionContent
 
     public string? Saying { get; init; }
 
+    public SharedRoll? Rolling { get; init; }
+
     public IReadOnlyList<StreamLine>? Entries { get; init; }
 }
 
```

`src/DungeonMasterXIV.Core/Net/SessionContentCodec.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/SessionContentCodec.cs b/src/DungeonMasterXIV.Core/Net/SessionContentCodec.cs
index 5ec2e6e..3f8b465 100644
--- a/src/DungeonMasterXIV.Core/Net/SessionContentCodec.cs
+++ b/src/DungeonMasterXIV.Core/Net/SessionContentCodec.cs
@@ -27,6 +27,7 @@ public static class SessionContentCodec
             Leaving = content.Leaving,
             Entries = lines,
             Saying = content.Saying,
+            Rolling = content.Rolling,
         };
     }
 
```

`src/DungeonMasterXIV.Core/Net/SessionRecording.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/SessionRecording.cs b/src/DungeonMasterXIV.Core/Net/SessionRecording.cs
index 9e74b05..63697bd 100644
--- a/src/DungeonMasterXIV.Core/Net/SessionRecording.cs
+++ b/src/DungeonMasterXIV.Core/Net/SessionRecording.cs
@@ -17,10 +17,11 @@ internal sealed class SessionRecording
     public bool RecordAsHost(StreamEventKind kind, PeerCode peer, string text, DateTimeOffset at) =>
         StampAsHost(kind, peer, text, at) is not null;
 
-    public StreamEntry? StampAsHost(StreamEventKind kind, PeerCode peer, string text, DateTimeOffset at)
+    public StreamEntry? StampAsHost(
+        StreamEventKind kind, PeerCode peer, string text, DateTimeOffset at, SharedRoll? roll = null)
     {
         _at = at;
-        var entry = new StreamEntry(_sequencer.Next(), kind, peer, text);
+        var entry = new StreamEntry(_sequencer.Next(), kind, peer, text, roll);
         return Record(entry) ? entry : null;
     }
 
```

A received line whose roll breaks the bounds fails `TryToEntry`. The codec already drops and counts such lines without recording their content, as product-overview D-22 requires.

- [ ] **Step 5: The member's roll path, and a counter for refused rolls**

The member queue now holds whole `SessionContent` items, so a roll waits in order with messages while the path is down.

`src/DungeonMasterXIV.Core/Net/MemberMessage.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/MemberMessage.cs b/src/DungeonMasterXIV.Core/Net/MemberMessage.cs
index bc2e3a4..a46c344 100644
--- a/src/DungeonMasterXIV.Core/Net/MemberMessage.cs
+++ b/src/DungeonMasterXIV.Core/Net/MemberMessage.cs
@@ -4,14 +4,14 @@ using DungeonMasterXIV.Chat;
 
 namespace DungeonMasterXIV.Net;
 
-/// <summary>Checks a member's chat message and sends it to the host sealed, holding it while the path is down.</summary>
+/// <summary>Checks a member's chat message or roll and sends it to the host sealed, holding it while the path is down.</summary>
 internal sealed class MemberMessage
 {
     private readonly RelayLink _link;
     private readonly Func<SessionCode?> _code;
     private readonly Func<byte[]?> _sessionKey;
     private readonly Func<bool> _pathDown;
-    private readonly Queue<string> _waiting = new();
+    private readonly Queue<SessionContent> _waiting = new();
 
     public MemberMessage(RelayLink link, Func<SessionCode?> code, Func<byte[]?> sessionKey, Func<bool> pathDown)
     {
@@ -41,16 +41,36 @@ internal sealed class MemberMessage
             return new MessageDraft(null, MessageFault.NotInASession, "This client is not in a session.");
         }
 
-        _waiting.Enqueue(draft.Text!);
+        _waiting.Enqueue(new SessionContent { Saying = draft.Text! });
         Flush();
         return draft;
     }
 
+    /// <summary>Queues a roll for the host; returns why it cannot be shared, or null once it is queued.</summary>
+    public string? ShareRoll(SharedRoll roll)
+    {
+        ArgumentNullException.ThrowIfNull(roll);
+
+        if (roll.RefusalToShare() is { } refusal)
+        {
+            return refusal;
+        }
+
+        if (_code() is null || _sessionKey() is null)
+        {
+            return "This client is not in a session.";
+        }
+
+        _waiting.Enqueue(new SessionContent { Rolling = roll });
+        Flush();
+        return null;
+    }
+
     public void Flush()
     {
         while (_waiting.Count > 0 && !_pathDown() && _code() is { } code && _sessionKey() is { } key)
         {
-            var plaintext = SessionContentCodec.Encode(new SessionContent { Saying = _waiting.Dequeue() });
+            var plaintext = SessionContentCodec.Encode(_waiting.Dequeue());
             var sealedPayload = SessionCipher.Seal(
                 key, plaintext, WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));
             _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForSessionPayload(code, sealedPayload)));
```

`src/DungeonMasterXIV.Core/Net/SessionMembership.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/SessionMembership.cs b/src/DungeonMasterXIV.Core/Net/SessionMembership.cs
index 285377a..468c095 100644
--- a/src/DungeonMasterXIV.Core/Net/SessionMembership.cs
+++ b/src/DungeonMasterXIV.Core/Net/SessionMembership.cs
@@ -36,6 +36,8 @@ public sealed class SessionMembership
     public MessageDraft Say(string? text, MessageLimits? limits = null) =>
         _message.Say(text, limits ?? MessageLimits.Default);
 
+    public string? ShareRoll(SharedRoll roll) => _message.ShareRoll(roll);
+
     public SessionClosing? Closing => _closing.Notice;
 
     public int Undelivered { get; internal set; }
```

`src/DungeonMasterXIV.Core/Net/MemberContentReceipts.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/MemberContentReceipts.cs b/src/DungeonMasterXIV.Core/Net/MemberContentReceipts.cs
index 1118ef8..e76fff4 100644
--- a/src/DungeonMasterXIV.Core/Net/MemberContentReceipts.cs
+++ b/src/DungeonMasterXIV.Core/Net/MemberContentReceipts.cs
@@ -2,6 +2,7 @@ using System;
 using System.Collections.Generic;
 using System.Linq;
 using DungeonMasterXIV.Chat;
+using DungeonMasterXIV.Rolls;
 
 namespace DungeonMasterXIV.Net;
 
@@ -14,6 +15,7 @@ public sealed class MemberContentReceipts
     private readonly Dictionary<string, MemberContentReceipt> _latest = new(StringComparer.Ordinal);
     private int _received;
     private int _refusedSayings;
+    private int _refusedRolls;
     private int _refusedRosters;
     private int _refusedEntries;
 
@@ -21,6 +23,8 @@ public sealed class MemberContentReceipts
 
     public int RefusedSayings => _refusedSayings;
 
+    public int RefusedRolls => _refusedRolls;
+
     public int RefusedRosters => _refusedRosters;
 
     public int RefusedEntries => _refusedEntries;
@@ -52,6 +56,11 @@ public sealed class MemberContentReceipts
             }
         }
 
+        if (content.Rolling is { } roll && !roll.IsWithinBounds(RollLimits.Default))
+        {
+            _refusedRolls = Counted(_refusedRolls);
+        }
+
         if (content.Roster is not null)
         {
             _refusedRosters = Counted(_refusedRosters);
@@ -77,6 +86,7 @@ public sealed class MemberContentReceipts
         _latest.Clear();
         _received = 0;
         _refusedSayings = 0;
+        _refusedRolls = 0;
         _refusedRosters = 0;
         _refusedEntries = 0;
     }
```

- [ ] **Step 6: `HostStream`, and wiring it**

Create `src/DungeonMasterXIV.Core/Net/HostStream.cs`:

`src/DungeonMasterXIV.Core/Net/HostStream.cs` (new file, complete):

```csharp
using System;

namespace DungeonMasterXIV.Net;

/// <summary>Stamps a host stream entry and sends it to every admitted member in the same step.</summary>
internal sealed class HostStream(SessionRecording recording, RosterBroadcast roster)
{
    public StreamEntry? Announce(
        StreamEventKind kind, PeerCode peer, string text, DateTimeOffset at, SharedRoll? roll = null)
    {
        if (recording.StampAsHost(kind, peer, text, at, roll) is not { } entry)
        {
            return null;
        }

        roster.PublishEntry(StreamLine.From(entry));
        return entry;
    }
}
```

`SessionWiring` builds one `HostIdentity`, shared by the roster broadcast and the coordinator's own send path, and one `HostStream`:

`src/DungeonMasterXIV.Core/Net/SessionWiring.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/SessionWiring.cs b/src/DungeonMasterXIV.Core/Net/SessionWiring.cs
index ec64e29..ffd2cbc 100644
--- a/src/DungeonMasterXIV.Core/Net/SessionWiring.cs
+++ b/src/DungeonMasterXIV.Core/Net/SessionWiring.cs
@@ -33,11 +33,9 @@ internal sealed class SessionWiring
             () => Interruption?.Grace.IsRunning == true,
             () => Joiner?.SessionKey,
             lastSequence);
-        Roster = new RosterBroadcast(
-            Link,
-            Admissions.Audience,
-            HostIdentity.ForHost(() => HostKeys, () => Host.Code, capabilities.HostNameSource, Admissions.PeerCodeFor),
-            log);
+        HostIdentity = HostIdentity.ForHost(
+            () => HostKeys, () => Host.Code, capabilities.HostNameSource, Admissions.PeerCodeFor);
+        Roster = new RosterBroadcast(Link, Admissions.Audience, HostIdentity, log);
         Resources = new SessionResources(
             Admissions,
             Inbox,
@@ -49,6 +47,7 @@ internal sealed class SessionWiring
         Membership = new SessionMembership(
             Link, Joiner, () => Join.Code, () => !Link.IsReadyToSend || Interruption.Reconnecting);
         Hosting = new HostRunner(Host, Resources, Handshake, newKeys, SynchroniseTransport);
+        Stream = new HostStream(Resources.Recording, Roster);
     }
 
     internal HostSession Host { get; } = new();
@@ -77,8 +76,12 @@ internal sealed class SessionWiring
 
     internal OutboundHandshake Handshake { get; }
 
+    internal HostIdentity HostIdentity { get; }
+
     internal RosterBroadcast Roster { get; }
 
+    internal HostStream Stream { get; }
+
     internal SessionResources Resources { get; }
 
     internal SessionInterruption Interruption { get; }
```

`RosterBroadcast` exposes the roster it sends as `Current()`, so the host's window can draw the same list. It also splits catch-up into frames under 24 KiB, because the relay drops any connection that sends more than 64 KiB at once:

`src/DungeonMasterXIV.Core/Net/RosterBroadcast.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/RosterBroadcast.cs b/src/DungeonMasterXIV.Core/Net/RosterBroadcast.cs
index 9ce60e0..8277106 100644
--- a/src/DungeonMasterXIV.Core/Net/RosterBroadcast.cs
+++ b/src/DungeonMasterXIV.Core/Net/RosterBroadcast.cs
@@ -13,6 +13,8 @@ internal sealed class RosterBroadcast
     private readonly HostIdentity _host;
     private readonly ISessionTransportLog _log;
 
+    private const int CatchUpBatchBytes = 24 * 1024;
+
     public RosterBroadcast(
         RelayLink link,
         SessionAudience audience,
@@ -32,21 +34,27 @@ internal sealed class RosterBroadcast
             return;
         }
 
-        var roster = _audience.Recipients
-            .Select(peer => new RosterEntry(peer.PeerCode.Value, peer.DisplayName.Value, peer.Role))
-            .ToList();
-
-        if (roster.Count == 0)
+        if (_audience.Recipients.Count == 0)
         {
             return;
         }
 
+        SealToEveryRecipient(new SessionContent { Roster = Current() }, keys, code);
+    }
+
+    /// <summary>The host first, as Dungeon Master, then every admitted member.</summary>
+    public List<RosterEntry> Current()
+    {
+        var roster = _audience.Recipients
+            .Select(peer => new RosterEntry(peer.PeerCode.Value, peer.DisplayName.Value, peer.Role))
+            .ToList();
+
         if (_host.OwnPeerCode() is { } ownCode)
         {
             roster.Insert(0, new RosterEntry(ownCode.Value, _host.Name().Value, SessionRole.DungeonMaster));
         }
 
-        SealToEveryRecipient(new SessionContent { Roster = roster }, keys, code);
+        return roster;
     }
 
     public void PublishClosing(SessionClosing closing)
@@ -80,8 +88,37 @@ internal sealed class RosterBroadcast
             return;
         }
 
-        var plaintext = SessionContentCodec.Encode(new SessionContent { Entries = lines });
-        SealTo(peer, plaintext, WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload), keys, code);
+        var associatedData = WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload);
+        foreach (var batch in Batches(lines))
+        {
+            SealTo(peer, SessionContentCodec.Encode(new SessionContent { Entries = batch }), associatedData, keys, code);
+        }
+    }
+
+    /// <summary>Splits catch-up lines so no sealed frame nears the relay's 64 KiB message limit.</summary>
+    private static IEnumerable<List<StreamLine>> Batches(IReadOnlyList<StreamLine> lines)
+    {
+        var batch = new List<StreamLine>();
+        var size = 0;
+
+        foreach (var line in lines)
+        {
+            var lineSize = SessionContentCodec.Encode(new SessionContent { Entries = new[] { line } }).Length;
+            if (batch.Count > 0 && size + lineSize > CatchUpBatchBytes)
+            {
+                yield return batch;
+                batch = new List<StreamLine>();
+                size = 0;
+            }
+
+            batch.Add(line);
+            size += lineSize;
+        }
+
+        if (batch.Count > 0)
+        {
+            yield return batch;
+        }
     }
 
     private void SealToEveryRecipient(SessionContent content, SessionKeyExchange keys, SessionCode code)
```

- [ ] **Step 7: The host handles rolls and announces membership**

In `InboundWiring`:
- a member's in-bounds roll is announced; an out-of-bounds one is dropped (it was already counted by `MemberContentReceipts`);
- an automatic admit announces `Joined`;
- a resume sends the catch-up, then announces `Reconnected`;
- a recorded drop announces `Dropped`;
- a departure is now sent to members as well as recorded.

`src/DungeonMasterXIV.Core/Net/InboundWiring.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/InboundWiring.cs b/src/DungeonMasterXIV.Core/Net/InboundWiring.cs
index 973a501..bfc937b 100644
--- a/src/DungeonMasterXIV.Core/Net/InboundWiring.cs
+++ b/src/DungeonMasterXIV.Core/Net/InboundWiring.cs
@@ -1,6 +1,7 @@
 using System;
 using System.Linq;
 using DungeonMasterXIV.Chat;
+using DungeonMasterXIV.Rolls;
 
 namespace DungeonMasterXIV.Net;
 
@@ -10,6 +11,7 @@ internal sealed class InboundWiring(
     SessionResources resources,
     Func<string?, RelinkClaim> resolveRelink,
     RosterBroadcast roster,
+    HostStream stream,
     Action onReclaimed,
     Action onReclaimRefused,
     Action onHostAway,
@@ -36,6 +38,7 @@ internal sealed class InboundWiring(
                     {
                         admissions.Admit(request.PeerCode, asClaimed: true);
                         roster.Publish();
+                        stream.Announce(StreamEventKind.Joined, request.PeerCode, string.Empty, now);
                     }
                 },
                 OnResume: (key, envelope) =>
@@ -47,12 +50,12 @@ internal sealed class InboundWiring(
 
                     var missed = resources.Recording.Entries
                         .Where(entry => entry.Stamp.Sequence > resumed.LastSequence)
-                        .Select(entry => new StreamLine(
-                            entry.Stamp.Sequence, entry.Stamp.AtUtcTicks, entry.Kind, entry.Peer.Value, entry.Text))
+                        .Select(StreamLine.From)
                         .ToList();
 
                     roster.Publish();
                     roster.PublishEntriesTo(resumed.Peer, missed);
+                    stream.Announce(StreamEventKind.Reconnected, resumed.Peer, string.Empty, now);
                 }),
             HostAuthored: new HostAuthoredContent(
                 OpenWith: sessionKey,
@@ -64,16 +67,23 @@ internal sealed class InboundWiring(
                     resources.MemberContent.Record(peer, content);
 
                     Said(peer, content, now);
+                    Rolled(peer, content, now);
 
                     if (content.Leaving is true)
                     {
-                        resources.Recording.RecordAsHost(StreamEventKind.Left, peer, string.Empty, now);
+                        stream.Announce(StreamEventKind.Left, peer, string.Empty, now);
 
                         admissions.Departed(peer);
                     }
                 }),
             Transport: new TransportNotices(
-                OnConnectionDropped: key => admissions.RecordDrop(key, now),
+                OnConnectionDropped: key =>
+                {
+                    if (admissions.RecordDrop(key, now))
+                    {
+                        stream.Announce(StreamEventKind.Dropped, admissions.PeerCodeFor(key), string.Empty, now);
+                    }
+                },
                 OnReclaimed: onReclaimed,
                 OnReclaimRefused: onReclaimRefused,
                 OnHostAway: onHostAway,
@@ -93,12 +103,16 @@ internal sealed class InboundWiring(
             return;
         }
 
-        if (resources.Recording.StampAsHost(StreamEventKind.Message, peer, said, now) is not { } entry)
+        stream.Announce(StreamEventKind.Message, peer, said, now);
+    }
+
+    private void Rolled(PeerCode peer, SessionContent content, DateTimeOffset now)
+    {
+        if (content.Rolling is not { } roll || !roll.IsWithinBounds(RollLimits.Default))
         {
             return;
         }
 
-        roster.PublishEntry(new StreamLine(
-            entry.Stamp.Sequence, entry.Stamp.AtUtcTicks, entry.Kind, entry.Peer.Value, entry.Text));
+        stream.Announce(StreamEventKind.Roll, peer, roll.Summary(), now, roll);
     }
 }
```

- [ ] **Step 8: The coordinator: announce manual admits, host send path, what the UI reads**

`src/DungeonMasterXIV.Core/Net/SessionCoordinator.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Net/SessionCoordinator.cs b/src/DungeonMasterXIV.Core/Net/SessionCoordinator.cs
index acfc6f2..5a3fc76 100644
--- a/src/DungeonMasterXIV.Core/Net/SessionCoordinator.cs
+++ b/src/DungeonMasterXIV.Core/Net/SessionCoordinator.cs
@@ -2,6 +2,7 @@ using System;
 using System.Security.Cryptography;
 using System.Collections.Generic;
 using System.Linq;
+using DungeonMasterXIV.Chat;
 
 namespace DungeonMasterXIV.Net;
 
@@ -59,6 +60,63 @@ public sealed class SessionCoordinator
     public MemberContentReceipts MemberContent => _resources.MemberContent;
     public IReadOnlyList<StreamEntry> Recorded => _resources.Recording.Entries;
 
+    /// <summary>This client's session stream: what the host recorded, or what a member was sent.</summary>
+    public IReadOnlyList<StreamLine> StreamLines =>
+        InAHostedSession ? Recorded.Select(StreamLine.From).ToList() : Received;
+
+    /// <summary>Who is in the session now: the host's own list, or the roster a member was sent.</summary>
+    public IReadOnlyList<RosterEntry> CurrentRoster => InAHostedSession ? _roster.Current() : Roster;
+
+    /// <summary>True while this client can send to a session, as its host or as an admitted member.</summary>
+    public bool InASession => InAHostedSession || Join.Phase == JoinPhase.Admitted;
+
+    /// <summary>Sends a message to the session: stamped directly when hosting, sealed to the host otherwise.</summary>
+    public MessageDraft Say(string? text, DateTimeOffset now)
+    {
+        if (!InAHostedSession)
+        {
+            return Membership.Say(text);
+        }
+
+        var draft = MessageDraft.Compose(text, MessageLimits.Default);
+        if (!draft.IsAccepted)
+        {
+            return draft;
+        }
+
+        if (_parts.HostIdentity.OwnPeerCode() is not { } own)
+        {
+            return new MessageDraft(null, MessageFault.NotInASession, "This client is not in a session.");
+        }
+
+        _parts.Stream.Announce(StreamEventKind.Message, own, draft.Text!, now);
+        return draft;
+    }
+
+    /// <summary>Shares a roll this client made; returns why it was not shared, or null.</summary>
+    public string? ShareRoll(SharedRoll roll, DateTimeOffset now)
+    {
+        ArgumentNullException.ThrowIfNull(roll);
+
+        if (!InAHostedSession)
+        {
+            return Membership.ShareRoll(roll);
+        }
+
+        if (roll.RefusalToShare() is { } refusal)
+        {
+            return refusal;
+        }
+
+        if (_parts.HostIdentity.OwnPeerCode() is not { } own)
+        {
+            return "This client is not in a session.";
+        }
+
+        _parts.Stream.Announce(StreamEventKind.Roll, own, roll.Summary(), now, roll);
+        return null;
+    }
+
     public HostSession Host => _parts.Host;
 
     public JoinAttempt Join => _parts.Join;
@@ -109,6 +167,7 @@ public sealed class SessionCoordinator
         var peer = _admissions.Admit(peerCode, role, asClaimed);
 
         _roster.Publish();
+        _parts.Stream.Announce(StreamEventKind.Joined, peerCode, string.Empty, DateTimeOffset.UtcNow);
         return peer;
     }
 
@@ -125,7 +184,7 @@ public sealed class SessionCoordinator
             Join,
             Membership.Keys,
             Host,
-            new InboundWiring(_admissions, _resources, _resolveRelink, _roster, Reclaimed, ReclaimRefused, HostWentAway, HostCameBack)
+            new InboundWiring(_admissions, _resources, _resolveRelink, _roster, _parts.Stream, Reclaimed, ReclaimRefused, HostWentAway, HostCameBack)
                 .For(now, Membership.SessionKey, content => HeardFromTheHost(content)),
             _log)
             ?? Membership.SessionKey;
```

Create `src/DungeonMasterXIV.Core/Net/SpeakerBook.cs`:

`src/DungeonMasterXIV.Core/Net/SpeakerBook.cs` (new file, complete):

```csharp
using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>A stream line's speaker as drawn: the display name the session knows them by, and their role.</summary>
public readonly record struct SpeakerName(string Name, SessionRole Role);

/// <summary>Remembers each peer's name and role from the rosters seen, so a line still names someone who has left.</summary>
public sealed class SpeakerBook
{
    private readonly Dictionary<string, SpeakerName> _known = new(StringComparer.Ordinal);

    public void Learn(IEnumerable<RosterEntry> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        foreach (var entry in roster)
        {
            _known[entry.PeerCode] = new SpeakerName(DisplayName.OrNone(entry.DisplayName).Value, entry.Role);
        }
    }

    public SpeakerName For(string peer) =>
        _known.TryGetValue(peer, out var known) ? known : new SpeakerName(DisplayName.Unstated, SessionRole.Player);
}
```

- [ ] **Step 9: Run the tests**

Run: `dotnet build DungeonMasterXIV.sln 2>&1 | grep -E '^ *[0-9]+ (Warning|Error)' ; dotnet test tests/DungeonMasterXIV.Tests --no-build 2>&1 | grep -E 'Passed!|Failed!|Assert'`

Expected:
- the build has 0 warnings and 0 errors;
- the new roll test **passes**;
- `BaseChatReachesEveryMemberTests` **fails** with `Assert.Single() Failure: The collection contained 2 items`, because the listener now also receives its own `Joined` line.

- [ ] **Step 10: Filter the chat smoke test to message lines**

`tests/DungeonMasterXIV.Tests/BaseChatReachesEveryMemberTests.cs` (diff):

```diff
diff --git a/tests/DungeonMasterXIV.Tests/BaseChatReachesEveryMemberTests.cs b/tests/DungeonMasterXIV.Tests/BaseChatReachesEveryMemberTests.cs
index b437ccc..25441f0 100644
--- a/tests/DungeonMasterXIV.Tests/BaseChatReachesEveryMemberTests.cs
+++ b/tests/DungeonMasterXIV.Tests/BaseChatReachesEveryMemberTests.cs
@@ -20,7 +20,8 @@ public sealed class BaseChatReachesEveryMemberTests
         transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = "the door is trapped" }));
         host.Tick(TimeSpan.Zero, Now);
 
-        var line = Assert.Single(StampedLinesFor(listener, host, transport));
+        var line = Assert.Single(
+            StampedLinesFor(listener, host, transport), sent => sent.Kind == StreamEventKind.Message);
 
         Assert.Equal("the door is trapped", line.Text);
         Assert.Equal(speakerCode.Value, line.Peer);
```

- [ ] **Step 11: Run the whole suite**

Run: `dotnet build DungeonMasterXIV.sln 2>&1 | grep -E '^ *[0-9]+ (Warning|Error)' ; dotnet test DungeonMasterXIV.sln --no-build 2>&1 | grep -E 'Passed!|Failed!'`

Expected:
- 0 warnings and 0 errors;
- Release.Tests Total 1, Tests Total 6, Relay.Tests Total 7, all passed, Skipped 0.

- [ ] **Step 12: Commit**

```bash
git add src/DungeonMasterXIV.Core tests/DungeonMasterXIV.Tests
git commit -F - <<'EOF'
feat: rolls, membership events and the DM's own lines travel through the host

A member's /roll goes to the host as a SharedRoll with every die; the host checks its bounds and
announces it to everyone. Joins, departures, drops and reconnects are announced the same way, the DM
can send messages and rolls, and resume catch-up is batched under the relay's frame limit.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

- [ ] **Step 13: Prove the new test can fail, then restore from git**

```bash
sed -i.bak 's/^                    Rolled(peer, content, now);$/                    \/\/ mutated/' src/DungeonMasterXIV.Core/Net/InboundWiring.cs && rm src/DungeonMasterXIV.Core/Net/InboundWiring.cs.bak
grep -c 'mutated' src/DungeonMasterXIV.Core/Net/InboundWiring.cs
dotnet test tests/DungeonMasterXIV.Tests 2>&1 | grep -E 'Passed!|Failed!'
git checkout -- src/DungeonMasterXIV.Core/Net/InboundWiring.cs
git status --short
dotnet test tests/DungeonMasterXIV.Tests 2>&1 | grep -E 'Passed!|Failed!'
```

Expected, in order:
- `grep` prints `1`, so the mutation landed;
- the first run shows `Failed: 1, Passed: 5`;
- `git status --short` prints nothing;
- the second run shows `Failed: 0, Passed: 6`.

---

### Task 2: Chronicle design language and the themed window base

Implements spec §1, §2 and §6 (`Theme`, `UiFonts`, `ThemedWindow`). Nothing uses it yet; Task 4 wires it in.

**Files:**
- Modify: `DungeonMasterXIV.csproj`
- Create: `Data/Fonts/Cinzel-Regular.ttf`, `Data/Fonts/Cinzel-Bold.ttf`, `Data/Fonts/Cinzel-OFL.txt`, `Data/Fonts/Spectral-Regular.ttf`, `Data/Fonts/Spectral-OFL.txt`
- Create: `Windows/Ui/Palette.cs`, `Windows/Ui/Metrics.cs`, `Windows/Ui/UiFonts.cs`, `Windows/Ui/Theme.cs`, `Windows/Ui/ThemedWindow.cs`

**Interfaces:**
- Consumes: Dalamud `IUiBuilder` (`FontAtlas`, `IconFontHandle`), `IPluginLog`, `ImRaii`, `ImGuiHelpers.GlobalScale`.
- Produces:
  - `internal static class Palette`, with `Vector4` fields `Surface`, `SurfaceRaised`, `SurfaceHover`, `SurfaceSunk`, `Rule`, `RuleSoft`, `RuleStrong`, `Text`, `TextMuted`, `Gold`, `GoldBright`, `GoldLabel`, `PrivateSurface`, `PrivateRule`, `PrivateText`, `Warning`, `Danger`.
  - `internal static class Metrics`, with `Step`, `CardPadding`, `CardGap`, `ControlRounding`, `WindowRounding` and `EdgeWidth` (floats, already scaled).
  - `internal sealed class UiFonts : IDisposable`, with `IFontHandle` properties `Body`, `Meta`, `Title`, `Code`, `Total`, `Voice` and `Icon`, and the constructor `UiFonts(IUiBuilder ui, string fontDirectory, IPluginLog log)`.
  - `internal static class Theme`, with `IDisposable Push()`.
  - `internal abstract class ThemedWindow : Window`:
    - constructor `ThemedWindow(string name, UiFonts fonts, IPluginLog log, ImGuiWindowFlags flags = ImGuiWindowFlags.None)`;
    - protected `Fonts` and `Log`;
    - `protected abstract void DrawContent()`.

    `Draw` is sealed; subclasses override `DrawContent`. A subclass overriding `PreDraw` must call `base.PreDraw()`.

- [ ] **Step 1: Reference ImPlot and ship the fonts folder**

`ImRaii.PushColor` and `ImRaii.PushStyle` have ImPlot overloads. Without this reference, the compiler fails with `CS0012: The type 'ImPlotCol' is defined in an assembly that is not referenced`.

`DungeonMasterXIV.csproj` (diff):

```diff
diff --git a/DungeonMasterXIV.csproj b/DungeonMasterXIV.csproj
index 847d970..7d9b1a1 100644
--- a/DungeonMasterXIV.csproj
+++ b/DungeonMasterXIV.csproj
@@ -62,6 +62,11 @@
     <None Remove="tools/**" />
   </ItemGroup>
 
+  <!-- The bundled fonts and their licences ship next to the plugin, so the release zip carries them. -->
+  <ItemGroup>
+    <None Update="Data/Fonts/*.ttf;Data/Fonts/*.txt" CopyToOutputDirectory="PreserveNewest" />
+  </ItemGroup>
+
   <!-- References the Dalamud-free half of the plugin. -->
   <ItemGroup>
     <ProjectReference Include="src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj" />
@@ -81,6 +86,11 @@
       <HintPath>$(DalamudLibPath)Dalamud.Bindings.ImGui.dll</HintPath>
       <Private>false</Private>
     </Reference>
+    <!-- ImRaii's colour and style pushes have ImPlot overloads, so the compiler needs this to pick one. -->
+    <Reference Include="Dalamud.Bindings.ImPlot">
+      <HintPath>$(DalamudLibPath)Dalamud.Bindings.ImPlot.dll</HintPath>
+      <Private>false</Private>
+    </Reference>
     <Reference Include="Newtonsoft.Json">
       <HintPath>$(DalamudLibPath)Newtonsoft.Json.dll</HintPath>
       <Private>false</Private>
```

- [ ] **Step 2: Download the fonts and licences, and check they are the files this plan was built with**

```bash
mkdir -p Data/Fonts && cd Data/Fonts
curl -sSfL -o Cinzel-Regular.ttf https://raw.githubusercontent.com/NDISCOVER/Cinzel/master/fonts/ttf/Cinzel-Regular.ttf
curl -sSfL -o Cinzel-Bold.ttf https://raw.githubusercontent.com/NDISCOVER/Cinzel/master/fonts/ttf/Cinzel-Bold.ttf
curl -sSfL -o Cinzel-OFL.txt https://raw.githubusercontent.com/NDISCOVER/Cinzel/master/OFL.txt
curl -sSfL -o Spectral-Regular.ttf https://raw.githubusercontent.com/google/fonts/main/ofl/spectral/Spectral-Regular.ttf
curl -sSfL -o Spectral-OFL.txt https://raw.githubusercontent.com/google/fonts/main/ofl/spectral/OFL.txt
shasum -a 256 *.ttf
cd ../..
```

Expected:

```
0c23ec565db45c5508ee95889c60ad87debd167ca07167a43a5d68572b4e2eac  Cinzel-Bold.ttf
af0031129f27dc752e8629a80b793d27abea94027faa27cc660c3fc33f607a1f  Cinzel-Regular.ttf
c89021dc20720c8d0dcf40b0b2f6e00c13665fa8041717f581396f51b8c78f5d  Spectral-Regular.ttf
```

If a hash differs, upstream has changed the file. Stop and tell the person running the plan; don't substitute another file. Both `.txt` files must begin with the project's copyright line and "SIL Open Font License, Version 1.1".

- [ ] **Step 3: Colour and spacing roles**

Create `Windows/Ui/Palette.cs`:

`Windows/Ui/Palette.cs` (new file, complete):

```csharp
using System.Globalization;
using System.Numerics;

namespace DungeonMasterXIV.Windows.Ui;

/// <summary>The Chronicle colour roles. Components use these names, never raw values.</summary>
internal static class Palette
{
    public static readonly Vector4 Surface = Hex("17140F");
    public static readonly Vector4 SurfaceRaised = Hex("221D15");
    public static readonly Vector4 SurfaceHover = Hex("2A2318");
    public static readonly Vector4 SurfaceSunk = Hex("120F0B");
    public static readonly Vector4 Rule = Hex("6B5631");
    public static readonly Vector4 RuleSoft = Hex("3A3020");
    public static readonly Vector4 RuleStrong = Hex("8A7040");
    public static readonly Vector4 Text = Hex("E9DFC9");
    public static readonly Vector4 TextMuted = Hex("B5A888");
    public static readonly Vector4 Gold = Hex("C9A65A");
    public static readonly Vector4 GoldBright = Hex("F2D891");
    public static readonly Vector4 GoldLabel = Hex("D8BF86");
    public static readonly Vector4 PrivateSurface = Hex("1F1A22");
    public static readonly Vector4 PrivateRule = Hex("6A5478");
    public static readonly Vector4 PrivateText = Hex("CFC2DC");
    public static readonly Vector4 Warning = Hex("D9824A");
    public static readonly Vector4 Danger = Hex("D9665A");

    private static Vector4 Hex(string rgb) => new(
        int.Parse(rgb[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f,
        int.Parse(rgb[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f,
        int.Parse(rgb[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f,
        1f);
}
```

Create `Windows/Ui/Metrics.cs`:

`Windows/Ui/Metrics.cs` (new file, complete):

```csharp
using Dalamud.Interface.Utility;

namespace DungeonMasterXIV.Windows.Ui;

/// <summary>The 4px spacing grid and corner radii, scaled by Dalamud's global scale.</summary>
internal static class Metrics
{
    public static float Step => 4f * ImGuiHelpers.GlobalScale;

    public static float CardPadding => 10f * ImGuiHelpers.GlobalScale;

    public static float CardGap => 8f * ImGuiHelpers.GlobalScale;

    public static float ControlRounding => 4f * ImGuiHelpers.GlobalScale;

    public static float WindowRounding => 6f * ImGuiHelpers.GlobalScale;

    public static float EdgeWidth => 3f * ImGuiHelpers.GlobalScale;
}
```

- [ ] **Step 4: Fonts**

Create `Windows/Ui/UiFonts.cs`. A missing file falls back to Dalamud's default at the same size and logs one warning. The player's language glyphs are merged into each bundled font, because Cinzel and Spectral have no Japanese, Korean or Chinese.

`Windows/Ui/UiFonts.cs` (new file, complete):

```csharp
using System;
using System.IO;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Services;

namespace DungeonMasterXIV.Windows.Ui;

/// <summary>The type roles: the game's Axis for body and meta, bundled Cinzel and Spectral for the rest.</summary>
internal sealed class UiFonts : IDisposable
{
    public UiFonts(IUiBuilder ui, string fontDirectory, IPluginLog log)
    {
        Body = ui.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis14));
        Meta = ui.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis12));
        Title = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Regular.ttf"), 15f, log);
        Code = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Regular.ttf"), 20f, log);
        Total = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Bold.ttf"), 24f, log);
        Voice = Bundled(ui, Path.Combine(fontDirectory, "Spectral-Regular.ttf"), 16f, log);
        Icon = ui.IconFontHandle;
    }

    public IFontHandle Body { get; }

    public IFontHandle Meta { get; }

    public IFontHandle Title { get; }

    public IFontHandle Code { get; }

    public IFontHandle Total { get; }

    public IFontHandle Voice { get; }

    /// <summary>Dalamud's FontAwesome handle. Owned by Dalamud, so never disposed here.</summary>
    public IFontHandle Icon { get; }

    public void Dispose()
    {
        Body.Dispose();
        Meta.Dispose();
        Title.Dispose();
        Code.Dispose();
        Total.Dispose();
        Voice.Dispose();
    }

    /// <summary>A bundled font, with the player's language glyphs merged in; Dalamud's default if the file is missing.</summary>
    private static IFontHandle Bundled(IUiBuilder ui, string path, float sizePx, IPluginLog log)
    {
        if (!File.Exists(path))
        {
            log.Warning("Font file {Path} is missing, so that text uses Dalamud's default font.", path);
            return ui.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(sizePx)));
        }

        return ui.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var font = tk.AddFontFromFile(path, new SafeFontConfig { SizePx = sizePx });
            tk.AttachExtraGlyphsForDalamudLanguage(new SafeFontConfig { SizePx = sizePx, MergeFont = font });
        }));
    }
}
```

- [ ] **Step 5: The scoped theme and the themed window base**

Create `Windows/Ui/Theme.cs`:

`Windows/Ui/Theme.cs` (new file, complete):

```csharp
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace DungeonMasterXIV.Windows.Ui;

/// <summary>Applies the Chronicle colours and shapes to ImGui until the returned scope is disposed.</summary>
internal static class Theme
{
    public static IDisposable Push()
    {
        var colours = ImRaii.PushColor(ImGuiCol.WindowBg, Palette.Surface)
            .Push(ImGuiCol.ChildBg, Vector4.Zero)
            .Push(ImGuiCol.PopupBg, Palette.SurfaceRaised)
            .Push(ImGuiCol.Border, Palette.RuleStrong)
            .Push(ImGuiCol.Text, Palette.Text)
            .Push(ImGuiCol.TextDisabled, Palette.TextMuted)
            .Push(ImGuiCol.TitleBg, Palette.SurfaceRaised)
            .Push(ImGuiCol.TitleBgActive, Palette.SurfaceRaised)
            .Push(ImGuiCol.TitleBgCollapsed, Palette.Surface)
            .Push(ImGuiCol.FrameBg, Palette.SurfaceSunk)
            .Push(ImGuiCol.FrameBgHovered, Palette.SurfaceRaised)
            .Push(ImGuiCol.FrameBgActive, Palette.SurfaceHover)
            .Push(ImGuiCol.Button, Palette.SurfaceRaised)
            .Push(ImGuiCol.ButtonHovered, Palette.SurfaceHover)
            .Push(ImGuiCol.ButtonActive, Palette.RuleSoft)
            .Push(ImGuiCol.Header, Palette.SurfaceRaised)
            .Push(ImGuiCol.HeaderHovered, Palette.SurfaceHover)
            .Push(ImGuiCol.HeaderActive, Palette.SurfaceHover)
            .Push(ImGuiCol.Separator, Palette.RuleSoft)
            .Push(ImGuiCol.NavHighlight, Palette.Gold)
            .Push(ImGuiCol.CheckMark, Palette.Gold)
            .Push(ImGuiCol.SliderGrab, Palette.Gold)
            .Push(ImGuiCol.SliderGrabActive, Palette.GoldBright)
            .Push(ImGuiCol.ScrollbarBg, Palette.Surface)
            .Push(ImGuiCol.ScrollbarGrab, Palette.Rule)
            .Push(ImGuiCol.ScrollbarGrabHovered, Palette.RuleStrong)
            .Push(ImGuiCol.ScrollbarGrabActive, Palette.Gold)
            .Push(ImGuiCol.ResizeGrip, Palette.Rule)
            .Push(ImGuiCol.ResizeGripHovered, Palette.RuleStrong)
            .Push(ImGuiCol.ResizeGripActive, Palette.Gold);

        var scale = ImGuiHelpers.GlobalScale;
        var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, Metrics.WindowRounding)
            .Push(ImGuiStyleVar.ChildRounding, Metrics.ControlRounding)
            .Push(ImGuiStyleVar.FrameRounding, Metrics.ControlRounding)
            .Push(ImGuiStyleVar.PopupRounding, Metrics.ControlRounding)
            .Push(ImGuiStyleVar.GrabRounding, Metrics.ControlRounding)
            .Push(ImGuiStyleVar.WindowBorderSize, 1f)
            .Push(ImGuiStyleVar.FrameBorderSize, 1f)
            .Push(ImGuiStyleVar.DisabledAlpha, 0.5f)
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(12f, 12f) * scale)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(8f, 4f) * scale)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 6f) * scale);

        return new Both(styles, colours);
    }

    private sealed class Both(IDisposable first, IDisposable second) : IDisposable
    {
        public void Dispose()
        {
            first.Dispose();
            second.Dispose();
        }
    }
}
```

Create `Windows/Ui/ThemedWindow.cs`. `PreDraw` pushes the theme and the `Title` font, so the title bar is Cinzel. The sealed `Draw` pushes `Body` for the content and catches a drawing failure, so `PostDraw` always runs and nothing leaks into other plugins.

`Windows/Ui/ThemedWindow.cs` (new file, complete):

```csharp
using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace DungeonMasterXIV.Windows.Ui;

/// <summary>A window drawn in the Chronicle theme: Cinzel title bar, Axis body, undone after every draw.</summary>
internal abstract class ThemedWindow : Window
{
    private IDisposable? _theme;
    private IDisposable? _titleFont;
    private bool _failureLogged;

    protected ThemedWindow(string name, UiFonts fonts, IPluginLog log, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        : base(name, flags)
    {
        Fonts = fonts;
        Log = log;
    }

    protected UiFonts Fonts { get; }

    protected IPluginLog Log { get; }

    public override void PreDraw()
    {
        _theme = Theme.Push();
        _titleFont = Fonts.Title.Push();
    }

    public sealed override void Draw()
    {
        using var body = Fonts.Body.Push();
        try
        {
            DrawContent();
        }
        catch (Exception exception)
        {
            if (!_failureLogged)
            {
                Log.Error(exception, "The {Window} window failed while drawing.", WindowName);
                _failureLogged = true;
            }

            ImGui.TextColored(Palette.Danger, "This window hit a problem and stopped drawing. Details are in /xllog.");
        }
    }

    public override void PostDraw()
    {
        _titleFont?.Dispose();
        _titleFont = null;
        _theme?.Dispose();
        _theme = null;
    }

    protected abstract void DrawContent();
}
```

- [ ] **Step 6: Build, and check the fonts reach the output**

Run: `dotnet build DungeonMasterXIV.sln 2>&1 | grep -E '^ *[0-9]+ (Warning|Error)' ; find bin -path '*Data/Fonts/*' | sort`

Expected:
- 0 warnings and 0 errors;
- the five `Data/Fonts` files are listed under the plugin's output folder.

- [ ] **Step 7: Commit**

```bash
git add DungeonMasterXIV.csproj Data/Fonts Windows/Ui
git commit -F - <<'EOF'
feat(ui): Chronicle palette, spacing, fonts and a themed window base

Colour and spacing roles, the game's Axis plus bundled Cinzel and Spectral (SIL OFL, licences
shipped), a theme scoped to our windows, and a window base that themes its title bar and never leaks
style into other plugins.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 3: The component layer

Implements the spec §3 "built now" table. Every component takes plain data and never reads session state.

**Files:**
- Create: `Windows/Ui/Components/{Card,RoleBadge,Speaker,MessageCard,EventLine,RollCard,Banner,ActionRow,RosterRow,CodeDisplay,EmptyState,RailButton,Section}.cs`

**Interfaces:**
- Consumes: Task 2's `Palette`, `Metrics` and `UiFonts`; Task 1's `SpeakerName` and `SharedRoll`; `SessionRole`, `SessionCode`, `StreamEventKind`.
- Produces:
  - `Card.Begin(Vector4 surface, Vector4 edge)`, `Card.WithLeftEdge(surface, edge, leftEdge)` and `Card.InnerWidth()`. **Cards do not nest, and are never drawn inside an ImGui table cell.**
  - `RoleBadge.TextFor(SessionRole)` and `RoleBadge.Draw(UiFonts, SessionRole)`.
  - `Speaker.Draw(UiFonts, SpeakerName)`, `Speaker.DrawWithTime(UiFonts, SpeakerName, long atUtcTicks)` and `Speaker.TimeOf(long)`.
  - `MessageCard.Draw(UiFonts, SpeakerName, long, string)`.
  - `EventLine.Draw(UiFonts, StreamEventKind, SpeakerName, long)`.
  - `RollCard.Draw(UiFonts, SpeakerName, long, SharedRoll, bool local = false)`.
  - `Banner.Draw(UiFonts, BannerKind, string)` and `Banner.Refusal(string)`.
  - `ActionRow.Primary(string)`, `ActionRow.Secondary(string)`, `ActionRow.Unavailable(string, string why)` and `ActionRow.Edged(string, Vector4 edge, Vector4 text)`.
  - `DangerAction.Draw(string label, string confirmLabel, string cancelLabel = "Cancel")`, an instance holding its own armed state.
  - `RosterRow.Draw(UiFonts, SpeakerName, bool away = false)`.
  - `CodeDisplay.Draw(UiFonts, SessionCode)`.
  - `EmptyState.Draw(UiFonts, string heading, string howTo)`.
  - `RailButton.Size` and `RailButton.Draw(UiFonts, FontAwesomeIcon, string tooltip, bool lit)`.
  - `Section.Heading(UiFonts, string)` and `Section.Collapsible(UiFonts, string, bool openByDefault = true)`.

- [ ] **Step 1: The card frame**

`Card` splits the draw list so its background is drawn behind content of unknown height.

`Windows/Ui/Components/Card.cs` (new file, complete):

```csharp
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A padded, full-width panel drawn behind whatever is drawn inside its scope. Cards do not nest.</summary>
internal readonly struct Card : IDisposable
{
    private readonly Vector2 _start;
    private readonly float _width;
    private readonly Vector4 _surface;
    private readonly Vector4 _edge;
    private readonly Vector4? _leftEdge;

    private Card(Vector4 surface, Vector4 edge, Vector4? leftEdge)
    {
        _surface = surface;
        _edge = edge;
        _leftEdge = leftEdge;
        _start = ImGui.GetCursorScreenPos();
        _width = ImGui.GetContentRegionAvail().X;

        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        var padding = Metrics.CardPadding;
        ImGui.SetCursorScreenPos(_start + new Vector2(padding + (leftEdge is null ? 0f : Metrics.EdgeWidth), padding));
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + _width - (2f * padding) - (leftEdge is null ? 0f : Metrics.EdgeWidth));
    }

    public static Card Begin(Vector4 surface, Vector4 edge) => new(surface, edge, null);

    /// <summary>The width left on the current line inside a card, stopping at its right padding.</summary>
    public static float InnerWidth() => ImGui.GetContentRegionAvail().X - Metrics.CardPadding;

    /// <summary>A card with a thick coloured left edge, used for banners.</summary>
    public static Card WithLeftEdge(Vector4 surface, Vector4 edge, Vector4 leftEdge) => new(surface, edge, leftEdge);

    public void Dispose()
    {
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();

        var end = new Vector2(_start.X + _width, ImGui.GetItemRectMax().Y + Metrics.CardPadding);
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(_start, end, ImGui.GetColorU32(_surface), Metrics.ControlRounding);
        drawList.AddRect(_start, end, ImGui.GetColorU32(_edge), Metrics.ControlRounding);
        if (_leftEdge is { } left)
        {
            drawList.AddRectFilled(
                _start, new Vector2(_start.X + Metrics.EdgeWidth, end.Y), ImGui.GetColorU32(left), Metrics.ControlRounding);
        }

        drawList.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(_start.X, end.Y));
        ImGui.Dummy(new Vector2(_width, Metrics.CardGap));
    }
}
```

- [ ] **Step 2: Who said it**

`Windows/Ui/Components/RoleBadge.cs` (new file, complete):

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A small outlined tag for a session role: "[DM]" for the host, "Assistant", nothing for a player.</summary>
internal static class RoleBadge
{
    /// <summary>The badge text, or null when the role has none. Only the host's is bracketed (rolls R-2.7a).</summary>
    public static string? TextFor(SessionRole role) => role switch
    {
        SessionRole.DungeonMaster => "[DM]",
        SessionRole.Assistant => "Assistant",
        _ => null,
    };

    /// <summary>Draws the badge and returns true, or draws nothing and returns false.</summary>
    public static bool Draw(UiFonts fonts, SessionRole role)
    {
        if (TextFor(role) is not { } text)
        {
            return false;
        }

        using var font = fonts.Meta.Push();
        var padding = new Vector2(Metrics.Step, 1f);
        var size = ImGui.CalcTextSize(text) + (padding * 2f);
        var start = ImGui.GetCursorScreenPos();

        ImGui.GetWindowDrawList().AddRect(start, start + size, ImGui.GetColorU32(Palette.GoldLabel), Metrics.ControlRounding);
        ImGui.SetCursorScreenPos(start + padding);
        ImGui.TextColored(Palette.GoldLabel, text);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(size);
        return true;
    }
}
```

`Windows/Ui/Components/Speaker.cs` (new file, complete):

```csharp
using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>Who said something, on one line: the host's "[DM]" badge first, then the name, never shortened.</summary>
internal static class Speaker
{
    public static void Draw(UiFonts fonts, SpeakerName speaker)
    {
        if (speaker.Role == SessionRole.DungeonMaster && RoleBadge.Draw(fonts, speaker.Role))
        {
            ImGui.SameLine();
        }

        ImGui.TextColored(Palette.Text, speaker.Name);
    }

    /// <summary>The speaker line with a right-aligned local time, as cards and event lines use it.</summary>
    public static void DrawWithTime(UiFonts fonts, SpeakerName speaker, long atUtcTicks)
    {
        Draw(fonts, speaker);
        DrawTime(fonts, atUtcTicks);
    }

    public static string TimeOf(long atUtcTicks) =>
        new DateTimeOffset(atUtcTicks, TimeSpan.Zero).ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);

    private static void DrawTime(UiFonts fonts, long atUtcTicks)
    {
        using var font = fonts.Meta.Push();
        var time = TimeOf(atUtcTicks);
        var width = ImGui.CalcTextSize(time).X;
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - width - Metrics.CardPadding);
        ImGui.TextColored(Palette.TextMuted, time);
    }
}
```

- [ ] **Step 3: Stream entries**

`Windows/Ui/Components/MessageCard.cs` (new file, complete):

```csharp
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A chat message: speaker and time on top, then the text.</summary>
internal static class MessageCard
{
    public static void Draw(UiFonts fonts, SpeakerName speaker, long atUtcTicks, string text)
    {
        using var card = Card.Begin(Palette.SurfaceRaised, Palette.Rule);
        Speaker.DrawWithTime(fonts, speaker, atUtcTicks);
        ImGui.TextWrapped(text);
    }
}
```

`Windows/Ui/Components/EventLine.cs` (new file, complete):

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A membership change as one compact muted line, or a gap as a rule with a note.</summary>
internal static class EventLine
{
    public const string GapText = "Some messages were not held.";

    public static string? TextFor(StreamEventKind kind, string name) => kind switch
    {
        StreamEventKind.Joined => $"{name} joined",
        StreamEventKind.Left => $"{name} left",
        StreamEventKind.Dropped => $"{name} lost connection",
        StreamEventKind.Reconnected => $"{name} reconnected",
        _ => null,
    };

    public static void Draw(UiFonts fonts, StreamEventKind kind, SpeakerName speaker, long atUtcTicks)
    {
        using var font = fonts.Meta.Push();

        if (kind == StreamEventKind.Gap)
        {
            DrawGap();
            return;
        }

        if (TextFor(kind, speaker.Name) is not { } text)
        {
            return;
        }

        ImGui.TextColored(Palette.TextMuted, $"{text} · {Speaker.TimeOf(atUtcTicks)}");
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
    }

    private static void DrawGap()
    {
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var middle = start.Y + (ImGui.GetTextLineHeight() / 2f);
        var textWidth = ImGui.CalcTextSize(GapText).X;
        var textStart = start.X + ((width - textWidth) / 2f);
        var colour = ImGui.GetColorU32(Palette.RuleSoft);
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddLine(new Vector2(start.X, middle), new Vector2(textStart - Metrics.Step, middle), colour);
        drawList.AddLine(new Vector2(textStart + textWidth + Metrics.Step, middle), new Vector2(start.X + width, middle), colour);
        ImGui.SetCursorScreenPos(new Vector2(textStart, start.Y));
        ImGui.TextColored(Palette.TextMuted, GapText);
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
    }
}
```

Set-aside dice are struck through, not hidden. That keeps the audit trail rolls R-2.2 requires.

`Windows/Ui/Components/RollCard.cs` (new file, complete):

```csharp
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A roll: speaker and time, the label, the expression, every die, and the total.</summary>
internal static class RollCard
{
    public const string OnlyYou = "Only you saw this.";

    public static void Draw(UiFonts fonts, SpeakerName speaker, long atUtcTicks, SharedRoll roll, bool local = false)
    {
        using var card = Card.Begin(Palette.SurfaceRaised, Palette.Rule);
        Speaker.DrawWithTime(fonts, speaker, atUtcTicks);

        if (!string.IsNullOrWhiteSpace(roll.Label))
        {
            using var title = fonts.Title.Push();
            ImGui.TextColored(Palette.GoldLabel, roll.Label);
        }

        Bar(fonts.Body, roll.Expression, Palette.Text);
        DrawDice(fonts, roll);
        Bar(fonts.Total, roll.Total.ToString(CultureInfo.InvariantCulture), Palette.GoldBright);

        if (roll.Notice is { } notice)
        {
            ImGui.TextColored(Palette.Warning, notice);
        }

        if (local)
        {
            using var meta = fonts.Meta.Push();
            ImGui.TextColored(Palette.TextMuted, OnlyYou);
        }
    }

    /// <summary>Kept dice plain; set-aside dice muted and struck through, so the audit trail stays visible.</summary>
    private static void DrawDice(UiFonts fonts, SharedRoll roll)
    {
        if (roll.Dice.Count == 0)
        {
            return;
        }

        using var meta = fonts.Meta.Push();
        var right = ImGui.GetCursorScreenPos().X + Card.InnerWidth();
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        for (var index = 0; index < roll.Dice.Count; index++)
        {
            var die = roll.Dice[index];
            var text = die.Value.ToString(CultureInfo.InvariantCulture);
            var width = ImGui.CalcTextSize(text).X;

            if (index > 0)
            {
                ImGui.SameLine();
                if (ImGui.GetCursorScreenPos().X + width > right)
                {
                    ImGui.NewLine();
                }
            }

            var start = ImGui.GetCursorScreenPos();
            ImGui.TextColored(die.Kept ? Palette.Text : Palette.TextMuted, text);

            if (!die.Kept)
            {
                var middle = start.Y + (ImGui.GetTextLineHeight() / 2f);
                ImGui.GetWindowDrawList().AddLine(
                    new Vector2(start.X - 1f, middle),
                    new Vector2(start.X + width + 1f, middle),
                    ImGui.GetColorU32(Palette.TextMuted));
            }
        }

        ImGui.Dummy(new Vector2(0f, spacing / 2f));
    }

    private static void Bar(IFontHandle font, string text, Vector4 colour)
    {
        using var pushed = font.Push();
        var start = ImGui.GetCursorScreenPos();
        var width = Card.InnerWidth();
        var height = ImGui.GetTextLineHeight() + (2f * Metrics.Step);
        var end = start + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(start, end, ImGui.GetColorU32(Palette.SurfaceSunk), Metrics.ControlRounding);
        drawList.AddRect(start, end, ImGui.GetColorU32(Palette.RuleSoft), Metrics.ControlRounding);

        var textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorScreenPos(new Vector2(start.X + ((width - textWidth) / 2f), start.Y + Metrics.Step));
        ImGui.TextColored(colour, text);
        ImGui.SetCursorScreenPos(new Vector2(start.X, end.Y + Metrics.Step));
        ImGui.Dummy(Vector2.Zero);
    }
}
```

- [ ] **Step 4: Status and actions**

`Windows/Ui/Components/Banner.cs` (new file, complete):

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>How serious a banner is: its edge colour and icon.</summary>
internal enum BannerKind
{
    Info,
    Warning,
    Danger,
}

/// <summary>A status note with a coloured left edge and an icon, so its kind never rests on colour alone.</summary>
internal static class Banner
{
    public static void Draw(UiFonts fonts, BannerKind kind, string text)
    {
        var (colour, icon) = kind switch
        {
            BannerKind.Warning => (Palette.Warning, FontAwesomeIcon.ExclamationTriangle),
            BannerKind.Danger => (Palette.Danger, FontAwesomeIcon.TimesCircle),
            _ => (Palette.Gold, FontAwesomeIcon.InfoCircle),
        };

        using var card = Card.WithLeftEdge(Palette.SurfaceRaised, Palette.RuleSoft, colour);
        using (fonts.Icon.Push())
        {
            ImGui.TextColored(colour, icon.ToIconString());
        }

        ImGui.SameLine();
        ImGui.TextWrapped(text);
    }

    /// <summary>A one-line refusal under an input, in the danger colour.</summary>
    public static void Refusal(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Palette.Danger);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
    }
}
```

`Windows/Ui/Components/ActionRow.cs` (new file, complete):

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>The button vocabulary: primary with a gold edge, secondary with a strong rule edge.</summary>
internal static class ActionRow
{
    public static bool Primary(string label) => Edged(label, Palette.Gold, Palette.Text);

    public static bool Secondary(string label) => Edged(label, Palette.RuleStrong, Palette.Text);

    /// <summary>A disabled button with a tooltip saying why it cannot be pressed.</summary>
    public static void Unavailable(string label, string why)
    {
        using (ImRaii.Disabled())
        {
            Edged(label, Palette.RuleStrong, Palette.Text);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(why);
        }
    }

    internal static bool Edged(string label, Vector4 edge, Vector4 text)
    {
        using var colours = ImRaii.PushColor(ImGuiCol.Border, edge).Push(ImGuiCol.Text, text);
        return ImGui.Button(label);
    }
}

/// <summary>A danger button that asks for confirmation on the first press and acts on the second.</summary>
internal sealed class DangerAction
{
    private bool _armed;

    /// <summary>Returns true once the person has confirmed.</summary>
    public bool Draw(string label, string confirmLabel, string cancelLabel = "Cancel")
    {
        if (!_armed)
        {
            if (ActionRow.Edged(label, Palette.Danger, Palette.Danger))
            {
                _armed = true;
            }

            return false;
        }

        var confirmed = ActionRow.Edged(confirmLabel, Palette.Danger, Palette.Danger);
        ImGui.SameLine();
        if (ActionRow.Secondary(cancelLabel) || confirmed)
        {
            _armed = false;
        }

        return confirmed;
    }
}
```

- [ ] **Step 5: People, the code, empty places, the rail, sections**

`Windows/Ui/Components/RosterRow.cs` (new file, complete):

```csharp
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>One person at the table: name, role badge, and "away" while disconnected. Never a role in parentheses.</summary>
internal static class RosterRow
{
    public const string Away = "away";

    public static void Draw(UiFonts fonts, SpeakerName person, bool away = false)
    {
        ImGui.TextColored(Palette.Text, person.Name);

        if (RoleBadge.TextFor(person.Role) is not null)
        {
            ImGui.SameLine();
            RoleBadge.Draw(fonts, person.Role);
        }

        if (away)
        {
            ImGui.SameLine();
            using var meta = fonts.Meta.Push();
            ImGui.TextColored(Palette.TextMuted, Away);
        }
    }
}
```

`Windows/Ui/Components/CodeDisplay.cs` (new file, complete):

```csharp
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>The session code, large, with a Copy button (session-layer R-1.3i).</summary>
internal static class CodeDisplay
{
    public static void Draw(UiFonts fonts, SessionCode code)
    {
        using (fonts.Meta.Push())
        {
            ImGui.TextColored(Palette.TextMuted, "Session code");
        }

        using (fonts.Code.Push())
        {
            ImGui.TextColored(Palette.GoldBright, code.ToDisplayString());
        }

        ImGui.SameLine();
        if (ActionRow.Secondary("Copy"))
        {
            ImGui.SetClipboardText(code.ToClipboardString());
        }
    }
}
```

`Windows/Ui/Components/EmptyState.cs` (new file, complete):

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>What will appear in an empty place and how to make it appear.</summary>
internal static class EmptyState
{
    public static void Draw(UiFonts fonts, string heading, string howTo)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Step * 2f));
        using (fonts.Title.Push())
        {
            ImGui.TextColored(Palette.GoldLabel, heading);
        }

        ImGui.PushStyleColor(ImGuiCol.Text, Palette.TextMuted);
        ImGui.TextWrapped(howTo);
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(0f, Metrics.Step * 2f));
    }
}
```

`Windows/Ui/Components/RailButton.cs` (new file, complete):

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A square icon button with a tooltip, lit with a gold edge while its window is open.</summary>
internal static class RailButton
{
    public static float Size => 34f * ImGuiHelpers.GlobalScale;

    public static bool Draw(UiFonts fonts, FontAwesomeIcon icon, string tooltip, bool lit)
    {
        bool pressed;
        using (ImRaii.PushColor(ImGuiCol.Button, lit ? Palette.SurfaceHover : Palette.Surface)
                   .Push(ImGuiCol.Border, lit ? Palette.Gold : Palette.Surface)
                   .Push(ImGuiCol.Text, lit ? Palette.GoldBright : Palette.GoldLabel))
        using (fonts.Icon.Push())
        {
            pressed = ImGui.Button($"{icon.ToIconString()}##{tooltip}", new Vector2(Size, Size));
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }

        return pressed;
    }
}
```

`Windows/Ui/Components/Section.cs` (new file, complete):

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A small-caps Cinzel label over a soft rule, optionally collapsible.</summary>
internal static class Section
{
    public static void Heading(UiFonts fonts, string label)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
        using (fonts.Title.Push())
        {
            ImGui.TextColored(Palette.GoldLabel, label.ToUpperInvariant());
        }

        Rule();
    }

    /// <summary>A heading that opens and closes; returns true while open.</summary>
    public static bool Collapsible(UiFonts fonts, string label, bool openByDefault = true)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
        bool open;
        using (fonts.Title.Push())
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.GoldLabel)
                   .Push(ImGuiCol.Header, Vector4.Zero)
                   .Push(ImGuiCol.HeaderHovered, Palette.SurfaceHover)
                   .Push(ImGuiCol.HeaderActive, Palette.SurfaceHover))
        {
            open = ImGui.CollapsingHeader(
                label.ToUpperInvariant(), openByDefault ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
        }

        Rule();
        return open;
    }

    private static void Rule()
    {
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.GetWindowDrawList().AddLine(start, start + new Vector2(width, 0f), ImGui.GetColorU32(Palette.RuleSoft));
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
    }
}
```

- [ ] **Step 6: Build**

Run: `dotnet build DungeonMasterXIV.sln 2>&1 | grep -E '^ *[0-9]+ (Warning|Error)'`

Expected: 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add Windows/Ui/Components
git commit -F - <<'EOF'
feat(ui): the component layer - cards, speaker, badges, rolls, banners, actions, rail buttons

Every feature window is built from these. Each takes plain data and draws itself in the Chronicle
theme; none reads session state.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 4: The rail replaces the main window

Implements spec §4 "The rail", and core-skeleton R-0.3/R-0.4 as amended.

**Files:**
- Create: `Windows/RailWindow.cs`
- Modify: `src/DungeonMasterXIV.Core/Data/PluginSettings.cs`, `Plugin.cs`
- Delete: `Windows/MainWindow.cs`

**Interfaces:**
- Consumes: `ThemedWindow`, `UiFonts`, `RailButton`, `Metrics`, `Palette`, `ConfigurationStore`.
- Produces:
  - `internal sealed record RailEntry(Window Window, FontAwesomeIcon Icon, string Tooltip)`;
  - `RailWindow(ConfigurationStore, UiFonts, IPluginLog, IReadOnlyList<RailEntry> entries, RailEntry settings, Window introduceBeside)`;
  - `PluginSettings.RailCollapsed`, `PluginSettings.RailIntroduced` and `PluginSettings.RecordRailCollapsed(bool)`;
  - `Plugin` now owns `_fonts` (a `UiFonts`) and disposes it last.

- [ ] **Step 1: Remember the rail's state**

`src/DungeonMasterXIV.Core/Data/PluginSettings.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Data/PluginSettings.cs b/src/DungeonMasterXIV.Core/Data/PluginSettings.cs
index 557b5b5..b785348 100644
--- a/src/DungeonMasterXIV.Core/Data/PluginSettings.cs
+++ b/src/DungeonMasterXIV.Core/Data/PluginSettings.cs
@@ -13,6 +13,11 @@ public sealed class PluginSettings
 
     public bool RestoreWindowState { get; set; } = true;
 
+    public bool RailCollapsed { get; set; }
+
+    /// <summary>False until the rail has opened once and put the session window beside it.</summary>
+    public bool RailIntroduced { get; set; }
+
     public static bool RequiresWriteOnLoad(int? versionOnDisk) => versionOnDisk is null;
 
     public string RelayAddress { get; set; } = Net.RelayEndpoint.Default;
@@ -52,6 +57,17 @@ public sealed class PluginSettings
         return true;
     }
 
+    public bool RecordRailCollapsed(bool collapsed)
+    {
+        if (RailCollapsed == collapsed)
+        {
+            return false;
+        }
+
+        RailCollapsed = collapsed;
+        return true;
+    }
+
     public bool RecordSettingsWindowOpen(bool isOpen)
     {
         if (SettingsWindowOpen == isOpen)
```

- [ ] **Step 2: The rail**

Create `Windows/RailWindow.cs`. It keeps the old main window's `###dmx-main` ID, so Dalamud's stored position carries over. It also keeps `MainWindowOpen`, so open-on-load still works.

`Windows/RailWindow.cs` (new file, complete):

```csharp
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>One rail button: the window it toggles, its icon, and its tooltip.</summary>
internal sealed record RailEntry(Window Window, FontAwesomeIcon Icon, string Tooltip);

/// <summary>The main window: a movable column of buttons, one per window that exists, lit while it is open.</summary>
internal sealed class RailWindow : ThemedWindow
{
    private readonly ConfigurationStore _configurationStore;
    private readonly IReadOnlyList<RailEntry> _entries;
    private readonly RailEntry _settings;
    private readonly Window _introduceBeside;

    public RailWindow(
        ConfigurationStore configurationStore,
        UiFonts fonts,
        IPluginLog log,
        IReadOnlyList<RailEntry> entries,
        RailEntry settings,
        Window introduceBeside)
        : base(
            "Dungeon Master XIV###dmx-main",
            fonts,
            log,
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse)
    {
        _configurationStore = configurationStore;
        _entries = entries;
        _settings = settings;
        _introduceBeside = introduceBeside;
        RespectCloseHotkey = false;

        IsOpen = configurationStore.Configuration.Settings.ShouldOpenOnLoad(
            configurationStore.Configuration.Settings.MainWindowOpen);
    }

    private PluginSettings Settings => _configurationStore.Configuration.Settings;

    protected override void DrawContent()
    {
        DrawGrip();

        if (Settings.RailCollapsed)
        {
            if (RailButton.Draw(Fonts, FontAwesomeIcon.DiceD20, "Show the Dungeon Master XIV buttons", lit: false))
            {
                Collapse(false);
            }

            return;
        }

        foreach (var entry in _entries)
        {
            DrawEntry(entry);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Step * 2f));
        DrawEntry(_settings);

        if (RailButton.Draw(Fonts, FontAwesomeIcon.ChevronLeft, "Collapse to one button", lit: false))
        {
            Collapse(true);
        }

        IntroduceOnce();
    }

    public override void OnOpen() => Remember(true);

    public override void OnClose() => Remember(false);

    private void DrawEntry(RailEntry entry)
    {
        if (RailButton.Draw(Fonts, entry.Icon, entry.Tooltip, entry.Window.IsOpen))
        {
            entry.Window.Toggle();
        }
    }

    /// <summary>A handle to drag the rail by, since it has no title bar.</summary>
    private void DrawGrip()
    {
        using var icon = Fonts.Icon.Push();
        var glyph = FontAwesomeIcon.GripLines.ToIconString();
        var glyphSize = ImGui.CalcTextSize(glyph);
        var size = new Vector2(RailButton.Size, glyphSize.Y + Metrics.Step);

        ImGui.InvisibleButton("##grip", size);
        var min = ImGui.GetItemRectMin();
        ImGui.GetWindowDrawList().AddText(
            min + ((size - glyphSize) / 2f), ImGui.GetColorU32(ImGui.IsItemHovered() ? Palette.Gold : Palette.Rule), glyph);

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        }
    }

    private void Collapse(bool collapsed)
    {
        if (Settings.RecordRailCollapsed(collapsed))
        {
            _configurationStore.Save();
        }
    }

    /// <summary>The first time the rail ever opens, the session window opens beside it.</summary>
    private void IntroduceOnce()
    {
        if (Settings.RailIntroduced)
        {
            return;
        }

        _introduceBeside.Position = ImGui.GetWindowPos() + new Vector2(ImGui.GetWindowSize().X + (Metrics.Step * 2f), 0f);
        _introduceBeside.PositionCondition = ImGuiCond.FirstUseEver;
        _introduceBeside.IsOpen = true;
        Settings.RailIntroduced = true;
        _configurationStore.Save();
    }

    private void Remember(bool isOpen)
    {
        if (Settings.RecordMainWindowOpen(isOpen))
        {
            _configurationStore.Save();
        }
    }
}
```

- [ ] **Step 3: Wire fonts and the rail into the plugin; delete the main window**

```bash
git rm Windows/MainWindow.cs
```

`Plugin.cs` (diff):

```diff
diff --git a/Plugin.cs b/Plugin.cs
index 3c662f8..a4acc5b 100644
--- a/Plugin.cs
+++ b/Plugin.cs
@@ -1,6 +1,7 @@
 using System;
 using System.IO;
 using Dalamud.Game.Command;
+using Dalamud.Interface;
 using Dalamud.Interface.Windowing;
 using Dalamud.Plugin;
 using Dalamud.Plugin.Services;
@@ -10,6 +11,7 @@ using DungeonMasterXIV.Net;
 using DungeonMasterXIV.Services;
 using DungeonMasterXIV.Transport;
 using DungeonMasterXIV.Windows;
+using DungeonMasterXIV.Windows.Ui;
 
 namespace DungeonMasterXIV;
 
@@ -24,7 +26,8 @@ public sealed class Plugin : IDalamudPlugin
     private readonly ConfigurationStore _configurationStore;
     private readonly CampaignStore _campaignStore;
     private readonly WindowSystem _windowSystem;
-    private readonly MainWindow _mainWindow;
+    private readonly UiFonts _fonts;
+    private readonly RailWindow _railWindow;
     private readonly ConfigWindow _configWindow;
     private readonly SessionWindow _sessionWindow;
     private readonly WebSocketSessionTransport _relayTransport;
@@ -46,7 +49,10 @@ public sealed class Plugin : IDalamudPlugin
             new CampaignFileArchive(pluginInterface.ConfigDirectory),
             new CampaignStoreLog(log));
         _windowSystem = new WindowSystem("DungeonMasterXIV");
-        _mainWindow = new MainWindow(_configurationStore);
+        _fonts = new UiFonts(
+            pluginInterface.UiBuilder,
+            Path.Combine(pluginInterface.AssemblyLocation.DirectoryName!, "Data", "Fonts"),
+            log);
 
         var characterName = new LocalCharacterName(objects).Current;
 
@@ -71,8 +77,14 @@ public sealed class Plugin : IDalamudPlugin
             NameWeSendAs(characterName),
             _hostingCampaign,
             () => _configurationStore.Configuration.Settings.Relink, SessionEndChoiceFor(pluginInterface.ConfigDirectory));
-        _mainWindow.OpenSession = _sessionWindow.Open;
-        _commandDispatcher = new CommandDispatcher(_mainWindow.Toggle, _configWindow.Open);
+        _railWindow = new RailWindow(
+            _configurationStore,
+            _fonts,
+            log,
+            [new RailEntry(_sessionWindow, FontAwesomeIcon.Comments, "Session")],
+            new RailEntry(_configWindow, FontAwesomeIcon.Cog, "Settings"),
+            _sessionWindow);
+        _commandDispatcher = new CommandDispatcher(_railWindow.Toggle, _configWindow.Open);
 
         try
         {
@@ -125,8 +137,10 @@ public sealed class Plugin : IDalamudPlugin
 
     private void Register(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IFramework framework)
     {
-        _windowSystem.AddWindow(_mainWindow);
-        _unwind.Push("main window", () => _windowSystem.RemoveWindow(_mainWindow));
+        _unwind.Push("fonts", _fonts.Dispose);
+
+        _windowSystem.AddWindow(_railWindow);
+        _unwind.Push("rail window", () => _windowSystem.RemoveWindow(_railWindow));
 
         _windowSystem.AddWindow(_configWindow);
         _unwind.Push("settings window", () => _windowSystem.RemoveWindow(_configWindow));
@@ -142,15 +156,15 @@ public sealed class Plugin : IDalamudPlugin
 
         commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
         {
-            HelpMessage = "Toggle the Dungeon Master XIV window. \"/dmx settings\" opens settings.",
+            HelpMessage = "Toggle the Dungeon Master XIV buttons. \"/dmx settings\" opens settings.",
         });
         _unwind.Push("/dmx command", () => commandManager.RemoveHandler(CommandName));
 
         pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
         _unwind.Push("draw handler", () => pluginInterface.UiBuilder.Draw -= _windowSystem.Draw);
 
-        pluginInterface.UiBuilder.OpenMainUi += _mainWindow.Toggle;
-        _unwind.Push("main UI handler", () => pluginInterface.UiBuilder.OpenMainUi -= _mainWindow.Toggle);
+        pluginInterface.UiBuilder.OpenMainUi += _railWindow.Toggle;
+        _unwind.Push("main UI handler", () => pluginInterface.UiBuilder.OpenMainUi -= _railWindow.Toggle);
 
         pluginInterface.UiBuilder.OpenConfigUi += _configWindow.Toggle;
         _unwind.Push("config UI handler", () => pluginInterface.UiBuilder.OpenConfigUi -= _configWindow.Toggle);
```

The fonts' teardown step is pushed first, so it runs last, after every window and the draw handler are gone.

- [ ] **Step 4: Build and test**

Run: `dotnet build DungeonMasterXIV.sln 2>&1 | grep -E '^ *[0-9]+ (Warning|Error)' ; dotnet test DungeonMasterXIV.sln --no-build 2>&1 | grep -E 'Passed!|Failed!'`

Expected:
- 0 warnings and 0 errors;
- totals 1 / 6 / 7, all passed, 0 skipped.

- [ ] **Step 5: Commit**

```bash
git add Windows/RailWindow.cs src/DungeonMasterXIV.Core/Data/PluginSettings.cs Plugin.cs
git commit -F - <<'EOF'
feat(ui): a launcher rail replaces the main window

A movable column of buttons, one per window that exists, lit while its window is open and
collapsible to one button. /dmx and Dalamud's open button toggle it; the first time it opens, the
session window opens beside it.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 5: The session window, with the stream on screen

Implements spec §4 "Session window" and "Session ending".

**Files:**
- Create: `Windows/StreamView.cs`
- Modify (each is rewritten; the full new file is given): `Windows/SessionWindow.cs`, `Windows/MessageComposeView.cs`, `Windows/AdmissionPromptView.cs`, `Windows/JoinFlowView.cs`, `Windows/SessionEndingView.cs`
- Modify: `Windows/JoinRequestForm.cs`, `Plugin.cs`
- Delete: `Windows/RosterView.cs`

**Interfaces:**
- Consumes:
  - from Task 1: `SessionCoordinator.StreamLines`, `.CurrentRoster`, `.InASession`, `.Say(string?, DateTimeOffset)` and `.ShareRoll(SharedRoll, DateTimeOffset)`; `SpeakerBook`; `SharedRoll.From`;
  - Task 3's components; Task 2's `ThemedWindow`.
- Produces:
  - `SessionWindow(SessionCoordinator, UiFonts, IPluginLog, Func<DisplayName>, HostingCampaign, Func<RelinkMemory>, KeepOrLose)`;
  - `internal sealed record LocalRoll(long AtUtcTicks, SharedRoll Roll)`;
  - `StreamView.MostShown = 300`.

Behaviour to keep exactly, because each piece carries a spec rule:
- the reconnecting line disables the composer;
- `AdmissionPrompt.Favoured` gets default focus;
- the keep-or-lose offer is shown after leaving;
- "Ask me each time" / "Let them straight in";
- every existing message string.

New behaviour:
- **Leave session** and **End session** ask for confirmation (spec §3 `ActionRow`).
- A local roll shows as a card marked "Only you saw this."

- [ ] **Step 1: The stream view**

Create `Windows/StreamView.cs`. It learns names from the current roster every frame. `SpeakerBook` keeps them, so a line from someone who has left still names them. It draws the latest 300 entries.

`Windows/StreamView.cs` (new file, complete):

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>A roll this client made outside a session, shown only here.</summary>
internal sealed record LocalRoll(long AtUtcTicks, SharedRoll Roll);

/// <summary>The session stream as cards and event lines, pinned to the newest entry unless scrolled up.</summary>
internal sealed class StreamView
{
    /// <summary>The most entries drawn each frame; older ones stay in the session log.</summary>
    public const int MostShown = 300;

    private readonly SessionCoordinator _coordinator;
    private readonly UiFonts _fonts;
    private readonly SpeakerBook _speakers = new();
    private int _lastCount;
    private bool _newBelow;

    public StreamView(SessionCoordinator coordinator, UiFonts fonts)
    {
        _coordinator = coordinator;
        _fonts = fonts;
    }

    public void Draw(float height, SpeakerName you, IReadOnlyList<LocalRoll> localRolls)
    {
        _speakers.Learn(_coordinator.CurrentRoster);
        var lines = _coordinator.InASession ? _coordinator.StreamLines : [];

        using var child = ImRaii.Child("##stream", new Vector2(0f, height), false);
        if (!child.Success)
        {
            return;
        }

        var atBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f;
        var count = lines.Count + localRolls.Count;

        if (lines.Count > MostShown)
        {
            using var meta = _fonts.Meta.Push();
            ImGui.TextColored(Palette.TextMuted, $"Showing the latest {MostShown} entries.");
        }

        foreach (var line in lines.Skip(Math.Max(0, lines.Count - MostShown)))
        {
            DrawLine(line);
        }

        foreach (var local in localRolls)
        {
            RollCard.Draw(_fonts, you, local.AtUtcTicks, local.Roll, local: true);
        }

        if (count > _lastCount)
        {
            if (atBottom)
            {
                ImGui.SetScrollHereY(1f);
            }
            else
            {
                _newBelow = true;
            }
        }

        if (atBottom)
        {
            _newBelow = false;
        }

        _lastCount = count;

        if (_newBelow)
        {
            DrawNewBelow();
        }
    }

    private void DrawLine(StreamLine line)
    {
        var speaker = _speakers.For(line.Peer);

        switch (line.Kind)
        {
            case StreamEventKind.Message:
                MessageCard.Draw(_fonts, speaker, line.AtUtcTicks, line.Text);
                break;
            case StreamEventKind.Roll when line.Roll is { } roll:
                RollCard.Draw(_fonts, speaker, line.AtUtcTicks, roll);
                break;
            case StreamEventKind.Roll:
                MessageCard.Draw(_fonts, speaker, line.AtUtcTicks, line.Text);
                break;
            default:
                EventLine.Draw(_fonts, line.Kind, speaker, line.AtUtcTicks);
                break;
        }
    }

    private void DrawNewBelow()
    {
        var label = "New below ↓";
        var width = ImGui.CalcTextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2f);
        var scroll = new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());
        var region = ImGui.GetWindowSize();
        ImGui.SetCursorPos(scroll + new Vector2((region.X - width) / 2f, region.Y - ImGui.GetFrameHeight() - Metrics.Step));

        if (ActionRow.Primary(label))
        {
            ImGui.SetScrollHereY(1f);
            _newBelow = false;
        }
    }
}
```

- [ ] **Step 2: The composer**

Replace `Windows/MessageComposeView.cs`. In a session:
- `/roll` shares the roll (the share cap and refusals come from Task 1);
- anything else is said.

Outside a session, `/roll` adds a local card, and saying something returns the existing "This client is not in a session." refusal.

`Windows/MessageComposeView.cs` (new file, complete):

```csharp
using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using DungeonMasterXIV.Chat;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Rolls;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The chat box: sends a message or shares a roll in a session, or rolls only for you outside one.</summary>
internal sealed class MessageComposeView
{
    private readonly SessionCoordinator _coordinator;

    private readonly RollEvaluator _rolls = new(new SystemDieRoller());

    private readonly List<LocalRoll> _local = new();

    private string _entry = string.Empty;

    private string? _refusal;

    private bool _refocus;

    public MessageComposeView(SessionCoordinator coordinator) => _coordinator = coordinator;

    internal string? Refusal => _refusal;

    public IReadOnlyList<LocalRoll> LocalRolls => _local;

    /// <summary>The height Draw needs, so the stream above can take the rest.</summary>
    public float Height =>
        ImGui.GetFrameHeightWithSpacing() + (_refusal is null ? 0f : ImGui.GetTextLineHeightWithSpacing() * 2f);

    public void Draw()
    {
        if (_coordinator.InASession && _local.Count > 0)
        {
            _local.Clear();
        }

        var reconnecting = _coordinator.ReconnectingLine is not null;

        using (ImRaii.Disabled(reconnecting))
        {
            var send = ImGui.GetStyle().ItemSpacing.X + ImGui.CalcTextSize("Send").X + (ImGui.GetStyle().FramePadding.X * 2f);
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - send);

            if (_refocus)
            {
                ImGui.SetKeyboardFocusHere();
                _refocus = false;
            }

            var entered = ImGui.InputTextWithHint(
                "##compose",
                "Say something, or /roll 1d20",
                ref _entry,
                MessageLimits.Default.MaxUtf8Bytes,
                ImGuiInputTextFlags.EnterReturnsTrue);

            ImGui.SameLine();
            if (ActionRow.Primary("Send") || entered)
            {
                Submit();
                _refocus = true;
            }
        }

        if (_refusal is { } refusal)
        {
            Banner.Refusal(refusal);
        }
    }

    internal void Submit()
    {
        if (RollCommand.TryRead(_entry, out var expression))
        {
            Roll(expression);
            return;
        }

        var draft = _coordinator.Say(_entry, DateTimeOffset.UtcNow);

        _refusal = draft.IsAccepted ? null : draft.Reason;

        if (draft.IsAccepted)
        {
            _entry = string.Empty;
        }
    }

    private void Roll(string expression)
    {
        var outcome = _rolls.Evaluate(expression);
        if (!outcome.Evaluated)
        {
            _refusal = outcome.Message;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var roll = SharedRoll.From(expression, outcome);

        if (_coordinator.InASession)
        {
            _refusal = _coordinator.ShareRoll(roll, now);
        }
        else
        {
            _local.Add(new LocalRoll(now.UtcTicks, roll));
            _refusal = null;
        }

        if (_refusal is null)
        {
            _entry = string.Empty;
        }
    }
}
```

- [ ] **Step 3: Admission requests as cards**

Replace `Windows/AdmissionPromptView.cs`:

`Windows/AdmissionPromptView.cs` (new file, complete):

```csharp
using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>Shows the host each pending join request as a card with Admit and Deny.</summary>
internal sealed class AdmissionPromptView
{
    private readonly SessionCoordinator _coordinator;
    private readonly UiFonts _fonts;

    public AdmissionPromptView(SessionCoordinator coordinator, UiFonts fonts)
    {
        _coordinator = coordinator;
        _fonts = fonts;
    }

    public void Draw()
    {
        var pending = _coordinator.Admissions.Pending;
        if (pending.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var request in pending.ToArray())
        {
            using var card = Card.Begin(Palette.SurfaceRaised, Palette.Gold);
            ImGui.PushID(request.PeerCode.Value);

            ImGui.TextWrapped(AdmissionPrompt.Headline(request));
            using (_fonts.Meta.Push())
            {
                ImGui.TextColored(Palette.TextMuted, $"This request lapses in {request.RemainingAt(now):mm\\:ss}");
            }

            DrawActions(request);
            ImGui.PopID();
        }
    }

    private void DrawActions(PendingAdmission request)
    {
        var favoured = AdmissionPrompt.Favoured(request) == AdmissionAction.Admit;

        if (_coordinator.CanAdmitAsClaimed(request))
        {
            var label = string.IsNullOrEmpty(request.RelinkLabel) ? "returning player" : request.RelinkLabel;
            if (ActionRow.Primary($"Admit as {label}"))
            {
                _coordinator.Admit(request.PeerCode, asClaimed: true);
            }

            ImGui.SameLine();
            if (ActionRow.Secondary("Admit as a new player"))
            {
                _coordinator.Admit(request.PeerCode);
            }
        }
        else if (ActionRow.Primary("Admit"))
        {
            _coordinator.Admit(request.PeerCode);
        }

        if (favoured)
        {
            ImGui.SetItemDefaultFocus();
        }

        ImGui.SameLine();
        if (ActionRow.Secondary("Deny"))
        {
            _coordinator.Deny(request.PeerCode);
        }
    }
}
```

- [ ] **Step 4: The joiner's side, and session ending**

Replace `Windows/JoinFlowView.cs`. It splits into three parts:
- `DrawStatus` (while joining or joined);
- `DrawForm` (inside the empty state);
- `DrawProblems` (banners, always outside any table, since a `Card` must not be drawn in a table cell).

`Windows/JoinFlowView.cs` (new file, complete):

```csharp
using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The joiner's side of the session window: status, banners, leaving, and the request form.</summary>
internal sealed class JoinFlowView
{
    private readonly SessionCoordinator _coordinator;

    private readonly UiFonts _fonts;

    private readonly SessionEndingView _ending;

    private readonly JoinRequestForm _requestForm;

    public JoinFlowView(
        SessionCoordinator coordinator,
        UiFonts fonts,
        Func<DisplayName> displayName,
        Func<RelinkMemory> relink,
        KeepOrLose keepOrLose)
    {
        _coordinator = coordinator;
        _fonts = fonts;
        _requestForm = new JoinRequestForm(coordinator, displayName, relink);
        _ending = new SessionEndingView(coordinator, fonts, keepOrLose);
    }

    /// <summary>True while a join is under way or admitted, so the session window shows this side.</summary>
    public bool IsActive => _coordinator.Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted;

    /// <summary>The status line, countdowns and problems; drawn above the stream.</summary>
    public void DrawStatus()
    {
        var join = _coordinator.Join;

        using (_fonts.Meta.Push())
        {
            ImGui.TextColored(Palette.TextMuted, $"Joining: {DescribeJoin(join.Phase)}");
        }

        if (join.Phase == JoinPhase.AwaitingDecision)
        {
            Banner.Draw(_fonts, BannerKind.Info, $"The DM has {join.RemainingAt(DateTimeOffset.UtcNow):mm\\:ss} left to answer");
        }

        DrawProblems();
        _ending.DrawLeaving(join);
    }

    /// <summary>The join form, for the Join side of the empty state. Its problems are drawn by DrawProblems.</summary>
    public void DrawForm()
    {
        if (!_coordinator.InAHostedSession && (_coordinator.Join.MayRequestAgain || _coordinator.Join.Phase == JoinPhase.Denied))
        {
            if (_coordinator.Join.Phase is JoinPhase.Denied or JoinPhase.Lapsed)
            {
                using var meta = _fonts.Meta.Push();
                ImGui.TextColored(Palette.TextMuted, $"Joining: {DescribeJoin(_coordinator.Join.Phase)}");
            }

            _requestForm.Draw();
        }
    }

    /// <summary>The keep-or-lose offer after leaving, drawn as a card where the stream was.</summary>
    public bool DrawOffer() => _ending.DrawOffer();

    /// <summary>A failed join and undelivered messages, as banners. Never drawn inside a table cell.</summary>
    public void DrawProblems()
    {
        var join = _coordinator.Join;

        if (join.Failure != SessionFailure.None)
        {
            Banner.Draw(_fonts, BannerKind.Danger, SessionFailureMessage.For(join.Failure));
        }

        if (_coordinator.Membership.Undelivered > 0)
        {
            Banner.Draw(
                _fonts, BannerKind.Warning, $"{_coordinator.Membership.Undelivered} messages you sent were not delivered.");
        }
    }

    private static string DescribeJoin(JoinPhase phase) => phase switch
    {
        JoinPhase.Idle => "not in a session",
        JoinPhase.Contacting => "contacting the relay",
        JoinPhase.AwaitingDecision => "waiting for the DM to decide",
        JoinPhase.Admitted => "in the session",
        JoinPhase.Denied => "not admitted",
        JoinPhase.Lapsed => "the DM did not answer in time - you can ask again",
        _ => "stopped after a problem",
    };
}
```

Replace `Windows/SessionEndingView.cs`:

`Windows/SessionEndingView.cs` (new file, complete):

```csharp
using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>Shows a joiner the closing countdown, a Leave button, and after leaving the offer to keep the log.</summary>
internal sealed class SessionEndingView
{
    private readonly SessionCoordinator _coordinator;

    private readonly UiFonts _fonts;

    private readonly KeepOrLose _keepOrLose;

    private readonly DangerAction _leave = new();

    private SessionLogOffer? _offer;

    public SessionEndingView(SessionCoordinator coordinator, UiFonts fonts, KeepOrLose keepOrLose)
    {
        _coordinator = coordinator;
        _fonts = fonts;
        _keepOrLose = keepOrLose;
    }

    public void DrawLeaving(JoinAttempt join)
    {
        ArgumentNullException.ThrowIfNull(join);

        if (_coordinator.Membership.Closing is { } closing)
        {
            Banner.Draw(
                _fonts,
                BannerKind.Warning,
                $"The DM has ended this session. It closes in {closing.RemainingAt(DateTimeOffset.UtcNow):mm\\:ss}");
        }

        if (join.Phase != JoinPhase.Admitted)
        {
            return;
        }

        if (_leave.Draw("Leave session", "Yes, leave"))
        {
            _offer = _keepOrLose.Open();
            _coordinator.Membership.Leave();
        }
    }

    /// <summary>Draws the offer while it is open; returns true when it drew one.</summary>
    public bool DrawOffer()
    {
        if (_offer is not { IsOpen: true } offer)
        {
            return false;
        }

        var remaining = offer.RemainingAt(DateTimeOffset.UtcNow.UtcTicks);
        if (offer.ElapseTo(DateTimeOffset.UtcNow.UtcTicks))
        {
            return false;
        }

        using var card = Card.Begin(Palette.SurfaceRaised, Palette.Gold);
        ImGui.TextWrapped(
            offer.HasAnything
                ? $"Keep this session's log? {offer.LineCount} lines, {offer.Participants.Count} people. {remaining:mm\\:ss}"
                : $"This session recorded nothing to keep. {remaining:mm\\:ss}");

        if (ActionRow.Primary("Keep"))
        {
            SessionExport.Produce(offer, _keepOrLose.Export);
        }

        ImGui.SameLine();
        if (ActionRow.Secondary("Discard"))
        {
            offer.Decline();
        }

        return true;
    }
}
```

`Windows/JoinRequestForm.cs` (diff):

```diff
diff --git a/Windows/JoinRequestForm.cs b/Windows/JoinRequestForm.cs
index 7815a40..abc64a8 100644
--- a/Windows/JoinRequestForm.cs
+++ b/Windows/JoinRequestForm.cs
@@ -2,6 +2,7 @@ using System;
 using Dalamud.Bindings.ImGui;
 using DungeonMasterXIV.Data;
 using DungeonMasterXIV.Net;
+using DungeonMasterXIV.Windows.Ui.Components;
 
 namespace DungeonMasterXIV.Windows;
 
@@ -51,7 +52,7 @@ internal sealed class JoinRequestForm
             : $"That name cannot be sent, so they will see \"{DisplayName.Unstated}\". Letters, "
               + "digits, spaces, apostrophes and hyphens work.");
 
-        if (ImGui.Button("Request to join") && JoinFlowCode.Accepts(_codeEntry, out var code))
+        if (ActionRow.Primary("Request to join") && JoinFlowCode.Accepts(_codeEntry, out var code))
         {
             _coordinator.RequestJoin(code, willSend, _relink().IdFor(code));
         }
```

- [ ] **Step 5: The session window**

Replace `Windows/SessionWindow.cs`. Its title is the campaign name while hosting one, or "Session" otherwise. The `###dmx-session` ID is kept, so Dalamud's stored geometry carries over.

`Windows/SessionWindow.cs` (new file, complete):

```csharp
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The session window: how to host or join, the session's status and people, requests, the stream and the chat box.</summary>
internal sealed class SessionWindow : ThemedWindow
{
    private const string CodeChangedWarning =
        "Your session code changed while you were disconnected, because it was taken by another "
        + "session. Your players are still holding the old one - read them the new code below.";

    private readonly SessionCoordinator _coordinator;

    private readonly Func<DisplayName> _displayName;

    private readonly HostingCampaign _hosting;

    private readonly HostCampaignPicker _campaignPicker;

    private readonly AdmissionPromptView _admissionPrompts;

    private readonly JoinFlowView _joinFlow;

    private readonly MessageComposeView _compose;

    private readonly StreamView _stream;

    private readonly DangerAction _endSession = new();

    /// <summary>Below this width the Host and Join paths stack instead of sitting side by side.</summary>
    private const float SideBySideWidth = 560f;

    public SessionWindow(
        SessionCoordinator coordinator,
        UiFonts fonts,
        IPluginLog log,
        Func<DisplayName> displayName,
        HostingCampaign hosting,
        Func<RelinkMemory> relink,
        KeepOrLose keepOrLose)
        : base("Session###dmx-session", fonts, log)
    {
        _coordinator = coordinator;
        _displayName = displayName;
        _hosting = hosting;
        _admissionPrompts = new AdmissionPromptView(coordinator, fonts);
        _campaignPicker = new HostCampaignPicker(hosting);
        _joinFlow = new JoinFlowView(coordinator, fonts, displayName, relink, keepOrLose);
        _compose = new MessageComposeView(coordinator);
        _stream = new StreamView(coordinator, fonts);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Open() => IsOpen = true;

    public override void PreDraw()
    {
        WindowName = $"{Title()}###dmx-session";
        base.PreDraw();
    }

    protected override void DrawContent()
    {
        if (_coordinator.InAHostedSession)
        {
            DrawHosting();
        }
        else if (_joinFlow.IsActive)
        {
            _joinFlow.DrawStatus();
            DrawPeople(host: false);
        }
        else
        {
            DrawNotInASession();
        }

        _admissionPrompts.Draw();

        var you = new SpeakerName(_displayName().Value, _coordinator.InAHostedSession ? SessionRole.DungeonMaster : SessionRole.Player);
        var streamHeight = ImGui.GetContentRegionAvail().Y - _compose.Height - ImGui.GetStyle().ItemSpacing.Y;
        _stream.Draw(Math.Max(streamHeight, ImGui.GetFrameHeight()), you, _compose.LocalRolls);
        _compose.Draw();
    }

    private string Title() =>
        _coordinator.InAHostedSession && _hosting.Current is { } campaign ? CampaignName.For(campaign) : "Session";

    private void DrawNotInASession()
    {
        if (_joinFlow.DrawOffer())
        {
            return;
        }

        if (_coordinator.Host.Failure != SessionFailure.None)
        {
            Banner.Draw(Fonts, BannerKind.Danger, SessionFailureMessage.For(_coordinator.Host.Failure));
        }

        EmptyState.Draw(Fonts, "No session yet", "Start one as the DM, or join one with the code your DM gives you.");

        var sideBySide = ImGui.GetContentRegionAvail().X >= SideBySideWidth * ImGuiHelpers.GlobalScale;
        if (sideBySide && ImGui.BeginTable("##paths", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            DrawHostPath();
            ImGui.TableNextColumn();
            DrawJoinPath();
            ImGui.EndTable();
        }
        else
        {
            DrawHostPath();
            DrawJoinPath();
        }

        _joinFlow.DrawProblems();
    }

    private void DrawHostPath()
    {
        Section.Heading(Fonts, "Host");
        _campaignPicker.Draw();
        if (ActionRow.Primary("Start session"))
        {
            _hosting.StartFor();
            _coordinator.StartHosting();
        }
    }

    private void DrawJoinPath()
    {
        Section.Heading(Fonts, "Join");
        _joinFlow.DrawForm();
    }

    private void DrawHosting()
    {
        var host = _coordinator.Host;

        if (host.Phase == HostingPhase.Registering)
        {
            Banner.Draw(Fonts, BannerKind.Info, "Hosting: registering with the relay");
            return;
        }

        if (host.CodeChangedMidSession)
        {
            Banner.Draw(Fonts, BannerKind.Warning, CodeChangedWarning);
            if (ActionRow.Secondary("I have told them"))
            {
                host.AcknowledgeCodeChange();
            }
        }

        if (_coordinator.ReconnectingLine is { } reconnecting)
        {
            Banner.Draw(Fonts, BannerKind.Warning, reconnecting);
        }

        if (host.Code is { } code)
        {
            CodeDisplay.Draw(Fonts, code);
        }

        DrawPeople(host: true);

        if (_endSession.Draw("End session", "Yes, end it for everyone"))
        {
            _coordinator.StopHosting(DateTimeOffset.UtcNow);
            _hosting.Ended();
        }
    }

    private void DrawPeople(bool host)
    {
        var roster = _coordinator.CurrentRoster;
        if (!Section.Collapsible(Fonts, $"At the table · {roster.Count}###table"))
        {
            return;
        }

        foreach (var entry in roster)
        {
            var away = host
                && PeerCode.TryParse(entry.PeerCode, out var peer)
                && _coordinator.Drops.WhenDropped(peer) is not null;
            RosterRow.Draw(Fonts, new SpeakerName(DisplayName.OrNone(entry.DisplayName).Value, entry.Role), away);
        }

        if (!host)
        {
            return;
        }

        using (Fonts.Meta.Push())
        {
            ImGui.TextColored(Palette.TextMuted, "Returning players");
        }

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
    }
}
```

```bash
git rm Windows/RosterView.cs
```

`Plugin.cs` (diff):

```diff
diff --git a/Plugin.cs b/Plugin.cs
index a4acc5b..3eb1116 100644
--- a/Plugin.cs
+++ b/Plugin.cs
@@ -74,6 +74,8 @@ public sealed class Plugin : IDalamudPlugin
                 LetReturningPlayersIn: () => _hostingCampaign.LetsReturningPlayersIn));
         _sessionWindow = new SessionWindow(
             _sessionCoordinator,
+            _fonts,
+            log,
             NameWeSendAs(characterName),
             _hostingCampaign,
             () => _configurationStore.Configuration.Settings.Relink, SessionEndChoiceFor(pluginInterface.ConfigDirectory));
```

- [ ] **Step 6: Build and test**

Run: `dotnet build DungeonMasterXIV.sln 2>&1 | grep -E '^ *[0-9]+ (Warning|Error)' ; dotnet test DungeonMasterXIV.sln --no-build 2>&1 | grep -E 'Passed!|Failed!'`

Expected:
- 0 warnings and 0 errors;
- totals 1 / 6 / 7, all passed, 0 skipped.

- [ ] **Step 7: Commit**

```bash
git add Windows Plugin.cs
git commit -F - <<'EOF'
feat(ui): the session window shows the stream as cards

Host and Join paths when not in a session; the code, the table and End session when hosting;
admission requests as cards; messages, rolls and membership events in a stream pinned to the newest
entry; one composer that says, shares a roll, or rolls only for you.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 6: The settings window

Implements spec §4 "Settings window". The contents and behaviour are unchanged: sections and banners replace separators, and **no wording changes**.

**Files:**
- Modify: `Windows/ConfigWindow.cs`, `Windows/CampaignStorageView.cs`, `Windows/RelinkMemoryView.cs`, `Plugin.cs`

**Interfaces:**
- Consumes: `ThemedWindow`, `Section`, `Banner`, `ActionRow`, `Palette`.
- Produces: `ConfigWindow(ConfigurationStore, Func<DisplayName>, Func<Campaign?>, Action<Campaign>, CampaignStorageView, UiFonts, IPluginLog)`, now `internal`. `CampaignStorageView` and `RelinkMemoryView` are now `internal`.

- [ ] **Step 1: Settings in sections**

The window is grouped into **You** (display name, window restore), **Connection** (relay, policy link), **Campaigns** and **Stored data**. The "Display name" and "Relay" labels are dropped, because the section headings replace them.

`Windows/ConfigWindow.cs` (diff):

```diff
diff --git a/Windows/ConfigWindow.cs b/Windows/ConfigWindow.cs
index dc1c538..01b3528 100644
--- a/Windows/ConfigWindow.cs
+++ b/Windows/ConfigWindow.cs
@@ -1,14 +1,17 @@
 using System;
 using Dalamud.Bindings.ImGui;
 using Dalamud.Interface.Windowing;
+using Dalamud.Plugin.Services;
 using DungeonMasterXIV.Campaigns;
 using DungeonMasterXIV.Data;
 using DungeonMasterXIV.Net;
+using DungeonMasterXIV.Windows.Ui;
+using DungeonMasterXIV.Windows.Ui.Components;
 
 namespace DungeonMasterXIV.Windows;
 
 /// <summary>The settings window: window restore, display name, relay address, the relay policy link, and campaign and user storage.</summary>
-public sealed class ConfigWindow : Window
+internal sealed class ConfigWindow : ThemedWindow
 {
     private readonly ConfigurationStore _configurationStore;
     private readonly Func<DisplayName> _characterName;
@@ -45,8 +48,10 @@ public sealed class ConfigWindow : Window
         Func<DisplayName> characterName,
         Func<Campaign?> currentCampaign,
         Action<Campaign> saveCampaign,
-        CampaignStorageView campaignStorage)
-        : base("Dungeon Master XIV settings###dmx-settings")
+        CampaignStorageView campaignStorage,
+        UiFonts fonts,
+        IPluginLog log)
+        : base("Settings###dmx-settings", fonts, log)
     {
         _configurationStore = configurationStore;
         _characterName = characterName;
@@ -71,10 +76,13 @@ public sealed class ConfigWindow : Window
 
     public void Open() => IsOpen = true;
 
-    public override void Draw()
+    protected override void DrawContent()
     {
         var settings = _configurationStore.Configuration.Settings;
 
+        Section.Heading(Fonts, "You");
+        DrawDisplayNameSetting(settings);
+
         var restore = settings.RestoreWindowState;
         if (ImGui.Checkbox("Reopen windows where I left them", ref restore))
         {
@@ -82,32 +90,29 @@ public sealed class ConfigWindow : Window
             _configurationStore.Save();
         }
 
-        ImGui.Separator();
-        DrawDisplayNameSetting(settings);
-
-        ImGui.Separator();
+        Section.Heading(Fonts, "Connection");
         DrawRelaySetting(settings);
 
-        if (ImGui.Button("Relay and privacy"))
+        if (ActionRow.Secondary("Relay and privacy"))
         {
             Dalamud.Utility.Util.OpenLink(RelayPolicyUrl);
         }
 
-        ImGui.Separator();
-        ImGui.TextUnformatted("Campaign storage");
+        Section.Heading(Fonts, "Campaigns");
         _campaignStorage.Draw();
 
-        ImGui.Separator();
-        ImGui.TextUnformatted("User storage");
+        Section.Heading(Fonts, "Stored data");
         _relinkMemory.Draw();
 
-        ImGui.Separator();
-        ImGui.TextDisabled(_schemaVersionLabel);
+        ImGui.Spacing();
+        using (Fonts.Meta.Push())
+        {
+            ImGui.TextColored(Palette.TextMuted, _schemaVersionLabel);
+        }
     }
 
     private void DrawDisplayNameSetting(PluginSettings settings)
     {
-        ImGui.TextUnformatted("Display name");
 
         var characterName = _characterName();
 
@@ -116,7 +121,7 @@ public sealed class ConfigWindow : Window
 
         if (NameInputCapacity.IsFull(typed))
         {
-            ImGui.TextWrapped(NameFieldIsFull);
+            Banner.Draw(Fonts, BannerKind.Warning, NameFieldIsFull);
         }
 
         var effective = CampaignDisplayName.Or(campaign, characterName);
@@ -125,7 +130,7 @@ public sealed class ConfigWindow : Window
         var stored = CampaignDisplayName.Stored(campaign);
         if (stored.Length > 0 && !DisplayName.TryParse(stored, out _))
         {
-            ImGui.TextWrapped(UnusableAliasWarning);
+            Banner.Draw(Fonts, BannerKind.Warning, UnusableAliasWarning);
         }
     }
 
@@ -145,7 +150,7 @@ public sealed class ConfigWindow : Window
         if (noCampaign)
         {
             ImGui.EndDisabled();
-            ImGui.TextWrapped(NameNeedsACampaign);
+            Banner.Draw(Fonts, BannerKind.Info, NameNeedsACampaign);
         }
 
         if (edited)
@@ -161,7 +166,6 @@ public sealed class ConfigWindow : Window
 
     private void DrawRelaySetting(PluginSettings settings)
     {
-        ImGui.TextUnformatted("Relay");
         var address = settings.RelayAddress;
         if (ImGui.InputText("Relay address", ref address, 256))
         {
@@ -171,7 +175,7 @@ public sealed class ConfigWindow : Window
 
         if (!RelayEndpoint.TryParse(settings.RelayAddress, out _))
         {
-            ImGui.TextWrapped(InvalidRelayWarning);
+            Banner.Draw(Fonts, BannerKind.Danger, InvalidRelayWarning);
         }
     }
 
```

- [ ] **Step 2: Storage views use the button vocabulary**

`Windows/CampaignStorageView.cs` (diff):

```diff
diff --git a/Windows/CampaignStorageView.cs b/Windows/CampaignStorageView.cs
index b6dc5f5..7362861 100644
--- a/Windows/CampaignStorageView.cs
+++ b/Windows/CampaignStorageView.cs
@@ -3,11 +3,13 @@ using System.Collections.Generic;
 using Dalamud.Bindings.ImGui;
 using DungeonMasterXIV.Campaigns;
 using DungeonMasterXIV.Data;
+using DungeonMasterXIV.Windows.Ui;
+using DungeonMasterXIV.Windows.Ui.Components;
 
 namespace DungeonMasterXIV.Windows;
 
 /// <summary>Lists stored campaigns and unreadable campaign files in settings, each with a delete that asks to confirm.</summary>
-public sealed class CampaignStorageView
+internal sealed class CampaignStorageView
 {
     private readonly CampaignStore _store;
 
@@ -29,7 +31,7 @@ public sealed class CampaignStorageView
 
         if (_rows.Count == 0)
         {
-            ImGui.TextDisabled("No campaigns stored yet.");
+            ImGui.TextColored(Palette.TextMuted, "No campaigns stored yet.");
         }
 
         foreach (var row in _rows)
@@ -72,7 +74,7 @@ public sealed class CampaignStorageView
             {
                 DrawConfirmation();
             }
-            else if (ImGui.Button("Delete file"))
+            else if (ActionRow.Secondary("Delete file"))
             {
                 _prompt.Request(row.FileName);
             }
@@ -86,14 +88,14 @@ public sealed class CampaignStorageView
     {
         ImGui.PushID(row.CampaignId.ToString());
         ImGui.TextUnformatted(row.Label);
-        ImGui.TextDisabled(row.Detail);
+        ImGui.TextColored(Palette.TextMuted, row.Detail);
         ImGui.SameLine();
 
         if (_prompt.IsAwaiting(row.CampaignId))
         {
             DrawConfirmation();
         }
-        else if (ImGui.Button("Delete"))
+        else if (ActionRow.Secondary("Delete"))
         {
             _prompt.Request(row.CampaignId);
         }
@@ -107,14 +109,14 @@ public sealed class CampaignStorageView
         ImGui.TextUnformatted("Delete permanently?");
         ImGui.SameLine();
 
-        if (ImGui.Button("Yes, delete"))
+        if (ActionRow.Edged("Yes, delete", Palette.Danger, Palette.Danger))
         {
             _prompt.Confirm();
         }
 
         ImGui.SameLine();
 
-        if (ImGui.Button("Cancel"))
+        if (ActionRow.Secondary("Cancel"))
         {
             _prompt.Cancel();
         }
```

`Windows/RelinkMemoryView.cs` (diff):

```diff
diff --git a/Windows/RelinkMemoryView.cs b/Windows/RelinkMemoryView.cs
index 8371015..777df74 100644
--- a/Windows/RelinkMemoryView.cs
+++ b/Windows/RelinkMemoryView.cs
@@ -3,11 +3,13 @@ using System.Linq;
 using Dalamud.Bindings.ImGui;
 using DungeonMasterXIV.Data;
 using DungeonMasterXIV.Net;
+using DungeonMasterXIV.Windows.Ui;
+using DungeonMasterXIV.Windows.Ui.Components;
 
 namespace DungeonMasterXIV.Windows;
 
 /// <summary>Lists the participant ids remembered per session code, each with a Forget that asks to confirm.</summary>
-public sealed class RelinkMemoryView
+internal sealed class RelinkMemoryView
 {
     private readonly Func<RelinkMemory> _relink;
     private readonly Action _persist;
@@ -49,7 +51,7 @@ public sealed class RelinkMemoryView
                 continue;
             }
 
-            if (ImGui.Button($"{RelinkDisclosure.BeginForgetting}##{entry.SessionCode}"))
+            if (ActionRow.Secondary($"{RelinkDisclosure.BeginForgetting}##{entry.SessionCode}"))
             {
                 _confirming = entry.SessionCode;
             }
@@ -60,14 +62,14 @@ public sealed class RelinkMemoryView
     {
         ImGui.TextWrapped(RelinkDisclosure.BeforeForgetting(sessionCode));
 
-        if (ImGui.Button($"{RelinkDisclosure.KeepIt}##keep-{sessionCode}"))
+        if (ActionRow.Secondary($"{RelinkDisclosure.KeepIt}##keep-{sessionCode}"))
         {
             _confirming = string.Empty;
         }
 
         ImGui.SameLine();
 
-        if (!ImGui.Button($"{RelinkDisclosure.ConfirmForget}##forget-{sessionCode}"))
+        if (!ActionRow.Edged($"{RelinkDisclosure.ConfirmForget}##forget-{sessionCode}", Palette.Danger, Palette.Danger))
         {
             return;
         }
```

- [ ] **Step 3: Wire it**

`Plugin.cs` (diff):

```diff
diff --git a/Plugin.cs b/Plugin.cs
index 3eb1116..3443fee 100644
--- a/Plugin.cs
+++ b/Plugin.cs
@@ -57,7 +57,7 @@ public sealed class Plugin : IDalamudPlugin
         var characterName = new LocalCharacterName(objects).Current;
 
         _hostingCampaign = new HostingCampaign(_campaignStore);
-        _configWindow = SettingsWindowFor(characterName, pluginInterface.ConfigDirectory);
+        _configWindow = SettingsWindowFor(characterName, pluginInterface.ConfigDirectory, log);
         var sessionLog = new SessionTransportLog(log);
         _relayTransport = new WebSocketSessionTransport(sessionLog);
         _sessionCoordinator = new SessionCoordinator(
@@ -104,7 +104,7 @@ public sealed class Plugin : IDalamudPlugin
     private Func<DisplayName> NameWeSendAs(Func<DisplayName> characterName) =>
         () => CampaignDisplayName.Or(_hostingCampaign.Current, characterName());
 
-    private ConfigWindow SettingsWindowFor(Func<DisplayName> characterName, DirectoryInfo configDirectory)
+    private ConfigWindow SettingsWindowFor(Func<DisplayName> characterName, DirectoryInfo configDirectory, IPluginLog log)
     {
         var retainedLogs = new RetainedLogStore(
             new RetainedLogFileArchive(Path.Combine(configDirectory.FullName, "logs")));
@@ -114,7 +114,9 @@ public sealed class Plugin : IDalamudPlugin
             characterName,
             () => _hostingCampaign.Current,
             _campaignStore.Save,
-            new CampaignStorageView(_campaignStore, new CampaignDeletion(_campaignStore, retainedLogs)));
+            new CampaignStorageView(_campaignStore, new CampaignDeletion(_campaignStore, retainedLogs)),
+            _fonts,
+            log);
     }
 
     private KeepOrLose SessionEndChoiceFor(DirectoryInfo configDirectory) =>
```

- [ ] **Step 4: Build and test**

Run: `dotnet build DungeonMasterXIV.sln 2>&1 | grep -E '^ *[0-9]+ (Warning|Error)' ; dotnet test DungeonMasterXIV.sln --no-build 2>&1 | grep -E 'Passed!|Failed!'`

Expected:
- 0 warnings and 0 errors;
- totals 1 / 6 / 7, all passed, 0 skipped.

- [ ] **Step 5: Commit**

```bash
git add Windows Plugin.cs
git commit -F - <<'EOF'
feat(ui): the settings window in sections

You, Connection, Campaigns and Stored data, with warnings as banners. Wording and behaviour are
unchanged.

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
EOF
```

---

### Task 7: Whole-branch gates and the in-game pass

No code. Everything here is a check; record each result in the PR description.

- [ ] **Step 1: Warnings against `main`**

```bash
OUT=$(mktemp -d)
git worktree add -q "$OUT/main" main
(cd "$OUT/main" && dotnet build DungeonMasterXIV.sln > "$OUT/main.log" 2>&1)
dotnet build DungeonMasterXIV.sln > "$OUT/branch.log" 2>&1
grep -c 'Build succeeded' "$OUT/main.log" "$OUT/branch.log"
grep -E ' warning ' "$OUT/main.log" | sed 's/\[.*//; s#.*/main/##' | sort -u > "$OUT/warn-main.txt"
grep -E ' warning ' "$OUT/branch.log" | sed 's/\[.*//; s#^.*dungeonmasterxiv[^/]*/##' | sort -u > "$OUT/warn-branch.txt"
diff "$OUT/warn-main.txt" "$OUT/warn-branch.txt" && echo "no new warnings"
git worktree remove "$OUT/main"
```

Expected: both logs count `1` for `Build succeeded`, so both builds actually ran; then `no new warnings`.

- [ ] **Step 2: Tests, with totals**

Run: `dotnet build DungeonMasterXIV.sln && dotnet test DungeonMasterXIV.sln --no-build 2>&1 | grep -E 'Passed!|Failed!'`

Expected: Release.Tests 1, Tests 6, Relay.Tests 7, all passed, Skipped 0. The total is 14, up from 13 on `main`.

- [ ] **Step 3: The release zip carries the fonts**

Run: `dotnet build DungeonMasterXIV.csproj -c Release && unzip -l bin/Release/DungeonMasterXIV/latest.zip | grep -E 'Data/Fonts|ImPlot'`

Expected:
- `Data/Fonts/Cinzel-Regular.ttf`, `Cinzel-Bold.ttf`, `Spectral-Regular.ttf`, `Cinzel-OFL.txt` and `Spectral-OFL.txt` are listed;
- **no** `Dalamud.Bindings.ImPlot.dll`.

- [ ] **Step 4: In game, one client**

This needs FFXIV with Dalamud, so it's done by the person, not an agent. Load the dev build and check each item:
- `/dmx` toggles the rail.
- The rail lights Session/Settings while they are open, collapses to one button and back, and can be dragged by its grip.
- `/dmx settings` opens Settings, in four sections, with the same wording as before.
- Not in a session:
  - the "No session yet" empty state appears;
  - Host and Join sit side by side in a wide window and stack at minimum width (Review Focus 3);
  - `/roll 4d6kh3+2` adds a card marked "Only you saw this.", with the set-aside die struck through.
- Hosting:
  - the code shows large with Copy;
  - At the table lists you with `[DM]`;
  - End session asks before ending.
- At Dalamud font scale 1.5, the time on each card doesn't overlap the name (Review Focus 3).
- Rename `Data/Fonts/Spectral-Regular.ttf` in the dev plugin folder and reload. `/xllog` shows one warning, and text still renders (Review Focus 4). Put the file back.
- Disable and re-enable the plugin five times. You should see exactly one of each window, no errors in `/xllog`, and another plugin's windows unaffected (A-0.6).

- [ ] **Step 5: In game, two or three clients through a relay**

Check each item:
- A player joins: both the host's stream and the player's stream show "… joined" with a time (A-2.27).
- A player's `/roll 4d6kh3+2` appears for the host and for a third client with the same dice (A-2.8, A-2.18, A-2.1).
- The DM's message and roll appear for every player, with `[DM]` before the name (A-2.24a-1). An Assistant shows no `[DM]` (A-2.24b).
- `/roll 600d6` in a session is refused, with the 500-dice cap named, and nothing is sent (Review Focus 1).
- Drop a player's network for under five minutes: "lost connection", then "reconnected". Messages sent meanwhile arrive in order. Send several long messages while they're away, to exercise batching (Review Focus 2).
- Scroll the stream up while messages arrive: it holds still and shows "New below ↓".
- The player leaves (with confirmation): the keep-or-lose offer appears as a card.

- [ ] **Step 6: Open the PR**

Push the branch and open a PR. The description must include:
- the warning-diff result;
- the test totals line;
- the zip listing;
- each in-game check, marked done or not done.

End the description with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
