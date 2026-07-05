using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpaceSensorDesigner.App.Services;

/// <summary>One entry in the "recent projects" list on the home screen.</summary>
public sealed class RecentProjectInfo
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime LastOpenedUtc { get; set; }

    public bool Exists => !string.IsNullOrWhiteSpace(Path) && File.Exists(Path);
}

/// <summary>
/// Persists the most-recently-opened projects to <c>%AppData%\SpaceSensor Designer\recent.json</c>.
/// De-duplicates by path, keeps newest first, and caps the list.
/// </summary>
public sealed class RecentFilesService
{
    private const int MaxEntries = 12;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private List<RecentProjectInfo> _items = new();

    public RecentFilesService() => Reload();

    /// <summary>Recent entries, newest first, with missing files pruned out.</summary>
    public IReadOnlyList<RecentProjectInfo> Items =>
        _items.Where(i => i.Exists).OrderByDescending(i => i.LastOpenedUtc).ToList();

    public void Reload()
    {
        try
        {
            var path = AppPaths.RecentFile;
            _items = File.Exists(path)
                ? JsonSerializer.Deserialize<List<RecentProjectInfo>>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch
        {
            _items = new();
        }
    }

    public void Add(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var full = Path.GetFullPath(path);

        _items.RemoveAll(i => string.Equals(i.Path, full, StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, new RecentProjectInfo { Path = full, Name = name, LastOpenedUtc = DateTime.UtcNow });

        if (_items.Count > MaxEntries) _items = _items.Take(MaxEntries).ToList();
        Persist();
    }

    public void Remove(string path)
    {
        _items.RemoveAll(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
        Persist();
    }

    public void Clear()
    {
        _items.Clear();
        Persist();
    }

    private void Persist()
    {
        try
        {
            File.WriteAllText(AppPaths.RecentFile, JsonSerializer.Serialize(_items, JsonOptions));
        }
        catch
        {
            // non-fatal
        }
    }
}
