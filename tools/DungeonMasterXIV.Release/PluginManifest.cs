using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Release;

/// <summary>The fields of a plugin manifest file, with a check that it comes from a build.</summary>
public sealed class PluginManifest
{
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

    [JsonPropertyName("DalamudApiLevel")]
    public int? DalamudApiLevel { get; set; }

    [JsonPropertyName("AssemblyVersion")]
    public string? AssemblyVersion { get; set; }

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
