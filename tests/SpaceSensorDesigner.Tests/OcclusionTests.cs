using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Models;
using SpaceSensorDesigner.Core.Optimization;
using Xunit;

namespace SpaceSensorDesigner.Tests;

/// <summary>
/// Proves the physics the user asked about: a sensor can't see through a wall into the next room,
/// so each enclosed room needs its own sensor.
/// </summary>
public class OcclusionTests
{
    private static void AddRoomWithWalls(FloorPlan plan, double x0, double y0, double x1, double y1)
    {
        var c = new[] { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1) };
        plan.Rooms.Add(new Room { Name = $"R{plan.Rooms.Count + 1}", Polygon = { c[0], c[1], c[2], c[3] } });
        for (int i = 0; i < 4; i++) plan.Walls.Add(new Wall { Start = c[i], End = c[(i + 1) % 4] });
    }

    [Fact]
    public void WallBlocksSensorFromCoveringTheNextRoom()
    {
        var plan = new FloorPlan { Name = "two rooms" };
        AddRoomWithWalls(plan, 0, 0, 3, 3);   // left room
        AddRoomWithWalls(plan, 3, 0, 6, 3);   // right room (shares the x=3 wall)

        // One sensor centred in the LEFT room. Its footprint reaches into the right room, but the
        // wall blocks line-of-sight, so the right room must read as (almost) uncovered.
        plan.Sensors.Add(new Sensor
        {
            Position = new Vec2(1.5, 1.5), Height = 2.7,
            HorizontalFovDegrees = 110, VerticalFovDegrees = 75
        });

        var result = CoverageCalculator.Compute(plan);

        Assert.True(result.Rooms[0].CoveredPercent > 80, $"left {result.Rooms[0].CoveredPercent}");
        Assert.True(result.Rooms[1].CoveredPercent < 20, $"right {result.Rooms[1].CoveredPercent}");
    }

    [Fact]
    public void Optimize_PlacesASensorPerEnclosedRoom()
    {
        var plan = new FloorPlan { Name = "four rooms" };
        AddRoomWithWalls(plan, 0, 0, 3, 3);
        AddRoomWithWalls(plan, 3, 0, 6, 3);
        AddRoomWithWalls(plan, 0, 3, 3, 6);
        AddRoomWithWalls(plan, 3, 3, 6, 6);

        var suggestions = SensorOptimizer.Optimize(plan);

        // Four walled-off rooms can't be covered by fewer than four sensors.
        Assert.True(suggestions.Count >= 4, $"expected >= 4 sensors, got {suggestions.Count}");
    }
}
