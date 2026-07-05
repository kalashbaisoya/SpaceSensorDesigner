using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Geometry;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.App.Rendering;

/// <summary>
/// Everything <see cref="SceneRenderer"/> needs to draw one frame. Assembled by the
/// <c>DesignSurface</c> from the current view-model state.
/// </summary>
public sealed class RenderContext
{
    public required FloorPlan Plan { get; init; }
    public required IProjection Projection { get; init; }
    public required ViewMode ViewMode { get; init; }

    public CoverageGrid? Coverage { get; init; }
    public bool ShowHeatmap { get; init; } = true;
    public bool ShowCones { get; init; } = true;
    public bool ShowGrid { get; init; } = true;

    /// <summary>When true the floor is coloured by sensor-overlap count instead of coverage state.</summary>
    public bool ShowRedundancy { get; init; }

    /// <summary>The primary selected model object (drives the properties panel), or null.</summary>
    public object? Selected { get; init; }

    /// <summary>The full multi-selection set (for highlighting).</summary>
    public System.Collections.Generic.IReadOnlyCollection<object>? SelectedSet { get; init; }

    /// <summary>True if the element is part of the current selection.</summary>
    public bool IsSelected(object element)
        => ReferenceEquals(element, Selected) || (SelectedSet != null && SelectedSet.Contains(element));

    /// <summary>Live marquee selection rectangle (world coords), or null.</summary>
    public (Vec2 a, Vec2 b)? Marquee { get; init; }

    /// <summary>Live wall being drawn (start..end) for preview, or null.</summary>
    public (Vec2 start, Vec2 end)? WallPreview { get; init; }

    /// <summary>Opacity (0..1) applied to optimizer suggestion sensors for the placement animation.</summary>
    public double SuggestionOpacity { get; init; } = 1.0;

    /// <summary>Optional traced-over floor-plan image (2D view only).</summary>
    public System.Windows.Media.ImageSource? BackgroundImage { get; init; }
}
