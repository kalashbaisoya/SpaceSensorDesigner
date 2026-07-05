using System;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Geometry;

/// <summary>
/// A projection maps world metres to screen pixels and back. Two implementations exist:
/// an identity (top-down) projection and a 2.5D isometric projection.
/// Height (metres above the floor) lifts a point along screen-Y for the isometric view.
/// </summary>
public interface IProjection
{
    /// <summary>Projects a world point (optionally elevated by <paramref name="heightMetres"/>) to screen pixels.</summary>
    Vec2 WorldToScreen(Vec2 world, double heightMetres = 0);

    /// <summary>Inverse of <see cref="WorldToScreen"/> at floor level (height = 0). Used for hit-testing.</summary>
    Vec2 ScreenToWorld(Vec2 screen);
}

/// <summary>Straight top-down projection: screen = world * scale + offset, Y grows downward.</summary>
public sealed class TopDownProjection : IProjection
{
    private readonly double _scale;   // pixels per metre
    private readonly Vec2 _offset;    // screen-space pan (pixels)

    public TopDownProjection(double pixelsPerMetre, Vec2 offset)
    {
        _scale = pixelsPerMetre;
        _offset = offset;
    }

    public Vec2 WorldToScreen(Vec2 world, double heightMetres = 0)
        => new(world.X * _scale + _offset.X, world.Y * _scale + _offset.Y);

    public Vec2 ScreenToWorld(Vec2 screen)
        => new((screen.X - _offset.X) / _scale, (screen.Y - _offset.Y) / _scale);
}

/// <summary>
/// A classic 2:1 isometric (dimetric) projection — the look of the Butlr reference.
/// World +X goes down-right, world +Y goes down-left, and height lifts the point up.
/// </summary>
public sealed class IsometricProjection : IProjection
{
    private readonly double _scale;      // pixels per metre (base)
    private readonly Vec2 _offset;       // screen-space pan (pixels)
    private readonly double _heightScale;// pixels per metre of elevation

    // 2:1 isometric tile: horizontal half-width and vertical half-height factors.
    private const double IsoX = 1.0;
    private const double IsoY = 0.5;

    public IsometricProjection(double pixelsPerMetre, Vec2 offset, double heightScale = 26)
    {
        _scale = pixelsPerMetre;
        _offset = offset;
        _heightScale = heightScale;
    }

    public Vec2 WorldToScreen(Vec2 world, double heightMetres = 0)
    {
        double sx = (world.X - world.Y) * IsoX * _scale;
        double sy = (world.X + world.Y) * IsoY * _scale - heightMetres * _heightScale;
        return new Vec2(sx + _offset.X, sy + _offset.Y);
    }

    public Vec2 ScreenToWorld(Vec2 screen)
    {
        // Invert the floor-level (height = 0) transform.
        double a = (screen.X - _offset.X) / (IsoX * _scale); // = X - Y
        double b = (screen.Y - _offset.Y) / (IsoY * _scale); // = X + Y
        double x = (a + b) * 0.5;
        double y = (b - a) * 0.5;
        return new Vec2(x, y);
    }
}
