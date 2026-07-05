using SpaceSensorDesigner.Core.Models;
using SpaceSensorDesigner.Core.Serialization;
using Xunit;

namespace SpaceSensorDesigner.Tests;

public class SerializationTests
{
    [Fact]
    public void RoundTrip_PreservesPlan()
    {
        var plan = FloorPlan.CreateSample();
        plan.Name = "Round Trip";

        var json = FloorPlanSerializer.Serialize(plan);
        var restored = FloorPlanSerializer.Deserialize(json);

        Assert.Equal(plan.Name, restored.Name);
        Assert.Equal(plan.Rooms.Count, restored.Rooms.Count);
        Assert.Equal(plan.Walls.Count, restored.Walls.Count);
        Assert.Equal(plan.Furniture.Count, restored.Furniture.Count);
        Assert.Equal(plan.Sensors.Count, restored.Sensors.Count);
    }

    [Fact]
    public void Project_RoundTrip_PreservesFloors()
    {
        var project = new DesignProject { Name = "Care Home" };
        var a = FloorPlan.CreateSample(); a.Name = "Apartment 1";
        var b = FloorPlan.CreateSample(); b.Name = "Apartment 2";
        b.Openings.Add(new Opening { WallId = b.Walls[0].Id, Kind = OpeningKind.Window, Center = new Vec2(1, 0), Width = 1.2 });
        project.Floors.Add(a);
        project.Floors.Add(b);

        var json = ProjectSerializer.Serialize(project);
        var restored = ProjectSerializer.Load(WriteTemp(json));

        Assert.Equal("Care Home", restored.Name);
        Assert.Equal(2, restored.Floors.Count);
        Assert.Equal("Apartment 2", restored.Floors[1].Name);
        Assert.Single(restored.Floors[1].Openings);
        Assert.Equal(OpeningKind.Window, restored.Floors[1].Openings[0].Kind);
    }

    [Fact]
    public void Project_Load_WrapsLegacySingleFloorFile()
    {
        // A legacy file is a bare FloorPlan; loading it must yield a one-floor project.
        var plan = FloorPlan.CreateSample();
        plan.Name = "Legacy Plan";
        var legacyJson = FloorPlanSerializer.Serialize(plan);

        var project = ProjectSerializer.Load(WriteTemp(legacyJson));

        Assert.Single(project.Floors);
        Assert.Equal("Legacy Plan", project.Floors[0].Name);
    }

    private static string WriteTemp(string json)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "ssd_test_" + System.Guid.NewGuid().ToString("N") + ".spacedesign");
        System.IO.File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void RoundTrip_PreservesVec2AndEnums()
    {
        var plan = new FloorPlan();
        plan.Sensors.Add(new Sensor
        {
            Type = SensorType.Headcount,
            Position = new Vec2(1.25, 3.5),
            Height = 2.4,
            HorizontalFovDegrees = 55,
            VerticalFovDegrees = 35,
            PixelColumns = 32,
            PixelRows = 24
        });
        plan.Rooms.Add(new Room { Type = RoomType.Kitchen, Polygon = { new Vec2(0, 0), new Vec2(2, 0), new Vec2(2, 2) } });

        var restored = FloorPlanSerializer.Deserialize(FloorPlanSerializer.Serialize(plan));

        var s = Assert.Single(restored.Sensors);
        Assert.Equal(SensorType.Headcount, s.Type);
        Assert.Equal(1.25, s.Position.X, 6);
        Assert.Equal(3.5, s.Position.Y, 6);
        Assert.Equal(55, s.HorizontalFovDegrees, 6);
        Assert.Equal(35, s.VerticalFovDegrees, 6);
        Assert.Equal(24, s.PixelRows);

        Assert.Equal(RoomType.Kitchen, restored.Rooms[0].Type);
        Assert.Equal(3, restored.Rooms[0].Polygon.Count);
        Assert.Equal(2.0, restored.Rooms[0].Polygon[1].X, 6);
    }
}
