using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class TextureMapNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "UV", Type = PortType.Float2, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f } },
        new() { Name = "Color", Type = PortType.Float4, Direction = PortDirection.Output },
        new() { Name = "R", Type = PortType.Float, Direction = PortDirection.Output },
        new() { Name = "G", Type = PortType.Float, Direction = PortDirection.Output },
        new() { Name = "B", Type = PortType.Float, Direction = PortDirection.Output },
        new() { Name = "A", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "TextureMap";
    public string FilePath { get; set; } = string.Empty;
    public float TilingX { get; set; } = 1f;
    public float TilingY { get; set; } = 1f;
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        Vector2 uv = context.HasInputConnection(Id, "UV")
            ? PortHelpers.ToFloat2(context.GetInputValue(Id, "UV", graph))
            : context.UV;
        float u = (uv.X * TilingX) + OffsetX;
        float v = (uv.Y * TilingY) + OffsetY;

        Vector4 color = Vector4.Zero;
        if (!string.IsNullOrWhiteSpace(FilePath) && context.LoadedTextures.TryGetValue(FilePath, out TextureData? texture))
        {
            color = texture.SampleBilinear(u, v);
        }

        context.SetPortValue(Id, "Color", PortHelpers.FromFloat4(color));
        context.SetPortValue(Id, "R", PortHelpers.FromFloat(color.X));
        context.SetPortValue(Id, "G", PortHelpers.FromFloat(color.Y));
        context.SetPortValue(Id, "B", PortHelpers.FromFloat(color.Z));
        context.SetPortValue(Id, "A", PortHelpers.FromFloat(color.W));
    }
}
