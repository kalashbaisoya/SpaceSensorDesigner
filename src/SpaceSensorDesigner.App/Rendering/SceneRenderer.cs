using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Geometry;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.App.Rendering;

/// <summary>
/// Immediate-mode renderer that paints a <see cref="RenderContext"/> onto a WPF
/// <see cref="DrawingContext"/> in the warm, soft "Butlr" style. The same code path serves both
/// the 2D top-down and the 2.5D isometric views — only the injected <see cref="IProjection"/> differs.
/// </summary>
public sealed class SceneRenderer
{
    private const double WallHeightMetres = 2.6;

    private static readonly Typeface LabelFace =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface PillFace =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public void Render(DrawingContext dc, RenderContext ctx)
    {
        bool iso = ctx.ViewMode == ViewMode.Isometric;

        if (ctx.ShowGrid) DrawGrid(dc, ctx);
        DrawRooms(dc, ctx);
        if (ctx.ShowHeatmap && ctx.Coverage != null) DrawHeatmap(dc, ctx);
        DrawWalls(dc, ctx, iso);
        DrawOpenings(dc, ctx, iso);
        DrawFurniture(dc, ctx, iso);
        if (ctx.ShowCones) DrawSensorCoverage(dc, ctx, iso);
        DrawSensors(dc, ctx, iso);
        DrawRoomPills(dc, ctx, iso);
        DrawWallPreview(dc, ctx);
    }

    private static Point P(IProjection proj, Vec2 world, double height = 0)
    {
        var s = proj.WorldToScreen(world, height);
        return new Point(s.X, s.Y);
    }

    // ---- Grid ---------------------------------------------------------------

    private void DrawGrid(DrawingContext dc, RenderContext ctx)
    {
        var (min, max) = ctx.Plan.GetBounds();
        var pen = new Pen(Palette.Brush(Palette.GridColor), 1);
        pen.Freeze();

        const double step = 1.0;
        for (double x = Math.Floor(min.X); x <= max.X; x += step)
            dc.DrawLine(pen, P(ctx.Projection, new Vec2(x, min.Y)), P(ctx.Projection, new Vec2(x, max.Y)));
        for (double y = Math.Floor(min.Y); y <= max.Y; y += step)
            dc.DrawLine(pen, P(ctx.Projection, new Vec2(min.X, y)), P(ctx.Projection, new Vec2(max.X, y)));
    }

    // ---- Rooms --------------------------------------------------------------

    private void DrawRooms(DrawingContext dc, RenderContext ctx)
    {
        foreach (var room in ctx.Plan.Rooms)
        {
            if (room.Polygon.Count < 3) continue;
            var geom = PolygonGeometry(ctx.Projection, room.Polygon);
            var stroke = new Pen(Palette.Brush(Palette.WallEdgeColor, 0.5), 1);
            stroke.Freeze();
            dc.DrawGeometry(Palette.Brush(Palette.RoomColor(room), 0.95), stroke, geom);

            if (ctx.IsSelected(room))
                dc.DrawGeometry(null, SelectionPen(2), geom);
        }
    }

