using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class VoronoiNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "UV", Type = PortType.Float2, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f } },
        new() { Name = "Distance", Type = PortType.Float, Direction = PortDirection.Output },
        new() { Name = "CellValue", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "Voronoi";
    public float Scale { get; set; } = 5f;
    public float Jitter { get; set; } = 1f;
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
        uv *= MathF.Max(0.0001f, Scale);

        Vector2 cell = new(MathF.Floor(uv.X), MathF.Floor(uv.Y));
        float bestDistance = float.MaxValue;
        float bestValue = 0f;
        float jitter = NoiseUtils.Clamp(Jitter, 0f, 1f);

        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                float cx = cell.X + ox;
                float cy = cell.Y + oy;
                float jx = NoiseUtils.Hash21(cx + (Seed * 3.1f), cy + (Seed * 5.7f));
                float jy = NoiseUtils.Hash21(cx + (Seed * 7.9f), cy + (Seed * 11.3f));
                Vector2 center = new(cx + (jx * jitter), cy + (jy * jitter));
                float distance = Vector2.Distance(uv, center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestValue = NoiseUtils.Hash21(cx + Seed, cy - Seed);
                }
            }
        }

        context.SetPortValue(Id, "Distance", PortHelpers.FromFloat(bestDistance));
        context.SetPortValue(Id, "CellValue", PortHelpers.FromFloat(bestValue));
    }
}
