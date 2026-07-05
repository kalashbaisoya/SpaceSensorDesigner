using System.Windows;
using SpaceSensorDesigner.App.Services;
using SpaceSensorDesigner.App.ViewModels;

namespace SpaceSensorDesigner.App;

/// <summary>
/// The landing / start screen. Hosts <see cref="HomeViewModel"/> and turns its navigation events
/// into real window transitions: open the designer, show the settings dialog, or quit.
/// </summary>
public partial class HomeWindow : Window
{
    private readonly HomeViewModel _vm;
    private readonly RecentFilesService _recent;
    private readonly AppSettings _settings;

    public HomeWindow(RecentFilesService recent, AppSettings settings)
    {
        InitializeComponent();
        _recent = recent;
        _settings = settings;

        _vm = new HomeViewModel(recent, settings);
        _vm.DesignerRequested += OpenDesigner;
        _vm.SettingsRequested += OpenSettings;
        _vm.ExitRequested += Close;
        DataContext = _vm;

        // Refresh the recent list whenever the window regains focus (e.g. after returning from the designer).
        Activated += (_, _) => _vm.RefreshRecent();
    }

    /// <summary>Opens a project by path (used for "reopen last project on startup"). Returns true if the designer launched.</summary>
    public bool OpenProjectPath(string path) => _vm.OpenPath(path);

    private void OpenDesigner(MainViewModel vm)
    {
        var win = new MainWindow(vm);
        win.Show();
        Close();
    }

    private void OpenSettings()
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        dlg.ShowDialog();
    }

    // ---- Caption buttons ----
    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
