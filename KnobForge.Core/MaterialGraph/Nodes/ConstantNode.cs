namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class ConstantNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Value", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "Constant";
    public float Value { get; set; }

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        context.SetPortValue(Id, "Value", PortHelpers.FromFloat(Value));
    }
}
