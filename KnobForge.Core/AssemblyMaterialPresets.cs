using System.Numerics;

namespace KnobForge.Core;

public readonly record struct AssemblyPartMaterial(
    Vector3 BaseColor,
    float Metallic,
    float Roughness,
    float DiffuseStrength,
    float SpecularStrength);

public readonly record struct AssemblyMaterialPresetDefinition(
    string Name,
    string Description,
    AssemblyPartMaterial[] PartMaterials);
