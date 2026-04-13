using System;
using System.Collections.Generic;
using System.Numerics;
using KnobForge.Core;
using KnobForge.Core.Scene;
using KnobForge.Rendering.GPU;

namespace KnobForge.App.Controls
{
    public sealed partial class MetalViewport
    {
        private readonly record struct DirectShadowMapConfig(
            bool Enabled,
            int SceneLightIndex,
            Vector3 CameraPos,
            Vector3 Right,
            Vector3 Up,
            Vector3 Forward,
            float ScaleX,
            float ScaleY,
            float OffsetX,
            float OffsetY,
            float NearPlane,
            float FarPlane,
            float TexelSizeX,
            float TexelSizeY,
            float DepthBias,
            float Strength);

        private void RenderShadowPasses(
            IntPtr encoderPtr,
            in GpuUniforms baseUniforms,
            in ShadowPassConfig config,
            MetalMeshGpuResources mesh)
        {
            if (encoderPtr == IntPtr.Zero ||
                mesh.VertexBuffer.Handle == IntPtr.Zero ||
                mesh.IndexBuffer.Handle == IntPtr.Zero ||
                config.Alpha <= 1e-5f)
            {
                return;
            }

            ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                encoderPtr,
                Selectors.SetVertexBufferOffsetAtIndex,
                mesh.VertexBuffer.Handle,
                0,
                0);

            int sampleCount = Math.Clamp(config.SampleCount, 1, ShadowSampleKernel.Length);
            // Keep the projected shadow slightly behind the caster, but not so far
            // back that close-fitting collars miss the knob surface entirely.
            const float shadowDepthBiasClip = 0.00075f;

            float weightSum = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                Vector2 s = ShadowSampleKernel[i];
                float r2 = (s.X * s.X) + (s.Y * s.Y);
                weightSum += MathF.Exp(-2.5f * r2);
            }
            weightSum = MathF.Max(1e-5f, weightSum);

