using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using KnobForge.Core;
using KnobForge.Core.Scene;

namespace KnobForge.Rendering.GPU;

public enum PushButtonPartKind
{
    Base = 0,
    Cap = 1
}

public readonly record struct PushButtonAssemblyConfig(
    bool Enabled,
    float PlateWidth,
    float PlateHeight,
    float PlateThickness,
    float BezelRadius,
    float BezelHeight,
    float CapRadius,
    float CapHeight,
    float PressDepth,
    PushButtonCapProfile CapProfile,
    PushButtonBezelProfile BezelProfile,
    PushButtonSkirtStyle SkirtStyle,
    float BezelChamferSize,
    float CapOverhang,
    int CapSegments,
    int BezelSegments,
    float SkirtHeight,
    float SkirtRadius,
    string BaseImportedMeshPath,
    long BaseImportedMeshTicks,
    string CapImportedMeshPath,
    long CapImportedMeshTicks);

public sealed class PushButtonPartMesh
{
    public MetalVertex[] Vertices { get; init; } = Array.Empty<MetalVertex>();

    public uint[] Indices { get; init; } = Array.Empty<uint>();

    public float ReferenceRadius { get; init; }
}

public static class PushButtonAssemblyMeshBuilder
{
    private static readonly string[] PushButtonRootDirectoryCandidates =
    {
        Path.Combine("models", "button_models"),
        "button_models"
    };

    private static readonly string[] SupportedExtensions = { ".glb", ".stl" };
    private static readonly string[] BaseDirectoryNames = { "base_models", "bases", "base" };
    private static readonly string[] CapDirectoryNames = { "cap_models", "caps", "cap" };

    public static PushButtonAssemblyConfig ResolveConfig(KnobProject? project, RenderQualityTier quality = RenderQualityTier.Normal)
    {
        if (project is null || project.ProjectType != InteractorProjectType.PushButton)
        {
            return default;
        }

        ModelNode? modelNode = project.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
        if (modelNode is null)
        {
            return default;
        }

        string pushButtonRootDirectory = ResolvePushButtonRootDirectory();
        float knobRadius = MathF.Max(40f, modelNode.Radius);
        float knobHeight = MathF.Max(20f, modelNode.Height);

        float plateWidth = knobRadius * 1.36f;
        float plateHeight = knobRadius * 1.36f;
        float plateThickness = 20f;
        float bezelRadius = knobRadius * 0.54f;
        float bezelHeight = MathF.Max(8f, knobHeight * 0.22f);
        float capRadius = knobRadius * 0.46f;
        float capHeight = MathF.Max(10f, knobHeight * 0.38f);
        float pressAmount = Math.Clamp(project.PushButtonPressAmountNormalized, 0f, 1f);
        float maxPressDepth = MathF.Max(1f, MathF.Min(capHeight * 0.58f, bezelHeight * 0.95f));
        float pressDepth = pressAmount * maxPressDepth;

        PushButtonCapProfile capProfile = project.PushButtonCapProfile;
        PushButtonBezelProfile bezelProfile = project.PushButtonBezelProfile;
        PushButtonSkirtStyle skirtStyle = project.PushButtonSkirtStyle;
        float chamferSize = project.PushButtonBezelChamferSize > 0f
            ? project.PushButtonBezelChamferSize
            : bezelHeight * 0.12f;
        float capOverhang = capProfile == PushButtonCapProfile.Mushroom
            ? (project.PushButtonCapOverhang > 0f ? project.PushButtonCapOverhang : bezelRadius * 0.15f)
            : 0f;
        int capSegments = ScaleSegments(project.PushButtonCapSegments > 0 ? project.PushButtonCapSegments : 28, quality, 8, 128);
        int bezelSegments = ScaleSegments(project.PushButtonBezelSegments > 0 ? project.PushButtonBezelSegments : 28, quality, 8, 128);
        float skirtHeight = project.PushButtonSkirtHeight > 0f
            ? project.PushButtonSkirtHeight
            : bezelHeight * 0.18f;
        float skirtRadius = project.PushButtonSkirtRadius > 0f
            ? project.PushButtonSkirtRadius
            : bezelRadius + 2f;
        string baseImportedMeshPath = ResolveImportedMeshPath(project.PushButtonBaseImportedMeshPath, pushButtonRootDirectory, PushButtonPartKind.Base);
        string capImportedMeshPath = ResolveImportedMeshPath(project.PushButtonCapImportedMeshPath, pushButtonRootDirectory, PushButtonPartKind.Cap);

        return new PushButtonAssemblyConfig(
            Enabled: true,
            PlateWidth: plateWidth,
            PlateHeight: plateHeight,
            PlateThickness: plateThickness,
            BezelRadius: bezelRadius,
            BezelHeight: bezelHeight,
            CapRadius: capRadius,
            CapHeight: capHeight,
            PressDepth: pressDepth,
            CapProfile: capProfile,
            BezelProfile: bezelProfile,
            SkirtStyle: skirtStyle,
            BezelChamferSize: chamferSize,
            CapOverhang: capOverhang,
            CapSegments: capSegments,
            BezelSegments: bezelSegments,
            SkirtHeight: skirtHeight,
            SkirtRadius: skirtRadius,
            BaseImportedMeshPath: baseImportedMeshPath,
            BaseImportedMeshTicks: ResolveFileTicks(baseImportedMeshPath),
            CapImportedMeshPath: capImportedMeshPath,
            CapImportedMeshTicks: ResolveFileTicks(capImportedMeshPath));
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

    public static PushButtonPartMesh BuildBaseMesh(in PushButtonAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new PushButtonPartMesh();
        }

        Vector3 baseCenter = ResolveBaseImportedCenter(config);
        PushButtonPartMesh? imported = TryBuildImportedPart(
            config.BaseImportedMeshPath,
            config.PlateWidth,
            config.PlateHeight,
            config.PlateThickness + config.BezelHeight,
            baseCenter);
        if (imported is not null)
        {
            return imported;
        }

        var vertices = new List<MetalVertex>(1200);
        var indices = new List<uint>(3600);
        Vector3 plateCenter = ResolvePlateCenter(config);
        AddBox(vertices, indices, config.PlateWidth, config.PlateHeight, config.PlateThickness, plateCenter);

        Vector3 bezelStart = plateCenter + new Vector3(0f, 0f, config.PlateThickness * 0.5f);
        Vector3 bezelEnd = bezelStart + new Vector3(0f, 0f, config.BezelHeight);
        AddBezelProfile(vertices, indices, config, bezelStart, bezelEnd);

        return BuildPartMesh(vertices, indices);
    }

