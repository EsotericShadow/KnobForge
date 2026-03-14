using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KnobForge.Core;
using KnobForge.Core.Scene;

namespace KnobForge.Rendering.GPU;

public readonly record struct IndicatorAssemblyConfig(
    bool Enabled,
    float BaseWidth,
    float BaseHeight,
    float BaseThickness,
    float HousingRadius,
    float HousingHeight,
    float LensRadius,
    float LensHeight,
    float ReflectorBaseRadius,
    float ReflectorTopRadius,
    float ReflectorDepth,
    float EmitterRadius,
    float EmitterSpread,
    float EmitterDepth,
    int EmitterCount,
    int RadialSegments,
    int LensLatitudeSegments,
    int LensLongitudeSegments);

public sealed class IndicatorPartMesh
{
    public MetalVertex[] Vertices { get; init; } = Array.Empty<MetalVertex>();

    public uint[] Indices { get; init; } = Array.Empty<uint>();

    public float ReferenceRadius { get; init; }
}

public static class IndicatorAssemblyMeshBuilder
{
    public static IndicatorAssemblyConfig ResolveConfig(KnobProject? project, RenderQualityTier quality = RenderQualityTier.Normal)
    {
        if (project is null || project.ProjectType != InteractorProjectType.IndicatorLight)
        {
            return default;
        }

        ModelNode? modelNode = project.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
        if (modelNode is null)
        {
            return default;
        }

        float knobRadius = MathF.Max(40f, modelNode.Radius);
        float knobHeight = MathF.Max(20f, modelNode.Height);

        float baseWidth = knobRadius * 1.55f;
        float baseHeight = knobRadius * 1.55f;
        float baseThickness = MathF.Max(10f, knobHeight * 0.25f);

        float housingRadius = knobRadius * 0.56f;
        float housingHeight = MathF.Max(8f, knobHeight * 0.46f);

        float lensRadius = housingRadius * 0.78f;
        float lensHeight = MathF.Max(6f, housingHeight * 0.78f);

        float reflectorBaseRadius = housingRadius * 0.70f;
        float reflectorTopRadius = reflectorBaseRadius * 0.36f;
        float reflectorDepth = MathF.Max(3f, housingHeight * 0.52f);

        float emitterRadius = MathF.Max(1.4f, lensRadius * 0.12f);
        float emitterSpread = lensRadius * 0.85f;
        float emitterDepth = -MathF.Max(0.8f, lensHeight * 0.38f);

        bool hasIndicatorOverrides =
            project.IndicatorBaseWidth > 0f ||
            project.IndicatorBaseHeight > 0f ||
            project.IndicatorBaseThickness > 0f ||
            project.IndicatorHousingRadius > 0f ||
            project.IndicatorHousingHeight > 0f ||
            project.IndicatorLensRadius > 0f ||
            project.IndicatorLensHeight > 0f ||
            project.IndicatorReflectorBaseRadius > 0f ||
            project.IndicatorReflectorTopRadius > 0f ||
            project.IndicatorReflectorDepth > 0f ||
            project.IndicatorEmitterRadius > 0f ||
            project.IndicatorEmitterSpread > 0f;

        int radialSegments = project.IndicatorRadialSegments > 0
            ? project.IndicatorRadialSegments
            : 56;
        int lensLatitudeSegments = project.IndicatorLensLatitudeSegments > 0
            ? project.IndicatorLensLatitudeSegments
            : 20;
        int lensLongitudeSegments = project.IndicatorLensLongitudeSegments > 0
            ? project.IndicatorLensLongitudeSegments
            : 40;

        radialSegments = ScaleSegments(radialSegments, quality, 8, 128);
        lensLatitudeSegments = ScaleSegments(lensLatitudeSegments, quality, 4, 96);
        lensLongitudeSegments = ScaleSegments(lensLongitudeSegments, quality, 6, 160);

        float resolvedBaseWidth = project.IndicatorBaseWidth > 0f ? project.IndicatorBaseWidth : baseWidth;
        float resolvedBaseHeight = project.IndicatorBaseHeight > 0f ? project.IndicatorBaseHeight : baseHeight;
        float resolvedBaseThickness = project.IndicatorBaseThickness > 0f ? project.IndicatorBaseThickness : baseThickness;
        float resolvedHousingRadius = project.IndicatorHousingRadius > 0f ? project.IndicatorHousingRadius : housingRadius;
        float resolvedHousingHeight = project.IndicatorHousingHeight > 0f ? project.IndicatorHousingHeight : housingHeight;
        float resolvedLensRadius = project.IndicatorLensRadius > 0f ? project.IndicatorLensRadius : lensRadius;
        float resolvedLensHeight = project.IndicatorLensHeight > 0f ? project.IndicatorLensHeight : lensHeight;
        float resolvedReflectorBaseRadius = project.IndicatorReflectorBaseRadius > 0f ? project.IndicatorReflectorBaseRadius : reflectorBaseRadius;
        float resolvedReflectorTopRadius = project.IndicatorReflectorTopRadius > 0f ? project.IndicatorReflectorTopRadius : reflectorTopRadius;
        float resolvedReflectorDepth = project.IndicatorReflectorDepth > 0f ? project.IndicatorReflectorDepth : reflectorDepth;
        float resolvedEmitterRadius = project.IndicatorEmitterRadius > 0f ? project.IndicatorEmitterRadius : emitterRadius;
        float resolvedEmitterSpread = project.IndicatorEmitterSpread > 0f ? project.IndicatorEmitterSpread : emitterSpread;
        float resolvedEmitterDepth = hasIndicatorOverrides && float.IsFinite(project.IndicatorEmitterDepth)
            ? project.IndicatorEmitterDepth
            : emitterDepth;
        int resolvedEmitterCount = project.IndicatorEmitterCount > 0 ? project.IndicatorEmitterCount : 3;

        return new IndicatorAssemblyConfig(
            Enabled: project.IndicatorAssemblyEnabled,
            BaseWidth: resolvedBaseWidth,
            BaseHeight: resolvedBaseHeight,
            BaseThickness: resolvedBaseThickness,
            HousingRadius: resolvedHousingRadius,
            HousingHeight: resolvedHousingHeight,
            LensRadius: resolvedLensRadius,
            LensHeight: resolvedLensHeight,
            ReflectorBaseRadius: resolvedReflectorBaseRadius,
            ReflectorTopRadius: resolvedReflectorTopRadius,
            ReflectorDepth: resolvedReflectorDepth,
            EmitterRadius: resolvedEmitterRadius,
            EmitterSpread: resolvedEmitterSpread,
            EmitterDepth: resolvedEmitterDepth,
            EmitterCount: resolvedEmitterCount,
            RadialSegments: radialSegments,
            LensLatitudeSegments: lensLatitudeSegments,
            LensLongitudeSegments: lensLongitudeSegments);
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

    public static IndicatorPartMesh BuildBaseMesh(in IndicatorAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new IndicatorPartMesh();
        }

        var vertices = new List<MetalVertex>(96);
        var indices = new List<uint>(192);

        Vector3 baseCenter = ResolveAssemblyCenter(config);
        AddBox(
            vertices,
            indices,
            config.BaseWidth,
            config.BaseHeight,
            config.BaseThickness,
            baseCenter);

        return BuildPartMesh(vertices, indices);
    }

