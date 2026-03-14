using System;
using System.Collections.Generic;
using System.Numerics;

namespace KnobForge.Core;

public enum PushButtonMaterialPresetId
{
    Custom = -1,
    NeveGray = 0,
    MoogBlack = 1,
    ArcadeGlow = 2,
    BrushedMetal = 3
}

public static class PushButtonMaterialPresets
{
    private static readonly PushButtonMaterialPresetId[] OrderedPresetIds =
    {
        PushButtonMaterialPresetId.NeveGray,
        PushButtonMaterialPresetId.MoogBlack,
        PushButtonMaterialPresetId.ArcadeGlow,
        PushButtonMaterialPresetId.BrushedMetal
    };

    public static readonly AssemblyMaterialPresetDefinition NeveGray = new(
        "Neve Gray",
        "Classic recording console button in warm gray.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.25f, 0.25f, 0.28f), 0.15f, 0.65f, 0.85f, 0.5f),
            new AssemblyPartMaterial(new Vector3(0.55f, 0.55f, 0.58f), 0.0f, 0.60f, 0.90f, 0.4f),
            new AssemblyPartMaterial(new Vector3(0.20f, 0.20f, 0.22f), 0.15f, 0.70f, 0.85f, 0.4f)
        });

    public static readonly AssemblyMaterialPresetDefinition MoogBlack = new(
        "Moog Black",
        "Soft-touch rubber button inspired by classic synths.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.04f, 0.04f, 0.04f), 0.0f, 0.88f, 0.90f, 0.2f),
            new AssemblyPartMaterial(new Vector3(0.06f, 0.06f, 0.06f), 0.0f, 0.92f, 0.92f, 0.15f),
            new AssemblyPartMaterial(new Vector3(0.03f, 0.03f, 0.03f), 0.0f, 0.90f, 0.90f, 0.2f)
        });

    public static readonly AssemblyMaterialPresetDefinition ArcadeGlow = new(
        "Arcade Glow",
        "Translucent colored button with backlit appearance.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.08f, 0.08f, 0.10f), 0.2f, 0.55f, 0.85f, 0.8f),
            new AssemblyPartMaterial(new Vector3(0.30f, 0.72f, 0.90f), 0.0f, 0.35f, 0.95f, 1.2f),
            new AssemblyPartMaterial(new Vector3(0.06f, 0.06f, 0.08f), 0.2f, 0.60f, 0.85f, 0.6f)
        });

    public static readonly AssemblyMaterialPresetDefinition BrushedMetal = new(
        "Brushed Metal",
        "Industrial brushed stainless steel button.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.15f, 0.15f, 0.18f), 0.85f, 0.50f, 0.82f, 1.0f),
            new AssemblyPartMaterial(new Vector3(0.70f, 0.70f, 0.73f), 0.95f, 0.38f, 0.85f, 1.4f),
            new AssemblyPartMaterial(new Vector3(0.15f, 0.15f, 0.18f), 0.85f, 0.55f, 0.82f, 0.9f)
        });

    public static IReadOnlyList<PushButtonMaterialPresetId> GetPresetIds() => OrderedPresetIds;

    public static string GetDisplayName(PushButtonMaterialPresetId id) => Resolve(id).Name;

    public static AssemblyMaterialPresetDefinition Resolve(PushButtonMaterialPresetId id) => id switch
    {
        PushButtonMaterialPresetId.MoogBlack => MoogBlack,
        PushButtonMaterialPresetId.ArcadeGlow => ArcadeGlow,
        PushButtonMaterialPresetId.BrushedMetal => BrushedMetal,
        _ => NeveGray
    };

    public static bool IsSupported(PushButtonMaterialPresetId id)
    {
        return id == PushButtonMaterialPresetId.Custom || Array.IndexOf(OrderedPresetIds, id) >= 0;
    }
}
