using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class PBROutputNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "Albedo", Type = PortType.Color, Direction = PortDirection.Input, DefaultValue = new[] { 0.8f, 0.8f, 0.8f } },
        new() { Name = "Normal", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 0.5f, 0.5f, 1f } },
        new() { Name = "Roughness", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0.5f } },
        new() { Name = "Metallic", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 0f } },
        new() { Name = "Emission", Type = PortType.Float3, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f, 0f } },
        new() { Name = "Alpha", Type = PortType.Float, Direction = PortDirection.Input, DefaultValue = new[] { 1f } }
    };

    public override string TypeId => "PBROutput";

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        Vector3 albedo = PortHelpers.ToFloat3(context.GetInputValue(Id, "Albedo", graph));
        Vector3 normal = PortHelpers.ToFloat3(context.GetInputValue(Id, "Normal", graph));
        float roughness = PortHelpers.ToFloat(context.GetInputValue(Id, "Roughness", graph));
        float metallic = PortHelpers.ToFloat(context.GetInputValue(Id, "Metallic", graph));
        Vector3 emission = PortHelpers.ToFloat3(context.GetInputValue(Id, "Emission", graph));
        float alpha = PortHelpers.ToFloat(context.GetInputValue(Id, "Alpha", graph));

        context.SetPortValue(Id, "Albedo", PortHelpers.FromFloat3(albedo));
        context.SetPortValue(Id, "Normal", PortHelpers.FromFloat3(normal));
        context.SetPortValue(Id, "Roughness", PortHelpers.FromFloat(roughness));
        context.SetPortValue(Id, "Metallic", PortHelpers.FromFloat(metallic));
        context.SetPortValue(Id, "Emission", PortHelpers.FromFloat3(emission));
        context.SetPortValue(Id, "Alpha", PortHelpers.FromFloat(alpha));
    }
}
