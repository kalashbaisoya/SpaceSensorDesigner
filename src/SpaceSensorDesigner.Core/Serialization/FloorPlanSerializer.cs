using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Serialization;

/// <summary>
/// Saves/loads a <see cref="FloorPlan"/> to a <c>.spacedesign</c> file (indented JSON).
/// Uses only <c>System.Text.Json</c> — no third-party serializer.
/// </summary>
public static class FloorPlanSerializer
{
    public const string FileExtension = ".spacedesign";

    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(), new Vec2JsonConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(FloorPlan plan)
        => JsonSerializer.Serialize(plan, Options);

    public static FloorPlan Deserialize(string json)
        => JsonSerializer.Deserialize<FloorPlan>(json, Options) ?? new FloorPlan();

    public static void Save(FloorPlan plan, string path)
        => File.WriteAllText(path, Serialize(plan));

    public static FloorPlan Load(string path)
        => Deserialize(File.ReadAllText(path));
}

/// <summary>Serializes <see cref="Vec2"/> as a compact <c>{ "x": .., "y": .. }</c> object.</summary>
public sealed class Vec2JsonConverter : JsonConverter<Vec2>
{
    public override Vec2 Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        double x = 0, y = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            string? prop = reader.GetString();
            reader.Read();
            if (prop is "x" or "X") x = reader.GetDouble();
            else if (prop is "y" or "Y") y = reader.GetDouble();
        }
        return new Vec2(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vec2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteEndObject();
    }
}
