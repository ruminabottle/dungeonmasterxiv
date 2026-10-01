using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DungeonMasterXIV.Release;

/// <summary>A committed repository manifest file, checked field by field against freshly generated output.</summary>
public sealed class PublishedManifest
{
    private readonly JsonNode document;

    private PublishedManifest(string path, JsonNode document)
    {
        Path = path;
        this.document = document;
    }

    public string Path { get; }

    public static PublishedManifest At(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"No repository manifest at '{path}'. This is the file a tester pastes into Dalamud " +
                "as a custom repository; without it the URL 404s while the release itself looks " +
                "fine. Generate it with the release tool and commit it (R-7.2).",
                path);
        }

        return new PublishedManifest(path, Parse(File.ReadAllText(path), path));
    }

    public void MustMatch(string generated, string tag)
    {
        var expected = Parse(generated, "the freshly generated manifest");

        var differences = Differences(document, expected).ToList();

        if (differences.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"'{Path}' is not what the release tool generates for '{tag}':\n" +
            string.Join("\n", differences) + "\n" +
            "The tool's output is the authority — this file is generated, never hand-edited (R-7.2). " +
            "Every field is checked, not a chosen few, so this catches an edit to any of them " +
            "including IsTestingExclusive, which is half of D-12's gate between a testing-only " +
            "plugin and one offered to everyone holding the URL.\n" +
            RegenerateWith(tag));
    }

    private static IEnumerable<string> Differences(JsonNode committed, JsonNode expected)
    {
        var here = Fields(committed);
        var there = Fields(expected);

        foreach (var field in here.Keys.Union(there.Keys).OrderBy(name => name, StringComparer.Ordinal))
        {
            var mine = here.GetValueOrDefault(field);
            var theirs = there.GetValueOrDefault(field);

            if (mine == theirs)
            {
                continue;
            }

            yield return mine is null ? $"  {field}: absent here, generated as {theirs}"
                : theirs is null ? $"  {field}: present here as {mine}, not generated at all"
                : $"  {field}: this file says {mine}, the tool generates {theirs}";
        }
    }

    private static Dictionary<string, string> Fields(JsonNode manifest) =>
        manifest.AsArray()[0]!.AsObject().ToDictionary(
            property => property.Key,
            property => Canonical(property.Value),
            StringComparer.Ordinal);

    private static string Canonical(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject entry => "{" + string.Join(
            ",",
            entry.OrderBy(property => property.Key, StringComparer.Ordinal)
                .Select(property => $"{JsonSerializer.Serialize(property.Key)}:{Canonical(property.Value)}")) + "}",
        JsonArray items => "[" + string.Join(",", items.Select(Canonical)) + "]",
        _ => node.ToJsonString(),
    };

    private static JsonNode Parse(string content, string describedAs)
    {
        JsonNode? parsed;

        try
        {
            parsed = JsonNode.Parse(content);
        }
        catch (JsonException notJson)
        {
            throw new InvalidOperationException(
                $"'{describedAs}' is not readable as JSON, so nothing can be checked against it. " +
                "Dalamud would reject it too, and the tester sees only an empty repository.", notJson);
        }

        if (parsed is not JsonArray entries || entries.Count != 1 || entries[0] is not JsonObject)
        {
            throw new InvalidOperationException(
                $"'{describedAs}' is not a repository manifest: it must be a JSON array of exactly " +
                "one plugin entry.");
        }

        return parsed;
    }

    private static string RegenerateWith(string tag) =>
        $"    dotnet build -c Release -p:ReleaseTag={tag}\n" +
        "    dotnet run --project tools/DungeonMasterXIV.Release -- \\\n" +
        "        --assembly bin/x64/Release/DungeonMasterXIV.dll \\\n" +
        "        --plugin-manifest bin/x64/Release/DungeonMasterXIV.json \\\n" +
        "        --asset bin/x64/Release/DungeonMasterXIV/latest.zip \\\n" +
        $"        --tag {tag} --out repo.json\n" +
        "  then commit repo.json.";
}