            for (int i = 0; i < sampleCount; i++)
            {
                Vector2 s = ShadowSampleKernel[i];
                float r2 = (s.X * s.X) + (s.Y * s.Y);
                float weight = MathF.Exp(-2.5f * r2) / weightSum;
                float jitterX = s.X * config.SoftRadiusXClip;
                float jitterY = s.Y * config.SoftRadiusYClip;

                GpuUniforms shadowUniforms = baseUniforms;
                shadowUniforms.ShadowParams = new Vector4(
                    1f,
                    config.OffsetXClip + jitterX,
                    config.OffsetYClip + jitterY,
                    config.Scale);
                float darkness = Math.Clamp(1f - config.Gray, 0f, 1f);
                shadowUniforms.ShadowColorAndOpacity = new Vector4(
                    shadowDepthBiasClip,
                    0f,
                    0f,
                    config.Alpha * darkness * weight);

                UploadUniforms(encoderPtr, shadowUniforms);
                ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                    encoderPtr,
                    Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                    3, // MTLPrimitiveTypeTriangle
                    (nuint)mesh.IndexCount,
                    (nuint)mesh.IndexType,
                    mesh.IndexBuffer.Handle,
                    0);
            }
        }

        private DirectShadowMapConfig ResolveDirectShadowMapConfig(
            KnobProject? project,
            float sceneReferenceRadius,
            Vector3 cameraRight,
            Vector3 cameraUp,
            Vector3 cameraForward,
            nuint shadowMapSize)
        {
            if (project == null ||
                !project.ShadowsEnabled ||
                project.ShadowStrength <= 1e-4f ||
                shadowMapSize == 0 ||
                !TryResolveDirectShadowLight(project, cameraRight, cameraUp, cameraForward, out int sceneLightIndex, out KnobLight light))
            {
                return default;
            }

            Vector3 sceneCenter = Vector3.Zero;
            Vector3 lightForward;
            if (light.Type == LightType.Directional)
            {
                Vector3 lightToSource = ApplyLightOrientation(GetDirectionalVector(light));
                if (lightToSource.LengthSquared() <= 1e-8f)
                {
                    return default;
                }

                lightForward = -Vector3.Normalize(lightToSource);
            }
            else
            {
                Vector3 lightPos = ApplyLightOrientation(new Vector3(light.X, light.Y, light.Z));
                Vector3 toScene = sceneCenter - lightPos;
                if (toScene.LengthSquared() <= 1e-8f)
                {
                    toScene = -Vector3.UnitZ;
                }

                lightForward = Vector3.Normalize(toScene);
            }

            if (lightForward.LengthSquared() <= 1e-8f)
            {
                return default;
            }

            Vector3 helperUp = MathF.Abs(Vector3.Dot(lightForward, Vector3.UnitZ)) > 0.92f
                ? Vector3.UnitY
                : Vector3.UnitZ;
            Vector3 lightRight = Vector3.Cross(helperUp, lightForward);
            if (lightRight.LengthSquared() <= 1e-8f)
            {
                lightRight = Vector3.UnitX;
            }
            else
            {
                lightRight = Vector3.Normalize(lightRight);
            }

            Vector3 lightUp = Vector3.Cross(lightForward, lightRight);
            if (lightUp.LengthSquared() <= 1e-8f)
            {
                lightUp = Vector3.UnitY;
            }
            else
            {
                lightUp = Vector3.Normalize(lightUp);
            }

            float orthoRadius = MathF.Max(24f, sceneReferenceRadius * 1.18f);
            float scale = 1f / MathF.Max(1f, orthoRadius);
            float padding = MathF.Max(6f, orthoRadius * 0.40f);
            float lightDistanceToCenter = orthoRadius + padding;
            Vector3 cameraPos = sceneCenter - (lightForward * lightDistanceToCenter);
            float nearPlane = MathF.Max(0.05f, padding * 0.35f);
            float farPlane = MathF.Max(nearPlane + 1f, padding + (orthoRadius * 2.35f));
            float texelSize = 1f / MathF.Max(1f, (float)shadowMapSize);
            float depthBias = MathF.Max(0.0009f, texelSize * 2.0f);

            return new DirectShadowMapConfig(
                Enabled: true,
                SceneLightIndex: sceneLightIndex,
                CameraPos: cameraPos,
                Right: lightRight,
                Up: lightUp,
                Forward: lightForward,
                ScaleX: scale,
                ScaleY: scale,
                OffsetX: -Vector3.Dot(sceneCenter, lightRight) * scale,
                OffsetY: -Vector3.Dot(sceneCenter, lightUp) * scale,
                NearPlane: nearPlane,
                FarPlane: farPlane,
                TexelSizeX: texelSize,
                TexelSizeY: texelSize,
                DepthBias: depthBias,
                Strength: Math.Clamp(project.ShadowStrength, 0f, 1f));
        }

        private bool TryResolveDirectShadowLight(
            KnobProject project,
            Vector3 cameraRight,
            Vector3 cameraUp,
            Vector3 cameraForward,
            out int sceneLightIndex,
            out KnobLight light)
        {
            sceneLightIndex = -1;
            light = null!;

            if (project.Lights.Count == 0)
            {
                return false;
            }

            project.EnsureSelection();
            if (project.ShadowMode == ShadowLightMode.Selected)
            {
                int selectedIndex = project.SelectedLightIndex;
                if ((uint)selectedIndex < (uint)project.Lights.Count)
                {
                    KnobLight selected = project.Lights[selectedIndex];
                    if (TryEvaluateShadowLight(project, selected, cameraRight, cameraUp, cameraForward, out _, out float weight, out _) &&
                        weight > 1e-6f)
                    {
                        sceneLightIndex = selectedIndex;
                        light = selected;
                        return true;
                    }
                }

                return false;
            }

            float bestWeight = 0f;
            int bestIndex = -1;
            for (int i = 0; i < project.Lights.Count; i++)
            {
                if (!TryEvaluateShadowLight(project, project.Lights[i], cameraRight, cameraUp, cameraForward, out _, out float weight, out _))
                {
                    continue;
                }

                if (weight <= bestWeight)
                {
                    continue;
                }

                bestWeight = weight;
                bestIndex = i;
            }

            if (bestIndex < 0)
            {
                return false;
            }

            sceneLightIndex = bestIndex;
            light = project.Lights[bestIndex];
            return true;
        }

        private void ApplyDirectShadowConfig(ref GpuUniforms uniforms, in DirectShadowMapConfig config)
        {
            if (!config.Enabled)
            {
                uniforms.DirectShadowCameraPosAndNear = Vector4.Zero;
                uniforms.DirectShadowRightAndScaleX = Vector4.Zero;
                uniforms.DirectShadowUpAndScaleY = Vector4.Zero;
                uniforms.DirectShadowForwardAndFar = Vector4.Zero;
                uniforms.DirectShadowProjectionOffsetsAndTexel = Vector4.Zero;
                uniforms.DirectShadowParams = Vector4.Zero;
                return;
            }

            uniforms.DirectShadowCameraPosAndNear = new Vector4(config.CameraPos, config.NearPlane);
            uniforms.DirectShadowRightAndScaleX = new Vector4(config.Right, config.ScaleX);
            uniforms.DirectShadowUpAndScaleY = new Vector4(config.Up, config.ScaleY);
            uniforms.DirectShadowForwardAndFar = new Vector4(config.Forward, config.FarPlane);
            uniforms.DirectShadowProjectionOffsetsAndTexel = new Vector4(
                config.OffsetX,
                config.OffsetY,
                config.TexelSizeX,
                config.TexelSizeY);
            uniforms.DirectShadowParams = new Vector4(
                1f,
                config.SceneLightIndex,
                config.DepthBias,
                config.Strength);
        }

        private IntPtr BeginDirectShadowMapPass(IntPtr commandBuffer, nuint shadowMapSize)
        {
            if (commandBuffer == IntPtr.Zero || shadowMapSize == 0)
            {
                return IntPtr.Zero;
            }

            EnsureDirectShadowTextures(shadowMapSize);
            if (_directShadowTexture == IntPtr.Zero || _directShadowDepthTexture == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            IntPtr passDescriptor = ObjC.IntPtr_objc_msgSend(ObjCClasses.MTLRenderPassDescriptor, Selectors.RenderPassDescriptor);
            if (passDescriptor == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            IntPtr colorAttachments = ObjC.IntPtr_objc_msgSend(passDescriptor, Selectors.ColorAttachments);
            IntPtr colorAttachment = ObjC.IntPtr_objc_msgSend_UInt(colorAttachments, Selectors.ObjectAtIndexedSubscript, 0);
            IntPtr depthAttachment = ObjC.IntPtr_objc_msgSend(passDescriptor, Selectors.DepthAttachment);
            if (colorAttachment == IntPtr.Zero || depthAttachment == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            ObjC.Void_objc_msgSend_IntPtr(colorAttachment, Selectors.SetTexture, _directShadowTexture);
            ObjC.Void_objc_msgSend_UInt(colorAttachment, Selectors.SetLoadAction, MTLLoadActionClear);
            ObjC.Void_objc_msgSend_UInt(colorAttachment, Selectors.SetStoreAction, MTLStoreActionStore);
            ObjC.Void_objc_msgSend_MTLClearColor(colorAttachment, Selectors.SetClearColor, new MTLClearColor(1d, 1d, 1d, 1d));

            ObjC.Void_objc_msgSend_IntPtr(depthAttachment, Selectors.SetTexture, _directShadowDepthTexture);
            ObjC.Void_objc_msgSend_UInt(depthAttachment, Selectors.SetLoadAction, MTLLoadActionClear);
            ObjC.Void_objc_msgSend_UInt(depthAttachment, Selectors.SetStoreAction, MTLStoreActionStore);
            ObjC.Void_objc_msgSend_Double(depthAttachment, Selectors.SetClearDepth, 1d);

            IntPtr encoderPtr = ObjC.IntPtr_objc_msgSend_IntPtr(commandBuffer, Selectors.RenderCommandEncoderWithDescriptor, passDescriptor);
            if (encoderPtr == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            MetalPipelineManager pipelineManager = MetalPipelineManager.Instance;
            pipelineManager.UsePipeline(new MTLRenderCommandEncoderHandle(encoderPtr), 1);
            ObjC.Void_objc_msgSend_IntPtr_UInt(
                encoderPtr,
                Selectors.SetVertexTextureAtIndex,
                _paintMaskTexture,
                1);
            return encoderPtr;
        }

        private void RenderDirectShadowCaster(
            IntPtr encoderPtr,
            in GpuUniforms baseUniforms,
            MetalMeshGpuResources? mesh,
            bool frontFacingClockwise)
        {
            if (encoderPtr == IntPtr.Zero || !IsRenderableMesh(mesh))
            {
                return;
            }

            MetalPipelineManager.SetFrontFacingWinding(
                new MTLRenderCommandEncoderHandle(encoderPtr),
                frontFacingClockwise);
            ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                encoderPtr,
                Selectors.SetVertexBufferOffsetAtIndex,
                mesh!.VertexBuffer.Handle,
                0,
                0);

            GpuUniforms shadowUniforms = baseUniforms;
            shadowUniforms.ShadowParams = new Vector4(2f, 0f, 0f, 0f);
            shadowUniforms.ShadowColorAndOpacity = Vector4.Zero;
            UploadUniforms(encoderPtr, shadowUniforms);
            ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                encoderPtr,
                Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                3, // MTLPrimitiveTypeTriangle
                (nuint)mesh.IndexCount,
                (nuint)mesh.IndexType,
                mesh.IndexBuffer.Handle,
                0);
        }

        private IReadOnlyList<ShadowPassConfig> ResolveShadowPassConfigs(
            KnobProject? project,
            Vector3 cameraRight,
            Vector3 cameraUp,
            Vector3 cameraForward,
            float viewportWidthPx,
            float viewportHeightPx)
        {
            _resolvedShadowPasses.Clear();
            _shadowLightContributions.Clear();
            if (project == null || !project.ShadowsEnabled)
            {
                return _resolvedShadowPasses;
            }

            // Keep Selected shadow mode stable even if UI selection briefly leaves the light list.
            project.EnsureSelection();

            switch (project.ShadowMode)
            {
                case ShadowLightMode.Selected:
                {
                    KnobLight? selected = project.SelectedLight;
                    if (selected != null &&
                        TryEvaluateShadowLight(project, selected, cameraRight, cameraUp, cameraForward, out Vector2 shadowVec, out float weight, out float planar))
                    {
                        _shadowLightContributions.Add(new ShadowLightContribution(shadowVec, weight, planar));
                    }

                    break;
                }

                case ShadowLightMode.Dominant:
                {
                    float bestWeight = 0f;
                    Vector2 bestVec = default;
                    float bestPlanar = 0f;
                    for (int i = 0; i < project.Lights.Count; i++)
                    {
                        if (!TryEvaluateShadowLight(project, project.Lights[i], cameraRight, cameraUp, cameraForward, out Vector2 shadowVec, out float weight, out float planar))
                        {
                            continue;
                        }

                        if (weight <= bestWeight)
                        {
                            continue;
                        }

                        bestWeight = weight;
                        bestVec = shadowVec;
                        bestPlanar = planar;
                    }

                    if (bestWeight > 1e-6f && bestVec.LengthSquared() > 1e-8f)
                    {
                        _shadowLightContributions.Add(new ShadowLightContribution(bestVec, bestWeight, bestPlanar));
                    }

                    break;
                }

                default:
                {
                    for (int i = 0; i < project.Lights.Count; i++)
                    {
                        if (!TryEvaluateShadowLight(project, project.Lights[i], cameraRight, cameraUp, cameraForward, out Vector2 shadowVec, out float weight, out float planar))
                        {
                            continue;
                        }

                        _shadowLightContributions.Add(new ShadowLightContribution(shadowVec, weight, planar));
                    }

                    break;
                }
            }

            if (_shadowLightContributions.Count == 0)
            {
                return _resolvedShadowPasses;
            }

            bool allowMultipleLights = project.ShadowMode == ShadowLightMode.Weighted && _shadowLightContributions.Count > 1;
            BuildShadowPassConfigs(project, viewportWidthPx, viewportHeightPx, allowMultipleLights);
            return _resolvedShadowPasses;
        }

        private void BuildShadowPassConfigs(
            KnobProject project,
            float viewportWidthPx,
            float viewportHeightPx,
            bool allowMultipleLights)
        {
            _shadowLightContributions.Sort((a, b) => b.Weight.CompareTo(a.Weight));

            int passCount;
            if (allowMultipleLights)
            {
                int desiredPassCount = 1 + (int)MathF.Round(Math.Clamp(project.ShadowQuality, 0f, 1f) * (MaxShadowPassLights - 1));
                desiredPassCount = Math.Clamp(desiredPassCount, 1, MaxShadowPassLights);
                if (_shadowLightContributions.Count >= 2)
                {
                    desiredPassCount = Math.Max(2, desiredPassCount);
                }

                passCount = Math.Min(desiredPassCount, _shadowLightContributions.Count);
            }
            else
            {
                passCount = 1;
            }

            float totalWeight = 0f;
            for (int i = 0; i < passCount; i++)
            {
                totalWeight += _shadowLightContributions[i].Weight;
            }

            totalWeight = MathF.Max(1e-6f, totalWeight);
            float baseSize = MathF.Max(1f, MathF.Min(viewportWidthPx, viewportHeightPx));
            float clipScaleX = 2f / MathF.Max(1f, viewportWidthPx);
            float clipScaleY = 2f / MathF.Max(1f, viewportHeightPx);
            float distanceUser = MathF.Max(0f, project.ShadowDistance);
            float softness = Math.Clamp(project.ShadowSoftness, 0f, 1f);
            float gray = project.ShadowGray;
            float quality = Math.Clamp(project.ShadowQuality, 0f, 1f);
            int sampleBudget = 1 + (int)MathF.Round(quality * 15f);
            int samplesPerPass = allowMultipleLights
                ? Math.Max(1, (int)MathF.Ceiling(sampleBudget / (float)passCount))
                : sampleBudget;

            float totalPowerNorm = Math.Clamp(totalWeight / 3f, 0.15f, 1.35f);
            float alphaBudget = Math.Clamp((0.08f + (0.26f * totalPowerNorm)) * project.ShadowStrength, 0f, 0.85f);

            for (int i = 0; i < passCount; i++)
            {
                ShadowLightContribution contribution = _shadowLightContributions[i];
                if (contribution.ShadowVec.LengthSquared() <= 1e-8f || contribution.Weight <= 1e-6f)
                {
                    continue;
                }

                Vector2 screenDirection = Vector2.Normalize(contribution.ShadowVec);
                float planar = contribution.Planar;
                float powerNorm = Math.Clamp(contribution.Weight / 3f, 0.15f, 1.35f);
                float weightRatio = Math.Clamp(contribution.Weight / totalWeight, 0f, 1f);
                float spread = allowMultipleLights ? 1f - weightRatio : 0f;
                float offsetMagPx = baseSize * (0.010f + (0.032f * planar)) * powerNorm * distanceUser;
                float scale = project.ShadowScale * (1.0f + (0.035f * planar));
                float softRadiusPx = baseSize * (0.003f + (0.026f * softness * (0.45f + (0.55f * spread))));
                float alpha = allowMultipleLights ? alphaBudget * weightRatio : alphaBudget;
                if (alpha <= 1e-5f)
                {
                    continue;
                }

                float offsetXClip = screenDirection.X * offsetMagPx * clipScaleX;
                float offsetYClip = -screenDirection.Y * offsetMagPx * clipScaleY;
                float softRadiusXClip = softRadiusPx * clipScaleX;
                float softRadiusYClip = softRadiusPx * clipScaleY;

                _resolvedShadowPasses.Add(new ShadowPassConfig(
                    true,
                    offsetXClip,
                    offsetYClip,
                    MathF.Max(0.5f, scale),
                    alpha,
                    gray,
                    softRadiusXClip,
                    softRadiusYClip,
                    samplesPerPass));
            }
        }

        private bool TryEvaluateShadowLight(
            KnobProject project,
            KnobLight light,
            Vector3 cameraRight,
            Vector3 cameraUp,
            Vector3 cameraForward,
            out Vector2 shadowVec,
            out float weight,
            out float planar)
        {
            shadowVec = default;
            weight = 0f;
            planar = 0f;

            float intensity = MathF.Max(0f, light.Intensity);
            if (intensity <= 1e-5f)
            {
                return false;
            }

            Vector3 dir;
            if (light.Type == LightType.Directional)
            {
                dir = ApplyLightOrientation(GetDirectionalVector(light));
                if (dir.LengthSquared() <= 1e-8f)
                {
                    return false;
                }

                dir = Vector3.Normalize(dir);
            }
            else
            {
                Vector3 lightPos = ApplyLightOrientation(new Vector3(light.X, light.Y, light.Z));
                if (lightPos.LengthSquared() <= 1e-8f)
                {
                    return false;
                }

                dir = Vector3.Normalize(lightPos);
            }

            float sceneRadius = 220f;
            sceneRadius = IncludeReferenceRadius(sceneRadius, _meshResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _collarResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _sliderBackplateResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _sliderThumbResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _toggleBaseResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _toggleLeverResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _toggleSleeveResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _pushButtonBaseResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _pushButtonCapResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _pushButtonSkirtResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _indicatorBaseResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _indicatorHousingResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _indicatorLensResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _indicatorReflectorResources);
            sceneRadius = IncludeReferenceRadius(sceneRadius, _indicatorEmitterResources);
            float distNorm = light.Type == LightType.Point
                ? MathF.Max(0.2f, new Vector3(light.X, light.Y, light.Z).Length() / MathF.Max(1f, sceneRadius * 2f))
                : 1f;
            float attenuation = light.Type == LightType.Point
                ? 1f / (1f + (MathF.Max(0f, light.Falloff) * distNorm * distNorm))
                : 1f;

            float luminance = ((0.2126f * light.Color.Red) + (0.7152f * light.Color.Green) + (0.0722f * light.Color.Blue)) / 255f;
            float diffuse = MathF.Max(0f, light.DiffuseBoost);
            float diffuseTerm = 0.35f + (0.65f * MathF.Pow(diffuse, MathF.Max(0f, project.ShadowDiffuseInfluence)));
            weight = intensity * attenuation * diffuseTerm * (0.35f + (0.65f * luminance));
            if (weight <= 1e-6f)
            {
                return false;
            }

            float sx = Vector3.Dot(dir, cameraRight);
            float sy = -Vector3.Dot(dir, cameraUp);
            Vector2 projected = new(sx, sy);
            float projectedLen = projected.Length();
            if (projectedLen <= 1e-6f)
            {
                return false;
            }

            float parallaxScale = light.Type == LightType.Point
                ? Math.Clamp(1.15f / MathF.Max(0.35f, distNorm), 0.45f, 1.75f)
                : 1f;
            shadowVec = -projected * parallaxScale;
            float viewIncidence = MathF.Abs(Vector3.Dot(dir, cameraForward));
            planar = MathF.Sqrt(MathF.Max(0f, 1f - (viewIncidence * viewIncidence)));
            return true;
        }

    }
}
