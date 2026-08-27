using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Release;

/// <summary>
/// The plugin's own manifest, as BUILT — the copy beside the assembly in the output directory,
/// which is what ships inside the zip.
/// </summary>
/// <remarks>
/// <para>
/// Read rather than restated so the product copy exists once. R-7.3 fixes <c>Name</c>,
/// <c>Punchline</c> and <c>Description</c> as the Product Owner's, and a second copy in a release
/// script is a second thing to keep in step that nobody would notice going stale.
/// </para>
/// <para>
/// <b>The BUILT manifest, not the source one at the repo root.</b> They are different files: the
/// source carries only product copy, and the build stamps <see cref="DalamudApiLevel"/>,
/// <see cref="AssemblyVersion"/> and <c>InternalName</c> onto the copy that ships. R-7.3a requires
/// the API level to be copied from the artefact rather than typed, so this must be the built one —
/// and <see cref="RequireBuilt"/> refuses the source manifest by name rather than producing a
/// manifest with a field missing.
/// </para>
/// </remarks>
public sealed class PluginManifest
{
    /// <summary>Permanent, per PRD-0 R-0.1. Never derived from a file name or a version.</summary>
    public const string InternalName = "DungeonMasterXIV";

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("Punchline")]
    public string Punchline { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("RepoUrl")]
    public string RepoUrl { get; set; } = string.Empty;

    [JsonPropertyName("Tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// The Dalamud API level this build targets, stamped by the SDK at build time. Null in the
    /// source manifest, which is how the two files are told apart.
    /// </summary>
    [JsonPropertyName("DalamudApiLevel")]
    public int? DalamudApiLevel { get; set; }

    /// <summary>
    /// The assembly version the build stamped here. A second reading of the same fact the DLL
    /// carries, and useful precisely because the two are produced by different steps.
    /// </summary>
    [JsonPropertyName("AssemblyVersion")]
    public string? AssemblyVersion { get; set; }

    /// <summary>
    /// Confirms this is the built manifest and not the source one.
    /// </summary>
    /// <remarks>
    /// The message matters as much as the check. A missing API level here means <b>the build did
    /// not produce what we expected</b> — a real failure worth investigating — and not "a human has
    /// not told us a number", which is a queue somebody clears by guessing. R-7.3a exists because
    /// this field fails silently: a wrong value makes Dalamud never offer the plugin, with nothing
    /// written anywhere on our side.
    /// </remarks>
    /// <param name="path">Where it was read from, for the message.</param>
    public PluginManifest RequireBuilt(string path)
    {
        if (DalamudApiLevel is null)
        {
            throw new InvalidOperationException(
                $"'{path}' carries no DalamudApiLevel, so it is not a built plugin manifest. The " +
                "build stamps that field; the source manifest at the repository root never has it. " +
                "Either this is the source manifest rather than the one beside the built assembly, " +
                "or the build did not produce what we expected — both are worth stopping for, and " +
                "neither is fixed by supplying a number.");
        }

        return this;
    }
}