    private static StreamGeometry PolygonGeometry(IProjection proj, IReadOnlyList<Vec2> world)
    {
        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            g.BeginFigure(P(proj, world[0]), true, true);
            for (int i = 1; i < world.Count; i++)
                g.LineTo(P(proj, world[i]), true, false);
        }
        geom.Freeze();
        return geom;
    }

    // ---- Heatmap (warm tiles with small gaps) -------------------------------

    // Overlap-view colors: 1 sensor (teal), 2 (blue), 3+ (indigo).
    private static readonly Color[] OverlapColors =
    {
        Color.FromRgb(0x9A, 0xD8, 0xCB), Color.FromRgb(0x6F, 0xA8, 0xE0), Color.FromRgb(0x5B, 0x57, 0xD6)
    };

    private void DrawHeatmap(DrawingContext dc, RenderContext ctx)
    {
        var grid = ctx.Coverage!;
        double inset = grid.CellSize * 0.08;

        Point[] CellQuad(int col, int row)
        {
            double ox = grid.Origin.X + col * grid.CellSize + inset;
            double oy = grid.Origin.Y + row * grid.CellSize + inset;
            double s = grid.CellSize - inset * 2;
            return new[]
            {
                P(ctx.Projection, new Vec2(ox, oy)), P(ctx.Projection, new Vec2(ox + s, oy)),
                P(ctx.Projection, new Vec2(ox + s, oy + s)), P(ctx.Projection, new Vec2(ox, oy + s))
            };
        }

        if (ctx.ShowRedundancy)
        {
            // Bucket floor cells by overlap count (0..3+) and fill each bucket once.
            var buckets = new StreamGeometry[4];
            var gcx = new StreamGeometryContext[4];
            for (int i = 0; i < 4; i++) { buckets[i] = new StreamGeometry(); gcx[i] = buckets[i].Open(); }

            for (int col = 0; col < grid.Columns; col++)
            for (int row = 0; row < grid.Rows; row++)
            {
                if (!grid.IsFloor(col, row)) continue;
                int b = Math.Min(3, grid.CoverCount(col, row));
                var q = CellQuad(col, row);
                gcx[b].BeginFigure(q[0], true, true);
                gcx[b].LineTo(q[1], false, false); gcx[b].LineTo(q[2], false, false); gcx[b].LineTo(q[3], false, false);
            }
            for (int i = 0; i < 4; i++) { gcx[i].Close(); buckets[i].Freeze(); }

            // Only shade where sensors actually overlap; leave uncovered floor clean.
            dc.DrawGeometry(Palette.Brush(OverlapColors[0], 0.5), null, buckets[1]);
            dc.DrawGeometry(Palette.Brush(OverlapColors[1], 0.55), null, buckets[2]);
            dc.DrawGeometry(Palette.Brush(OverlapColors[2], 0.6), null, buckets[3]);
            return;
        }

        // Coverage view: only the COVERED floor is tinted (graded by confidence) plus a light hint for
        // partial. Uncovered floor is left clean so the imported plan / room colours stay readable.
        var geoms = new StreamGeometry[4];   // 0..2 covered bands (low..high), 3 partial
        var ctxs = new StreamGeometryContext[4];
        for (int i = 0; i < 4; i++) { geoms[i] = new StreamGeometry(); ctxs[i] = geoms[i].Open(); }

        for (int col = 0; col < grid.Columns; col++)
        for (int row = 0; row < grid.Rows; row++)
        {
            if (!grid.IsFloor(col, row)) continue;
            var state = grid[col, row];
            if (state == CoverageState.Uncovered) continue; // no red shade — keep the floor clean
            int bucket = state == CoverageState.Covered
                ? (grid.Confidence(col, row) >= 0.8 ? 2 : grid.Confidence(col, row) >= 0.6 ? 1 : 0)
                : 3; // partial
            var q = CellQuad(col, row);
            ctxs[bucket].BeginFigure(q[0], true, true);
            ctxs[bucket].LineTo(q[1], false, false); ctxs[bucket].LineTo(q[2], false, false); ctxs[bucket].LineTo(q[3], false, false);
        }
        for (int i = 0; i < 4; i++) { ctxs[i].Close(); geoms[i].Freeze(); }

        dc.DrawGeometry(Palette.Brush(Palette.PartialColor, 0.35), null, geoms[3]);
        dc.DrawGeometry(Palette.Brush(Palette.CoveredColor, 0.28), null, geoms[0]); // low confidence
        dc.DrawGeometry(Palette.Brush(Palette.CoveredColor, 0.40), null, geoms[1]); // medium
        dc.DrawGeometry(Palette.Brush(Palette.CoveredColor, 0.52), null, geoms[2]); // high
    }

    // ---- Walls (frosted, translucent) ---------------------------------------

    private void DrawWalls(DrawingContext dc, RenderContext ctx, bool iso)
    {
        var (min, max) = ctx.Plan.GetBounds(0);
        var planCenter = (min + max) * 0.5;
        double centerScreenY = ctx.Projection.WorldToScreen(planCenter).Y;

        foreach (var wall in ctx.Plan.Walls)
        {
            if (iso)
            {
                var mid = (wall.Start + wall.End) * 0.5;
                bool front = ctx.Projection.WorldToScreen(mid).Y > centerScreenY;
                double height = front ? 0.4 : WallHeightMetres;
                double opacity = front ? 0.35 : 0.62;

                var b0 = P(ctx.Projection, wall.Start);
                var b1 = P(ctx.Projection, wall.End);
                var t0 = P(ctx.Projection, wall.Start, height);
                var t1 = P(ctx.Projection, wall.End, height);
                var quad = new StreamGeometry();
                using (var g = quad.Open())
                {
                    g.BeginFigure(b0, true, true);
                    g.LineTo(b1, true, false);
                    g.LineTo(t1, true, false);
                    g.LineTo(t0, true, false);
                }
                quad.Freeze();
                dc.DrawGeometry(Palette.Brush(Palette.WallColor, opacity),
                    new Pen(Palette.Brush(Palette.WallEdgeColor, 0.8), 1), quad);
            }
            else
            {
                var pen = new Pen(Palette.Brush(Palette.WallEdgeColor), 6)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                pen.Freeze();
                dc.DrawLine(pen, P(ctx.Projection, wall.Start), P(ctx.Projection, wall.End));
            }

            if (ctx.IsSelected(wall))
                dc.DrawLine(SelectionPen(3), P(ctx.Projection, wall.Start), P(ctx.Projection, wall.End));
        }
    }

    // ---- Doors & windows ----------------------------------------------------

    private static readonly Color DoorColor = Color.FromRgb(0xB0, 0x8A, 0x5E);
    private static readonly Color WindowColor = Color.FromRgb(0x6E, 0x9A, 0xD8);

    private void DrawOpenings(DrawingContext dc, RenderContext ctx, bool iso)
    {
        foreach (var op in ctx.Plan.Openings)
        {
            var wall = FindWall(ctx.Plan, op.WallId);
            if (wall == null) continue;
            double len = wall.Length;
            if (len < 1e-6) continue;

            var dir = (wall.End - wall.Start) / len;
            var perp = new Vec2(-dir.Y, dir.X);
            var a = op.Center - dir * (op.Width * 0.5);
            var b = op.Center + dir * (op.Width * 0.5);
            bool selected = ctx.IsSelected(op);
            var col = op.Kind == OpeningKind.Door ? DoorColor : WindowColor;

            if (iso)
            {
                var post = new Pen(Palette.Brush(col), 2);
                post.Freeze();
                dc.DrawLine(new Pen(Palette.Brush(col), 3), P(ctx.Projection, a), P(ctx.Projection, b)); // threshold
                dc.DrawLine(post, P(ctx.Projection, a), P(ctx.Projection, a, 2.0));
                dc.DrawLine(post, P(ctx.Projection, b), P(ctx.Projection, b, 2.0));
                if (op.Kind == OpeningKind.Window)
                {
                    var bar = new Pen(Palette.Brush(col, 0.7), 1.2);
                    bar.Freeze();
                    dc.DrawLine(bar, P(ctx.Projection, a, 0.9), P(ctx.Projection, b, 0.9));
                    dc.DrawLine(bar, P(ctx.Projection, a, 1.7), P(ctx.Projection, b, 1.7));
                }
                else
                {
                    dc.DrawLine(post, P(ctx.Projection, a, 2.0), P(ctx.Projection, b, 2.0)); // lintel
                }
            }
            else
            {
                // Erase the wall across the opening span, then draw the symbol.
                var erase = new Pen(Palette.Brush(Palette.CanvasColor), 7);
                erase.Freeze();
                dc.DrawLine(erase, P(ctx.Projection, a), P(ctx.Projection, b));

                var pen = new Pen(Palette.Brush(col), 1.8);
                pen.Freeze();
                if (op.Kind == OpeningKind.Door)
                {
                    // Door leaf + quarter-circle swing arc, hinged at a.
                    var leaf = a + perp * op.Width;
                    dc.DrawLine(pen, P(ctx.Projection, a), P(ctx.Projection, leaf));
                    var arc = new StreamGeometry();
                    using (var g = arc.Open())
                    {
                        g.BeginFigure(P(ctx.Projection, b), false, false);
                        for (int i = 1; i <= 10; i++)
                        {
                            double th = Math.PI / 2 * i / 10;
                            var pt = a + dir.Rotate(th) * op.Width;
                            g.LineTo(P(ctx.Projection, pt), true, false);
                        }
                    }
                    arc.Freeze();
                    dc.DrawGeometry(null, new Pen(Palette.Brush(col, 0.6), 1), arc);
                }
                else
                {
                    var off = perp * 0.06;
                    dc.DrawLine(pen, P(ctx.Projection, a + off), P(ctx.Projection, b + off));
                    dc.DrawLine(pen, P(ctx.Projection, a - off), P(ctx.Projection, b - off));
                }
            }

            if (selected)
                dc.DrawEllipse(null, SelectionPen(2), P(ctx.Projection, op.Center), 9, 9);
        }
    }

    private static Wall? FindWall(FloorPlan plan, Guid id)
    {
        foreach (var w in plan.Walls)
            if (w.Id == id) return w;
        return null;
    }

    // ---- Furniture (ivory, shaded, soft ground shadow) ----------------------

    private readonly FurnitureRenderer _furniture = new();

    private void DrawFurniture(DrawingContext dc, RenderContext ctx, bool iso)
    {
        foreach (var f in ctx.Plan.Furniture)
        {
            var corners = FootprintCorners(f);

            // Soft ground shadow (offset slightly toward the viewer)
            var shadow = new StreamGeometry();
            using (var g = shadow.Open())
            {
                var s0 = P(ctx.Projection, corners[0]); s0.Y += 5;
                g.BeginFigure(s0, true, true);
                for (int i = 1; i < corners.Count; i++)
                {
                    var s = P(ctx.Projection, corners[i]); s.Y += 5;
                    g.LineTo(s, true, false);
                }
            }
            shadow.Freeze();
            dc.DrawGeometry(Palette.Brush(Palette.ShadowColor, 0.10), null, shadow);

            // Illustrated model (3D prisms in iso, plan symbol in 2D)
            _furniture.Draw(dc, ctx.Projection, f, iso);

            if (ctx.IsSelected(f))
                dc.DrawGeometry(null, SelectionPen(2), PolygonGeometry(ctx.Projection, corners));
        }
    }

    private static List<Vec2> FootprintCorners(FurnitureItem f)
    {
        var hf = f.Size * 0.5;
        var local = new[]
        {
            new Vec2(-hf.X, -hf.Y), new Vec2(hf.X, -hf.Y), new Vec2(hf.X, hf.Y), new Vec2(-hf.X, hf.Y)
        };
        double rot = f.RotationDegrees * Math.PI / 180.0;
        var result = new List<Vec2>(4);
        foreach (var p in local) result.Add(f.Position + p.Rotate(rot));
        return result;
    }

    // ---- Sensor coverage: soft pyramid + footprint + measurements -----------

    private void DrawSensorCoverage(DrawingContext dc, RenderContext ctx, bool iso)
    {
        foreach (var s in ctx.Plan.Sensors)
        {
            var corners = SensorFootprint.FloorCorners(s);
            bool selected = ReferenceEquals(s, ctx.Selected);
            double opac = s.IsSuggestion ? ctx.SuggestionOpacity : 1.0;

            if (iso)
            {
                var apex = P(ctx.Projection, s.Position, s.Height);
                var faceFill = Palette.Brush(Palette.SensorConeColor, 0.14 * opac);
                var faceEdge = new Pen(Palette.Brush(Palette.SensorConeColor, 0.4 * opac), 0.8);
                faceEdge.Freeze();
                for (int i = 0; i < 4; i++)
                {
                    var a = P(ctx.Projection, corners[i]);
                    var b = P(ctx.Projection, corners[(i + 1) % 4]);
                    var tri = new StreamGeometry();
                    using (var g = tri.Open())
                    {
                        g.BeginFigure(apex, true, true);
                        g.LineTo(a, true, false);
                        g.LineTo(b, true, false);
                    }
                    tri.Freeze();
                    dc.DrawGeometry(faceFill, faceEdge, tri);
                }
            }

            // Floor footprint with a crisp dark outline (Butlr's black-bordered rectangle)
            var floorGeom = PolygonGeometry(ctx.Projection, corners);
            dc.DrawGeometry(Palette.Brush(Palette.SensorConeColor, 0.12 * opac),
                new Pen(Palette.Brush(Palette.PillColor, (selected ? 0.85 : 0.35) * opac), selected ? 1.4 : 1.0),
                floorGeom);

            if (selected)
            {
                DrawPixelGrid(dc, ctx.Projection, corners, s.PixelColumns, s.PixelRows);
                DrawMeasurements(dc, ctx, s);
            }
        }
    }

    private void DrawPixelGrid(DrawingContext dc, IProjection proj, Vec2[] c, int cols, int rows)
    {
        var pen = new Pen(Palette.Brush(Palette.SensorColor, 0.14), 0.5);
        pen.Freeze();
        int cx = Math.Min(cols, 32), ry = Math.Min(rows, 24);
        for (int i = 0; i <= cx; i++)
        {
            double t = (double)i / cx;
            dc.DrawLine(pen, P(proj, Lerp(c[0], c[1], t)), P(proj, Lerp(c[3], c[2], t)));
        }
        for (int j = 0; j <= ry; j++)
        {
            double t = (double)j / ry;
            dc.DrawLine(pen, P(proj, Lerp(c[0], c[3], t)), P(proj, Lerp(c[1], c[2], t)));
        }
    }

    /// <summary>Dashed cross from the sensor to the walls of its room, with black distance pills.</summary>
    private void DrawMeasurements(DrawingContext dc, RenderContext ctx, Sensor s)
    {
        if (!TryRoomBounds(ctx.Plan, s.Position, out var min, out var max)) return;

        var p = s.Position;
        var dash = new Pen(Palette.Brush(Palette.PillColor, 0.55), 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) };
        dash.Freeze();

        var targets = new (Vec2 edge, double dist)[]
        {
            (new Vec2(min.X, p.Y), p.X - min.X), // left
            (new Vec2(max.X, p.Y), max.X - p.X), // right
            (new Vec2(p.X, min.Y), p.Y - min.Y), // top
            (new Vec2(p.X, max.Y), max.Y - p.Y), // bottom
        };
        foreach (var (edge, dist) in targets)
        {
            dc.DrawLine(dash, P(ctx.Projection, p), P(ctx.Projection, edge));
            var mid = P(ctx.Projection, (p + edge) * 0.5);
            DrawPill(dc, mid, $"{dist:0.00} m", Palette.PillColor, Palette.PillTextColor, 10);
        }
    }

    private static bool TryRoomBounds(FloorPlan plan, Vec2 point, out Vec2 min, out Vec2 max)
    {
        foreach (var room in plan.Rooms)
        {
            if (room.Polygon.Count < 3 || !GeometryUtils.PointInPolygon(point, room.Polygon)) continue;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var v in room.Polygon)
            {
                minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y);
                maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y);
            }
            min = new Vec2(minX, minY); max = new Vec2(maxX, maxY);
            return true;
        }
        min = max = Vec2.Zero;
        return false;
    }

    private static Vec2 Lerp(Vec2 a, Vec2 b, double t) => a + (b - a) * t;

    // ---- Sensor device (indigo glowing dot) ---------------------------------

    private void DrawSensors(DrawingContext dc, RenderContext ctx, bool iso)
    {
        foreach (var s in ctx.Plan.Sensors)
        {
            double h = iso ? s.Height : 0;
            var center = P(ctx.Projection, s.Position, h);
            double opacity = s.IsSuggestion ? ctx.SuggestionOpacity : 1.0;

            if (iso)
            {
                var floor = P(ctx.Projection, s.Position);
                var drop = new Pen(Palette.Brush(Palette.SensorColor, 0.35 * opacity), 1) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) };
                drop.Freeze();
                dc.DrawLine(drop, center, floor);
                dc.DrawEllipse(Palette.Brush(Palette.SensorColor, 0.18 * opacity), null, floor, 3, 1.5);
            }

            // Soft glow (concentric translucent rings)
            dc.DrawEllipse(Palette.Brush(Palette.SensorGlow, 0.18 * opacity), null, center, 16, 16);
            dc.DrawEllipse(Palette.Brush(Palette.SensorGlow, 0.22 * opacity), null, center, 11, 11);

            var accent = s.IsSuggestion ? Palette.SelectionColor : Palette.SensorColor;
            dc.DrawEllipse(Palette.Brush(Colors.White, opacity), new Pen(Palette.Brush(accent, opacity), 3), center, 8, 8);
            dc.DrawEllipse(Palette.Brush(accent, opacity), null, center, 4, 4);

            if (!s.IsOnline)
            {
                var off = new Pen(Palette.Brush(Palette.UncoveredColor, opacity), 2);
                off.Freeze();
                dc.DrawLine(off, new Point(center.X - 7, center.Y + 7), new Point(center.X + 7, center.Y - 7));
            }

            if (ctx.IsSelected(s))
                dc.DrawEllipse(null, SelectionPen(2), center, 13, 13);
        }
    }

    // ---- Room label pills ---------------------------------------------------

    private void DrawRoomPills(DrawingContext dc, RenderContext ctx, bool iso)
    {
        foreach (var room in ctx.Plan.Rooms)
        {
            if (room.Polygon.Count < 3) continue;
            var anchor = P(ctx.Projection, room.Centroid, iso ? WallHeightMetres * 0.5 : 0);
            DrawPill(dc, anchor, room.Name, Palette.RoomPillColor(room.Type), Colors.White, 11);
        }
    }

    // ---- Pills / preview ----------------------------------------------------

    private void DrawPill(DrawingContext dc, Point center, string text, Color bg, Color fg, double fontSize)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            PillFace, fontSize, Palette.Brush(fg), 1.0);
        double padX = 7, padY = 3;
        double w = ft.Width + padX * 2, hgt = ft.Height + padY * 2;
        var rect = new Rect(center.X - w / 2, center.Y - hgt / 2, w, hgt);
        dc.DrawRoundedRectangle(Palette.Brush(bg, 0.95), null, rect, hgt / 2, hgt / 2);
        dc.DrawText(ft, new Point(rect.X + padX, rect.Y + padY));
    }

    private void DrawWallPreview(DrawingContext dc, RenderContext ctx)
    {
        if (ctx.WallPreview is not { } wp) return;
        var pen = new Pen(Palette.Brush(Palette.SelectionColor), 3)
        {
            DashStyle = DashStyles.Dash, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        dc.DrawLine(pen, P(ctx.Projection, wp.start), P(ctx.Projection, wp.end));
        var mid = P(ctx.Projection, (wp.start + wp.end) * 0.5);
        DrawPill(dc, new Point(mid.X, mid.Y - 14), $"{wp.start.DistanceTo(wp.end):0.00} m", Palette.PillColor, Colors.White, 10);
    }

    private static Pen SelectionPen(double thickness)
    {
        var pen = new Pen(Palette.Brush(Palette.SelectionColor), thickness);
        pen.Freeze();
        return pen;
    }
}
