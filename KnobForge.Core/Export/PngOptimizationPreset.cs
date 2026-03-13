namespace KnobForge.Core.Export
{
    public enum PngOptimizationPreset
    {
        Lossless,
        Safe,
        Balanced,
        Aggressive,
        Custom
    }

    public readonly record struct PngOptimizationProfileDefinition(
        PngOptimizationPreset Preset,
        string DisplayName,
        string Description,
        int MinimumSavingsBytes,
        int OpaqueRgbStep,
        int OpaqueAlphaStep,
        int TranslucentRgbStep,
        int TranslucentAlphaStep,
        byte TranslucentAlphaThreshold,
        byte MaxOpaqueRgbDelta,
        byte MaxVisibleRgbDelta,
        byte MaxVisibleAlphaDelta,
        float MeanVisibleLumaDelta,
        float MeanVisibleAlphaDelta);

    public static class PngOptimizationProfiles
    {
        public static PngOptimizationProfileDefinition Get(PngOptimizationPreset preset)
        {
            return preset switch
            {
                PngOptimizationPreset.Lossless => new(
                    preset,
                    "Lossless",
                    "No perceptual quantization. Only PNG deflate compression is applied.",
                    0,
                    1,
                    1,
                    1,
                    1,
                    0,
                    0,
                    0,
                    0,
                    0f,
                    0f),
                PngOptimizationPreset.Safe => new(
                    preset,
                    "Safe",
                    "Very conservative near-lossless tuning with strong protection for gradients and highlights.",
                    4096,
                    1,
                    1,
                    4,
                    1,
                    96,
                    1,
                    3,
                    1,
                    0.5f,
                    0.5f),
                PngOptimizationPreset.Aggressive => new(
                    preset,
                    "Aggressive",
                    "Pushes harder on translucent gradients and can produce much smaller PNGs with higher visual risk.",
                    1024,
                    2,
                    2,
                    8,
                    2,
                    80,
                    2,
                    6,
                    3,
                    1.2f,
                    1.0f),
                PngOptimizationPreset.Custom => Get(PngOptimizationPreset.Balanced) with
                {
                    Preset = PngOptimizationPreset.Custom,
                    DisplayName = "Custom",
                    Description = "Use the manual compression values below."
                },
                _ => new(
                    PngOptimizationPreset.Balanced,
                    "Balanced",
                    "Default near-lossless tuning. Good size reduction with low visible risk.",
                    2048,
                    2,
                    1,
                    4,
                    1,
                    128,
                    1,
                    4,
                    2,
                    0.75f,
                    0.75f)
            };
        }
    }
}
