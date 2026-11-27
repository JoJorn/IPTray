using System.IO;
using System.Text.Json;

namespace IPTray;

/// <summary>User settings, stored in %LOCALAPPDATA%\IPTray\settings.json.</summary>
public sealed class Settings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IPTray", "settings.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Name of the adapter selected last time.</summary>
    public string? LastAdapter { get; set; }

    // Add new settings here (e.g. IP profiles); missing ones get their default.

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new(); }
        catch { return new(); } // first run, or file unreadable
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch { } // not worth bothering the user about
    }
}
