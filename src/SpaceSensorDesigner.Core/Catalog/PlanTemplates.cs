using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Catalog;

/// <summary>Starter floor-plan templates for the New / Add-Floor flows.</summary>
public static class PlanTemplates
{
    public static readonly string[] Keys = { "studio", "onebed", "twobed" };

    public static string DisplayName(string key) => key switch
    {
        "studio" => "Studio",
        "onebed" => "1-Bedroom",
        "twobed" => "2-Bedroom",
        _ => "Empty"
    };

    public static FloorPlan Create(string key) => key switch
    {
        "studio" => Studio(),
        "onebed" => OneBed(),
        "twobed" => TwoBed(),
        _ => new FloorPlan { Name = "Floor" }
    };

    private static FloorPlan Studio()
    {
        var p = new FloorPlan { Name = "Studio" };
        AddRoom(p, "Living / Bed", RoomType.LivingRoom, 0, 0, 5, 3.4);
        AddRoom(p, "Bathroom", RoomType.Bathroom, 0, 3.4, 2.2, 5);
        AddRoom(p, "Kitchen", RoomType.Kitchen, 2.2, 3.4, 5, 5);
        p.Furniture.Add(new FurnitureItem { Type = FurnitureType.Bed, Name = "Bed", Position = new Vec2(1.2, 1.1), Size = new Vec2(1.6, 2.0) });
        p.Furniture.Add(new FurnitureItem { Type = FurnitureType.Sofa, Name = "Sofa", Position = new Vec2(3.6, 0.7), Size = new Vec2(2.0, 0.9) });
        p.Furniture.Add(new FurnitureItem { Type = FurnitureType.Toilet, Name = "Toilet", Position = new Vec2(0.5, 4.6), Size = new Vec2(0.5, 0.7) });
        return p;
    }

    private static FloorPlan OneBed()
    {
        var p = new FloorPlan { Name = "1-Bedroom" };
        AddRoom(p, "Living Room", RoomType.LivingRoom, 0, 0, 4, 4.5);
        AddRoom(p, "Kitchen", RoomType.Kitchen, 4, 0, 7, 2.2);
        AddRoom(p, "Bathroom", RoomType.Bathroom, 4, 2.2, 5.6, 4.5);
        AddRoom(p, "Bedroom", RoomType.Bedroom, 5.6, 2.2, 9, 4.5);
        AddRoom(p, "Hallway", RoomType.Hallway, 7, 0, 9, 2.2);
        p.Furniture.Add(new FurnitureItem { Type = FurnitureType.Sofa, Name = "Sofa", Position = new Vec2(1.4, 3.8), Size = new Vec2(2.0, 0.9) });
        p.Furniture.Add(new FurnitureItem { Type = FurnitureType.Bed, Name = "Bed", Position = new Vec2(7.6, 3.4), Size = new Vec2(1.6, 2.0) });
        p.Furniture.Add(new FurnitureItem { Type = FurnitureType.KitchenCounter, Name = "Counter", Position = new Vec2(5.5, 0.4), Size = new Vec2(2.4, 0.6) });
        return p;
    }

    private static FloorPlan TwoBed()
    {
        var p = new FloorPlan { Name = "2-Bedroom" };
        AddRoom(p, "Living Room", RoomType.LivingRoom, 0, 0, 4.5, 5);
        AddRoom(p, "Kitchen", RoomType.Kitchen, 0, 5, 4.5, 7);
        AddRoom(p, "Bathroom", RoomType.Bathroom, 4.5, 0, 6.4, 2.4);
        AddRoom(p, "Bedroom 1", RoomType.Bedroom, 6.4, 0, 10, 3.5);
        AddRoom(p, "Bedroom 2", RoomType.Bedroom, 4.5, 3.5, 10, 7);
        AddRoom(p, "Hallway", RoomType.Hallway, 4.5, 2.4, 6.4, 3.5);
        p.Furniture.Add(new FurnitureItem { Type = FurnitureType.Sofa, Name = "Sofa", Position = new Vec2(1.6, 4.4), Size = new Vec2(2.0, 0.9) });
        p.Furniture.Add(new FurnitureItem { Type = FurnitureType.Bed, Name = "Bed 1", Position = new Vec2(8.2, 1.2), Size = new Vec2(1.6, 2.0) });
        p.Furniture.Add(new FurnitureItem { Type = FurnitureType.Bed, Name = "Bed 2", Position = new Vec2(8.2, 5.6), Size = new Vec2(1.6, 2.0) });
        return p;
    }

    /// <summary>Adds a rectangular room plus its four perimeter walls.</summary>
    private static void AddRoom(FloorPlan p, string name, RoomType type, double x0, double y0, double x1, double y1)
    {
        p.Rooms.Add(new Room
        {
            Name = name, Type = type,
            Polygon = { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1) }
        });
        p.Walls.Add(new Wall { Start = new Vec2(x0, y0), End = new Vec2(x1, y0) });
        p.Walls.Add(new Wall { Start = new Vec2(x1, y0), End = new Vec2(x1, y1) });
        p.Walls.Add(new Wall { Start = new Vec2(x1, y1), End = new Vec2(x0, y1) });
        p.Walls.Add(new Wall { Start = new Vec2(x0, y1), End = new Vec2(x0, y0) });
    }
}
