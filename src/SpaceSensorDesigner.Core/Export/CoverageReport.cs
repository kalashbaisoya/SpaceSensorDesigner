using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Geometry;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Export;

public sealed record FloorReport(string Name, double CoveredPercent, int SensorCount, IReadOnlyList<RoomCoverage> Rooms);

/// <summary>One line in the sensor install schedule (BOM detail).</summary>
public sealed record SensorRow(
    string Floor, string Name, string Model,
    double X, double Y, double Height,
    double HFov, double VFov, double Orientation,
    double FootprintW, double FootprintD);

public sealed record BomRow(string Model, int Count);

public sealed record ProjectReport(
    string ProjectName, DateTime GeneratedUtc,
    double OverallCoveredPercent, int TotalSensors,
    IReadOnlyList<FloorReport> Floors, IReadOnlyList<SensorRow> Sensors, IReadOnlyList<BomRow> Bom);

/// <summary>
/// Builds a coverage + install report for a whole project: per-floor coverage, a sensor schedule,
/// and a bill of materials (sensor counts by model). Renders to CSV (the schedule) or a
/// self-contained HTML document (print → PDF from any browser, no PDF library needed).
/// </summary>
public static class CoverageReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string ModelName(Sensor s) =>
        $"MLX90640 {s.HorizontalFovDegrees:0}°×{s.VerticalFovDegrees:0}°";

    public static ProjectReport Build(DesignProject project)
    {
        var floors = new List<FloorReport>();
        var sensors = new List<SensorRow>();
        long coveredCells = 0, floorCells = 0;

        foreach (var floor in project.Floors)
        {
            var result = CoverageCalculator.Compute(floor);
            var s = result.Summary;
            coveredCells += s.CoveredCells;
            floorCells += s.CoveredCells + s.PartialCells + s.UncoveredCells;

            var real = floor.Sensors.Where(x => !x.IsSuggestion).ToList();
            floors.Add(new FloorReport(floor.Name, s.CoveredPercent, real.Count, result.Rooms.ToList()));

            foreach (var sensor in real)
            {
                sensors.Add(new SensorRow(
                    floor.Name, sensor.Name, ModelName(sensor),
                    sensor.Position.X, sensor.Position.Y, sensor.Height,
                    sensor.HorizontalFovDegrees, sensor.VerticalFovDegrees, sensor.OrientationDegrees,
                    2 * SensorFootprint.HalfWidth(sensor), 2 * SensorFootprint.HalfDepth(sensor)));
            }
        }

        var bom = sensors
            .GroupBy(r => r.Model)
            .Select(g => new BomRow(g.Key, g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

        double overall = floorCells > 0 ? 100.0 * coveredCells / floorCells : 0;
        return new ProjectReport(project.Name, DateTime.UtcNow, overall, sensors.Count, floors, sensors, bom);
    }

    // ---- CSV (sensor install schedule) -------------------------------------

    public static string ToCsv(ProjectReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Floor,Sensor,Model,X (m),Y (m),Height (m),H-FOV (deg),V-FOV (deg),Orientation (deg),Footprint W (m),Footprint D (m)");
        foreach (var s in r.Sensors)
        {
            sb.Append(Csv(s.Floor)).Append(',')
              .Append(Csv(s.Name)).Append(',')
              .Append(Csv(s.Model)).Append(',')
              .Append(N(s.X)).Append(',').Append(N(s.Y)).Append(',').Append(N(s.Height)).Append(',')
              .Append(N(s.HFov)).Append(',').Append(N(s.VFov)).Append(',').Append(N(s.Orientation)).Append(',')
              .Append(N(s.FootprintW)).Append(',').Append(N(s.FootprintD)).AppendLine();
        }
        return sb.ToString();
    }

    private static string N(double v) => v.ToString("0.##", Inv);

    private static string Csv(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        return field;
    }

    // ---- HTML report -------------------------------------------------------

    /// <summary>
    /// Renders a self-contained HTML report. <paramref name="floorPngBase64"/> (optional) embeds a
    /// snapshot of the current floor under a "Floor plan" heading.
    /// </summary>
    public static string ToHtml(ProjectReport r, string? floorPngBase64 = null, string? floorCaption = null)
    {
        var sb = new StringBuilder();
        sb.Append(@"<!doctype html><html><head><meta charset=""utf-8""><title>");
        sb.Append(Html(r.ProjectName)).Append(" — Coverage Report</title><style>");
        sb.Append(@"
:root{--ink:#33302b;--muted:#8a8172;--line:#ece7de;--accent:#f2726f;--good:#6fc7b6;--card:#fff;--bg:#f7f5f0;}
*{box-sizing:border-box}body{font-family:'Segoe UI',system-ui,sans-serif;color:var(--ink);background:var(--bg);margin:0;padding:32px;}
.wrap{max-width:900px;margin:0 auto}
h1{font-size:26px;margin:0 0 4px}h2{font-size:16px;margin:28px 0 10px;color:var(--ink)}
.sub{color:var(--muted);font-size:13px;margin-bottom:18px}
.cards{display:flex;gap:12px;flex-wrap:wrap}
.stat{flex:1;min-width:150px;background:var(--card);border:1px solid var(--line);border-radius:14px;padding:16px}
.stat .n{font-size:28px;font-weight:700}.stat .l{color:var(--muted);font-size:12px}
table{width:100%;border-collapse:collapse;background:var(--card);border:1px solid var(--line);border-radius:12px;overflow:hidden;font-size:13px}
th,td{text-align:left;padding:9px 12px;border-bottom:1px solid var(--line)}th{background:var(--bg);color:var(--muted);font-weight:600}
tr:last-child td{border-bottom:none}
.bar{height:8px;background:var(--line);border-radius:5px;overflow:hidden}.bar>span{display:block;height:100%;background:var(--good)}
.floor{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:16px;margin-bottom:12px}
.row{display:flex;justify-content:space-between;font-size:13px;margin:6px 0 3px}
img{max-width:100%;border:1px solid var(--line);border-radius:12px;margin-top:8px}
.foot{color:var(--muted);font-size:11px;margin-top:26px;text-align:center}
");
        sb.Append("</style></head><body><div class=\"wrap\">");

        sb.Append("<h1>").Append(Html(r.ProjectName)).Append("</h1>");
        sb.Append("<div class=\"sub\">Coverage &amp; install report · generated ")
          .Append(r.GeneratedUtc.ToLocalTime().ToString("d MMM yyyy, HH:mm")).Append("</div>");

        // Summary stat cards
        sb.Append("<div class=\"cards\">");
        Stat(sb, r.OverallCoveredPercent.ToString("0.#", Inv) + "%", "Overall coverage");
        Stat(sb, r.Floors.Count.ToString(Inv), r.Floors.Count == 1 ? "Floor" : "Floors");
        Stat(sb, r.TotalSensors.ToString(Inv), "MLX90640 sensors");
        sb.Append("</div>");

        // Optional floor snapshot
        if (!string.IsNullOrEmpty(floorPngBase64))
        {
            sb.Append("<h2>Floor plan</h2>");
            if (!string.IsNullOrEmpty(floorCaption)) sb.Append("<div class=\"sub\">").Append(Html(floorCaption!)).Append("</div>");
            sb.Append("<img alt=\"floor plan\" src=\"data:image/png;base64,").Append(floorPngBase64).Append("\"/>");
        }

        // Per-floor coverage
        sb.Append("<h2>Coverage by floor</h2>");
        foreach (var f in r.Floors)
        {
            sb.Append("<div class=\"floor\">");
            sb.Append("<div class=\"row\"><b>").Append(Html(f.Name)).Append("</b><span>")
              .Append(f.CoveredPercent.ToString("0.#", Inv)).Append("% · ").Append(f.SensorCount).Append(" sensor(s)</span></div>");
            sb.Append("<div class=\"bar\"><span style=\"width:").Append(Clamp(f.CoveredPercent)).Append("%\"></span></div>");
            foreach (var room in f.Rooms)
            {
                sb.Append("<div class=\"row\"><span>").Append(Html(room.Name)).Append("</span><span>")
                  .Append(room.CoveredPercent.ToString("0", Inv)).Append("%</span></div>");
                sb.Append("<div class=\"bar\"><span style=\"width:").Append(Clamp(room.CoveredPercent)).Append("%\"></span></div>");
            }
            sb.Append("</div>");
        }

        // Bill of materials
        sb.Append("<h2>Bill of materials</h2><table><tr><th>Model</th><th>Qty</th></tr>");
        foreach (var b in r.Bom)
            sb.Append("<tr><td>").Append(Html(b.Model)).Append("</td><td>").Append(b.Count).Append("</td></tr>");
        if (r.Bom.Count == 0) sb.Append("<tr><td colspan=\"2\">No sensors placed.</td></tr>");
        sb.Append("</table>");

        // Sensor install schedule
        sb.Append("<h2>Install schedule</h2><table><tr><th>Floor</th><th>Sensor</th><th>Model</th><th>Pos (m)</th><th>Height</th><th>Footprint</th></tr>");
        foreach (var s in r.Sensors)
        {
            sb.Append("<tr><td>").Append(Html(s.Floor)).Append("</td><td>").Append(Html(s.Name)).Append("</td><td>")
              .Append(Html(s.Model)).Append("</td><td>").Append(N(s.X)).Append(", ").Append(N(s.Y)).Append("</td><td>")
              .Append(N(s.Height)).Append(" m</td><td>").Append(N(s.FootprintW)).Append(" × ").Append(N(s.FootprintD)).Append(" m</td></tr>");
        }
        if (r.Sensors.Count == 0) sb.Append("<tr><td colspan=\"6\">No sensors placed.</td></tr>");
        sb.Append("</table>");

        sb.Append("<div class=\"foot\">SpaceSensor Designer · Melexis MLX90640 thermal coverage · generated locally</div>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static void Stat(StringBuilder sb, string n, string label)
        => sb.Append("<div class=\"stat\"><div class=\"n\">").Append(Html(n)).Append("</div><div class=\"l\">").Append(Html(label)).Append("</div></div>");

    private static string Clamp(double p) => Math.Clamp(p, 0, 100).ToString("0.#", Inv);

    private static string Html(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
