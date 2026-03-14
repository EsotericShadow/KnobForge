using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class BrightnessContrastNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Color", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 1f, 1f, 1f } },
        new() { Name = "Brightness", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "Contrast", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 1f } },
        new() { Name = "ColorOut", Type = PortType.Float3, Direction = PortDirection.Output }
    };

    public override string TypeId => "BrightnessContrast";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        Vector3 color = PortHelpers.ToFloat3(context.GetInputValue(Id, "Color", graph));
        float brightness = PortHelpers.ToFloat(context.GetInputValue(Id, "Brightness", graph));
        float contrast = PortHelpers.ToFloat(context.GetInputValue(Id, "Contrast", graph));
        Vector3 adjusted = ((color - new Vector3(0.5f)) * contrast) + new Vector3(0.5f + brightness);
        adjusted = Vector3.Clamp(adjusted, Vector3.Zero, Vector3.One);
        context.SetPortValue(Id, "ColorOut", PortHelpers.FromFloat3(adjusted));
    }
}
