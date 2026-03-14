using System;
using System.Collections.Generic;
using System.Numerics;
using KnobForge.Core.Scene;
using KnobForge.Rendering.GPU;

namespace KnobForge.App.Controls
{
    public sealed partial class MetalViewport
    {
        private void BindMaterialTextures(IntPtr encoderPtr, MaterialNode? materialNode)
        {
            ObjC.Void_objc_msgSend_IntPtr_UInt(
                encoderPtr,
                Selectors.SetFragmentTextureAtIndex,
                ResolveMaterialTexture(materialNode, TextureMapType.Albedo),
                4);
            ObjC.Void_objc_msgSend_IntPtr_UInt(
                encoderPtr,
                Selectors.SetFragmentTextureAtIndex,
                ResolveMaterialTexture(materialNode, TextureMapType.Normal),
                5);
            ObjC.Void_objc_msgSend_IntPtr_UInt(
                encoderPtr,
                Selectors.SetFragmentTextureAtIndex,
                ResolveMaterialTexture(materialNode, TextureMapType.Roughness),
                6);
            ObjC.Void_objc_msgSend_IntPtr_UInt(
                encoderPtr,
                Selectors.SetFragmentTextureAtIndex,
                ResolveMaterialTexture(materialNode, TextureMapType.Metallic),
                7);
        }

        private static void ApplyMaterialToUniforms(ref GpuUniforms uniforms, MaterialNode? materialNode, bool allowPartMaterials)
        {
            if (materialNode is null)
            {
                return;
            }

            Vector3 baseColor = materialNode.BaseColor;
            float metallic = Math.Clamp(materialNode.Metallic, 0f, 1f);
            float roughness = Math.Clamp(materialNode.Roughness, 0.04f, 1f);
            uniforms.MaterialBaseColorAndMetallic = new Vector4(baseColor, metallic);
            uniforms.MaterialRoughnessDiffuseSpecMode.X = roughness;
            uniforms.MaterialRoughnessDiffuseSpecMode.Y = MathF.Max(0f, materialNode.DiffuseStrength);
            uniforms.MaterialRoughnessDiffuseSpecMode.Z = MathF.Max(0f, materialNode.SpecularStrength);
            if (allowPartMaterials && materialNode.PartMaterialsEnabled)
            {
                uniforms.MaterialPartTopColorAndMetallic = new Vector4(materialNode.TopBaseColor, materialNode.TopMetallic);
                uniforms.MaterialPartBevelColorAndMetallic = new Vector4(materialNode.BevelBaseColor, materialNode.BevelMetallic);
                uniforms.MaterialPartSideColorAndMetallic = new Vector4(materialNode.SideBaseColor, materialNode.SideMetallic);
                uniforms.MaterialPartRoughnessAndEnable = new Vector4(
                    materialNode.TopRoughness,
                    materialNode.BevelRoughness,
                    materialNode.SideRoughness,
                    1f);
            }
            else
            {
                uniforms.MaterialPartTopColorAndMetallic = uniforms.MaterialBaseColorAndMetallic;
                uniforms.MaterialPartBevelColorAndMetallic = uniforms.MaterialBaseColorAndMetallic;
                uniforms.MaterialPartSideColorAndMetallic = uniforms.MaterialBaseColorAndMetallic;
                uniforms.MaterialPartRoughnessAndEnable = new Vector4(roughness, roughness, roughness, 0f);
            }

            uniforms.MaterialSurfaceBrushParams = new Vector4(
                Math.Clamp(materialNode.RadialBrushStrength, 0f, 1f),
                MathF.Max(1f, materialNode.RadialBrushDensity),
                Math.Clamp(materialNode.SurfaceCharacter, 0f, 1f),
                uniforms.MaterialSurfaceBrushParams.W);
            uniforms.WeatherParams = new Vector4(
                Math.Clamp(materialNode.RustAmount, 0f, 1f),
                Math.Clamp(materialNode.WearAmount, 0f, 1f),
                Math.Clamp(materialNode.GunkAmount, 0f, 1f),
                uniforms.WeatherParams.W);
            uniforms.IndicatorParams1.W = Math.Clamp(materialNode.Pearlescence, 0f, 1f);
            uniforms.TextureMapFlags = new Vector4(
                materialNode.HasAlbedoMap ? 1f : 0f,
                materialNode.HasNormalMap ? 1f : 0f,
                materialNode.HasRoughnessMap ? 1f : 0f,
                materialNode.HasMetallicMap ? 1f : 0f);
            uniforms.TextureMapParams = new Vector4(
                Math.Clamp(materialNode.NormalMapStrength, 0f, 2f),
                0f,
                0f,
                0f);
        }

