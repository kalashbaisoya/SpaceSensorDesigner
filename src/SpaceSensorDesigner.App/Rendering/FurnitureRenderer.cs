using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using SpaceSensorDesigner.Core.Geometry;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.App.Rendering;

/// <summary>
/// Draws furniture as small illustrated models rather than plain cuboids. In the isometric view
/// each item is composed from a few shaded 3D prisms (plus rounded discs for bowls/basins) with
/// simple depth sorting; in the 2D top-down view it draws a clean schematic plan symbol. All
/// geometry is authored in local metres and pushed through the active <see cref="IProjection"/>,
/// so models scale with Size, rotate with RotationDegrees, and work in both views without assets.
/// </summary>
public sealed class FurnitureRenderer
{
    // Material palettes (top face is the lit face; sides are shaded darker).
    private static readonly Color WoodTop = Color.FromRgb(0xE8, 0xD8, 0xBE);
    private static readonly Color WoodSide = Color.FromRgb(0xD2, 0xBE, 0x9C);
    private static readonly Color FabricTop = Color.FromRgb(0xE9, 0xDD, 0xC8);
    private static readonly Color FabricSide = Color.FromRgb(0xD6, 0xC6, 0xA9);
    private static readonly Color CushionTop = Color.FromRgb(0xF1, 0xE7, 0xD5);
    private static readonly Color LinenTop = Color.FromRgb(0xF3, 0xEE, 0xE4);
    private static readonly Color LinenSide = Color.FromRgb(0xE0, 0xD8, 0xC8);
    private static readonly Color Porcelain = Color.FromRgb(0xF3, 0xF1, 0xEB);
    private static readonly Color PorcelainSide = Color.FromRgb(0xDD, 0xD9, 0xD0);
    private static readonly Color Metal = Color.FromRgb(0xBF, 0xB6, 0xA6);
    private static readonly Color BasinColor = Color.FromRgb(0xDF, 0xE6, 0xE8);

    private static readonly Pen Stroke = MakeStroke(Palette.FurnitureStroke, 0.8);
    private static readonly Pen SoftStroke = MakeStroke(Palette.FurnitureStroke, 0.6);

    public void Draw(DrawingContext dc, IProjection proj, FurnitureItem f, bool iso)
    {
        if (iso) DrawIso(dc, proj, f);
        else DrawPlan(dc, proj, f);
    }

    // ---- Isometric illustrated models --------------------------------------

    private void DrawIso(DrawingContext dc, IProjection proj, FurnitureItem f)
    {
        double rot = f.RotationDegrees * Math.PI / 180.0;
        double hw = f.Size.X / 2, hd = f.Size.Y / 2;
        var ctx = new Ctx(dc, proj, f, rot);

        switch (f.Type)
        {
            case FurnitureType.Bed: BuildBed(ctx, hw, hd); break;
            case FurnitureType.Sofa: BuildSofa(ctx, hw, hd); break;
            case FurnitureType.Chair: BuildChair(ctx, hw, hd); break;
            case FurnitureType.Table: BuildTable(ctx, hw, hd, 0.75); break;
            case FurnitureType.Desk: BuildDesk(ctx, hw, hd); break;
            case FurnitureType.Wardrobe: BuildWardrobe(ctx, hw, hd); break;
            case FurnitureType.KitchenCounter: BuildCounter(ctx, hw, hd); break;
            case FurnitureType.Sink: BuildSink(ctx, hw, hd); break;
            case FurnitureType.Toilet: BuildToilet(ctx, hw, hd); break;
            case FurnitureType.Rug: BuildRug(ctx, hw, hd); break;
            default: Box(ctx, -hw, -hd, hw, hd, 0, 0.6, WoodTop, WoodSide); break;
        }
    }

    private void BuildBed(Ctx c, double hw, double hd)
    {
        Box(c, -hw, -hd, hw, hd, 0, 0.30, LinenTop, LinenSide);          // mattress
        Box(c, -hw + 0.05, -hd + 0.05, hw - 0.05, -hd + 0.55, 0.30, 0.45, CushionTop, FabricSide); // pillows
        Box(c, -hw, -hd + 0.55, hw, hd, 0.30, 0.36, Color.FromRgb(0xEA, 0xDE, 0xC9), LinenSide);    // duvet
    }

