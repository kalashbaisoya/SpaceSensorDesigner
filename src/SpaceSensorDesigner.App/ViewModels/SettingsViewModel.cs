using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpaceSensorDesigner.App.Services;

namespace SpaceSensorDesigner.App.ViewModels;

/// <summary>
/// Edits application preferences. Binds to a working copy of the fields; <see cref="Save"/> writes
/// them back into the shared <see cref="AppSettings"/> instance (so a running designer picks them up),
/// persists to disk, and raises <see cref="Saved"/>.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _shared;
    private readonly UpdateService _updates = new();

    /// <summary>Raised when the user saves; the host closes the dialog.</summary>
    public event Action? Saved;
    /// <summary>Raised when the user cancels.</summary>
    public event Action? Cancelled;

    public SettingsViewModel(AppSettings shared)
    {
        _shared = shared;
        LoadFrom(shared);
        DataFolder = AppPaths.DataFolder;
        VersionText = $"{AppInfo.ProductName} {AppInfo.Version}";
    }

    // ---- Bindable working copy ----
    [ObservableProperty] private bool _defaultViewIsometric;
    [ObservableProperty] private bool _showGridOnStart;
    [ObservableProperty] private bool _showHeatmapOnStart;
    [ObservableProperty] private bool _reopenLastProject;
    [ObservableProperty] private double _snapSizeMeters;
    [ObservableProperty] private double _defaultSensorHeightMeters;
    [ObservableProperty] private int _autosaveMinutes;
    [ObservableProperty] private string _updateFeedUrl = string.Empty;
    [ObservableProperty] private bool _checkForUpdatesOnStartup;

    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private bool _isCheckingUpdate;

    public string DataFolder { get; }
    public string VersionText { get; }

    private void LoadFrom(AppSettings s)
    {
        DefaultViewIsometric = s.DefaultViewIsometric;
        ShowGridOnStart = s.ShowGridOnStart;
        ShowHeatmapOnStart = s.ShowHeatmapOnStart;
        ReopenLastProject = s.ReopenLastProject;
        SnapSizeMeters = s.SnapSizeMeters;
        DefaultSensorHeightMeters = s.DefaultSensorHeightMeters;
        AutosaveMinutes = s.AutosaveMinutes;
        UpdateFeedUrl = s.UpdateFeedUrl;
        CheckForUpdatesOnStartup = s.CheckForUpdatesOnStartup;
    }

    private void CopyInto(AppSettings s)
    {
        s.DefaultViewIsometric = DefaultViewIsometric;
        s.ShowGridOnStart = ShowGridOnStart;
        s.ShowHeatmapOnStart = ShowHeatmapOnStart;
        s.ReopenLastProject = ReopenLastProject;
        s.SnapSizeMeters = Math.Clamp(SnapSizeMeters, 0.05, 1.0);
        s.DefaultSensorHeightMeters = Math.Clamp(DefaultSensorHeightMeters, 1.5, 5.0);
        s.AutosaveMinutes = Math.Clamp(AutosaveMinutes, 0, 120);
        s.UpdateFeedUrl = (UpdateFeedUrl ?? string.Empty).Trim();
        s.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;
    }

    [RelayCommand]
    private void Save()
    {
        CopyInto(_shared);
        _shared.Save();
        Saved?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    [RelayCommand]
    private void ResetDefaults() => LoadFrom(AppSettings.Default);

    [RelayCommand]
    private void OpenDataFolder()
    {
        try { Process.Start(new ProcessStartInfo(DataFolder) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task CheckForUpdate()
    {
        IsCheckingUpdate = true;
        UpdateStatus = "Checking…";
        var result = await _updates.CheckAsync((UpdateFeedUrl ?? string.Empty).Trim());
        UpdateStatus = result.Message ?? result.Status.ToString();
        IsCheckingUpdate = false;
    }
}
