using System;

namespace DungeonMasterXIV.Data;

/// <summary>
/// The settings themselves, as a plain serializable type with no Dalamud dependency.
/// <c>Configuration</c>, over in the plugin project, is the thin adapter that hands this to
/// Dalamud's config mechanism — it cannot be named in a cref from here, because this project
/// deliberately cannot see it. The skeleton stores window state and nothing else; the session,
/// campaign and character data described in the brief are not part of it.
/// </summary>
public sealed class PluginSettings
{
    /// <summary>
    /// The schema version written by this build. Bump it whenever the shape of this type changes
    /// in a way that settings already on a user's disk would not survive being read as-is.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Whether the main window was open when the plugin was last unloaded.</summary>
    public bool MainWindowOpen { get; set; }

    /// <summary>Whether the settings window was open when the plugin was last unloaded.</summary>
    public bool SettingsWindowOpen { get; set; }

    /// <summary>
    /// When true, windows reopen on load if they were open on unload. When false, the plugin
    /// always starts with everything closed.
    /// </summary>
    public bool RestoreWindowState { get; set; } = true;

    /// <summary>
    /// Whether loading should write the settings straight back out. True only when nothing
    /// readable came off disk, so a first run leaves a schema version behind without the user
    /// having to open anything, and a config already at the current version is not rewritten
    /// identically on every load. A version we do not recognise is left for a migration to deal
    /// with rather than stamped down to this one.
    /// </summary>
    /// <param name="versionOnDisk">The schema version that was loaded, or <c>null</c> if none was.</param>
    public static bool RequiresWriteOnLoad(int? versionOnDisk) => versionOnDisk is null;

    /// <summary>
    /// The relay this client dials. R-1.8 requires this to be swappable and discoverable — the
    /// default is a default, not a dependency. Validated by
    /// <c>RelayEndpoint.TryParse</c> before use rather than trusted from disk.
    /// </summary>
    public string RelayAddress { get; set; } = Net.RelayEndpoint.Default;

    /// <summary>
    /// How long a session survives an interruption (R-1.4, R-1.5a, A-1.23). <b>The one settable
    /// value both windows read</b>, so changing it moves the host-loss grace and the seat window
    /// together, without a protocol decision.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One value for two clocks, and they are not rival settings of one thing.</b> R-1.4's grace
    /// runs only while the host is <i>unreachable</i>; R-1.5a's seat clock runs only while the host
    /// is <i>reachable</i>. They never tick together (A-1.25), so there is no arithmetic between
    /// them to get wrong — which is exactly why one number can serve both without meaning two
    /// different things.
    /// </para>
    /// <para>
    /// <b>Settable, not knobbed.</b> A-1.23 requires the length be changeable without a protocol
    /// decision; it does not require a control. Whether the two should be unified for
    /// comprehensibility is an open Product Owner question, and a UI control here would settle it
    /// by implementation.
    /// </para>
    /// <para>
    /// <b>Five minutes is not arbitrary, and the reason belongs beside the number.</b> Two minutes
    /// was actively wrong: relaunching FFXIV takes minutes, so a two-minute host grace
    /// <i>guaranteed</i> that any DM crash ended the session (BUG-54). A reader who meets this
    /// value with no rationale will eventually shorten it and be able to argue for it.
    /// </para>
    /// <para>
    /// <b>No schema bump.</b> <see cref="CurrentSchemaVersion"/> moves when settings already on disk
    /// would not survive being read as-is. A key absent from an older file leaves this at its
    /// initializer, which is the pre-existing behaviour — the same argument as
    /// <see cref="RelayAddress"/>.
    /// </para>
    /// </remarks>
    public TimeSpan InterruptionWindow { get; set; } = Net.GraceWindow.Default;

    /// <summary>
    /// What this client remembers about which participant it is, per session code (R-1.5b). Empty
    /// until it has been admitted somewhere.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Carried by the settings because that is what already persists, and NOT because it is a
    /// setting.</b> Nothing here is a preference and none of it is shown in the settings window as
    /// one — see <see cref="RelinkMemory"/> for what it is and what the player may do with it.
    /// </para>
    /// <para>
    /// <b>No schema bump.</b> <see cref="CurrentSchemaVersion"/> moves when settings already on disk
    /// would not survive being read as-is; a key absent from an older file leaves this at its
    /// initializer, which is an empty memory — exactly what an older build had.
    /// </para>
    /// </remarks>
    public RelinkMemory Relink { get; set; } = new();

