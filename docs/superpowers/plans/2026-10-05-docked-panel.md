# Docked Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the floating rail and separate windows with one panel whose attached rail switches Chat, Session and Settings tabs. A waiting join request is announced on any tab. Four in-game bugs are fixed: a stale campaign after a failed start, a name box that cannot be cleared, headings smaller than body text, and an empty send that scolds.

**Architecture:**
- **One window.** `PanelWindow` (a `ThemedWindow`) is the plugin's only window. It draws a rail column of `RailButton`s on its left edge, and the selected tab in a child region beside it.
- **Tabs as views.** The three tabs are plain views with a `Draw()` method: `ChatTab`, `SessionTab` and `SettingsTab`. `SessionTab` and `SettingsTab` are carved from today's `SessionWindow` and `ConfigWindow`, and `ChatTab` takes the stream and composer.
- **Shared join view.** `JoinFlowView` is shared between Session (status, form, problems) and Chat (the keep-or-lose card).
- **Settings.** The selected tab and the collapsed state live in `PluginSettings`.
- **Core fixes.** Two small fixes in Core: `HostingCampaign.Follow`, and a cleared name stored as an explicit choice.

**Tech Stack:** C# / .NET 10, Dalamud ImGui bindings and `ImRaii`, xUnit smoke tests.

**Spec:** `docs/superpowers/specs/2026-10-05-docked-panel-design.md`. Read it first. It amends `2026-10-04-ui-foundation-design.md` §4 and core-skeleton R-0.3/R-0.4. The name fix must also satisfy rolls R-2.17: the carried-over name is only a pre-fill, and accepting a name is an act.

**How this plan was checked:** every code block below was built and tested in a scratch worktree off `main`, one commit per task, in this order.
- **Every task:** `dotnet build DungeonMasterXIV.sln -warnaserror` gives 0 warnings and 0 errors, and the tests pass with Release.Tests 1, Tests 8, Relay.Tests 7, and 0 skipped.
- **Task 1's two tests were watched failing first:**
  - the hosting test failed to compile, because `Follow` did not exist;
  - the name test failed with `Assert.True() Failure` from `RecordChosen` returning false.
- **The replayed result matches the draft exactly.**
- **Not checked:** the UI's look and behaviour in game. That is Task 4, and it needs FFXIV.

## Global Constraints

- **Smoke tests only.** The two tests in Task 1 are the only new tests. Keep test comments to one line.
- **Build gate.** `dotnet build DungeonMasterXIV.sln -warnaserror` gives 0 warnings and 0 errors. **Test gate:** Release.Tests 1, Tests 8, Relay.Tests 7, all passed, 0 skipped. Totals are compared, not just `Passed!`.
- **Copy is unchanged** except the strings this plan shows: the join notice, "Review", "Collapse to the rail", "Expand", and the `/dmx` help text.
- **Parentheses mean only "the person behind the speaker".** Square brackets belong only to `[DM]`.
- **Colour is never the only signal.** The badge carries a number, and the notice carries words.
- **Cards and Banners never nest, and are never drawn inside an ImGui table cell.**
- **Admission mechanics stay out of the stream (rolls R-2.3).** The Chat notice is a local `Banner`, never a stream entry.
- **Window geometry is Dalamud's to persist (core-skeleton R-0.3).** The plugin stores only the selected tab and the collapsed state.
- **Commit messages** use `git commit -F -` with a quoted heredoc and end with a `Co-Authored-By:` line naming the model that wrote the commit.

## Review Focus

These failure modes are not exercised by any test here. Read the code for each, and check them in game in Task 4:

1. **Collapse, then expand:** the panel must come back at its previous size, not stuck at the rail's width. *Owner: Task 3 (`PanelWindow.PreDraw`, `SetCollapsed`, `_expandedSize`).*
2. **A join request arrives while the DM is on Chat or Settings, or collapsed:** the badge shows the count, the Chat notice names the person, and both disappear after Admit or Deny. *Owner: Tasks 2 and 3.*
3. **"Start session" in the same frame as `HostingCampaign.Follow`:** the just-started campaign must not be cleared. `HostRunner.Start` sets `Registering` synchronously, so `InAHostedSession` is already true by the next framework update. *Owner: Tasks 1 and 3.*
4. **An existing campaign whose alias is null (never chosen):** it must still offer the carried-over pre-fill, as A-2.31 requires. Only an explicitly cleared alias (`""`) stops it. *Owner: Task 1.*
5. **Settings written by the previous version:** `SettingsWindowOpen`, `RailCollapsed` and `RailIntroduced` are gone from `PluginSettings`. Loading an old settings file must not fail. *Owner: Task 3. Check by reloading the plugin over your existing settings in Task 4.*

---

### Task 1: The two Core bugs

**Files:**
- Modify: `src/DungeonMasterXIV.Core/Campaigns/HostingCampaign.cs`, `src/DungeonMasterXIV.Core/Campaigns/CampaignDisplayName.cs`
- Test: create `tests/DungeonMasterXIV.Tests/AFailedStartClosesItsCampaignTests.cs`, `tests/DungeonMasterXIV.Tests/AClearedNameStaysClearedTests.cs`

**Interfaces:**
- Produces:
  - `HostingCampaign.Follow(bool hosting)`, which clears `Current` whenever `hosting` is false;
  - `CampaignDisplayName.Record` stores `""` for an explicitly cleared name. `ToEdit` and `ToPreFill` read a `null` alias as never chosen (offer the carried-over default) and `""` as chosen to use the character name.

- [ ] **Step 1: Write both tests**

`tests/DungeonMasterXIV.Tests/AFailedStartClosesItsCampaignTests.cs` (new file, complete):

```csharp
using System.Collections.Generic;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>A campaign started for hosting stops being current as soon as hosting is not running.</summary>
public sealed class AFailedStartClosesItsCampaignTests
{
    [Fact]
    public void HostingThatStopsAfterAFailureLeavesNoCampaignCurrent()
    {
        var hosting = new HostingCampaign(new CampaignStore(new MemoryArchive(), new SilentStoreLog()));
        hosting.StartFor();

        hosting.Follow(hosting: false);

        Assert.Null(hosting.Current);
    }

    private sealed class MemoryArchive : ICampaignArchive
    {
        private readonly Dictionary<string, string> _files = new();

        public IReadOnlyList<string> CampaignFiles() => new List<string>(_files.Keys);

        public string? ReadCampaign(string name) => _files.TryGetValue(name, out var contents) ? contents : null;

        public void WriteCampaign(string name, string contents) => _files[name] = contents;

        public bool Delete(string name) => _files.Remove(name);

        public string? ReadLegacy() => null;

        public IReadOnlyList<string> OtherOwnedFiles() => new List<string>();
    }

    private sealed class SilentStoreLog : ICampaignStoreLog
    {
        public void Information(string message)
        {
        }

        public void Warning(string message)
        {
        }
    }
}
```

