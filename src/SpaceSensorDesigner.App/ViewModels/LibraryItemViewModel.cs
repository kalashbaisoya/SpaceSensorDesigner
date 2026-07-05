using SpaceSensorDesigner.Core.Catalog;

namespace SpaceSensorDesigner.App.ViewModels;

/// <summary>A draggable entry in the library panel. Wraps either a furniture or sensor template.</summary>
public sealed class LibraryItemViewModel
{
    public string DisplayName { get; }
    public string Glyph { get; }
    public string Detail { get; }

    public FurnitureTemplate? Furniture { get; }
    public SensorTemplate? Sensor { get; }

    public bool IsSensor => Sensor != null;

    private LibraryItemViewModel(string name, string glyph, string detail,
        FurnitureTemplate? furniture, SensorTemplate? sensor)
    {
        DisplayName = name;
        Glyph = glyph;
        Detail = detail;
        Furniture = furniture;
        Sensor = sensor;
    }

    public static LibraryItemViewModel FromFurniture(FurnitureTemplate t)
        => new(t.DisplayName, t.Glyph, $"{t.Width:0.#} × {t.Depth:0.#} m", t, null);

    public static LibraryItemViewModel FromSensor(SensorTemplate t)
        => new(t.DisplayName, t.Glyph, $"{t.HorizontalFovDegrees:0}°×{t.VerticalFovDegrees:0}° · {t.PixelColumns}×{t.PixelRows}px", null, t);
}
