using System.Numerics;

namespace KnobForge.Core.MaterialGraph;

public static class GraphEvaluator
{
    public static MaterialOutput EvaluateAtTexel(
        MaterialGraph graph,
        float u,
        float v,
        GraphEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(context);

        List<GraphNode> sorted = graph.TopologicalSort();
        return EvaluateSorted(graph, sorted, u, v, context);
    }

    public static GraphBakeResult BakeGraph(
        MaterialGraph graph,
        int width,
        int height,
        Dictionary<string, TextureData> textures,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);

        List<GraphNode> sorted = graph.TopologicalSort();
        GraphNode? outputNode = graph.FindOutputNode();
        var context = new GraphEvaluationContext
        {
            Graph = graph
        };

        foreach ((string path, TextureData tex) in textures)
        {
            context.LoadedTextures[path] = tex;
        }

        byte[] albedo = new byte[width * height * 4];
        byte[] normal = new byte[width * height * 4];
        byte[] roughness = new byte[width * height * 4];
        byte[] metallic = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            if ((y & 7) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report((float)y / Math.Max(1, height));
            }

            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                MaterialOutput output = EvaluateSorted(graph, sorted, u, v, context, outputNode);
                int offset = (y * width + x) * 4;
                WriteColorToBuffer(albedo, offset, output.Albedo);
                WriteColorToBuffer(normal, offset, output.Normal);
                WriteGrayscaleToBuffer(roughness, offset, output.Roughness);
                WriteGrayscaleToBuffer(metallic, offset, output.Metallic);
            }
        }

        progress?.Report(1f);
        return new GraphBakeResult(width, height, albedo, normal, roughness, metallic);
    }

    private static MaterialOutput EvaluateSorted(
        MaterialGraph graph,
        IReadOnlyList<GraphNode> sorted,
        float u,
        float v,
        GraphEvaluationContext context,
        GraphNode? outputNode = null)
    {
        context.Clear();
        context.Graph = graph;
        context.UV = new Vector2(u, v);

        foreach (GraphNode node in sorted)
        {
            node.Evaluate(context);
        }

        outputNode ??= graph.FindOutputNode();
        return outputNode != null ? ExtractOutput(context, outputNode) : MaterialOutput.Default;
    }

    private static MaterialOutput ExtractOutput(GraphEvaluationContext ctx, GraphNode outputNode)
    {
        return new MaterialOutput
        {
            Albedo = PortHelpers.ToFloat3(ctx.GetPortValue(outputNode.Id, "Albedo")),
            Normal = PortHelpers.ToFloat3(ctx.GetPortValue(outputNode.Id, "Normal")),
            Roughness = PortHelpers.ToFloat(ctx.GetPortValue(outputNode.Id, "Roughness")),
            Metallic = PortHelpers.ToFloat(ctx.GetPortValue(outputNode.Id, "Metallic")),
            Emission = PortHelpers.ToFloat3(ctx.GetPortValue(outputNode.Id, "Emission")),
            Alpha = PortHelpers.ToFloat(ctx.GetPortValue(outputNode.Id, "Alpha"))
        };
    }

    private static void WriteColorToBuffer(byte[] buffer, int offset, Vector3 color)
    {
        buffer[offset] = (byte)Math.Clamp((color.X * 255f) + 0.5f, 0f, 255f);
        buffer[offset + 1] = (byte)Math.Clamp((color.Y * 255f) + 0.5f, 0f, 255f);
        buffer[offset + 2] = (byte)Math.Clamp((color.Z * 255f) + 0.5f, 0f, 255f);
        buffer[offset + 3] = 255;
    }

    private static void WriteGrayscaleToBuffer(byte[] buffer, int offset, float value)
    {
        byte component = (byte)Math.Clamp((value * 255f) + 0.5f, 0f, 255f);
        buffer[offset] = component;
        buffer[offset + 1] = component;
        buffer[offset + 2] = component;
        buffer[offset + 3] = 255;
    }
}

public readonly struct MaterialOutput
{
    public Vector3 Albedo { get; init; }
    public Vector3 Normal { get; init; }
    public float Roughness { get; init; }
    public float Metallic { get; init; }
    public Vector3 Emission { get; init; }
    public float Alpha { get; init; }

    public static MaterialOutput Default => new()
    {
        Albedo = new Vector3(0.8f, 0.8f, 0.8f),
        Normal = new Vector3(0.5f, 0.5f, 1f),
        Roughness = 0.5f,
        Metallic = 0f,
        Emission = Vector3.Zero,
        Alpha = 1f
    };
}

public readonly struct GraphBakeResult
{
    public GraphBakeResult(int width, int height, byte[] albedo, byte[] normal, byte[] roughness, byte[] metallic)
    {
        Width = width;
        Height = height;
        Albedo = albedo;
        Normal = normal;
        Roughness = roughness;
        Metallic = metallic;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Albedo { get; }
    public byte[] Normal { get; }
    public byte[] Roughness { get; }
    public byte[] Metallic { get; }
}