    public static PushButtonPartMesh BuildCapMesh(in PushButtonAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new PushButtonPartMesh();
        }

        Vector3 capCenter = ResolveCapImportedCenter(config);
        float capDiameter = (config.CapRadius + MathF.Max(0f, config.CapOverhang)) * 2f;
        PushButtonPartMesh? imported = TryBuildImportedPart(
            config.CapImportedMeshPath,
            capDiameter,
            capDiameter,
            MathF.Max(config.CapHeight, capDiameter * 0.35f),
            capCenter);
        if (imported is not null)
        {
            return imported;
        }

        var vertices = new List<MetalVertex>(1600);
        var indices = new List<uint>(4800);

        float capBottom = ResolveCapBottomZ(config);
        float capTop = capBottom + config.CapHeight;
        float centerY = ResolvePlateCenter(config).Y;

        switch (config.CapProfile)
        {
            case PushButtonCapProfile.Domed:
                AddDomedCap(vertices, indices, config, centerY, capBottom, capTop, config.CapRadius);
                break;
            case PushButtonCapProfile.Concave:
                AddConcaveCap(vertices, indices, config, centerY, capBottom, capTop);
                break;
            case PushButtonCapProfile.Stepped:
                AddSteppedCap(vertices, indices, config, centerY, capBottom, capTop);
                break;
            case PushButtonCapProfile.Mushroom:
                AddDomedCap(vertices, indices, config, centerY, capBottom, capTop, config.CapRadius + MathF.Max(0f, config.CapOverhang));
                break;
            default:
                AddCylinderFrustum(
                    vertices,
                    indices,
                    new Vector3(0f, centerY, capBottom),
                    new Vector3(0f, centerY, capTop),
                    config.CapRadius,
                    config.CapRadius,
                    config.CapSegments,
                    capStart: true,
                    capEnd: true);
                break;
        }

