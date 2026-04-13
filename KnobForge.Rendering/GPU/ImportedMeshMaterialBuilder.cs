using KnobForge.Core.Scene;

namespace KnobForge.Rendering.GPU;

public static class ImportedMeshMaterialBuilder
{
    public static bool TryBuildMaterialNodesFromPath(string path, out MaterialNode[] materials)
    {
        return ImportedStlCollarMeshBuilder.TryBuildMaterialNodesFromPath(path, out materials);
    }
}