        private void DrawMeshWithMaterials(
            IntPtr encoderPtr,
            MetalMeshGpuResources resources,
            in GpuUniforms baseUniforms,
            ModelNode? modelNode,
            bool frontFacingClockwise,
            bool allowPartMaterials)
        {
            MetalPipelineManager.SetFrontFacingWinding(
                new MTLRenderCommandEncoderHandle(encoderPtr),
                frontFacingClockwise);
            ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                encoderPtr,
                Selectors.SetVertexBufferOffsetAtIndex,
                resources.VertexBuffer.Handle,
                0,
                0);

            SubMesh[] subMeshes = resources.SubMeshes.Length > 0
                ? resources.SubMeshes
                : new[]
                {
                    new SubMesh
                    {
                        IndexOffset = 0,
                        IndexCount = resources.IndexCount,
                        MaterialIndex = 0
                    }
                };

            List<MergedSubMeshDraw>? mergedSubmeshes = MergeSubmeshesByMaterial(resources, modelNode, allowPartMaterials);
            if (mergedSubmeshes is { Count: > 0 } && _context != null)
            {
                foreach (MergedSubMeshDraw mergedSubmesh in mergedSubmeshes)
                {
                    if (mergedSubmesh.MergedIndices.Length == 0)
                    {
                        continue;
                    }

                    GpuUniforms subUniforms = baseUniforms;
                    ApplyMaterialToUniforms(ref subUniforms, mergedSubmesh.Material, allowPartMaterials);
                    BindMaterialTextures(encoderPtr, mergedSubmesh.Material);
                    UploadUniforms(encoderPtr, subUniforms);

                    using IMTLBuffer mergedIndexBuffer = _context.CreateBuffer<uint>(mergedSubmesh.MergedIndices);
                    if (mergedIndexBuffer.Handle == IntPtr.Zero)
                    {
                        continue;
                    }

                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3,
                        (nuint)mergedSubmesh.MergedIndices.Length,
                        (nuint)MTLIndexType.UInt32,
                        mergedIndexBuffer.Handle,
                        0);
                }

                return;
            }

