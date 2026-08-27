using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DungeonMasterXIV.Release;

// The named mechanism R-7.2 asks for: the repository manifest is generated, never hand-edited.
//
//   dotnet run --project tools/DungeonMasterXIV.Release --                 \
//       --assembly bin/x64/Release/DungeonMasterXIV.dll                    \
//       --plugin-manifest bin/x64/Release/DungeonMasterXIV.json            \
//       --tag v0.1.0 --out repo.json [--dry-run]
//
// --plugin-manifest is the BUILT manifest beside the assembly, not the source one at the repository
// root. The build stamps DalamudApiLevel and AssemblyVersion onto it; the source has neither. There
// is deliberately no --api-level: R-7.3a requires it copied from the artefact, and an override would
// reintroduce the typed value the requirement removes -- and would be used the first time somebody
// was in a hurry.
//
// --dry-run prints the manifest and writes nothing. It is how this is verified without cutting a
// release, which is the whole point of the current gate: a manifest today would deliver a plugin
// that installs cleanly and does nothing, because no relay is running.

var options = ParseArguments(args);

// Rejected rather than ignored. Silently accepting a flag that no longer does anything lets someone
// believe they set the API level, which is worse than the flag never having existed.
if (options.ContainsKey("api-level"))
{
    Console.Error.WriteLine(
        "--api-level no longer exists. The API level is copied from the built plugin manifest " +
        "(R-7.3a) so that it is never typed. Pass the BUILT manifest to --plugin-manifest.");
    return 2;
}

if (!options.TryGetValue("assembly", out var assemblyPath) ||
    !options.TryGetValue("plugin-manifest", out var pluginManifestPath) ||
    !options.TryGetValue("tag", out var tag))
{
    Console.Error.WriteLine(
        "Required: --assembly <path> --plugin-manifest <BUILT manifest> --tag <git tag>. " +
        "Optional: --out <path>, --dry-run. Nothing is defaulted or typed; see ReleaseInputs.");
    return 2;
}

PluginManifest plugin;
ReleaseInputs inputs;
string manifest;

// A refusal has to READ like a refusal. These stops are the mechanism working -- a missing API level
// means the build did not produce what we expected -- and a stack trace buries the one sentence that
// says what to do about it. Caught narrowly: anything else still crashes loudly.
try
{
    plugin = (JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(pluginManifestPath))
        ?? throw new InvalidOperationException($"'{pluginManifestPath}' is not a plugin manifest."))
        .RequireBuilt(pluginManifestPath);

    inputs = new ReleaseInputs(
        tag, PluginAssemblyVersion.Of(assemblyPath), plugin.DalamudApiLevel!.Value, plugin.RepoUrl);
    manifest = RepositoryManifest.Build(inputs, plugin);
}
catch (Exception failure) when (
    failure is InvalidOperationException or ArgumentException or FileNotFoundException or JsonException)
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
