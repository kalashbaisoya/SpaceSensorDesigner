using System.Collections.Generic;

namespace SpaceSensorDesigner.Core.Models;

/// <summary>
/// A project groups one or more floor plans (e.g. the floors of a building or the apartments in a
/// care home). This is the top-level document saved to a <c>.spacedesign</c> file.
/// </summary>
public sealed class DesignProject
{
    public int SchemaVersion { get; set; } = 2;
    public string Name { get; set; } = "Untitled Project";
    public List<FloorPlan> Floors { get; set; } = new();

    public static DesignProject CreateSample()
    {
        var project = new DesignProject { Name = "Senior Living" };
        var floor = FloorPlan.CreateSample();
        floor.Name = "Apartment 1";
        project.Floors.Add(floor);
        return project;
    }

    /// <summary>Wraps a single loaded floor plan (for opening legacy single-floor files).</summary>
    public static DesignProject FromSingleFloor(FloorPlan plan)
    {
        var project = new DesignProject { Name = plan.Name };
        project.Floors.Add(plan);
        return project;
    }
}
