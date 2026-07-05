using System;

namespace SpaceSensorDesigner.Core.Models;

/// <summary>
/// A placed furniture element. Position is the centre in world metres; Size is width/depth in metres.
/// </summary>
public sealed class FurnitureItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FurnitureType Type { get; set; }
    public string Name { get; set; } = "Furniture";

    /// <summary>Centre position in world metres.</summary>
    public Vec2 Position { get; set; }

    /// <summary>Footprint size (width x depth) in metres.</summary>
    public Vec2 Size { get; set; } = new(1.0, 1.0);

    /// <summary>Rotation in degrees, clockwise, about the centre.</summary>
    public double RotationDegrees { get; set; }
}
