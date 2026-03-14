namespace KnobForge.Core.MaterialGraph;

public static class NoiseUtils
{
    public static float Fract(float v) => v - MathF.Floor(v);

    public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);

    public static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    public static float SmoothStep(float edge0, float edge1, float value)
    {
        if (MathF.Abs(edge1 - edge0) <= 1e-6f)
        {
            return value < edge0 ? 0f : 1f;
        }

        float t = Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    public static float Hash21(float x, float y)
    {
        float px = Fract(x * 123.34f);
        float py = Fract(y * 456.21f);
        float d = (px * (px + 45.32f)) + (py * (py + 45.32f));
        px += d;
        py += d;
        return Fract(px * py);
    }

    public static float ValueNoise2(float x, float y)
    {
        float ix = MathF.Floor(x);
        float iy = MathF.Floor(y);
        float fx = x - ix;
        float fy = y - iy;
        float a = Hash21(ix, iy);
        float b = Hash21(ix + 1f, iy);
        float c = Hash21(ix, iy + 1f);
        float d = Hash21(ix + 1f, iy + 1f);
        float ux = fx * fx * (3f - (2f * fx));
        float uy = fy * fy * (3f - (2f * fy));
        float ab = Lerp(a, b, ux);
        float cd = Lerp(c, d, ux);
        return Lerp(ab, cd, uy);
    }
}
