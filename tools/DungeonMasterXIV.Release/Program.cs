using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DungeonMasterXIV.Release;

// The named mechanism R-7.2 asks for: the repository manifest is generated, never hand-edited.
//
//   dotnet run --project tools/DungeonMasterXIV.Release --                 \
//       --assembly bin/x64/Release/DungeonMasterXIV.dll                    \
//       --plugin-manifest DungeonMasterXIV.json                            \
//       --tag v0.1.0 --api-level <confirmed> --out repo.json               \
//       [--dry-run]
//
// --dry-run prints the manifest and writes nothing. It is how this is verified without cutting a
// release, which is the whole point of the current gate: a manifest today would deliver a plugin
// that installs cleanly and does nothing, because no relay is running.

var options = ParseArguments(args);

if (!options.TryGetValue("assembly", out var assemblyPath) ||
    !options.TryGetValue("plugin-manifest", out var pluginManifestPath) ||
    !options.TryGetValue("tag", out var tag) ||
    !options.TryGetValue("api-level", out var apiLevelText))
{
    Console.Error.WriteLine(
        "Required: --assembly <path> --plugin-manifest <path> --tag <git tag> --api-level <n>. " +
        "Optional: --out <path>, --dry-run. Nothing is defaulted; see ReleaseInputs.");
    return 2;
}

if (!int.TryParse(apiLevelText, out var apiLevel))
{
    Console.Error.WriteLine($"--api-level must be a number; got '{apiLevelText}'.");
    return 2;
}

var plugin = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(pluginManifestPath))
    ?? throw new InvalidOperationException($"'{pluginManifestPath}' is not a plugin manifest.");

var inputs = new ReleaseInputs(tag, PluginAssemblyVersion.Of(assemblyPath), apiLevel, plugin.RepoUrl);
var manifest = RepositoryManifest.Build(inputs, plugin);

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
