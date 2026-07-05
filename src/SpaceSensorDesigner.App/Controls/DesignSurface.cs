using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SpaceSensorDesigner.App.Rendering;
using SpaceSensorDesigner.App.ViewModels;
using SpaceSensorDesigner.Core.Catalog;
using SpaceSensorDesigner.Core.Geometry;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.App.Controls;

/// <summary>
/// The interactive design canvas. Owns pan/zoom, dispatches mouse input to the active tool,
/// accepts drag-and-drop from the library, and re-renders via <see cref="SceneRenderer"/>.
/// It reads/writes state through the bound <see cref="MainViewModel"/>.
/// </summary>
public sealed class DesignSurface : FrameworkElement
{
    public const string FurnitureDataFormat = "SpaceSensor.FurnitureTemplate";
    public const string SensorDataFormat = "SpaceSensor.SensorTemplate";

    private readonly SceneRenderer _renderer = new();

    private enum DragMode { None, Panning, MovingElement, MovingBackground, Resizing, Rotating, Marquee }
    private DragMode _mode = DragMode.None;

    private Point _lastMouse;
    private Vec2 _bgLastWorld;

    // Group move
    private readonly Dictionary<object, Vec2> _moveStarts = new();
    private object? _grabbed;
    private Vec2 _moveAnchor;

    // Marquee selection
    private Vec2 _marqueeStart;

    private FurnitureItem? _xformItem;
    private Vec2 _xformStartSize;
    private double _xformStartRot;

    private Vec2? _wallStart;
    private Vec2 _previewEnd;
    private Vec2? _rectStart;

    private bool _viewInitialized;