    /// <summary>
    /// A display name this client stored BEFORE names were campaign-scoped, kept as a local
    /// pre-fill default and nothing else (SQ-87, and A-2.31's single exception added by SQ-112).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE NAME OF THIS PROPERTY IS THE v0.1.5 ON-DISK KEY AND WAS NOT CHOSEN.</b> Dalamud
    /// persists settings with Newtonsoft, which matches a JSON member to a property by NAME. A
    /// v0.1.5 file carries <c>DisplayNameAlias</c>, so a property called anything else recovers
    /// nothing, and renaming the key needs a serializer attribute this project deliberately cannot
    /// reach — <c>DungeonMasterXIV.Core</c> takes no serializer dependency. <b>The name is
    /// historical. What it MEANS is this remark.</b>
    /// </para>
    /// <para>
    /// <b>WHY THE DEPENDENCY IS NOT ADDED. CORRECTED 2026-08-30, AND THE OLD REASON IS STRUCK
    /// RATHER THAN DELETED BECAUSE IT WAS CITED AS AUTHORITY.</b> This used to read: <i>"adding one
    /// so that a field could be spelt better would risk AN ASSEMBLY MISMATCH that fails in the game
    /// and that nothing in this repository can detect."</i> <b>That was false in both halves</b>,
    /// and DMXENG-117 quoted it to tell a ticket-taker an option was unavailable on evidence.
    /// Measured three times independently — feature-engineer-3, feature-engineer-1, the Deployment
    /// Manager — with <c>AssemblyName.GetAssemblyName</c>, the API that governs binding: Dalamud's
    /// shipped Newtonsoft and the 13.0.3 package are <b>AssemblyVersion 13.0.0.0, PublicKeyToken
    /// 30ad4fe6b2a6aeed, identical</b>. Only <c>FileVersion</c> differs (13.0.4.30916 against
    /// 13.0.3.27908), <b>and FileVersion does not govern binding</b> — the trap that produced two
    /// confident wrong answers before the right one.
    /// </para>
    /// <para>
    /// <b>THE REAL MECHANISM, MEASURED BY feature-engineer-3 RATHER THAN INFERRED.</b> Adding a
    /// <c>PackageReference</c> to <c>Core</c> makes the build deposit a <b>second physical
    /// <c>Newtonsoft.Json.dll</c></b> into the plugin output, which unmodified <c>main</c> does not:
    /// <c>DungeonMasterXIV.csproj</c> sets <c>CopyLocalLockFileAssemblies=true</c>, and the
    /// <c>Private=false</c> that keeps Dalamud's own copy out <b>does not extend to transitive
    /// package references</b>.
    /// </para>
    /// <para>
    /// <b>WHAT WAS MEASURED AND WHAT WAS NOT, KEPT APART.</b> That the second file is deposited
    /// <b>was measured here, by building</b>. <b>Which copy Dalamud's loader then prefers was NOT
    /// measured, and cannot be here — it needs FFXIV.</b> That question is recorded with its control
    /// in <c>.claude/team/IN-GAME-BACKLOG.md</c>, so it has a home rather than sitting in this
    /// remark as an indefinite hold.
    /// </para>
    /// <para>
    /// <b>A READER WHO GREPS THIS AND CONCLUDES THE CAMPAIGN-SCOPING WAS REVERTED IS READING IT
    /// EXACTLY AS THE PRD PREDICTED, AND IS WRONG.</b> A-2.31 forbids a display name persisting
    /// outside a campaign and now carries ONE exception: this value. <b>Exactly one, whose only
    /// permitted reader is the pre-fill path, and which never travels as itself</b> (A-2.32).
    /// <c>Campaigns.CampaignDisplayName.Or</c> — the send path — has no overload that can see it,
    /// and <b>that absence is the mechanism rather than a convention.</b>
    /// </para>
    /// <para>
    /// <b>NOTHING IN THE PRODUCT WRITES THIS.</b> It arrives only by deserialising a file an older
    /// build wrote, so a player who never ran v0.1.5 cannot acquire one at all — which is what
    /// keeps A-2.31's <i>"about what the product CAN do"</i> true of a build that carries it.
    /// Choosing a name writes <c>Campaigns.Campaign.DisplayNameAlias</c>, never this. The setter is
    /// public only because the deserialiser needs it.
    /// </para>
    /// <para>
    /// <b>No schema bump, and the reason is not the usual one.</b> <see cref="CurrentSchemaVersion"/>
    /// moves when settings already on disk would not survive being read as-is. This recovers a value
    /// by name out of JSON that is already there, so it needs no version comparison — and <b>a bump
    /// would not have helped anyway.</b> <see cref="RequiresWriteOnLoad"/> fires only when nothing
    /// readable loaded; a v0.1.5 file carries version 1 and this build still writes 1, so the
    /// existing hook cannot see this upgrade at all. Recovering by name is what makes the version
    /// irrelevant rather than merely unchanged.
    /// </para>
    /// </remarks>
    public string DisplayNameAlias { get; set; } = string.Empty;

