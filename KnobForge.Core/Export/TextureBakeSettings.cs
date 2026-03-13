namespace KnobForge.Core.Export;

public sealed class TextureBakeSettings
{
    public int Resolution { get; set; } = 1024;

    public bool BakeAlbedo { get; set; } = true;

    public bool BakeNormal { get; set; } = true;

    public bool BakeRoughness { get; set; } = true;

    public bool BakeMetallic { get; set; } = true;

    public string OutputFolder { get; set; } = string.Empty;

    public string BaseName { get; set; } = "bake";
}