    public DesignSurface()
    {
        Focusable = true;
        AllowDrop = true;
        ClipToBounds = true;
        // A white background rectangle is painted in OnRender, which also makes the whole
        // surface hit-testable for mouse input.
    }

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(MainViewModel), typeof(DesignSurface),
        new PropertyMetadata(null, OnViewModelChanged));

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var surface = (DesignSurface)d;
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.PropertyChanged -= surface.OnViewModelPropertyChanged;
            oldVm.RequestFitToView -= surface.FitToView;
            oldVm.RequestInvalidate -= surface.InvalidateVisual;
        }
        if (e.NewValue is MainViewModel newVm)
        {
            newVm.PropertyChanged += surface.OnViewModelPropertyChanged;
            newVm.RequestFitToView += surface.FitToView;
            newVm.RequestInvalidate += surface.InvalidateVisual;
        }
        surface._viewInitialized = false;
        surface.InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Any of these affect the drawing; the simplest correct thing is to repaint.
        InvalidateVisual();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        if (!_viewInitialized && ViewModel != null)
            FitToView();
        InvalidateVisual();
    }

    // ---- Projection ---------------------------------------------------------

    private IProjection BuildProjection(Vec2 offset)
    {
        var vm = ViewModel!;
        return vm.ViewMode == ViewMode.Isometric
            ? new IsometricProjection(vm.Zoom, offset)
            : new TopDownProjection(vm.Zoom, offset);
    }

    private IProjection CurrentProjection => BuildProjection(new Vec2(ViewModel!.PanX, ViewModel.PanY));

    private Vec2 ScreenToWorld(Point p) => CurrentProjection.ScreenToWorld(new Vec2(p.X, p.Y));

    /// <summary>Frames the whole plan within the current control size.</summary>
    public void FitToView()
    {
        var vm = ViewModel;
        if (vm == null || ActualWidth < 20 || ActualHeight < 20) return;

        var (min, max) = vm.Plan.GetBounds(1.0);
        double spanX = Math.Max(0.5, max.X - min.X);
        double spanY = Math.Max(0.5, max.Y - min.Y);

        // For iso the projected footprint is wider/taller; use a conservative factor.
        double factor = vm.ViewMode == ViewMode.Isometric ? 1.9 : 1.05;
        double zoomX = ActualWidth / (spanX * factor);
        double zoomY = ActualHeight / (spanY * factor);
        vm.Zoom = Math.Clamp(Math.Min(zoomX, zoomY), 8, 400);

        // Center the plan's world center in the viewport.
        var center = (min + max) * 0.5;
        var proj0 = BuildProjection(Vec2.Zero);
        var screenCenter = proj0.WorldToScreen(center);
        vm.PanX = ActualWidth / 2 - screenCenter.X;
        vm.PanY = ActualHeight / 2 - screenCenter.Y;

        _viewInitialized = true;
        InvalidateVisual();
    }

    // ---- Rendering ----------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Palette.Brush(Palette.CanvasColor), null, new Rect(0, 0, ActualWidth, ActualHeight));

        var vm = ViewModel;
        if (vm == null) return;

        if (!_viewInitialized) FitToView();

        // Traced-over floor-plan background (2D view only), under the scene.
        if (vm.ViewMode == ViewMode.TopDown2D && vm.BackgroundImage is { } bg)
        {
            var proj = CurrentProjection;
            var origin = vm.Plan.BackgroundOrigin;
            double mpp = vm.Plan.BackgroundMetresPerPixel;
            var tl = proj.WorldToScreen(origin);
            var br = proj.WorldToScreen(new Vec2(origin.X + bg.Width * mpp, origin.Y + bg.Height * mpp));
            var rect = new Rect(new Point(tl.X, tl.Y), new Point(br.X, br.Y));
            dc.PushOpacity(Math.Clamp(vm.Plan.BackgroundOpacity, 0, 1));
            dc.DrawImage(bg, rect);
            dc.Pop();
        }

        var ctx = new RenderContext
        {
            Plan = vm.Plan,
            Projection = CurrentProjection,
            ViewMode = vm.ViewMode,
            Coverage = vm.Coverage,
            ShowHeatmap = (vm.ShowHeatmap || vm.ShowRedundancy) && vm.Plan.Sensors.Exists(s => !s.IsSuggestion),
            ShowRedundancy = vm.ShowRedundancy,
            ShowCones = vm.ShowCones,
            ShowGrid = vm.ShowGrid,
            Selected = vm.SelectedElement,
            SelectedSet = vm.SelectedElements,
            SuggestionOpacity = vm.SuggestionOpacity,
            WallPreview = _wallStart is { } ws ? (ws, _previewEnd) : null
        };
        _renderer.Render(dc, ctx);

        if (_rectStart is { } rs)
            DrawRectPreview(dc, rs, _previewEnd);

        if (_mode == DragMode.Marquee)
            DrawMarquee(dc, _marqueeStart, _previewEnd);

        DrawFurnitureHandles(dc, vm);
    }

    // ---- Furniture transform handles (2D) ----------------------------------

    private (Point[] corners, Point rotate)? FurnitureHandles(MainViewModel vm)
    {
        if (vm.ViewMode != ViewMode.TopDown2D || vm.SelectedElement is not FurnitureItem f) return null;
        double rot = f.RotationDegrees * Math.PI / 180.0;
        double hw = f.Size.X / 2, hd = f.Size.Y / 2;
        var proj = CurrentProjection;

        Point Pw(double lx, double ly)
        {
            var s = proj.WorldToScreen(f.Position + new Vec2(lx, ly).Rotate(rot));
            return new Point(s.X, s.Y);
        }

        var corners = new[] { Pw(-hw, -hd), Pw(hw, -hd), Pw(hw, hd), Pw(-hw, hd) };
        var rs = proj.WorldToScreen(f.Position + new Vec2(0, -hd - 0.5).Rotate(rot));
        return (corners, new Point(rs.X, rs.Y));
    }

    private void DrawFurnitureHandles(DrawingContext dc, MainViewModel vm)
    {
        if (FurnitureHandles(vm) is not { } h) return;
        var accent = Palette.Brush(Palette.SelectionColor);
        var white = new Pen(Brushes.White, 1.5); white.Freeze();
        var line = new Pen(accent, 1.5); line.Freeze();

        var midTop = new Point((h.corners[0].X + h.corners[1].X) / 2, (h.corners[0].Y + h.corners[1].Y) / 2);
        dc.DrawLine(line, midTop, h.rotate);
        dc.DrawEllipse(Brushes.White, line, h.rotate, 6, 6);
        dc.DrawEllipse(accent, null, h.rotate, 3, 3);

        foreach (var c in h.corners)
            dc.DrawRectangle(accent, white, new Rect(c.X - 5, c.Y - 5, 10, 10));
    }

    private static double Dist(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void DrawMarquee(DrawingContext dc, Vec2 a, Vec2 b)
    {
        var proj = CurrentProjection;
        var corners = new[] { new Vec2(a.X, a.Y), new Vec2(b.X, a.Y), new Vec2(b.X, b.Y), new Vec2(a.X, b.Y) };
        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            var p0 = proj.WorldToScreen(corners[0]);
            g.BeginFigure(new Point(p0.X, p0.Y), true, true);
            for (int i = 1; i < 4; i++) { var p = proj.WorldToScreen(corners[i]); g.LineTo(new Point(p.X, p.Y), true, false); }
        }
        geom.Freeze();
        var fill = new SolidColorBrush(Palette.SelectionColor) { Opacity = 0.10 }; fill.Freeze();
        var pen = new Pen(Palette.Brush(Palette.SelectionColor), 1) { DashStyle = DashStyles.Dash }; pen.Freeze();
        dc.DrawGeometry(fill, pen, geom);
    }

    private void DrawRectPreview(DrawingContext dc, Vec2 a, Vec2 b)
    {
        var proj = CurrentProjection;
        var pen = new Pen(Palette.Brush(Palette.SelectionColor), 2) { DashStyle = DashStyles.Dash };
        pen.Freeze();
        var corners = new[]
        {
            new Vec2(a.X, a.Y), new Vec2(b.X, a.Y), new Vec2(b.X, b.Y), new Vec2(a.X, b.Y)
        };
        for (int i = 0; i < 4; i++)
        {
            var s0 = proj.WorldToScreen(corners[i]);
            var s1 = proj.WorldToScreen(corners[(i + 1) % 4]);
            dc.DrawLine(pen, new Point(s0.X, s0.Y), new Point(s1.X, s1.Y));
        }
    }

    // ---- Mouse: zoom & pan --------------------------------------------------

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        var cursor = e.GetPosition(this);
        var worldUnderCursor = ScreenToWorld(cursor);

        double factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
        vm.Zoom = Math.Clamp(vm.Zoom * factor, 8, 400);

        // Keep the world point under the cursor stationary.
        var proj0 = BuildProjection(Vec2.Zero);
        var s = proj0.WorldToScreen(worldUnderCursor);
        vm.PanX = cursor.X - s.X;
        vm.PanY = cursor.Y - s.Y;

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var vm = ViewModel;
        if (vm == null) return;

        _lastMouse = e.GetPosition(this);

        // Right / middle button always pans, regardless of the active tool.
        if (e.ChangedButton is MouseButton.Right or MouseButton.Middle)
        {
            _mode = DragMode.Panning;
            CaptureMouse();
            return;
        }

        if (e.ChangedButton != MouseButton.Left) return;

        // Reposition the traced-over background image (takes priority when enabled).
        if (vm is { MoveBackground: true, HasBackground: true })
        {
            _mode = DragMode.MovingBackground;
            _bgLastWorld = ScreenToWorld(_lastMouse);
            CaptureMouse();
            return;
        }

        // Grab a transform handle on the already-selected furniture item.
        if (vm.SelectedElement is FurnitureItem fsel && FurnitureHandles(vm) is { } h)
        {
            void BeginXform(DragMode mode)
            {
                _mode = mode;
                _xformItem = fsel;
                _xformStartSize = fsel.Size;
                _xformStartRot = fsel.RotationDegrees;
                CaptureMouse();
            }
            if (Dist(_lastMouse, h.rotate) <= 10) { BeginXform(DragMode.Rotating); return; }
            foreach (var c in h.corners)
                if (Dist(_lastMouse, c) <= 10) { BeginXform(DragMode.Resizing); return; }
        }

        var world = vm.SnapWorld(ScreenToWorld(_lastMouse));

        switch (vm.ActiveTool)
        {
            case ToolType.Select:
                HandleSelectPress(vm, _lastMouse);
                break;
            case ToolType.Sensor:
                vm.PlaceSensorAt(world);
                break;
            case ToolType.Furniture:
                vm.PlaceDefaultFurnitureAt(world);
                break;
            case ToolType.Wall:
                HandleWallPress(world);
                break;
            case ToolType.Room:
                _rectStart = world;
                _previewEnd = world;
                CaptureMouse();
                break;
            case ToolType.Door:
                vm.AddOpeningAt(vm.SnapWorld(ScreenToWorld(_lastMouse)), OpeningKind.Door);
                break;
            case ToolType.Window:
                vm.AddOpeningAt(vm.SnapWorld(ScreenToWorld(_lastMouse)), OpeningKind.Window);
                break;
        }
        InvalidateVisual();
    }

    private void HandleSelectPress(MainViewModel vm, Point mouse)
    {
        double tolerance = 8 / vm.Zoom; // 8px pick radius in world units
        var world = ScreenToWorld(mouse);
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        // Sensors render elevated (at mount height) in the isometric view, so a floor-level
        // inverse projection misses them. Pick them in screen space first, in either view.
        object? hit = SensorAtScreenPoint(vm, mouse) ?? HitTester.HitTest(vm.Plan, world, tolerance);

        if (hit == null)
        {
            if (!shift) vm.SelectElement(null);
            _mode = DragMode.Marquee;
            _marqueeStart = world;
            _previewEnd = world;
            CaptureMouse();
            return;
        }

        if (shift)
        {
            vm.ToggleSelect(hit); // add/remove without starting a drag
            return;
        }

        if (!vm.IsSelected(hit)) vm.SelectElement(hit); // keep an existing multi-selection for group drag

        // Layout lock: a wall/room can be selected (to edit or delete) but not dragged.
        if (!vm.CanMove(hit)) return;

        // Begin a (possibly group) move: capture every movable selected element's start position
        // (locked walls/rooms in a mixed selection stay put).
        _moveStarts.Clear();
        foreach (var el in vm.SelectedElements)
            if (vm.CanMove(el)) _moveStarts[el] = vm.GetElementPosition(el);
        if (_moveStarts.Count == 0) return;

        _grabbed = hit;
        _moveAnchor = world;
        _mode = DragMode.MovingElement;
        CaptureMouse();
    }

    /// <summary>Screen-space sensor pick: returns the sensor whose drawn icon is under the cursor.</summary>
    private Sensor? SensorAtScreenPoint(MainViewModel vm, Point mouse)
    {
        const double pickRadiusPx = 16; // generous target over the ~18px device housing
        var proj = CurrentProjection;
        bool iso = vm.ViewMode == ViewMode.Isometric;

        for (int i = vm.Plan.Sensors.Count - 1; i >= 0; i--)
        {
            var s = vm.Plan.Sensors[i];
            if (s.IsSuggestion) continue;
            var c = proj.WorldToScreen(s.Position, iso ? s.Height : 0);
            double dx = c.X - mouse.X, dy = c.Y - mouse.Y;
            if (dx * dx + dy * dy <= pickRadiusPx * pickRadiusPx) return s;
        }
        return null;
    }

    private void HandleWallPress(Vec2 world)
    {
        if (_wallStart is { } start)
        {
            ViewModel!.AddWall(start, world);
            _wallStart = world; // chain walls; press Escape to stop
            _previewEnd = world;
        }
        else
        {
            _wallStart = world;
            _previewEnd = world;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var vm = ViewModel;
        if (vm == null) return;

        var pos = e.GetPosition(this);

        if (_mode == DragMode.Panning)
        {
            vm.PanX += pos.X - _lastMouse.X;
            vm.PanY += pos.Y - _lastMouse.Y;
            _lastMouse = pos;
            InvalidateVisual();
            return;
        }

        if (_mode == DragMode.MovingBackground)
        {
            var world = ScreenToWorld(pos);
            vm.NudgeBackground(world - _bgLastWorld);
            _bgLastWorld = world;
            InvalidateVisual();
            return;
        }

        if (_mode == DragMode.Resizing && _xformItem != null)
        {
            var world = ScreenToWorld(pos);
            double rot = _xformItem.RotationDegrees * Math.PI / 180.0;
            var local = (world - _xformItem.Position).Rotate(-rot);
            vm.ResizeFurnitureLive(_xformItem, new Vec2(
                Math.Max(0.1, Math.Abs(local.X)) * 2, Math.Max(0.1, Math.Abs(local.Y)) * 2));
            InvalidateVisual();
            return;
        }

        if (_mode == DragMode.Rotating && _xformItem != null)
        {
            var dir = ScreenToWorld(pos) - _xformItem.Position;
            double deg = Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI + 90.0;
            vm.RotateFurnitureLive(_xformItem, deg);
            InvalidateVisual();
            return;
        }

        if (_mode == DragMode.MovingElement && _grabbed != null)
        {
            var rawDelta = ScreenToWorld(pos) - _moveAnchor;
            var grabbedStart = _moveStarts[_grabbed];
            var delta = vm.SnapWorld(grabbedStart + rawDelta) - grabbedStart; // snap the grabbed item; move group rigidly
            vm.MoveSelectionTo(_moveStarts, delta);
            InvalidateVisual();
            return;
        }

        if (_mode == DragMode.Marquee)
        {
            _previewEnd = ScreenToWorld(pos);
            InvalidateVisual();
            return;
        }

        if (_wallStart != null || _rectStart != null)
        {
            _previewEnd = vm.SnapWorld(ScreenToWorld(pos));
            InvalidateVisual();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        var vm = ViewModel;
        if (vm == null) return;

        if (_mode == DragMode.MovingElement && _grabbed != null)
        {
            var ends = new Dictionary<object, Vec2>();
            bool moved = false;
            foreach (var el in _moveStarts.Keys)
            {
                var end = vm.GetElementPosition(el);
                ends[el] = end;
                if (end.DistanceTo(_moveStarts[el]) > 1e-6) moved = true;
            }
            if (moved) vm.RecordSelectionMove(_moveStarts, ends);
            _grabbed = null;
        }

        if (_mode == DragMode.Marquee)
            vm.SelectInRect(_marqueeStart, _previewEnd);

        if ((_mode == DragMode.Resizing || _mode == DragMode.Rotating) && _xformItem != null)
        {
            vm.RecordFurnitureTransform(_xformItem, _xformStartSize, _xformStartRot,
                _xformItem.Size, _xformItem.RotationDegrees);
            _xformItem = null;
        }

        if (_rectStart is { } rs && e.ChangedButton == MouseButton.Left)
        {
            var end = vm.SnapWorld(ScreenToWorld(e.GetPosition(this)));
            if (Math.Abs(end.X - rs.X) > 0.3 && Math.Abs(end.Y - rs.Y) > 0.3)
                vm.AddRoomRect(rs, end);
            _rectStart = null;
        }

        _mode = DragMode.None;
        _grabbed = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var vm = ViewModel;
        if (vm == null) return;

        switch (e.Key)
        {
            case Key.Escape:
                _wallStart = null;
                _rectStart = null;
                vm.SelectElement(null);
                InvalidateVisual();
                break;
            case Key.Delete:
            case Key.Back:
                vm.DeleteSelectedCommand.Execute(null);
                break;
            case Key.D when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                vm.DuplicateSelectionCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // ---- Drag & drop from the library --------------------------------------

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        e.Effects = e.Data.GetDataPresent(FurnitureDataFormat) || e.Data.GetDataPresent(SensorDataFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        var vm = ViewModel;
        if (vm == null) return;

        var world = vm.SnapWorld(ScreenToWorld(e.GetPosition(this)));

        if (e.Data.GetData(FurnitureDataFormat) is FurnitureTemplate ft)
            vm.PlaceFurnitureTemplateAt(ft, world);
        else if (e.Data.GetData(SensorDataFormat) is SensorTemplate st)
            vm.PlaceSensorTemplateAt(st, world);

        InvalidateVisual();
        e.Handled = true;
    }
}
