namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class PowerNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Base", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "Exponent", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 1f } },
        new() { Name = "Result", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "Power";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        float baseValue = PortHelpers.ToFloat(context.GetInputValue(Id, "Base", graph));
        float exponent = PortHelpers.ToFloat(context.GetInputValue(Id, "Exponent", graph));
        float result = baseValue < 0f && MathF.Abs(exponent % 1f) > 1e-6f
            ? 0f
            : MathF.Pow(baseValue, exponent);
        context.SetPortValue(Id, "Result", PortHelpers.FromFloat(result));
    }
}