`tests/DungeonMasterXIV.Tests/AClearedNameStaysClearedTests.cs` (new file, complete):

```csharp
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>Clearing a campaign's name box is a choice: the old carried-over name is not offered again.</summary>
public sealed class AClearedNameStaysClearedTests
{
    [Fact]
    public void ACampaignNameClearedByThePlayerShowsTheCharacterNameNotTheOldDefault()
    {
        var campaign = new Campaign();
        DisplayName.TryParse("Svafnir Asoltun", out var characterName);

        Assert.Equal("Old Name", CampaignDisplayName.ToPreFill(campaign, "Old Name", characterName));

        Assert.True(CampaignDisplayName.RecordChosen(campaign, string.Empty, characterName));

        Assert.Equal("Svafnir Asoltun", CampaignDisplayName.ToPreFill(campaign, "Old Name", characterName));
    }
}
```

- [ ] **Step 2: Watch them fail**

Run: `dotnet build tests/DungeonMasterXIV.Tests 2>&1 | grep -E 'error' | sort -u`

Expected: `error CS1061: 'HostingCampaign' does not contain a definition for 'Follow'`.

- [ ] **Step 3: Add `Follow`**

`src/DungeonMasterXIV.Core/Campaigns/HostingCampaign.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Campaigns/HostingCampaign.cs b/src/DungeonMasterXIV.Core/Campaigns/HostingCampaign.cs
index c97d6b6..76d96f0 100644
--- a/src/DungeonMasterXIV.Core/Campaigns/HostingCampaign.cs
+++ b/src/DungeonMasterXIV.Core/Campaigns/HostingCampaign.cs
@@ -29,6 +29,15 @@ public sealed class HostingCampaign
 
     public void Ended() => Current = null;
 
+    /// <summary>Clears the current campaign whenever hosting is not running, including after a failed start.</summary>
+    public void Follow(bool hosting)
+    {
+        if (!hosting)
+        {
+            Current = null;
+        }
+    }
+
     public bool LetsReturningPlayersIn => Current?.LetReturningPlayersIn == true;
 
     public void SetReturningPlayers(bool letIn)
```

- [ ] **Step 4: Watch the name test fail on behaviour**

Run: `dotnet test tests/DungeonMasterXIV.Tests 2>&1 | grep -E 'Passed!|Failed!|Failed |Assert'`

Expected: `AFailedStartClosesItsCampaignTests` passes, and `AClearedNameStaysClearedTests` fails with `Assert.True() Failure`. That is the bug: clearing the box records nothing, so the old name refills it.

- [ ] **Step 5: Store a cleared name as a choice**

`src/DungeonMasterXIV.Core/Campaigns/CampaignDisplayName.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Campaigns/CampaignDisplayName.cs b/src/DungeonMasterXIV.Core/Campaigns/CampaignDisplayName.cs
index 5f6ba75..198f4d9 100644
--- a/src/DungeonMasterXIV.Core/Campaigns/CampaignDisplayName.cs
+++ b/src/DungeonMasterXIV.Core/Campaigns/CampaignDisplayName.cs
@@ -4,6 +4,8 @@ using System.Text;
 namespace DungeonMasterXIV.Campaigns;
 
 /// <summary>Reads and records a campaign's display-name alias, else a carried-over default or the character name.</summary>
+/// <remarks>A null alias means no name was ever chosen for the campaign; an empty one means the player chose
+/// their character name, so the carried-over default is not offered again (rolls R-2.17).</remarks>
 public static class CampaignDisplayName
 {
     public static string Stored(Campaign? campaign) => campaign?.DisplayNameAlias ?? string.Empty;
@@ -17,12 +19,12 @@ public static class CampaignDisplayName
 
         var trimmed = string.IsNullOrWhiteSpace(alias) ? string.Empty : alias.Trim();
 
-        if (string.Equals(Stored(campaign), trimmed, StringComparison.Ordinal))
+        if (string.Equals(campaign.DisplayNameAlias, trimmed, StringComparison.Ordinal))
         {
             return false;
         }
 
-        campaign.DisplayNameAlias = trimmed.Length > 0 ? trimmed : null;
+        campaign.DisplayNameAlias = trimmed;
         return true;
     }
 
@@ -34,11 +36,13 @@ public static class CampaignDisplayName
 
     public static string ToEdit(
         Campaign? campaign, string? carriedOverDefault, Net.DisplayName characterName) =>
-        Stored(campaign) is { Length: > 0 } stored
-            ? stored
-            : carriedOverDefault is { Length: > 0 } carried
-                ? carried
-                : characterName.Value;
+        campaign?.DisplayNameAlias switch
+        {
+            { Length: > 0 } stored => stored,
+            not null => characterName.Value,
+            null when carriedOverDefault is { Length: > 0 } carried => carried,
+            null => characterName.Value,
+        };
 
     public static string ToPreFill(
         Campaign? campaign, string? carriedOverDefault, Net.DisplayName characterName) =>
```

- [ ] **Step 6: Run the suite**

Run: `dotnet build DungeonMasterXIV.sln -warnaserror 2>&1 | grep -E '^ *[0-9]+ (Warning|Error)' ; dotnet test DungeonMasterXIV.sln --no-build 2>&1 | grep -E 'Passed!|Failed!'`

Expected: 0 warnings, 0 errors. Release.Tests 1, Tests 8, Relay.Tests 7, all passed, 0 skipped.

- [ ] **Step 7: Commit**

```bash
git add src/DungeonMasterXIV.Core/Campaigns tests/DungeonMasterXIV.Tests
git commit -F - <<'EOF'
fix(campaigns): a failed start closes its campaign; a cleared name stays cleared

HostingCampaign.Follow clears the current campaign whenever hosting is not running, so a failed
start no longer leaves settings acting as if a campaign were open. A name the player clears is now
stored as an explicit choice of their character name, so the carried-over default (rolls R-2.17)
is not offered again and the box no longer snaps back.

Co-Authored-By: <model> <noreply@anthropic.com>
EOF
```

---

### Task 2: Component changes: heading sizes, rail badge, banner action, empty send

