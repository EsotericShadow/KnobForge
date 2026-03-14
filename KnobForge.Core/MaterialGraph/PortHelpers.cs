using System.Numerics;

namespace KnobForge.Core.MaterialGraph;

public static class PortHelpers
{
    public static float ToFloat(float[] v) => v.Length > 0 ? v[0] : 0f;

    public static Vector2 ToFloat2(float[] v)
    {
        float[] value = BroadcastTo(v, 2);
        return new Vector2(value[0], value[1]);
    }

    public static Vector3 ToFloat3(float[] v)
    {
        float[] value = BroadcastTo(v, 3);
        return new Vector3(value[0], value[1], value[2]);
    }

    public static Vector4 ToFloat4(float[] v)
    {
        float[] value = BroadcastTo(v, 4);
        return new Vector4(value[0], value[1], value[2], value[3]);
    }

    public static float[] FromFloat(float f) => new[] { f };
    public static float[] FromFloat2(Vector2 v) => new[] { v.X, v.Y };
    public static float[] FromFloat3(Vector3 v) => new[] { v.X, v.Y, v.Z };
    public static float[] FromFloat4(Vector4 v) => new[] { v.X, v.Y, v.Z, v.W };

    public static float[] BroadcastTo(float[] value, int targetDimension)
    {
        if (value.Length >= targetDimension)
        {
            return value;
        }

        if (value.Length == 1)
        {
            var result = new float[targetDimension];
            Array.Fill(result, value[0]);
            return result;
        }

        var padded = new float[targetDimension];
        Array.Copy(value, padded, Math.Min(value.Length, targetDimension));
        return padded;
    }

    public static int GetDimension(PortType type) => type switch
    {
        PortType.Float => 1,
        PortType.Float2 => 2,
        PortType.Float3 => 3,
        PortType.Color => 3,
        PortType.Float4 => 4,
        _ => 1
    };
}
