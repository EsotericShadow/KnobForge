using System.Collections.Generic;
using System.Numerics;

namespace KnobForge.Core;

public readonly record struct EnvironmentPresetDefinition(
    EnvironmentPreset Preset,
    string DisplayName,
    Vector3 TopColor,
    Vector3 BottomColor,
    float Intensity,
    float RoughnessMix);

public static class EnvironmentPresets
{
    private static readonly EnvironmentPresetDefinition[] Definitions =
    {
        new(
            EnvironmentPreset.Studio,
            "Studio",
            new Vector3(0.85f, 0.82f, 0.78f),
            new Vector3(0.18f, 0.17f, 0.20f),
            1.20f,
            0.75f),
        new(
            EnvironmentPreset.Rack,
            "Rack",
            new Vector3(0.22f, 0.24f, 0.30f),
            new Vector3(0.05f, 0.04f, 0.06f),
            0.85f,
            0.60f),
        new(
            EnvironmentPreset.Showroom,
            "Showroom",
            new Vector3(1.00f, 0.97f, 0.92f),
            new Vector3(0.35f, 0.32f, 0.28f),
            1.50f,
            0.90f),
        new(
            EnvironmentPreset.Dark,
            "Dark",
            new Vector3(0.10f, 0.10f, 0.12f),
            new Vector3(0.03f, 0.02f, 0.02f),
            0.55f,
            0.40f)
    };

    public static IReadOnlyList<EnvironmentPresetDefinition> All => Definitions;

    public static EnvironmentPresetDefinition? Get(EnvironmentPreset preset)
    {
        foreach (EnvironmentPresetDefinition definition in Definitions)
        {
            if (definition.Preset == preset)
            {
                return definition;
            }
        }

        return null;
    }
}
