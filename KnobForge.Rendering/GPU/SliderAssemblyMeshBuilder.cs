using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using KnobForge.Core;
using KnobForge.Core.Scene;

namespace KnobForge.Rendering.GPU;

public enum SliderPartKind
{
    Backplate = 0,
    Thumb = 1
}

public readonly record struct SliderAssemblyConfig(
    bool Enabled,
    float BackplateWidth,
    float BackplateHeight,
    float BackplateThickness,
    float ThumbWidth,
    float ThumbHeight,
    float ThumbDepth,
    string BackplateImportedMeshPath,
    long BackplateImportedMeshTicks,
    string ThumbImportedMeshPath,
    long ThumbImportedMeshTicks);

public sealed class SliderPartMesh
{
    public MetalVertex[] Vertices { get; init; } = Array.Empty<MetalVertex>();

    public uint[] Indices { get; init; } = Array.Empty<uint>();

    public float ReferenceRadius { get; init; }
}

public static class SliderAssemblyMeshBuilder
{
    private static readonly string[] SliderRootDirectoryCandidates =
    {
        Path.Combine("models", "slider_models"),
        "slider_models"
    };
    private static readonly string[] SupportedExtensions = { ".glb", ".stl" };
    private static readonly string[] BackplateDirectoryNames = { "backplate_models", "backplates", "backplate" };
    private static readonly string[] ThumbDirectoryNames = { "sliderthumb_models", "thumb_models", "thumbs", "slider_thumbs" };

    public static SliderAssemblyConfig ResolveConfig(KnobProject? project)
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

        string root = ResolveSliderRootDirectory();
        bool sliderRootExists = Directory.Exists(root);
        bool envEnabled = IsFeatureEnabledByEnvironmentVariable();
        bool enabled = ResolveEnabled(project.SliderMode, sliderRootExists, envEnabled);
        if (!enabled)
        {
            return default;
        }

        float knobRadius = MathF.Max(40f, modelNode.Radius);
        float defaultBackplateWidth = knobRadius * 0.62f;
        float defaultBackplateHeight = knobRadius * 2.30f;
        float defaultBackplateThickness = 20f;

        float defaultThumbWidth = knobRadius * 0.36f;
        float defaultThumbHeight = knobRadius * 0.52f;
        float defaultThumbDepth = knobRadius * 0.30f;

        float backplateWidth = ResolveDimensionOverride(project.SliderBackplateWidth, defaultBackplateWidth);
        float backplateHeight = ResolveDimensionOverride(project.SliderBackplateHeight, defaultBackplateHeight);
        float backplateThickness = ResolveDimensionOverride(project.SliderBackplateThickness, defaultBackplateThickness);
        float thumbWidth = ResolveDimensionOverride(project.SliderThumbWidth, defaultThumbWidth);
        float thumbHeight = ResolveDimensionOverride(project.SliderThumbHeight, defaultThumbHeight);
        float thumbDepth = ResolveDimensionOverride(project.SliderThumbDepth, defaultThumbDepth);

        string backplateImportedPath = ResolveImportedMeshPath(
            project.SliderBackplateImportedMeshPath,
            root,
            SliderPartKind.Backplate);
        string thumbImportedPath = ResolveImportedMeshPath(
            project.SliderThumbImportedMeshPath,
            root,
            SliderPartKind.Thumb);
        long backplateTicks = ResolveFileTicks(backplateImportedPath);
        long thumbTicks = ResolveFileTicks(thumbImportedPath);