    private void BuildSofa(Ctx c, double hw, double hd)
    {
        Box(c, -hw, -hd, hw, hd, 0, 0.32, FabricTop, FabricSide);                 // base
        Box(c, -hw, hd - 0.22, hw, hd, 0.32, 0.78, FabricTop, FabricSide);        // backrest
        Box(c, -hw, -hd, -hw + 0.16, hd, 0, 0.52, FabricTop, FabricSide);         // left arm
        Box(c, hw - 0.16, -hd, hw, hd, 0, 0.52, FabricTop, FabricSide);           // right arm
        // seat cushions
        double innerW = hw - 0.18;
        Box(c, -innerW, -hd + 0.05, 0, hd - 0.24, 0.32, 0.42, CushionTop, FabricSide);
        Box(c, 0, -hd + 0.05, innerW, hd - 0.24, 0.32, 0.42, CushionTop, FabricSide);
    }

    private void BuildChair(Ctx c, double hw, double hd)
    {
        Legs(c, hw, hd, 0.42);
        Box(c, -hw, -hd, hw, hd, 0.42, 0.48, WoodTop, WoodSide);          // seat
        Box(c, -hw, hd - 0.06, hw, hd, 0.48, 0.9, WoodTop, WoodSide);     // backrest
    }

    private void BuildTable(Ctx c, double hw, double hd, double h)
    {
        Legs(c, hw, hd, h - 0.05);
        Box(c, -hw, -hd, hw, hd, h - 0.05, h, WoodTop, WoodSide);         // top slab
    }

    private void BuildDesk(Ctx c, double hw, double hd)
    {
        double h = 0.75;
        Box(c, -hw, -hd, hw, hd, h - 0.05, h, WoodTop, WoodSide);         // top
        Box(c, hw - 0.45, -hd, hw, hd, 0, h - 0.05, WoodTop, WoodSide);   // drawer pedestal
        Box(c, -hw, hd - 0.04, -hw + 0.05, hd, 0, h - 0.05, Metal, Metal);// far-left leg
        Box(c, -hw, -hd, -hw + 0.05, -hd + 0.04, 0, h - 0.05, Metal, Metal);
    }

    private void BuildWardrobe(Ctx c, double hw, double hd)
    {
        Box(c, -hw, -hd, hw, hd, 0, 1.9, WoodTop, WoodSide);
        // door split + handles on the front (-Y) face
        c.Line(new Vec2(0, -hd), 0.02, new Vec2(0, -hd), 1.88, Metal);
        c.Line(new Vec2(-0.12, -hd), 0.9, new Vec2(-0.12, -hd), 1.1, Metal);
        c.Line(new Vec2(0.12, -hd), 0.9, new Vec2(0.12, -hd), 1.1, Metal);
    }

    private void BuildCounter(Ctx c, double hw, double hd)
    {
        Box(c, -hw, -hd, hw, hd, 0, 0.85, WoodTop, WoodSide);            // cabinet
        Box(c, -hw, -hd, hw, hd, 0.85, 0.92, LinenTop, LinenSide);       // countertop
        Disc(c, hw * 0.4, 0, 0.16, 0.11, 0.925, BasinColor);            // sink basin
    }

    private void BuildSink(Ctx c, double hw, double hd)
    {
        Box(c, -hw, -hd, hw, hd, 0, 0.8, Porcelain, PorcelainSide);
        Box(c, -hw, -hd, hw, hd, 0.8, 0.86, Porcelain, PorcelainSide);
        Disc(c, 0, 0, hw * 0.6, hd * 0.55, 0.865, BasinColor);
    }

    private void BuildToilet(Ctx c, double hw, double hd)
    {
        Box(c, -hw * 0.55, hd - 0.16, hw * 0.55, hd, 0, 0.75, Porcelain, PorcelainSide); // tank
        Box(c, -hw * 0.5, -hd, hw * 0.5, hd - 0.16, 0, 0.38, Porcelain, PorcelainSide);  // pedestal
        Disc(c, 0, -hd * 0.35, hw * 0.55, hd * 0.5, 0.4, Porcelain);                     // seat
        Disc(c, 0, -hd * 0.35, hw * 0.32, hd * 0.28, 0.405, BasinColor);                 // bowl opening
    }

