using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class CheckerNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "UV", Type = PortType.Float2, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f } },
        new() { Name = "Value", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "Checker";
    public float Scale { get; set; } = 4f;

    public override IReadOnlyList<GraphPort> GetPorts() => Ports;

    public override void Evaluate(GraphEvaluationContext context)
    {
        MaterialGraph? graph = context.Graph;
        if (graph == null)
        {
            return;
        }

        Vector2 uv = context.HasInputConnection(Id, "UV")
            ? PortHelpers.ToFloat2(context.GetInputValue(Id, "UV", graph))
            : context.UV;
        float scale = MathF.Max(0.0001f, Scale);
        int x = (int)MathF.Floor(uv.X * scale);
        int y = (int)MathF.Floor(uv.Y * scale);
        float value = ((x + y) & 1) == 0 ? 0f : 1f;
        context.SetPortValue(Id, "Value", PortHelpers.FromFloat(value));
    }
}
