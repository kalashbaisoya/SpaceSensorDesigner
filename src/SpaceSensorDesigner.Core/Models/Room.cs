using System;
using System.Collections.Generic;

namespace SpaceSensorDesigner.Core.Models;

/// <summary>
/// A room is a closed polygon (in world metres) with a semantic type used for color coding.
/// </summary>
public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Room";
    public RoomType Type { get; set; } = RoomType.Other;

    /// <summary>Ordered polygon vertices in world coordinates (metres).</summary>
    public List<Vec2> Polygon { get; set; } = new();

    /// <summary>Optional explicit color override as #AARRGGBB. When null the type's default color is used.</summary>
    public string? ColorOverride { get; set; }

    public Vec2 Centroid
    {
        get
        {
            if (Polygon.Count == 0) return Vec2.Zero;
            double x = 0, y = 0;
            foreach (var p in Polygon) { x += p.X; y += p.Y; }
            return new Vec2(x / Polygon.Count, y / Polygon.Count);
        }
    }
}
