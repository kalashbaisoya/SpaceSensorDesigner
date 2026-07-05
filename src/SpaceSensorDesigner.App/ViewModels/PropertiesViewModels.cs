using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using SpaceSensorDesigner.App.Services;
using SpaceSensorDesigner.Core.Geometry;
using SpaceSensorDesigner.Core.Models;
using SpaceSensorDesigner.Core.Telemetry;

namespace SpaceSensorDesigner.App.ViewModels;

/// <summary>
/// Base for the context-sensitive properties panel. Each concrete wrapper edits a model object
/// in place and invokes <see cref="OnChanged"/> so the canvas + coverage refresh live.
/// </summary>
public abstract class PropertiesViewModelBase : ObservableObject
{
    protected Action OnChanged { get; }
    protected PropertiesViewModelBase(Action onChanged) => OnChanged = onChanged;
    public abstract string Header { get; }
}

public sealed class SensorPropertiesViewModel : PropertiesViewModelBase
{
    private readonly Sensor _sensor;
    private readonly FloorPlan _plan;
    private readonly LiveTelemetryService? _telemetry;
    private SensorTelemetry _live;

    public SensorPropertiesViewModel(Sensor sensor, FloorPlan plan, Action onChanged, LiveTelemetryService? telemetry = null) : base(onChanged)
    {
        _sensor = sensor;
        _plan = plan;
        _telemetry = telemetry;
        if (_telemetry != null) _live = _telemetry.Sample(_sensor);
    }

    public override string Header => "Sensor";
    public string TypeName => _sensor.Name;
    public string Resolution => $"{_sensor.PixelColumns} × {_sensor.PixelRows} px thermal array";

    // ---- Room + distance-to-wall readouts (recomputed live) ----
    public string RoomName => FindRoom()?.Name ?? "—";

    public string DistanceToLeft   => Dist(d => _sensor.Position.X - d.minX);
    public string DistanceToRight  => Dist(d => d.maxX - _sensor.Position.X);
    public string DistanceToTop    => Dist(d => _sensor.Position.Y - d.minY);
    public string DistanceToBottom => Dist(d => d.maxY - _sensor.Position.Y);

    private string Dist(Func<(double minX, double minY, double maxX, double maxY), double> pick)
    {
        var b = RoomBounds();
        return b == null ? "—" : $"{pick(b.Value):0.00} m";
    }

    private Room? FindRoom()
    {
        foreach (var r in _plan.Rooms)
            if (r.Polygon.Count >= 3 && GeometryUtils.PointInPolygon(_sensor.Position, r.Polygon))
                return r;
        return null;
    }

    private (double minX, double minY, double maxX, double maxY)? RoomBounds()
    {
        var room = FindRoom();
        if (room == null) return null;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var v in room.Polygon)
        {
            minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y);
            maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y);
        }
        return (minX, minY, maxX, maxY);
    }

    private void RaiseDistances()
    {
        OnPropertyChanged(nameof(DistanceToLeft));
        OnPropertyChanged(nameof(DistanceToRight));
        OnPropertyChanged(nameof(DistanceToTop));
        OnPropertyChanged(nameof(DistanceToBottom));
    }

    // ---- Static device identity (stable, derived from the id) ----
    public string MacAddress
    {
        get
        {
            var b = _sensor.Id.ToByteArray();
            return $"00-17-0d-{b[0]:x2}-{b[1]:x2}-{b[2]:x2}-{b[3]:x2}-{b[4]:x2}";
        }
    }
    public int NetworkId => Math.Abs(_sensor.Id.GetHashCode()) % 90 + 10;

    // ---- Live telemetry (simulated feed, refreshed on the telemetry timer) ----
    public bool IsSimulatedFeed => _telemetry?.IsSimulated ?? true;
    public bool LiveOnline => _live.Online;
    public string LiveStatusText => _live.Online ? "Online" : "Offline";
    public string LiveLastSeen => _live.Online
        ? "Live now"
        : $"{(int)Math.Max(1, (DateTime.UtcNow - _live.LastSeenUtc).TotalMinutes)} min ago";
    public string LiveOccupancyText => !_live.Online ? "—"
        : _live.Occupancy == 0 ? "Vacant" : _live.Occupancy == 1 ? "1 person" : $"{_live.Occupancy} people";
    public string LiveAmbientText => _live.Online ? $"{_live.AmbientTempC:0.0} °C" : "—";
    public string LivePeakText => _live.Online ? $"{_live.PeakTempC:0.0} °C" : "—";
    public int LiveBattery => _live.BatteryPercent;
    public string LiveBatteryText => $"{_live.BatteryPercent}%";

    /// <summary>Re-samples the live feed and raises change notifications (called by the telemetry timer).</summary>
    public void RefreshLive()
    {
        if (_telemetry == null) return;
        _live = _telemetry.Sample(_sensor);
        OnPropertyChanged(nameof(LiveOnline));
        OnPropertyChanged(nameof(LiveStatusText));
        OnPropertyChanged(nameof(LiveLastSeen));
        OnPropertyChanged(nameof(LiveOccupancyText));
        OnPropertyChanged(nameof(LiveAmbientText));
        OnPropertyChanged(nameof(LivePeakText));
        OnPropertyChanged(nameof(LiveBattery));
        OnPropertyChanged(nameof(LiveBatteryText));
    }

    public string Name
    {
        get => _sensor.Name;
        set { if (_sensor.Name != value) { _sensor.Name = value; OnPropertyChanged(); OnChanged(); } }
    }

    public double Height
    {
        get => _sensor.Height;
        set { var v = Math.Clamp(value, 1.5, 6); if (Math.Abs(_sensor.Height - v) > 1e-6) { _sensor.Height = v; OnPropertyChanged(); RaiseFootprint(); OnChanged(); } }
    }

    public double HorizontalFovDegrees
    {
        get => _sensor.HorizontalFovDegrees;
        set { var v = Math.Clamp(value, 10, 170); if (Math.Abs(_sensor.HorizontalFovDegrees - v) > 1e-6) { _sensor.HorizontalFovDegrees = v; OnPropertyChanged(); RaiseFootprint(); OnChanged(); } }
    }

    public double VerticalFovDegrees
    {
        get => _sensor.VerticalFovDegrees;
        set { var v = Math.Clamp(value, 10, 170); if (Math.Abs(_sensor.VerticalFovDegrees - v) > 1e-6) { _sensor.VerticalFovDegrees = v; OnPropertyChanged(); RaiseFootprint(); OnChanged(); } }
    }

    public double OrientationDegrees
    {
        get => _sensor.OrientationDegrees;
        set { if (Math.Abs(_sensor.OrientationDegrees - value) > 1e-6) { _sensor.OrientationDegrees = value; OnPropertyChanged(); OnChanged(); } }
    }

    /// <summary>Read-only footprint size (metres) derived from height + FOV — updates live.</summary>
    public string FootprintText
    {
        get
        {
            double w = 2 * SensorFootprint.HalfWidth(_sensor);
            double d = 2 * SensorFootprint.HalfDepth(_sensor);
            return $"{w:0.0} × {d:0.0} m on the floor";
        }
    }

    private void RaiseFootprint() => OnPropertyChanged(nameof(FootprintText));

    public int BatteryPercent
    {
        get => _sensor.BatteryPercent;
        set { var v = Math.Clamp(value, 0, 100); if (_sensor.BatteryPercent != v) { _sensor.BatteryPercent = v; OnPropertyChanged(); } }
    }

    public bool IsOnline
    {
        get => _sensor.IsOnline;
        set { if (_sensor.IsOnline != value) { _sensor.IsOnline = value; OnPropertyChanged(); OnChanged(); } }
    }

    public string PositionText => _sensor.Position.ToString();
}

