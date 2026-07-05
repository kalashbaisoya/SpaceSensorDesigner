using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SpaceSensorDesigner.App.Services;

namespace SpaceSensorDesigner.App;

/// <summary>
/// Application entry point. Loads persisted settings + recent files, then opens the home screen —
/// or jumps straight into the last project when "reopen last project" is enabled.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        var settings = AppSettings.Load();
        var recent = new RecentFilesService();

        var home = new HomeWindow(recent, settings);

        // Optional: skip the landing screen and reopen the most recent project.
        // If the reopen fails (missing/corrupt file), fall through to the home screen.
        if (settings.ReopenLastProject && recent.Items.FirstOrDefault() is { } last && home.OpenProjectPath(last.Path))
            return;

        home.Show();
    }

    /// <summary>
    /// Last-resort guard: log the failure and keep the app alive rather than crashing to desktop.
    /// </summary>
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppPaths.DataFolder, "error.log"),
                $"{DateTime.Now:O}  {e.Exception}\n\n");
        }
        catch { /* logging must never throw */ }

        MessageBox.Show(
            "Something went wrong:\n\n" + e.Exception.Message +
            "\n\nThe app will try to keep running. Details were written to error.log.",
            AppInfo.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);

        e.Handled = true;
    }
}
