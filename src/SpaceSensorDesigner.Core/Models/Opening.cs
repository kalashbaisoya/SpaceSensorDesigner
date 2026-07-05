using System;

namespace SpaceSensorDesigner.Core.Models;

/// <summary>
/// A door or window cut into a wall. Doors create a gap that thermal line-of-sight passes through;
/// windows are drawn but still block the sensor (glass is opaque to the MLX90640's LWIR band).
/// </summary>
public sealed class Opening
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The wall this opening sits on.</summary>
    public Guid WallId { get; set; }

    /// <summary>Centre of the opening in world metres (snapped onto the wall).</summary>
    public Vec2 Center { get; set; }

    /// <summary>Opening width in metres.</summary>
    public double Width { get; set; } = 0.9;

    public OpeningKind Kind { get; set; } = OpeningKind.Door;

    /// <summary>True when this opening lets the sensor see through it (open doorways).</summary>
    public bool IsSeeThrough => Kind == OpeningKind.Door;
}
