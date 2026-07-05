using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Models;
using Xunit;

namespace SpaceSensorDesigner.Tests;

public class OpeningAndOccluderTests
{
    private static FloorPlan RoomWithWall(out Wall wall)
    {
        var plan = new FloorPlan { CellSize = 0.25 };
        plan.Rooms.Add(new Room
        {
            Name = "Test", Type = RoomType.Other,
            Polygon = { new Vec2(0, 0), new Vec2(6, 0), new Vec2(6, 6), new Vec2(0, 6) }
        });
        wall = new Wall { Start = new Vec2(3, 0), End = new Vec2(3, 6) };
        plan.Walls.Add(wall);
        plan.Sensors.Add(new Sensor
        {
            Position = new Vec2(1.5, 3), Height = 2.7,
            HorizontalFovDegrees = 110, VerticalFovDegrees = 75
        });
        return plan;
    }

    [Fact]
    public void OpenDoor_LetsCoveragePassThroughWall()
    {
        var plan = RoomWithWall(out var wall);
        var blocked = CoverageCalculator.Compute(plan).Summary;

        // Add a wide open doorway at the sensor's height on the wall.
        plan.Openings.Add(new Opening
        {
            WallId = wall.Id, Kind = OpeningKind.Door,
            Center = new Vec2(3, 3), Width = 2.0
        });
        var withDoor = CoverageCalculator.Compute(plan).Summary;

        Assert.True(withDoor.CoveredCells > blocked.CoveredCells,
            $"Door should reveal cells beyond the wall ({blocked.CoveredCells} → {withDoor.CoveredCells})");
    }

    [Fact]
    public void Window_DoesNotLetThermalPassThrough()
    {
        var plan = RoomWithWall(out var wall);
        var blocked = CoverageCalculator.Compute(plan).Summary;

        plan.Openings.Add(new Opening
        {
            WallId = wall.Id, Kind = OpeningKind.Window,
            Center = new Vec2(3, 3), Width = 2.0
        });
        var withWindow = CoverageCalculator.Compute(plan).Summary;

        // Glass is opaque to LWIR, so a window must not change coverage.
        Assert.Equal(blocked.CoveredCells, withWindow.CoveredCells);
    }

    [Fact]
    public void Wardrobe_BlocksLineOfSight()
    {
        var plan = new FloorPlan { CellSize = 0.25 };
        plan.Rooms.Add(new Room
        {
            Name = "Test", Type = RoomType.Other,
            Polygon = { new Vec2(0, 0), new Vec2(6, 0), new Vec2(6, 6), new Vec2(0, 6) }
        });
        plan.Sensors.Add(new Sensor
        {
            Position = new Vec2(1.5, 3), Height = 2.7,
            HorizontalFovDegrees = 110, VerticalFovDegrees = 75
        });
        var clear = CoverageCalculator.Compute(plan).Summary;

        // A wardrobe spanning across the room casts a thermal shadow.
        plan.Furniture.Add(new FurnitureItem
        {
            Type = FurnitureType.Wardrobe, Position = new Vec2(3, 3), Size = new Vec2(0.6, 4.0)
        });
        var shadowed = CoverageCalculator.Compute(plan).Summary;

        Assert.True(shadowed.PartialCells > clear.PartialCells,
            "A tall wardrobe should occlude cells behind it.");
    }

    [Fact]
    public void PerRoomCoverage_IsReported()
    {
        var plan = new FloorPlan { CellSize = 0.25 };
        plan.Rooms.Add(new Room { Name = "A", Polygon = { new Vec2(0, 0), new Vec2(3, 0), new Vec2(3, 3), new Vec2(0, 3) } });
        plan.Rooms.Add(new Room { Name = "B", Polygon = { new Vec2(4, 0), new Vec2(7, 0), new Vec2(7, 3), new Vec2(4, 3) } });
        plan.Sensors.Add(new Sensor { Position = new Vec2(1.5, 1.5), Height = 2.7, HorizontalFovDegrees = 110, VerticalFovDegrees = 75 });

        var result = CoverageCalculator.Compute(plan);

        Assert.Equal(2, result.Rooms.Count);
        var a = result.Rooms[0];
        var b = result.Rooms[1];
        Assert.True(a.CoveredFraction > b.CoveredFraction, "Room A has the sensor and should be better covered than B.");
    }
}
