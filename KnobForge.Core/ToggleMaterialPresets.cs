using System;
using System.Collections.Generic;
using System.Numerics;

namespace KnobForge.Core;

public enum ToggleMaterialPresetId
{
    Custom = -1,
    StudioChrome = 0,
    VintageBakelite = 1,
    MilSpec = 2,
    BrushedBrass = 3
}

public static class ToggleMaterialPresets
{
    private static readonly ToggleMaterialPresetId[] OrderedPresetIds =
    {
        ToggleMaterialPresetId.StudioChrome,
        ToggleMaterialPresetId.VintageBakelite,
        ToggleMaterialPresetId.MilSpec,
        ToggleMaterialPresetId.BrushedBrass
    };

    public static readonly AssemblyMaterialPresetDefinition StudioChrome = new(
        "Studio Chrome",
        "Classic chrome toggle switch for studio hardware.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.85f, 0.85f, 0.88f), 1.0f, 0.15f, 0.9f, 1.8f),
            new AssemblyPartMaterial(new Vector3(0.82f, 0.82f, 0.85f), 1.0f, 0.12f, 0.9f, 1.9f),
            new AssemblyPartMaterial(new Vector3(0.80f, 0.80f, 0.84f), 1.0f, 0.10f, 0.9f, 2.0f)
        });

    public static readonly AssemblyMaterialPresetDefinition VintageBakelite = new(
        "Vintage Bakelite",
        "Warm Bakelite toggle for vintage-style gear.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.18f, 0.10f, 0.06f), 0.0f, 0.65f, 0.85f, 0.6f),
            new AssemblyPartMaterial(new Vector3(0.90f, 0.85f, 0.72f), 0.0f, 0.55f, 0.90f, 0.5f),
            new AssemblyPartMaterial(new Vector3(0.95f, 0.92f, 0.82f), 0.0f, 0.50f, 0.92f, 0.5f)
        });

    public static readonly AssemblyMaterialPresetDefinition MilSpec = new(
        "Mil-Spec",
        "Military-specification toggle switch with safety markings.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.28f, 0.30f, 0.22f), 0.1f, 0.75f, 0.80f, 0.4f),
            new AssemblyPartMaterial(new Vector3(0.08f, 0.08f, 0.08f), 0.05f, 0.80f, 0.85f, 0.3f),
            new AssemblyPartMaterial(new Vector3(0.82f, 0.12f, 0.08f), 0.0f, 0.60f, 0.90f, 0.6f)
        });

    public static readonly AssemblyMaterialPresetDefinition BrushedBrass = new(
        "Brushed Brass",
        "Warm brushed brass toggle for boutique hardware.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.78f, 0.62f, 0.30f), 0.95f, 0.35f, 0.85f, 1.4f),
            new AssemblyPartMaterial(new Vector3(0.80f, 0.65f, 0.32f), 0.95f, 0.30f, 0.85f, 1.5f),
            new AssemblyPartMaterial(new Vector3(0.06f, 0.06f, 0.06f), 0.0f, 0.85f, 0.90f, 0.3f)
        });

    public static IReadOnlyList<ToggleMaterialPresetId> GetPresetIds() => OrderedPresetIds;

    public static string GetDisplayName(ToggleMaterialPresetId id) => Resolve(id).Name;

    public static AssemblyMaterialPresetDefinition Resolve(ToggleMaterialPresetId id) => id switch
    {
        ToggleMaterialPresetId.VintageBakelite => VintageBakelite,
        ToggleMaterialPresetId.MilSpec => MilSpec,
        ToggleMaterialPresetId.BrushedBrass => BrushedBrass,
        _ => StudioChrome
    };

    public static bool IsSupported(ToggleMaterialPresetId id)
    {
        return id == ToggleMaterialPresetId.Custom || Array.IndexOf(OrderedPresetIds, id) >= 0;
    }
}