**Files:**
- Modify: `Windows/Ui/UiFonts.cs`, `Windows/Ui/Components/RailButton.cs`, `Windows/Ui/Components/Banner.cs`, `Windows/MessageComposeView.cs`

**Interfaces:**
- Produces:
  - `RailButton.Draw(UiFonts, FontAwesomeIcon, string tooltip, bool lit, int badge = 0)`, where a badge above 0 draws a gold count, shown as `9+` above 9;
  - `Banner.Draw(UiFonts, BannerKind, string text, string? action)`, which returns true the frame its button is pressed. The three-argument overload is unchanged;
  - type sizes Title 18, Code 24, Total 28;
  - an empty or whitespace-only Submit, which does nothing and clears any shown refusal.

- [ ] **Step 1: Apply the four diffs**

`Windows/Ui/UiFonts.cs` (diff):

```diff
diff --git a/Windows/Ui/UiFonts.cs b/Windows/Ui/UiFonts.cs
index 055e8bf..73def18 100644
--- a/Windows/Ui/UiFonts.cs
+++ b/Windows/Ui/UiFonts.cs
@@ -14,9 +14,9 @@ internal sealed class UiFonts : IDisposable
     {
         Body = ui.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis14));
         Meta = ui.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis12));
-        Title = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Regular.ttf"), 15f, log);
-        Code = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Regular.ttf"), 20f, log);
-        Total = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Bold.ttf"), 24f, log);
+        Title = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Regular.ttf"), 18f, log);
+        Code = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Regular.ttf"), 24f, log);
+        Total = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Bold.ttf"), 28f, log);
         Voice = Bundled(ui, Path.Combine(fontDirectory, "Spectral-Regular.ttf"), 16f, log);
         Icon = ui.IconFontHandle;
     }
```

`Windows/Ui/Components/RailButton.cs` (diff):

```diff
diff --git a/Windows/Ui/Components/RailButton.cs b/Windows/Ui/Components/RailButton.cs
index d1fee74..0eb6e32 100644
--- a/Windows/Ui/Components/RailButton.cs
+++ b/Windows/Ui/Components/RailButton.cs
@@ -1,3 +1,4 @@
+using System;
 using System.Numerics;
 using Dalamud.Bindings.ImGui;
 using Dalamud.Interface;
@@ -6,12 +7,12 @@ using Dalamud.Interface.Utility.Raii;
 
 namespace DungeonMasterXIV.Windows.Ui.Components;
 
-/// <summary>A square icon button with a tooltip, lit with a gold edge while its window is open.</summary>
+/// <summary>A square icon button with a tooltip, lit with a gold edge while its tab is shown, with an optional count badge.</summary>
 internal static class RailButton
 {
     public static float Size => 34f * ImGuiHelpers.GlobalScale;
 
-    public static bool Draw(UiFonts fonts, FontAwesomeIcon icon, string tooltip, bool lit)
+    public static bool Draw(UiFonts fonts, FontAwesomeIcon icon, string tooltip, bool lit, int badge = 0)
     {
         bool pressed;
         using (ImRaii.PushColor(ImGuiCol.Button, lit ? Palette.SurfaceRaised : Palette.Surface)
@@ -27,6 +28,25 @@ internal static class RailButton
             ImGui.SetTooltip(tooltip);
         }
 
+        if (badge > 0)
+        {
+            DrawBadge(fonts, badge);
+        }
+
         return pressed;
     }
+
+    /// <summary>A gold count on the button's top-right corner; the count is the signal, not only the colour.</summary>
+    private static void DrawBadge(UiFonts fonts, int count)
+    {
+        using var font = fonts.Meta.Push();
+        var text = count > 9 ? "9+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
+        var textSize = ImGui.CalcTextSize(text);
+        var radius = MathF.Max(textSize.X, textSize.Y) / 2f + 2f;
+        var corner = new Vector2(ImGui.GetItemRectMax().X - radius / 2f, ImGui.GetItemRectMin().Y + radius / 2f);
+        var drawList = ImGui.GetWindowDrawList();
+
+        drawList.AddCircleFilled(corner, radius, ImGui.GetColorU32(Palette.Gold));
+        drawList.AddText(corner - (textSize / 2f), ImGui.GetColorU32(Palette.Surface), text);
+    }
 }
```

`Windows/Ui/Components/Banner.cs` (diff):

```diff
diff --git a/Windows/Ui/Components/Banner.cs b/Windows/Ui/Components/Banner.cs
index 831afa8..b398401 100644
--- a/Windows/Ui/Components/Banner.cs
+++ b/Windows/Ui/Components/Banner.cs
@@ -15,7 +15,10 @@ internal enum BannerKind
 /// <summary>A status note with a coloured left edge and an icon, so its kind never rests on colour alone.</summary>
 internal static class Banner
 {
-    public static void Draw(UiFonts fonts, BannerKind kind, string text)
+    public static void Draw(UiFonts fonts, BannerKind kind, string text) => Draw(fonts, kind, text, action: null);
+
+    /// <summary>A banner with one button after its text; returns true the frame the button is pressed.</summary>
+    public static bool Draw(UiFonts fonts, BannerKind kind, string text, string? action)
     {
         var (colour, icon) = kind switch
         {
@@ -32,6 +35,8 @@ internal static class Banner
 
         ImGui.SameLine();
         ImGui.TextWrapped(text);
+
+        return action is not null && ActionRow.Secondary(action);
     }
 
     /// <summary>A one-line refusal under an input, in the danger colour.</summary>
```

`Windows/MessageComposeView.cs` (diff):

```diff
diff --git a/Windows/MessageComposeView.cs b/Windows/MessageComposeView.cs
index 03805ba..8ebdf84 100644
--- a/Windows/MessageComposeView.cs
+++ b/Windows/MessageComposeView.cs
@@ -85,6 +85,12 @@ internal sealed class MessageComposeView
 
     internal void Submit()
     {
+        if (string.IsNullOrWhiteSpace(_entry))
+        {
+            _refusal = null;
+            return;
+        }
+
         if (RollCommand.TryRead(_entry, out var expression))
         {
             Roll(expression);
```

- [ ] **Step 2: Build and test**

Run: `dotnet build DungeonMasterXIV.sln -warnaserror 2>&1 | grep -E '^ *[0-9]+ (Warning|Error)' ; dotnet test DungeonMasterXIV.sln --no-build 2>&1 | grep -E 'Passed!|Failed!'`

Expected: 0 warnings, 0 errors, and 1 / 8 / 7 passed with 0 skipped. Nothing calls the new parameters yet; Task 3 does.

