using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public enum GradientType
{
    Linear,
    Radial,
    Angular
}

public sealed class GradientNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "UV", Type = PortType.Float2, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f } },
        new() { Name = "Value", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "Gradient";
    public GradientType Type { get; set; } = GradientType.Linear;
    public float Rotation { get; set; }

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
        Vector2 centered = uv - new Vector2(0.5f, 0.5f);
        float radians = Rotation * (MathF.PI / 180f);
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        Vector2 rotated = new(
            (centered.X * cos) - (centered.Y * sin),
            (centered.X * sin) + (centered.Y * cos));

        float value = Type switch
        {
            GradientType.Linear => NoiseUtils.Clamp(rotated.X + 0.5f, 0f, 1f),
            GradientType.Radial => NoiseUtils.Clamp(Vector2.Distance(uv, new Vector2(0.5f, 0.5f)) / 0.70710677f, 0f, 1f),
            GradientType.Angular => NoiseUtils.Clamp((MathF.Atan2(rotated.Y, rotated.X) / (MathF.PI * 2f)) + 0.5f, 0f, 1f),
            _ => 0f
        };

        context.SetPortValue(Id, "Value", PortHelpers.FromFloat(value));
    }
}
