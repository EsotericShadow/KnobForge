namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class VertexNormalNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Normal", Type = PortType.Float3, Direction = PortDirection.Output }
    };

    public override string TypeId => "VertexNormal";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        context.SetPortValue(Id, "Normal", PortHelpers.FromFloat3(context.WorldNormal));
    }
}
