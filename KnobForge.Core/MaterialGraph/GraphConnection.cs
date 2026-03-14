namespace KnobForge.Core.MaterialGraph;

public sealed class GraphConnection
{
    public Guid SourceNodeId { get; set; }
    public string SourcePortName { get; set; } = string.Empty;
    public Guid TargetNodeId { get; set; }
    public string TargetPortName { get; set; } = string.Empty;
}