            for (int i = 0; i < subMeshes.Length; i++)
            {
                SubMesh subMesh = subMeshes[i];
                if (subMesh.IndexCount <= 0)
                {
                    continue;
                }

                MaterialNode? materialNode = modelNode?.GetMaterialByIndex(subMesh.MaterialIndex);
                GpuUniforms subUniforms = baseUniforms;
                ApplyMaterialToUniforms(ref subUniforms, materialNode, allowPartMaterials);
                BindMaterialTextures(encoderPtr, materialNode);
                UploadUniforms(encoderPtr, subUniforms);

                ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                    encoderPtr,
                    Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                    3,
                    (nuint)subMesh.IndexCount,
                    (nuint)resources.IndexType,
                    resources.IndexBuffer.Handle,
                    (nuint)(Math.Max(0, subMesh.IndexOffset) * sizeof(uint)));
            }
        }

        private List<MergedSubMeshDraw>? MergeSubmeshesByMaterial(
            MetalMeshGpuResources resources,
            ModelNode? modelNode,
            bool allowPartMaterials)
        {
            if (resources.SubMeshes.Length <= 1 || resources.Indices.Length == 0)
            {
                return null;
            }

            var lookup = new Dictionary<MaterialIdentity, int>();
            var groups = new List<MergedSubMeshBuilder>();
            bool mergedAnything = false;

            foreach (SubMesh subMesh in resources.SubMeshes)
            {
                if (subMesh.IndexCount <= 0)
                {
                    continue;
                }

                MaterialNode? materialNode = modelNode?.GetMaterialByIndex(subMesh.MaterialIndex);
                MaterialIdentity identity = BuildMaterialIdentity(materialNode, allowPartMaterials);
                if (!lookup.TryGetValue(identity, out int groupIndex))
                {
                    groupIndex = groups.Count;
                    lookup.Add(identity, groupIndex);
                    groups.Add(new MergedSubMeshBuilder(materialNode));
                }
                else
                {
                    mergedAnything = true;
                }

                int clampedOffset = Math.Clamp(subMesh.IndexOffset, 0, resources.Indices.Length);
                int clampedCount = Math.Clamp(subMesh.IndexCount, 0, resources.Indices.Length - clampedOffset);
                if (clampedCount <= 0)
                {
                    continue;
                }

                groups[groupIndex].MergedIndices.AddRange(resources.Indices.AsSpan(clampedOffset, clampedCount).ToArray());
            }

            if (!mergedAnything)
            {
                return null;
            }

            var mergedDraws = new List<MergedSubMeshDraw>(groups.Count);
            foreach (MergedSubMeshBuilder group in groups)
            {
                if (group.MergedIndices.Count <= 0)
                {
                    continue;
                }

                mergedDraws.Add(new MergedSubMeshDraw(group.Material, group.MergedIndices.ToArray()));
            }

            return mergedDraws;
        }

        private static MaterialIdentity BuildMaterialIdentity(MaterialNode? materialNode, bool allowPartMaterials)
        {
            if (materialNode == null)
            {
                return default;
            }

            bool partMaterialsEnabled = allowPartMaterials && materialNode.PartMaterialsEnabled;
            return new MaterialIdentity(
                HasMaterial: true,
                BaseColorX: QuantizeMaterialValue(materialNode.BaseColor.X),
                BaseColorY: QuantizeMaterialValue(materialNode.BaseColor.Y),
                BaseColorZ: QuantizeMaterialValue(materialNode.BaseColor.Z),
                Metallic: QuantizeMaterialValue(materialNode.Metallic),
                Roughness: QuantizeMaterialValue(materialNode.Roughness),
                DiffuseStrength: QuantizeMaterialValue(materialNode.DiffuseStrength),
                SpecularStrength: QuantizeMaterialValue(materialNode.SpecularStrength),
                TopColorX: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.TopBaseColor.X) : 0f,
                TopColorY: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.TopBaseColor.Y) : 0f,
                TopColorZ: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.TopBaseColor.Z) : 0f,
                TopMetallic: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.TopMetallic) : 0f,
                TopRoughness: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.TopRoughness) : 0f,
                BevelColorX: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.BevelBaseColor.X) : 0f,
                BevelColorY: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.BevelBaseColor.Y) : 0f,
                BevelColorZ: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.BevelBaseColor.Z) : 0f,
                BevelMetallic: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.BevelMetallic) : 0f,
                BevelRoughness: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.BevelRoughness) : 0f,
                SideColorX: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.SideBaseColor.X) : 0f,
                SideColorY: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.SideBaseColor.Y) : 0f,
                SideColorZ: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.SideBaseColor.Z) : 0f,
                SideMetallic: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.SideMetallic) : 0f,
                SideRoughness: partMaterialsEnabled ? QuantizeMaterialValue(materialNode.SideRoughness) : 0f,
                PartMaterialsEnabled: partMaterialsEnabled ? 1 : 0,
                RadialBrushStrength: QuantizeMaterialValue(materialNode.RadialBrushStrength),
                RadialBrushDensity: QuantizeMaterialValue(materialNode.RadialBrushDensity),
                SurfaceCharacter: QuantizeMaterialValue(materialNode.SurfaceCharacter),
                RustAmount: QuantizeMaterialValue(materialNode.RustAmount),
                WearAmount: QuantizeMaterialValue(materialNode.WearAmount),
                GunkAmount: QuantizeMaterialValue(materialNode.GunkAmount),
                Pearlescence: QuantizeMaterialValue(materialNode.Pearlescence),
                NormalMapStrength: QuantizeMaterialValue(materialNode.NormalMapStrength),
                AlbedoMapPath: materialNode.AlbedoMapPath ?? string.Empty,
                NormalMapPath: materialNode.NormalMapPath ?? string.Empty,
                RoughnessMapPath: materialNode.RoughnessMapPath ?? string.Empty,
                MetallicMapPath: materialNode.MetallicMapPath ?? string.Empty);
        }

        private static float QuantizeMaterialValue(float value) => MathF.Round(value, 4);

        private readonly record struct MaterialIdentity(
            bool HasMaterial,
            float BaseColorX,
            float BaseColorY,
            float BaseColorZ,
            float Metallic,
            float Roughness,
            float DiffuseStrength,
            float SpecularStrength,
            float TopColorX,
            float TopColorY,
            float TopColorZ,
            float TopMetallic,
            float TopRoughness,
            float BevelColorX,
            float BevelColorY,
            float BevelColorZ,
            float BevelMetallic,
            float BevelRoughness,
            float SideColorX,
            float SideColorY,
            float SideColorZ,
            float SideMetallic,
            float SideRoughness,
            int PartMaterialsEnabled,
            float RadialBrushStrength,
            float RadialBrushDensity,
            float SurfaceCharacter,
            float RustAmount,
            float WearAmount,
            float GunkAmount,
            float Pearlescence,
            float NormalMapStrength,
            string AlbedoMapPath,
            string NormalMapPath,
            string RoughnessMapPath,
            string MetallicMapPath);

        private sealed class MergedSubMeshBuilder
        {
            public MergedSubMeshBuilder(MaterialNode? material)
            {
                Material = material;
            }

            public MaterialNode? Material { get; }

            public List<uint> MergedIndices { get; } = new();
        }

        private readonly record struct MergedSubMeshDraw(MaterialNode? Material, uint[] MergedIndices);
    }
}