    /// <summary>
    /// The window to actually use: <see cref="InterruptionWindow"/> when it is safe, otherwise
    /// R-1.4's default.
    /// </summary>
    /// <remarks>
    /// <b>Validated before use rather than trusted from disk</b>, exactly as
    /// <see cref="RelayAddress"/> is. The setter is public because the serialiser needs it, so a
    /// hand-edited or corrupted file can put any value in the property — including one short enough
    /// that an ordinary lull between rolls trips host-loss detection and ends a live session
    /// mid-play (<see cref="Net.TransportContract.IsKeepAliveSafeFor"/>).
    /// <para>
    /// <b>This one falls back rather than throwing, and that is the opposite of what
    /// <see cref="Net.GraceWindow"/>'s constructor does — deliberately.</b> A caller passing a bad
    /// value in code has made a mistake and should hear about it loudly. A bad value arriving from
    /// a config file is a user's typo, and throwing on it would stop the plugin loading over a
    /// number that has a perfectly good default. Same distinction as a wire value versus a
    /// programming error.
    /// </para>
    /// </remarks>
    public TimeSpan InterruptionWindowOrDefault() =>
        Net.TransportContract.IsKeepAliveSafeFor(InterruptionWindow)
            ? InterruptionWindow
            : Net.GraceWindow.Default;

    /// <summary>
    /// Records a new window, reporting whether it was accepted. <b>Refuses rather than clamps.</b>
    /// </summary>
    /// <remarks>
    /// Refusing is <see cref="Net.GraceWindow"/>'s own choice and this matches it: a silently
    /// shortened window produces sessions that end mid-play for no visible reason, so a caller must
    /// learn its value was rejected rather than discover later that it was quietly altered.
    /// </remarks>
    /// <param name="window">The requested length.</param>
    /// <returns>False if the value is unsafe or unchanged; true if it was stored.</returns>
    public bool RecordInterruptionWindow(TimeSpan window)
    {
        if (!Net.TransportContract.IsKeepAliveSafeFor(window) || InterruptionWindow == window)
        {
            return false;
        }

        InterruptionWindow = window;
        return true;
    }

    /// <summary>
    /// Whether a window that was open on unload should be reopened on load.
    /// </summary>
    /// <param name="wasOpen">The remembered open state of that window.</param>
    public bool ShouldOpenOnLoad(bool wasOpen) => RestoreWindowState && wasOpen;

    /// <summary>
    /// Records the main window's open state, reporting whether that changed anything. Callers
    /// save only when it returns true, so reopening the plugin does not rewrite an identical file.
    /// </summary>
    /// <param name="isOpen">The window's new open state.</param>
    public bool RecordMainWindowOpen(bool isOpen)
    {
        if (MainWindowOpen == isOpen)
        {
            return false;
        }

        MainWindowOpen = isOpen;
        return true;
    }

    /// <summary>
    /// Records the settings window's open state, reporting whether that changed anything.
    /// </summary>
    /// <param name="isOpen">The window's new open state.</param>
    public bool RecordSettingsWindowOpen(bool isOpen)
    {
        if (SettingsWindowOpen == isOpen)
        {
            return false;
        }

        SettingsWindowOpen = isOpen;
        return true;
    }
}
