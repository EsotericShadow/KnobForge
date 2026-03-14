using System.Numerics;

namespace KnobForge.Core.MaterialGraph.Nodes;

public sealed class BrickNode : GraphNode
{
    private static readonly GraphPort[] Ports =
    {
        new() { Name = "UV", Type = PortType.Float2, Direction = PortDirection.Input, DefaultValue = new[] { 0f, 0f } },
        new() { Name = "Value", Type = PortType.Float, Direction = PortDirection.Output },
        new() { Name = "Mortar", Type = PortType.Float, Direction = PortDirection.Output }
    };

    public override string TypeId => "Brick";
    public float BrickWidth { get; set; } = 0.5f;
    public float BrickHeight { get; set; } = 0.25f;
    public float MortarWidth { get; set; } = 0.05f;
    public float RowOffset { get; set; } = 0.5f;

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

        float bw = MathF.Max(0.0001f, BrickWidth);
        float bh = MathF.Max(0.0001f, BrickHeight);
        float mortar = NoiseUtils.Clamp(MortarWidth, 0f, 0.49f);

        float row = MathF.Floor(uv.Y / bh);
        float rowOffset = ((int)row & 1) == 1 ? RowOffset * bw : 0f;
        float localX = NoiseUtils.Fract((uv.X + rowOffset) / bw);
        float localY = NoiseUtils.Fract(uv.Y / bh);
        bool insideBrick = localX >= mortar && localX <= 1f - mortar &&
                          localY >= mortar && localY <= 1f - mortar;
        context.SetPortValue(Id, "Value", PortHelpers.FromFloat(insideBrick ? 1f : 0f));
        context.SetPortValue(Id, "Mortar", PortHelpers.FromFloat(insideBrick ? 0f : 1f));
    }
}
