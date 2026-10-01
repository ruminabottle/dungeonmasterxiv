using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DungeonMasterXIV.Release;

/// <summary>Builds the testing-only Dalamud repository manifest JSON for a release.</summary>
public static class RepositoryManifest
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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

            ["IsTestingExclusive"] = true,
            ["TestingAssemblyVersion"] = inputs.AssemblyVersion.ToString(),
            ["TestingDalamudApiLevel"] = inputs.DalamudApiLevel,
            ["DownloadLinkTesting"] = inputs.DownloadLink,

            ["DownloadLinkInstall"] = inputs.DownloadLink,
            ["DownloadLinkUpdate"] = inputs.DownloadLink,
            ["DalamudApiLevel"] = inputs.DalamudApiLevel,

            ["AssemblyVersion"] = inputs.AssemblyVersion.ToString(),
        };

        return new JsonArray(entry).ToJsonString(Options);
    }
}
