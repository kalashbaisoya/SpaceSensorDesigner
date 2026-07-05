using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Models;
using SpaceSensorDesigner.Core.Optimization;
using Xunit;

namespace SpaceSensorDesigner.Tests;

/// <summary>
/// When a plan has walls but no rooms, the floor is the interior of the walls' bounding box —
/// so coverage and Optimize stay inside the traced apartment instead of spilling across the canvas.
/// </summary>
public class WallsAsFloorTests
{
    private static FloorPlan WallsOnly(double w = 6, double h = 4)
    {
        var plan = new FloorPlan { Name = "Walls only" };
        var c = new[] { new Vec2(0, 0), new Vec2(w, 0), new Vec2(w, h), new Vec2(0, h) };
        for (int i = 0; i < 4; i++)
            plan.Walls.Add(new Wall { Start = c[i], End = c[(i + 1) % 4] });
        return plan;
    }

    [Fact]
    public void Floor_IsConfinedToWalls_EvenWithAStraySensorFarAway()
    {
        var plan = WallsOnly(); // 6 × 4 m = 24 m² → ~600 cells at 0.2 m
        // A sensor dropped far outside would balloon the plan bounds; the floor must NOT follow it.
        plan.Sensors.Add(new Sensor
        {
            Position = new Vec2(40, 40), Height = 2.7,
            HorizontalFovDegrees = 110, VerticalFovDegrees = 75
        });

        var result = CoverageCalculator.Compute(plan);

        // Floor stays ~ the 24 m² apartment, not the ~40×40 m ballooned bounds (which would be tens of thousands of cells).
        Assert.InRange(result.Summary.TotalCells, 450, 800);
    }

    [Fact]
    public void Optimize_KeepsSensorsInsideWalls_AndUsesFew()
    {
        var plan = WallsOnly();

        var suggestions = SensorOptimizer.Optimize(plan);

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s => Assert.InRange(s.Position.X, 0.0, 6.0));
        Assert.All(suggestions, s => Assert.InRange(s.Position.Y, 0.0, 4.0));
        // One wide MLX90640 at 2.7 m covers 7.7 × 4.1 m, so a bare 6 × 4 room needs only a couple.
        Assert.True(suggestions.Count <= 3, $"expected a handful of sensors, got {suggestions.Count}");
    }

    [Fact]
    public void EmptyPlan_StillTreatsEverythingAsFloor()
    {
        // No rooms and no walls → the whole default grid is floor (unchanged behaviour).
        var plan = new FloorPlan { Name = "Empty" };
        var result = CoverageCalculator.Compute(plan);
        Assert.True(result.Summary.TotalCells > 0);
    }
}
