using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KnobForge.Core;
using KnobForge.Core.Scene;

namespace KnobForge.Rendering.GPU;

public readonly record struct PushButtonAssemblyConfig(
    bool Enabled,
    float PlateWidth,
    float PlateHeight,
    float PlateThickness,
    float BezelRadius,
    float BezelHeight,
    float CapRadius,
    float CapHeight,
    float PressDepth);

public sealed class PushButtonPartMesh
{
    public MetalVertex[] Vertices { get; init; } = Array.Empty<MetalVertex>();

    public uint[] Indices { get; init; } = Array.Empty<uint>();

    public float ReferenceRadius { get; init; }
}

public static class PushButtonAssemblyMeshBuilder
{
    public static PushButtonAssemblyConfig ResolveConfig(KnobProject? project)
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

        return new PushButtonAssemblyConfig(
            Enabled: true,
            PlateWidth: plateWidth,
            PlateHeight: plateHeight,
            PlateThickness: plateThickness,
            BezelRadius: bezelRadius,
            BezelHeight: bezelHeight,
            CapRadius: capRadius,
            CapHeight: capHeight,
            PressDepth: pressDepth);
    }

    public static PushButtonPartMesh BuildBaseMesh(in PushButtonAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new PushButtonPartMesh();
        }

        var vertices = new List<MetalVertex>(640);
        var indices = new List<uint>(1400);
        Vector3 plateCenter = ResolvePlateCenter(config);
        AddBox(vertices, indices, config.PlateWidth, config.PlateHeight, config.PlateThickness, plateCenter);

        Vector3 bezelStart = plateCenter + new Vector3(0f, 0f, config.PlateThickness * 0.5f);
        Vector3 bezelEnd = bezelStart + new Vector3(0f, 0f, config.BezelHeight);
        AddCylinder(vertices, indices, bezelStart, bezelEnd, config.BezelRadius, 28);

        return BuildPartMesh(vertices, indices);
    }

    public static PushButtonPartMesh BuildCapMesh(in PushButtonAssemblyConfig config)
    {
        if (!config.Enabled)
        {
            return new PushButtonPartMesh();
        }

        var vertices = new List<MetalVertex>(640);
        var indices = new List<uint>(1400);

        Vector3 plateCenter = ResolvePlateCenter(config);
        float topOfBezel = plateCenter.Z + (config.PlateThickness * 0.5f) + config.BezelHeight;
        float pressedTop = topOfBezel + config.CapHeight - MathF.Max(0f, config.PressDepth);
        Vector3 capStart = new(0f, plateCenter.Y, pressedTop - config.CapHeight);
        Vector3 capEnd = new(0f, plateCenter.Y, pressedTop);
        AddCylinder(vertices, indices, capStart, capEnd, config.CapRadius, 28);

        return BuildPartMesh(vertices, indices);
    }

    private static Vector3 ResolvePlateCenter(in PushButtonAssemblyConfig config)
    {
        return new Vector3(
            0f,
            -config.PlateHeight * 0.45f,
            -(config.PlateThickness * 0.5f) - 8f);
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

        Vector3 axisDir = axis / axisLength;
        Vector3 tangent = MathF.Abs(Vector3.Dot(axisDir, Vector3.UnitZ)) > 0.95f
            ? Vector3.UnitX
            : Vector3.UnitZ;
        Vector3 basisX = SafeNormalize(Vector3.Cross(axisDir, tangent), Vector3.UnitX);
        Vector3 basisY = SafeNormalize(Vector3.Cross(axisDir, basisX), Vector3.UnitY);
        int sideCount = Math.Clamp(sides, 6, 64);
        float step = (MathF.PI * 2f) / sideCount;
        float r = MathF.Max(0.5f, radius);

        for (int i = 0; i < sideCount; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            Vector3 radial0 = (basisX * MathF.Cos(a0)) + (basisY * MathF.Sin(a0));
            Vector3 radial1 = (basisX * MathF.Cos(a1)) + (basisY * MathF.Sin(a1));
            Vector3 p0 = start + (radial0 * r);
            Vector3 p1 = start + (radial1 * r);
            Vector3 p2 = end + (radial1 * r);
            Vector3 p3 = end + (radial0 * r);
            Vector3 normal = SafeNormalize(radial0 + radial1, radial0);
            AddFace(vertices, indices, p0, p1, p2, p3, normal, axisDir);
        }

        AddDisc(vertices, indices, end, r, sideCount, axisDir);
        AddDisc(vertices, indices, start, r, sideCount, -axisDir);
    }

    private static void AddDisc(
        List<MetalVertex> vertices,
        List<uint> indices,
        Vector3 center,
        float radius,
        int sides,
        Vector3 normal)
    {
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
            Tangent = discTangent
        });

        float step = (MathF.PI * 2f) / sides;
        float r = MathF.Max(0.5f, radius);
        for (int i = 0; i < sides; i++)
        {
            float angle = i * step;
            Vector3 radial = (basisX * MathF.Cos(angle)) + (basisY * MathF.Sin(angle));
            vertices.Add(new MetalVertex
            {
                Position = center + (radial * r),
                Normal = normal,
                Tangent = discTangent
            });
        }

        for (int i = 0; i < sides; i++)
        {
            uint i0 = centerIndex;
            uint i1 = centerIndex + 1u + (uint)i;
            uint i2 = centerIndex + 1u + (uint)((i + 1) % sides);
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