    private void BuildRug(Ctx c, double hw, double hd)
    {
        Box(c, -hw, -hd, hw, hd, 0, 0.02, Color.FromRgb(0xEC, 0xE0, 0xCB), Color.FromRgb(0xE0, 0xD3, 0xBB));
        c.Quad(new[]
        {
            c.S(-hw + 0.1, -hd + 0.1, 0.021), c.S(hw - 0.1, -hd + 0.1, 0.021),
            c.S(hw - 0.1, hd - 0.1, 0.021), c.S(-hw + 0.1, hd - 0.1, 0.021)
        }, Color.FromArgb(0, 0, 0, 0), MakeStroke(Palette.FurnitureStroke, 0.7));
    }

    private void Legs(Ctx c, double hw, double hd, double h)
    {
        double t = 0.05, i = 0.04;
        Box(c, -hw + i, -hd + i, -hw + i + t, -hd + i + t, 0, h, Metal, Metal);
        Box(c, hw - i - t, -hd + i, hw - i, -hd + i + t, 0, h, Metal, Metal);
        Box(c, hw - i - t, hd - i - t, hw - i, hd - i, 0, h, Metal, Metal);
        Box(c, -hw + i, hd - i - t, -hw + i + t, hd - i, 0, h, Metal, Metal);
    }

    // ---- Primitive: shaded 3D box ------------------------------------------

    private void Box(Ctx c, double x0, double y0, double x1, double y1, double z0, double z1,
        Color topCol, Color sideCol)
    {
        var baseL = new (double x, double y, double nx, double ny)[]
        {
            (x0, y0, 0, -1), (x1, y0, 1, 0), (x1, y1, 0, 1), (x0, y1, -1, 0)
        };

        var faces = new List<(double depth, Point[] pts, Color col)>(4);
        for (int i = 0; i < 4; i++)
        {
            var a = baseL[i];
            var b = baseL[(i + 1) % 4];
            var p0 = c.S(a.x, a.y, z0);
            var p1 = c.S(b.x, b.y, z0);
            var p2 = c.S(b.x, b.y, z1);
            var p3 = c.S(a.x, a.y, z1);
            var wn = new Vec2(a.nx, a.ny).Rotate(c.Rot);
            faces.Add((Math.Max(p0.Y, p1.Y), new[] { p0, p1, p2, p3 }, Shade(sideCol, wn)));
        }
        faces.Sort((u, v) => u.depth.CompareTo(v.depth));
        foreach (var fc in faces) c.Quad(fc.pts, fc.col, Stroke);

        c.Quad(new[] { c.S(x0, y0, z1), c.S(x1, y0, z1), c.S(x1, y1, z1), c.S(x0, y1, z1) }, topCol, Stroke);
    }

    // ---- Primitive: rounded disc at a height (bowl / basin) -----------------

