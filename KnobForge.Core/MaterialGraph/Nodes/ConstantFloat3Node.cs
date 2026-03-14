using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class ConstantFloat3Node : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Value", Type = PortType.Float3, Direction = PortDirection.Output }
    };

    public override string TypeId => "ConstantFloat3";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        context.SetPortValue(Id, "Value", PortHelpers.FromFloat3(new Vector3(X, Y, Z)));
    }
}
