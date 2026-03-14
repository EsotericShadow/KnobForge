using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class PerlinNoiseNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "UV", Type = PortType.Float2, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f } },
        new() { Name = "Value", Type = PortType.Float, Direction = PortDirection.Output },
        new() { Name = "Color", Type = PortType.Float3, Direction = PortDirection.Output }
    };

    public override string TypeId => "PerlinNoise";
    public float Scale { get; set; } = 8f;
    public int Octaves { get; set; } = 4;
    public float Persistence { get; set; } = 0.5f;
    public float Lacunarity { get; set; } = 2f;
    public int Seed { get; set; }

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

        float value = 0f;
        float amplitude = 1f;
        float frequency = MathF.Max(0.0001f, Scale);
        float totalAmplitude = 0f;
        int octaves = Math.Clamp(Octaves, 1, 8);
        for (int i = 0; i < octaves; i++)
        {
            float nx = (uv.X + (Seed * 17.3f)) * frequency;
            float ny = (uv.Y + (Seed * 31.7f)) * frequency;
            float n = NoiseUtils.ValueNoise2(nx, ny);
            value += n * amplitude;
            totalAmplitude += amplitude;
            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        value = totalAmplitude <= 1e-6f ? 0f : value / totalAmplitude;
        value = NoiseUtils.Clamp(value, 0f, 1f);
        Vector3 grayscale = new(value, value, value);
        context.SetPortValue(Id, "Value", PortHelpers.FromFloat(value));
        context.SetPortValue(Id, "Color", PortHelpers.FromFloat3(grayscale));
    }
}