        return BuildPartMesh(vertices, indices);
    }

    public static PushButtonPartMesh BuildSkirtMesh(in PushButtonAssemblyConfig config)
    {
        if (!config.Enabled || config.SkirtStyle == PushButtonSkirtStyle.None)
        {
            return new PushButtonPartMesh();
        }

        var vertices = new List<MetalVertex>(1200);
        var indices = new List<uint>(3600);

        Vector3 plateCenter = ResolvePlateCenter(config);
        float baseZ = plateCenter.Z + (config.PlateThickness * 0.5f);
        float outerRadius = MathF.Max(config.BezelRadius + 0.5f, config.SkirtRadius);
        float innerRadius = MathF.Max(0.5f, config.BezelRadius * 0.94f);
        float height;

        switch (config.SkirtStyle)
        {
            case PushButtonSkirtStyle.Ring:
                height = MathF.Max(0.35f, config.SkirtHeight * 0.3f);
                AddTube(
                    vertices,
                    indices,
                    new Vector3(0f, plateCenter.Y, baseZ),
                    new Vector3(0f, plateCenter.Y, baseZ + height),
                    outerRadius,
                    innerRadius,
                    config.BezelSegments);
                break;
            case PushButtonSkirtStyle.Collar:
                height = MathF.Max(0.5f, config.SkirtHeight);
                AddTube(
                    vertices,
                    indices,
                    new Vector3(0f, plateCenter.Y, baseZ),
                    new Vector3(0f, plateCenter.Y, baseZ + height),
                    outerRadius,
                    innerRadius,
                    config.BezelSegments);
                break;
            case PushButtonSkirtStyle.Flange:
                height = MathF.Max(0.25f, config.SkirtHeight * 0.2f);
                AddTube(
                    vertices,
                    indices,
                    new Vector3(0f, plateCenter.Y, baseZ),
                    new Vector3(0f, plateCenter.Y, baseZ + height),
                    outerRadius * 1.3f,
                    innerRadius,
                    config.BezelSegments);
                break;
        }

        return BuildPartMesh(vertices, indices);
    }

    private static void AddBezelProfile(
        List<MetalVertex> vertices,
        List<uint> indices,
        in PushButtonAssemblyConfig config,
        Vector3 start,
        Vector3 end)
    {
        float radius = config.BezelRadius;
        int segments = config.BezelSegments;
        float height = MathF.Max(0.5f, end.Z - start.Z);
        float chamfer = Math.Clamp(config.BezelChamferSize, 0.25f, height * 0.4f);

        switch (config.BezelProfile)
        {
            case PushButtonBezelProfile.Chamfered:
            {
                Vector3 mid0 = start + new Vector3(0f, 0f, chamfer);
                Vector3 mid1 = end - new Vector3(0f, 0f, chamfer);
                float chamferDelta = MathF.Min(radius * 0.18f, chamfer * 0.8f);
                AddCylinderFrustum(vertices, indices, start, mid0, radius + chamferDelta, radius, segments, capStart: true, capEnd: false);
                AddCylinderFrustum(vertices, indices, mid0, mid1, radius, radius, segments, capStart: false, capEnd: false);
                AddCylinderFrustum(vertices, indices, mid1, end, radius, MathF.Max(0.5f, radius - chamferDelta), segments, capStart: false, capEnd: true);
                break;
            }

            case PushButtonBezelProfile.Filleted:
            {
                float filletDelta = MathF.Min(radius * 0.16f, chamfer * 0.65f);
                float slice = height / 5f;
                Vector3 p0 = start;
                Vector3 p1 = start + new Vector3(0f, 0f, slice);
                Vector3 p2 = start + new Vector3(0f, 0f, slice * 2f);
                Vector3 p3 = start + new Vector3(0f, 0f, slice * 3f);
                Vector3 p4 = start + new Vector3(0f, 0f, slice * 4f);
                Vector3 p5 = end;
                AddCylinderFrustum(vertices, indices, p0, p1, radius + filletDelta, radius + (filletDelta * 0.55f), segments, capStart: true, capEnd: false);
                AddCylinderFrustum(vertices, indices, p1, p2, radius + (filletDelta * 0.55f), radius, segments, capStart: false, capEnd: false);
                AddCylinderFrustum(vertices, indices, p2, p3, radius, radius, segments, capStart: false, capEnd: false);
                AddCylinderFrustum(vertices, indices, p3, p4, radius, MathF.Max(0.5f, radius - (filletDelta * 0.30f)), segments, capStart: false, capEnd: false);
                AddCylinderFrustum(vertices, indices, p4, p5, MathF.Max(0.5f, radius - (filletDelta * 0.30f)), MathF.Max(0.5f, radius - (filletDelta * 0.55f)), segments, capStart: false, capEnd: true);
                break;
            }

            case PushButtonBezelProfile.Flared:
                AddCylinderFrustum(vertices, indices, start, end, radius * 0.88f, radius, segments, capStart: true, capEnd: true);
                break;

            default:
                AddCylinderFrustum(vertices, indices, start, end, radius, radius, segments, capStart: true, capEnd: true);
                break;
        }
    }

    private static void AddDomedCap(
        List<MetalVertex> vertices,
        List<uint> indices,
        in PushButtonAssemblyConfig config,
        float centerY,
        float capBottom,
        float capTop,
        float domeOuterRadius)
    {
        float baseRadius = config.CapRadius;
        float bodyHeight = MathF.Max(0.5f, config.CapHeight * 0.42f);
        float domeHeight = MathF.Max(0.5f, config.CapHeight - bodyHeight);
        Vector3 bodyStart = new(0f, centerY, capBottom);
        Vector3 bodyEnd = new(0f, centerY, capBottom + bodyHeight);
        AddCylinderFrustum(vertices, indices, bodyStart, bodyEnd, baseRadius, baseRadius, config.CapSegments, capStart: true, capEnd: false);

        const int domeSlices = 5;
        Vector3 previousCenter = bodyEnd;
        float previousRadius = baseRadius;
        for (int i = 1; i <= domeSlices; i++)
        {
            float t = i / (float)domeSlices;
            float theta = t * (MathF.PI * 0.5f);
            float z = bodyEnd.Z + (domeHeight * MathF.Sin(theta));
            float radius = MathF.Max(0.12f, domeOuterRadius * MathF.Cos(theta));
            Vector3 nextCenter = new(0f, centerY, z);
            AddCylinderFrustum(
                vertices,
                indices,
                previousCenter,
                nextCenter,
                previousRadius,
                radius,
                config.CapSegments,
                capStart: false,
                capEnd: i == domeSlices);
            previousCenter = nextCenter;
            previousRadius = radius;
        }
    }

    private static void AddConcaveCap(
        List<MetalVertex> vertices,
        List<uint> indices,
        in PushButtonAssemblyConfig config,
        float centerY,
        float capBottom,
        float capTop)
    {
        float recessDepth = MathF.Max(0.5f, config.CapRadius * 0.15f);
        float rimRadius = MathF.Max(0.5f, config.CapRadius * 0.82f);
        Vector3 capStart = new(0f, centerY, capBottom);
        Vector3 capEnd = new(0f, centerY, capTop);

        AddCylinderFrustum(vertices, indices, capStart, capEnd, config.CapRadius, config.CapRadius, config.CapSegments, capStart: true, capEnd: false);
        AddRingFace(vertices, indices, capEnd, rimRadius, config.CapRadius, config.CapSegments, Vector3.UnitZ);

        const int bowlSlices = 4;
        Vector3 previousCenter = new(0f, centerY, capTop - 0.02f);
        float previousRadius = rimRadius;
        for (int i = 1; i <= bowlSlices; i++)
        {
            float t = i / (float)bowlSlices;
            float theta = t * (MathF.PI * 0.5f);
            float z = capTop - (recessDepth * MathF.Sin(theta));
            float radius = MathF.Max(0.12f, rimRadius * MathF.Cos(theta));
            Vector3 nextCenter = new(0f, centerY, z);
            AddCylinderFrustum(
                vertices,
                indices,
                previousCenter,
                nextCenter,
                previousRadius,
                radius,
                config.CapSegments,
                capStart: false,
                capEnd: i == bowlSlices,
                invertFacing: true);
            previousCenter = nextCenter;
            previousRadius = radius;
        }
    }

    private static void AddSteppedCap(
        List<MetalVertex> vertices,
        List<uint> indices,
        in PushButtonAssemblyConfig config,
        float centerY,
        float capBottom,
        float capTop)
    {
        float lowerHeight = MathF.Max(0.5f, config.CapHeight * 0.6f);
        float upperHeight = MathF.Max(0.35f, config.CapHeight - lowerHeight);
        float upperRadius = MathF.Max(0.5f, config.CapRadius * 0.7f);

        Vector3 lowerStart = new(0f, centerY, capBottom);
        Vector3 lowerEnd = new(0f, centerY, capBottom + lowerHeight);
        Vector3 upperEnd = new(0f, centerY, MathF.Min(capTop, lowerEnd.Z + upperHeight));

        AddCylinderFrustum(vertices, indices, lowerStart, lowerEnd, config.CapRadius, config.CapRadius, config.CapSegments, capStart: true, capEnd: false);
        AddCylinderFrustum(vertices, indices, lowerEnd, upperEnd, upperRadius, upperRadius, config.CapSegments, capStart: true, capEnd: true);
    }

    private static PushButtonPartMesh? TryBuildImportedPart(
        string importedPath,
        float targetWidth,
        float targetHeight,
        float targetDepth,
        Vector3 center)
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

        MetalVertex[] transformedVertices = new MetalVertex[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            transformedVertices[i] = new MetalVertex
            {
                Position = vertices[i].Position + center,
                Normal = vertices[i].Normal,
                Tangent = vertices[i].Tangent,
                Texcoord = vertices[i].Texcoord
            };
        }

        return new PushButtonPartMesh
        {
            Vertices = transformedVertices,
            Indices = indices,
            ReferenceRadius = MathF.Max(referenceRadius, center.Length() + referenceRadius)
        };
    }

    private static Vector3 ResolvePlateCenter(in PushButtonAssemblyConfig config)
    {
        return new Vector3(
            0f,
            -config.PlateHeight * 0.45f,
            -(config.PlateThickness * 0.5f) - 8f);
    }

    private static Vector3 ResolveBaseImportedCenter(in PushButtonAssemblyConfig config)
    {
        Vector3 plateCenter = ResolvePlateCenter(config);
        return plateCenter + new Vector3(0f, 0f, config.BezelHeight * 0.45f);
    }

    private static float ResolveCapBottomZ(in PushButtonAssemblyConfig config)
    {
        Vector3 plateCenter = ResolvePlateCenter(config);
        float topOfBezel = plateCenter.Z + (config.PlateThickness * 0.5f) + config.BezelHeight;
        return topOfBezel - MathF.Max(0f, config.PressDepth);
    }

    private static Vector3 ResolveCapImportedCenter(in PushButtonAssemblyConfig config)
    {
        Vector3 plateCenter = ResolvePlateCenter(config);
        float capBottom = ResolveCapBottomZ(config);
        return new Vector3(0f, plateCenter.Y, capBottom + (config.CapHeight * 0.5f));
    }

    private static PushButtonPartMesh BuildPartMesh(List<MetalVertex> vertices, List<uint> indices)
    {
        float referenceRadius = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            referenceRadius = MathF.Max(referenceRadius, vertices[i].Position.Length());
        }

        return new PushButtonPartMesh
        {
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray(),
            ReferenceRadius = referenceRadius
        };
    }

    private static void AddTube(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 start,
        Vector3 end,
        float outerRadius,
        float innerRadius,
        int sides)
    {
        float resolvedOuter = MathF.Max(0.5f, outerRadius);
        float resolvedInner = Math.Clamp(innerRadius, 0.15f, resolvedOuter - 0.1f);
        AddCylinderFrustum(vertices, indices, start, end, resolvedOuter, resolvedOuter, sides, capStart: false, capEnd: false);
        AddCylinderFrustum(vertices, indices, start, end, resolvedInner, resolvedInner, sides, capStart: false, capEnd: false, invertFacing: true);
        AddRingFace(vertices, indices, end, resolvedInner, resolvedOuter, sides, Vector3.UnitZ);
        AddRingFace(vertices, indices, start, resolvedInner, resolvedOuter, sides, -Vector3.UnitZ);
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
            center + new Vector3(-hx, -hy, -hz),
            center + new Vector3(hx, -hy, -hz),
            center + new Vector3(hx, hy, -hz),
            center + new Vector3(-hx, hy, -hz),
            center + new Vector3(-hx, -hy, hz),
            center + new Vector3(hx, -hy, hz),
            center + new Vector3(hx, hy, hz),
            center + new Vector3(-hx, hy, hz)
        };

        AddFace(vertices, indices, corners[0], corners[1], corners[2], corners[3], new Vector3(0f, 0f, -1f), Vector3.UnitX);
        AddFace(vertices, indices, corners[5], corners[4], corners[7], corners[6], new Vector3(0f, 0f, 1f), -Vector3.UnitX);
        AddFace(vertices, indices, corners[4], corners[0], corners[3], corners[7], -Vector3.UnitX, Vector3.UnitY);
        AddFace(vertices, indices, corners[1], corners[5], corners[6], corners[2], Vector3.UnitX, -Vector3.UnitY);
        AddFace(vertices, indices, corners[3], corners[2], corners[6], corners[7], Vector3.UnitY, Vector3.UnitX);
        AddFace(vertices, indices, corners[4], corners[5], corners[1], corners[0], -Vector3.UnitY, Vector3.UnitX);
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
        bool capEnd,
        bool invertFacing = false)
    {
        Vector3 axis = end - start;
        float axisLength = axis.Length();
        if (axisLength <= 1e-5f)
        {
            return;
        }

        int radialSegments = Math.Clamp(segments, 6, 128);
        float r0 = MathF.Max(0.12f, startRadius);
        float r1 = MathF.Max(0.12f, endRadius);

        Vector3 axisDir = axis / axisLength;
        Vector3 tangent = MathF.Abs(Vector3.Dot(axisDir, Vector3.UnitZ)) > 0.95f
            ? Vector3.UnitX
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

            if (invertFacing)
            {
                n0 = -n0;
                n1 = -n1;
                t0 = -t0;
                t1 = -t1;
            }

            AddCurvedFace(vertices, indices, p0, p1, p2, p3, n0, n1, t0, t1, invertFacing);
        }

        if (capEnd)
        {
            AddDisc(vertices, indices, end, r1, radialSegments, invertFacing ? -axisDir : axisDir, invertFacing);
        }

        if (capStart)
        {
            AddDisc(vertices, indices, start, r0, radialSegments, invertFacing ? axisDir : -axisDir, invertFacing);
        }
    }

    private static void AddDisc(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 center,
        float radius,
        int sides,
        Vector3 normal,
        bool flipWinding = false)
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
            if (flipWinding)
            {
                indices.Add(i0);
                indices.Add(i2);
                indices.Add(i1);
            }
            else
            {
                indices.Add(i0);
                indices.Add(i1);
                indices.Add(i2);
            }
        }
    }

    private static void AddRingFace(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 center,
        float innerRadius,
        float outerRadius,
        int sides,
        Vector3 normal)
    {
        int segmentCount = Math.Clamp(sides, 6, 128);
        float resolvedInner = Math.Max(0.12f, Math.Min(innerRadius, outerRadius - 0.05f));
        float resolvedOuter = Math.Max(resolvedInner + 0.05f, outerRadius);

        Vector3 tangent = MathF.Abs(Vector3.Dot(normal, Vector3.UnitZ)) > 0.95f
            ? Vector3.UnitX
            : Vector3.UnitZ;
        Vector3 basisX = SafeNormalize(Vector3.Cross(normal, tangent), Vector3.UnitX);
        Vector3 basisY = SafeNormalize(Vector3.Cross(normal, basisX), Vector3.UnitY);
        float step = (MathF.PI * 2f) / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            Vector3 radial0 = (basisX * MathF.Cos(a0)) + (basisY * MathF.Sin(a0));
            Vector3 radial1 = (basisX * MathF.Cos(a1)) + (basisY * MathF.Sin(a1));
            Vector3 p0 = center + (radial0 * resolvedOuter);
            Vector3 p1 = center + (radial1 * resolvedOuter);
            Vector3 p2 = center + (radial1 * resolvedInner);
            Vector3 p3 = center + (radial0 * resolvedInner);
            AddFace(vertices, indices, p0, p1, p2, p3, normal, radial0);
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
        Vector3 tangentDirection,
        bool flipWinding = false)
    {
        uint start = (uint)vertices.Count;
        Vector3 tangent = SafeNormalize(tangentDirection, Vector3.UnitX);
        Vector4 packedTangent = new(tangent, 1f);

        vertices.Add(new MetalVertex { Position = p0, Normal = normal, Tangent = packedTangent, Texcoord = new Vector2(0f, 0f) });
        vertices.Add(new MetalVertex { Position = p1, Normal = normal, Tangent = packedTangent, Texcoord = new Vector2(1f, 0f) });
        vertices.Add(new MetalVertex { Position = p2, Normal = normal, Tangent = packedTangent, Texcoord = new Vector2(1f, 1f) });
        vertices.Add(new MetalVertex { Position = p3, Normal = normal, Tangent = packedTangent, Texcoord = new Vector2(0f, 1f) });

        if (flipWinding)
        {
            indices.Add(start + 0u);
            indices.Add(start + 2u);
            indices.Add(start + 1u);
            indices.Add(start + 0u);
            indices.Add(start + 3u);
            indices.Add(start + 2u);
        }
        else
        {
            indices.Add(start + 0u);
            indices.Add(start + 1u);
            indices.Add(start + 2u);
            indices.Add(start + 0u);
            indices.Add(start + 2u);
            indices.Add(start + 3u);
        }
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
        Vector3 t1,
        bool flipWinding = false)
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

        if (flipWinding)
        {
            indices.Add(baseIndex + 0u);
            indices.Add(baseIndex + 2u);
            indices.Add(baseIndex + 1u);
            indices.Add(baseIndex + 0u);
            indices.Add(baseIndex + 3u);
            indices.Add(baseIndex + 2u);
        }
        else
        {
            indices.Add(baseIndex + 0u);
            indices.Add(baseIndex + 1u);
            indices.Add(baseIndex + 2u);
            indices.Add(baseIndex + 0u);
            indices.Add(baseIndex + 2u);
            indices.Add(baseIndex + 3u);
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

    private static string ResolvePushButtonRootDirectory()
    {
        string desktopRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Monozukuri");
        for (int i = 0; i < PushButtonRootDirectoryCandidates.Length; i++)
        {
            string candidate = Path.Combine(desktopRoot, PushButtonRootDirectoryCandidates[i]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(desktopRoot, PushButtonRootDirectoryCandidates[0]);
    }

    private static string ResolveImportedMeshPath(
        string configuredPath,
        string pushButtonRootDirectory,
        PushButtonPartKind partKind)
    {
        string? explicitPath = TryResolveExplicitMeshPath(configuredPath, pushButtonRootDirectory);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

        return ResolveLibraryImportedMeshPath(pushButtonRootDirectory, configuredPath, partKind);
    }

    private static string? TryResolveExplicitMeshPath(string configuredPath, string pushButtonRootDirectory)
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

        if (string.IsNullOrWhiteSpace(pushButtonRootDirectory))
        {
            return null;
        }

        string combined = Path.GetFullPath(Path.Combine(pushButtonRootDirectory, trimmed));
        return File.Exists(combined) ? combined : null;
    }

    private static string ResolveLibraryImportedMeshPath(
        string pushButtonRootDirectory,
        string configuredPath,
        PushButtonPartKind partKind)
    {
        if (string.IsNullOrWhiteSpace(pushButtonRootDirectory) || !Directory.Exists(pushButtonRootDirectory))
        {
            return string.Empty;
        }

        string fileName = Path.GetFileName(configuredPath);
        foreach (string directory in EnumeratePartDirectories(pushButtonRootDirectory, partKind))
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate) && SupportedExtensions.Any(ext => candidate.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumeratePartDirectories(string pushButtonRootDirectory, PushButtonPartKind partKind)
    {
        string[] names = partKind == PushButtonPartKind.Base
            ? BaseDirectoryNames
            : CapDirectoryNames;
        for (int i = 0; i < names.Length; i++)
        {
            string candidate = Path.Combine(pushButtonRootDirectory, names[i]);
            if (Directory.Exists(candidate))
            {
                yield return candidate;
            }
        }
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
}
