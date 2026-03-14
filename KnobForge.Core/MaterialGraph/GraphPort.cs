namespace KnobForge.Core.MaterialGraph;

public sealed class GraphPort
{
    public string Name { get; set; } = string.Empty;
    public PortType Type { get; set; }
    public PortDirection Direction { get; set; }
    public float[]? DefaultValue { get; set; }
}
