using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class GradientStop
{
    public float Position { get; set; }
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
}

public sealed class ColorRampNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Value", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "Color", Type = PortType.Float3, Direction = PortDirection.Output }
    };

    public override string TypeId => "ColorRamp";
    public List<GradientStop> Stops { get; set; } = new()
    {
        new GradientStop { Position = 0f, R = 0f, G = 0f, B = 0f },
        new GradientStop { Position = 1f, R = 1f, G = 1f, B = 1f }
    };

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        float value = NoiseUtils.Clamp(PortHelpers.ToFloat(context.GetInputValue(Id, "Value", graph)), 0f, 1f);
        List<GradientStop> ordered = Stops.OrderBy(stop => stop.Position).ToList();
        if (ordered.Count == 0)
        {
            context.SetPortValue(Id, "Color", PortHelpers.FromFloat3(Vector3.Zero));
            return;
        }

        GradientStop first = ordered[0];
        if (value <= first.Position)
        {
            context.SetPortValue(Id, "Color", PortHelpers.FromFloat3(new Vector3(first.R, first.G, first.B)));
            return;
        }

        GradientStop last = ordered[^1];
        if (value >= last.Position)
        {
            context.SetPortValue(Id, "Color", PortHelpers.FromFloat3(new Vector3(last.R, last.G, last.B)));
            return;
        }

        for (int i = 0; i < ordered.Count - 1; i++)
        {
            GradientStop left = ordered[i];
            GradientStop right = ordered[i + 1];
            if (value < left.Position || value > right.Position)
            {
                continue;
            }

            float t = MathF.Abs(right.Position - left.Position) <= 1e-6f
                ? 0f
                : (value - left.Position) / (right.Position - left.Position);
            Vector3 color = Vector3.Lerp(new Vector3(left.R, left.G, left.B), new Vector3(right.R, right.G, right.B), t);
            context.SetPortValue(Id, "Color", PortHelpers.FromFloat3(color));
            return;
        }

        context.SetPortValue(Id, "Color", PortHelpers.FromFloat3(new Vector3(last.R, last.G, last.B)));
    }
}
