using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-24: adding a field to <see cref="PluginManifest"/> fails this until the comparison covers it.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this replaces is a sentence, not a mechanism.</b> BUG-16 shipped a hand-written array of
/// eight fields with a comment saying the list was derived from what the repository entry
/// republishes. That was an accurate account of how the list was written and it held nothing true:
/// a ninth property, republished, was compared by nothing, and the whole suite stayed green while a
/// zip advertising one value shipped another. An invariant that enumerates a world which grows
/// fails silently in the direction of passing.
/// </para>
/// <para>
/// <b>Behaviour, not a list of names.</b> This does not check that each property name appears in
/// <c>Differences</c> — a check like that passes on a field that is named and then compared against
/// itself. It varies each property in turn and requires the comparison to <b>refuse and name it</b>,
/// so the thing asserted is the thing wanted.
/// </para>
/// <para>
/// <b>Why the per-field message survives.</b> Deriving the comparison itself by reflection would
/// have cost the message — <i>"Punchline: the build says X, the zip says Y"</i> is what ends an
/// investigation, and a generic "the manifests differ" sends somebody diffing two files by hand. So
/// the production comparison stays hand-written and names its fields; what is mechanical is the
/// proof that the hand-written list is still complete.
/// </para>
/// </remarks>
public class EveryFieldTheManifestCarriesIsComparedTests
{
    private static PropertyInfo[] ManifestProperties() =>
        typeof(PluginManifest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .ToArray();

    // The control. If a mismatch in EVERY property were refused for some reason unrelated to the
    // comparison -- a malformed zip, say -- the coverage assertion below would pass over nothing.
    [Fact]
    public void TwoIdenticalManifestsAreAccepted()
    {
        Asset(Manifest()).MustCarryTheSameMetadataAs(Manifest(), "bin/x64/Release/DungeonMasterXIV.json");
    }

    [Fact]
    public void EveryPropertyOfTheManifestIsComparedAgainstTheZip()
    {
        var properties = ManifestProperties();

        // A world with nothing in it would make the loop below vacuous.
        Assert.NotEmpty(properties);

        var uncovered = properties.Where(property => !ADifferenceIsRefused(property)).ToList();

        Assert.True(
            uncovered.Count == 0,
            $"PluginManifest carries {string.Join(", ", uncovered.Select(property => property.Name))}, and a " +
            "zip differing only in that value is accepted. The repository entry would advertise one " +
            "thing while the archive a user installs says another, which is BUG-16 reopened for the " +
            "new field.\n" +
            "Add it to ReleaseAsset.Differences, keeping the per-field message. If it genuinely must " +
            "not be compared, exclude it here deliberately and say why -- the point of this test is " +
            "that the decision is taken rather than defaulted.");
    }

    private static bool ADifferenceIsRefused(PropertyInfo property)
    {
        var built = Manifest();
        var packaged = Manifest(varying: property);

        try
        {
            Asset(packaged).MustCarryTheSameMetadataAs(built, "bin/x64/Release/DungeonMasterXIV.json");
            return false;
        }
        catch (InvalidOperationException refusal)
        {
            // Refused is not enough -- it has to say WHICH field, or the message stops ending
            // investigations the moment the list grows.
            Assert.Contains(property.Name, refusal.Message, StringComparison.Ordinal);
            return true;
        }
    }

    private static ReleaseAsset Asset(PluginManifest manifest) =>
        ReleaseAsset.At(Assets.Zip(
            Assets.PackagerName,
            (Assets.PluginAssembly, "a build"),
            (Assets.PluginManifestName, JsonSerializer.Serialize(manifest))));

    /// <summary>
    /// A manifest with every property populated, differing from its sibling only in
    /// <paramref name="varying"/>. Built by reflection so a newly added property is populated
    /// without anybody remembering to come here.
    /// </summary>
    private static PluginManifest Manifest(PropertyInfo? varying = null)
    {
        var manifest = new PluginManifest();

        foreach (var property in ManifestProperties())
        {
            property.SetValue(manifest, ValueFor(property, secondValue: property.Name == varying?.Name));
        }

        return manifest;
    }

    private static object ValueFor(PropertyInfo property, bool secondValue)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(string))
        {
            return secondValue ? "the zip's value" : "the build's value";
        }

        if (type == typeof(int))
        {
            return secondValue ? 14 : 13;
        }

        if (type == typeof(List<string>))
        {
            return secondValue ? new List<string> { "second" } : new List<string> { "first" };
        }

        // Not a silent skip. A property of an unhandled type would otherwise be dropped from the
        // sweep, which is this bug's own failure mode arriving through its own fix.
        throw new Xunit.Sdk.XunitException(
            $"PluginManifest.{property.Name} is a {property.PropertyType.Name}, which this test does " +
            "not know how to vary, so it cannot tell whether the comparison covers it. Teach ValueFor " +
            "about that type.");
    }
}
