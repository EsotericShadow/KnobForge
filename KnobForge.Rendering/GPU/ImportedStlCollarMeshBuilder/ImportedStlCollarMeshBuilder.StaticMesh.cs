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
        List<Vector3>? sourceNormals = null;
        List<Vector2>? sourceTexcoords = null;
        if (string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase))
        {
            readOk = TryReadBinaryGlb(path, out ImportedMeshData sourceMesh);
            sourcePositions = readOk ? sourceMesh.Positions : new List<Vector3>();
            sourceIndices = readOk ? sourceMesh.Indices : new List<uint>();
            sourceNormals = readOk ? sourceMesh.Normals : null;
            sourceTexcoords = readOk ? sourceMesh.Texcoords : null;
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
        Vector3[] normals = sourceNormals is not null && sourceNormals.Count == positions.Length
            ? sourceNormals.Select(n => n.LengthSquared() > 1e-8f ? Vector3.Normalize(n) : Vector3.UnitZ).ToArray()
            : ComputeVertexNormals(positions, mutableIndices);
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        for (int i = 0; i < positions.Length; i++)
        {
            minZ = MathF.Min(minZ, positions[i].Z);
            maxZ = MathF.Max(maxZ, positions[i].Z);
        }

        float zSpan = MathF.Max(1e-6f, maxZ - minZ);

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

            Vector2 uv;
            if (sourceTexcoords is not null && sourceTexcoords.Count == positions.Length)
            {
                uv = sourceTexcoords[i];
            }
            else
            {
                float angle = MathF.Atan2(positions[i].Y, positions[i].X);
                float u = Wrap01((angle / (MathF.PI * 2f)) + 0.5f);
                float v = (positions[i].Z - minZ) / zSpan;
                uv = new Vector2(u, v);
            }

            vertices[i] = new MetalVertex
            {
                Position = positions[i],
                Normal = n,
                Tangent = new Vector4(tangent, 1f),
                Texcoord = uv
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
