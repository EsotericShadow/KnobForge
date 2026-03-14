using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class DotProductNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "A", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f, 0f } },
        new() { Name = "B", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f, 0f } },
        new() { Name = "Result", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "DotProduct";

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
        context.SetPortValue(Id, "Result", PortHelpers.FromFloat(Vector3.Dot(a, b)));
    }
}
