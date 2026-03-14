namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class ClampNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Value", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "Min", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "Max", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 1f } },
        new() { Name = "Result", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "Clamp";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        float value = PortHelpers.ToFloat(context.GetInputValue(Id, "Value", graph));
        float min = PortHelpers.ToFloat(context.GetInputValue(Id, "Min", graph));
        float max = PortHelpers.ToFloat(context.GetInputValue(Id, "Max", graph));
        if (min > max)
        {
            (min, max) = (max, min);
        }
        context.SetPortValue(Id, "Result", PortHelpers.FromFloat(NoiseUtils.Clamp(value, min, max)));
    }
}
