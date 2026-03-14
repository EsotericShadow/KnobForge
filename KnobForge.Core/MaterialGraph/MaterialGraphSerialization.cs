using System.Text.Json;
using System.Text.Json.Serialization;

namespace KnobForge.Core.MaterialGraph;

public static class MaterialGraphSerialization
{
    public static JsonSerializerOptions CreateJsonOptions(bool indented = false)
    {
        return new JsonSerializerOptions
        {
            WriteIndented = indented,
            Converters =
            {
                new JsonStringEnumConverter(),
                new MaterialGraphJsonConverter()
            }
        };
    }

    public static string Serialize(MaterialGraph graph, bool indented = false)
    {
        return JsonSerializer.Serialize(graph, CreateJsonOptions(indented));
    }

    public static MaterialGraph? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<MaterialGraph>(json, CreateJsonOptions());
    }

    public static MaterialGraph? Clone(MaterialGraph? graph)
    {
        if (graph == null)
        {
            return null;
        }

        return Deserialize(Serialize(graph));
    }
}
