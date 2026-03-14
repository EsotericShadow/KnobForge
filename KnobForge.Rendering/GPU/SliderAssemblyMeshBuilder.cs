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
    float ThumbPositionNormalized,
    string BackplateImportedMeshPath,
    long BackplateImportedMeshTicks,
    string ThumbImportedMeshPath,
    long ThumbImportedMeshTicks,
    SliderThumbProfile ThumbProfile,
    SliderTrackStyle TrackStyle,
    float TrackWidth,
    float TrackDepth,
    float RailHeight,
    float RailSpacing,
    int ThumbRidgeCount,
    float ThumbRidgeDepth,
    float ThumbCornerRadius);

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

    public static SliderAssemblyConfig ResolveConfig(KnobProject? project, RenderQualityTier quality = RenderQualityTier.Normal)
    {
        if (project is null || project.ProjectType != InteractorProjectType.ThumbSlider)
        {
            return default;
        }

        ModelNode? modelNode = project.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
        if (modelNode is null)
        {
            return default;
        }

        bool enabled = project.SliderMode != SliderAssemblyMode.Disabled;
        if (!enabled)
        {
            return default;
        }

        string sliderRootDirectory = ResolveSliderRootDirectory();
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
        float thumbPositionNormalized = Math.Clamp(project.SliderThumbPositionNormalized, 0f, 1f);

        float trackWidth = ResolveDimensionOverride(project.SliderTrackWidth, thumbWidth * 0.35f);
        float trackDepth = ResolveDimensionOverride(project.SliderTrackDepth, backplateThickness * 0.15f);
        float railHeight = ResolveDimensionOverride(project.SliderRailHeight, thumbHeight * 0.08f);
        float railSpacing = ResolveDimensionOverride(project.SliderRailSpacing, thumbWidth * 1.1f);
        int thumbRidgeCount = ScaleSegments(project.SliderThumbRidgeCount > 0 ? project.SliderThumbRidgeCount : 5, quality, 3, 16);
        float thumbRidgeDepth = ResolveDimensionOverride(project.SliderThumbRidgeDepth, thumbDepth * 0.06f);
        float thumbCornerRadius = ResolveDimensionOverride(project.SliderThumbCornerRadius, MathF.Min(thumbWidth, thumbHeight) * 0.12f);
        string backplateImportedMeshPath = ResolveImportedMeshPath(project.SliderBackplateImportedMeshPath, sliderRootDirectory);
        string thumbImportedMeshPath = ResolveImportedMeshPath(project.SliderThumbImportedMeshPath, sliderRootDirectory);

        return new SliderAssemblyConfig(
            Enabled: true,
            BackplateWidth: backplateWidth,
            BackplateHeight: backplateHeight,
            BackplateThickness: backplateThickness,
            ThumbWidth: thumbWidth,
            ThumbHeight: thumbHeight,
            ThumbDepth: thumbDepth,
            ThumbPositionNormalized: thumbPositionNormalized,
            BackplateImportedMeshPath: backplateImportedMeshPath,
            BackplateImportedMeshTicks: ResolveFileTicks(backplateImportedMeshPath),
            ThumbImportedMeshPath: thumbImportedMeshPath,
            ThumbImportedMeshTicks: ResolveFileTicks(thumbImportedMeshPath),
            ThumbProfile: project.SliderThumbProfile,
            TrackStyle: project.SliderTrackStyle,
            TrackWidth: trackWidth,
            TrackDepth: trackDepth,
            RailHeight: railHeight,
            RailSpacing: railSpacing,
            ThumbRidgeCount: thumbRidgeCount,
            ThumbRidgeDepth: thumbRidgeDepth,
            ThumbCornerRadius: thumbCornerRadius);
    }

    public static SliderPartMesh BuildBackplateMesh(in SliderAssemblyConfig config, RenderQualityTier quality = RenderQualityTier.Normal)
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

        return config.TrackStyle switch
        {
            SliderTrackStyle.Channel or SliderTrackStyle.VGroove => BuildBackplateWithTrack(config),
            SliderTrackStyle.Rail => BuildBackplateWithRails(config),
            _ => BuildPrimitiveCuboid(
                config.BackplateWidth,
                config.BackplateHeight,
                config.BackplateThickness,
                ResolveBackplateCenter(config))
        };
    }

    public static SliderPartMesh BuildThumbMesh(in SliderAssemblyConfig config, RenderQualityTier quality = RenderQualityTier.Normal)
    {
        if (!config.Enabled)
        {
            return new SliderPartMesh();
        }

        Vector3 thumbCenter = ResolveThumbCenter(config);
        SliderPartMesh? imported = TryBuildImportedPart(
            config.ThumbImportedMeshPath,
            config.ThumbWidth,
            config.ThumbHeight,
            config.ThumbDepth,
            SliderPartKind.Thumb,
            thumbCenter);
        if (imported is not null)
        {
            return imported;
        }

        return config.ThumbProfile switch
        {
            SliderThumbProfile.Rounded => BuildRoundedThumb(config, thumbCenter, quality),
            SliderThumbProfile.Ridged => BuildRidgedThumb(config, thumbCenter),
            SliderThumbProfile.Pointer => BuildPointerThumb(config, thumbCenter),
            SliderThumbProfile.BarHandle => BuildBarHandleThumb(config, thumbCenter),
            _ => BuildPrimitiveCuboid(config.ThumbWidth, config.ThumbHeight, config.ThumbDepth, thumbCenter)
        };
    }

    private static SliderPartMesh BuildBackplateWithRails(in SliderAssemblyConfig config)
    {
        SliderPartMesh baseMesh = BuildPrimitiveCuboid(
            config.BackplateWidth,
            config.BackplateHeight,
            config.BackplateThickness,
            ResolveBackplateCenter(config));
        var vertices = baseMesh.Vertices.ToList();
        var indices = baseMesh.Indices.ToList();

        Vector3 center = ResolveBackplateCenter(config);
        float topZ = center.Z + (config.BackplateThickness * 0.5f);
        float railWidth = MathF.Max(0.6f, config.TrackWidth * 0.25f);
        float railX = MathF.Max(railWidth * 0.5f, config.RailSpacing * 0.5f);
        float railDepth = MathF.Max(0.25f, config.RailHeight);

        AddBox(vertices, indices, railWidth, config.BackplateHeight, railDepth, new Vector3(-railX, center.Y, topZ + (railDepth * 0.5f)));
        AddBox(vertices, indices, railWidth, config.BackplateHeight, railDepth, new Vector3(railX, center.Y, topZ + (railDepth * 0.5f)));

        return BuildPartMesh(vertices, indices);
    }

    private static SliderPartMesh BuildBackplateWithTrack(in SliderAssemblyConfig config)
    {
        Vector3 center = ResolveBackplateCenter(config);
        float hx = MathF.Max(0.5f, config.BackplateWidth * 0.5f);
        float hy = MathF.Max(0.5f, config.BackplateHeight * 0.5f);
        float hz = MathF.Max(0.5f, config.BackplateThickness * 0.5f);
        float topZ = center.Z + hz;
        float bottomZ = center.Z - hz;
        float trackHalf = Math.Clamp(config.TrackWidth * 0.5f, 0.5f, hx - 0.5f);
        float grooveDepth = Math.Clamp(config.TrackDepth, 0.25f, (hz * 1.4f));
        float grooveZ = topZ - grooveDepth;

        var vertices = new List<MetalVertex>(64);
        var indices = new List<uint>(128);

        Vector3 lbf = new(center.X - hx, center.Y - hy, bottomZ);
        Vector3 rbf = new(center.X + hx, center.Y - hy, bottomZ);
        Vector3 rtf = new(center.X + hx, center.Y + hy, bottomZ);
        Vector3 ltf = new(center.X - hx, center.Y + hy, bottomZ);
        Vector3 lbk = new(center.X - hx, center.Y - hy, topZ);
        Vector3 rbk = new(center.X + hx, center.Y - hy, topZ);
        Vector3 rtk = new(center.X + hx, center.Y + hy, topZ);
        Vector3 ltk = new(center.X - hx, center.Y + hy, topZ);

        AddFace(vertices, indices, lbf, rbf, rtf, ltf, -Vector3.UnitZ, Vector3.UnitX); // bottom
        AddFace(vertices, indices, lbk, lbf, ltf, ltk, -Vector3.UnitX, Vector3.UnitY); // left
        AddFace(vertices, indices, rbf, rbk, rtk, rtf, Vector3.UnitX, -Vector3.UnitY); // right
        AddFace(vertices, indices, ltf, rtf, rtk, ltk, Vector3.UnitY, Vector3.UnitX); // top end
        AddFace(vertices, indices, rbk, rbf, lbf, lbk, -Vector3.UnitY, Vector3.UnitX); // bottom end

        AddFace(
            vertices,
            indices,
            new Vector3(center.X - hx, center.Y - hy, topZ),
            new Vector3(center.X - trackHalf, center.Y - hy, topZ),
            new Vector3(center.X - trackHalf, center.Y + hy, topZ),
            new Vector3(center.X - hx, center.Y + hy, topZ),
            Vector3.UnitZ,
            Vector3.UnitY);
        AddFace(
            vertices,
            indices,
            new Vector3(center.X + trackHalf, center.Y - hy, topZ),
            new Vector3(center.X + hx, center.Y - hy, topZ),
            new Vector3(center.X + hx, center.Y + hy, topZ),
            new Vector3(center.X + trackHalf, center.Y + hy, topZ),
            Vector3.UnitZ,
            Vector3.UnitY);

        if (config.TrackStyle == SliderTrackStyle.Channel)
        {
            AddFace(
                vertices,
                indices,
                new Vector3(center.X - trackHalf, center.Y - hy, grooveZ),
                new Vector3(center.X + trackHalf, center.Y - hy, grooveZ),
                new Vector3(center.X + trackHalf, center.Y + hy, grooveZ),
                new Vector3(center.X - trackHalf, center.Y + hy, grooveZ),
                Vector3.UnitZ,
                Vector3.UnitY);
            AddFace(
                vertices,
                indices,
                new Vector3(center.X - trackHalf, center.Y - hy, topZ),
                new Vector3(center.X - trackHalf, center.Y + hy, topZ),
                new Vector3(center.X - trackHalf, center.Y + hy, grooveZ),
                new Vector3(center.X - trackHalf, center.Y - hy, grooveZ),
                -Vector3.UnitX,
                Vector3.UnitY);
            AddFace(
                vertices,
                indices,
                new Vector3(center.X + trackHalf, center.Y - hy, grooveZ),
                new Vector3(center.X + trackHalf, center.Y + hy, grooveZ),
                new Vector3(center.X + trackHalf, center.Y + hy, topZ),
                new Vector3(center.X + trackHalf, center.Y - hy, topZ),
                Vector3.UnitX,
                Vector3.UnitY);
            AddFace(
                vertices,
                indices,
                new Vector3(center.X - trackHalf, center.Y - hy, topZ),
                new Vector3(center.X + trackHalf, center.Y - hy, topZ),
                new Vector3(center.X + trackHalf, center.Y - hy, grooveZ),
                new Vector3(center.X - trackHalf, center.Y - hy, grooveZ),
                -Vector3.UnitY,
                Vector3.UnitX);
            AddFace(
                vertices,
                indices,
                new Vector3(center.X - trackHalf, center.Y + hy, grooveZ),
                new Vector3(center.X + trackHalf, center.Y + hy, grooveZ),
                new Vector3(center.X + trackHalf, center.Y + hy, topZ),
                new Vector3(center.X - trackHalf, center.Y + hy, topZ),
                Vector3.UnitY,
                Vector3.UnitX);
        }
        else
        {
            AddFace(
                vertices,
                indices,
                new Vector3(center.X - trackHalf, center.Y - hy, topZ),
                new Vector3(center.X, center.Y - hy, grooveZ),
                new Vector3(center.X, center.Y + hy, grooveZ),
                new Vector3(center.X - trackHalf, center.Y + hy, topZ),
                SafeNormalize(new Vector3(-grooveDepth, 0f, trackHalf), new Vector3(-1f, 0f, 1f)),
                Vector3.UnitY);
            AddFace(
                vertices,
                indices,
                new Vector3(center.X, center.Y - hy, grooveZ),
                new Vector3(center.X + trackHalf, center.Y - hy, topZ),
                new Vector3(center.X + trackHalf, center.Y + hy, topZ),
                new Vector3(center.X, center.Y + hy, grooveZ),
                SafeNormalize(new Vector3(grooveDepth, 0f, trackHalf), new Vector3(1f, 0f, 1f)),
                Vector3.UnitY);

            AddFace(
                vertices,
                indices,
                new Vector3(center.X - trackHalf, center.Y - hy, topZ),
                new Vector3(center.X, center.Y - hy, grooveZ),
                new Vector3(center.X, center.Y - hy, grooveZ),
                new Vector3(center.X + trackHalf, center.Y - hy, topZ),
                -Vector3.UnitY,
                Vector3.UnitX);
            AddFace(
                vertices,
                indices,
                new Vector3(center.X - trackHalf, center.Y + hy, topZ),
                new Vector3(center.X + trackHalf, center.Y + hy, topZ),
                new Vector3(center.X, center.Y + hy, grooveZ),
                new Vector3(center.X, center.Y + hy, grooveZ),
                Vector3.UnitY,
                Vector3.UnitX);
        }

        return BuildPartMesh(vertices, indices);
    }

    private static SliderPartMesh BuildRoundedThumb(in SliderAssemblyConfig config, Vector3 center, RenderQualityTier quality)
    {
        float radius = Math.Clamp(config.ThumbCornerRadius, 0.5f, MathF.Min(config.ThumbWidth, config.ThumbHeight) * 0.25f);
        SliderPartMesh baseMesh = BuildPrimitiveCuboid(
            config.ThumbWidth,
            MathF.Max(0.5f, config.ThumbHeight - (radius * 0.35f)),
            MathF.Max(0.5f, config.ThumbDepth - radius),
            new Vector3(center.X, center.Y - (radius * 0.12f), center.Z - (radius * 0.18f)));
        var vertices = baseMesh.Vertices.ToList();
        var indices = baseMesh.Indices.ToList();

        Vector3 tubeStart = new(center.X - (config.ThumbWidth * 0.5f) + radius, center.Y + (config.ThumbHeight * 0.5f) - radius, center.Z + (config.ThumbDepth * 0.5f) - radius);
        Vector3 tubeEnd = new(center.X + (config.ThumbWidth * 0.5f) - radius, center.Y + (config.ThumbHeight * 0.5f) - radius, center.Z + (config.ThumbDepth * 0.5f) - radius);
        AddCylinderFrustum(vertices, indices, tubeStart, tubeEnd, radius, radius, ScaleSegments(14, quality, 6, 24), capStart: true, capEnd: true);

        return BuildPartMesh(vertices, indices);
    }

    private static int ScaleSegments(int baseCount, RenderQualityTier quality, int minimum, int maximum)
    {
        float scale = quality switch
        {
            RenderQualityTier.Draft => 0.5f,
            RenderQualityTier.Production => 1.5f,
            _ => 1f
        };

        return Math.Clamp((int)MathF.Round(baseCount * scale), minimum, maximum);
    }

    private static SliderPartMesh BuildRidgedThumb(in SliderAssemblyConfig config, Vector3 center)
    {
        SliderPartMesh baseMesh = BuildPrimitiveCuboid(config.ThumbWidth, config.ThumbHeight, config.ThumbDepth, center);
        var vertices = baseMesh.Vertices.ToList();
        var indices = baseMesh.Indices.ToList();

        int ridgeCount = Math.Max(1, config.ThumbRidgeCount);
        float ridgeBand = config.ThumbHeight / ((ridgeCount * 2f) + 1f);
        float ridgeDepth = MathF.Max(0.2f, config.ThumbRidgeDepth);
        float startY = center.Y - (config.ThumbHeight * 0.5f) + ridgeBand;
        float frontZ = center.Z + (config.ThumbDepth * 0.5f) + (ridgeDepth * 0.5f);

        for (int i = 0; i < ridgeCount; i++)
        {
            float y = startY + (i * ridgeBand * 2f);
            AddBox(
                vertices,
                indices,
                config.ThumbWidth * 0.92f,
                ridgeBand * 0.45f,
                ridgeDepth,
                new Vector3(center.X, y, frontZ));
        }

        return BuildPartMesh(vertices, indices);
    }

    private static SliderPartMesh BuildPointerThumb(in SliderAssemblyConfig config, Vector3 center)
    {
        float hx = MathF.Max(0.5f, config.ThumbWidth * 0.5f);
        float hy = MathF.Max(0.5f, config.ThumbHeight * 0.5f);
        float hz = MathF.Max(0.5f, config.ThumbDepth * 0.5f);
        float tipDepth = hz + (config.ThumbDepth * 0.3f);

        Vector3 b0 = center + new Vector3(-hx, -hy, -hz);
        Vector3 b1 = center + new Vector3(hx, -hy, -hz);
        Vector3 b2 = center + new Vector3(hx, hy, -hz);
        Vector3 b3 = center + new Vector3(-hx, hy, -hz);
        Vector3 f0 = center + new Vector3(-hx, -hy, hz * 0.3f);
        Vector3 f1 = center + new Vector3(hx, -hy, hz * 0.3f);
        Vector3 f2 = center + new Vector3(hx, hy, hz * 0.3f);
        Vector3 f3 = center + new Vector3(-hx, hy, hz * 0.3f);
        Vector3 tipBottom = center + new Vector3(0f, -hy, tipDepth);
        Vector3 tipTop = center + new Vector3(0f, hy, tipDepth);

        var vertices = new List<MetalVertex>(40);
        var indices = new List<uint>(80);

        AddFace(vertices, indices, b0, b1, b2, b3, -Vector3.UnitZ, Vector3.UnitX);
        AddFace(vertices, indices, b0, f0, f3, b3, -Vector3.UnitX, Vector3.UnitY);
        AddFace(vertices, indices, f1, b1, b2, f2, Vector3.UnitX, Vector3.UnitY);
        AddFace(vertices, indices, b3, b2, f2, f3, Vector3.UnitY, Vector3.UnitX);
        AddFace(vertices, indices, f0, f1, b1, b0, -Vector3.UnitY, Vector3.UnitX);
        AddFace(vertices, indices, f0, tipBottom, tipTop, f3, SafeNormalize(new Vector3(-1f, 0f, 1.2f), new Vector3(-1f, 0f, 1f)), Vector3.UnitY);
        AddFace(vertices, indices, tipBottom, f1, f2, tipTop, SafeNormalize(new Vector3(1f, 0f, 1.2f), new Vector3(1f, 0f, 1f)), Vector3.UnitY);
        AddFace(vertices, indices, f0, f1, tipBottom, tipBottom, -Vector3.UnitY, Vector3.UnitX);
        AddFace(vertices, indices, f3, tipTop, tipTop, f2, Vector3.UnitY, Vector3.UnitX);

        return BuildPartMesh(vertices, indices);
    }

    private static SliderPartMesh BuildBarHandleThumb(in SliderAssemblyConfig config, Vector3 center)
    {
        var vertices = new List<MetalVertex>(96);
        var indices = new List<uint>(192);
        float shaftWidth = MathF.Max(0.5f, config.ThumbWidth * 0.6f);
        float shaftHeight = MathF.Max(0.5f, config.ThumbHeight * 0.68f);
        float gripHeight = MathF.Max(0.5f, config.ThumbHeight - shaftHeight);
        float shaftDepth = MathF.Max(0.5f, config.ThumbDepth * 0.72f);

        AddBox(
            vertices,
            indices,
            shaftWidth,
            shaftHeight,
            shaftDepth,
            new Vector3(center.X, center.Y - (gripHeight * 0.35f), center.Z - (config.ThumbDepth * 0.08f)));
        AddBox(
            vertices,
            indices,
            config.ThumbWidth,
            gripHeight,
            config.ThumbDepth,
            new Vector3(center.X, center.Y + (shaftHeight * 0.5f) - (gripHeight * 0.5f), center.Z));

        return BuildPartMesh(vertices, indices);
    }

    private static SliderPartMesh? TryBuildImportedPart(
        string importedPath,
        float targetWidth,
        float targetHeight,
        float targetDepth,
        SliderPartKind partKind,
        Vector3? centerOverride = null)
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

        Vector3 offset = centerOverride ?? (partKind == SliderPartKind.Backplate
            ? new Vector3(0f, 0f, -targetDepth * 0.90f)
            : new Vector3(0f, 0f, targetDepth * 0.25f));
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
                    Tangent = vertices[i].Tangent,
                    Texcoord = vertices[i].Texcoord
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

    private static SliderPartMesh BuildPrimitiveCuboid(float width, float height, float depth, Vector3 center)
    {
        var vertices = new List<MetalVertex>(24);
        var indices = new List<uint>(36);
        AddBox(vertices, indices, width, height, depth, center);
        return BuildPartMesh(vertices, indices);
    }

    private static SliderPartMesh BuildPartMesh(List<MetalVertex> vertices, List<uint> indices)
    {
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

    private static void AddBox(List<MetalVertex> vertices, List<uint> indices, float width, float height, float depth, Vector3 center)
    {
        float hx = MathF.Max(0.5f, width * 0.5f);
        float hy = MathF.Max(0.5f, height * 0.5f);
        float hz = MathF.Max(0.5f, depth * 0.5f);

        Vector3[] corners =
        {
            center + new Vector3(-hx, -hy, -hz),
            center + new Vector3(hx, -hy, -hz),
            center + new Vector3(hx, hy, -hz),
            center + new Vector3(-hx, hy, -hz),
            center + new Vector3(-hx, -hy, hz),
            center + new Vector3(hx, -hy, hz),
            center + new Vector3(hx, hy, hz),
            center + new Vector3(-hx, hy, hz)
        };

        AddFace(vertices, indices, corners[0], corners[1], corners[2], corners[3], new Vector3(0f, 0f, -1f), new Vector3(1f, 0f, 0f));
        AddFace(vertices, indices, corners[5], corners[4], corners[7], corners[6], new Vector3(0f, 0f, 1f), new Vector3(-1f, 0f, 0f));
        AddFace(vertices, indices, corners[4], corners[0], corners[3], corners[7], new Vector3(-1f, 0f, 0f), new Vector3(0f, 1f, 0f));
        AddFace(vertices, indices, corners[1], corners[5], corners[6], corners[2], new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f));
        AddFace(vertices, indices, corners[3], corners[2], corners[6], corners[7], new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f));
        AddFace(vertices, indices, corners[4], corners[5], corners[1], corners[0], new Vector3(0f, -1f, 0f), new Vector3(1f, 0f, 0f));
    }

    private static void AddCylinderFrustum(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 start,
        Vector3 end,
        float startRadius,
        float endRadius,
        int segments,
        bool capStart,
        bool capEnd)
    {
        Vector3 axis = end - start;
        float axisLength = axis.Length();
        if (axisLength <= 1e-5f)
        {
            return;
        }

        int radialSegments = Math.Clamp(segments, 6, 96);
        float r0 = MathF.Max(0.12f, startRadius);
        float r1 = MathF.Max(0.12f, endRadius);

        Vector3 axisDir = axis / axisLength;
        Vector3 tangent = MathF.Abs(Vector3.Dot(axisDir, Vector3.UnitZ)) > 0.95f
            ? Vector3.UnitY
            : Vector3.UnitZ;
        Vector3 basisX = SafeNormalize(Vector3.Cross(axisDir, tangent), Vector3.UnitX);
        Vector3 basisY = SafeNormalize(Vector3.Cross(axisDir, basisX), Vector3.UnitY);

        float step = (MathF.PI * 2f) / radialSegments;
        float slope = (r0 - r1) / axisLength;

        for (int i = 0; i < radialSegments; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            Vector3 radial0 = (basisX * MathF.Cos(a0)) + (basisY * MathF.Sin(a0));
            Vector3 radial1 = (basisX * MathF.Cos(a1)) + (basisY * MathF.Sin(a1));
            Vector3 p0 = start + (radial0 * r0);
            Vector3 p1 = start + (radial1 * r0);
            Vector3 p2 = end + (radial1 * r1);
            Vector3 p3 = end + (radial0 * r1);
            Vector3 n0 = SafeNormalize(radial0 + (axisDir * slope), radial0);
            Vector3 n1 = SafeNormalize(radial1 + (axisDir * slope), radial1);
            Vector3 t0 = SafeNormalize(Vector3.Cross(axisDir, radial0), basisX);
            Vector3 t1 = SafeNormalize(Vector3.Cross(axisDir, radial1), basisX);
            AddCurvedFace(vertices, indices, p0, p1, p2, p3, n0, n1, t0, t1);
        }

        if (capEnd)
        {
            AddDisc(vertices, indices, end, r1, radialSegments, axisDir);
        }

        if (capStart)
        {
            AddDisc(vertices, indices, start, r0, radialSegments, -axisDir);
        }
    }

    private static void AddDisc(List<MetalVertex> vertices, List<uint> indices, Vector3 center, float radius, int sides, Vector3 normal)
    {
        int segmentCount = Math.Clamp(sides, 3, 128);
        Vector3 tangent = MathF.Abs(Vector3.Dot(normal, Vector3.UnitZ)) > 0.95f
            ? Vector3.UnitX
            : Vector3.UnitZ;
        Vector3 basisX = SafeNormalize(Vector3.Cross(normal, tangent), Vector3.UnitX);
        Vector3 basisY = SafeNormalize(Vector3.Cross(normal, basisX), Vector3.UnitY);
        uint centerIndex = (uint)vertices.Count;
        Vector4 discTangent = new(SafeNormalize(basisX, Vector3.UnitX), 1f);
        vertices.Add(new MetalVertex
        {
            Position = center,
            Normal = normal,
            Tangent = discTangent,
            Texcoord = new Vector2(0.5f, 0.5f)
        });

        float step = (MathF.PI * 2f) / segmentCount;
        float resolvedRadius = MathF.Max(0.12f, radius);
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = i * step;
            Vector3 radial = (basisX * MathF.Cos(angle)) + (basisY * MathF.Sin(angle));
            vertices.Add(new MetalVertex
            {
                Position = center + (radial * resolvedRadius),
                Normal = normal,
                Tangent = discTangent,
                Texcoord = new Vector2((MathF.Cos(angle) * 0.5f) + 0.5f, (MathF.Sin(angle) * 0.5f) + 0.5f)
            });
        }

        for (int i = 0; i < segmentCount; i++)
        {
            uint i0 = centerIndex;
            uint i1 = centerIndex + 1u + (uint)i;
            uint i2 = centerIndex + 1u + (uint)((i + 1) % segmentCount);
            indices.Add(i0);
            indices.Add(i1);
            indices.Add(i2);
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
        Vector3 tangent = SafeNormalize(tangentDirection, Vector3.UnitX);
        Vector4 packedTangent = new(tangent, 1f);
        vertices.Add(new MetalVertex { Position = p0, Normal = normal, Tangent = packedTangent, Texcoord = new Vector2(0f, 0f) });
        vertices.Add(new MetalVertex { Position = p1, Normal = normal, Tangent = packedTangent, Texcoord = new Vector2(1f, 0f) });
        vertices.Add(new MetalVertex { Position = p2, Normal = normal, Tangent = packedTangent, Texcoord = new Vector2(1f, 1f) });
        vertices.Add(new MetalVertex { Position = p3, Normal = normal, Tangent = packedTangent, Texcoord = new Vector2(0f, 1f) });

        indices.Add(start + 0u);
        indices.Add(start + 1u);
        indices.Add(start + 2u);
        indices.Add(start + 0u);
        indices.Add(start + 2u);
        indices.Add(start + 3u);
    }

    private static void AddCurvedFace(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        Vector3 n0,
        Vector3 n1,
        Vector3 t0,
        Vector3 t1)
    {
        uint baseIndex = (uint)vertices.Count;
        Vector3 normal0 = SafeNormalize(n0, Vector3.UnitZ);
        Vector3 normal1 = SafeNormalize(n1, normal0);
        Vector3 tangent0Vec = SafeNormalize(t0, Vector3.UnitX);
        Vector3 tangent1Vec = SafeNormalize(t1, tangent0Vec);
        Vector4 tangent0 = new(tangent0Vec, 1f);
        Vector4 tangent1 = new(tangent1Vec, 1f);

        vertices.Add(new MetalVertex { Position = p0, Normal = normal0, Tangent = tangent0, Texcoord = new Vector2(0f, 0f) });
        vertices.Add(new MetalVertex { Position = p1, Normal = normal1, Tangent = tangent1, Texcoord = new Vector2(1f, 0f) });
        vertices.Add(new MetalVertex { Position = p2, Normal = normal1, Tangent = tangent1, Texcoord = new Vector2(1f, 1f) });
        vertices.Add(new MetalVertex { Position = p3, Normal = normal0, Tangent = tangent0, Texcoord = new Vector2(0f, 1f) });

        indices.Add(baseIndex + 0u);
        indices.Add(baseIndex + 1u);
        indices.Add(baseIndex + 2u);
        indices.Add(baseIndex + 0u);
        indices.Add(baseIndex + 2u);
        indices.Add(baseIndex + 3u);
    }

    private static Vector3 ResolveBackplateCenter(in SliderAssemblyConfig config)
    {
        return new Vector3(0f, 0f, -config.ThumbDepth * 0.90f);
    }

    private static Vector3 ResolveThumbCenter(in SliderAssemblyConfig config)
    {
        float travel = MathF.Max(0f, config.BackplateHeight - config.ThumbHeight);
        float minY = -travel * 0.5f;
        float y = minY + (travel * config.ThumbPositionNormalized);
        return new Vector3(0f, y, config.ThumbDepth * 0.25f);
    }

    private static string ResolveSliderRootDirectory()
    {
        string desktopRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Monozukuri");
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

    private static float ResolveDimensionOverride(float overrideValue, float fallbackValue)
    {
        if (float.IsFinite(overrideValue) && overrideValue > 0f)
        {
            return overrideValue;
        }

        return fallbackValue;
    }

    private static string ResolveImportedMeshPath(string configuredPath, string sliderRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

        string trimmed = configuredPath.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            return File.Exists(trimmed) ? trimmed : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(sliderRootDirectory))
        {
            return string.Empty;
        }

        string combined = Path.GetFullPath(Path.Combine(sliderRootDirectory, trimmed));
        return File.Exists(combined) ? combined : string.Empty;
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

    private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        if (value.LengthSquared() > 1e-8f)
        {
            return Vector3.Normalize(value);
        }

        if (fallback.LengthSquared() > 1e-8f)
        {
            return Vector3.Normalize(fallback);
        }

        return Vector3.UnitZ;
    }
}
