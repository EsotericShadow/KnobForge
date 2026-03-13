using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using KnobForge.Core;
using KnobForge.Core.Scene;


namespace KnobForge.Rendering.GPU;

public static partial class ImportedStlCollarMeshBuilder
{
    private static bool TryReadImportedMesh(string path, out ImportedMeshData meshData)
    {
        meshData = new ImportedMeshData();
        long fileTicks;
        try
        {
            fileTicks = File.GetLastWriteTimeUtc(path).Ticks;
        }
        catch
        {
            return false;
        }

        lock (ImportedMeshCacheLock)
        {
            if (_cachedImportedMeshData is not null &&
                string.Equals(_cachedImportedMeshPath, path, StringComparison.Ordinal) &&
                _cachedImportedMeshTicks == fileTicks)
            {
                meshData = CloneImportedMeshData(_cachedImportedMeshData);
                return true;
            }
        }

        string extension = Path.GetExtension(path);
        bool readOk;
        if (string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase))
        {
            readOk = TryReadBinaryGlb(path, out meshData);
        }
        else if (string.Equals(extension, ".stl", StringComparison.OrdinalIgnoreCase) &&
                 TryReadBinaryStl(path, out List<Vector3> positions, out List<uint> indices))
        {
            meshData = new ImportedMeshData
            {
                Positions = positions,
                Indices = indices
            };
            readOk = true;
        }
        else
        {
            readOk = false;
        }

        if (!readOk || meshData.Positions.Count == 0 || meshData.Indices.Count < 3)
        {
            meshData = new ImportedMeshData();
            return false;
        }

        if (TryExtractLikelyCollarComponent(meshData, out ImportedMeshData extractedMesh))
        {
            meshData = extractedMesh;
        }

        lock (ImportedMeshCacheLock)
        {
            _cachedImportedMeshPath = path;
            _cachedImportedMeshTicks = fileTicks;
            _cachedImportedMeshData = CloneImportedMeshData(meshData);
        }

