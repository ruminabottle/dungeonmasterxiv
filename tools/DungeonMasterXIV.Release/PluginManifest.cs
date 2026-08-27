using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Release;

/// <summary>
/// The plugin's own manifest — <c>DungeonMasterXIV.json</c> in the repo root, which ships inside
/// the zip.
/// </summary>
/// <remarks>
/// Read rather than restated so the product copy exists once. R-7.3 fixes <c>Name</c>,
/// <c>Punchline</c> and <c>Description</c> as the Product Owner's, and a second copy in a release
/// script is a second thing to keep in step that nobody would notice going stale.
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
}
