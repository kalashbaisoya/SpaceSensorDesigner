using SpaceSensorDesigner.Core.Catalog;
using SpaceSensorDesigner.Core.Serialization;
using Xunit;

namespace SpaceSensorDesigner.Tests;

public class ImportAndTemplateTests
{
    [Theory]
    [InlineData("studio")]
    [InlineData("onebed")]
    [InlineData("twobed")]
    public void Template_ProducesRoomsAndWalls(string key)
    {
        var plan = PlanTemplates.Create(key);

        Assert.NotEmpty(plan.Rooms);
        Assert.NotEmpty(plan.Walls);
        Assert.All(plan.Rooms, r => Assert.True(r.Polygon.Count >= 4));
    }

    [Fact]
    public void Dxf_ParsesLineEntities()
    {
        string[] dxf =
        {
            "0", "SECTION", "2", "ENTITIES",
            "0", "LINE", "10", "0", "20", "0", "11", "4", "21", "0",
            "0", "LINE", "10", "4", "20", "0", "11", "4", "21", "3",
            "0", "ENDSEC", "0", "EOF"
        };

        var walls = DxfImporter.Parse(dxf);

        Assert.Equal(2, walls.Count);
        Assert.Equal(0, walls[0].Start.X, 6);
        Assert.Equal(4, walls[0].End.X, 6);
        Assert.Equal(3, walls[1].End.Y, 6);
    }

    [Fact]
    public void Dxf_ParsesClosedPolyline()
    {
        // A closed triangle LWPOLYLINE → 3 wall segments.
        string[] dxf =
        {
            "0", "LWPOLYLINE", "90", "3", "70", "1",
            "10", "0", "20", "0",
            "10", "3", "20", "0",
            "10", "3", "20", "2",
            "0", "EOF"
        };

        var walls = DxfImporter.Parse(dxf);

        Assert.Equal(3, walls.Count); // 2 edges + 1 closing edge
    }

    [Fact]
    public void Dxf_AppliesScale()
    {
        string[] dxf = { "0", "LINE", "10", "0", "20", "0", "11", "1000", "21", "0", "0", "EOF" };

        var walls = DxfImporter.Parse(dxf, 0.001); // mm → m

        Assert.Single(walls);
        Assert.Equal(1.0, walls[0].End.X, 6);
    }
}
