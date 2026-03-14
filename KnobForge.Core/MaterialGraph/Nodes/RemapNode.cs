namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class RemapNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Value", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "InMin", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "InMax", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 1f } },
        new() { Name = "OutMin", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "OutMax", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 1f } },
        new() { Name = "Result", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "Remap";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        float value = PortHelpers.ToFloat(context.GetInputValue(Id, "Value", graph));
        float inMin = PortHelpers.ToFloat(context.GetInputValue(Id, "InMin", graph));
        float inMax = PortHelpers.ToFloat(context.GetInputValue(Id, "InMax", graph));
        float outMin = PortHelpers.ToFloat(context.GetInputValue(Id, "OutMin", graph));
        float outMax = PortHelpers.ToFloat(context.GetInputValue(Id, "OutMax", graph));

        float result = MathF.Abs(inMax - inMin) <= 1e-6f
            ? outMin
            : outMin + (((value - inMin) / (inMax - inMin)) * (outMax - outMin));
        context.SetPortValue(Id, "Result", PortHelpers.FromFloat(result));
    }
}
