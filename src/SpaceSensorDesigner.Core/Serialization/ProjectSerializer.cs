using System.IO;
using System.Text.Json;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Serialization;

/// <summary>
/// Saves/loads a <see cref="DesignProject"/> (multi-floor document) to a <c>.spacedesign</c> file.
/// Falls back to reading a legacy single-<see cref="FloorPlan"/> file and wrapping it in a project.
/// </summary>
public static class ProjectSerializer
{
    public static string Serialize(DesignProject project)
        => JsonSerializer.Serialize(project, FloorPlanSerializer.Options);

    public static void Save(DesignProject project, string path)
        => File.WriteAllText(path, Serialize(project));

    public static DesignProject Load(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        // A project document has a "floors" array; a legacy file is a bare FloorPlan.
        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("floors", out _))
        {
            var project = JsonSerializer.Deserialize<DesignProject>(json, FloorPlanSerializer.Options) ?? new DesignProject();
            if (project.Floors.Count == 0) project.Floors.Add(FloorPlan.CreateSample());
            return project;
        }

        var plan = JsonSerializer.Deserialize<FloorPlan>(json, FloorPlanSerializer.Options) ?? new FloorPlan();
        return DesignProject.FromSingleFloor(plan);
    }
}
