using KnobForge.Core.MaterialGraph.Nodes;

namespace KnobForge.Core.MaterialGraph;

public static class GraphNodeTypeRegistry
{
    private static readonly Dictionary<string, Type> Registry = new(StringComparer.Ordinal);

    static GraphNodeTypeRegistry()
    {
        Register("Constant", typeof(ConstantNode));
        Register("ConstantFloat3", typeof(ConstantFloat3Node));
        Register("UVInput", typeof(UVInputNode));
        Register("WorldPosition", typeof(WorldPositionNode));
        Register("VertexNormal", typeof(VertexNormalNode));
        Register("TextureMap", typeof(TextureMapNode));
        Register("Arithmetic", typeof(ArithmeticNode));
        Register("ArithmeticFloat3", typeof(ArithmeticFloat3Node));
        Register("Lerp", typeof(LerpNode));
        Register("Clamp", typeof(ClampNode));
        Register("Remap", typeof(RemapNode));
        Register("Power", typeof(PowerNode));
        Register("DotProduct", typeof(DotProductNode));
        Register("Normalize", typeof(NormalizeNode));
        Register("PerlinNoise", typeof(PerlinNoiseNode));
        Register("Voronoi", typeof(VoronoiNode));
        Register("Gradient", typeof(GradientNode));
        Register("Checker", typeof(CheckerNode));
        Register("Brick", typeof(BrickNode));
        Register("ColorRamp", typeof(ColorRampNode));
        Register("HSVToRGB", typeof(HSVToRGBNode));
        Register("RGBToHSV", typeof(RGBToHSVNode));
        Register("BrightnessContrast", typeof(BrightnessContrastNode));
        Register("PBROutput", typeof(PBROutputNode));
    }

    public static void Register(string typeId, Type nodeType)
    {
        Registry[typeId] = nodeType;
    }

    public static GraphNode? CreateByTypeId(string typeId)
    {
        return Registry.TryGetValue(typeId, out Type? type)
            ? Activator.CreateInstance(type) as GraphNode
            : null;
    }

    public static IReadOnlyDictionary<string, Type> GetAllTypes() => Registry;
}
