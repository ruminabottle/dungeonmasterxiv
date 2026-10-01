using System;
using System.IO;
using System.Reflection;

namespace DungeonMasterXIV.Release;

/// <summary>Reads the assembly version from a built plugin assembly.</summary>
public static class PluginAssemblyVersion
{
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
