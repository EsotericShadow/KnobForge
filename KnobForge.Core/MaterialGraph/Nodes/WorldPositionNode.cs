namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class WorldPositionNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Position", Type = PortType.Float3, Direction = PortDirection.Output }
    };

    public override string TypeId => "WorldPosition";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        context.SetPortValue(Id, "Position", PortHelpers.FromFloat3(context.WorldPosition));
    }
}