- [ ] **Step 3: Commit**

```bash
git add Windows/Ui Windows/MessageComposeView.cs
git commit -F - <<'EOF'
feat(ui): headings no shorter than body text, a rail badge, a banner action, a quiet empty send

Co-Authored-By: <model> <noreply@anthropic.com>
EOF
```

---

### Task 3: The panel and its tabs

**Files:**
- Delete: `Windows/RailWindow.cs`, `Windows/SessionWindow.cs`, `Windows/ConfigWindow.cs`.
- Create, each given complete:
  - `Windows/PanelWindow.cs`;
  - `Windows/ChatTab.cs`;
  - `Windows/SessionTab.cs`, today's `SessionWindow` without the window, stream and composer;
  - `Windows/SettingsTab.cs`, today's `ConfigWindow` without the window.
- Modify: `src/DungeonMasterXIV.Core/Data/PluginSettings.cs`, `Windows/JoinFlowView.cs`, `Plugin.cs`.

**Interfaces:**
- Consumes: Task 1's `HostingCampaign.Follow`, and Task 2's `RailButton.Draw(…, badge)` and `Banner.Draw(…, action)`.
- Produces:
  - `enum PanelTab { Chat, Session, Settings }`, plus `PluginSettings.SelectedTab` (default Session), `PanelCollapsed`, `RecordSelectedTab` and `RecordPanelCollapsed`. `SettingsWindowOpen`, `RailCollapsed` and `RailIntroduced` are removed;
  - `PanelWindow(ConfigurationStore, SessionCoordinator, UiFonts, IPluginLog)`, with `Attach(ChatTab, SessionTab, SettingsTab)`, `Show(PanelTab)` and `Select(PanelTab)`;
  - `ChatTab(SessionCoordinator, UiFonts, Func<DisplayName>, JoinFlowView, Action showSession)`;
  - `SessionTab(SessionCoordinator, UiFonts, HostingCampaign, JoinFlowView)`;
  - `SettingsTab(ConfigurationStore, Func<DisplayName>, Func<Campaign?>, Action<Campaign>, CampaignStorageView, UiFonts)`.

Behaviour to keep, and where it moves:
- **The reconnecting banner** moves from `SessionWindow.DrawHosting` and `JoinFlowView.DrawStatus` to the top of `ChatTab`.
- **The keep-or-lose card** stays at the end of the stream, through `JoinFlowView.DrawOffer`.
- **Everything else in the old windows** is unchanged in wording and order inside its tab.

- [ ] **Step 1: Delete the old windows**

```bash
git rm Windows/RailWindow.cs Windows/SessionWindow.cs Windows/ConfigWindow.cs
```

- [ ] **Step 2: The panel's settings**

`src/DungeonMasterXIV.Core/Data/PluginSettings.cs` (diff):

```diff
diff --git a/src/DungeonMasterXIV.Core/Data/PluginSettings.cs b/src/DungeonMasterXIV.Core/Data/PluginSettings.cs
index b785348..d39ba42 100644
--- a/src/DungeonMasterXIV.Core/Data/PluginSettings.cs
+++ b/src/DungeonMasterXIV.Core/Data/PluginSettings.cs
@@ -2,21 +2,28 @@ using System;
 
 namespace DungeonMasterXIV.Data;
 
-/// <summary>The plugin's saved settings: window state, relay address, interruption window, relink memory, alias.</summary>
+/// <summary>The tabs the plugin's panel switches between.</summary>
+public enum PanelTab
+{
+    Chat = 0,
+
+    Session = 1,
+
+    Settings = 2,
+}
+
+/// <summary>The plugin's saved settings: panel state, relay address, interruption window, relink memory, alias.</summary>
 public sealed class PluginSettings
 {
     public const int CurrentSchemaVersion = 1;
 
     public bool MainWindowOpen { get; set; }
 
-    public bool SettingsWindowOpen { get; set; }
-
     public bool RestoreWindowState { get; set; } = true;
 
-    public bool RailCollapsed { get; set; }
+    public bool PanelCollapsed { get; set; }
 
-    /// <summary>False until the rail has opened once and put the session window beside it.</summary>
-    public bool RailIntroduced { get; set; }
+    public PanelTab SelectedTab { get; set; } = PanelTab.Session;
 
     public static bool RequiresWriteOnLoad(int? versionOnDisk) => versionOnDisk is null;
 
@@ -57,25 +64,25 @@ public sealed class PluginSettings
         return true;
     }
 
-    public bool RecordRailCollapsed(bool collapsed)
+    public bool RecordPanelCollapsed(bool collapsed)
     {
-        if (RailCollapsed == collapsed)
+        if (PanelCollapsed == collapsed)
         {
             return false;
         }
 
-        RailCollapsed = collapsed;
+        PanelCollapsed = collapsed;
         return true;
     }
 
-    public bool RecordSettingsWindowOpen(bool isOpen)
+    public bool RecordSelectedTab(PanelTab tab)
     {
-        if (SettingsWindowOpen == isOpen)
+        if (SelectedTab == tab)
         {
             return false;
         }
 
-        SettingsWindowOpen = isOpen;
+        SelectedTab = tab;
         return true;
     }
 }
```

- [ ] **Step 3: The three tabs**

`Windows/SettingsTab.cs` (new file, complete):

