using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class NormalizeNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Value", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f, 1f } },
        new() { Name = "Result", Type = PortType.Float3, Direction = PortDirection.Output }
    };

    public override string TypeId => "Normalize";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        Vector3 value = PortHelpers.ToFloat3(context.GetInputValue(Id, "Value", graph));
        value = value.LengthSquared() <= 1e-6f ? Vector3.Zero : Vector3.Normalize(value);
        context.SetPortValue(Id, "Result", PortHelpers.FromFloat3(value));
    }
}
