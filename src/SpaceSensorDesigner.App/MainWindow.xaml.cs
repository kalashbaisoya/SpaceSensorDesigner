using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SpaceSensorDesigner.App.Controls;
using SpaceSensorDesigner.App.Services;
using SpaceSensorDesigner.App.ViewModels;

namespace SpaceSensorDesigner.App;

/// <summary>
/// Interaction logic for MainWindow.xaml. Owns the <see cref="MainViewModel"/>, drives the custom
/// window caption buttons, and starts drag-and-drop from the library list boxes.
/// </summary>
public partial class MainWindow : Window
{
    // Segoe MDL2 Assets glyphs for the maximize / restore caption button (built from code points
    // so the source stays plain ASCII).
    private static readonly string GlyphMaximize = char.ConvertFromUtf32(0xE922);
    private static readonly string GlyphRestore = char.ConvertFromUtf32(0xE923);

    private readonly MainViewModel _vm;
    private bool _goingHome;

    private Point _dragStart;
    private bool _dragArmed;

    public MainWindow() : this(new MainViewModel()) { }

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.RequestGoHome += OnGoHomeRequested;
        _vm.CaptureFloorImage = CaptureSurface;
        Closing += OnWindowClosing;
        StateChanged += (_, _) =>
            MaxButton.Content = WindowState == WindowState.Maximized ? GlyphRestore : GlyphMaximize;
    }

    // ---- Navigation back to the home screen --------------------------------

    private void OnGoHomeRequested()
    {
        _goingHome = true;
        Close();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmDiscardIfNeeded())
        {
            e.Cancel = true;
            _goingHome = false;
            return;
        }

        if (_goingHome)
        {
            var home = new HomeWindow(_vm.RecentFiles ?? new RecentFilesService(), _vm.Settings);
            home.Show();
        }
    }

    /// <summary>Returns false only when the user cancels leaving (so the caller aborts the close).</summary>
    private bool ConfirmDiscardIfNeeded()
    {
        if (!_vm.HasUnsavedChanges) return true;

        var choice = MessageBox.Show(this,
            "You have unsaved changes. Save before leaving?",
            AppInfo.ProductName, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        switch (choice)
        {
            case MessageBoxResult.Yes:
                _vm.SaveCommand.Execute(null);
                return !_vm.HasUnsavedChanges; // Save dialog cancelled ⇒ still dirty ⇒ block the close
            case MessageBoxResult.No:
                return true;
            default:
                return false;
        }
    }

    // ---- Snapshot the canvas for PNG / report export -----------------------

    private System.Windows.Media.Imaging.BitmapSource? CaptureSurface()
    {
        if (Surface.ActualWidth < 2 || Surface.ActualHeight < 2) return null;
        const double scale = 1.5; // render at 1.5× for a crisp export
        int pw = (int)Math.Ceiling(Surface.ActualWidth * scale);
        int ph = (int)Math.Ceiling(Surface.ActualHeight * scale);
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            pw, ph, 96 * scale, 96 * scale, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(Surface);
        rtb.Freeze();
        return rtb;
    }

    private void OnDropdownButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.ContextMenu is { } menu)
        {
            menu.PlacementTarget = b;
            menu.IsOpen = true;
        }
    }

    // ---- Custom window caption controls ------------------------------------

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    // ---- Library drag source -----------------------------------------------

    private void Library_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragArmed = true;
    }

    private void Library_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragArmed || e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is not ListBox list) return;
        var item = ItemUnderMouse(list, e);
        if (item is not LibraryItemViewModel vm) return;

        _dragArmed = false;

        var data = new DataObject();
        if (vm.IsSensor && vm.Sensor != null)
            data.SetData(DesignSurface.SensorDataFormat, vm.Sensor);
        else if (vm.Furniture != null)
            data.SetData(DesignSurface.FurnitureDataFormat, vm.Furniture);
        else
            return;

        DragDrop.DoDragDrop(list, data, DragDropEffects.Copy);
    }

    private static object? ItemUnderMouse(ListBox list, MouseEventArgs e)
    {
        var element = list.InputHitTest(e.GetPosition(list)) as DependencyObject;
        while (element != null && element is not ListBoxItem)
            element = VisualTreeHelper.GetParent(element);
        return (element as ListBoxItem)?.DataContext;
    }
}
