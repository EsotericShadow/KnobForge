using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class ArithmeticFloat3Node : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "A", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f, 0f } },
        new() { Name = "B", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f, 0f } },
        new() { Name = "Result", Type = PortType.Float3, Direction = PortDirection.Output }
    };

    public override string TypeId => "ArithmeticFloat3";
    public ArithmeticOp Operation { get; set; } = ArithmeticOp.Add;

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
        Vector3 result = Operation switch
        {
            ArithmeticOp.Add => a + b,
            ArithmeticOp.Subtract => a - b,
            ArithmeticOp.Multiply => new Vector3(a.X * b.X, a.Y * b.Y, a.Z * b.Z),
            ArithmeticOp.Divide => new Vector3(
                MathF.Abs(b.X) <= 1e-6f ? 0f : a.X / b.X,
                MathF.Abs(b.Y) <= 1e-6f ? 0f : a.Y / b.Y,
                MathF.Abs(b.Z) <= 1e-6f ? 0f : a.Z / b.Z),
            _ => Vector3.Zero
        };
        context.SetPortValue(Id, "Result", PortHelpers.FromFloat3(result));
    }
}