    private void Disc(Ctx c, double cx, double cy, double rx, double ry, double z, Color col)
    {
        const int n = 22;
        var pts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            double a = 2 * Math.PI * i / n;
            pts[i] = c.S(cx + Math.Cos(a) * rx, cy + Math.Sin(a) * ry, z);
        }
        c.Quad(pts, col, SoftStroke);
    }

    private static Color Shade(Color c, Vec2 worldNormal)
    {
        // Faces pointing down-left (+Y in iso) are darkest; +X faces are brightest.
        double f = 0.9;
        if (Math.Abs(worldNormal.X) >= Math.Abs(worldNormal.Y))
            f = worldNormal.X > 0 ? 1.0 : 0.86;
        else
            f = worldNormal.Y > 0 ? 0.8 : 0.94;
        return Mul(c, f);
    }

    private static Color Mul(Color c, double f) => Color.FromRgb(
        (byte)Math.Clamp(c.R * f, 0, 255),
        (byte)Math.Clamp(c.G * f, 0, 255),
        (byte)Math.Clamp(c.B * f, 0, 255));

    // ---- 2D plan symbols ----------------------------------------------------

    private void DrawPlan(DrawingContext dc, IProjection proj, FurnitureItem f)
    {
        double rot = f.RotationDegrees * Math.PI / 180.0;
        double hw = f.Size.X / 2, hd = f.Size.Y / 2;
        var c = new Ctx(dc, proj, f, rot);

        Color fill = f.Type is FurnitureType.Toilet or FurnitureType.Sink ? Porcelain
            : f.Type is FurnitureType.Sofa or FurnitureType.Bed ? FabricTop : WoodTop;
        c.Quad(new[] { c.S(-hw, -hd, 0), c.S(hw, -hd, 0), c.S(hw, hd, 0), c.S(-hw, hd, 0) }, fill, Stroke);

        switch (f.Type)
        {
            case FurnitureType.Bed:
                c.Line(new Vec2(-hw, -hd + 0.55), 0, new Vec2(hw, -hd + 0.55), 0, Palette.FurnitureStroke);
                c.Quad(new[] { c.S(-hw + 0.08, -hd + 0.08, 0), c.S(hw - 0.08, -hd + 0.08, 0), c.S(hw - 0.08, -hd + 0.45, 0), c.S(-hw + 0.08, -hd + 0.45, 0) }, CushionTop, SoftStroke);
                break;
            case FurnitureType.Sofa:
                c.Line(new Vec2(-hw, hd - 0.2), 0, new Vec2(hw, hd - 0.2), 0, Palette.FurnitureStroke);
                c.Line(new Vec2(0, -hd), 0, new Vec2(0, hd - 0.2), 0, Palette.FurnitureStroke);
                break;
            case FurnitureType.Toilet:
                Disc(c, 0, -hd * 0.35, hw * 0.6, hd * 0.5, 0, BasinColor);
                break;
            case FurnitureType.Sink:
            case FurnitureType.KitchenCounter:
                Disc(c, f.Type == FurnitureType.Sink ? 0 : hw * 0.4, 0, 0.18, 0.13, 0, BasinColor);
                break;
            case FurnitureType.Wardrobe:
                c.Line(new Vec2(0, -hd), 0, new Vec2(0, hd), 0, Palette.FurnitureStroke);
                break;
            case FurnitureType.Rug:
                c.Quad(new[] { c.S(-hw + 0.12, -hd + 0.12, 0), c.S(hw - 0.12, -hd + 0.12, 0), c.S(hw - 0.12, hd - 0.12, 0), c.S(-hw + 0.12, hd - 0.12, 0) }, Color.FromArgb(0, 0, 0, 0), SoftStroke);
                break;
        }
    }

    private static Pen MakeStroke(Color c, double thickness)
    {
        var p = new Pen(Palette.Brush(c), thickness);
        p.Freeze();
        return p;
    }

    /// <summary>Per-item drawing context: projects local metre coords to screen and draws primitives.</summary>
    private readonly struct Ctx
    {
        private readonly DrawingContext _dc;
        private readonly IProjection _proj;
        private readonly FurnitureItem _f;
        public double Rot { get; }

        public Ctx(DrawingContext dc, IProjection proj, FurnitureItem f, double rot)
        {
            _dc = dc; _proj = proj; _f = f; Rot = rot;
        }

        public Point S(double lx, double ly, double lz)
        {
            var w = _f.Position + new Vec2(lx, ly).Rotate(Rot);
            var s = _proj.WorldToScreen(w, lz);
            return new Point(s.X, s.Y);
        }

        public void Quad(Point[] pts, Color fill, Pen stroke)
        {
            var geom = new StreamGeometry();
            using (var g = geom.Open())
            {
                g.BeginFigure(pts[0], fill.A > 0, true);
                for (int i = 1; i < pts.Length; i++) g.LineTo(pts[i], true, false);
            }
            geom.Freeze();
            _dc.DrawGeometry(fill.A > 0 ? Palette.Brush(fill) : null, stroke, geom);
        }

        public void Line(Vec2 aXY, double aZ, Vec2 bXY, double bZ, Color col)
            => _dc.DrawLine(MakeStroke(col, 1), S(aXY.X, aXY.Y, aZ), S(bXY.X, bXY.Y, bZ));
    }
}
