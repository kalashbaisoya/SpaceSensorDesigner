using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Models;
using Xunit;

namespace SpaceSensorDesigner.Tests;

public class CoverageTests
{
    private static FloorPlan RoomWithSensor(Sensor sensor, params Wall[] walls)
    {
        var plan = new FloorPlan { CellSize = 0.25 };
        plan.Rooms.Add(new Room
        {
            Name = "Test", Type = RoomType.Other,
            Polygon = { new Vec2(0, 0), new Vec2(6, 0), new Vec2(6, 6), new Vec2(0, 6) }
        });
        plan.Sensors.Add(sensor);
        plan.Walls.AddRange(walls);
        return plan;
    }

    [Fact]
    public void CeilingSensor_WithClearLineOfSight_CoversFootprint()
    {
        // Wide MLX90640 at 2.7 m → ~7.7 × 4.1 m footprint centred in a 6×6 room.
        var plan = RoomWithSensor(new Sensor
        {
            Position = new Vec2(3, 3), Height = 2.7,
            HorizontalFovDegrees = 110, VerticalFovDegrees = 75
        });

        var summary = CoverageCalculator.Compute(plan).Summary;

        Assert.True(summary.CoveredCells > 0);
        Assert.True(summary.CoveredFraction > 0.4, $"Expected substantial coverage, got {summary.CoveredPercent:0.#}%");
    }

    [Fact]
    public void NarrowSensor_LeavesFarCornersUncovered()
    {
        // A narrow-FOV sensor low in a corner only sees a small patch beneath it.
        var plan = RoomWithSensor(new Sensor
        {
            Position = new Vec2(0.6, 0.6), Height = 1.6,
            HorizontalFovDegrees = 20, VerticalFovDegrees = 20
        });

        var summary = CoverageCalculator.Compute(plan).Summary;

        Assert.True(summary.UncoveredCells > 0, "Far cells should be outside the footprint.");
    }

    [Fact]
    public void WallBetweenSensorAndCell_ProducesPartialCoverage()
    {
        // A wall splitting the room; the footprint spans it but line of sight is blocked beyond it.
        var wall = new Wall { Start = new Vec2(3, 0), End = new Vec2(3, 6) };
        var plan = RoomWithSensor(new Sensor
        {
            Position = new Vec2(1.5, 3), Height = 2.7,
            HorizontalFovDegrees = 110, VerticalFovDegrees = 75
        }, wall);

        var summary = CoverageCalculator.Compute(plan).Summary;

        Assert.True(summary.PartialCells > 0, "Expected wall-blocked partial cells beyond the wall.");
    }

    [Fact]
    public void OfflineSensor_CoversNothing()
    {
        var plan = RoomWithSensor(new Sensor
        {
            Position = new Vec2(3, 3), Height = 2.7,
            HorizontalFovDegrees = 110, VerticalFovDegrees = 75, IsOnline = false
        });

        var summary = CoverageCalculator.Compute(plan).Summary;

        Assert.Equal(0, summary.CoveredCells);
    }
}