public sealed class FurniturePropertiesViewModel : PropertiesViewModelBase
{
    private readonly FurnitureItem _item;
    public FurniturePropertiesViewModel(FurnitureItem item, Action onChanged) : base(onChanged) => _item = item;

    public override string Header => "Furniture";
    public string TypeName => _item.Type.ToString();

    public string Name
    {
        get => _item.Name;
        set { if (_item.Name != value) { _item.Name = value; OnPropertyChanged(); OnChanged(); } }
    }

    public double Width
    {
        get => _item.Size.X;
        set { var v = Math.Clamp(value, 0.1, 10); if (Math.Abs(_item.Size.X - v) > 1e-6) { _item.Size = new Vec2(v, _item.Size.Y); OnPropertyChanged(); OnChanged(); } }
    }

    public double Depth
    {
        get => _item.Size.Y;
        set { var v = Math.Clamp(value, 0.1, 10); if (Math.Abs(_item.Size.Y - v) > 1e-6) { _item.Size = new Vec2(_item.Size.X, v); OnPropertyChanged(); OnChanged(); } }
    }

    public double RotationDegrees
    {
        get => _item.RotationDegrees;
        set { if (Math.Abs(_item.RotationDegrees - value) > 1e-6) { _item.RotationDegrees = value; OnPropertyChanged(); OnChanged(); } }
    }
}

public sealed class OpeningPropertiesViewModel : PropertiesViewModelBase
{
    private readonly Opening _opening;
    public OpeningPropertiesViewModel(Opening opening, Action onChanged) : base(onChanged) => _opening = opening;

    public override string Header => "Opening";

    public IReadOnlyList<OpeningKind> Kinds { get; } = (OpeningKind[])Enum.GetValues(typeof(OpeningKind));

    public OpeningKind Kind
    {
        get => _opening.Kind;
        set { if (_opening.Kind != value) { _opening.Kind = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThermalNote)); OnChanged(); } }
    }

    public double Width
    {
        get => _opening.Width;
        set { var v = Math.Clamp(value, 0.3, 3.0); if (Math.Abs(_opening.Width - v) > 1e-6) { _opening.Width = v; OnPropertyChanged(); OnChanged(); } }
    }

    public string ThermalNote => _opening.Kind == OpeningKind.Door
        ? "Open doorway — thermal line-of-sight passes through."
        : "Window — glass is opaque to LWIR, so it still blocks the sensor.";
}

public sealed class RoomPropertiesViewModel : PropertiesViewModelBase
{
    private readonly Room _room;
    public RoomPropertiesViewModel(Room room, Action onChanged) : base(onChanged) => _room = room;

    public override string Header => "Room";

    public IReadOnlyList<RoomType> RoomTypes { get; } = (RoomType[])Enum.GetValues(typeof(RoomType));

    public string Name
    {
        get => _room.Name;
        set { if (_room.Name != value) { _room.Name = value; OnPropertyChanged(); OnChanged(); } }
    }

    public RoomType Type
    {
        get => _room.Type;
        set { if (_room.Type != value) { _room.Type = value; OnPropertyChanged(); OnChanged(); } }
    }
}
