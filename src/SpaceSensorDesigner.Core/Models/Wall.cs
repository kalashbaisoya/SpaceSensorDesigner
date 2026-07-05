using System;

namespace SpaceSensorDesigner.Core.Models;

/// <summary>
/// A straight wall segment. Walls double as line-of-sight obstacles for the coverage engine.
/// </summary>
public sealed class Wall
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Start point in world metres.</summary>
    public Vec2 Start { get; set; }

    /// <summary>End point in world metres.</summary>
    public Vec2 End { get; set; }

    /// <summary>Wall thickness in metres (used for rendering; LoS uses the centre line).</summary>
    public double Thickness { get; set; } = 0.12;

    public double Length => Start.DistanceTo(End);
}