        return true;
    }

    private static bool TryExtractLikelyCollarComponent(
        ImportedMeshData sourceMesh,
        out ImportedMeshData extractedMesh)
    {
        extractedMesh = new ImportedMeshData();

        IReadOnlyList<Vector3> sourcePositions = sourceMesh.Positions;
        IReadOnlyList<uint> sourceIndices = sourceMesh.Indices;
        IReadOnlyList<Vector3>? sourceNormals = sourceMesh.Normals;
        IReadOnlyList<Vector2>? sourceTexcoords = sourceMesh.Texcoords;
        bool hasNormals = sourceNormals is not null && sourceNormals.Count == sourcePositions.Count;
        bool hasTexcoords = sourceTexcoords is not null && sourceTexcoords.Count == sourcePositions.Count;

        var extractedPositions = new List<Vector3>();
        var extractedIndices = new List<uint>();
        List<Vector3>? extractedNormals = hasNormals ? new List<Vector3>() : null;
        List<Vector2>? extractedTexcoords = hasTexcoords ? new List<Vector2>() : null;

        int vertexCount = sourcePositions.Count;
        int triangleCount = sourceIndices.Count / 3;
        if (vertexCount == 0 || triangleCount < 3)
        {
            return false;
        }

        var dsu = new DisjointSet(vertexCount);
        for (int tri = 0; tri < triangleCount; tri++)
        {
            int baseIndex = tri * 3;
            int i0 = (int)sourceIndices[baseIndex + 0];
            int i1 = (int)sourceIndices[baseIndex + 1];
            int i2 = (int)sourceIndices[baseIndex + 2];
            if (!IsVertexIndexValid(i0, vertexCount) ||
                !IsVertexIndexValid(i1, vertexCount) ||
                !IsVertexIndexValid(i2, vertexCount) ||
                i0 == i1 || i1 == i2 || i2 == i0)
            {
                continue;
            }

            dsu.Union(i0, i1);
            dsu.Union(i1, i2);
            dsu.Union(i2, i0);
        }

        int[] rootByVertex = new int[vertexCount];
        var verticesByRoot = new Dictionary<int, List<int>>();
        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            int root = dsu.Find(vertex);
            rootByVertex[vertex] = root;
            if (!verticesByRoot.TryGetValue(root, out List<int>? list))
            {
                list = new List<int>();
                verticesByRoot[root] = list;
            }

            list.Add(vertex);
        }

        if (verticesByRoot.Count <= 1)
        {
            return false;
        }

        var trianglesByRoot = new Dictionary<int, List<int>>();
        for (int tri = 0; tri < triangleCount; tri++)
        {
            int baseIndex = tri * 3;
            int i0 = (int)sourceIndices[baseIndex + 0];
            int i1 = (int)sourceIndices[baseIndex + 1];
            int i2 = (int)sourceIndices[baseIndex + 2];
            if (!IsVertexIndexValid(i0, vertexCount) ||
                !IsVertexIndexValid(i1, vertexCount) ||
                !IsVertexIndexValid(i2, vertexCount) ||
                i0 == i1 || i1 == i2 || i2 == i0)
            {
                continue;
            }

            int root = rootByVertex[i0];
            if (rootByVertex[i1] != root || rootByVertex[i2] != root)
            {
                continue;
            }

            if (!trianglesByRoot.TryGetValue(root, out List<int>? list))
            {
                list = new List<int>();
                trianglesByRoot[root] = list;
            }

            list.Add(tri);
        }

        int minTriangles = Math.Max(128, triangleCount / 200);
        int minVertices = Math.Max(128, vertexCount / 300);
        int bestRoot = int.MinValue;
        float bestScore = float.MinValue;
        foreach ((int root, List<int> triangles) in trianglesByRoot)
        {
            if (triangles.Count < minTriangles)
            {
                continue;
            }

            if (!verticesByRoot.TryGetValue(root, out List<int>? componentVertices) ||
                componentVertices.Count < minVertices)
            {
                continue;
            }

            var radii = new List<float>(componentVertices.Count);
            for (int i = 0; i < componentVertices.Count; i++)
            {
                Vector3 p = sourcePositions[componentVertices[i]];
                radii.Add(new Vector2(p.X, p.Y).Length());
            }

            if (radii.Count < minVertices)
            {
                continue;
            }

            radii.Sort();
            float r05 = Percentile(radii, 0.05f);
            float r50 = Percentile(radii, 0.50f);
            float r95 = Percentile(radii, 0.95f);
            if (r95 <= 1e-6f)
            {
                continue;
            }

            float holeRatio = r05 / r95;
            float triangleFraction = triangles.Count / (float)triangleCount;
            float score = (holeRatio * 12f) + r50 + (triangleFraction * 2f);
            if (score > bestScore)
            {
                bestScore = score;
                bestRoot = root;
            }
        }

        if (bestRoot == int.MinValue ||
            !trianglesByRoot.TryGetValue(bestRoot, out List<int>? selectedTriangles) ||
            !verticesByRoot.TryGetValue(bestRoot, out List<int>? selectedVertices))
        {
            return false;
        }

        if (selectedTriangles.Count <= 0 || selectedTriangles.Count >= triangleCount)
        {
            return false;
        }

        var oldToNew = new Dictionary<int, uint>(selectedVertices.Count);
        for (int i = 0; i < selectedVertices.Count; i++)
        {
            int oldIndex = selectedVertices[i];
            uint newIndex = (uint)extractedPositions.Count;
            oldToNew[oldIndex] = newIndex;
            extractedPositions.Add(sourcePositions[oldIndex]);
            if (extractedNormals is not null)
            {
                extractedNormals.Add(sourceNormals![oldIndex]);
            }

            if (extractedTexcoords is not null)
            {
                extractedTexcoords.Add(sourceTexcoords![oldIndex]);
            }
        }

        extractedIndices.Capacity = selectedTriangles.Count * 3;
        for (int i = 0; i < selectedTriangles.Count; i++)
        {
            int tri = selectedTriangles[i];
            int baseIndex = tri * 3;
            int old0 = (int)sourceIndices[baseIndex + 0];
            int old1 = (int)sourceIndices[baseIndex + 1];
            int old2 = (int)sourceIndices[baseIndex + 2];
            if (!oldToNew.TryGetValue(old0, out uint i0) ||
                !oldToNew.TryGetValue(old1, out uint i1) ||
                !oldToNew.TryGetValue(old2, out uint i2) ||
                i0 == i1 || i1 == i2 || i2 == i0)
            {
                continue;
            }

            extractedIndices.Add(i0);
            extractedIndices.Add(i1);
            extractedIndices.Add(i2);
        }

        if (extractedPositions.Count == 0 || extractedIndices.Count < 3)
        {
            extractedMesh = new ImportedMeshData();
            return false;
        }

        extractedMesh = new ImportedMeshData
        {
            Positions = extractedPositions,
            Indices = extractedIndices,
            Normals = extractedNormals,
            Texcoords = extractedTexcoords
        };

        Console.WriteLine(
            $"[ImportedMesh] Extracted outer component for collar: components={verticesByRoot.Count}, selectedTriangles={selectedTriangles.Count}/{triangleCount}, selectedVertices={extractedPositions.Count}/{vertexCount}");
        return true;
    }

    private static ImportedMeshData CloneImportedMeshData(ImportedMeshData source)
    {
        return new ImportedMeshData
        {
            Positions = new List<Vector3>(source.Positions),
            Indices = new List<uint>(source.Indices),
            Normals = source.Normals is null ? null : new List<Vector3>(source.Normals),
            Texcoords = source.Texcoords is null ? null : new List<Vector2>(source.Texcoords)
        };
    }

    private static bool IsVertexIndexValid(int index, int vertexCount)
    {
        return index >= 0 && index < vertexCount;
    }
}
