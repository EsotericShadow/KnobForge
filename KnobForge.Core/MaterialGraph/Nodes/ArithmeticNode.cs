namespace KnobForge.Core.MaterialGraph.Nodes;

public enum ArithmeticOp
{
    Add,
    Subtract,
    Multiply,
    Divide
}

public sealed class ArithmeticNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "A", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "B", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "Result", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "Arithmetic";
    public ArithmeticOp Operation { get; set; } = ArithmeticOp.Add;

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        float a = PortHelpers.ToFloat(context.GetInputValue(Id, "A", graph));
        float b = PortHelpers.ToFloat(context.GetInputValue(Id, "B", graph));
        float result = Operation switch
        {
            ArithmeticOp.Add => a + b,
            ArithmeticOp.Subtract => a - b,
            ArithmeticOp.Multiply => a * b,
            ArithmeticOp.Divide => MathF.Abs(b) <= 1e-6f ? 0f : a / b,
            _ => 0f
        };
        context.SetPortValue(Id, "Result", PortHelpers.FromFloat(result));
    }
}
