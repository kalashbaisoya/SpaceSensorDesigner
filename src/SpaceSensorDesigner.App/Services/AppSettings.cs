using System;
using System.IO;
using System.Text.Json;

namespace SpaceSensorDesigner.App.Services;

/// <summary>
/// User preferences for the app, persisted to <c>%AppData%\SpaceSensor Designer\settings.json</c>.
/// Every field maps to a real, applied behaviour in the designer (see <c>MainViewModel.ApplySettings</c>).
/// </summary>
public sealed class AppSettings
{
    // ---- Startup / view defaults ----
    /// <summary>Open new designs in the isometric (3D) view instead of top-down (2D).</summary>
    public bool DefaultViewIsometric { get; set; }
    public bool ShowGridOnStart { get; set; } = true;
    public bool ShowHeatmapOnStart { get; set; } = true;

    /// <summary>Re-open the most recent project automatically when the app launches.</summary>
    public bool ReopenLastProject { get; set; }

    // ---- Editing defaults ----
    /// <summary>Grid snap increment in metres for the drawing tools.</summary>
    public double SnapSizeMeters { get; set; } = 0.25;

    /// <summary>Default ceiling-mount height (m) applied to sensors dropped onto the canvas.</summary>
    public double DefaultSensorHeightMeters { get; set; } = 2.7;

    /// <summary>Auto-save the current file every N minutes (0 disables). Only fires once a file path exists.</summary>
    public int AutosaveMinutes { get; set; }

    // ---- Updates ----
    /// <summary>Optional URL of a version manifest (JSON: <c>{ "version": "1.2.0", "url": "..." }</c>).
    /// Empty means the update check is not configured.</summary>
    public string UpdateFeedUrl { get; set; } = string.Empty;
    public bool CheckForUpdatesOnStartup { get; set; }

    public static AppSettings Default => new();

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    // ---- Persistence ----
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AppSettings Load()
    {
        try
        {
            var path = AppPaths.SettingsFile;
            if (!File.Exists(path)) return Default;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? Default;
        }
        catch
        {
            return Default; // corrupt/unreadable settings should never block launch
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // best-effort; a failure to persist prefs must not crash the app
        }
    }
}
