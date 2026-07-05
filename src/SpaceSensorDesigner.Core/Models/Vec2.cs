using System;

namespace SpaceSensorDesigner.Core.Models;

/// <summary>
/// A lightweight, UI-agnostic 2D vector expressed in world units (metres).
/// The Core library must not depend on WPF, so we cannot use <c>System.Windows.Point</c> here.
/// </summary>
public readonly struct Vec2 : IEquatable<Vec2>
{
    public double X { get; }
    public double Y { get; }

    public Vec2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vec2 Zero => new(0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;

    public Vec2 Normalized
    {
        get
        {
            var len = Length;
            return len > 1e-9 ? new Vec2(X / len, Y / len) : Zero;
        }
    }

    public double DistanceTo(Vec2 other) => (this - other).Length;

    public double Dot(Vec2 other) => X * other.X + Y * other.Y;

    /// <summary>Z component of the 3D cross product; sign tells orientation.</summary>
    public double Cross(Vec2 other) => X * other.Y - Y * other.X;

    /// <summary>Angle of the vector in radians, measured from +X axis, CCW positive.</summary>
    public double Angle => Math.Atan2(Y, X);

    public Vec2 Rotate(double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new Vec2(X * c - Y * s, X * s + Y * c);
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);

    public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is Vec2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X:0.###}, {Y:0.###})";
}
