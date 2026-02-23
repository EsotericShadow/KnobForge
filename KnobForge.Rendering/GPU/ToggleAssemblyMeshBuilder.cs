using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using KnobForge.Core;
using KnobForge.Core.Scene;

namespace KnobForge.Rendering.GPU;

public enum TogglePartKind
{
    Base = 0,
    Lever = 1
}

public readonly record struct ToggleAssemblyConfig(
    bool Enabled,
    int StateCount,
    int StateIndex,
    float LeverAngleDeg,
    float PlateWidth,
    float PlateHeight,
    float PlateThickness,
    float BushingRadius,
    float BushingHeight,
    float LeverLength,
    float LeverRadius,
    float TipRadius,
    string BaseImportedMeshPath,
    long BaseImportedMeshTicks,
    string LeverImportedMeshPath,
    long LeverImportedMeshTicks);

public sealed class TogglePartMesh
{
    public MetalVertex[] Vertices { get; init; } = Array.Empty<MetalVertex>();

    public uint[] Indices { get; init; } = Array.Empty<uint>();

    public float ReferenceRadius { get; init; }
}

public static class ToggleAssemblyMeshBuilder
{
    private static readonly string[] ToggleRootDirectoryCandidates =
    {
        Path.Combine("models", "switch_models"),
        Path.Combine("models", "toggle_models"),
        "switch_models",
        "toggle_models"
    };
    private static readonly string[] SupportedExtensions = { ".glb", ".stl" };
    private static readonly string[] BaseDirectoryNames = { "base_models", "bases", "base" };
    private static readonly string[] LeverDirectoryNames = { "lever_models", "levers", "lever" };

    public static ToggleAssemblyConfig ResolveConfig(KnobProject? project)
    {
        if (project is null)
        {
            return default;
        }

        ModelNode? modelNode = project.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
        if (modelNode is null)
        {
            return default;
        }

        string root = ResolveToggleRootDirectory();
        bool toggleRootExists = Directory.Exists(root);
        bool envEnabled = IsFeatureEnabledByEnvironmentVariable();
        bool enabled = ResolveEnabled(project.ToggleMode, toggleRootExists, envEnabled);
        if (!enabled)
        {
            return default;
        }

        float knobRadius = MathF.Max(40f, modelNode.Radius);
        float plateWidth = ResolveDimensionOverride(project.TogglePlateWidth, knobRadius * 0.88f);
        float plateHeight = ResolveDimensionOverride(project.TogglePlateHeight, knobRadius * 1.08f);
        float plateThickness = ResolveDimensionOverride(project.TogglePlateThickness, 20f);
        float bushingRadius = ResolveDimensionOverride(project.ToggleBushingRadius, knobRadius * 0.16f);
        float bushingHeight = ResolveDimensionOverride(project.ToggleBushingHeight, knobRadius * 0.18f);
        float leverLength = ResolveDimensionOverride(project.ToggleLeverLength, knobRadius * 0.86f);
        float leverRadius = ResolveDimensionOverride(project.ToggleLeverRadius, knobRadius * 0.055f);
        float tipRadius = ResolveDimensionOverride(project.ToggleTipRadius, knobRadius * 0.11f);

        int stateCount = project.ToggleStateCount == ToggleAssemblyStateCount.ThreePosition ? 3 : 2;
        int stateIndex = ClampStateIndex(project.ToggleStateIndex, stateCount);
        float maxAngle = Math.Clamp(project.ToggleMaxAngleDeg, 5f, 85f);
        float leverAngleDeg = ResolveLeverAngleDeg(stateCount, stateIndex, maxAngle);
        string baseImportedPath = ResolveImportedMeshPath(
            project.ToggleBaseImportedMeshPath,
            root,
            TogglePartKind.Base);
        string leverImportedPath = ResolveImportedMeshPath(
            project.ToggleLeverImportedMeshPath,
            root,
            TogglePartKind.Lever);
        long baseTicks = ResolveFileTicks(baseImportedPath);
        long leverTicks = ResolveFileTicks(leverImportedPath);

        return new ToggleAssemblyConfig(
            Enabled: true,
            StateCount: stateCount,
            StateIndex: stateIndex,
            LeverAngleDeg: leverAngleDeg,
            PlateWidth: plateWidth,
            PlateHeight: plateHeight,
            PlateThickness: plateThickness,
            BushingRadius: bushingRadius,
            BushingHeight: bushingHeight,
            LeverLength: leverLength,
            LeverRadius: leverRadius,
            TipRadius: tipRadius,
            BaseImportedMeshPath: baseImportedPath,
            BaseImportedMeshTicks: baseTicks,
            LeverImportedMeshPath: leverImportedPath,
            LeverImportedMeshTicks: leverTicks);
    }

