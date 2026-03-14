using System.Numerics;

namespace KnobForge.Core.MaterialGraph;

public sealed class GraphEvaluationContext
{
    private readonly Dictionary<(Guid nodeId, string portName), float[]> _portValues = new();

    public Vector2 UV { get; set; }
    public Vector3 WorldPosition { get; set; }
    public Vector3 WorldNormal { get; set; } = Vector3.UnitY;
    public MaterialGraph? Graph { get; set; }
    public Dictionary<string, TextureData> LoadedTextures { get; } = new(StringComparer.Ordinal);

    public void SetPortValue(Guid nodeId, string portName, float[] value)
    {
        _portValues[(nodeId, portName)] = value;
    }

    public float[] GetPortValue(Guid nodeId, string portName)
    {
        return _portValues.TryGetValue((nodeId, portName), out float[]? value) ? value : Array.Empty<float>();
    }

    public bool HasInputConnection(Guid nodeId, string portName)
    {
        return Graph != null && Graph.Connections.Any(c => c.TargetNodeId == nodeId && string.Equals(c.TargetPortName, portName, StringComparison.Ordinal));
    }

    public float[] GetInputValue(Guid nodeId, string portName, MaterialGraph graph)
    {
        GraphConnection? conn = graph.Connections.FirstOrDefault(c =>
            c.TargetNodeId == nodeId && string.Equals(c.TargetPortName, portName, StringComparison.Ordinal));

        if (conn != null)
        {
            return GetPortValue(conn.SourceNodeId, conn.SourcePortName);
        }

        GraphNode? node = graph.GetNodeById(nodeId);
        GraphPort? port = node?.GetPorts().FirstOrDefault(p =>
            string.Equals(p.Name, portName, StringComparison.Ordinal) && p.Direction == PortDirection.Input);
        return port?.DefaultValue ?? Array.Empty<float>();
    }

    public void Clear()
    {
        _portValues.Clear();
    }
}

public sealed class TextureData
{
    public int Width { get; init; }
    public int Height { get; init; }
    public byte[] Rgba8 { get; init; } = Array.Empty<byte>();

    public Vector4 SampleBilinear(float u, float v)
    {
        if (Width <= 0 || Height <= 0 || Rgba8.Length == 0)
        {
            return Vector4.Zero;
        }

        u = u - MathF.Floor(u);
        v = v - MathF.Floor(v);

        float fx = u * (Width - 1);
        float fy = v * (Height - 1);
        int x0 = (int)MathF.Floor(fx);
        int y0 = (int)MathF.Floor(fy);
        int x1 = Math.Min(x0 + 1, Width - 1);
        int y1 = Math.Min(y0 + 1, Height - 1);
        float tx = fx - x0;
        float ty = fy - y0;

        Vector4 c00 = ReadPixel(x0, y0);
        Vector4 c10 = ReadPixel(x1, y0);
        Vector4 c01 = ReadPixel(x0, y1);
        Vector4 c11 = ReadPixel(x1, y1);

        Vector4 top = Vector4.Lerp(c00, c10, tx);
        Vector4 bottom = Vector4.Lerp(c01, c11, tx);
        return Vector4.Lerp(top, bottom, ty);
    }

    private Vector4 ReadPixel(int x, int y)
    {
        int offset = (y * Width + x) * 4;
        if (offset + 3 >= Rgba8.Length)
        {
            return Vector4.Zero;
        }

        return new Vector4(
            Rgba8[offset] / 255f,
            Rgba8[offset + 1] / 255f,
            Rgba8[offset + 2] / 255f,
            Rgba8[offset + 3] / 255f);
    }
}