        return new SliderAssemblyConfig(
            Enabled: true,
            BackplateWidth: backplateWidth,
            BackplateHeight: backplateHeight,
            BackplateThickness: backplateThickness,
            ThumbWidth: thumbWidth,
            ThumbHeight: thumbHeight,
            ThumbDepth: thumbDepth,
            BackplateImportedMeshPath: backplateImportedPath,
            BackplateImportedMeshTicks: backplateTicks,
            ThumbImportedMeshPath: thumbImportedPath,
            ThumbImportedMeshTicks: thumbTicks);
    }

    public static SliderPartMesh BuildBackplateMesh(in SliderAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new SliderPartMesh();
        }

        SliderPartMesh? imported = TryBuildImportedPart(
            config.BackplateImportedMeshPath,
            config.BackplateWidth,
            config.BackplateHeight,
            config.BackplateThickness,
            SliderPartKind.Backplate);
        if (imported is not null)
        {
            return imported;
        }

        return BuildPrimitiveCuboid(
            config.BackplateWidth,
            config.BackplateHeight,
            config.BackplateThickness,
            new Vector3(0f, 0f, -config.ThumbDepth * 0.90f));
    }

    public static SliderPartMesh BuildThumbMesh(in SliderAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new SliderPartMesh();
        }

        SliderPartMesh? imported = TryBuildImportedPart(
            config.ThumbImportedMeshPath,
            config.ThumbWidth,
            config.ThumbHeight,
            config.ThumbDepth,
            SliderPartKind.Thumb);
        if (imported is not null)
        {
            return imported;
        }

        return BuildPrimitiveCuboid(
            config.ThumbWidth,
            config.ThumbHeight,
            config.ThumbDepth,
            new Vector3(0f, 0f, config.ThumbDepth * 0.25f));
    }

    private static SliderPartMesh? TryBuildImportedPart(
        string importedPath,
        float targetWidth,
        float targetHeight,
        float targetDepth,
        SliderPartKind partKind)
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
                out float referenceRadius))
        {
            return null;
        }

        Vector3 offset = partKind == SliderPartKind.Backplate
            ? new Vector3(0f, 0f, -targetDepth * 0.90f)
            : new Vector3(0f, 0f, targetDepth * 0.25f);
        MetalVertex[] transformedVertices = vertices;
        if (offset.LengthSquared() > 1e-6f)
        {
            transformedVertices = new MetalVertex[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                transformedVertices[i] = new MetalVertex
                {
                    Position = vertices[i].Position + offset,
                    Normal = vertices[i].Normal,
                    Tangent = vertices[i].Tangent
                };
            }
        }

        float adjustedReference = MathF.Max(referenceRadius, offset.Length() + referenceRadius);
        return new SliderPartMesh
        {
            Vertices = transformedVertices,
            Indices = indices,
            ReferenceRadius = adjustedReference
        };
    }

    private static SliderPartMesh BuildPrimitiveCuboid(
        float width,
        float height,
        float depth,
        Vector3 center)
    {
        float hx = MathF.Max(0.5f, width * 0.5f);
        float hy = MathF.Max(0.5f, height * 0.5f);
        float hz = MathF.Max(0.5f, depth * 0.5f);

        var corners = new Vector3[]
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

        var vertices = new List<MetalVertex>(24);
        var indices = new List<uint>(36);

        AddFace(vertices, indices, corners[0], corners[1], corners[2], corners[3], new Vector3(0f, 0f, -1f), new Vector3(1f, 0f, 0f)); // back
        AddFace(vertices, indices, corners[5], corners[4], corners[7], corners[6], new Vector3(0f, 0f, 1f), new Vector3(-1f, 0f, 0f)); // front
        AddFace(vertices, indices, corners[4], corners[0], corners[3], corners[7], new Vector3(-1f, 0f, 0f), new Vector3(0f, 1f, 0f)); // left
        AddFace(vertices, indices, corners[1], corners[5], corners[6], corners[2], new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f)); // right
        AddFace(vertices, indices, corners[3], corners[2], corners[6], corners[7], new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f)); // top
        AddFace(vertices, indices, corners[4], corners[5], corners[1], corners[0], new Vector3(0f, -1f, 0f), new Vector3(1f, 0f, 0f)); // bottom

        float referenceRadius = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            referenceRadius = MathF.Max(referenceRadius, vertices[i].Position.Length());
        }

        return new SliderPartMesh
        {
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray(),
            ReferenceRadius = referenceRadius
        };
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
        Vector3 tangent = Vector3.Normalize(tangentDirection);
        Vector4 packedTangent = new(tangent, 1f);
        vertices.Add(new MetalVertex { Position = p0, Normal = normal, Tangent = packedTangent });
        vertices.Add(new MetalVertex { Position = p1, Normal = normal, Tangent = packedTangent });
        vertices.Add(new MetalVertex { Position = p2, Normal = normal, Tangent = packedTangent });
        vertices.Add(new MetalVertex { Position = p3, Normal = normal, Tangent = packedTangent });

        indices.Add(start + 0);
        indices.Add(start + 1);
        indices.Add(start + 2);
        indices.Add(start + 0);
        indices.Add(start + 2);
        indices.Add(start + 3);
    }

    private static string ResolveSliderRootDirectory()
    {
        string desktopRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "KnobForge");
        for (int i = 0; i < SliderRootDirectoryCandidates.Length; i++)
        {
            string candidate = Path.Combine(desktopRoot, SliderRootDirectoryCandidates[i]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(desktopRoot, SliderRootDirectoryCandidates[0]);
    }

    private static bool ResolveEnabled(SliderAssemblyMode mode, bool sliderRootExists, bool envEnabled)
    {
        return mode switch
        {
            SliderAssemblyMode.Enabled => true,
            SliderAssemblyMode.Disabled => false,
            _ => sliderRootExists || envEnabled
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

    private static string ResolveImportedMeshPath(
        string configuredPath,
        string sliderRootDirectory,
        SliderPartKind partKind)
    {
        string? explicitPath = TryResolveExplicitMeshPath(configuredPath, sliderRootDirectory);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        return ResolveLibraryImportedMeshPath(sliderRootDirectory, partKind);
    }

    private static string? TryResolveExplicitMeshPath(string configuredPath, string sliderRootDirectory)
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

        if (string.IsNullOrWhiteSpace(sliderRootDirectory))
        {
            return null;
        }

        string combined = Path.GetFullPath(Path.Combine(sliderRootDirectory, trimmed));
        return File.Exists(combined) ? combined : null;
    }

    private static string ResolveLibraryImportedMeshPath(string sliderRootDirectory, SliderPartKind partKind)
    {
        if (string.IsNullOrWhiteSpace(sliderRootDirectory) || !Directory.Exists(sliderRootDirectory))
        {
            return string.Empty;
        }

        IEnumerable<string> directoryCandidates = EnumeratePartDirectories(sliderRootDirectory, partKind);
        foreach (string directory in directoryCandidates)
        {
            string? model = TryGetFirstSupportedModel(directory);
            if (!string.IsNullOrWhiteSpace(model))
            {
                return model;
            }
        }

        string? rootFallback = TryGetFirstSupportedModel(sliderRootDirectory, partKind == SliderPartKind.Backplate ? "backplate" : "thumb");
        return rootFallback ?? string.Empty;
    }

    private static IEnumerable<string> EnumeratePartDirectories(string sliderRootDirectory, SliderPartKind partKind)
    {
        string[] names = partKind == SliderPartKind.Backplate
            ? BackplateDirectoryNames
            : ThumbDirectoryNames;
        for (int i = 0; i < names.Length; i++)
        {
            string candidate = Path.Combine(sliderRootDirectory, names[i]);
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
        string? value = Environment.GetEnvironmentVariable("KNOBFORGE_ENABLE_SLIDER_PARTS");
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
