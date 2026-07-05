using System;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Geometry;

/// <summary>
/// Computes the floor footprint of a downward-facing thermal sensor. Because an MLX90640 points
/// straight down with a rectangular field of view, the region it can see on the floor is a
/// rectangle (the base of a pyramid whose apex is the sensor). The rectangle's half-extents are
/// <c>height · tan(fov/2)</c> for each axis, then rotated by the sensor's yaw.
/// </summary>
public static class SensorFootprint
{
    private const double DegToRad = Math.PI / 180.0;

    public static double HalfWidth(double height, double horizontalFovDegrees)
        => height * Math.Tan(Clamp(horizontalFovDegrees) * 0.5 * DegToRad);

    public static double HalfDepth(double height, double verticalFovDegrees)
        => height * Math.Tan(Clamp(verticalFovDegrees) * 0.5 * DegToRad);

    public static double HalfWidth(Sensor s) => HalfWidth(s.Height, s.HorizontalFovDegrees);
    public static double HalfDepth(Sensor s) => HalfDepth(s.Height, s.VerticalFovDegrees);

    /// <summary>The four floor corners of the footprint (clockwise), in world metres.</summary>
    public static Vec2[] FloorCorners(Sensor s)
        => FloorCorners(s.Position, s.Height, s.HorizontalFovDegrees, s.VerticalFovDegrees, s.OrientationDegrees);

    public static Vec2[] FloorCorners(Vec2 position, double height, double hFovDeg, double vFovDeg, double orientationDeg)
    {
        double hw = HalfWidth(height, hFovDeg);
        double hd = HalfDepth(height, vFovDeg);
        double rot = orientationDeg * DegToRad;

        Span<Vec2> local = stackalloc Vec2[4]
        {
            new Vec2(-hw, -hd), new Vec2(hw, -hd), new Vec2(hw, hd), new Vec2(-hw, hd)
        };

        var result = new Vec2[4];
        for (int i = 0; i < 4; i++)
            result[i] = position + local[i].Rotate(rot);
        return result;
    }

    /// <summary>Radius of a circle bounding the footprint — used to grow plan bounds around a sensor.</summary>
    public static double BoundingRadius(Sensor s)
    {
        double hw = HalfWidth(s);
        double hd = HalfDepth(s);
        return Math.Sqrt(hw * hw + hd * hd);
    }

    private static double Clamp(double fovDeg) => Math.Clamp(fovDeg, 1, 179);
}