    public static IndicatorPartMesh BuildHousingMesh(in IndicatorAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new IndicatorPartMesh();
        }

        var vertices = new List<MetalVertex>(520);
        var indices = new List<uint>(1280);

        Vector3 baseCenter = ResolveAssemblyCenter(config);
        float baseTop = baseCenter.Z + (config.BaseThickness * 0.5f);
        float lowerHeight = config.HousingHeight * 0.74f;
        float upperHeight = config.HousingHeight - lowerHeight;
        Vector3 lowerStart = new(0f, baseCenter.Y, baseTop);
        Vector3 lowerEnd = lowerStart + new Vector3(0f, 0f, lowerHeight);
        Vector3 upperEnd = lowerEnd + new Vector3(0f, 0f, upperHeight);

        AddCylinderFrustum(
            vertices,
            indices,
            lowerStart,
            lowerEnd,
            config.HousingRadius,
            config.HousingRadius * 0.95f,
            config.RadialSegments,
            capStart: true,
            capEnd: false);

        AddCylinderFrustum(
            vertices,
            indices,
            lowerEnd,
            upperEnd,
            config.HousingRadius * 0.95f,
            config.HousingRadius * 0.86f,
            config.RadialSegments,
            capStart: false,
            // Keep the housing top open so reflector/emitter internals are visible through the lens.
            capEnd: false);

