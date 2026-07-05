using System.Collections.Generic;
using System.Windows.Media;
using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.App.Rendering;

/// <summary>
/// Canvas color palette tuned to the warm, soft "Butlr Senior Living" aesthetic:
/// off-white paper, frosted translucent walls, ivory furniture, gentle amber/teal heatmap
/// tiles, and an indigo glowing sensor. Kept in sync with the XAML chrome in Theme.xaml.
/// </summary>
public static class Palette
{
    // Canvas surface + lattice
    public static readonly Color CanvasColor = Color.FromRgb(0xF7, 0xF5, 0xF0); // warm paper
    public static readonly Color GridColor   = Color.FromRgb(0xE7, 0xE2, 0xD8); // faint warm lattice

    // Room floors — warm, soft, per type
    private static readonly Dictionary<RoomType, Color> RoomColors = new()
    {
        [RoomType.Bedroom]    = Color.FromRgb(0xF4, 0xE8, 0xD2), // warm cream
        [RoomType.LivingRoom] = Color.FromRgb(0xE6, 0xEF, 0xDE), // soft sage
        [RoomType.Kitchen]    = Color.FromRgb(0xFA, 0xE6, 0xCF), // warm peach
        [RoomType.Bathroom]   = Color.FromRgb(0xDD, 0xED, 0xEA), // soft aqua
        [RoomType.Balcony]    = Color.FromRgb(0xDD, 0xE8, 0xF3), // soft sky
        [RoomType.Hallway]    = Color.FromRgb(0xF0, 0xEA, 0xDF), // warm greige
        [RoomType.Other]      = Color.FromRgb(0xF0, 0xEA, 0xDF),
    };

    // Room-label pill colors (saturated; white text sits on top)
    private static readonly Dictionary<RoomType, Color> RoomPillColors = new()
    {
        [RoomType.Bedroom]    = Color.FromRgb(0x6C, 0x8B, 0xD6),
        [RoomType.LivingRoom] = Color.FromRgb(0x6F, 0xB1, 0x7A),
        [RoomType.Kitchen]    = Color.FromRgb(0xE7, 0x9A, 0x54),
        [RoomType.Bathroom]   = Color.FromRgb(0x54, 0xB0, 0xB8),
        [RoomType.Balcony]    = Color.FromRgb(0x6E, 0x9A, 0xD8),
        [RoomType.Hallway]    = Color.FromRgb(0x9A, 0x8C, 0xC4),
        [RoomType.Other]      = Color.FromRgb(0x9A, 0x8C, 0xC4),
    };

    // Coverage heatmap — warm, pastel, drawn as tiles with small gaps (Butlr floor look)
    public static readonly Color CoveredColor   = Color.FromRgb(0x6F, 0xC7, 0xB6); // gentle teal-green
    public static readonly Color PartialColor    = Color.FromRgb(0xF3, 0xC4, 0x6B); // warm amber
    public static readonly Color UncoveredColor  = Color.FromRgb(0xEE, 0x9C, 0x7E); // soft coral

    // Walls — frosted, translucent lavender-white
    public static readonly Color WallColor      = Color.FromRgb(0xE7, 0xE4, 0xEE);
    public static readonly Color WallEdgeColor  = Color.FromRgb(0xC9, 0xC4, 0xD6);

    // Furniture — ivory, gently shaded
    public static readonly Color FurnitureTop   = Color.FromRgb(0xF4, 0xEE, 0xE2);
    public static readonly Color FurnitureSide  = Color.FromRgb(0xE4, 0xDA, 0xC8);
    public static readonly Color FurnitureStroke= Color.FromRgb(0xD3, 0xC8, 0xB2);
    public static readonly Color ShadowColor    = Color.FromRgb(0x7A, 0x6E, 0x5C);

    // Sensor + coverage volume
    public static readonly Color SensorColor    = Color.FromRgb(0x5B, 0x57, 0xD6); // indigo
    public static readonly Color SensorGlow     = Color.FromRgb(0x8E, 0x8B, 0xEC);
    public static readonly Color SensorConeColor= Color.FromRgb(0x8F, 0xA3, 0xC4); // cool gray-blue volume
    public static readonly Color SelectionColor = Color.FromRgb(0x5B, 0x57, 0xD6);

    // Measurement pills (Butlr's black "4.22 m" labels)
    public static readonly Color PillColor      = Color.FromRgb(0x2E, 0x2A, 0x26);
    public static readonly Color PillTextColor  = Colors.White;

    public static Color RoomColor(Room room)
    {
        if (!string.IsNullOrWhiteSpace(room.ColorOverride))
        {
            try { return (Color)ColorConverter.ConvertFromString(room.ColorOverride)!; }
            catch { /* fall through */ }
        }
        return RoomColor(room.Type);
    }

    public static Color RoomColor(RoomType type)
        => RoomColors.TryGetValue(type, out var c) ? c : RoomColors[RoomType.Other];

    public static Color RoomPillColor(RoomType type)
        => RoomPillColors.TryGetValue(type, out var c) ? c : RoomPillColors[RoomType.Other];

    public static Color CoverageColor(CoverageState state) => state switch
    {
        CoverageState.Covered => CoveredColor,
        CoverageState.Partial => PartialColor,
        _ => UncoveredColor
    };

    /// <summary>Returns a frozen solid brush (frozen brushes are cheap to reuse across renders).</summary>
    public static SolidColorBrush Brush(Color c, double opacity = 1.0)
    {
        var b = new SolidColorBrush(c) { Opacity = opacity };
        b.Freeze();
        return b;
    }
}
