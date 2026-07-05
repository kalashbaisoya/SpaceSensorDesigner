using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpaceSensorDesigner.App.Services;
using SpaceSensorDesigner.App.ViewModels;
using SpaceSensorDesigner.Core.Catalog;
using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Export;
using SpaceSensorDesigner.Core.Geometry;
using SpaceSensorDesigner.Core.Models;
using SpaceSensorDesigner.Core.Optimization;
using SpaceSensorDesigner.Core.Serialization;
using SpaceSensorDesigner.Core.Undo;

namespace SpaceSensorDesigner.App.ViewModels;

/// <summary>
/// The single DataContext for the main window. Holds the document, drives the canvas via the
/// <c>DesignSurface</c>, and hosts every command in the toolbar / palette / status bar.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly FileDialogService _files = new();
    private readonly UndoRedoManager _undo = new();
    private readonly DispatcherTimer _coverageTimer;
    private readonly DispatcherTimer _optimizeTimer;
    private readonly DispatcherTimer _autosaveTimer;
    private readonly RecentFilesService? _recentFiles;
    private readonly LiveTelemetryService _telemetry = new();
    private AppSettings _settings;

    /// <summary>Set by the window: renders the current floor view to a bitmap (for PNG / report export).</summary>
    public Func<System.Windows.Media.Imaging.BitmapSource?>? CaptureFloorImage;

    private string? _currentPath;
    private readonly Queue<Sensor> _pendingSuggestions = new();

    /// <summary>Raised when the canvas should reframe the whole plan.</summary>
    public event Action? RequestFitToView;

    /// <summary>Raised when the canvas should repaint (for model mutations that are not observable properties).</summary>
    public event Action? RequestInvalidate;

    /// <summary>Raised when the user asks to return to the home / landing screen.</summary>
    public event Action? RequestGoHome;

    /// <summary>Convenience ctor for the sample document with no shell services (used by design-time / previews).</summary>
    public MainViewModel() : this(DesignProject.CreateSample(), null, null, null) { }

    /// <summary>
    /// Builds the editor around a loaded <paramref name="project"/>. When launched from the home
    /// screen it also receives the recent-files list and the app settings so both stay in sync.
    /// </summary>
    public MainViewModel(DesignProject project, string? path, RecentFilesService? recentFiles, AppSettings? settings)
    {
        _recentFiles = recentFiles;
        _settings = settings ?? AppSettings.Default;
        _project = project;
        _currentPath = path;
        RebuildFloorNames();

        FurnitureLibrary = new ObservableCollection<LibraryItemViewModel>();
        foreach (var t in LibraryCatalog.Furniture) FurnitureLibrary.Add(LibraryItemViewModel.FromFurniture(t));
        SensorLibrary = new ObservableCollection<LibraryItemViewModel>();
        foreach (var t in LibraryCatalog.Sensors) SensorLibrary.Add(LibraryItemViewModel.FromSensor(t));

        _coverageTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _coverageTimer.Tick += (_, _) => { _coverageTimer.Stop(); RecomputeCoverageNow(); };

        _optimizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _optimizeTimer.Tick += OnOptimizeTick;

        _autosaveTimer = new DispatcherTimer();
        _autosaveTimer.Tick += OnAutosaveTick;

        _undo.Changed += (_, _) => { RefreshUndoState(); HasUnsavedChanges = true; };

        ApplySettings(_settings);
        LoadBackgroundFromPlan(); // restore a traced-over floor plan when opening a saved project
        RecomputeCoverageNow();

        _telemetry.Updated += OnTelemetryTick;
        _telemetry.Start();

        if (path != null) _recentFiles?.Add(path, _project.Name);
        HasUnsavedChanges = false;
        StatusText = "Ready — draw walls, drag furniture and sensors, then Optimize Coverage.";
    }

    /// <summary>The live telemetry feed (simulated) for the selected sensor's readouts.</summary>
    internal LiveTelemetryService Telemetry => _telemetry;

    private void OnTelemetryTick() => (SelectedProperties as SensorPropertiesViewModel)?.RefreshLive();

    /// <summary>The recent-files list (so the window can rebuild the home screen when going back).</summary>
    internal RecentFilesService? RecentFiles => _recentFiles;

    /// <summary>The shared app settings instance.</summary>
    internal AppSettings Settings => _settings;

    /// <summary>True when the document has edits that have not been saved to disk.</summary>
    [ObservableProperty] private bool _hasUnsavedChanges;

    /// <summary>Applies user preferences to the live editor state (view, overlays, snap, autosave).</summary>
    public void ApplySettings(AppSettings s)
    {
        _settings = s;
        ShowGrid = s.ShowGridOnStart;
        ShowHeatmap = s.ShowHeatmapOnStart;
        ViewMode = s.DefaultViewIsometric ? ViewMode.Isometric : ViewMode.TopDown2D;
        ApplySnapToFloors();
        ConfigureAutosave();
    }

    private void ApplySnapToFloors()
    {
        foreach (var f in _project.Floors) f.SnapSize = _settings.SnapSizeMeters;
    }

    private void ConfigureAutosave()
    {
        _autosaveTimer.Stop();
        if (_settings.AutosaveMinutes > 0)
        {
            _autosaveTimer.Interval = TimeSpan.FromMinutes(_settings.AutosaveMinutes);
            _autosaveTimer.Start();
        }
    }

    private void OnAutosaveTick(object? sender, EventArgs e)
    {
        if (_currentPath == null || !HasUnsavedChanges) return;
        Persist(_currentPath);
        StatusText = $"Auto-saved {System.IO.Path.GetFileName(_currentPath)} at {DateTime.Now:HH:mm}.";
    }

    [RelayCommand]
    private void GoHome() => RequestGoHome?.Invoke();

    // ---- Document (a project of one or more floors) ------------------------

    private DesignProject _project;

    /// <summary>The floor currently being edited.</summary>
    public FloorPlan Plan => _project.Floors[Math.Clamp(ActiveFloorIndex, 0, _project.Floors.Count - 1)];

    [ObservableProperty] private int _activeFloorIndex;

    /// <summary>Display names of every floor, kept in sync with the project (for the floor switcher).</summary>
    public ObservableCollection<string> FloorNames { get; } = new();

    public string ActiveFloorName
    {
        get => Plan.Name;
        set
        {
            if (Plan.Name == value) return;
            Plan.Name = value;
            if (ActiveFloorIndex >= 0 && ActiveFloorIndex < FloorNames.Count) FloorNames[ActiveFloorIndex] = value;
            OnPropertyChanged();
        }
    }

    public bool CanRemoveFloor => _project.Floors.Count > 1;

    public ObservableCollection<LibraryItemViewModel> FurnitureLibrary { get; }
    public ObservableCollection<LibraryItemViewModel> SensorLibrary { get; }

    /// <summary>Per-room coverage breakdown, refreshed on every recompute.</summary>
    public ObservableCollection<RoomCoverage> RoomCoverages { get; } = new();

    private void RebuildFloorNames()
    {
        FloorNames.Clear();
        foreach (var f in _project.Floors) FloorNames.Add(f.Name);
    }

    partial void OnActiveFloorIndexChanged(int value)
    {
        // Switching floors: reset per-floor UI state. Undo history is per-session and cleared
        // so it never applies an action to the wrong floor.
        _undo.Clear();
        SelectElement(null);
        LoadBackgroundFromPlan();
        RecomputeCoverageNow();
        OnPropertyChanged(nameof(ActiveFloorName));
        OnPropertyChanged(nameof(Plan));
        RequestFitToView?.Invoke();
    }

    [RelayCommand]
    private void AddFloor()
    {
        var floor = new FloorPlan { Name = $"Floor {_project.Floors.Count + 1}", SnapSize = _settings.SnapSizeMeters };
        _project.Floors.Add(floor);
        FloorNames.Add(floor.Name);
        OnPropertyChanged(nameof(CanRemoveFloor));
        RemoveFloorCommand.NotifyCanExecuteChanged();
        ActiveFloorIndex = _project.Floors.Count - 1;
        StatusText = $"Added {floor.Name}.";
    }

    [RelayCommand]
    private void AddTemplateFloor(string key)
    {
        var floor = PlanTemplates.Create(key);
        floor.SnapSize = _settings.SnapSizeMeters;
        _project.Floors.Add(floor);
        FloorNames.Add(floor.Name);
        OnPropertyChanged(nameof(CanRemoveFloor));
        RemoveFloorCommand.NotifyCanExecuteChanged();
        ActiveFloorIndex = _project.Floors.Count - 1;
        StatusText = $"Added a {PlanTemplates.DisplayName(key)} floor.";
    }

    [RelayCommand]
    private void ImportDxf()
    {
        var path = _files.AskOpenDxfPath();
        if (path == null) return;
        try
        {
            var walls = DxfImporter.ImportWalls(path);
            if (walls.Count == 0)
            {
                StatusText = "No LINE / LWPOLYLINE entities found in that DXF.";
                return;
            }
            ExecuteEdit(new DelegateAction($"Import DXF (+{walls.Count} walls)",
                () => Plan.Walls.AddRange(walls),
                () => { foreach (var w in walls) Plan.Walls.Remove(w); }));
            RequestFitToView?.Invoke();
            StatusText = $"Imported {walls.Count} walls from {System.IO.Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            StatusText = $"DXF import failed: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveFloor))]
    private void RemoveFloor()
    {
        if (_project.Floors.Count <= 1) return;
        int idx = ActiveFloorIndex;
        _project.Floors.RemoveAt(idx);
        FloorNames.RemoveAt(idx);
        OnPropertyChanged(nameof(CanRemoveFloor));
        RemoveFloorCommand.NotifyCanExecuteChanged();
        ActiveFloorIndex = Math.Clamp(idx, 0, _project.Floors.Count - 1);
        // Force a refresh even if the clamped index equals the old value.
        OnActiveFloorIndexChanged(ActiveFloorIndex);
    }

    // ---- Observable UI state -----------------------------------------------

    [ObservableProperty] private ToolType _activeTool = ToolType.Select;
    [ObservableProperty] private ViewMode _viewMode = ViewMode.TopDown2D;
    [ObservableProperty] private double _zoom = 60;
    [ObservableProperty] private double _panX;
    [ObservableProperty] private double _panY;
    [ObservableProperty] private bool _showHeatmap = true;
    [ObservableProperty] private bool _showCones = true;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private bool _showRedundancy;
    [ObservableProperty] private int _redundantCells;

    partial void OnShowRedundancyChanged(bool value) => RequestInvalidate?.Invoke();

    /// <summary>When true, walls and rooms can't be dragged (prevents accidentally moving the floor plan).
    /// Furniture and sensors still move freely.</summary>
    [ObservableProperty] private bool _layoutLocked = true;

    partial void OnLayoutLockedChanged(bool value)
        => StatusText = value
            ? "Layout locked — walls & rooms are fixed. Furniture and sensors still move freely."
            : "Layout unlocked — walls & rooms can be dragged.";

    /// <summary>Whether an element may be moved by dragging, honouring the layout lock (walls &amp; rooms).</summary>
    public bool CanMove(object element) => element switch
    {
        Wall or Room => !LayoutLocked,
        _ => true
    };
    [ObservableProperty] private object? _selectedElement;
    [ObservableProperty] private PropertiesViewModelBase? _selectedProperties;
    [ObservableProperty] private CoverageGrid? _coverage;
    [ObservableProperty] private double _coveredPercent;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private double _suggestionOpacity = 1.0;
    [ObservableProperty] private bool _isOptimizing;

    // Traced-over floor-plan background (Phase 4 import)
    [ObservableProperty] private System.Windows.Media.ImageSource? _backgroundImage;
    [ObservableProperty] private bool _moveBackground;

    public bool HasBackground => BackgroundImage != null;

    public double BackgroundOpacity
    {
        get => Plan.BackgroundOpacity;
        set { if (System.Math.Abs(Plan.BackgroundOpacity - value) > 1e-4) { Plan.BackgroundOpacity = value; OnPropertyChanged(); RequestInvalidate?.Invoke(); } }
    }

    public double BackgroundScalePercent
    {
        get => Plan.BackgroundMetresPerPixel * 1000; // mm per pixel, friendlier slider units
        set { var v = System.Math.Clamp(value, 1, 60); if (System.Math.Abs(BackgroundScalePercent - v) > 1e-4) { Plan.BackgroundMetresPerPixel = v / 1000.0; OnPropertyChanged(); RequestInvalidate?.Invoke(); } }
    }

    partial void OnBackgroundImageChanged(System.Windows.Media.ImageSource? value)
        => OnPropertyChanged(nameof(HasBackground));

    public bool IsIsometric => ViewMode == ViewMode.Isometric;

    partial void OnViewModeChanged(ViewMode value)
    {
        OnPropertyChanged(nameof(IsIsometric));
        RequestFitToView?.Invoke();
    }

    partial void OnActiveToolChanged(ToolType value)
    {
        StatusText = value switch
        {
            ToolType.Wall => "Wall tool — click to start, click again to place segments. Esc to finish.",
            ToolType.Room => "Room tool — drag a rectangle to create a room.",
            ToolType.Sensor => "Sensor tool — click to drop a sensor, or drag one from the library.",
            ToolType.Furniture => "Furniture tool — click to drop, or drag items from the library.",
            ToolType.Select => "Select tool — click to select, drag to move, Delete to remove.",
            ToolType.Door or ToolType.Window => $"{value} placement is a Phase 1 stub.",
            _ => $"{value} tool"
        };
    }

    partial void OnSelectedElementChanged(object? value)
    {
        SelectedProperties = value switch
        {
            Sensor s => new SensorPropertiesViewModel(s, Plan, OnPropertyEdited, _telemetry),
            FurnitureItem f => new FurniturePropertiesViewModel(f, OnPropertyEdited),
            Room r => new RoomPropertiesViewModel(r, OnPropertyEdited),
            Opening o => new OpeningPropertiesViewModel(o, OnPropertyEdited),
            _ => null
        };
    }

    private void OnPropertyEdited()
    {
        ScheduleCoverageRecompute();
        RequestInvalidate?.Invoke();
    }

    // ---- Undo / Redo state -------------------------------------------------

    public bool CanUndo => _undo.CanUndo;
    public bool CanRedo => _undo.CanRedo;
    public string UndoLabel => _undo.NextUndoLabel is { } l ? $"Undo {l}" : "Undo";
    public string RedoLabel => _undo.NextRedoLabel is { } l ? $"Redo {l}" : "Redo";

    private void RefreshUndoState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoLabel));
        OnPropertyChanged(nameof(RedoLabel));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // ---- Coverage ----------------------------------------------------------

    public void ScheduleCoverageRecompute()
    {
        _coverageTimer.Stop();
        _coverageTimer.Start();
    }

    private void RecomputeCoverageNow()
    {
        var result = CoverageCalculator.Compute(Plan);
        Coverage = result.Grid;
        CoveredPercent = result.Summary.CoveredPercent;
        RedundantCells = result.Summary.RedundantCells;

        RoomCoverages.Clear();
        foreach (var r in result.Rooms)
            RoomCoverages.Add(r);

        StatusText = $"Coverage {result.Summary.CoveredPercent:0.#}%  ·  {result.Summary.CoveredCells} covered / {result.Summary.PartialCells} partial / {result.Summary.UncoveredCells} uncovered cells";
    }

    // ---- Editing API used by DesignSurface ---------------------------------

    public Vec2 SnapWorld(Vec2 world) => GeometryUtils.Snap(world, Plan.SnapSize);

    /// <summary>The full selection set (for multi-select); the properties panel edits it only when a single item is selected.</summary>
    public ObservableCollection<object> SelectedElements { get; } = new();

    public bool IsSelected(object element) => SelectedElements.Contains(element);

    /// <summary>Single-selects an element (clears any existing multi-selection).</summary>
    public void SelectElement(object? element)
    {
        SelectedElements.Clear();
        if (element != null) SelectedElements.Add(element);
        SelectedElement = element;
        OnPropertyChanged(nameof(SelectionCount));
        RequestInvalidate?.Invoke();
    }

    /// <summary>Shift-click: toggles an element in/out of the selection.</summary>
    public void ToggleSelect(object element)
    {
        if (!SelectedElements.Remove(element)) SelectedElements.Add(element);
        SelectedElement = SelectedElements.Count == 1 ? SelectedElements[0] : null;
        OnPropertyChanged(nameof(SelectionCount));
        RequestInvalidate?.Invoke();
    }

    /// <summary>Marquee: selects every element whose reference point lies inside the rectangle.</summary>
    public void SelectInRect(Vec2 a, Vec2 b)
    {
        double minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
        double minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
        bool Inside(Vec2 p) => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY;

        SelectedElements.Clear();
        foreach (var s in Plan.Sensors) if (!s.IsSuggestion && Inside(s.Position)) SelectedElements.Add(s);
        foreach (var f in Plan.Furniture) if (Inside(f.Position)) SelectedElements.Add(f);
        foreach (var o in Plan.Openings) if (Inside(o.Center)) SelectedElements.Add(o);
        foreach (var w in Plan.Walls) if (Inside((w.Start + w.End) * 0.5)) SelectedElements.Add(w);

        SelectedElement = SelectedElements.Count == 1 ? SelectedElements[0] : null;
        OnPropertyChanged(nameof(SelectionCount));
        RequestInvalidate?.Invoke();
    }

    public int SelectionCount => SelectedElements.Count;

    // ---- Group move (undoable) ---------------------------------------------

    public void SetElementReference(object element, Vec2 refPosition)
        => TranslateElement(element, refPosition - GetElementPosition(element));

    public void MoveSelectionTo(IReadOnlyDictionary<object, Vec2> startPositions, Vec2 delta)
    {
        foreach (var kv in startPositions)
            SetElementReference(kv.Key, kv.Value + delta);
        ScheduleCoverageRecompute();
    }

    public void RecordSelectionMove(IReadOnlyDictionary<object, Vec2> starts, IReadOnlyDictionary<object, Vec2> ends)
    {
        _undo.Push(new DelegateAction(starts.Count > 1 ? "Move Selection" : "Move",
            () => { foreach (var kv in ends) SetElementReference(kv.Key, kv.Value); },
            () => { foreach (var kv in starts) SetElementReference(kv.Key, kv.Value); }));
        RecomputeCoverageNow();
    }

    [RelayCommand]
    private void DuplicateSelection()
    {
        var clones = new List<object>();
        foreach (var el in SelectedElements)
        {
            switch (el)
            {
                case Sensor s:
                    var cs = s.Clone(); cs.Id = Guid.NewGuid(); cs.Position += new Vec2(0.4, 0.4); cs.IsSuggestion = false; clones.Add(cs); break;
                case FurnitureItem f:
                    clones.Add(new FurnitureItem { Type = f.Type, Name = f.Name, Position = f.Position + new Vec2(0.4, 0.4), Size = f.Size, RotationDegrees = f.RotationDegrees }); break;
            }
        }
        if (clones.Count == 0) return;

        ExecuteEdit(new DelegateAction($"Duplicate ({clones.Count})",
            () => AddClones(clones), () => RemoveClones(clones)));

        SelectedElements.Clear();
        foreach (var c in clones) SelectedElements.Add(c);
        SelectedElement = clones.Count == 1 ? clones[0] : null;
        OnPropertyChanged(nameof(SelectionCount));
    }

    private void AddClones(List<object> clones)
    {
        foreach (var c in clones)
        {
            if (c is Sensor s) Plan.Sensors.Add(s);
            else if (c is FurnitureItem f) Plan.Furniture.Add(f);
        }
    }

    private void RemoveClones(List<object> clones)
    {
        foreach (var c in clones)
        {
            if (c is Sensor s) Plan.Sensors.Remove(s);
            else if (c is FurnitureItem f) Plan.Furniture.Remove(f);
        }
    }

    public Vec2 GetElementPosition(object element) => element switch
    {
        Sensor s => s.Position,
        FurnitureItem f => f.Position,
        Wall w => (w.Start + w.End) * 0.5,
        Room r => r.Centroid,
        Opening o => o.Center,
        _ => Vec2.Zero
    };

    public void MoveElementLive(object element, Vec2 newReference)
    {
        var delta = newReference - GetElementPosition(element);
        TranslateElement(element, delta);
        ScheduleCoverageRecompute();
    }

    private static void TranslateElement(object element, Vec2 delta)
    {
        switch (element)
        {
            case Sensor s: s.Position += delta; break;
            case FurnitureItem f: f.Position += delta; break;
            case Wall w: w.Start += delta; w.End += delta; break;
            case Opening o: o.Center += delta; break;
            case Room r:
                for (int i = 0; i < r.Polygon.Count; i++) r.Polygon[i] += delta;
                break;
        }
    }

    public void ResizeFurnitureLive(FurnitureItem f, Vec2 newSize)
    {
        f.Size = newSize;
        ScheduleCoverageRecompute();
    }

    public void RotateFurnitureLive(FurnitureItem f, double degrees)
    {
        f.RotationDegrees = degrees;
        ScheduleCoverageRecompute();
    }

    public void RecordFurnitureTransform(FurnitureItem f, Vec2 oldSize, double oldRot, Vec2 newSize, double newRot)
    {
        _undo.Push(new DelegateAction("Resize / Rotate",
            () => { f.Size = newSize; f.RotationDegrees = newRot; },
            () => { f.Size = oldSize; f.RotationDegrees = oldRot; }));
        RecomputeCoverageNow();
        // refresh the properties panel width/depth/rotation readouts
        if (ReferenceEquals(SelectedElement, f)) OnSelectedElementChanged(f);
    }

    public void RecordMove(object element, Vec2 oldReference, Vec2 newReference)
    {
        var delta = newReference - oldReference; // already applied to the model
        _undo.Push(new DelegateAction("Move",
            () => TranslateElement(element, delta),
            () => TranslateElement(element, -delta)));
        RecomputeCoverageNow();
    }

    public void PlaceSensorAt(Vec2 world)
        => PlaceSensorTemplateAt(LibraryCatalog.Sensors[0], world);

    public void PlaceSensorTemplateAt(SensorTemplate template, Vec2 world)
    {
        var sensor = LibraryCatalog.CreateSensor(template, world);
        if (_settings.DefaultSensorHeightMeters > 0) sensor.Height = _settings.DefaultSensorHeightMeters;
        ExecuteEdit(new DelegateAction($"Add {template.DisplayName}",
            () => Plan.Sensors.Add(sensor),
            () => Plan.Sensors.Remove(sensor)));
        SelectElement(sensor);
    }

    public void PlaceDefaultFurnitureAt(Vec2 world)
        => PlaceFurnitureTemplateAt(LibraryCatalog.Furniture[1], world); // Table

    public void PlaceFurnitureTemplateAt(FurnitureTemplate template, Vec2 world)
    {
        var item = LibraryCatalog.CreateFurniture(template, world);
        ExecuteEdit(new DelegateAction($"Add {template.DisplayName}",
            () => Plan.Furniture.Add(item),
            () => Plan.Furniture.Remove(item)));
        SelectElement(item);
    }

    public void AddWall(Vec2 a, Vec2 b)
    {
        if (a.DistanceTo(b) < 1e-3) return;
        var wall = new Wall { Start = a, End = b };
        ExecuteEdit(new DelegateAction("Add Wall",
            () => Plan.Walls.Add(wall),
            () => Plan.Walls.Remove(wall)));
    }

    /// <summary>Places a door/window on the wall nearest the click, snapped onto its centre line.</summary>
    public void AddOpeningAt(Vec2 world, OpeningKind kind)
    {
        Wall? best = null;
        double bestDist = double.MaxValue;
        var bestPoint = Vec2.Zero;
        foreach (var w in Plan.Walls)
        {
            var p = GeometryUtils.ClosestPointOnSegment(world, w.Start, w.End);
            double d = p.DistanceTo(world);
            if (d < bestDist) { bestDist = d; best = w; bestPoint = p; }
        }

        if (best == null || bestDist > 0.7)
        {
            StatusText = $"Click closer to a wall to place a {kind}.";
            return;
        }

        var op = new Opening
        {
            WallId = best.Id,
            Center = bestPoint,
            Kind = kind,
            Width = kind == OpeningKind.Door ? 0.9 : 1.2
        };
        ExecuteEdit(new DelegateAction($"Add {kind}",
            () => Plan.Openings.Add(op),
            () => Plan.Openings.Remove(op)));
        SelectElement(op);
    }

    public void AddRoomRect(Vec2 a, Vec2 b)
    {
        var room = new Room
        {
            Name = "Room",
            Type = RoomType.Other,
            Polygon =
            {
                new Vec2(a.X, a.Y), new Vec2(b.X, a.Y), new Vec2(b.X, b.Y), new Vec2(a.X, b.Y)
            }
        };
        ExecuteEdit(new DelegateAction("Add Room",
            () => Plan.Rooms.Add(room),
            () => Plan.Rooms.Remove(room)));
        SelectElement(room);
    }

    private void ExecuteEdit(IUndoableAction action)
    {
        _undo.Execute(action);
        RecomputeCoverageNow();
        RequestInvalidate?.Invoke();
    }

    // ---- Commands ----------------------------------------------------------

    [RelayCommand]
    private void SelectTool(ToolType tool) => ActiveTool = tool;

    [RelayCommand]
    private void SetViewMode(ViewMode mode) => ViewMode = mode;

    [RelayCommand]
    private void ToggleView() => ViewMode = ViewMode == ViewMode.TopDown2D ? ViewMode.Isometric : ViewMode.TopDown2D;

    [RelayCommand]
    private void ZoomIn() { Zoom = Math.Clamp(Zoom * 1.15, 8, 400); RequestInvalidate?.Invoke(); }

    [RelayCommand]
    private void ZoomOut() { Zoom = Math.Clamp(Zoom / 1.15, 8, 400); RequestInvalidate?.Invoke(); }

    [RelayCommand]
    private void FitToView() => RequestFitToView?.Invoke();

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() { _undo.Undo(); AfterUndoRedo(); }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() { _undo.Redo(); AfterUndoRedo(); }

    private void AfterUndoRedo()
    {
        SelectElement(null);
        RecomputeCoverageNow();
        RequestInvalidate?.Invoke();
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedElements.Count == 0) return;
        var items = new List<object>(SelectedElements);

        SelectElement(null);
        ExecuteEdit(new DelegateAction(items.Count > 1 ? $"Delete ({items.Count})" : "Delete",
            () => { foreach (var e in items) RemoveElement(e); },
            () => { foreach (var e in items) AddElement(e); }));
    }

    private void RemoveElement(object e)
    {
        switch (e)
        {
            case Sensor s: Plan.Sensors.Remove(s); break;
            case FurnitureItem f: Plan.Furniture.Remove(f); break;
            case Wall w: Plan.Walls.Remove(w); break;
            case Room r: Plan.Rooms.Remove(r); break;
            case Opening o: Plan.Openings.Remove(o); break;
        }
    }

    private void AddElement(object e)
    {
        switch (e)
        {
            case Sensor s: Plan.Sensors.Add(s); break;
            case FurnitureItem f: Plan.Furniture.Add(f); break;
            case Wall w: Plan.Walls.Add(w); break;
            case Room r: Plan.Rooms.Add(r); break;
            case Opening o: Plan.Openings.Add(o); break;
        }
    }

    [RelayCommand]
    private void New()
    {
        _project = DesignProject.CreateSample();
        _currentPath = null;
        _undo.Clear();
        ApplySnapToFloors();
        RebuildFloorNames();
        SelectElement(null);
        OnPropertyChanged(nameof(CanRemoveFloor));
        RemoveFloorCommand.NotifyCanExecuteChanged();
        ActiveFloorIndex = 0;
        OnActiveFloorIndexChanged(0);
        HasUnsavedChanges = false;
    }

    [RelayCommand]
    private void Open()
    {
        var path = _files.AskOpenPath();
        if (path == null) return;
        try
        {
            _project = ProjectSerializer.Load(path);
            _currentPath = path;
            _undo.Clear();
            ApplySnapToFloors();
            RebuildFloorNames();
            SelectElement(null);
            OnPropertyChanged(nameof(CanRemoveFloor));
            RemoveFloorCommand.NotifyCanExecuteChanged();
            ActiveFloorIndex = 0;
            OnActiveFloorIndexChanged(0);
            _recentFiles?.Add(path, _project.Name);
            HasUnsavedChanges = false;
            StatusText = $"Opened {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Save()
    {
        var path = _currentPath ?? _files.AskSavePath(_project.Name);
        if (path == null) return;
        Persist(path);
    }

    [RelayCommand]
    private void SaveAs()
    {
        var path = _files.AskSavePath(_project.Name);
        if (path == null) return;
        Persist(path);
    }

    private void Persist(string path)
    {
        try
        {
            ProjectSerializer.Save(_project, path);
            _currentPath = path;
            _recentFiles?.Add(path, _project.Name);
            HasUnsavedChanges = false;
            StatusText = $"Saved {path} ({_project.Floors.Count} floor(s))";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to save: {ex.Message}";
        }
    }

    // ---- Export & reporting (Phase 5) --------------------------------------

    [RelayCommand]
    private void ExportReport()
    {
        try
        {
            var report = CoverageReport.Build(_project);
            string? b64 = null;
            if (CaptureFloorImage?.Invoke() is { } img) b64 = PngBase64(img);
            var html = CoverageReport.ToHtml(report, b64, $"{Plan.Name} · {(IsIsometric ? "isometric" : "top-down")} view");

            var path = _files.AskSaveExport(_project.Name + " report", "HTML report (*.html)|*.html", "html");
            if (path == null) return;
            System.IO.File.WriteAllText(path, html);
            StatusText = $"Report saved: {System.IO.Path.GetFileName(path)} — opening…";
            TryOpen(path);
        }
        catch (Exception ex) { StatusText = $"Report export failed: {ex.Message}"; }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        try
        {
            var csv = CoverageReport.ToCsv(CoverageReport.Build(_project));
            var path = _files.AskSaveExport(_project.Name + " sensors", "CSV (*.csv)|*.csv", "csv");
            if (path == null) return;
            System.IO.File.WriteAllText(path, csv);
            StatusText = $"Sensor schedule exported: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusText = $"CSV export failed: {ex.Message}"; }
    }

    [RelayCommand]
    private void ExportPng()
    {
        try
        {
            if (CaptureFloorImage?.Invoke() is not { } img) { StatusText = "Nothing to export."; return; }
            var path = _files.AskSaveExport(Plan.Name, "PNG image (*.png)|*.png", "png");
            if (path == null) return;
            using var fs = System.IO.File.Create(path);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(img));
            enc.Save(fs);
            StatusText = $"Floor image exported: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusText = $"Image export failed: {ex.Message}"; }
    }

    private static string PngBase64(System.Windows.Media.Imaging.BitmapSource img)
    {
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(img));
        using var ms = new System.IO.MemoryStream();
        enc.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static void TryOpen(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { /* leave the saved file on disk */ }
    }

    // ---- Background floor-plan image (Phase 4 import) ----------------------

    [RelayCommand]
    private void ImportBackground()
    {
        var path = _files.AskOpenImagePath();
        if (path == null) return;
        try
        {
            ApplyBackground(path, LoadBitmap(path));
            StatusText = "Floor plan imported — detecting rooms…";
            if (Plan.Rooms.Count == 0) RunRoomDetection(showDialogOnEmpty: true);
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to import image: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ImportPdf()
    {
        var pdf = _files.AskOpenPdfPath();
        if (pdf == null) return;
        try
        {
            int pages = PdfImportService.GetPageCount(pdf);
            var img = PdfImportService.RenderPage(pdf, 0);

            // Persist the rendered page as a PNG so the traced background survives save/reload.
            var cacheDir = System.IO.Path.Combine(AppPaths.DataFolder, "plan-cache");
            System.IO.Directory.CreateDirectory(cacheDir);
            var pngPath = System.IO.Path.Combine(cacheDir,
                $"{System.IO.Path.GetFileNameWithoutExtension(pdf)}-p1-{Guid.NewGuid():N}.png");
            PdfImportService.SavePng(img, pngPath);

            ApplyBackground(pngPath, img);
            StatusText = pages > 1
                ? $"Imported page 1 of {pages} from {System.IO.Path.GetFileName(pdf)} — detecting rooms…"
                : $"Imported {System.IO.Path.GetFileName(pdf)} — detecting rooms…";
            if (Plan.Rooms.Count == 0) RunRoomDetection(showDialogOnEmpty: true);
        }
        catch (Exception ex)
        {
            StatusText = $"PDF import failed: {ex.Message}";
        }
    }

    /// <summary>Shared setup for a traced-over background from any image source (PNG/JPG or a rendered PDF page).</summary>
    private void ApplyBackground(string path, System.Windows.Media.ImageSource img)
    {
        Plan.BackgroundImagePath = path;
        BackgroundImage = img;

        // Default scale: make the image about 8 m wide, centred on the plan bounds.
        if (img.Width > 0)
            Plan.BackgroundMetresPerPixel = 8.0 / img.Width;
        var (min, max) = Plan.GetBounds();
        var center = (min + max) * 0.5;
        Plan.BackgroundOrigin = new Vec2(
            center.X - img.Width * Plan.BackgroundMetresPerPixel / 2,
            center.Y - img.Height * Plan.BackgroundMetresPerPixel / 2);

        OnPropertyChanged(nameof(BackgroundOpacity));
        OnPropertyChanged(nameof(BackgroundScalePercent));
        ViewMode = ViewMode.TopDown2D;
        RequestFitToView?.Invoke();
    }

    [RelayCommand]
    private void RemoveBackground()
    {
        Plan.BackgroundImagePath = null;
        BackgroundImage = null;
        MoveBackground = false;
        RequestInvalidate?.Invoke();
    }

    /// <summary>Auto-detects rooms from the traced-over floor-plan image and adds them (undoable).</summary>
    [RelayCommand]
    private void DetectRooms() => RunRoomDetection(showDialogOnEmpty: true);

    /// <summary>
    /// Runs room detection on the current background image. When nothing is found it (optionally)
    /// pops a dialog inviting the user to define rooms manually with the Room tool.
    /// </summary>
    private void RunRoomDetection(bool showDialogOnEmpty)
    {
        if (BackgroundImage is not System.Windows.Media.Imaging.BitmapSource bmp)
        {
            StatusText = "Import a floor plan first (Import ▾ → image or PDF), then Detect rooms.";
            return;
        }

        List<Room> rooms;
        try
        {
            rooms = RoomDetectionService.DetectRooms(bmp, Plan.BackgroundOrigin, Plan.BackgroundMetresPerPixel);
        }
        catch (Exception ex)
        {
            StatusText = $"Room detection failed: {ex.Message}";
            return;
        }

        if (rooms.Count == 0)
        {
            StatusText = "Couldn't detect rooms automatically — define them manually with the Room tool.";
            if (showDialogOnEmpty)
                System.Windows.MessageBox.Show(
                    "Couldn't detect the rooms automatically from this floor plan.\n\n" +
                    "Please define them manually: pick the Room tool on the left and drag a rectangle over each room.",
                    AppInfo.ProductName + " — Define rooms manually",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        // Also generate a wall around each detected room, so the coverage engine blocks a sensor's
        // line-of-sight between rooms — a sensor in one room can't "see" into the next, so the
        // optimizer needs (at least) one sensor per room, not one for the whole plan.
        var walls = WallsFromRooms(rooms);

        ExecuteEdit(new DelegateAction($"Detect rooms (+{rooms.Count})",
            () => { Plan.Rooms.AddRange(rooms); Plan.Walls.AddRange(walls); },
            () => { foreach (var r in rooms) Plan.Rooms.Remove(r); foreach (var w in walls) Plan.Walls.Remove(w); }));
        RequestFitToView?.Invoke();
        StatusText = $"Detected {rooms.Count} room(s) and walled them off. Optimize to place sensors (≈ one or more per room).";
    }

    /// <summary>Builds a wall along every edge of each room polygon (so rooms are enclosed occluders).</summary>
    private static List<Wall> WallsFromRooms(IEnumerable<Room> rooms)
    {
        var walls = new List<Wall>();
        foreach (var room in rooms)
        {
            var p = room.Polygon;
            for (int i = 0; i < p.Count; i++)
                walls.Add(new Wall { Start = p[i], End = p[(i + 1) % p.Count] });
        }
        return walls;
    }

    /// <summary>Moves the background image by a world-space delta (used while 'Move background' is on).</summary>
    public void NudgeBackground(Vec2 delta)
    {
        Plan.BackgroundOrigin += delta;
        RequestInvalidate?.Invoke();
    }

    private void LoadBackgroundFromPlan()
    {
        BackgroundImage = null;
        if (string.IsNullOrWhiteSpace(Plan.BackgroundImagePath) || !System.IO.File.Exists(Plan.BackgroundImagePath))
            return;
        try
        {
            BackgroundImage = LoadBitmap(Plan.BackgroundImagePath);
            OnPropertyChanged(nameof(BackgroundOpacity));
            OnPropertyChanged(nameof(BackgroundScalePercent));
        }
        catch { /* ignore a missing/broken image on load */ }
    }

    private static System.Windows.Media.Imaging.BitmapImage LoadBitmap(string path)
    {
        var bmp = new System.Windows.Media.Imaging.BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; // don't lock the file
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    // ---- Optimize (greedy + animated placement) ----------------------------

    [RelayCommand]
    private void Optimize()
    {
        if (IsOptimizing) return;

        var suggestions = SensorOptimizer.Optimize(Plan);
        if (suggestions.Count == 0)
        {
            StatusText = "Coverage is already near-optimal — no additional sensors suggested.";
            return;
        }

        _pendingSuggestions.Clear();
        foreach (var s in suggestions) _pendingSuggestions.Enqueue(s);

        IsOptimizing = true;
        SuggestionOpacity = 0.9;
        StatusText = $"Optimizing… suggesting {suggestions.Count} sensor(s).";
        _optimizeTimer.Start();
    }

    private void OnOptimizeTick(object? sender, EventArgs e)
    {
        if (_pendingSuggestions.Count > 0)
        {
            var next = _pendingSuggestions.Dequeue();
            Plan.Sensors.Add(next); // rendered ghosted while IsSuggestion == true
            RequestInvalidate?.Invoke();
            return;
        }

        // Animation finished — commit the suggestions as one undoable action.
        _optimizeTimer.Stop();
        IsOptimizing = false;

        var committed = new List<Sensor>();
        for (int i = Plan.Sensors.Count - 1; i >= 0; i--)
        {
            if (Plan.Sensors[i].IsSuggestion)
            {
                var real = Plan.Sensors[i];
                Plan.Sensors.RemoveAt(i);
                real.IsSuggestion = false;
                real.Name = $"Optimized {committed.Count + 1}";
                committed.Add(real);
            }
        }
        committed.Reverse();

        if (committed.Count > 0)
        {
            ExecuteEdit(new DelegateAction($"Optimize (+{committed.Count} sensors)",
                () => Plan.Sensors.AddRange(committed),
                () => { foreach (var s in committed) Plan.Sensors.Remove(s); }));
            StatusText = $"Added {committed.Count} optimized sensor(s). Coverage {CoveredPercent:0.#}%.";
        }
    }
}
