using System;
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
    }
}
