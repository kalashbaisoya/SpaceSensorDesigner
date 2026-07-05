using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpaceSensorDesigner.App.Services;
using SpaceSensorDesigner.Core.Catalog;
using SpaceSensorDesigner.Core.Models;
using SpaceSensorDesigner.Core.Serialization;

namespace SpaceSensorDesigner.App.ViewModels;

/// <summary>A single recent-project card shown on the landing screen.</summary>
public sealed class RecentProjectViewModel
{
    public RecentProjectViewModel(RecentProjectInfo info)
    {
        Path = info.Path;
        Name = string.IsNullOrWhiteSpace(info.Name) ? System.IO.Path.GetFileNameWithoutExtension(info.Path) : info.Name;
        FileName = System.IO.Path.GetFileName(info.Path);
        Folder = System.IO.Path.GetDirectoryName(info.Path) ?? string.Empty;
        LastOpened = info.LastOpenedUtc.ToLocalTime().ToString("d MMM yyyy · HH:mm");
        Initials = MakeInitials(Name);
    }

    public string Path { get; }
    public string Name { get; }
    public string FileName { get; }
    public string Folder { get; }
    public string LastOpened { get; }
    public string Initials { get; }

    private static string MakeInitials(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return "•";
        var parts = name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}"
            : char.ToUpperInvariant(parts[0][0]).ToString();
    }
}

/// <summary>
/// Backing view model for the landing / home screen: new-project actions, templates, and the
/// recent-projects list. Raises <see cref="DesignerRequested"/> / <see cref="SettingsRequested"/> /
/// <see cref="ExitRequested"/> which the window turns into navigation.
/// </summary>
public sealed partial class HomeViewModel : ObservableObject
{
    private readonly FileDialogService _files = new();
    private readonly RecentFilesService _recent;
    private readonly AppSettings _settings;

    /// <summary>Raised with a fully-loaded designer VM to switch into the editor.</summary>
    public event Action<MainViewModel>? DesignerRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public HomeViewModel(RecentFilesService recent, AppSettings settings)
    {
        _recent = recent;
        _settings = settings;
        Version = $"Version {AppInfo.Version}";
        RefreshRecent();
    }

    public string Version { get; }

    public ObservableCollection<RecentProjectViewModel> Recent { get; } = new();

    public bool HasRecent => Recent.Count > 0;

    public void RefreshRecent()
    {
        _recent.Reload();
        Recent.Clear();
        foreach (var i in _recent.Items) Recent.Add(new RecentProjectViewModel(i));
        OnPropertyChanged(nameof(HasRecent));
    }

    private MainViewModel BuildDesigner(DesignProject project, string? path)
        => new(project, path, _recent, _settings);

    private void Launch(DesignProject project, string? path)
        => DesignerRequested?.Invoke(BuildDesigner(project, path));

    // ---- New ----------------------------------------------------------------

    [RelayCommand]
    private void NewProject() => Launch(DesignProject.CreateSample(), null);

    [RelayCommand]
    private void NewBlank()
    {
        var project = new DesignProject { Name = "Untitled Project" };
        project.Floors.Add(new FloorPlan { Name = "Floor 1", SnapSize = _settings.SnapSizeMeters });
        Launch(project, null);
    }

    [RelayCommand]
    private void NewFromTemplate(string key)
    {
        var project = new DesignProject { Name = PlanTemplates.DisplayName(key) };
        project.Floors.Add(PlanTemplates.Create(key));
        Launch(project, null);
    }

    // ---- Open ---------------------------------------------------------------

    [RelayCommand]
    private void OpenProject()
    {
        var path = _files.AskOpenPath();
        if (path == null) return;
        OpenPath(path);
    }

    [RelayCommand]
    private void OpenRecent(RecentProjectViewModel? item)
    {
        if (item != null) OpenPath(item.Path);
    }

    /// <summary>Loads and opens a project. Returns true if the designer was launched.</summary>
    public bool OpenPath(string path)
    {
        try
        {
            var project = ProjectSerializer.Load(path);
            Launch(project, path);
            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Could not open \"{Path.GetFileName(path)}\".\n\n{ex.Message}",
                AppInfo.ProductName, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            _recent.Remove(path);
            RefreshRecent();
            return false;
        }
    }

    [RelayCommand]
    private void RemoveRecent(RecentProjectViewModel? item)
    {
        if (item == null) return;
        _recent.Remove(item.Path);
        RefreshRecent();
    }

    [RelayCommand]
    private void ClearRecent()
    {
        _recent.Clear();
        RefreshRecent();
    }

    // ---- Misc ---------------------------------------------------------------

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke();

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke();
}
