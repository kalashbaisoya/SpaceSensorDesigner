using System.Collections.Generic;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Catalog;

/// <summary>Template describing a draggable furniture item in the library panel.</summary>
public sealed record FurnitureTemplate(FurnitureType Type, string DisplayName, double Width, double Depth, string Glyph);

/// <summary>
/// Template describing a draggable thermal sensor in the library panel, with its FOV / resolution
/// defaults (modelled on MLX90640 variants).
/// </summary>
public sealed record SensorTemplate(
    SensorType Type, string DisplayName,
    double HorizontalFovDegrees, double VerticalFovDegrees,
    double Height, int PixelColumns, int PixelRows, string Glyph);

/// <summary>
/// Default content for the drag-and-drop library. Sizes are in metres; glyphs are used as
/// lightweight icons so the app has no binary asset dependencies.
/// </summary>
public static class LibraryCatalog
{
    public static IReadOnlyList<FurnitureTemplate> Furniture { get; } = new List<FurnitureTemplate>
    {
        new(FurnitureType.Bed,            "Bed",            1.6, 2.0, "🛏"),
        new(FurnitureType.Sofa,           "Sofa",           2.0, 0.9, "🛋"),
        new(FurnitureType.Table,          "Table",          1.2, 0.8, "🪑"),
        new(FurnitureType.Chair,          "Chair",          0.5, 0.5, "🪑"),
        new(FurnitureType.Desk,           "Desk",           1.4, 0.7, "🖥"),
        new(FurnitureType.Wardrobe,       "Wardrobe",       1.2, 0.6, "🚪"),
        new(FurnitureType.KitchenCounter, "Kitchen Counter",2.4, 0.6, "🍳"),
        new(FurnitureType.Sink,           "Sink",           0.6, 0.5, "🚰"),
        new(FurnitureType.Toilet,         "Toilet",         0.5, 0.7, "🚽"),
        new(FurnitureType.Rug,            "Rug",            2.0, 1.4, "▧"),
    };

    // Both entries are the Melexis MLX90640 (32×24 thermal array); the two variants differ only in
    // the lens FOV. Wide 110°×75° (BAB) and standard 55°×35° (BAA).
    public static IReadOnlyList<SensorTemplate> Sensors { get; } = new List<SensorTemplate>
    {
        new(SensorType.Activity,  "MLX90640 · 110° Wide", 110, 75, 2.7, 32, 24, "◨"),
        new(SensorType.Headcount, "MLX90640 · 55° Std",    55, 35, 2.7, 32, 24, "◧"),
        new(SensorType.Presence,  "MLX90640 · Ceiling 90°",90, 65, 2.4, 32, 24, "▦"),
    };

    public static FurnitureItem CreateFurniture(FurnitureTemplate t, Vec2 position) => new()
    {
        Type = t.Type,
        Name = t.DisplayName,
        Position = position,
        Size = new Vec2(t.Width, t.Depth)
    };

    public static Sensor CreateSensor(SensorTemplate t, Vec2 position) => new()
    {
        Type = t.Type,
        Name = t.DisplayName,
        Position = position,
        Height = t.Height,
        HorizontalFovDegrees = t.HorizontalFovDegrees,
        VerticalFovDegrees = t.VerticalFovDegrees,
        PixelColumns = t.PixelColumns,
        PixelRows = t.PixelRows
    };
}
