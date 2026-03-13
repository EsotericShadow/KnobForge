using System;
using System.Collections.Generic;
using System.Numerics;

namespace KnobForge.Core
{
    public static class PaintLayerCompositor
    {
        public static void Composite(IReadOnlyList<PaintLayer> layers, byte[] outputRgba8, int size)
        {
            CompositeMask(layers, outputRgba8, size, -1, 1f);
        }

        public static void CompositeMask(
            IReadOnlyList<PaintLayer> layers,
            byte[] outputRgba8,
            int size,
            int focusedLayerIndex,
            float nonFocusedOpacityScale)
        {
            CompositeCore(
                layers,
                outputRgba8,
                size,
                layer => layer.PixelData,
                focusedLayerIndex,
                nonFocusedOpacityScale);
        }

        public static void CompositeColor(
            IReadOnlyList<PaintLayer> layers,
            byte[] outputRgba8,
            int size,
            int focusedLayerIndex,
            float nonFocusedOpacityScale)
        {
            CompositeCore(
                layers,
                outputRgba8,
                size,
                layer => layer.ColorPixelData,
                focusedLayerIndex,
                nonFocusedOpacityScale);
        }

        public static void CompositeMask2(
            IReadOnlyList<PaintLayer> layers,
            byte[] outputRgba8,
            int size,
            int focusedLayerIndex,
            float nonFocusedOpacityScale)
        {
            CompositeCore(
                layers,
                outputRgba8,
                size,
                layer => layer.PixelData2,
                focusedLayerIndex,
                nonFocusedOpacityScale);
        }

        private static void CompositeCore(
            IReadOnlyList<PaintLayer> layers,
            byte[] outputRgba8,
            int size,
            Func<PaintLayer, byte[]?> pixelSelector,
            int focusedLayerIndex,
            float nonFocusedOpacityScale)
        {
            Array.Clear(outputRgba8, 0, outputRgba8.Length);
            if (layers.Count == 0 || size <= 0)
            {
                return;
            }

            int expectedLength = size * size * 4;
            if (outputRgba8.Length < expectedLength)
            {
                throw new ArgumentException("Output buffer is smaller than the requested paint-mask size.", nameof(outputRgba8));
            }

            float focusScale = Math.Clamp(nonFocusedOpacityScale, 0f, 1f);
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                PaintLayer layer = layers[layerIndex];
                if (!layer.Visible)
                {
                    continue;
                }

                byte[]? source = pixelSelector(layer);
                if (source == null || source.Length < expectedLength)
                {
                    continue;
                }

                float layerOpacity = Math.Clamp(layer.Opacity, 0f, 1f);
                if (focusedLayerIndex >= 0 && layerIndex != focusedLayerIndex)
                {
                    layerOpacity *= focusScale;
                }

                if (layerOpacity <= 1e-6f)
                {
                    continue;
                }

                for (int idx = 0; idx < expectedLength; idx += 4)
                {
                    float srcA = (source[idx + 3] / 255f) * layerOpacity;
                    if (srcA <= 1e-6f)
                    {
                        continue;
                    }

                    Vector3 dstRgb = new(
                        outputRgba8[idx + 0] / 255f,
                        outputRgba8[idx + 1] / 255f,
                        outputRgba8[idx + 2] / 255f);
                    Vector3 srcRgb = new(
                        source[idx + 0] / 255f,
                        source[idx + 1] / 255f,
                        source[idx + 2] / 255f);

                    Vector3 blendedRgb = ApplyBlendMode(layer.BlendMode, dstRgb, srcRgb);
                    Vector3 outRgb = Vector3.Lerp(dstRgb, blendedRgb, srcA);
                    float dstA = outputRgba8[idx + 3] / 255f;
                    float outA = srcA + (dstA * (1f - srcA));

                    outputRgba8[idx + 0] = ToByte(outRgb.X);
                    outputRgba8[idx + 1] = ToByte(outRgb.Y);
                    outputRgba8[idx + 2] = ToByte(outRgb.Z);
                    outputRgba8[idx + 3] = ToByte(outA);
                }
            }
        }

        private static Vector3 ApplyBlendMode(PaintBlendMode mode, Vector3 dst, Vector3 src)
        {
            return mode switch
            {
                PaintBlendMode.Multiply => new Vector3(dst.X * src.X, dst.Y * src.Y, dst.Z * src.Z),
                PaintBlendMode.Screen => new Vector3(
                    1f - ((1f - dst.X) * (1f - src.X)),
                    1f - ((1f - dst.Y) * (1f - src.Y)),
                    1f - ((1f - dst.Z) * (1f - src.Z))),
                PaintBlendMode.Overlay => new Vector3(
                    ApplyOverlay(dst.X, src.X),
                    ApplyOverlay(dst.Y, src.Y),
                    ApplyOverlay(dst.Z, src.Z)),
                PaintBlendMode.Add => Vector3.Min(Vector3.One, dst + src),
                _ => src
            };
        }

        private static float ApplyOverlay(float dst, float src)
        {
            return dst < 0.5f
                ? 2f * dst * src
                : 1f - (2f * (1f - dst) * (1f - src));
        }

        private static byte ToByte(float value)
        {
            return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
        }
    }
}