```csharp
using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The panel's Settings tab: display name, window restore, relay address, the policy link, and campaign and user storage.</summary>
internal sealed class SettingsTab
{
    private readonly UiFonts _fonts;
    private readonly ConfigurationStore _configurationStore;
    private readonly Func<DisplayName> _characterName;

    private readonly Func<Campaign?> _currentCampaign;
    private readonly Action<Campaign> _saveCampaign;

    private readonly string _schemaVersionLabel;
    private readonly RelinkMemoryView _relinkMemory;
    private readonly CampaignStorageView _campaignStorage;

    private static readonly string UnusableAliasWarning =
        $"This name cannot be used, so your character name will be sent instead. Names are limited "
        + $"to {DisplayName.MaxLength} characters and cannot contain line breaks or invisible "
        + "formatting characters.";

    private const string RelayPolicyUrl =
        "https://github.com/ruminabottle/dungeonmasterxiv/blob/main/RELAY-SERVICE-POLICY.md";

    private const string NameFieldIsFull =
        "This box is full and will not take any more. If you were still typing, the rest did not go "
        + "in - use a shorter name.";

    private const string NameNeedsACampaign =
        "A name is saved with a campaign, and no campaign is open. Until you open or create one, "
        + "you will join as your character name and this box cannot be changed.";

    private const string InvalidRelayWarning =
        "This is not a usable relay address. It must start with wss:// - or ws:// for a relay "
        + "running on this machine.";

    public SettingsTab(
        ConfigurationStore configurationStore,
        Func<DisplayName> characterName,
        Func<Campaign?> currentCampaign,
        Action<Campaign> saveCampaign,
        CampaignStorageView campaignStorage,
        UiFonts fonts)
    {
        _fonts = fonts;
        _configurationStore = configurationStore;
        _characterName = characterName;
        _currentCampaign = currentCampaign;
        _saveCampaign = saveCampaign;
        _campaignStorage = campaignStorage;

        _relinkMemory = new RelinkMemoryView(
            () => _configurationStore.Configuration.Settings.Relink,
            _configurationStore.Save);
        _schemaVersionLabel = $"Settings schema version {configurationStore.Configuration.Version}";
    }

    public void Draw()
    {
        var settings = _configurationStore.Configuration.Settings;

        Section.Heading(_fonts, "You");
        DrawDisplayNameSetting(settings);

        var restore = settings.RestoreWindowState;
        if (ImGui.Checkbox("Reopen windows where I left them", ref restore))
        {
            settings.RestoreWindowState = restore;
            _configurationStore.Save();
        }

        Section.Heading(_fonts, "Connection");
        DrawRelaySetting(settings);

        if (ActionRow.Secondary("Relay and privacy"))
        {
            Dalamud.Utility.Util.OpenLink(RelayPolicyUrl);
        }

        Section.Heading(_fonts, "Campaigns");
        _campaignStorage.Draw();

        Section.Heading(_fonts, "Stored data");
        _relinkMemory.Draw();

        ImGui.Spacing();
        using (_fonts.Meta.Push())
        {
            ImGui.TextColored(Palette.TextMuted, _schemaVersionLabel);
        }
    }

    private void DrawDisplayNameSetting(PluginSettings settings)
    {
        var characterName = _characterName();

        var campaign = _currentCampaign();
        var typed = DrawNameBox(campaign, settings.DisplayNameAlias, characterName);

        if (NameInputCapacity.IsFull(typed))
        {
            Banner.Draw(_fonts, BannerKind.Warning, NameFieldIsFull);
        }

        var effective = CampaignDisplayName.Or(campaign, characterName);
        ImGui.TextUnformatted($"You will join as: {effective.Value}");

        var stored = CampaignDisplayName.Stored(campaign);
        if (stored.Length > 0 && !DisplayName.TryParse(stored, out _))
        {
            Banner.Draw(_fonts, BannerKind.Warning, UnusableAliasWarning);
        }
    }

    private string DrawNameBox(Campaign? campaign, string? carriedOverDefault, DisplayName characterName)
    {
        var noCampaign = campaign is null;

        var typed = CampaignDisplayName.ToPreFill(campaign, carriedOverDefault, characterName);

        if (noCampaign)
        {
            ImGui.BeginDisabled();
        }

        var edited = ImGui.InputText("Name others see", ref typed, DisplayName.MaxUtf8Bytes);

        if (noCampaign)
        {
            ImGui.EndDisabled();
            Banner.Draw(_fonts, BannerKind.Info, NameNeedsACampaign);
        }

        if (edited)
        {
            if (campaign is not null && CampaignDisplayName.RecordChosen(campaign, typed, characterName))
            {
                _saveCampaign(campaign);
            }
        }

        return typed;
    }

    private void DrawRelaySetting(PluginSettings settings)
    {
        var address = settings.RelayAddress;
        if (ImGui.InputText("Relay address", ref address, 256))
        {
            settings.RelayAddress = address;
            _configurationStore.Save();
        }

        if (!RelayEndpoint.TryParse(settings.RelayAddress, out _))
        {
            Banner.Draw(_fonts, BannerKind.Danger, InvalidRelayWarning);
        }
    }
}
```

`Windows/SessionTab.cs` (new file, complete):

```csharp
using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The panel's Session tab: how to host or join, the code, who is at the table, and join requests.</summary>
internal sealed class SessionTab
{
    private const string CodeChangedWarning =
        "Your session code changed while you were disconnected, because it was taken by another "
        + "session. Your players are still holding the old one - read them the new code below.";

    private readonly SessionCoordinator _coordinator;

    private readonly UiFonts _fonts;

    private readonly HostingCampaign _hosting;

    private readonly HostCampaignPicker _campaignPicker;

    private readonly AdmissionPromptView _admissionPrompts;

    private readonly JoinFlowView _joinFlow;

    private readonly DangerAction _endSession = new();

    /// <summary>Below this width the Host and Join paths stack instead of sitting side by side.</summary>
    private const float SideBySideWidth = 560f;

    public SessionTab(SessionCoordinator coordinator, UiFonts fonts, HostingCampaign hosting, JoinFlowView joinFlow)
    {
        _coordinator = coordinator;
        _fonts = fonts;
        _hosting = hosting;
        _joinFlow = joinFlow;
        _admissionPrompts = new AdmissionPromptView(coordinator, fonts);
        _campaignPicker = new HostCampaignPicker(hosting);
    }

    public void Draw()
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
    }

    private void DrawNotInASession()
    {
        if (_joinFlow.OfferIsOpen)
        {
            return;
        }

        if (_coordinator.Host.Failure != SessionFailure.None)
        {
            Banner.Draw(_fonts, BannerKind.Danger, SessionFailureMessage.For(_coordinator.Host.Failure));
        }

        EmptyState.Draw(_fonts, "No session yet", "Start one as the DM, or join one with the code your DM gives you.");

        var sideBySide = ImGui.GetContentRegionAvail().X >= SideBySideWidth * ImGuiHelpers.GlobalScale;
        if (sideBySide)
        {
            DrawPathsSideBySide();
        }
        else
        {
            DrawHostPath();
            DrawJoinPath();
        }

        _joinFlow.DrawProblems();
    }

    private void DrawPathsSideBySide()
    {
        using var table = ImRaii.Table("##paths", 2, ImGuiTableFlags.SizingStretchSame);
        if (!table.Success)
        {
            DrawHostPath();
            DrawJoinPath();
            return;
        }

        ImGui.TableNextColumn();
        DrawHostPath();
        ImGui.TableNextColumn();
        DrawJoinPath();
    }

    private void DrawHostPath()
    {
        Section.Heading(_fonts, "Host");
        _campaignPicker.Draw();
        if (ActionRow.Primary("Start session"))
        {
            _hosting.StartFor();
            _coordinator.StartHosting();
        }
    }

    private void DrawJoinPath()
    {
        Section.Heading(_fonts, "Join");
        _joinFlow.DrawForm();
    }

    private void DrawHosting()
    {
        var host = _coordinator.Host;

        if (host.Phase == HostingPhase.Registering)
        {
            Banner.Draw(_fonts, BannerKind.Info, "Hosting: registering with the relay");
            return;
        }

        if (host.CodeChangedMidSession)
        {
            Banner.Draw(_fonts, BannerKind.Warning, CodeChangedWarning);
            if (ActionRow.Secondary("I have told them"))
            {
                host.AcknowledgeCodeChange();
            }
        }

        if (host.Code is { } code)
        {
            CodeDisplay.Draw(_fonts, code);
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
        if (!Section.Collapsible(_fonts, $"At the table · {roster.Count}###table"))
        {
            return;
        }

        foreach (var entry in roster)
        {
            var away = host
                && PeerCode.TryParse(entry.PeerCode, out var peer)
                && _coordinator.Drops.WhenDropped(peer) is not null;
            RosterRow.Draw(_fonts, new SpeakerName(DisplayName.OrNone(entry.DisplayName).Value, entry.Role), away);
        }

        if (!host)
        {
            return;
        }

        using (_fonts.Meta.Push())
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

`Windows/ChatTab.cs` (new file, complete):

```csharp
using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The panel's Chat tab: the stream and the message box, with the notices a person mid-conversation needs.</summary>
internal sealed class ChatTab
{
    private readonly SessionCoordinator _coordinator;

