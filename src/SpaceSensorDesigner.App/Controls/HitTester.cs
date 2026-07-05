using System;
using SpaceSensorDesigner.Core.Geometry;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.App.Controls;

/// <summary>
/// World-space hit testing for selection. Priority (top to bottom): Sensor, Furniture, Wall, Room.
/// All distances are in world metres; <paramref name="tolerance"/> is derived from the pixel pick
/// radius divided by the current zoom.
/// </summary>
public static class HitTester
{
    public static object? HitTest(FloorPlan plan, Vec2 world, double tolerance)
    {
        double sensorRadius = Math.Max(tolerance, 0.25);
        for (int i = plan.Sensors.Count - 1; i >= 0; i--)
        {
            var s = plan.Sensors[i];
            if (s.IsSuggestion) continue;
            if (s.Position.DistanceTo(world) <= sensorRadius) return s;
        }

        for (int i = plan.Openings.Count - 1; i >= 0; i--)
        {
            var o = plan.Openings[i];
            if (o.Center.DistanceTo(world) <= Math.Max(tolerance, o.Width * 0.5)) return o;
        }

        for (int i = plan.Furniture.Count - 1; i >= 0; i--)
        {
            var f = plan.Furniture[i];
            if (PointInFurniture(f, world)) return f;
        }

        for (int i = plan.Walls.Count - 1; i >= 0; i--)
        {
            var w = plan.Walls[i];
            if (DistanceToSegment(world, w.Start, w.End) <= Math.Max(tolerance, w.Thickness)) return w;
        }

        for (int i = plan.Rooms.Count - 1; i >= 0; i--)
        {
            var r = plan.Rooms[i];
            if (GeometryUtils.PointInPolygon(world, r.Polygon)) return r;
        }

        return null;
    }

    private static bool PointInFurniture(FurnitureItem f, Vec2 world)
    {
        // Transform the point into the furniture's local (unrotated) frame.
        double rot = -f.RotationDegrees * Math.PI / 180.0;
        var local = (world - f.Position).Rotate(rot);
        var h = f.Size * 0.5;
        return Math.Abs(local.X) <= h.X && Math.Abs(local.Y) <= h.Y;
    }

    public static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        double lenSq = ab.LengthSquared;
        if (lenSq < 1e-9) return p.DistanceTo(a);
        double t = Math.Clamp((p - a).Dot(ab) / lenSq, 0, 1);
        var proj = a + ab * t;
        return p.DistanceTo(proj);
    }
}
