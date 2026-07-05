using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceSensorDesigner.Core.Models;

/// <summary>
/// The root document. A plain, serialization-friendly aggregate of everything on the floor.
/// </summary>
public sealed class FloorPlan
{
    /// <summary>File format version, so future loaders can migrate older documents.</summary>
    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = "Untitled Plan";

    /// <summary>Coverage grid cell size in metres (default 0.2 m per the spec).</summary>
    public double CellSize { get; set; } = 0.2;

    /// <summary>Grid snapping increment in metres for the designer tools.</summary>
    public double SnapSize { get; set; } = 0.25;

    public List<Room> Rooms { get; set; } = new();
    public List<Wall> Walls { get; set; } = new();
    public List<Opening> Openings { get; set; } = new();
    public List<FurnitureItem> Furniture { get; set; } = new();
    public List<Sensor> Sensors { get; set; } = new();

    // --- Optional traced-over background floor plan (Phase 4 import). Shown in the 2D view. ---
    public string? BackgroundImagePath { get; set; }
    public double BackgroundOpacity { get; set; } = 0.55;
    /// <summary>World scale of the background: metres represented by one image pixel.</summary>
    public double BackgroundMetresPerPixel { get; set; } = 0.02;
    /// <summary>World position (metres) of the image's top-left corner.</summary>
    public Vec2 BackgroundOrigin { get; set; }

    /// <summary>
    /// Axis-aligned bounding box (metres) of the drawn walls. Used as the floor region when the plan
    /// has no rooms, so coverage and optimisation stay inside the walls the user traced instead of
    /// spilling across the whole canvas. Returns null when there are no walls.
    /// </summary>
    public (Vec2 min, Vec2 max)? WallBounds()
    {
        if (Walls.Count == 0) return null;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var w in Walls)
        {
            minX = Math.Min(minX, Math.Min(w.Start.X, w.End.X));
            minY = Math.Min(minY, Math.Min(w.Start.Y, w.End.Y));
            maxX = Math.Max(maxX, Math.Max(w.Start.X, w.End.X));
            maxY = Math.Max(maxY, Math.Max(w.Start.Y, w.End.Y));
        }
        return (new Vec2(minX, minY), new Vec2(maxX, maxY));
    }

    /// <summary>
    /// Axis-aligned bounds of everything in the plan (metres). Returns a small default box
    /// when the plan is empty so the coverage grid always has something to work with.
    /// </summary>
    public (Vec2 min, Vec2 max) GetBounds(double padding = 0.5)
    {
        var points = new List<Vec2>();
        foreach (var r in Rooms) points.AddRange(r.Polygon);
        foreach (var w in Walls) { points.Add(w.Start); points.Add(w.End); }
        foreach (var f in Furniture)
        {
            var h = f.Size * 0.5;
            points.Add(f.Position - h);
            points.Add(f.Position + h);
        }
        foreach (var s in Sensors)
        {
            double r = Geometry.SensorFootprint.BoundingRadius(s);
            points.Add(s.Position - new Vec2(r, r));
            points.Add(s.Position + new Vec2(r, r));
        }

        if (points.Count == 0)
            return (new Vec2(0, 0), new Vec2(8, 6));

        double minX = points.Min(p => p.X) - padding;
        double minY = points.Min(p => p.Y) - padding;
        double maxX = points.Max(p => p.X) + padding;
        double maxY = points.Max(p => p.Y) + padding;
        return (new Vec2(minX, minY), new Vec2(maxX, maxY));
    }

    /// <summary>Creates a small starter apartment so a new document is not blank.</summary>
    public static FloorPlan CreateSample()
    {
        var plan = new FloorPlan { Name = "Sample Apartment" };

        // Outer rectangle 6m x 4.5m
        var corners = new[]
        {
            new Vec2(0, 0), new Vec2(6, 0), new Vec2(6, 4.5), new Vec2(0, 4.5)
        };
        for (int i = 0; i < corners.Length; i++)
        {
            plan.Walls.Add(new Wall { Start = corners[i], End = corners[(i + 1) % corners.Length] });
        }
        // Interior partition
        plan.Walls.Add(new Wall { Start = new Vec2(3.5, 0), End = new Vec2(3.5, 2.6) });

        plan.Rooms.Add(new Room
        {
            Name = "Living Room",
            Type = RoomType.LivingRoom,
            Polygon = { new Vec2(0, 0), new Vec2(3.5, 0), new Vec2(3.5, 4.5), new Vec2(0, 4.5) }
        });
        plan.Rooms.Add(new Room
        {
            Name = "Bedroom",
            Type = RoomType.Bedroom,
            Polygon = { new Vec2(3.5, 0), new Vec2(6, 0), new Vec2(6, 4.5), new Vec2(3.5, 4.5) }
        });

        plan.Furniture.Add(new FurnitureItem
        {
            Type = FurnitureType.Sofa, Name = "Sofa",
            Position = new Vec2(1.6, 3.6), Size = new Vec2(2.0, 0.9)
        });
        plan.Furniture.Add(new FurnitureItem
        {
            Type = FurnitureType.Bed, Name = "Bed",
            Position = new Vec2(4.9, 3.3), Size = new Vec2(1.6, 2.0)
        });

        plan.Sensors.Add(new Sensor
        {
            Type = SensorType.Activity, Name = "MLX90640 · Living",
            Position = new Vec2(1.75, 2.0), Height = 2.7,
            HorizontalFovDegrees = 110, VerticalFovDegrees = 75
        });

        return plan;
    }
}
