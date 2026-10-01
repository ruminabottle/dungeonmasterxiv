using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DungeonMasterXIV.Data;

/// <summary>Keeps retained session logs as one .log.txt file per campaign in a directory.</summary>
public sealed class RetainedLogFileArchive(string directory) : IRetainedLogArchive
{
    private const string Extension = ".log.txt";

    private readonly string _directory =
        directory ?? throw new ArgumentNullException(nameof(directory));

    public IReadOnlyList<Guid> Campaigns()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        return Directory.GetFiles(_directory, $"*{Extension}")
            .Select(path => Path.GetFileName(path)[..^Extension.Length])
            .Select(name => Guid.TryParse(name, out var id) ? id : (Guid?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToList();
    }

    public string? Read(Guid campaignId)
    {
        var path = PathFor(campaignId);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void Write(Guid campaignId, string contents)
    {
        Directory.CreateDirectory(_directory);

        var path = PathFor(campaignId);
        var pending = path + ".writing";

        File.WriteAllText(pending, contents);
        File.Move(pending, path, overwrite: true);
    }

    public IReadOnlyList<string> Unnameable()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        return Directory.GetFiles(_directory)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .Where(name => !IsAWellFormedLog(name))
            .ToList();
    }

    private static bool IsAWellFormedLog(string name) =>
        name.EndsWith(Extension, StringComparison.Ordinal)
        && Guid.TryParse(name[..^Extension.Length], out _);

    public bool DeleteUnnameable(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var path = Path.Combine(_directory, Path.GetFileName(fileName));
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public bool Delete(Guid campaignId)
    {
        var path = PathFor(campaignId);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private string PathFor(Guid campaignId) =>
        Path.Combine(_directory, $"{campaignId}{Extension}");
}
