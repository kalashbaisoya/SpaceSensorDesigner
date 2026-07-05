namespace SpaceSensorDesigner.Core.Models;

/// <summary>Semantic room category. Drives the color coding in the designer.</summary>
public enum RoomType
{
    Bedroom,
    LivingRoom,
    Kitchen,
    Bathroom,
    Balcony,
    Hallway,
    Other
}

/// <summary>Furniture kinds available in the drag-and-drop library.</summary>
public enum FurnitureType
{
    Bed,
    Table,
    Sofa,
    Chair,
    KitchenCounter,
    Toilet,
    Sink,
    Wardrobe,
    Desk,
    Rug
}

/// <summary>Sensor kinds. Each has different default range / field of view presets.</summary>
public enum SensorType
{
    Activity,
    Headcount,
    Motion,
    Presence
}

/// <summary>An aperture cut into a wall.</summary>
public enum OpeningKind
{
    /// <summary>An open doorway — lets thermal line-of-sight pass through.</summary>
    Door,

    /// <summary>A window — rendered, but glass is opaque to long-wave IR, so it still blocks the sensor.</summary>
    Window
}

/// <summary>The currently active design tool in the left palette.</summary>
public enum ToolType
{
    Select,
    Wall,
    Door,
    Window,
    Furniture,
    Sensor,
    Room
}

/// <summary>Rendering mode of the central canvas.</summary>
public enum ViewMode
{
    TopDown2D,
    Isometric
}

/// <summary>Result of the per-cell coverage evaluation, used to color the heatmap.</summary>
public enum CoverageState
{
    /// <summary>No sensor reaches this cell (range/FOV) — rendered red.</summary>
    Uncovered = 0,

    /// <summary>A sensor is in range and FOV but a wall blocks line of sight — rendered yellow.</summary>
    Partial = 1,

    /// <summary>At least one sensor has clear line of sight — rendered green.</summary>
    Covered = 2
}