    private readonly UiFonts _fonts;

    private readonly Func<DisplayName> _displayName;

    private readonly JoinFlowView _joinFlow;

    private readonly Action _showSession;

    private readonly MessageComposeView _compose;

    private readonly StreamView _stream;

    public ChatTab(
        SessionCoordinator coordinator,
        UiFonts fonts,
        Func<DisplayName> displayName,
        JoinFlowView joinFlow,
        Action showSession)
    {
        _coordinator = coordinator;
        _fonts = fonts;
        _displayName = displayName;
        _joinFlow = joinFlow;
        _showSession = showSession;
        _compose = new MessageComposeView(coordinator);
        _stream = new StreamView(coordinator, fonts);
    }

    public void Draw()
    {
        if (_coordinator.ReconnectingLine is { } reconnecting)
        {
            Banner.Draw(_fonts, BannerKind.Warning, reconnecting);
        }

        DrawJoinRequestNotice();

        var you = new SpeakerName(
            _displayName().Value, _coordinator.InAHostedSession ? SessionRole.DungeonMaster : SessionRole.Player);
        var streamHeight = ImGui.GetContentRegionAvail().Y - _compose.Height - ImGui.GetStyle().ItemSpacing.Y;
        _stream.Draw(Math.Max(streamHeight, ImGui.GetFrameHeight()), you, _compose.LocalRolls, _joinFlow.DrawOffer);
        _compose.Draw();
    }

    /// <summary>Tells the host someone is waiting. Drawn locally and never recorded or sent (rolls R-2.3).</summary>
    private void DrawJoinRequestNotice()
    {
        var pending = _coordinator.Admissions.Pending;
        if (!_coordinator.InAHostedSession || pending.Count == 0)
        {
            return;
        }

        var text = pending.Count == 1
            ? $"{pending[0].DisplayName.Value} is asking to join"
            : $"{pending.Count} people are asking to join";

        if (Banner.Draw(_fonts, BannerKind.Info, text, "Review"))
        {
            _showSession();
        }
    }
}
```

`Windows/JoinFlowView.cs` (diff):

```diff
diff --git a/Windows/JoinFlowView.cs b/Windows/JoinFlowView.cs
index 4b871a7..e36697a 100644
--- a/Windows/JoinFlowView.cs
+++ b/Windows/JoinFlowView.cs
@@ -44,11 +44,6 @@ internal sealed class JoinFlowView
             ImGui.TextColored(Palette.TextMuted, $"Joining: {DescribeJoin(join.Phase)}");
         }
 
