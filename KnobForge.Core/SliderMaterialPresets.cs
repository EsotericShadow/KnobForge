using System;
using System.Collections.Generic;
using System.Numerics;

namespace KnobForge.Core;

public enum SliderMaterialPresetId
{
    Custom = -1,
    ConsoleStrip = 0,
    ModularSynth = 1,
    VintageConsole = 2,
    StudioFader = 3
}

public static class SliderMaterialPresets
{
    private static readonly SliderMaterialPresetId[] OrderedPresetIds =
    {
        SliderMaterialPresetId.ConsoleStrip,
        SliderMaterialPresetId.ModularSynth,
        SliderMaterialPresetId.VintageConsole,
        SliderMaterialPresetId.StudioFader
    };

    public static readonly AssemblyMaterialPresetDefinition ConsoleStrip = new(
        "Console Strip",
        "Classic console channel strip fader look.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.12f, 0.12f, 0.14f), 0.1f, 0.70f, 0.85f, 0.4f),
            new AssemblyPartMaterial(new Vector3(0.92f, 0.92f, 0.92f), 0.0f, 0.45f, 0.95f, 0.6f)
        });

    public static readonly AssemblyMaterialPresetDefinition ModularSynth = new(
        "Modular Synth",
        "Eurorack-style anodized slider.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.02f, 0.02f, 0.02f), 0.9f, 0.25f, 0.80f, 1.6f),
            new AssemblyPartMaterial(new Vector3(0.85f, 0.25f, 0.15f), 0.0f, 0.55f, 0.90f, 0.5f)
        });

    public static readonly AssemblyMaterialPresetDefinition VintageConsole = new(
        "Vintage Console",
        "Warm vintage mixing console fader.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.88f, 0.84f, 0.72f), 0.0f, 0.60f, 0.90f, 0.5f),
            new AssemblyPartMaterial(new Vector3(0.80f, 0.80f, 0.82f), 0.95f, 0.18f, 0.85f, 1.7f)
        });

    public static readonly AssemblyMaterialPresetDefinition StudioFader = new(
        "Studio Fader",
        "Modern studio fader with brushed aluminum.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.72f, 0.72f, 0.75f), 0.95f, 0.40f, 0.85f, 1.2f),
            new AssemblyPartMaterial(new Vector3(0.05f, 0.05f, 0.05f), 0.0f, 0.82f, 0.90f, 0.3f)
        });

    public static IReadOnlyList<SliderMaterialPresetId> GetPresetIds() => OrderedPresetIds;

    public static string GetDisplayName(SliderMaterialPresetId id) => Resolve(id).Name;

    public static AssemblyMaterialPresetDefinition Resolve(SliderMaterialPresetId id) => id switch
    {
        SliderMaterialPresetId.ModularSynth => ModularSynth,
        SliderMaterialPresetId.VintageConsole => VintageConsole,
        SliderMaterialPresetId.StudioFader => StudioFader,
        _ => ConsoleStrip
    };

    public static bool IsSupported(SliderMaterialPresetId id)
    {
        return id == SliderMaterialPresetId.Custom || Array.IndexOf(OrderedPresetIds, id) >= 0;
    }
}
