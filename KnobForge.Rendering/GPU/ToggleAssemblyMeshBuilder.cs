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
    float PlateOffsetY,
    float PlateOffsetZ,
    float BushingRadius,
    float BushingHeight,
    int BushingSides,
    ToggleBushingShape LowerBushingShape,
    ToggleBushingShape UpperBushingShape,
    float LowerBushingRadiusScale,
    float LowerBushingHeightRatio,
    float UpperBushingRadiusScale,
    float UpperBushingHeightRatio,
    float LeverLength,
    float LeverBottomRadius,
    float LeverTopRadius,
    int LeverSides,
    float LeverPivotOffset,
    float TipRadius,
    int TipLatitudeSegments,
    int TipLongitudeSegments,
    bool TipSleeveEnabled,
    float TipSleeveLength,
    float TipSleeveThickness,
    float TipSleeveOuterRadius,
    float TipSleeveCoverage,
    int TipSleeveSides,
    ToggleTipSleeveStyle TipSleeveStyle,
    ToggleTipSleeveTipStyle TipSleeveTipStyle,
    int TipSleevePatternCount,
    float TipSleevePatternDepth,
    float TipSleeveTipAmount,
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
        if (project is null || project.ProjectType != InteractorProjectType.FlipSwitch)
        {
            return default;
        }

        ModelNode? modelNode = project.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
        if (modelNode is null)
        {
            return default;
        }

        bool enabled = project.ToggleMode != ToggleAssemblyMode.Disabled;
        if (!enabled)
        {
            return default;
        }

        float knobRadius = MathF.Max(40f, modelNode.Radius);
        float plateWidth = ResolveDimensionOverride(project.TogglePlateWidth, knobRadius * 0.88f);
        float plateHeight = ResolveDimensionOverride(project.TogglePlateHeight, knobRadius * 1.08f);
        float plateThickness = ResolveDimensionOverride(project.TogglePlateThickness, 20f);
        float plateOffsetY = project.TogglePlateOffsetY;
        float plateOffsetZ = project.TogglePlateOffsetZ;
        float bushingRadius = ResolveDimensionOverride(project.ToggleBushingRadius, knobRadius * 0.16f);
        float bushingHeight = ResolveDimensionOverride(project.ToggleBushingHeight, knobRadius * 0.18f);
        int bushingSides = Math.Clamp(project.ToggleBushingSides, 3, 32);
        ToggleBushingShape lowerBushingShape = project.ToggleLowerBushingShape;
        ToggleBushingShape upperBushingShape = project.ToggleUpperBushingShape;
        float lowerBushingRadiusScale = Math.Clamp(project.ToggleLowerBushingRadiusScale, 0.25f, 4f);
        float lowerBushingHeightRatio = Math.Clamp(project.ToggleLowerBushingHeightRatio, 0.05f, 2f);
        float upperBushingRadiusScale = Math.Clamp(project.ToggleUpperBushingRadiusScale, 0.25f, 4f);
        float upperBushingHeightRatio = Math.Clamp(project.ToggleUpperBushingHeightRatio, 0.05f, 2f);
        float leverLength = ResolveDimensionOverride(project.ToggleLeverLength, knobRadius * 0.90f);
        float leverBottomRadius = ResolveDimensionOverride(project.ToggleLeverRadius, knobRadius * 0.065f);
        float leverTopRadius = ResolveDimensionOverride(project.ToggleLeverTopRadius, leverBottomRadius * 0.52f);
        int leverSides = Math.Clamp(project.ToggleLeverSides, 6, 64);
        float leverPivotOffset = project.ToggleLeverPivotOffset;
        float tipRadius = ResolveDimensionOverride(project.ToggleTipRadius, knobRadius * 0.11f);
        int tipLatitudeSegments = Math.Clamp(project.ToggleTipLatitudeSegments, 4, 64);
        int tipLongitudeSegments = Math.Clamp(project.ToggleTipLongitudeSegments, 6, 128);
        bool tipSleeveEnabled = project.ToggleTipSleeveEnabled;
        float tipSleeveLength = ResolveDimensionOverride(project.ToggleTipSleeveLength, tipRadius * 1.15f);
        float tipSleeveThickness = ResolveDimensionOverride(project.ToggleTipSleeveThickness, MathF.Max(0.75f, tipRadius * 0.18f));
        float tipSleeveOuterRadius = ResolveDimensionOverride(
            project.ToggleTipSleeveOuterRadius,
            MathF.Max(leverTopRadius, tipRadius * 0.62f) + tipSleeveThickness);
        float tipSleeveCoverage = Math.Clamp(project.ToggleTipSleeveCoverage, 0f, 1f);
        int tipSleeveSides = Math.Clamp(project.ToggleTipSleeveSides, 6, 64);
        ToggleTipSleeveStyle tipSleeveStyle = project.ToggleTipSleeveStyle;
        ToggleTipSleeveTipStyle tipSleeveTipStyle = project.ToggleTipSleeveTipStyle;
        int tipSleevePatternCount = Math.Clamp(project.ToggleTipSleevePatternCount, 3, 64);
        float tipSleevePatternDepth = Math.Clamp(project.ToggleTipSleevePatternDepth, 0f, 0.9f);
        float tipSleeveTipAmount = Math.Clamp(project.ToggleTipSleeveTipAmount, 0f, 0.95f);

        int stateCount = project.ToggleStateCount == ToggleAssemblyStateCount.ThreePosition ? 3 : 2;
        int stateIndex = ClampStateIndex(project.ToggleStateIndex, stateCount);
        float stateBlendPosition = float.IsFinite(project.ToggleStateBlendPosition)
            ? Math.Clamp(project.ToggleStateBlendPosition, 0f, stateCount - 1f)
            : stateIndex;
        float maxAngle = Math.Clamp(project.ToggleMaxAngleDeg, 5f, 85f);
        float leverAngleDeg = ResolveLeverAngleDeg(stateCount, stateBlendPosition, maxAngle);
        return new ToggleAssemblyConfig(
            Enabled: true,
            StateCount: stateCount,
            StateIndex: stateIndex,
            LeverAngleDeg: leverAngleDeg,
            PlateWidth: plateWidth,
            PlateHeight: plateHeight,
            PlateThickness: plateThickness,
            PlateOffsetY: plateOffsetY,
            PlateOffsetZ: plateOffsetZ,
            BushingRadius: bushingRadius,
            BushingHeight: bushingHeight,
            BushingSides: bushingSides,
            LowerBushingShape: lowerBushingShape,
            UpperBushingShape: upperBushingShape,
            LowerBushingRadiusScale: lowerBushingRadiusScale,
            LowerBushingHeightRatio: lowerBushingHeightRatio,
            UpperBushingRadiusScale: upperBushingRadiusScale,
            UpperBushingHeightRatio: upperBushingHeightRatio,
            LeverLength: leverLength,
            LeverBottomRadius: leverBottomRadius,
            LeverTopRadius: leverTopRadius,
            LeverSides: leverSides,
            LeverPivotOffset: leverPivotOffset,
            TipRadius: tipRadius,
            TipLatitudeSegments: tipLatitudeSegments,
            TipLongitudeSegments: tipLongitudeSegments,
            TipSleeveEnabled: tipSleeveEnabled,
            TipSleeveLength: tipSleeveLength,
            TipSleeveThickness: tipSleeveThickness,
            TipSleeveOuterRadius: tipSleeveOuterRadius,
            TipSleeveCoverage: tipSleeveCoverage,
            TipSleeveSides: tipSleeveSides,
            TipSleeveStyle: tipSleeveStyle,
            TipSleeveTipStyle: tipSleeveTipStyle,
            TipSleevePatternCount: tipSleevePatternCount,
            TipSleevePatternDepth: tipSleevePatternDepth,
            TipSleeveTipAmount: tipSleeveTipAmount,
            BaseImportedMeshPath: string.Empty,
            BaseImportedMeshTicks: 0L,
            LeverImportedMeshPath: string.Empty,
            LeverImportedMeshTicks: 0L);
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

        float lowerBushingHeight = MathF.Max(1f, config.BushingHeight * config.LowerBushingHeightRatio);
        float upperBushingHeight = MathF.Max(1f, config.BushingHeight * config.UpperBushingHeightRatio);
        float lowerBushingRadius = MathF.Max(0.5f, config.BushingRadius * config.LowerBushingRadiusScale);
        float upperBushingRadius = MathF.Max(0.5f, config.BushingRadius * config.UpperBushingRadiusScale);
        int lowerBushingSides = ResolveBushingSides(config.LowerBushingShape, config.BushingSides);
        int upperBushingSides = ResolveBushingSides(config.UpperBushingShape, config.BushingSides);
        float plateTop = plateCenter.Z + (config.PlateThickness * 0.5f);

        Vector3 lowerBushingCenter = new(
            plateCenter.X,
            plateCenter.Y,
            plateTop + (lowerBushingHeight * 0.5f));
        AddPrism(
            vertices,
            indices,
            lowerBushingCenter,
            lowerBushingRadius,
            lowerBushingHeight,
            lowerBushingSides);

        Vector3 upperBushingCenter = new(
            plateCenter.X,
            plateCenter.Y,
            plateTop + lowerBushingHeight + (upperBushingHeight * 0.5f));
        AddPrism(
            vertices,
            indices,
            upperBushingCenter,
            upperBushingRadius,
            upperBushingHeight,
            upperBushingSides);

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
        float lowerBushingHeight = MathF.Max(1f, config.BushingHeight * config.LowerBushingHeightRatio);
        float upperBushingHeight = MathF.Max(1f, config.BushingHeight * config.UpperBushingHeightRatio);
        Vector3 pivot = plateCenter + new Vector3(
            0f,
            0f,
            (config.PlateThickness * 0.5f) + lowerBushingHeight + upperBushingHeight + config.LeverPivotOffset);

        float angleRadians = MathF.PI * config.LeverAngleDeg / 180f;
        Matrix4x4 leverRotation = Matrix4x4.CreateRotationX(-angleRadians);
        Vector3 direction = Vector3.TransformNormal(Vector3.UnitZ, leverRotation);
        direction = SafeNormalize(direction, Vector3.UnitZ);
        Vector3 importedCenter = pivot + (direction * (config.LeverLength * 0.5f));
        float leverDiameter = MathF.Max(1f, MathF.Max(config.LeverBottomRadius, config.LeverTopRadius) * 2f);
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

        float effectiveTopRadius = MathF.Min(
            config.LeverTopRadius,
            MathF.Max(0.25f, config.TipRadius * 0.96f));
        Vector3 endpoint = pivot + (direction * config.LeverLength);
        AddRoundedTaperedCylinder(
            vertices,
            indices,
            pivot,
            endpoint,
            config.LeverBottomRadius,
            effectiveTopRadius,
            config.LeverSides,
            16);
        float tipJoinOffset = MathF.Sqrt(MathF.Max(
            0f,
            (config.TipRadius * config.TipRadius) - (effectiveTopRadius * effectiveTopRadius)));
        AddSphere(
            vertices,
            indices,
            endpoint + (direction * tipJoinOffset),
            config.TipRadius,
            config.TipLatitudeSegments,
            config.TipLongitudeSegments);

        return BuildPartMesh(vertices, indices);
    }

    public static TogglePartMesh BuildSleeveMesh(in ToggleAssemblyConfig config)
    {
        if (!config.Enabled || !config.TipSleeveEnabled)
        {
            return new TogglePartMesh();
        }

        var vertices = new List<MetalVertex>(640);
        var indices = new List<uint>(1800);
        Vector3 plateCenter = ResolvePlateCenter(config);
        float lowerBushingHeight = MathF.Max(1f, config.BushingHeight * config.LowerBushingHeightRatio);
        float upperBushingHeight = MathF.Max(1f, config.BushingHeight * config.UpperBushingHeightRatio);
        Vector3 pivot = plateCenter + new Vector3(
            0f,
            0f,
            (config.PlateThickness * 0.5f) + lowerBushingHeight + upperBushingHeight + config.LeverPivotOffset);

        float angleRadians = MathF.PI * config.LeverAngleDeg / 180f;
        Matrix4x4 leverRotation = Matrix4x4.CreateRotationX(-angleRadians);
        Vector3 direction = Vector3.TransformNormal(Vector3.UnitZ, leverRotation);
        direction = SafeNormalize(direction, Vector3.UnitZ);
        float effectiveTopRadius = MathF.Min(
            config.LeverTopRadius,
            MathF.Max(0.25f, config.TipRadius * 0.96f));
        Vector3 endpoint = pivot + (direction * config.LeverLength);
        float tipJoinOffset = MathF.Sqrt(MathF.Max(
            0f,
            (config.TipRadius * config.TipRadius) - (effectiveTopRadius * effectiveTopRadius)));
        Vector3 tipCenter = endpoint + (direction * tipJoinOffset);

        float sleeveLength = MathF.Max(1f, config.TipSleeveLength);
        float sleeveOuterRadius = MathF.Max(0.5f, config.TipSleeveOuterRadius);
        float sleeveThickness = MathF.Max(0.25f, config.TipSleeveThickness);
        float sleeveInnerRadius = Math.Clamp(sleeveOuterRadius - sleeveThickness, 0.2f, sleeveOuterRadius - 0.05f);
        if (sleeveInnerRadius >= sleeveOuterRadius - 0.05f)
        {
            return new TogglePartMesh();
        }

        float coverage = Math.Clamp(config.TipSleeveCoverage, 0f, 1f);
        Vector3 lowerAnchor = endpoint - (direction * (sleeveLength * 0.35f));
        Vector3 upperAnchor = tipCenter;
        Vector3 sleeveCenter = Vector3.Lerp(lowerAnchor, upperAnchor, coverage);
        Vector3 sleeveStart = sleeveCenter - (direction * (sleeveLength * 0.5f));
        Vector3 sleeveEnd = sleeveCenter + (direction * (sleeveLength * 0.5f));
        AddStyledHollowCylinder(
            vertices,
            indices,
            sleeveStart,
            sleeveEnd,
            sleeveInnerRadius,
            sleeveOuterRadius,
            config.TipSleeveSides,
            config.TipSleeveStyle,
            config.TipSleeveTipStyle,
            config.TipSleevePatternCount,
            config.TipSleevePatternDepth,
            config.TipSleeveTipAmount);

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
            (-config.PlateHeight * 0.60f) + config.PlateOffsetY,
            (-(config.PlateThickness * 0.5f) - 8f) + config.PlateOffsetZ);
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
            AddFace(vertices, indices, p0, p3, p2, p1, normal, Vector3.UnitZ);
        }

        // Keep cap angular phase aligned with the side-wall radial basis (+X at angle 0).
        AddDisc(
            vertices,
            indices,
            center + new Vector3(0f, 0f, hz),
            r,
            sideCount,
            Vector3.UnitZ,
            Vector3.UnitX,
            Vector3.UnitY);
        AddDisc(
            vertices,
            indices,
            center + new Vector3(0f, 0f, -hz),
            r,
            sideCount,
            -Vector3.UnitZ,
            Vector3.UnitX,
            Vector3.UnitY);
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
            AddFace(vertices, indices, p0, p3, p2, p1, normal, forward);
        }

        AddDisc(vertices, indices, start, r, sideCount, -forward, tangentA, tangentB);
        AddDisc(vertices, indices, end, r, sideCount, forward, tangentA, tangentB);
    }

    private static void AddHollowCylinder(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 start,
        Vector3 end,
        float innerRadius,
        float outerRadius,
        int sides)
    {
        Vector3 axis = end - start;
        float axisLength = axis.Length();
        if (axisLength <= 1e-5f)
        {
            return;
        }

        float outer = MathF.Max(0.25f, outerRadius);
        float inner = Math.Clamp(innerRadius, 0.1f, outer - 0.05f);
        if (inner >= outer - 0.05f)
        {
            return;
        }

        int sideCount = Math.Clamp(sides, 6, 64);
        float step = MathF.PI * 2f / sideCount;
        Vector3 forward = axis / axisLength;
        Vector3 tangentA = MathF.Abs(forward.Z) < 0.999f
            ? Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ))
            : Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitX));
        Vector3 tangentB = SafeNormalize(Vector3.Cross(forward, tangentA), Vector3.UnitY);

        for (int i = 0; i < sideCount; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            Vector3 radial0 = (MathF.Cos(a0) * tangentA) + (MathF.Sin(a0) * tangentB);
            Vector3 radial1 = (MathF.Cos(a1) * tangentA) + (MathF.Sin(a1) * tangentB);
            Vector3 avgRadial = SafeNormalize(radial0 + radial1, radial0);

            Vector3 so0 = start + (radial0 * outer);
            Vector3 so1 = start + (radial1 * outer);
            Vector3 eo0 = end + (radial0 * outer);
            Vector3 eo1 = end + (radial1 * outer);

            Vector3 si0 = start + (radial0 * inner);
            Vector3 si1 = start + (radial1 * inner);
            Vector3 ei0 = end + (radial0 * inner);
            Vector3 ei1 = end + (radial1 * inner);

            AddFace(vertices, indices, so0, eo0, eo1, so1, avgRadial, forward);
            AddFace(vertices, indices, si1, ei1, ei0, si0, -avgRadial, forward);
            AddFace(vertices, indices, eo0, ei0, ei1, eo1, forward, radial0);
            AddFace(vertices, indices, so1, si1, si0, so0, -forward, radial0);
        }
    }

    private static void AddStyledHollowCylinder(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 start,
        Vector3 end,
        float innerRadius,
        float outerRadius,
        int sides,
        ToggleTipSleeveStyle sleeveStyle,
        ToggleTipSleeveTipStyle tipStyle,
        int patternCount,
        float patternDepth,
        float tipAmount)
    {
        int sideCount = sleeveStyle switch
        {
            ToggleTipSleeveStyle.Hex => 6,
            ToggleTipSleeveStyle.Octagon => 8,
            _ => Math.Clamp(sides, 6, 96)
        };
        int ringSegments = sleeveStyle switch
        {
            ToggleTipSleeveStyle.KnurledSquare => 24,
            ToggleTipSleeveStyle.KnurledDiamond => 24,
            ToggleTipSleeveStyle.Fluted => 18,
            _ => 12
        };

        Vector3 axis = end - start;
        float axisLength = axis.Length();
        if (axisLength <= 1e-5f)
        {
            return;
        }

        float outer = MathF.Max(0.25f, outerRadius);
        float inner = Math.Clamp(innerRadius, 0.1f, outer - 0.05f);
        if (inner >= outer - 0.05f)
        {
            return;
        }

        Vector3 forward = axis / axisLength;
        Vector3 tangentA = MathF.Abs(forward.Z) < 0.999f
            ? Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ))
            : Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitX));
        Vector3 tangentB = SafeNormalize(Vector3.Cross(forward, tangentA), Vector3.UnitY);
        float step = MathF.PI * 2f / sideCount;

        for (int i = 0; i < ringSegments; i++)
        {
            float t0 = i / (float)ringSegments;
            float t1 = (i + 1) / (float)ringSegments;
            Vector3 center0 = Vector3.Lerp(start, end, t0);
            Vector3 center1 = Vector3.Lerp(start, end, t1);

            for (int j = 0; j < sideCount; j++)
            {
                float theta0 = j * step;
                float theta1 = (j + 1) * step;

                Vector3 radial0 = (MathF.Cos(theta0) * tangentA) + (MathF.Sin(theta0) * tangentB);
                Vector3 radial1 = (MathF.Cos(theta1) * tangentA) + (MathF.Sin(theta1) * tangentB);
                Vector3 radialMid = SafeNormalize(radial0 + radial1, radial0);

                float outer00 = ResolveSleeveOuterRadius(outer, sleeveStyle, tipStyle, theta0, t0, patternCount, patternDepth, tipAmount, inner);
                float outer01 = ResolveSleeveOuterRadius(outer, sleeveStyle, tipStyle, theta1, t0, patternCount, patternDepth, tipAmount, inner);
                float outer10 = ResolveSleeveOuterRadius(outer, sleeveStyle, tipStyle, theta0, t1, patternCount, patternDepth, tipAmount, inner);
                float outer11 = ResolveSleeveOuterRadius(outer, sleeveStyle, tipStyle, theta1, t1, patternCount, patternDepth, tipAmount, inner);

                Vector3 so0 = center0 + (radial0 * outer00);
                Vector3 so1 = center0 + (radial1 * outer01);
                Vector3 eo0 = center1 + (radial0 * outer10);
                Vector3 eo1 = center1 + (radial1 * outer11);
                AddFace(vertices, indices, so0, eo0, eo1, so1, radialMid, forward);

                Vector3 si0 = center0 + (radial0 * inner);
                Vector3 si1 = center0 + (radial1 * inner);
                Vector3 ei0 = center1 + (radial0 * inner);
                Vector3 ei1 = center1 + (radial1 * inner);
                AddFace(vertices, indices, si1, ei1, ei0, si0, -radialMid, forward);
            }
        }

        for (int j = 0; j < sideCount; j++)
        {
            float theta0 = j * step;
            float theta1 = (j + 1) * step;
            Vector3 radial0 = (MathF.Cos(theta0) * tangentA) + (MathF.Sin(theta0) * tangentB);
            Vector3 radial1 = (MathF.Cos(theta1) * tangentA) + (MathF.Sin(theta1) * tangentB);

            float outerStart0 = ResolveSleeveOuterRadius(outer, sleeveStyle, tipStyle, theta0, 0f, patternCount, patternDepth, tipAmount, inner);
            float outerStart1 = ResolveSleeveOuterRadius(outer, sleeveStyle, tipStyle, theta1, 0f, patternCount, patternDepth, tipAmount, inner);
            Vector3 bso0 = start + (radial0 * outerStart0);
            Vector3 bso1 = start + (radial1 * outerStart1);
            Vector3 bsi0 = start + (radial0 * inner);
            Vector3 bsi1 = start + (radial1 * inner);
            AddFace(vertices, indices, bso1, bsi1, bsi0, bso0, -forward, radial0);

            float outerEnd0 = ResolveSleeveOuterRadius(outer, sleeveStyle, tipStyle, theta0, 1f, patternCount, patternDepth, tipAmount, inner);
            float outerEnd1 = ResolveSleeveOuterRadius(outer, sleeveStyle, tipStyle, theta1, 1f, patternCount, patternDepth, tipAmount, inner);
            Vector3 tso0 = end + (radial0 * outerEnd0);
            Vector3 tso1 = end + (radial1 * outerEnd1);
            Vector3 tsi0 = end + (radial0 * inner);
            Vector3 tsi1 = end + (radial1 * inner);
            AddFace(vertices, indices, tso0, tsi0, tsi1, tso1, forward, radial0);
        }
    }

    private static float ResolveSleeveOuterRadius(
        float baseOuterRadius,
        ToggleTipSleeveStyle sleeveStyle,
        ToggleTipSleeveTipStyle tipStyle,
        float theta,
        float axialT,
        int patternCount,
        float patternDepth,
        float tipAmount,
        float innerRadius)
    {
        float availableThickness = MathF.Max(0.05f, baseOuterRadius - innerRadius);
        float clampedDepth = Math.Clamp(patternDepth, 0f, 0.9f);
        float clampedTipAmount = Math.Clamp(tipAmount, 0f, 0.95f);
        float styleOffset = ComputeSleevePatternOffset(
            sleeveStyle,
            theta,
            Math.Clamp(axialT, 0f, 1f),
            Math.Clamp(patternCount, 3, 64),
            clampedDepth,
            availableThickness);

        float normalizedDistanceToCenter = Math.Clamp(1f - (MathF.Abs((axialT * 2f) - 1f)), 0f, 1f);
        float edgeProximity = 1f - normalizedDistanceToCenter;
        float tipScale = tipStyle switch
        {
            ToggleTipSleeveTipStyle.Bevel => edgeProximity,
            ToggleTipSleeveTipStyle.Rounded => 1f - SmootherStep(normalizedDistanceToCenter),
            _ => 0f
        };
        float tipInset = clampedTipAmount * availableThickness * 0.9f * Math.Clamp(tipScale, 0f, 1f);

        float resolvedOuter = baseOuterRadius + styleOffset - tipInset;
        return MathF.Max(innerRadius + 0.05f, resolvedOuter);
    }

    private static float ComputeSleevePatternOffset(
        ToggleTipSleeveStyle sleeveStyle,
        float theta,
        float axialT,
        int patternCount,
        float patternDepth,
        float availableThickness)
    {
        float depth = Math.Clamp(patternDepth, 0f, 0.9f) * availableThickness * 0.85f;
        float angleFrequency = MathF.Max(1f, patternCount);
        float ringWave = MathF.Sin(theta * angleFrequency);
        float axialPhase = axialT * MathF.PI * 2f * MathF.Max(2f, patternCount * 0.5f);
        float axialWave = MathF.Sin(axialPhase);

        return sleeveStyle switch
        {
            ToggleTipSleeveStyle.Round => 0f,
            ToggleTipSleeveStyle.Hex => 0f,
            ToggleTipSleeveStyle.Octagon => 0f,
            ToggleTipSleeveStyle.Fluted => -depth * (0.5f + (0.5f * MathF.Max(0f, ringWave))),
            ToggleTipSleeveStyle.KnurledSquare => depth * (MathF.Max(MathF.Abs(ringWave), MathF.Abs(axialWave)) - 0.5f),
            ToggleTipSleeveStyle.KnurledDiamond => depth * (MathF.Abs(ringWave * axialWave) - 0.3f),
            _ => 0f
        };
    }

    private static void AddRoundedTaperedCylinder(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 start,
        Vector3 end,
        float startRadius,
        float endRadius,
        int sides,
        int ringSegments)
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
        int segmentCount = Math.Clamp(ringSegments, 4, 64);
        float r0 = MathF.Max(0.25f, startRadius);
        float r1 = MathF.Max(0.25f, endRadius);
        float dt = 1f / segmentCount;
        float step = MathF.PI * 2f / sideCount;
        int stride = sideCount + 1;
        uint startIndex = (uint)vertices.Count;
        var ringRadii = new float[segmentCount + 1];

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i * dt;
            float shaped = SmootherStep(t);
            ringRadii[i] = r0 + ((r1 - r0) * shaped);
        }

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i * dt;
            Vector3 center = start + (forward * (axisLength * t));
            float radius = ringRadii[i];
            float prev = i == 0 ? ringRadii[i] : ringRadii[i - 1];
            float next = i == segmentCount ? ringRadii[i] : ringRadii[i + 1];
            float drdt = (next - prev) / MathF.Max(dt, 1e-5f);
            float drds = drdt / axisLength;

            for (int j = 0; j <= sideCount; j++)
            {
                float angle = j * step;
                Vector3 radial = (MathF.Cos(angle) * tangentA) + (MathF.Sin(angle) * tangentB);
                Vector3 position = center + (radial * radius);
                Vector3 normal = SafeNormalize(radial - (forward * drds), radial);
                Vector3 tangent = SafeNormalize((-MathF.Sin(angle) * tangentA) + (MathF.Cos(angle) * tangentB), tangentA);
                vertices.Add(new MetalVertex
                {
                    Position = position,
                    Normal = normal,
                    Tangent = new Vector4(tangent, 1f)
                });
            }
        }

        for (int i = 0; i < segmentCount; i++)
        {
            for (int j = 0; j < sideCount; j++)
            {
                uint a = startIndex + (uint)(i * stride + j);
                uint b = a + 1;
                uint d = startIndex + (uint)((i + 1) * stride + j);
                uint c = d + 1;

                indices.Add(a);
                indices.Add(d);
                indices.Add(c);
                indices.Add(a);
                indices.Add(c);
                indices.Add(b);
            }
        }

        AddDisc(vertices, indices, start, r0, sideCount, -forward, tangentA, tangentB);
        AddDisc(vertices, indices, end, r1, sideCount, forward, tangentA, tangentB);
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
                indices.Add(i1);
                indices.Add(i2);
                indices.Add(i1);
                indices.Add(i3);
                indices.Add(i2);
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
                indices.Add(next);
                indices.Add(current);
            }
            else
            {
                indices.Add(centerIndex);
                indices.Add(current);
                indices.Add(next);
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

    private static int ResolveBushingSides(ToggleBushingShape shape, int fallbackSides)
    {
        return shape switch
        {
            ToggleBushingShape.Square => 4,
            ToggleBushingShape.Hex => 6,
            ToggleBushingShape.Octagon => 8,
            ToggleBushingShape.Round => Math.Clamp(fallbackSides, 12, 64),
            _ => Math.Clamp(fallbackSides, 6, 64)
        };
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

    private static float ResolveLeverAngleDeg(int stateCount, float statePosition, float maxAngleDeg)
    {
        if (stateCount <= 2)
        {
            float t = Math.Clamp(statePosition, 0f, 1f);
            return (-maxAngleDeg) + (t * (2f * maxAngleDeg));
        }

        float clamped = Math.Clamp(statePosition, 0f, 2f);
        return (-maxAngleDeg) + (clamped * maxAngleDeg);
    }

    private static float SmootherStep(float t)
    {
        float x = Math.Clamp(t, 0f, 1f);
        return x * x * x * (x * ((x * 6f) - 15f) + 10f);
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
