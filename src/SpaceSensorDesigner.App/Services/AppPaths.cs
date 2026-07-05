using System;
using System.IO;

namespace SpaceSensorDesigner.App.Services;

/// <summary>
/// Resolves the per-user application-data folder (<c>%AppData%\SpaceSensor Designer</c>) and the
/// files the shell persists there (settings, recent-projects list). Creates the folder on demand.
/// </summary>
public static class AppPaths
{
    /// <summary>The root data folder, e.g. <c>C:\Users\me\AppData\Roaming\SpaceSensor Designer</c>.</summary>
    public static string DataFolder
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppInfo.ProductName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
    public static string RecentFile => Path.Combine(DataFolder, "recent.json");
}
