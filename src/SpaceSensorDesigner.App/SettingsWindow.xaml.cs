using System.Windows;
using SpaceSensorDesigner.App.Services;
using SpaceSensorDesigner.App.ViewModels;

namespace SpaceSensorDesigner.App;

/// <summary>Modal preferences dialog. Edits the shared <see cref="AppSettings"/> in place on Save.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _vm = new SettingsViewModel(settings);
        _vm.Saved += () => { DialogResult = true; Close(); };
        _vm.Cancelled += Close;
        DataContext = _vm;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
