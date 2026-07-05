using System.Collections.Generic;
using SpaceSensorDesigner.Core.Geometry;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Coverage;

/// <summary>
/// Evaluates thermal-sensor coverage over a grid. Each sensor sees a rectangular floor footprint
/// (the base of its downward FOV pyramid); a cell counts as covered when it lies inside that
/// footprint and has clear line of sight to the sensor. Line of sight is blocked by walls (except
/// open doorways) and by tall furniture occluders. Fully local and deterministic.
/// </summary>
public static class CoverageCalculator
{
    /// <summary>
    /// Computes coverage for the whole plan and returns the filled grid, an overall summary, and
    /// per-room breakdown. A cell is:
    ///   Covered   – inside at least one sensor's footprint with clear line of sight;
    ///   Partial   – inside a footprint but every covering sensor is blocked;
    ///   Uncovered – outside every footprint.
    /// Only floor cells (inside a room, or all cells if the plan has no rooms) are counted.
    /// </summary>
    public static CoverageResult Compute(FloorPlan plan)
    {
        var grid = CoverageGrid.ForPlan(plan);
        var obstacles = Obstacles.Build(plan);
        bool hasRooms = plan.Rooms.Count > 0;

        // With no rooms drawn, treat the interior of the traced walls as the floor (so coverage and
        // optimisation stay inside the apartment). With neither rooms nor walls, everything is floor.
        var wallBox = !hasRooms ? plan.WallBounds() : null;
        bool useWalls = wallBox is not null;
        Vec2 wmin = useWalls ? wallBox!.Value.min : default;
        Vec2 wmax = useWalls ? wallBox!.Value.max : default;

        var active = new List<(Sensor sensor, Vec2[] footprint, double reach)>();
        foreach (var s in plan.Sensors)
            if (!s.IsSuggestion && s.IsOnline)
                active.Add((s, SensorFootprint.FloorCorners(s), SensorFootprint.BoundingRadius(s)));

        int covered = 0, partial = 0, uncovered = 0, total = 0, redundant = 0;

        // Per-room accumulators (index matches plan.Rooms).
        var roomTotal = new int[plan.Rooms.Count];
        var roomCovered = new int[plan.Rooms.Count];

        for (int col = 0; col < grid.Columns; col++)
        {
            for (int row = 0; row < grid.Rows; row++)
            {
                var center = grid.CellCenter(col, row);

                int roomIndex = hasRooms ? FindRoom(center, plan.Rooms) : -1;
                bool isFloor = hasRooms
                    ? roomIndex >= 0
                    : !useWalls // no rooms and no walls: the whole grid is floor
                      || (center.X >= wmin.X && center.X <= wmax.X &&
                          center.Y >= wmin.Y && center.Y <= wmax.Y);
                grid.SetFloor(col, row, isFloor);
                if (!isFloor)
                {
                    grid[col, row] = CoverageState.Uncovered;
                    continue;
                }

                var (state, confidence, count) = EvaluateCell(center, active, obstacles);
                grid[col, row] = state;
                grid.SetConfidence(col, row, confidence);
                grid.SetCoverCount(col, row, count);

                total++;
                bool isCovered = state == CoverageState.Covered;
                switch (state)
                {
                    case CoverageState.Covered: covered++; break;
                    case CoverageState.Partial: partial++; break;
                    default: uncovered++; break;
                }
                if (count >= 2) redundant++;

                if (roomIndex >= 0)
                {
                    roomTotal[roomIndex]++;
                    if (isCovered) roomCovered[roomIndex]++;
                }
            }
        }

        var rooms = new List<RoomCoverage>(plan.Rooms.Count);
        for (int i = 0; i < plan.Rooms.Count; i++)
            rooms.Add(new RoomCoverage(plan.Rooms[i].Name, roomTotal[i], roomCovered[i]));

        return new CoverageResult
        {
            Grid = grid,
            Summary = new CoverageSummary(total, covered, partial, uncovered, redundant),
            Rooms = rooms
        };
    }

    /// <summary>
    /// Evaluates one cell: returns its coverage state, the best detection confidence (0..1, higher
    /// nearer a sensor's nadir), and how many sensors see it (for redundancy analysis).
    /// </summary>
    private static (CoverageState state, double confidence, int count) EvaluateCell(
        Vec2 cell, List<(Sensor sensor, Vec2[] footprint, double reach)> sensors, IReadOnlyList<Segment> obstacles)
    {
        bool insideAny = false;
        int count = 0;
        double bestConfidence = 0;

        foreach (var (sensor, footprint, reach) in sensors)
        {
            if (!GeometryUtils.PointInPolygon(cell, footprint)) continue;
            insideAny = true;
            if (!GeometryUtils.HasLineOfSight(sensor.Position, cell, obstacles)) continue;

            count++;
            // Confidence falls off with distance from the sensor's nadir toward the footprint edge.
            double dist = sensor.Position.DistanceTo(cell);
            double conf = reach > 1e-6 ? 1.0 - 0.6 * System.Math.Clamp(dist / reach, 0, 1) : 1.0;
            if (conf > bestConfidence) bestConfidence = conf;
        }

        if (count > 0) return (CoverageState.Covered, bestConfidence, count);
        return (insideAny ? CoverageState.Partial : CoverageState.Uncovered, 0, 0);
    }

    private static int FindRoom(Vec2 point, IReadOnlyList<Room> rooms)
    {
        for (int i = 0; i < rooms.Count; i++)
            if (GeometryUtils.PointInPolygon(point, rooms[i].Polygon))
                return i;
        return -1;
    }
}
