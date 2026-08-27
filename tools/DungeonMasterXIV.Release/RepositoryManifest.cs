using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DungeonMasterXIV.Release;

/// <summary>
/// Builds the repository manifest a user pastes into Dalamud as a custom repository.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not the same artefact as <c>DungeonMasterXIV.json</c> in the repo root.</b> That one is the
/// plugin's own manifest and ships inside the zip; this one is served over raw GitHub and is what
/// Dalamud reads to decide whether to offer the plugin at all (R-7.2).
/// </para>
/// <para>
/// <b>Testing channel only.</b> Only the testing fields are populated and
/// <c>IsTestingExclusive</c> is true, so a user who has not deliberately enabled testing builds
/// receives nothing (R-7.1). That is D-12's second gate and the one that actually holds — the
/// unadvertised URL stops being a gate the moment somebody pastes it into a Discord.
/// </para>
/// <para>
/// Generated, never hand-edited (R-7.2). A hand-maintained manifest drifts from the artefact it
/// points at and fails at the moment a user installs, with an error only that user ever sees.
/// </para>
/// </remarks>
public static class RepositoryManifest
{
    // Relaxed escaping so the description reads as written rather than as \u2014 and \u0027. Both
    // forms are valid JSON and Dalamud parses either; the difference is that A-7.7 is checked by a
    // person reading this file, and escaped punctuation is exactly the kind of noise that gets
    // skimmed past. Nothing here is embedded in HTML, which is the case relaxed escaping is unsafe
    // for.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Builds the manifest as a JSON array of one plugin, which is the shape Dalamud expects of a
    /// custom repository.
    /// </summary>
    /// <param name="inputs">The release being described. Validated before anything is built.</param>
    /// <param name="plugin">The plugin's own manifest, whose product copy is reused verbatim.</param>
    public static string Build(ReleaseInputs inputs, PluginManifest plugin)
    {
        inputs.Validate();

        var entry = new JsonObject
        {
            ["Author"] = plugin.Author,
            ["Name"] = plugin.Name,
            ["InternalName"] = PluginManifest.InternalName,
            ["Punchline"] = plugin.Punchline,
            ["Description"] = plugin.Description,
            ["RepoUrl"] = plugin.RepoUrl,
            ["Tags"] = new JsonArray(plugin.Tags.ConvertAll(tag => (JsonNode)JsonValue.Create(tag)!).ToArray()),
            ["ApplicableVersion"] = "any",

            // Testing channel only. The stable fields are deliberately absent rather than blank:
            // a populated stable field is what would make this visible to everyone (R-7.1, D-12).
            ["IsTestingExclusive"] = true,
            ["TestingAssemblyVersion"] = inputs.AssemblyVersion.ToString(),
            ["TestingDalamudApiLevel"] = inputs.DalamudApiLevel,
            ["DownloadLinkTesting"] = inputs.DownloadLink,

            // Dalamud reads these even for a testing-exclusive plugin. They point at the same tagged
            // asset; what keeps the plugin off the stable channel is IsTestingExclusive, not a
            // missing link.
            ["DownloadLinkInstall"] = inputs.DownloadLink,
            ["DownloadLinkUpdate"] = inputs.DownloadLink,
            ["DalamudApiLevel"] = inputs.DalamudApiLevel,
        };

        return new JsonArray(entry).ToJsonString(Options);
    }
}
