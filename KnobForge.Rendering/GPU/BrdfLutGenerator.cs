using System;
using System.Numerics;

namespace KnobForge.Rendering.GPU;

public static class BrdfLutGenerator
{
    public const int LutSize = 256;

    private static readonly Lazy<float[]> CachedData = new(GenerateCore);

    public static float[] Generate() => CachedData.Value;

    private static float[] GenerateCore()
    {
        var data = new float[LutSize * LutSize * 2];
        for (int y = 0; y < LutSize; y++)
        {
            float roughness = (y + 0.5f) / LutSize;
            float alpha = roughness * roughness;
            for (int x = 0; x < LutSize; x++)
            {
                float ndotv = MathF.Max((x + 0.5f) / LutSize, 1e-4f);
                (float scale, float bias) = IntegrateBrdf(ndotv, alpha);
                int index = (y * LutSize + x) * 2;
                data[index] = scale;
                data[index + 1] = bias;
            }
        }

        return data;
    }

    private static (float scale, float bias) IntegrateBrdf(float ndotv, float alpha)
    {
        float sinTheta = MathF.Sqrt(MathF.Max(0f, 1f - ndotv * ndotv));
        var v = new Vector3(sinTheta, 0f, ndotv);

        float scale = 0f;
        float bias = 0f;
        const int SampleCount = 1024;
        float alphaSq = alpha * alpha;

        for (int i = 0; i < SampleCount; i++)
        {
            float xi1 = (float)i / SampleCount;
            float xi2 = RadicalInverseVdC((uint)i);

            float phi = 2f * MathF.PI * xi1;
            float cosTheta = MathF.Sqrt(MathF.Max(0f, (1f - xi2) / (1f + (alphaSq - 1f) * xi2)));
            float sinThetaH = MathF.Sqrt(MathF.Max(0f, 1f - cosTheta * cosTheta));
            var h = new Vector3(
                MathF.Cos(phi) * sinThetaH,
                MathF.Sin(phi) * sinThetaH,
                cosTheta);

            float vdoth = MathF.Max(Vector3.Dot(v, h), 0f);
            Vector3 l = (2f * vdoth * h) - v;
            float ndotl = MathF.Max(l.Z, 0f);
            float ndoth = MathF.Max(h.Z, 0f);

            if (ndotl <= 0f)
            {
                continue;
            }

            float g = SmithGgx(ndotv, ndotl, alpha);
            float gVis = g * vdoth / MathF.Max(ndoth * ndotv, 1e-7f);
            float fc = MathF.Pow(1f - vdoth, 5f);
            scale += (1f - fc) * gVis;
            bias += fc * gVis;
        }

        return (scale / SampleCount, bias / SampleCount);
    }

    private static float SmithGgx(float ndotv, float ndotl, float alpha)
    {
        float k = alpha / 2f;
        float gv = ndotv / (ndotv * (1f - k) + k);
        float gl = ndotl / (ndotl * (1f - k) + k);
        return gv * gl;
    }

    private static float RadicalInverseVdC(uint bits)
    {
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        return bits * 2.3283064365386963e-10f;
    }
}
