using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class RGBToHSVNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Color", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 1f, 1f, 1f } },
        new() { Name = "H", Type = PortType.Float, Direction = PortDirection.Output },
        new() { Name = "S", Type = PortType.Float, Direction = PortDirection.Output },
        new() { Name = "V", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "RGBToHSV";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        Vector3 color = PortHelpers.ToFloat3(context.GetInputValue(Id, "Color", graph));
        float max = MathF.Max(color.X, MathF.Max(color.Y, color.Z));
        float min = MathF.Min(color.X, MathF.Min(color.Y, color.Z));
        float delta = max - min;

        float h = 0f;
        if (delta > 1e-6f)
        {
            if (MathF.Abs(max - color.X) <= 1e-6f)
            {
                h = ((color.Y - color.Z) / delta) % 6f;
            }
            else if (MathF.Abs(max - color.Y) <= 1e-6f)
            {
                h = ((color.Z - color.X) / delta) + 2f;
            }
            else
            {
                h = ((color.X - color.Y) / delta) + 4f;
            }
            h /= 6f;
            if (h < 0f)
            {
                h += 1f;
            }
        }

        float s = max <= 1e-6f ? 0f : delta / max;
        context.SetPortValue(Id, "H", PortHelpers.FromFloat(h));
        context.SetPortValue(Id, "S", PortHelpers.FromFloat(s));
        context.SetPortValue(Id, "V", PortHelpers.FromFloat(max));
    }
}
