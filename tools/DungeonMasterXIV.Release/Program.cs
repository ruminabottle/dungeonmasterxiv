using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DungeonMasterXIV.Release;

var options = ParseArguments(args);

if (options.ContainsKey("api-level"))
{
    Console.Error.WriteLine(
        "--api-level no longer exists. The API level is copied from the built plugin manifest " +
        "so that it is never typed. Pass the BUILT manifest to --plugin-manifest.");
    return 2;
}

if (!options.TryGetValue("assembly", out var assemblyPath) ||
    !options.TryGetValue("plugin-manifest", out var pluginManifestPath) ||
    !options.TryGetValue("tag", out var tag) ||
    !options.TryGetValue("asset", out var assetPath))
{
    Console.Error.WriteLine(
        "Required: --assembly <path> --plugin-manifest <BUILT manifest> --asset <built zip> " +
        "--tag <git tag>. Optional: --out <path>, --dry-run. " +
        "Nothing is defaulted or typed; see ReleaseInputs.");
    return 2;
}

PluginManifest plugin;
ReleaseInputs inputs;
string manifest;

try
{
    var asset = ReleaseAsset.At(assetPath);
    asset.MustMatchTheAssembly(assemblyPath);

    plugin = (JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(pluginManifestPath))
        ?? throw new InvalidOperationException($"'{pluginManifestPath}' is not a plugin manifest."))
        .RequireBuilt(pluginManifestPath);

    asset.MustCarryTheSameMetadataAs(plugin, pluginManifestPath);

    inputs = new ReleaseInputs(
        tag, PluginAssemblyVersion.Of(assemblyPath), plugin.DalamudApiLevel!.Value, plugin.RepoUrl, asset);
    manifest = RepositoryManifest.Build(inputs, plugin);
}
catch (Exception failure) when (
    failure is InvalidOperationException or ArgumentException or FileNotFoundException
        or InvalidDataException or JsonException)
{
    Console.Error.WriteLine(failure.Message);
    Console.Error.WriteLine("No manifest generated. No tag created, no artefact published.");
    return 2;
}

if (options.ContainsKey("dry-run") || !options.TryGetValue("out", out var outputPath))
{
    Console.WriteLine(manifest);
    Console.Error.WriteLine("Dry run: nothing written, no tag created, no artefact published.");
    return 0;
}

File.WriteAllText(outputPath, manifest);
Console.Error.WriteLine($"Wrote {outputPath} for {tag} at assembly version {inputs.AssemblyVersion}.");
return 0;

static Dictionary<string, string> ParseArguments(string[] arguments)
{
    var parsed = new Dictionary<string, string>(StringComparer.Ordinal);

    for (var i = 0; i < arguments.Length; i++)
    {
        if (!arguments[i].StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var name = arguments[i][2..];
        var hasValue = i + 1 < arguments.Length && !arguments[i + 1].StartsWith("--", StringComparison.Ordinal);
        parsed[name] = hasValue ? arguments[++i] : string.Empty;
    }

    return parsed;
}
