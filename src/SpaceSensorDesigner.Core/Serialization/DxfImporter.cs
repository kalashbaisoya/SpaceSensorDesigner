using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Serialization;

/// <summary>
/// Minimal importer for the AutoCAD DXF ASCII format. It reads <c>LINE</c> and <c>LWPOLYLINE</c>
/// entities and turns them into <see cref="Wall"/> segments — enough to bring a CAD floor plan in
/// as tracing geometry. Coordinates are multiplied by <paramref name="scale"/> (default assumes the
/// DXF is already in metres; pass 0.001 for a millimetre drawing).
/// </summary>
public static class DxfImporter
{
    public static List<Wall> ImportWalls(string path, double scale = 1.0)
        => Parse(File.ReadAllLines(path), scale);

    public static List<Wall> Parse(IReadOnlyList<string> lines, double scale = 1.0)
    {
        var walls = new List<Wall>();

        string? entity = null;
        double x1 = 0, y1 = 0, x2 = 0, y2 = 0;    // LINE
        var verts = new List<Vec2>();               // LWPOLYLINE
        double pendingX = 0; bool haveX = false;
        int flags = 0;

        void Finalize()
        {
            if (entity == "LINE")
            {
                walls.Add(new Wall { Start = new Vec2(x1 * scale, y1 * scale), End = new Vec2(x2 * scale, y2 * scale) });
            }
            else if (entity == "LWPOLYLINE" && verts.Count >= 2)
            {
                for (int i = 0; i < verts.Count - 1; i++)
                    walls.Add(new Wall { Start = verts[i], End = verts[i + 1] });
                if ((flags & 1) != 0)
                    walls.Add(new Wall { Start = verts[^1], End = verts[0] });
            }
            verts = new List<Vec2>();
            haveX = false;
            flags = 0;
        }

        for (int i = 0; i + 1 < lines.Count; i += 2)
        {
            if (!int.TryParse(lines[i].Trim(), out int code)) continue;
            string value = lines[i + 1].Trim();

            if (code == 0) // start of a new entity — finalize the previous one
            {
                Finalize();
                entity = value;
                continue;
            }

            double num = ParseD(value);
            switch (entity)
            {
                case "LINE":
                    if (code == 10) x1 = num;
                    else if (code == 20) y1 = num;
                    else if (code == 11) x2 = num;
                    else if (code == 21) y2 = num;
                    break;
                case "LWPOLYLINE":
                    if (code == 70) flags = (int)num;
                    else if (code == 10) { pendingX = num; haveX = true; }
                    else if (code == 20 && haveX) { verts.Add(new Vec2(pendingX * scale, num * scale)); haveX = false; }
                    break;
            }
        }
        Finalize(); // last entity in the file

        return walls;
    }

    private static double ParseD(string s)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
}
