namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class UVInputNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "UV", Type = PortType.Float2, Direction = PortDirection.Output }
    };

    public override string TypeId => "UVInput";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        context.SetPortValue(Id, "UV", PortHelpers.FromFloat2(context.UV));
    }
}