-        if (_coordinator.ReconnectingLine is { } reconnecting)
-        {
-            Banner.Draw(_fonts, BannerKind.Warning, reconnecting);
-        }
-
         if (join.Phase == JoinPhase.AwaitingDecision)
         {
             Banner.Draw(_fonts, BannerKind.Info, $"The DM has {join.RemainingAt(DateTimeOffset.UtcNow):mm\\:ss} left to answer");
```

- [ ] **Step 4: The panel**

`Windows/PanelWindow.cs` (new file, complete):

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The plugin's one window: a rail on its left edge whose buttons switch the tab shown beside it.</summary>
internal sealed class PanelWindow : ThemedWindow
{
    private static readonly Vector2 MinimumSize = new(480, 360);

    private static readonly Vector2 FirstSize = new(640, 560);

    private readonly ConfigurationStore _configurationStore;
    private readonly SessionCoordinator _coordinator;
    private ChatTab? _chat;
    private SessionTab? _session;
    private SettingsTab? _settings;

    private Vector2 _expandedSize = FirstSize;
    private bool _restoreSize;

    public PanelWindow(ConfigurationStore configurationStore, SessionCoordinator coordinator, UiFonts fonts, IPluginLog log)
        : base("Dungeon Master XIV###dmx-main", fonts, log)
    {
        _configurationStore = configurationStore;
        _coordinator = coordinator;
        RespectCloseHotkey = false;

        IsOpen = Settings.ShouldOpenOnLoad(Settings.MainWindowOpen);
    }

    private PluginSettings Settings => _configurationStore.Configuration.Settings;

    /// <summary>Supplies the tabs once they exist; the Chat tab needs this window to switch to Session.</summary>
    public void Attach(ChatTab chat, SessionTab session, SettingsTab settings)
    {
        _chat = chat;
        _session = session;
        _settings = settings;
    }

    /// <summary>Opens the panel, expanded, on one tab.</summary>
    public void Show(PanelTab tab)
    {
        IsOpen = true;
        Select(tab);
        SetCollapsed(false);
    }

    public void Select(PanelTab tab)
    {
        if (Settings.RecordSelectedTab(tab))
        {
            _configurationStore.Save();
        }
    }

    public override void PreDraw()
    {
        var baseFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse;
        if (Settings.PanelCollapsed)
        {
            Flags = baseFlags | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize;
            SizeConstraints = null;
            Size = null;
        }
        else
        {
            Flags = baseFlags;
            SizeConstraints = new WindowSizeConstraints { MinimumSize = MinimumSize, MaximumSize = new Vector2(float.MaxValue) };
            Size = _restoreSize ? _expandedSize : null;
            SizeCondition = ImGuiCond.Always;
            _restoreSize = false;
        }

        base.PreDraw();
    }

    public override void OnOpen() => Remember(true);

    public override void OnClose() => Remember(false);

    protected override void DrawContent()
    {
        if (Settings.PanelCollapsed)
        {
            DrawRail(collapsed: true);
            return;
        }

        _expandedSize = ImGui.GetWindowSize() / ImGuiHelpers.GlobalScale;

        var railWidth = RailButton.Size + (ImGui.GetStyle().WindowPadding.X * 2f);
        using (var rail = ImRaii.Child("##rail", new Vector2(railWidth, 0f), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (rail.Success)
            {
                DrawRail(collapsed: false);
            }
        }

        ImGui.SameLine();
        using var tab = ImRaii.Child("##tab", Vector2.Zero, false);
        if (!tab.Success)
        {
            return;
        }

        switch (Settings.SelectedTab)
        {
            case PanelTab.Chat:
                _chat?.Draw();
                break;
            case PanelTab.Settings:
                _settings?.Draw();
                break;
            default:
                _session?.Draw();
                break;
        }
    }

    private void DrawRail(bool collapsed)
    {
        var selected = collapsed ? (PanelTab?)null : Settings.SelectedTab;

        if (RailButton.Draw(Fonts, FontAwesomeIcon.Comments, "Chat", selected == PanelTab.Chat))
        {
            Show(PanelTab.Chat);
        }

        var waiting = _coordinator.InAHostedSession ? _coordinator.Admissions.Pending.Count : 0;
        if (RailButton.Draw(Fonts, FontAwesomeIcon.Users, "Session", selected == PanelTab.Session, waiting))
        {
            Show(PanelTab.Session);
        }

        if (!collapsed)
        {
            var bottom = (RailButton.Size * 2f) + ImGui.GetStyle().ItemSpacing.Y;
            var space = ImGui.GetContentRegionAvail().Y - bottom;
            if (space > 0f)
            {
                ImGui.Dummy(new Vector2(0f, space));
            }
        }

        if (RailButton.Draw(Fonts, FontAwesomeIcon.Cog, "Settings", selected == PanelTab.Settings))
        {
            Show(PanelTab.Settings);
        }

        if (collapsed)
        {
            if (RailButton.Draw(Fonts, FontAwesomeIcon.ChevronRight, "Expand", lit: false))
            {
                SetCollapsed(false);
            }
        }
        else if (RailButton.Draw(Fonts, FontAwesomeIcon.ChevronLeft, "Collapse to the rail", lit: false))
        {
            SetCollapsed(true);
        }
    }

    private void SetCollapsed(bool collapsed)
    {
        if (!Settings.RecordPanelCollapsed(collapsed))
        {
            return;
        }

        _configurationStore.Save();
        _restoreSize = !collapsed;
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

- [ ] **Step 5: Wire it**

`Plugin.cs` (diff):

```diff
diff --git a/Plugin.cs b/Plugin.cs
index 3443fee..811c30c 100644
--- a/Plugin.cs
+++ b/Plugin.cs
@@ -1,7 +1,6 @@
 using System;
 using System.IO;
 using Dalamud.Game.Command;
-using Dalamud.Interface;
 using Dalamud.Interface.Windowing;
 using Dalamud.Plugin;
 using Dalamud.Plugin.Services;
@@ -27,9 +26,7 @@ public sealed class Plugin : IDalamudPlugin
     private readonly CampaignStore _campaignStore;
     private readonly WindowSystem _windowSystem;
     private readonly UiFonts _fonts;
-    private readonly RailWindow _railWindow;
-    private readonly ConfigWindow _configWindow;
-    private readonly SessionWindow _sessionWindow;
+    private readonly PanelWindow _panel;
     private readonly WebSocketSessionTransport _relayTransport;
     private readonly SessionCoordinator _sessionCoordinator;
     private readonly HostingCampaign _hostingCampaign;
@@ -57,7 +54,6 @@ public sealed class Plugin : IDalamudPlugin
         var characterName = new LocalCharacterName(objects).Current;
 
         _hostingCampaign = new HostingCampaign(_campaignStore);
-        _configWindow = SettingsWindowFor(characterName, pluginInterface.ConfigDirectory, log);
         var sessionLog = new SessionTransportLog(log);
         _relayTransport = new WebSocketSessionTransport(sessionLog);
         _sessionCoordinator = new SessionCoordinator(
@@ -72,21 +68,18 @@ public sealed class Plugin : IDalamudPlugin
                     : null,
                 ResolveRelink: claimed => CampaignRelink.Resolve(_hostingCampaign.Current, claimed),
                 LetReturningPlayersIn: () => _hostingCampaign.LetsReturningPlayersIn));
