using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class HSVToRGBNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "H", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "S", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 1f } },
        new() { Name = "V", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 1f } },
        new() { Name = "Color", Type = PortType.Float3, Direction = PortDirection.Output }
    };

    public override string TypeId => "HSVToRGB";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        float h = NoiseUtils.Fract(PortHelpers.ToFloat(context.GetInputValue(Id, "H", graph)));
        float s = NoiseUtils.Clamp(PortHelpers.ToFloat(context.GetInputValue(Id, "S", graph)), 0f, 1f);
        float v = NoiseUtils.Clamp(PortHelpers.ToFloat(context.GetInputValue(Id, "V", graph)), 0f, 1f);

        float c = v * s;
        float x = c * (1f - MathF.Abs(((h * 6f) % 2f) - 1f));
        float m = v - c;
        Vector3 rgb = h switch
        {
            < 1f / 6f => new Vector3(c, x, 0f),
            < 2f / 6f => new Vector3(x, c, 0f),
            < 3f / 6f => new Vector3(0f, c, x),
            < 4f / 6f => new Vector3(0f, x, c),
            < 5f / 6f => new Vector3(x, 0f, c),
            _ => new Vector3(c, 0f, x)
        };
        context.SetPortValue(Id, "Color", PortHelpers.FromFloat3(rgb + new Vector3(m, m, m)));
    }
}
