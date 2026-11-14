using System.Text.Json;

namespace SmartHomeAI.Hub;

/// <summary>Manual-command times kept in a small JSON file so a restart keeps the auto pause. Write errors only warn.</summary>
public class ManualStore(string path, ILogger? logger = null)
{
    public Dictionary<string, DateTimeOffset> Load()
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(File.ReadAllText(path)) ?? [] : []; }
        catch (Exception ex) { logger?.LogWarning("Cannot read {Path}: {Error}", path, ex.Message); return []; }
    }

    public void Save(IReadOnlyDictionary<string, DateTimeOffset> times)
    {
        try
        {
            if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } dir) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(times));
        }
        catch (Exception ex) { logger?.LogWarning("Cannot write {Path}: {Error}", path, ex.Message); }
    }
}
