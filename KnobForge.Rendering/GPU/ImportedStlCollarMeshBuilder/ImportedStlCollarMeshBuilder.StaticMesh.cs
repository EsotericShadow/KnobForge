using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace KnobForge.Rendering.GPU;

public static partial class ImportedStlCollarMeshBuilder
{
    public static bool TryBuildStaticMeshFromPath(
        string path,
        float targetWidth,
        float targetHeight,
        float targetDepth,
        out MetalVertex[] vertices,
        out uint[] indices,
        out float referenceRadius)
    {
        vertices = Array.Empty<MetalVertex>();
        indices = Array.Empty<uint>();
        referenceRadius = 0f;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        string extension = Path.GetExtension(path);
        bool readOk;
        List<Vector3> sourcePositions;
        List<uint> sourceIndices;
        if (string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase))
        {
            readOk = TryReadBinaryGlb(path, out sourcePositions, out sourceIndices);
        }
        else if (string.Equals(extension, ".stl", StringComparison.OrdinalIgnoreCase))
        {
            readOk = TryReadBinaryStl(path, out sourcePositions, out sourceIndices);
        }
        else
        {
            readOk = false;
            sourcePositions = new List<Vector3>();
            sourceIndices = new List<uint>();
        }

        if (!readOk ||
            sourcePositions.Count == 0 ||
            sourceIndices.Count < 3)
        {
            return false;
        }

        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        for (int i = 0; i < sourcePositions.Count; i++)
        {
            Vector3 p = sourcePositions[i];
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 extents = max - min;
        float sourceWidth = MathF.Max(1e-5f, extents.X);
        float sourceHeight = MathF.Max(1e-5f, extents.Y);
        float sourceDepth = MathF.Max(1e-5f, extents.Z);
        float sx = targetWidth / sourceWidth;
        float sy = targetHeight / sourceHeight;
        float sz = targetDepth / sourceDepth;
        float uniformScale = MathF.Max(1e-5f, MathF.Min(sx, MathF.Min(sy, sz)));

        var positions = new Vector3[sourcePositions.Count];
        for (int i = 0; i < sourcePositions.Count; i++)
        {
            positions[i] = (sourcePositions[i] - center) * uniformScale;
        }

        var mutableIndices = new List<uint>(sourceIndices);
        NormalizeTriangleWinding(positions, mutableIndices);
        Vector3[] normals = ComputeVertexNormals(positions, mutableIndices);

        vertices = new MetalVertex[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 n = normals[i];
            Vector3 tangent = Vector3.Cross(Vector3.UnitZ, n);
            if (tangent.LengthSquared() <= 1e-8f)
            {
                tangent = Vector3.Cross(Vector3.UnitX, n);
            }

            tangent = tangent.LengthSquared() > 1e-8f
                ? Vector3.Normalize(tangent)
                : Vector3.UnitX;

            vertices[i] = new MetalVertex
            {
                Position = positions[i],
                Normal = n,
                Tangent = new Vector4(tangent, 1f)
            };
        }

        indices = mutableIndices.ToArray();

        for (int i = 0; i < positions.Length; i++)
        {
            referenceRadius = MathF.Max(referenceRadius, positions[i].Length());
        }

        return vertices.Length > 0 && indices.Length >= 3;
    }
}
