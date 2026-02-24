using System;
using System.Numerics;

namespace KnobForge.Core
{
    public enum IndicatorLensMaterialPresetId
    {
        Clear = 0,
        Frosted = 1,
        SaturatedLed = 2
    }

    public readonly record struct IndicatorLensMaterialPresetDefinition(
        float Transmission,
        float Ior,
        float Thickness,
        Vector3 Tint,
        float Absorption,
        float SurfaceRoughness,
        float SurfaceSpecularStrength);

    public static class IndicatorLensMaterialPresets
    {
        public static readonly IndicatorLensMaterialPresetDefinition Clear = new(
            Transmission: 0.96f,
            Ior: 1.58f,
            Thickness: 1.45f,
            Tint: new Vector3(0.92f, 0.98f, 0.95f),
            Absorption: 0.55f,
            SurfaceRoughness: 0.05f,
            SurfaceSpecularStrength: 1.85f);

        public static readonly IndicatorLensMaterialPresetDefinition Frosted = new(
            Transmission: 0.70f,
            Ior: 1.56f,
            Thickness: 1.90f,
            Tint: new Vector3(0.90f, 0.96f, 0.93f),
            Absorption: 0.95f,
            SurfaceRoughness: 0.26f,
            SurfaceSpecularStrength: 1.45f);

        public static readonly IndicatorLensMaterialPresetDefinition SaturatedLed = new(
            Transmission: 0.98f,
            Ior: 1.60f,
            Thickness: 1.65f,
            Tint: new Vector3(0.48f, 0.96f, 0.62f),
            Absorption: 2.80f,
            SurfaceRoughness: 0.05f,
            SurfaceSpecularStrength: 2.10f);

        public static IndicatorLensMaterialPresetDefinition Resolve(IndicatorLensMaterialPresetId preset)
        {
            return preset switch
            {
                IndicatorLensMaterialPresetId.Frosted => Frosted,
                IndicatorLensMaterialPresetId.SaturatedLed => SaturatedLed,
                _ => Clear
            };
        }

        public static bool IsWithinSupportedRange(IndicatorLensMaterialPresetDefinition preset)
        {
            return
                float.IsFinite(preset.Transmission) &&
                float.IsFinite(preset.Ior) &&
                float.IsFinite(preset.Thickness) &&
                float.IsFinite(preset.Tint.X) &&
                float.IsFinite(preset.Tint.Y) &&
                float.IsFinite(preset.Tint.Z) &&
                float.IsFinite(preset.Absorption) &&
                float.IsFinite(preset.SurfaceRoughness) &&
                float.IsFinite(preset.SurfaceSpecularStrength) &&
                preset.Transmission >= 0f && preset.Transmission <= 1f &&
                preset.Ior >= 1f && preset.Ior <= 2.5f &&
                preset.Thickness >= 0f && preset.Thickness <= 10f &&
                preset.Tint.X >= 0f && preset.Tint.X <= 1f &&
                preset.Tint.Y >= 0f && preset.Tint.Y <= 1f &&
                preset.Tint.Z >= 0f && preset.Tint.Z <= 1f &&
                preset.Absorption >= 0f && preset.Absorption <= 8f &&
                preset.SurfaceRoughness >= 0.04f && preset.SurfaceRoughness <= 1f &&
                preset.SurfaceSpecularStrength >= 0f && preset.SurfaceSpecularStrength <= 2.5f;
        }
    }
}
