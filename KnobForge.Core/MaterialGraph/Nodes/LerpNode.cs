using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class LerpNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "A", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f, 0f } },
        new() { Name = "B", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 1f, 1f, 1f } },
        new() { Name = "T", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0.5f } },
        new() { Name = "Result", Type = PortType.Float3, Direction = PortDirection.Output }
    };

    public override string TypeId => "Lerp";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        Vector3 a = PortHelpers.ToFloat3(context.GetInputValue(Id, "A", graph));
        Vector3 b = PortHelpers.ToFloat3(context.GetInputValue(Id, "B", graph));
        float t = NoiseUtils.Clamp(PortHelpers.ToFloat(context.GetInputValue(Id, "T", graph)), 0f, 1f);
        context.SetPortValue(Id, "Result", PortHelpers.FromFloat3(Vector3.Lerp(a, b, t)));
    }
}
