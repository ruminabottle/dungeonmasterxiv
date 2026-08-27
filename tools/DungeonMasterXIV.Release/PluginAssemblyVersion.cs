using System;
using System.IO;
using System.Reflection;

namespace DungeonMasterXIV.Release;

/// <summary>
/// Reads the version out of the built plugin assembly.
/// </summary>
/// <remarks>
/// <b>The manifest's version comes from the artefact, not from the project file.</b> A-7.2 requires
/// the manifest to match the assembly it links to, and reading the csproj would compare a source of
/// truth with itself — the check could not fail, while the thing it is supposed to catch is a
/// manifest describing a build that was never produced from that source.
/// <para>
/// Metadata only. The assembly is never loaded, so the plugin's Dalamud references never have to
/// resolve — this runs on a machine that has no game on it.
/// </para>
/// </remarks>
public static class PluginAssemblyVersion
{
    /// <summary>Reads the assembly version from a built plugin DLL.</summary>
    /// <param name="assemblyPath">Path to <c>DungeonMasterXIV.dll</c>.</param>
    public static Version Of(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                $"No built plugin assembly at '{assemblyPath}'. Build the plugin before generating a " +
                "manifest: the manifest's version has to come from the artefact it describes.",
                assemblyPath);
        }

        return AssemblyName.GetAssemblyName(assemblyPath).Version
            ?? throw new InvalidOperationException($"'{assemblyPath}' carries no assembly version.");
    }
}
