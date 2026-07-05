using System;
using System.Collections.Generic;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Geometry;

/// <summary>
/// Pure geometry helpers shared by the coverage engine and the optimizer.
/// All coordinates are in world metres.
/// </summary>
public static class GeometryUtils
{
    private const double Epsilon = 1e-9;

    /// <summary>
    /// Returns true if the two closed segments [p1,p2] and [p3,p4] properly intersect.
    /// Collinear/touching-at-endpoint cases return false, which is what we want for
    /// line-of-sight: a ray that only grazes a wall endpoint is still considered visible.
    /// </summary>
    public static bool SegmentsIntersect(Vec2 p1, Vec2 p2, Vec2 p3, Vec2 p4)
    {
        double d1 = Cross(p3, p4, p1);
        double d2 = Cross(p3, p4, p2);
        double d3 = Cross(p1, p2, p3);
        double d4 = Cross(p1, p2, p4);

        // Strictly opposite signs on both tests => a proper crossing.
        if (((d1 > Epsilon && d2 < -Epsilon) || (d1 < -Epsilon && d2 > Epsilon)) &&
            ((d3 > Epsilon && d4 < -Epsilon) || (d3 < -Epsilon && d4 > Epsilon)))
        {
            return true;
        }
        return false;
    }

    // Cross product of (b - a) x (c - a). Sign indicates which side of line ab point c is on.
    private static double Cross(Vec2 a, Vec2 b, Vec2 c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    /// <summary>Ray-casting point-in-polygon test for a simple polygon.</summary>
    public static bool PointInPolygon(Vec2 point, IReadOnlyList<Vec2> polygon)
    {
        if (polygon.Count < 3) return false;

        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            bool crosses = (pi.Y > point.Y) != (pj.Y > point.Y);
            if (crosses)
            {
                double xCross = (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X;
                if (point.X < xCross) inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// Returns true if there is a clear line of sight between <paramref name="from"/> and
    /// <paramref name="to"/> — i.e. no wall centre line crosses the segment between them.
    /// </summary>
    public static bool HasLineOfSight(Vec2 from, Vec2 to, IReadOnlyList<Wall> walls)
    {
        foreach (var wall in walls)
        {
            if (SegmentsIntersect(from, to, wall.Start, wall.End))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Line-of-sight test against a pre-built obstacle set (walls with doorways cut out, plus
    /// furniture occluders). See <see cref="Obstacles"/>.
    /// </summary>
    public static bool HasLineOfSight(Vec2 from, Vec2 to, IReadOnlyList<Segment> obstacles)
    {
        foreach (var s in obstacles)
        {
            if (SegmentsIntersect(from, to, s.A, s.B))
                return false;
        }
        return true;
    }

    /// <summary>The closest point to <paramref name="p"/> on the segment [a,b].</summary>
    public static Vec2 ClosestPointOnSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        double len2 = ab.LengthSquared;
        if (len2 < 1e-9) return a;
        double t = Math.Clamp((p - a).Dot(ab) / len2, 0, 1);
        return a + ab * t;
    }

    /// <summary>Snaps a world coordinate to the nearest multiple of <paramref name="step"/>.</summary>
    public static Vec2 Snap(Vec2 p, double step)
    {
        if (step <= Epsilon) return p;
        return new Vec2(Math.Round(p.X / step) * step, Math.Round(p.Y / step) * step);
    }
}
