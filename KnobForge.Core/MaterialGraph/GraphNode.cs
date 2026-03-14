using System.Numerics;

namespace KnobForge.Core.MaterialGraph;

public abstract class GraphNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public abstract string TypeId { get; }
    public Vector2 EditorPosition { get; set; }
    public abstract IReadOnlyList<GraphPort> GetPorts();
    public abstract void Evaluate(GraphEvaluationContext context);
}
