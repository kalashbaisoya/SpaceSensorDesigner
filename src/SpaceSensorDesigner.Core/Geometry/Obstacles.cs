using System;
using System.Collections.Generic;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Geometry;

/// <summary>A line segment that blocks thermal line of sight.</summary>
public readonly struct Segment
{
    public Vec2 A { get; }
    public Vec2 B { get; }
    public Segment(Vec2 a, Vec2 b) { A = a; B = b; }
}

/// <summary>
/// Builds the set of line-of-sight blockers for a plan: wall centre-lines (with see-through
/// doorways cut out) plus the footprint edges of tall furniture that occludes a downward sensor.
/// </summary>
public static class Obstacles
{
    /// <summary>Furniture types tall enough to block a ceiling sensor's view of the floor beyond them.</summary>
    public static bool IsOccluder(FurnitureType type) => type switch
    {
        FurnitureType.Wardrobe => true,
        _ => false
    };

    public static List<Segment> Build(FloorPlan plan)
    {
        var result = new List<Segment>();

        foreach (var wall in plan.Walls)
            AddWallWithOpenings(result, wall, plan.Openings);

        foreach (var f in plan.Furniture)
        {
            if (!IsOccluder(f.Type)) continue;
            AddFootprintEdges(result, f);
        }

        return result;
    }

    private static void AddWallWithOpenings(List<Segment> result, Wall wall, IReadOnlyList<Opening> openings)
    {
        double length = wall.Length;
        if (length < 1e-6) return;
        var dir = (wall.End - wall.Start) / length;

        // Collect see-through (door) spans along the wall as [t0,t1] fractions.
        var gaps = new List<(double t0, double t1)>();
        foreach (var op in openings)
        {
            if (op.WallId != wall.Id || !op.IsSeeThrough) continue;
            double proj = (op.Center - wall.Start).Dot(dir);           // metres along the wall
            double half = op.Width * 0.5;
            double t0 = Math.Clamp((proj - half) / length, 0, 1);
            double t1 = Math.Clamp((proj + half) / length, 0, 1);
            if (t1 > t0) gaps.Add((t0, t1));
        }

        if (gaps.Count == 0)
        {
            result.Add(new Segment(wall.Start, wall.End));
            return;
        }

        gaps.Sort((a, b) => a.t0.CompareTo(b.t0));

        double cursor = 0;
        foreach (var (t0, t1) in gaps)
        {
            if (t0 > cursor)
                result.Add(new Segment(PointAt(wall, cursor), PointAt(wall, t0)));
            cursor = Math.Max(cursor, t1);
        }
        if (cursor < 1)
            result.Add(new Segment(PointAt(wall, cursor), wall.End));
    }

    private static Vec2 PointAt(Wall wall, double t) => wall.Start + (wall.End - wall.Start) * t;

    private static void AddFootprintEdges(List<Segment> result, FurnitureItem f)
    {
        var h = f.Size * 0.5;
        double rot = f.RotationDegrees * Math.PI / 180.0;
        var c = new[]
        {
            f.Position + new Vec2(-h.X, -h.Y).Rotate(rot),
            f.Position + new Vec2(h.X, -h.Y).Rotate(rot),
            f.Position + new Vec2(h.X, h.Y).Rotate(rot),
            f.Position + new Vec2(-h.X, h.Y).Rotate(rot),
        };
        for (int i = 0; i < 4; i++)
            result.Add(new Segment(c[i], c[(i + 1) % 4]));
    }
}
