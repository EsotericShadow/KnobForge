using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KnobForge.Core.MaterialGraph;

public sealed class MaterialGraphJsonConverter : JsonConverter<MaterialGraph>
{
    public override MaterialGraph? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        JsonElement root = doc.RootElement;

        var graph = new MaterialGraph();

        if (root.TryGetProperty("Nodes", out JsonElement nodesArray))
        {
            foreach (JsonElement nodeElement in nodesArray.EnumerateArray())
            {
                string? typeId = nodeElement.TryGetProperty("TypeId", out JsonElement typeElement)
                    ? typeElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(typeId))
                {
                    continue;
                }

                GraphNode? node = GraphNodeTypeRegistry.CreateByTypeId(typeId);
                if (node == null)
                {
                    continue;
                }

                if (nodeElement.TryGetProperty("Id", out JsonElement idElement) && idElement.TryGetGuid(out Guid nodeId))
                {
                    node.Id = nodeId;
                }

                if (nodeElement.TryGetProperty("EditorPosition", out JsonElement positionElement))
                {
                    float x = positionElement.TryGetProperty("X", out JsonElement px) ? px.GetSingle() : 0f;
                    float y = positionElement.TryGetProperty("Y", out JsonElement py) ? py.GetSingle() : 0f;
                    node.EditorPosition = new Vector2(x, y);
                }

                if (nodeElement.TryGetProperty("Parameters", out JsonElement paramsElement))
                {
                    DeserializeNodeParameters(node, paramsElement, options);
                }

                graph.Nodes.Add(node);
            }
        }

        if (root.TryGetProperty("Connections", out JsonElement connectionsArray))
        {
            foreach (JsonElement connElement in connectionsArray.EnumerateArray())
            {
                graph.Connections.Add(new GraphConnection
                {
                    SourceNodeId = connElement.GetProperty("SourceNodeId").GetGuid(),
                    SourcePortName = connElement.GetProperty("SourcePortName").GetString() ?? string.Empty,
                    TargetNodeId = connElement.GetProperty("TargetNodeId").GetGuid(),
                    TargetPortName = connElement.GetProperty("TargetPortName").GetString() ?? string.Empty
                });
            }
        }

        return graph;
    }

    public override void Write(Utf8JsonWriter writer, MaterialGraph value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteStartArray("Nodes");
        foreach (GraphNode node in value.Nodes)
        {
            writer.WriteStartObject();
            writer.WriteString("TypeId", node.TypeId);
            writer.WriteString("Id", node.Id);
            writer.WriteStartObject("EditorPosition");
            writer.WriteNumber("X", node.EditorPosition.X);
            writer.WriteNumber("Y", node.EditorPosition.Y);
            writer.WriteEndObject();
            writer.WritePropertyName("Parameters");
            SerializeNodeParameters(writer, node, options);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteStartArray("Connections");
        foreach (GraphConnection conn in value.Connections)
        {
            writer.WriteStartObject();
            writer.WriteString("SourceNodeId", conn.SourceNodeId);
            writer.WriteString("SourcePortName", conn.SourcePortName);
            writer.WriteString("TargetNodeId", conn.TargetNodeId);
            writer.WriteString("TargetPortName", conn.TargetPortName);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private static void SerializeNodeParameters(Utf8JsonWriter writer, GraphNode node, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, node, node.GetType(), options);
    }

    private static void DeserializeNodeParameters(GraphNode node, JsonElement paramsElement, JsonSerializerOptions options)
    {
        string json = paramsElement.GetRawText();
        GraphNode? tempNode = JsonSerializer.Deserialize(json, node.GetType(), options) as GraphNode;
        if (tempNode != null)
        {
            CopyParameters(tempNode, node);
        }
    }

    private static void CopyParameters(GraphNode source, GraphNode target)
    {
        PropertyInfo[] props = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (PropertyInfo prop in props)
        {
            if (!prop.CanRead || !prop.CanWrite || prop.Name is nameof(GraphNode.Id) or nameof(GraphNode.EditorPosition) or nameof(GraphNode.TypeId))
            {
                continue;
            }

            try
            {
                prop.SetValue(target, prop.GetValue(source));
            }
            catch
            {
                // Skip invalid or readonly parameter properties.
            }
        }
    }
}
