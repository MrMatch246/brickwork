using System.Text.Json;
using Brickwork.Core.Models;

namespace Brickwork.Core.Settings;

/// <summary>
/// Persists per-version tree trunk adjustments (<c>tree-trunks.json</c> next to <c>settings.json</c>),
/// keyed by <see cref="TreeTrunks.VersionKey(int?, string?)"/>. Inkarnate asset ids are the same in
/// every map, so an adjusted tree version is right on all maps that use it.
/// </summary>
public sealed class TreeTrunkLibraryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public TreeTrunkLibraryStore(string? directory = null)
    {
        var root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Brickwork");
        FilePath = Path.Combine(root, "tree-trunks.json");
    }

    public string FilePath { get; }

    public Dictionary<string, TreeTrunkAdjustment> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var file = JsonSerializer.Deserialize<LibraryFile>(File.ReadAllText(FilePath), SerializerOptions);
            return file?.Versions is { } versions
                ? versions
                    .Where(entry => entry.Value is { Scale: > 0 })
                    .ToDictionary(entry => entry.Key, entry => entry.Value)
                : [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IReadOnlyDictionary<string, TreeTrunkAdjustment> versions)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var file = new LibraryFile
        {
            Versions = versions
                .Where(entry => !entry.Value.IsDefault)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key, entry => entry.Value),
        };
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(file, SerializerOptions));
        File.Move(tempPath, FilePath, overwrite: true);
    }

    private sealed class LibraryFile
    {
        public Dictionary<string, TreeTrunkAdjustment> Versions { get; set; } = [];
    }
}