        return BuildPartMesh(vertices, indices);
    }

    public static IndicatorPartMesh BuildLensMesh(in IndicatorAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new IndicatorPartMesh();
        }

        var vertices = new List<MetalVertex>(840);
        var indices = new List<uint>(2200);

        Vector3 baseCenter = ResolveAssemblyCenter(config);
        float baseTop = baseCenter.Z + (config.BaseThickness * 0.5f);
        float housingTop = baseTop + config.HousingHeight;
        float stemHeight = MathF.Max(1f, config.LensHeight * 0.25f);
        Vector3 stemStart = new(0f, baseCenter.Y, housingTop - stemHeight * 0.55f);
        Vector3 stemEnd = stemStart + new Vector3(0f, 0f, stemHeight);

        AddCylinderFrustum(
            vertices,
            indices,
            stemStart,
            stemEnd,
            config.LensRadius * 0.95f,
            config.LensRadius,
            config.RadialSegments,
            capStart: false,
            capEnd: false);

        float domeRadius = MathF.Max(1f, config.LensRadius);
        Vector3 domeCenter = new(0f, baseCenter.Y, stemEnd.Z + (domeRadius * 0.35f));
        AddSphere(
            vertices,
            indices,
            domeCenter,
            domeRadius,
            config.LensLatitudeSegments,
            config.LensLongitudeSegments);

        return BuildPartMesh(vertices, indices);
    }

    public static IndicatorPartMesh BuildReflectorMesh(in IndicatorAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new IndicatorPartMesh();
        }

        var vertices = new List<MetalVertex>(320);
        var indices = new List<uint>(880);

        Vector3 baseCenter = ResolveAssemblyCenter(config);
        float baseTop = baseCenter.Z + (config.BaseThickness * 0.5f);
        float reflectorBottom = baseTop + (config.HousingHeight * 0.08f);
        Vector3 start = new(0f, baseCenter.Y, reflectorBottom);
        Vector3 end = start + new Vector3(0f, 0f, config.ReflectorDepth);

        AddCylinderFrustum(
            vertices,
            indices,
            start,
            end,
            config.ReflectorBaseRadius,
            config.ReflectorTopRadius,
            config.RadialSegments,
            capStart: true,
            capEnd: true);

        return BuildPartMesh(vertices, indices);
    }

    public static IndicatorPartMesh BuildEmitterCoreMesh(in IndicatorAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new IndicatorPartMesh();
        }

        var vertices = new List<MetalVertex>(560);
        var indices = new List<uint>(1600);

        Vector3 baseCenter = ResolveAssemblyCenter(config);
        float baseTop = baseCenter.Z + (config.BaseThickness * 0.5f);
        float housingTop = baseTop + config.HousingHeight;
        float emitterZ = housingTop + config.EmitterDepth + (config.LensRadius * 0.35f);
        int emitterCount = Math.Max(1, config.EmitterCount);
        float spread = MathF.Max(0f, config.EmitterSpread);

        for (int i = 0; i < emitterCount; i++)
        {
            float t = emitterCount == 1 ? 0.5f : i / (float)(emitterCount - 1);
            float x = (t - 0.5f) * spread;
            Vector3 center = new(x, baseCenter.Y, emitterZ);
                AddSphere(
                    vertices,
                    indices,
                    center,
                    config.EmitterRadius,
                    latitudeSegments: 12,
                    longitudeSegments: 18);
        }

        return BuildPartMesh(vertices, indices);
    }

    public static IndicatorPartMesh BuildAuraMesh(in IndicatorAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new IndicatorPartMesh();
        }

        var vertices = new List<MetalVertex>(560);
        var indices = new List<uint>(1600);

        Vector3 baseCenter = ResolveAssemblyCenter(config);
        float baseTop = baseCenter.Z + (config.BaseThickness * 0.5f);
        float housingTop = baseTop + config.HousingHeight;
        float stemHeight = MathF.Max(1f, config.LensHeight * 0.25f);
        Vector3 stemStart = new(0f, baseCenter.Y, housingTop - stemHeight * 0.55f);
        Vector3 stemEnd = stemStart + new Vector3(0f, 0f, stemHeight);
        float domeRadius = MathF.Max(1f, config.LensRadius);
        Vector3 domeCenter = new(0f, baseCenter.Y, stemEnd.Z + (domeRadius * 0.35f));

        float auraRadius = MathF.Max(
            config.LensRadius * 1.35f,
            config.LensRadius + (config.EmitterSpread * 0.22f) + (config.EmitterRadius * 2.0f));

        AddSphere(
            vertices,
            indices,
            domeCenter,
            auraRadius,
            latitudeSegments: Math.Max(10, config.LensLatitudeSegments),
            longitudeSegments: Math.Max(18, config.LensLongitudeSegments));

        return BuildPartMesh(vertices, indices);
    }

    private static Vector3 ResolveAssemblyCenter(in IndicatorAssemblyConfig config)
    {
        return new Vector3(
            0f,
            -config.BaseHeight * 0.45f,
            -(config.BaseThickness * 0.5f) - 8f);
    }

    private static IndicatorPartMesh BuildPartMesh(List<MetalVertex> vertices, List<uint> indices)
    {
        float referenceRadius = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            referenceRadius = MathF.Max(referenceRadius, vertices[i].Position.Length());
        }

        return new IndicatorPartMesh
        {
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray(),
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
        bool capEnd)
    {
        Vector3 axis = end - start;
        float axisLength = axis.Length();
        if (axisLength <= 1e-5f)
        {
            return;
        }

        int radialSegments = Math.Clamp(segments, 6, 96);
        float r0 = MathF.Max(0.25f, startRadius);
        float r1 = MathF.Max(0.25f, endRadius);

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

    private static void AddSphere(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 center,
        float radius,
        int latitudeSegments,
        int longitudeSegments)
    {
        int lat = Math.Clamp(latitudeSegments, 4, 64);
        int lon = Math.Clamp(longitudeSegments, 6, 128);
        float r = MathF.Max(0.25f, radius);

        int baseIndex = vertices.Count;
        for (int y = 0; y <= lat; y++)
        {
            float v = y / (float)lat;
            float phi = v * MathF.PI;
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);

            for (int x = 0; x <= lon; x++)
            {
                float u = x / (float)lon;
                float theta = u * MathF.PI * 2f;
                float sinTheta = MathF.Sin(theta);
                float cosTheta = MathF.Cos(theta);

                Vector3 normal = new(cosTheta * sinPhi, sinTheta * sinPhi, cosPhi);
                normal = SafeNormalize(normal, Vector3.UnitZ);
                Vector3 tangentDir = SafeNormalize(new Vector3(-sinTheta, cosTheta, 0f), Vector3.UnitX);

                vertices.Add(new MetalVertex
                {
                    Position = center + (normal * r),
                    Normal = normal,
                    Tangent = new Vector4(tangentDir, 1f),
                    Texcoord = new Vector2(u, v)
                });
            }
        }

        for (int y = 0; y < lat; y++)
        {
            for (int x = 0; x < lon; x++)
            {
                int i0 = baseIndex + (y * (lon + 1)) + x;
                int i1 = i0 + 1;
                int i2 = i0 + (lon + 1);
                int i3 = i2 + 1;

                indices.Add((uint)i0);
                indices.Add((uint)i2);
                indices.Add((uint)i1);

                indices.Add((uint)i1);
                indices.Add((uint)i2);
                indices.Add((uint)i3);
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
        float r = MathF.Max(0.25f, radius);
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = i * step;
            Vector3 radial = (basisX * MathF.Cos(angle)) + (basisY * MathF.Sin(angle));
            vertices.Add(new MetalVertex
            {
                Position = center + (radial * r),
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
        uint baseIndex = (uint)vertices.Count;
        Vector3 n = SafeNormalize(normal, Vector3.UnitZ);
        Vector3 tangent = SafeNormalize(tangentDirection, Vector3.UnitX);
        Vector4 tangent4 = new(tangent, 1f);

        vertices.Add(new MetalVertex { Position = p0, Normal = n, Tangent = tangent4, Texcoord = new Vector2(0f, 0f) });
        vertices.Add(new MetalVertex { Position = p1, Normal = n, Tangent = tangent4, Texcoord = new Vector2(1f, 0f) });
        vertices.Add(new MetalVertex { Position = p2, Normal = n, Tangent = tangent4, Texcoord = new Vector2(1f, 1f) });
        vertices.Add(new MetalVertex { Position = p3, Normal = n, Tangent = tangent4, Texcoord = new Vector2(0f, 1f) });

        indices.Add(baseIndex + 0u);
        indices.Add(baseIndex + 1u);
        indices.Add(baseIndex + 2u);
        indices.Add(baseIndex + 0u);
        indices.Add(baseIndex + 2u);
        indices.Add(baseIndex + 3u);
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
        Vector3 tangent1Fallback = tangent0Vec;
        Vector3 tangent1Vec = SafeNormalize(t1, tangent1Fallback);
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

    private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        float lengthSq = value.LengthSquared();
        if (lengthSq <= 1e-8f)
        {
            return fallback;
        }

        return value / MathF.Sqrt(lengthSq);
    }
}
