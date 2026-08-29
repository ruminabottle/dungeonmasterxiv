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
    /// <see cref="DisplayNameAlias"/>.
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
    /// The name this player sends instead of their character name (R-1.3e). Empty means "use the
    /// character name", which is the default the requirement asks for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stored as the raw string and validated at the point of use</b>, exactly as
    /// <see cref="RelayAddress"/> is — <c>DisplayName.TryParse</c> decides, not this type. Repairing
    /// it here would make what is on disk disagree with what the user typed, and the validation
    /// rules belong with the thing that renders it next to a fingerprint.
    /// </para>
    /// <para>
    /// <b>Empty rather than the character name.</b> This project cannot see the game, so it has no
    /// character name to store; and storing one would freeze a value the game supplies afresh each
    /// session. Absence means "whatever I am called", which stays true when it changes.
    /// </para>
    /// <para>
    /// <b>No schema bump.</b> <see cref="CurrentSchemaVersion"/> is bumped when settings already on
    /// disk would not survive being read as-is; a string that defaults to empty is read from an
    /// older file as empty, which is exactly the pre-existing behaviour.
    /// </para>
    /// </remarks>
    public string DisplayNameAlias { get; set; } = string.Empty;

    /// <summary>
    /// Records a new alias, reporting whether that changed anything, so a caller does not rewrite an
    /// identical file on every keystroke that changes nothing.
    /// </summary>
    /// <param name="alias">What the user typed. Whitespace-only is stored as empty.</param>
    public bool RecordDisplayNameAlias(string? alias)
    {
        var trimmed = string.IsNullOrWhiteSpace(alias) ? string.Empty : alias.Trim();

        if (string.Equals(DisplayNameAlias, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        DisplayNameAlias = trimmed;
        return true;
    }

    /// <summary>
    /// The name this client will actually send: the alias if there is a usable one, otherwise
    /// <paramref name="characterName"/> (R-1.3e — "defaults to the character name and may be changed
    /// to an alias"). A-1.2g asserts this on what leaves the client, not on what settings shows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rule lives here rather than at the wiring point.</b> Putting it in the plugin's
    /// composition root would make it Dalamud-side and untestable, and it is a rule about what the
    /// product sends.
    /// </para>
    /// <para>
    /// <b>An unusable alias falls back rather than failing.</b> A name <c>DisplayName</c> refuses to
    /// render beside a fingerprint — control characters, overlong, bidi overrides — falls back to the
    /// character name, not to nothing. Sending nothing would show the DM "a player who gave no name"
    /// and make a typo look like deliberate anonymity. The settings window says the alias is
    /// unusable; the join does not silently become nameless because of it.
    /// </para>
    /// </remarks>
    /// <param name="characterName">What the game says this player is called.</param>
    public Net.DisplayName DisplayNameOr(Net.DisplayName characterName) =>
        Net.DisplayName.TryParse(DisplayNameAlias, out var alias) ? alias : characterName;

    /// <summary>
    /// What the settings box starts out showing (R-1.3e — "pre-filled with their character name").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An empty box does not satisfy "pre-filled", and that is a citation rather than a
    /// preference.</b> The user opens the control and sees the name that will be sent already in it,
    /// then edits or leaves it.
    /// </para>
    /// <para>
    /// <b>Deliberately the raw alias rather than <see cref="DisplayNameOr"/>.</b> When an alias is
    /// stored but unusable the effective name is the character name — showing that here would
    /// replace what the user typed with something they did not, while the warning beside it tells
    /// them to fix a value the box no longer contains.
    /// </para>
    /// </remarks>
    /// <param name="characterName">What the game says this player is called.</param>
    public string NameToEdit(Net.DisplayName characterName) =>
        DisplayNameAlias.Length > 0 ? DisplayNameAlias : characterName.Value;

    /// <summary>
    /// Records what the user left in the settings box, reporting whether anything changed.
    /// </summary>
    /// <remarks>
    /// <b>Typing your own character name means "use my character name", not "freeze this string".</b>
    /// The box is pre-filled with it, so the commonest edit is no edit at all — and storing it as an
    /// alias would pin today's name, so a player who is renamed would keep sending the old one with
    /// nothing on screen explaining why. Matching it clears the alias instead, which keeps the
    /// default tracking rather than snapshotting it.
    /// </remarks>
    /// <param name="typed">What is in the box.</param>
    /// <param name="characterName">What the game says this player is called.</param>
    public bool RecordChosenName(string? typed, Net.DisplayName characterName)
    {
        var trimmed = string.IsNullOrWhiteSpace(typed) ? string.Empty : typed.Trim();

        return RecordDisplayNameAlias(
            string.Equals(trimmed, characterName.Value, StringComparison.Ordinal)
                ? string.Empty
                : trimmed);
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
