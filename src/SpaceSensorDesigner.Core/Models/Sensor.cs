using System;

namespace SpaceSensorDesigner.Core.Models;

/// <summary>
/// A ceiling-mounted thermal sensor (modelled on the Melexis <c>MLX90640</c>: a 32×24 far-infrared
/// array with a wide rectangular field of view). It points straight down, so its coverage on the
/// floor is a <b>rectangular pyramid footprint</b> (a frustum base), not a circular cone — the
/// footprint size grows with mounting height and the horizontal/vertical FOV angles.
/// </summary>
public sealed class Sensor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public SensorType Type { get; set; } = SensorType.Activity;
    public string Name { get; set; } = "MLX90640";

    /// <summary>Floor position directly beneath the ceiling-mounted device, in world metres.</summary>
    public Vec2 Position { get; set; }

    /// <summary>Mounting height above the floor in metres. Drives the footprint size.</summary>
    public double Height { get; set; } = 2.7;

    /// <summary>Horizontal field of view in degrees (MLX90640 wide variant ≈ 110°).</summary>
    public double HorizontalFovDegrees { get; set; } = 110;

    /// <summary>Vertical field of view in degrees (MLX90640 wide variant ≈ 75°).</summary>
    public double VerticalFovDegrees { get; set; } = 75;

    /// <summary>Yaw rotation of the footprint about the vertical axis, in degrees.</summary>
    public double OrientationDegrees { get; set; }

    // Thermal array resolution — shown in the UI and used to draw the pixel-grid overlay.
    public int PixelColumns { get; set; } = 32;
    public int PixelRows { get; set; } = 24;

    // --- Mock telemetry (Phase 1: displayed, not simulated) ---
    public int BatteryPercent { get; set; } = 100;
    public bool IsOnline { get; set; } = true;

    /// <summary>True when this sensor is a not-yet-committed suggestion from the optimizer.</summary>
    public bool IsSuggestion { get; set; }

    public Sensor Clone() => new()
    {
        Id = Id,
        Type = Type,
        Name = Name,
        Position = Position,
        Height = Height,
        HorizontalFovDegrees = HorizontalFovDegrees,
        VerticalFovDegrees = VerticalFovDegrees,
        OrientationDegrees = OrientationDegrees,
        PixelColumns = PixelColumns,
        PixelRows = PixelRows,
        BatteryPercent = BatteryPercent,
        IsOnline = IsOnline,
        IsSuggestion = IsSuggestion
    };
}