    public static TogglePartMesh BuildBaseMesh(in ToggleAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new TogglePartMesh();
        }

        Vector3 plateCenter = ResolvePlateCenter(config);
        TogglePartMesh? imported = TryBuildImportedPart(
            config.BaseImportedMeshPath,
            config.PlateWidth,
            config.PlateHeight,
            config.PlateThickness,
            plateCenter,
            null);
        if (imported is not null)
        {
            return imported;
        }

        var vertices = new List<MetalVertex>(420);
        var indices = new List<uint>(900);
        AddBox(vertices, indices, config.PlateWidth, config.PlateHeight, config.PlateThickness, plateCenter);

        Vector3 bushingCenter = plateCenter + new Vector3(
            0f,
            0f,
            (config.PlateThickness * 0.5f) + (config.BushingHeight * 0.5f));
        AddPrism(vertices, indices, bushingCenter, config.BushingRadius, config.BushingHeight, 6);

        return BuildPartMesh(vertices, indices);
    }

    public static TogglePartMesh BuildLeverMesh(in ToggleAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new TogglePartMesh();
        }

        var vertices = new List<MetalVertex>(960);
        var indices = new List<uint>(2400);
        Vector3 plateCenter = ResolvePlateCenter(config);
        Vector3 pivot = plateCenter + new Vector3(
            0f,
            0f,
            (config.PlateThickness * 0.5f) + (config.BushingHeight * 0.95f));

        float angleRadians = MathF.PI * config.LeverAngleDeg / 180f;
        Matrix4x4 leverRotation = Matrix4x4.CreateRotationX(-angleRadians);
        Vector3 direction = Vector3.TransformNormal(Vector3.UnitZ, leverRotation);
        direction = SafeNormalize(direction, Vector3.UnitZ);
        Vector3 importedCenter = pivot + (direction * (config.LeverLength * 0.5f));
        float leverDiameter = MathF.Max(1f, config.LeverRadius * 2f);
        float tipDiameter = MathF.Max(1f, config.TipRadius * 2f);
        float targetLeverWidth = MathF.Max(leverDiameter, tipDiameter);
        float targetLeverHeight = MathF.Max(leverDiameter, tipDiameter);
        float targetLeverDepth = MathF.Max(targetLeverWidth, config.LeverLength + tipDiameter);
        TogglePartMesh? imported = TryBuildImportedPart(
            config.LeverImportedMeshPath,
            targetLeverWidth,
            targetLeverHeight,
            targetLeverDepth,
            importedCenter,
            leverRotation);
        if (imported is not null)
        {
            return imported;
        }

        Vector3 endpoint = pivot + (direction * config.LeverLength);

        AddCylinder(vertices, indices, pivot, endpoint, config.LeverRadius, 20);
        AddSphere(vertices, indices, endpoint, config.TipRadius, 10, 16);

        return BuildPartMesh(vertices, indices);
    }

    private static TogglePartMesh BuildPartMesh(List<MetalVertex> vertices, List<uint> indices)
    {
        float referenceRadius = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            referenceRadius = MathF.Max(referenceRadius, vertices[i].Position.Length());
        }

        return new TogglePartMesh
        {
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray(),
            ReferenceRadius = referenceRadius
        };
    }

    private static Vector3 ResolvePlateCenter(in ToggleAssemblyConfig config)
    {
        return new Vector3(
            0f,
            -config.PlateHeight * 0.60f,
            -(config.PlateThickness * 0.5f) - 8f);
    }

    private static TogglePartMesh? TryBuildImportedPart(
        string importedPath,
        float targetWidth,
        float targetHeight,
        float targetDepth,
        Vector3 translation,
        Matrix4x4? rotation)
    {
        if (string.IsNullOrWhiteSpace(importedPath))
        {
            return null;
        }

        if (!ImportedStlCollarMeshBuilder.TryBuildStaticMeshFromPath(
                importedPath,
                targetWidth,
                targetHeight,
                targetDepth,
                out MetalVertex[] vertices,
                out uint[] indices,
                out _))
        {
            return null;
        }

        Matrix4x4 rotationMatrix = rotation ?? Matrix4x4.Identity;
        bool hasRotation = rotation.HasValue;
        var transformedVertices = new MetalVertex[vertices.Length];
        float referenceRadius = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 position = vertices[i].Position;
            Vector3 normal = vertices[i].Normal;
            Vector4 tangent = vertices[i].Tangent;

            if (hasRotation)
            {
                position = Vector3.Transform(position, rotationMatrix);
                normal = SafeNormalize(Vector3.TransformNormal(normal, rotationMatrix), normal);
                Vector3 tangentDirection = new(tangent.X, tangent.Y, tangent.Z);
                tangentDirection = SafeNormalize(Vector3.TransformNormal(tangentDirection, rotationMatrix), tangentDirection);
                tangent = new Vector4(tangentDirection, tangent.W);
            }

            position += translation;
            transformedVertices[i] = new MetalVertex
            {
                Position = position,
                Normal = normal,
                Tangent = tangent
            };
            referenceRadius = MathF.Max(referenceRadius, position.Length());
        }

        return new TogglePartMesh
        {
            Vertices = transformedVertices,
            Indices = indices,
            ReferenceRadius = referenceRadius
        };
    }

    private static void AddBox(
        List<MetalVertex> vertices,
        List<uint> indices,
        float width,
        float height,
        float depth,
        Vector3 center)
    {
        float hx = MathF.Max(0.5f, width * 0.5f);
        float hy = MathF.Max(0.5f, height * 0.5f);
        float hz = MathF.Max(0.5f, depth * 0.5f);

        Vector3[] corners =
        {
            center + new Vector3(-hx, -hy, -hz), // 0
            center + new Vector3(hx, -hy, -hz),  // 1
            center + new Vector3(hx, hy, -hz),   // 2
            center + new Vector3(-hx, hy, -hz),  // 3
            center + new Vector3(-hx, -hy, hz),  // 4
            center + new Vector3(hx, -hy, hz),   // 5
            center + new Vector3(hx, hy, hz),    // 6
            center + new Vector3(-hx, hy, hz)    // 7
        };

        AddFace(vertices, indices, corners[0], corners[1], corners[2], corners[3], new Vector3(0f, 0f, -1f), Vector3.UnitX);
        AddFace(vertices, indices, corners[5], corners[4], corners[7], corners[6], new Vector3(0f, 0f, 1f), -Vector3.UnitX);
        AddFace(vertices, indices, corners[4], corners[0], corners[3], corners[7], -Vector3.UnitX, Vector3.UnitY);
        AddFace(vertices, indices, corners[1], corners[5], corners[6], corners[2], Vector3.UnitX, -Vector3.UnitY);
        AddFace(vertices, indices, corners[3], corners[2], corners[6], corners[7], Vector3.UnitY, Vector3.UnitX);
        AddFace(vertices, indices, corners[4], corners[5], corners[1], corners[0], -Vector3.UnitY, Vector3.UnitX);
    }

    private static void AddPrism(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 center,
        float radius,
        float height,
        int sides)
    {
        int sideCount = Math.Clamp(sides, 3, 32);
        float hz = MathF.Max(0.5f, height * 0.5f);
        float r = MathF.Max(0.5f, radius);
        float step = MathF.PI * 2f / sideCount;

        for (int i = 0; i < sideCount; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            Vector3 radial0 = new(MathF.Cos(a0), MathF.Sin(a0), 0f);
            Vector3 radial1 = new(MathF.Cos(a1), MathF.Sin(a1), 0f);
            Vector3 p0 = center + (radial0 * r) + new Vector3(0f, 0f, -hz);
            Vector3 p1 = center + (radial1 * r) + new Vector3(0f, 0f, -hz);
            Vector3 p2 = center + (radial1 * r) + new Vector3(0f, 0f, hz);
            Vector3 p3 = center + (radial0 * r) + new Vector3(0f, 0f, hz);
            Vector3 normal = SafeNormalize(radial0 + radial1, radial0);
            AddFace(vertices, indices, p0, p1, p2, p3, normal, Vector3.UnitZ);
        }

        AddDisc(vertices, indices, center + new Vector3(0f, 0f, hz), r, sideCount, Vector3.UnitZ);
        AddDisc(vertices, indices, center + new Vector3(0f, 0f, -hz), r, sideCount, -Vector3.UnitZ);
    }

    private static void AddCylinder(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 start,
        Vector3 end,
        float radius,
        int sides)
    {
        Vector3 axis = end - start;
        float axisLength = axis.Length();
        if (axisLength <= 1e-5f)
        {
            return;
        }

        Vector3 forward = axis / axisLength;
        Vector3 tangentA = MathF.Abs(forward.Z) < 0.999f
            ? Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ))
            : Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitX));
        Vector3 tangentB = SafeNormalize(Vector3.Cross(forward, tangentA), Vector3.UnitY);
        int sideCount = Math.Clamp(sides, 6, 64);
        float r = MathF.Max(0.25f, radius);
        float step = MathF.PI * 2f / sideCount;

        for (int i = 0; i < sideCount; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            Vector3 radial0 = (MathF.Cos(a0) * tangentA) + (MathF.Sin(a0) * tangentB);
            Vector3 radial1 = (MathF.Cos(a1) * tangentA) + (MathF.Sin(a1) * tangentB);
            Vector3 p0 = start + (radial0 * r);
            Vector3 p1 = start + (radial1 * r);
            Vector3 p2 = end + (radial1 * r);
            Vector3 p3 = end + (radial0 * r);
            Vector3 normal = SafeNormalize(radial0 + radial1, radial0);
            AddFace(vertices, indices, p0, p1, p2, p3, normal, forward);
        }

        AddDisc(vertices, indices, start, r, sideCount, -forward, tangentA, tangentB);
        AddDisc(vertices, indices, end, r, sideCount, forward, tangentA, tangentB);
    }

    private static void AddSphere(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 center,
        float radius,
        int latitudeSegments,
        int longitudeSegments)
    {
        int latSeg = Math.Clamp(latitudeSegments, 4, 64);
        int lonSeg = Math.Clamp(longitudeSegments, 6, 128);
        float r = MathF.Max(0.25f, radius);
        uint startIndex = (uint)vertices.Count;

        for (int lat = 0; lat <= latSeg; lat++)
        {
            float v = lat / (float)latSeg;
            float phi = v * MathF.PI;
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);
            for (int lon = 0; lon <= lonSeg; lon++)
            {
                float u = lon / (float)lonSeg;
                float theta = u * MathF.PI * 2f;
                float sinTheta = MathF.Sin(theta);
                float cosTheta = MathF.Cos(theta);
                Vector3 normal = new(sinPhi * cosTheta, sinPhi * sinTheta, cosPhi);
                Vector3 position = center + (normal * r);
                Vector3 tangent = SafeNormalize(new Vector3(-sinTheta, cosTheta, 0f), Vector3.UnitX);
                vertices.Add(new MetalVertex
                {
                    Position = position,
                    Normal = normal,
                    Tangent = new Vector4(tangent, 1f)
                });
            }
        }

        int stride = lonSeg + 1;
        for (int lat = 0; lat < latSeg; lat++)
        {
            for (int lon = 0; lon < lonSeg; lon++)
            {
                uint i0 = startIndex + (uint)((lat * stride) + lon);
                uint i1 = i0 + 1;
                uint i2 = i0 + (uint)stride;
                uint i3 = i2 + 1;
                indices.Add(i0);
                indices.Add(i2);
                indices.Add(i1);
                indices.Add(i1);
                indices.Add(i2);
                indices.Add(i3);
            }
        }
    }

    private static void AddDisc(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 center,
        float radius,
        int sides,
        Vector3 normal)
    {
        Vector3 tangentA = MathF.Abs(normal.Z) < 0.999f
            ? Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitZ))
            : Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitX));
        Vector3 tangentB = SafeNormalize(Vector3.Cross(normal, tangentA), Vector3.UnitY);
        AddDisc(vertices, indices, center, radius, sides, normal, tangentA, tangentB);
    }

    private static void AddDisc(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 center,
        float radius,
        int sides,
        Vector3 normal,
        Vector3 tangentA,
        Vector3 tangentB)
    {
        int sideCount = Math.Clamp(sides, 3, 64);
        float r = MathF.Max(0.25f, radius);
        float step = MathF.PI * 2f / sideCount;
        uint centerIndex = (uint)vertices.Count;
        vertices.Add(new MetalVertex
        {
            Position = center,
            Normal = normal,
            Tangent = new Vector4(SafeNormalize(tangentA, BuildTangentFromNormal(normal)), 1f)
        });

        uint ringStart = (uint)vertices.Count;
        for (int i = 0; i < sideCount; i++)
        {
            float angle = i * step;
            Vector3 radial = (MathF.Cos(angle) * tangentA) + (MathF.Sin(angle) * tangentB);
            Vector3 position = center + (radial * r);
            vertices.Add(new MetalVertex
            {
                Position = position,
                Normal = normal,
                Tangent = new Vector4(SafeNormalize(radial, BuildTangentFromNormal(normal)), 1f)
            });
        }

        for (int i = 0; i < sideCount; i++)
        {
            uint current = ringStart + (uint)i;
            uint next = ringStart + (uint)((i + 1) % sideCount);
            if (normal.Z >= 0f)
            {
                indices.Add(centerIndex);
                indices.Add(current);
                indices.Add(next);
            }
            else
            {
                indices.Add(centerIndex);
                indices.Add(next);
                indices.Add(current);
            }
        }
    }

    private static void AddFace(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        Vector3 normal,
        Vector3 tangentDirection)
    {
        uint start = (uint)vertices.Count;
        Vector3 tangent = SafeNormalize(tangentDirection, BuildTangentFromNormal(normal));
        Vector4 packedTangent = new(tangent, 1f);
        Vector3 safeNormal = SafeNormalize(normal, Vector3.UnitZ);

        vertices.Add(new MetalVertex { Position = p0, Normal = safeNormal, Tangent = packedTangent });
        vertices.Add(new MetalVertex { Position = p1, Normal = safeNormal, Tangent = packedTangent });
        vertices.Add(new MetalVertex { Position = p2, Normal = safeNormal, Tangent = packedTangent });
        vertices.Add(new MetalVertex { Position = p3, Normal = safeNormal, Tangent = packedTangent });

        indices.Add(start + 0);
        indices.Add(start + 1);
        indices.Add(start + 2);
        indices.Add(start + 0);
        indices.Add(start + 2);
        indices.Add(start + 3);
    }

    private static Vector3 BuildTangentFromNormal(Vector3 normal)
    {
        Vector3 axis = MathF.Abs(normal.Z) < 0.999f ? Vector3.UnitZ : Vector3.UnitY;
        Vector3 tangent = Vector3.Cross(axis, normal);
        if (tangent.LengthSquared() <= 1e-10f)
        {
            tangent = Vector3.Cross(Vector3.UnitX, normal);
        }

        return SafeNormalize(tangent, Vector3.UnitX);
    }

    private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        float lengthSq = value.LengthSquared();
        if (lengthSq <= 1e-10f)
        {
            return fallback;
        }

        return value / MathF.Sqrt(lengthSq);
    }

    private static bool ResolveEnabled(ToggleAssemblyMode mode, bool toggleRootExists, bool envEnabled)
    {
        return mode switch
        {
            ToggleAssemblyMode.Enabled => true,
            ToggleAssemblyMode.Disabled => false,
            _ => toggleRootExists || envEnabled
        };
    }

    private static float ResolveDimensionOverride(float overrideValue, float fallbackValue)
    {
        if (float.IsFinite(overrideValue) && overrideValue > 0f)
        {
            return overrideValue;
        }

        return fallbackValue;
    }

    private static int ClampStateIndex(int stateIndex, int stateCount)
    {
        int max = Math.Max(0, stateCount - 1);
        return Math.Clamp(stateIndex, 0, max);
    }

    private static float ResolveLeverAngleDeg(int stateCount, int stateIndex, float maxAngleDeg)
    {
        if (stateCount <= 2)
        {
            return stateIndex <= 0 ? -maxAngleDeg : maxAngleDeg;
        }

        return stateIndex switch
        {
            0 => -maxAngleDeg,
            2 => maxAngleDeg,
            _ => 0f
        };
    }

    private static string ResolveToggleRootDirectory()
    {
        string desktopRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "KnobForge");
        for (int i = 0; i < ToggleRootDirectoryCandidates.Length; i++)
        {
            string candidate = Path.Combine(desktopRoot, ToggleRootDirectoryCandidates[i]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(desktopRoot, ToggleRootDirectoryCandidates[0]);
    }

    private static string ResolveImportedMeshPath(
        string configuredPath,
        string toggleRootDirectory,
        TogglePartKind partKind)
    {
        string? explicitPath = TryResolveExplicitMeshPath(configuredPath, toggleRootDirectory);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        return ResolveLibraryImportedMeshPath(toggleRootDirectory, partKind);
    }

    private static string? TryResolveExplicitMeshPath(string configuredPath, string toggleRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        string trimmed = configuredPath.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            return File.Exists(trimmed) ? trimmed : null;
        }

        if (string.IsNullOrWhiteSpace(toggleRootDirectory))
        {
            return null;
        }

        string combined = Path.GetFullPath(Path.Combine(toggleRootDirectory, trimmed));
        return File.Exists(combined) ? combined : null;
    }

    private static string ResolveLibraryImportedMeshPath(string toggleRootDirectory, TogglePartKind partKind)
    {
        if (string.IsNullOrWhiteSpace(toggleRootDirectory) || !Directory.Exists(toggleRootDirectory))
        {
            return string.Empty;
        }

        IEnumerable<string> directoryCandidates = EnumeratePartDirectories(toggleRootDirectory, partKind);
        foreach (string directory in directoryCandidates)
        {
            string? model = TryGetFirstSupportedModel(directory);
            if (!string.IsNullOrWhiteSpace(model))
            {
                return model;
            }
        }

        string preferredToken = partKind == TogglePartKind.Base ? "base" : "lever";
        string? rootFallback = TryGetFirstSupportedModel(toggleRootDirectory, preferredToken);
        return rootFallback ?? string.Empty;
    }

    private static IEnumerable<string> EnumeratePartDirectories(string toggleRootDirectory, TogglePartKind partKind)
    {
        string[] names = partKind == TogglePartKind.Base
            ? BaseDirectoryNames
            : LeverDirectoryNames;
        for (int i = 0; i < names.Length; i++)
        {
            string candidate = Path.Combine(toggleRootDirectory, names[i]);
            if (Directory.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static string? TryGetFirstSupportedModel(string directory, string? preferredToken = null)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        IEnumerable<string> files = Directory
            .EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(preferredToken))
        {
            string? preferred = files.FirstOrDefault(path =>
                Path.GetFileNameWithoutExtension(path).Contains(preferredToken, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred;
            }
        }

        return files.FirstOrDefault();
    }

    private static long ResolveFileTicks(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return 0L;
        }

        try
        {
            return File.GetLastWriteTimeUtc(path).Ticks;
        }
        catch
        {
            return 0L;
        }
    }

    private static bool IsFeatureEnabledByEnvironmentVariable()
    {
        string? value = Environment.GetEnvironmentVariable("KNOBFORGE_ENABLE_TOGGLE_SWITCH");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