-        _sessionWindow = new SessionWindow(
+        _panel = new PanelWindow(_configurationStore, _sessionCoordinator, _fonts, log);
+        var joinFlow = new JoinFlowView(
             _sessionCoordinator,
             _fonts,
-            log,
             NameWeSendAs(characterName),
-            _hostingCampaign,
-            () => _configurationStore.Configuration.Settings.Relink, SessionEndChoiceFor(pluginInterface.ConfigDirectory));
-        _railWindow = new RailWindow(
-            _configurationStore,
-            _fonts,
-            log,
-            [new RailEntry(_sessionWindow, FontAwesomeIcon.Comments, "Session")],
-            new RailEntry(_configWindow, FontAwesomeIcon.Cog, "Settings"),
-            _sessionWindow);
-        _commandDispatcher = new CommandDispatcher(_railWindow.Toggle, _configWindow.Open);
+            () => _configurationStore.Configuration.Settings.Relink,
+            SessionEndChoiceFor(pluginInterface.ConfigDirectory));
+        _panel.Attach(
+            new ChatTab(_sessionCoordinator, _fonts, NameWeSendAs(characterName), joinFlow, () => _panel.Select(PanelTab.Session)),
+            new SessionTab(_sessionCoordinator, _fonts, _hostingCampaign, joinFlow),
+            SettingsTabFor(characterName, pluginInterface.ConfigDirectory));
+        _commandDispatcher = new CommandDispatcher(_panel.Toggle, OpenSettingsTab);
 
         try
         {
@@ -104,19 +97,18 @@ public sealed class Plugin : IDalamudPlugin
     private Func<DisplayName> NameWeSendAs(Func<DisplayName> characterName) =>
         () => CampaignDisplayName.Or(_hostingCampaign.Current, characterName());
 
-    private ConfigWindow SettingsWindowFor(Func<DisplayName> characterName, DirectoryInfo configDirectory, IPluginLog log)
+    private SettingsTab SettingsTabFor(Func<DisplayName> characterName, DirectoryInfo configDirectory)
     {
         var retainedLogs = new RetainedLogStore(
             new RetainedLogFileArchive(Path.Combine(configDirectory.FullName, "logs")));
 
-        return new ConfigWindow(
+        return new SettingsTab(
             _configurationStore,
             characterName,
             () => _hostingCampaign.Current,
             _campaignStore.Save,
             new CampaignStorageView(_campaignStore, new CampaignDeletion(_campaignStore, retainedLogs)),
-            _fonts,
-            log);
+            _fonts);
     }
 
     private KeepOrLose SessionEndChoiceFor(DirectoryInfo configDirectory) =>
@@ -143,14 +135,8 @@ public sealed class Plugin : IDalamudPlugin
     {
         _unwind.Push("fonts", _fonts.Dispose);
 
-        _windowSystem.AddWindow(_railWindow);
-        _unwind.Push("rail window", () => _windowSystem.RemoveWindow(_railWindow));
-
-        _windowSystem.AddWindow(_configWindow);
-        _unwind.Push("settings window", () => _windowSystem.RemoveWindow(_configWindow));
-
-        _windowSystem.AddWindow(_sessionWindow);
-        _unwind.Push("session window", () => _windowSystem.RemoveWindow(_sessionWindow));
+        _windowSystem.AddWindow(_panel);
+        _unwind.Push("panel window", () => _windowSystem.RemoveWindow(_panel));
 
         _unwind.Push("session and relay connection", () =>
         {
@@ -160,18 +146,18 @@ public sealed class Plugin : IDalamudPlugin
 
         commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
         {
-            HelpMessage = "Toggle the Dungeon Master XIV buttons. \"/dmx settings\" opens settings.",
+            HelpMessage = "Toggle the Dungeon Master XIV panel. \"/dmx settings\" opens its Settings tab.",
         });
         _unwind.Push("/dmx command", () => commandManager.RemoveHandler(CommandName));
 
         pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
         _unwind.Push("draw handler", () => pluginInterface.UiBuilder.Draw -= _windowSystem.Draw);
 
-        pluginInterface.UiBuilder.OpenMainUi += _railWindow.Toggle;
-        _unwind.Push("main UI handler", () => pluginInterface.UiBuilder.OpenMainUi -= _railWindow.Toggle);
+        pluginInterface.UiBuilder.OpenMainUi += _panel.Toggle;
+        _unwind.Push("main UI handler", () => pluginInterface.UiBuilder.OpenMainUi -= _panel.Toggle);
 
-        pluginInterface.UiBuilder.OpenConfigUi += _configWindow.Toggle;
-        _unwind.Push("config UI handler", () => pluginInterface.UiBuilder.OpenConfigUi -= _configWindow.Toggle);
+        pluginInterface.UiBuilder.OpenConfigUi += OpenSettingsTab;
+        _unwind.Push("config UI handler", () => pluginInterface.UiBuilder.OpenConfigUi -= OpenSettingsTab);
 
         framework.Update += OnFrameworkUpdate;
         _unwind.Push("framework update handler", () => framework.Update -= OnFrameworkUpdate);
@@ -185,9 +171,12 @@ public sealed class Plugin : IDalamudPlugin
 
     private void OnCommand(string command, string arguments) => _commandDispatcher.Execute(arguments);
 
+    private void OpenSettingsTab() => _panel.Show(PanelTab.Settings);
+
     private void OnFrameworkUpdate(IFramework framework)
     {
         _sessionCoordinator.Tick(framework.UpdateDelta, DateTimeOffset.UtcNow);
+        _hostingCampaign.Follow(_sessionCoordinator.InAHostedSession);
         RememberWhoWeAre();
     }
 
```

- [ ] **Step 6: Build and test**

Run: `dotnet build DungeonMasterXIV.sln -warnaserror 2>&1 | grep -E '^ *[0-9]+ (Warning|Error)' ; dotnet test DungeonMasterXIV.sln --no-build 2>&1 | grep -E 'Passed!|Failed!'`

Expected: 0 warnings, 0 errors. Release.Tests 1, Tests 8, Relay.Tests 7, all passed, 0 skipped.

- [ ] **Step 7: Commit**

```bash
git add -A Windows Plugin.cs src/DungeonMasterXIV.Core/Data/PluginSettings.cs
git commit -F - <<'EOF'
feat(ui): one panel with the rail on its edge, switching Chat, Session and Settings

The rail is no longer a window of its own: its buttons switch the tab the panel shows. A waiting
join request shows as a count on the Session button and a notice above the chat (drawn locally,
never a stream entry). The selected tab and the collapsed state are remembered.

Co-Authored-By: <model> <noreply@anthropic.com>
EOF
```

---

### Task 4: In game (the person)

This needs FFXIV with Dalamud. Load the build as a dev plugin, or release it with a `v*` tag once it's merged, and check each item:

- [ ] **Commands:** `/dmx` toggles the panel, and `/dmx settings` opens it on Settings. Dalamud's plugin and settings buttons do the same.
- [ ] **The rail is attached:** Chat, Session and Settings switch the content beside it, the lit button matches the tab shown, and Settings sits at the bottom.
- [ ] **Collapse:** ◂ collapses the panel to the rail, and ▸ restores it at its previous size (Review Focus 1).
- [ ] **Remembered state:** close and reopen, and reload the plugin. The same tab and collapsed state come back, and no error appears from older settings (Review Focus 5).
- [ ] **Headings:** HOST, JOIN, NO SESSION YET and the section labels are no shorter than the text under them, at font scale 1.0 and 1.5.
- [ ] **Empty send:** pressing Send with an empty box shows nothing.
- [ ] **Name box:** with no session running, the Settings name box shows your character name greyed out, with the "a name is saved with a campaign" notice. While hosting, clearing it sticks, and "You will join as" shows your character name.
- [ ] **Join requests, with a second client:** while you're on Chat, the Session button shows `1` and the notice reads "<name> is asking to join". Review switches to Session, and after Admit both the badge and the notice are gone (Review Focus 2).
- [ ] **Teardown:** five disable/re-enable cycles leave exactly one panel, with nothing leaking into other plugins (A-0.6).
