namespace KnobForge.Core.Export;

public sealed class BakeResult
{
    public string? AlbedoPath { get; set; }

    public string? NormalPath { get; set; }

    public string? RoughnessPath { get; set; }

    public string? MetallicPath { get; set; }

    public string? MetadataPath { get; set; }

    public int Resolution { get; set; }

    public bool Success { get; set; }

    public string? Error { get; set; }
}
