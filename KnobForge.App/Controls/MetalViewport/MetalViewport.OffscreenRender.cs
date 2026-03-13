using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using KnobForge.Core;
using KnobForge.Core.Scene;
using KnobForge.Rendering;
using KnobForge.Rendering.GPU;
using SkiaSharp;

namespace KnobForge.App.Controls
{
    public sealed partial class MetalViewport
    {
        public bool TryRenderFrameToBitmap(
            int widthPx,
            int heightPx,
            ViewportCameraState cameraState,
            out SKBitmap? bitmap,
            double? dynamicLightAnimationTimeSeconds = null)
        {
            bitmap = null;
            if (_isShuttingDown || !CanRenderOffscreen || _context is null || _project is null)
            {
                return false;
            }

            ModelNode? modelNode = _project.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
            if (modelNode is null)
            {
                return false;
            }

            int width = Math.Max(1, widthPx);
            int height = Math.Max(1, heightPx);

            float savedYaw = _orbitYawDeg;
            float savedPitch = _orbitPitchDeg;
            float savedZoom = _zoom;
            Vector2 savedPan = _panPx;

            IntPtr colorTexture = IntPtr.Zero;
            IntPtr postColorTexture = IntPtr.Zero;
            IntPtr depthTexture = IntPtr.Zero;
            IntPtr msaaColorTexture = IntPtr.Zero;
            IntPtr msaaDepthTexture = IntPtr.Zero;

            try
            {
                _orbitYawDeg = cameraState.OrbitYawDeg;
                _orbitPitchDeg = cameraState.OrbitPitchDeg;
                _zoom = cameraState.Zoom;
                _panPx = new Vector2(cameraState.PanPx.X, cameraState.PanPx.Y);

                RefreshMeshResources(_project, modelNode);
                CollarNode? collarNode = modelNode.Children.OfType<CollarNode>().FirstOrDefault();
                MetalPipelineManager pipelineManager = MetalPipelineManager.Instance;
                nuint mainPassSampleCount = pipelineManager.ResolveSupportedSampleCount(ViewportMsaaSampleCount);

                IntPtr colorDescriptor = ObjC.IntPtr_objc_msgSend_UInt_UInt_UInt_Bool(
                    ObjCClasses.MTLTextureDescriptor,
                    Selectors.Texture2DDescriptorWithPixelFormatWidthHeightMipmapped,
                    (nuint)MetalRendererContext.DefaultColorFormat,
                    (nuint)width,
                    (nuint)height,
                    false);
                if (colorDescriptor == IntPtr.Zero)
                {
                    return false;
                }

                ObjC.Void_objc_msgSend_UInt(colorDescriptor, Selectors.SetUsage, 4); // MTLTextureUsageRenderTarget
                ObjC.Void_objc_msgSend_UInt(colorDescriptor, Selectors.SetStorageMode, 0); // MTLStorageModeShared
                // Allow post-process sampling (bloom) from the resolved color buffer.
                ObjC.Void_objc_msgSend_UInt(colorDescriptor, Selectors.SetUsage, 5); // MTLTextureUsageShaderRead | MTLTextureUsageRenderTarget
                colorTexture = ObjC.IntPtr_objc_msgSend_IntPtr(_context.Device.Handle, Selectors.NewTextureWithDescriptor, colorDescriptor);
                if (colorTexture == IntPtr.Zero)
                {
                    return false;
                }

                IntPtr mainPassColorTexture = colorTexture;
                IntPtr mainPassDepthTexture = IntPtr.Zero;
                if (mainPassSampleCount > 1)
                {
                    IntPtr msaaColorDescriptor = ObjC.IntPtr_objc_msgSend_UInt_UInt_UInt_Bool(
                        ObjCClasses.MTLTextureDescriptor,
                        Selectors.Texture2DDescriptorWithPixelFormatWidthHeightMipmapped,
                        (nuint)MetalRendererContext.DefaultColorFormat,
                        (nuint)width,
                        (nuint)height,
                        false);
                    if (msaaColorDescriptor != IntPtr.Zero)
                    {
                        ObjC.Void_objc_msgSend_UInt(msaaColorDescriptor, Selectors.SetTextureType, MTLTextureType2DMultisample);
                        ObjC.Void_objc_msgSend_UInt(msaaColorDescriptor, Selectors.SetSampleCount, mainPassSampleCount);
                        ObjC.Void_objc_msgSend_UInt(msaaColorDescriptor, Selectors.SetUsage, 4); // MTLTextureUsageRenderTarget
                        ObjC.Void_objc_msgSend_UInt(msaaColorDescriptor, Selectors.SetStorageMode, 2); // MTLStorageModePrivate
                        msaaColorTexture = ObjC.IntPtr_objc_msgSend_IntPtr(_context.Device.Handle, Selectors.NewTextureWithDescriptor, msaaColorDescriptor);
                    }

                    IntPtr msaaDepthDescriptor = ObjC.IntPtr_objc_msgSend_UInt_UInt_UInt_Bool(
                        ObjCClasses.MTLTextureDescriptor,
                        Selectors.Texture2DDescriptorWithPixelFormatWidthHeightMipmapped,
                        DepthPixelFormat,
                        (nuint)width,
                        (nuint)height,
                        false);
                    if (msaaDepthDescriptor != IntPtr.Zero)
                    {
                        ObjC.Void_objc_msgSend_UInt(msaaDepthDescriptor, Selectors.SetTextureType, MTLTextureType2DMultisample);
                        ObjC.Void_objc_msgSend_UInt(msaaDepthDescriptor, Selectors.SetSampleCount, mainPassSampleCount);
                        ObjC.Void_objc_msgSend_UInt(msaaDepthDescriptor, Selectors.SetUsage, 4); // MTLTextureUsageRenderTarget
                        ObjC.Void_objc_msgSend_UInt(msaaDepthDescriptor, Selectors.SetStorageMode, 2); // MTLStorageModePrivate
                        msaaDepthTexture = ObjC.IntPtr_objc_msgSend_IntPtr(_context.Device.Handle, Selectors.NewTextureWithDescriptor, msaaDepthDescriptor);
                    }

                    if (msaaColorTexture != IntPtr.Zero && msaaDepthTexture != IntPtr.Zero)
                    {
                        mainPassColorTexture = msaaColorTexture;
                        mainPassDepthTexture = msaaDepthTexture;
                    }
                    else
                    {
                        mainPassSampleCount = 1;
                        if (msaaColorTexture != IntPtr.Zero)
                        {
                            ObjC.Void_objc_msgSend(msaaColorTexture, Selectors.Release);
                            msaaColorTexture = IntPtr.Zero;
                        }

                        if (msaaDepthTexture != IntPtr.Zero)
                        {
                            ObjC.Void_objc_msgSend(msaaDepthTexture, Selectors.Release);
                            msaaDepthTexture = IntPtr.Zero;
                        }
                    }
                }

                if (mainPassSampleCount <= 1)
                {
                    IntPtr depthDescriptor = ObjC.IntPtr_objc_msgSend_UInt_UInt_UInt_Bool(
                        ObjCClasses.MTLTextureDescriptor,
                        Selectors.Texture2DDescriptorWithPixelFormatWidthHeightMipmapped,
                        DepthPixelFormat,
                        (nuint)width,
                        (nuint)height,
                        false);
                    if (depthDescriptor == IntPtr.Zero)
                    {
                        return false;
                    }

                    ObjC.Void_objc_msgSend_UInt(depthDescriptor, Selectors.SetUsage, 4); // MTLTextureUsageRenderTarget
                    depthTexture = ObjC.IntPtr_objc_msgSend_IntPtr(_context.Device.Handle, Selectors.NewTextureWithDescriptor, depthDescriptor);
                    if (depthTexture == IntPtr.Zero)
                    {
                        return false;
                    }

                    mainPassDepthTexture = depthTexture;
                }

                IntPtr passDescriptor = ObjC.IntPtr_objc_msgSend(ObjCClasses.MTLRenderPassDescriptor, Selectors.RenderPassDescriptor);
                if (passDescriptor == IntPtr.Zero)
                {
                    return false;
                }

                IntPtr colorAttachments = ObjC.IntPtr_objc_msgSend(passDescriptor, Selectors.ColorAttachments);
                IntPtr colorAttachment = ObjC.IntPtr_objc_msgSend_UInt(colorAttachments, Selectors.ObjectAtIndexedSubscript, 0);
                if (colorAttachment == IntPtr.Zero)
                {
                    return false;
                }

                ObjC.Void_objc_msgSend_IntPtr(colorAttachment, Selectors.SetTexture, mainPassColorTexture);
                ObjC.Void_objc_msgSend_UInt(colorAttachment, Selectors.SetLoadAction, 2); // MTLLoadActionClear
                if (mainPassSampleCount > 1)
                {
                    ObjC.Void_objc_msgSend_IntPtr(colorAttachment, Selectors.SetResolveTexture, colorTexture);
                    ObjC.Void_objc_msgSend_UInt(colorAttachment, Selectors.SetStoreAction, MTLStoreActionMultisampleResolve);
                }
                else
                {
                    ObjC.Void_objc_msgSend_UInt(colorAttachment, Selectors.SetStoreAction, MTLStoreActionStore);
                }
                // Offscreen export should preserve transparency for PNG alpha compositing (e.g., shadows).
                ObjC.Void_objc_msgSend_MTLClearColor(colorAttachment, Selectors.SetClearColor, new MTLClearColor(0d, 0d, 0d, 0d));

                IntPtr depthAttachment = ObjC.IntPtr_objc_msgSend(passDescriptor, Selectors.DepthAttachment);
                if (depthAttachment != IntPtr.Zero && mainPassDepthTexture != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend_IntPtr(depthAttachment, Selectors.SetTexture, mainPassDepthTexture);
                    ObjC.Void_objc_msgSend_UInt(depthAttachment, Selectors.SetLoadAction, 2); // MTLLoadActionClear
                    ObjC.Void_objc_msgSend_UInt(depthAttachment, Selectors.SetStoreAction, 0); // MTLStoreActionDontCare
                    ObjC.Void_objc_msgSend_Double(depthAttachment, Selectors.SetClearDepth, 1.0);
                }

                IntPtr commandBuffer = _context.CreateCommandBuffer().Handle;
                if (commandBuffer == IntPtr.Zero)
                {
                    return false;
                }

                bool drawKnob =
                    _project.ProjectType == InteractorProjectType.RotaryKnob &&
                    IsRenderableMesh(_meshResources);
                bool drawCollar =
                    drawKnob &&
                    collarNode is { Enabled: true } &&
                    IsRenderableMesh(_collarResources);
                bool drawSliderBackplate =
                    _project.ProjectType == InteractorProjectType.ThumbSlider &&
                    IsRenderableMesh(_sliderBackplateResources);
                bool drawSliderThumb =
                    _project.ProjectType == InteractorProjectType.ThumbSlider &&
                    IsRenderableMesh(_sliderThumbResources);
                bool drawToggleBase =
                    _project.ProjectType == InteractorProjectType.FlipSwitch &&
                    IsRenderableMesh(_toggleBaseResources);
                bool drawToggleLever =
                    _project.ProjectType == InteractorProjectType.FlipSwitch &&
                    IsRenderableMesh(_toggleLeverResources);
                bool drawToggleSleeve =
                    _project.ProjectType == InteractorProjectType.FlipSwitch &&
                    IsRenderableMesh(_toggleSleeveResources);
                bool drawPushButtonBase =
                    _project.ProjectType == InteractorProjectType.PushButton &&
                    IsRenderableMesh(_pushButtonBaseResources);
                bool drawPushButtonCap =
                    _project.ProjectType == InteractorProjectType.PushButton &&
                    IsRenderableMesh(_pushButtonCapResources);
                bool drawIndicatorBase =
                    _project.ProjectType == InteractorProjectType.IndicatorLight &&
                    IsRenderableMesh(_indicatorBaseResources);
                bool drawIndicatorHousing =
                    _project.ProjectType == InteractorProjectType.IndicatorLight &&
                    IsRenderableMesh(_indicatorHousingResources);
                bool drawIndicatorLens =
                    _project.ProjectType == InteractorProjectType.IndicatorLight &&
                    IsRenderableMesh(_indicatorLensResources);
                bool drawIndicatorReflector =
                    _project.ProjectType == InteractorProjectType.IndicatorLight &&
                    IsRenderableMesh(_indicatorReflectorResources);
                bool drawIndicatorEmitters =
                    _project.ProjectType == InteractorProjectType.IndicatorLight &&
                    IsRenderableMesh(_indicatorEmitterResources);
                bool drawIndicatorAura =
                    _project.ProjectType == InteractorProjectType.IndicatorLight &&
                    IsRenderableMesh(_indicatorAuraResources);
                if (!drawKnob &&
                    !drawCollar &&
                    !drawSliderBackplate &&
                    !drawSliderThumb &&
                    !drawToggleBase &&
                    !drawToggleLever &&
                    !drawToggleSleeve &&
                    !drawPushButtonBase &&
                    !drawPushButtonCap &&
                    !drawIndicatorBase &&
                    !drawIndicatorHousing &&
                    !drawIndicatorLens &&
                    !drawIndicatorReflector &&
                    !drawIndicatorEmitters &&
                    !drawIndicatorAura)
                {
                    return false;
                }
                if (!_offscreenCollarStateLogged)
                {
                    _offscreenCollarStateLogged = true;
                    LogCollarState("offscreen", collarNode, _collarResources);
                }
                float sceneReferenceRadius = MathF.Max(1f, modelNode.Radius);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawKnob ? _meshResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawCollar ? _collarResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawSliderBackplate ? _sliderBackplateResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawSliderThumb ? _sliderThumbResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawToggleBase ? _toggleBaseResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawToggleLever ? _toggleLeverResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawToggleSleeve ? _toggleSleeveResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawPushButtonBase ? _pushButtonBaseResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawPushButtonCap ? _pushButtonCapResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawIndicatorBase ? _indicatorBaseResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawIndicatorHousing ? _indicatorHousingResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawIndicatorLens ? _indicatorLensResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawIndicatorReflector ? _indicatorReflectorResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawIndicatorEmitters ? _indicatorEmitterResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawIndicatorAura ? _indicatorAuraResources : null);
                EnsureEnvironmentMapTexture(_project);
                GpuUniforms knobUniforms = BuildUniformsForPixels(
                    _project,
                    modelNode,
                    sceneReferenceRadius,
                    width,
                    height,
                    dynamicLightAnimationTimeSeconds);
                knobUniforms.EnvironmentMapParams.Y = _environmentMapTexture != IntPtr.Zero ? 1f : 0f;
                GpuUniforms postProcessUniforms = knobUniforms;
                if (drawCollar)
                {
                    ApplyImportedCollarMirrorToEnvironmentOrientation(ref postProcessUniforms, collarNode);
                }
                MaterialNode? materialNode = modelNode?.Children.OfType<MaterialNode>().FirstOrDefault();
                AssemblyPartMaterialPalette assemblyMaterialPalette = ResolveAssemblyPartMaterialPalette(materialNode);
                GpuUniforms collarUniforms = drawCollar
                    ? BuildCollarUniforms(knobUniforms, collarNode!)
                    : default;
                GpuUniforms sliderBackplateUniforms = drawSliderBackplate
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        assemblyMaterialPalette.BaseColor,
                        assemblyMaterialPalette.BaseMetallic,
                        assemblyMaterialPalette.BaseRoughness,
                        assemblyMaterialPalette.Pearlescence)
                    : default;
                GpuUniforms sliderThumbUniforms = drawSliderThumb
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        assemblyMaterialPalette.AccentColor,
                        assemblyMaterialPalette.AccentMetallic,
                        assemblyMaterialPalette.AccentRoughness,
                        assemblyMaterialPalette.Pearlescence)
                    : default;
                Vector3 toggleSleeveColor = _project.ToggleTipSleeveColor;
                float toggleSleeveMetallic = Math.Clamp(_project.ToggleTipSleeveMetallic, 0f, 1f);
                float toggleSleeveRoughness = Math.Clamp(_project.ToggleTipSleeveRoughness, 0.04f, 1f);
                float toggleSleevePearlescence = Math.Clamp(_project.ToggleTipSleevePearlescence, 0f, 1f);
                float toggleSleeveDiffuseStrength = _project.ToggleTipSleeveDiffuseStrength;
                float toggleSleeveSpecularStrength = _project.ToggleTipSleeveSpecularStrength;
                float toggleSleeveRust = _project.ToggleTipSleeveRustAmount;
                float toggleSleeveWear = _project.ToggleTipSleeveWearAmount;
                float toggleSleeveGunk = _project.ToggleTipSleeveGunkAmount;
                float toggleBushingAnisotropyStrength = _project.ToggleUpperBushingAnisotropyStrength;
                float toggleBushingAnisotropyDensity = _project.ToggleUpperBushingAnisotropyDensity;
                float toggleBushingSurfaceCharacter = _project.ToggleUpperBushingSurfaceCharacter;
                float toggleBushingAnisotropyAngle = _project.ToggleUpperBushingAnisotropyAngleDegrees * (MathF.PI / 180f);
                GpuUniforms toggleBaseUniforms = drawToggleBase
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        assemblyMaterialPalette.BaseColor,
                        assemblyMaterialPalette.BaseMetallic,
                        assemblyMaterialPalette.BaseRoughness,
                        assemblyMaterialPalette.Pearlescence,
                        surfaceBrushStrength: toggleBushingAnisotropyStrength,
                        surfaceBrushDensity: toggleBushingAnisotropyDensity,
                        surfaceCharacter: toggleBushingSurfaceCharacter,
                        anisotropyAngleRadians: toggleBushingAnisotropyAngle)
                    : default;
                GpuUniforms toggleLeverUniforms = drawToggleLever
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        assemblyMaterialPalette.AccentColor,
                        assemblyMaterialPalette.AccentMetallic,
                        assemblyMaterialPalette.AccentRoughness,
                        assemblyMaterialPalette.Pearlescence)
                    : default;
                GpuUniforms toggleSleeveUniforms = drawToggleSleeve
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        toggleSleeveColor,
                        toggleSleeveMetallic,
                        toggleSleeveRoughness,
                        toggleSleevePearlescence,
                        toggleSleeveDiffuseStrength,
                        toggleSleeveSpecularStrength,
                        toggleSleeveRust,
                        toggleSleeveWear,
                        toggleSleeveGunk)
                    : default;
                GpuUniforms pushButtonBaseUniforms = drawPushButtonBase
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        assemblyMaterialPalette.BaseColor,
                        assemblyMaterialPalette.BaseMetallic,
                        assemblyMaterialPalette.BaseRoughness,
                        assemblyMaterialPalette.Pearlescence)
                    : default;
                GpuUniforms pushButtonCapUniforms = drawPushButtonCap
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        assemblyMaterialPalette.AccentColor,
                        assemblyMaterialPalette.AccentMetallic,
                        assemblyMaterialPalette.AccentRoughness,
                        assemblyMaterialPalette.Pearlescence)
                    : default;
                Vector3 emitterColor = new(180f / 255f, 1f, 210f / 255f);
                float primaryEmitterIntensity = 0f;
                var enabledEmitters = _project.DynamicLightRig.Sources.Where(source => source.Enabled).ToList();
                if (enabledEmitters.Count > 0)
                {
                    float weightedR = 0f;
                    float weightedG = 0f;
                    float weightedB = 0f;
                    float weightSum = 0f;
                    foreach (DynamicLightSource source in enabledEmitters)
                    {
                        float weight = MathF.Max(0.01f, source.Intensity);
                        weightedR += (source.Color.Red / 255f) * weight;
                        weightedG += (source.Color.Green / 255f) * weight;
                        weightedB += (source.Color.Blue / 255f) * weight;
                        weightSum += weight;
                        primaryEmitterIntensity = MathF.Max(primaryEmitterIntensity, source.Intensity);
                    }

                    if (weightSum > 1e-5f)
                    {
                        emitterColor = new Vector3(weightedR, weightedG, weightedB) / weightSum;
                    }
                }
                float indicatorLightEnabledFactor = _project.DynamicLightRig.Enabled ? 1f : 0f;
                float indicatorMasterBrightness = ComputeIndicatorEmissiveResponse(_project.DynamicLightRig.MasterIntensity);
                float indicatorGlow = MathF.Max(0f, _project.DynamicLightRig.EmissiveGlow);
                float emitterDrive = MathF.Max(0.35f, primaryEmitterIntensity);
                float indicatorEmissionStrength =
                    indicatorLightEnabledFactor *
                    (0.20f + (emitterDrive * indicatorMasterBrightness * 3.50f)) *
                    (0.25f + (indicatorGlow * 1.10f));
                float indicatorAuraStrength =
                    indicatorLightEnabledFactor *
                    (0.25f + (emitterDrive * indicatorMasterBrightness * 1.85f)) *
                    (0.50f + (indicatorGlow * 1.65f));
                GpuUniforms indicatorBaseUniforms = drawIndicatorBase
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        assemblyMaterialPalette.BaseColor,
                        assemblyMaterialPalette.BaseMetallic,
                        assemblyMaterialPalette.BaseRoughness,
                        assemblyMaterialPalette.Pearlescence)
                    : default;
                GpuUniforms indicatorHousingUniforms = drawIndicatorHousing
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        assemblyMaterialPalette.AccentColor,
                        assemblyMaterialPalette.AccentMetallic,
                        assemblyMaterialPalette.AccentRoughness,
                        assemblyMaterialPalette.Pearlescence,
                        surfaceBrushStrength: 0.20f,
                        surfaceBrushDensity: 110f,
                        surfaceCharacter: 0.25f)
                    : default;
                GpuUniforms indicatorLensUniforms = drawIndicatorLens
                    ? BuildIndicatorLensUniforms(
                        knobUniforms,
                        surfaceColor: Vector3.Lerp(new Vector3(0.80f, 0.82f, 0.84f), _project.IndicatorLensTint, 0.10f),
                        surfaceRoughness: _project.IndicatorLensSurfaceRoughness,
                        surfaceSpecularStrength: _project.IndicatorLensSurfaceSpecularStrength,
                        transmission: _project.IndicatorLensTransmission,
                        ior: _project.IndicatorLensIor,
                        thickness: _project.IndicatorLensThickness,
                        tint: _project.IndicatorLensTint,
                        absorption: _project.IndicatorLensAbsorption,
                        emissionColor: emitterColor,
                        emissionStrength: indicatorEmissionStrength)
                    : default;
                GpuUniforms indicatorReflectorUniforms = drawIndicatorReflector
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        new Vector3(0.82f, 0.84f, 0.88f),
                        metallic: 0.92f,
                        roughness: 0.15f,
                        pearlescence: 0f)
                    : default;
                GpuUniforms indicatorEmitterUniforms = drawIndicatorEmitters
                    ? BuildIndicatorEmitterUniforms(
                        knobUniforms,
                        emissionColor: emitterColor,
                        emissionStrength: indicatorEmissionStrength,
                        roughness: 0.10f)
                    : default;
                GpuUniforms indicatorAuraUniforms = drawIndicatorAura
                    ? BuildIndicatorEmitterUniforms(
                        knobUniforms,
                        emissionColor: emitterColor,
                        emissionStrength: indicatorAuraStrength,
                        roughness: 1.0f)
                    : default;
                EnsurePaintMaskTexture(_project);
                EnsurePaintColorTexture(_project);
                EnsurePaintMask2Texture(_project);
                ApplyPendingPaintStamps(commandBuffer);

                IntPtr encoderPtr = ObjC.IntPtr_objc_msgSend_IntPtr(commandBuffer, Selectors.RenderCommandEncoderWithDescriptor, passDescriptor);
                if (encoderPtr == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    pipelineManager.UsePipeline(new MTLRenderCommandEncoderHandle(encoderPtr), mainPassSampleCount);
                    ObjC.Void_objc_msgSend_IntPtr_UInt(
                        encoderPtr,
                        Selectors.SetVertexTextureAtIndex,
                        _paintMaskTexture,
                        1);
                    ObjC.Void_objc_msgSend_IntPtr_UInt(
                        encoderPtr,
                        Selectors.SetFragmentTextureAtIndex,
                        _spiralNormalTexture,
                        0);
                    ObjC.Void_objc_msgSend_IntPtr_UInt(
                        encoderPtr,
                        Selectors.SetFragmentTextureAtIndex,
                        _paintMaskTexture,
                        1);
                    ObjC.Void_objc_msgSend_IntPtr_UInt(
                        encoderPtr,
                        Selectors.SetFragmentTextureAtIndex,
                        _paintColorTexture,
                        2);
                    ObjC.Void_objc_msgSend_IntPtr_UInt(
                        encoderPtr,
                        Selectors.SetFragmentTextureAtIndex,
                        _environmentMapTexture,
                        3);
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
                    ObjC.Void_objc_msgSend_IntPtr_UInt(
                        encoderPtr,
                        Selectors.SetFragmentTextureAtIndex,
                        _paintMask2Texture,
                        8);
                    GetCameraBasis(out Vector3 right, out Vector3 up, out Vector3 forward);
                    bool frontFacingClockwiseBase = ResolveFrontFacingClockwise(right, up, forward);
                    bool frontFacingClockwiseKnob = _invertKnobFrontFaceWinding
                        ? !frontFacingClockwiseBase
                        : frontFacingClockwiseBase;
                    bool frontFacingClockwiseAssembly = !frontFacingClockwiseBase;
                    bool frontFacingClockwiseToggleBase = _project?.ToggleInvertBaseFrontFaceWinding == true
                        ? !frontFacingClockwiseAssembly
                        : frontFacingClockwiseAssembly;
                    bool frontFacingClockwiseToggleLever = _project?.ToggleInvertLeverFrontFaceWinding == true
                        ? !frontFacingClockwiseAssembly
                        : frontFacingClockwiseAssembly;
                    bool frontFacingClockwiseToggleSleeve = _project?.ToggleInvertLeverFrontFaceWinding == true
                        ? !frontFacingClockwiseAssembly
                        : frontFacingClockwiseAssembly;
                    bool frontFacingClockwiseCollar = frontFacingClockwiseBase;
                    if (drawCollar &&
                        IsImportedCollarPreset(collarNode) &&
                        _invertImportedStlFrontFaceWinding)
                    {
                        frontFacingClockwiseCollar = !frontFacingClockwiseCollar;
                    }
                    IReadOnlyList<ShadowPassConfig> shadowConfigs = ResolveShadowPassConfigs(_project, right, up, forward, width, height);

                    bool collarDrawExecuted = false;
                    if (drawSliderBackplate)
                    {
                        MetalPipelineManager.SetFrontFacingWinding(
                            new MTLRenderCommandEncoderHandle(encoderPtr),
                            frontFacingClockwiseAssembly);
                        ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                            encoderPtr,
                            Selectors.SetVertexBufferOffsetAtIndex,
                            _sliderBackplateResources!.VertexBuffer.Handle,
                            0,
                            0);
                        UploadUniforms(encoderPtr, sliderBackplateUniforms);
                        ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                            encoderPtr,
                            Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                            3, // MTLPrimitiveTypeTriangle
                            (nuint)_sliderBackplateResources.IndexCount,
                            (nuint)_sliderBackplateResources.IndexType,
                            _sliderBackplateResources.IndexBuffer.Handle,
                            0);
                    }

                if (drawSliderThumb)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseAssembly);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _sliderThumbResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, sliderThumbUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_sliderThumbResources.IndexCount,
                        (nuint)_sliderThumbResources.IndexType,
                        _sliderThumbResources.IndexBuffer.Handle,
                        0);
                }

                if (drawToggleBase)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseToggleBase);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _toggleBaseResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, toggleBaseUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_toggleBaseResources.IndexCount,
                        (nuint)_toggleBaseResources.IndexType,
                        _toggleBaseResources.IndexBuffer.Handle,
                        0);
                }

                if (drawToggleLever)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseToggleLever);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _toggleLeverResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, toggleLeverUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_toggleLeverResources.IndexCount,
                        (nuint)_toggleLeverResources.IndexType,
                        _toggleLeverResources.IndexBuffer.Handle,
                        0);
                }

                if (drawToggleSleeve)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseToggleSleeve);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _toggleSleeveResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, toggleSleeveUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_toggleSleeveResources.IndexCount,
                        (nuint)_toggleSleeveResources.IndexType,
                        _toggleSleeveResources.IndexBuffer.Handle,
                        0);
                }

                if (drawPushButtonBase)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseAssembly);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _pushButtonBaseResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, pushButtonBaseUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_pushButtonBaseResources.IndexCount,
                        (nuint)_pushButtonBaseResources.IndexType,
                        _pushButtonBaseResources.IndexBuffer.Handle,
                        0);
                }

                if (drawPushButtonCap)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseAssembly);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _pushButtonCapResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, pushButtonCapUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_pushButtonCapResources.IndexCount,
                        (nuint)_pushButtonCapResources.IndexType,
                        _pushButtonCapResources.IndexBuffer.Handle,
                        0);
                }

                if (drawIndicatorBase)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseAssembly);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _indicatorBaseResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, indicatorBaseUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_indicatorBaseResources.IndexCount,
                        (nuint)_indicatorBaseResources.IndexType,
                        _indicatorBaseResources.IndexBuffer.Handle,
                        0);
                }

                if (drawIndicatorReflector)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseAssembly);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _indicatorReflectorResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, indicatorReflectorUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_indicatorReflectorResources.IndexCount,
                        (nuint)_indicatorReflectorResources.IndexType,
                        _indicatorReflectorResources.IndexBuffer.Handle,
                        0);
                }

                if (drawIndicatorHousing)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseAssembly);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _indicatorHousingResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, indicatorHousingUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_indicatorHousingResources.IndexCount,
                        (nuint)_indicatorHousingResources.IndexType,
                        _indicatorHousingResources.IndexBuffer.Handle,
                        0);
                }

                if (drawIndicatorEmitters)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseAssembly);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _indicatorEmitterResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, indicatorEmitterUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_indicatorEmitterResources.IndexCount,
                        (nuint)_indicatorEmitterResources.IndexType,
                        _indicatorEmitterResources.IndexBuffer.Handle,
                        0);
                }

                if (drawIndicatorLens)
                {
                    pipelineManager.UseDepthReadOnlyState(new MTLRenderCommandEncoderHandle(encoderPtr));
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseAssembly);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _indicatorLensResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, indicatorLensUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_indicatorLensResources.IndexCount,
                        (nuint)_indicatorLensResources.IndexType,
                        _indicatorLensResources.IndexBuffer.Handle,
                        0);
                    pipelineManager.UseDepthWriteState(new MTLRenderCommandEncoderHandle(encoderPtr));
                }

                if (drawCollar)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                        new MTLRenderCommandEncoderHandle(encoderPtr),
                        frontFacingClockwiseCollar);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _collarResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, collarUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_collarResources.IndexCount,
                        (nuint)_collarResources.IndexType,
                        _collarResources.IndexBuffer.Handle,
                        0);
                    collarDrawExecuted = true;
                }

                if (collarNode is { Enabled: true } &&
                    _collarResources != null &&
                    _collarResources.VertexBuffer.Handle != IntPtr.Zero &&
                    _collarResources.IndexBuffer.Handle != IntPtr.Zero &&
                    !collarDrawExecuted)
                {
                    throw new InvalidOperationException("Collar was enabled with valid GPU resources but was not drawn in offscreen render.");
                }

                if (drawKnob)
                {
                    MetalPipelineManager.SetFrontFacingWinding(
                            new MTLRenderCommandEncoderHandle(encoderPtr),
                            frontFacingClockwiseAssembly);
                    ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                        encoderPtr,
                        Selectors.SetVertexBufferOffsetAtIndex,
                        _meshResources!.VertexBuffer.Handle,
                        0,
                        0);
                    UploadUniforms(encoderPtr, knobUniforms);
                    ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                        encoderPtr,
                        Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                        3, // MTLPrimitiveTypeTriangle
                        (nuint)_meshResources!.IndexCount,
                        (nuint)_meshResources!.IndexType,
                        _meshResources!.IndexBuffer.Handle,
                        0);
                }

                    if (shadowConfigs.Count > 0)
                    {
                        pipelineManager.UseDepthReadOnlyState(new MTLRenderCommandEncoderHandle(encoderPtr));
                        for (int shadowIndex = 0; shadowIndex < shadowConfigs.Count; shadowIndex++)
                        {
                            ShadowPassConfig shadowConfig = shadowConfigs[shadowIndex];
                            if (!shadowConfig.Enabled)
                            {
                                continue;
                            }

                        if (drawCollar)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                                new MTLRenderCommandEncoderHandle(encoderPtr),
                                frontFacingClockwiseCollar);
                            RenderShadowPasses(encoderPtr, collarUniforms, shadowConfig, _collarResources!);
                        }

                        if (drawSliderBackplate)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                            new MTLRenderCommandEncoderHandle(encoderPtr),
                            frontFacingClockwiseAssembly);
                            RenderShadowPasses(encoderPtr, sliderBackplateUniforms, shadowConfig, _sliderBackplateResources!);
                        }

                        if (drawSliderThumb)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                            new MTLRenderCommandEncoderHandle(encoderPtr),
                            frontFacingClockwiseAssembly);
                            RenderShadowPasses(encoderPtr, sliderThumbUniforms, shadowConfig, _sliderThumbResources!);
                        }

                        if (drawToggleBase)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                            new MTLRenderCommandEncoderHandle(encoderPtr),
                            frontFacingClockwiseToggleBase);
                            RenderShadowPasses(encoderPtr, toggleBaseUniforms, shadowConfig, _toggleBaseResources!);
                        }

                        if (drawToggleLever)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                            new MTLRenderCommandEncoderHandle(encoderPtr),
                            frontFacingClockwiseToggleLever);
                            RenderShadowPasses(encoderPtr, toggleLeverUniforms, shadowConfig, _toggleLeverResources!);
                        }

                        if (drawToggleSleeve)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                            new MTLRenderCommandEncoderHandle(encoderPtr),
                            frontFacingClockwiseToggleSleeve);
                            RenderShadowPasses(encoderPtr, toggleSleeveUniforms, shadowConfig, _toggleSleeveResources!);
                        }

                        if (drawPushButtonBase)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                            new MTLRenderCommandEncoderHandle(encoderPtr),
                            frontFacingClockwiseAssembly);
                            RenderShadowPasses(encoderPtr, pushButtonBaseUniforms, shadowConfig, _pushButtonBaseResources!);
                        }

                        if (drawPushButtonCap)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                                new MTLRenderCommandEncoderHandle(encoderPtr),
                                frontFacingClockwiseAssembly);
                            RenderShadowPasses(encoderPtr, pushButtonCapUniforms, shadowConfig, _pushButtonCapResources!);
                        }

                        if (drawIndicatorBase)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                                new MTLRenderCommandEncoderHandle(encoderPtr),
                                frontFacingClockwiseAssembly);
                            RenderShadowPasses(encoderPtr, indicatorBaseUniforms, shadowConfig, _indicatorBaseResources!);
                        }

                        if (drawIndicatorHousing)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                                new MTLRenderCommandEncoderHandle(encoderPtr),
                                frontFacingClockwiseAssembly);
                            RenderShadowPasses(encoderPtr, indicatorHousingUniforms, shadowConfig, _indicatorHousingResources!);
                        }

                        if (drawIndicatorLens)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                                new MTLRenderCommandEncoderHandle(encoderPtr),
                                frontFacingClockwiseAssembly);
                            RenderShadowPasses(encoderPtr, indicatorLensUniforms, shadowConfig, _indicatorLensResources!);
                        }

                        if (drawIndicatorReflector)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                                new MTLRenderCommandEncoderHandle(encoderPtr),
                                frontFacingClockwiseAssembly);
                            RenderShadowPasses(encoderPtr, indicatorReflectorUniforms, shadowConfig, _indicatorReflectorResources!);
                        }

                        if (drawIndicatorEmitters)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                                new MTLRenderCommandEncoderHandle(encoderPtr),
                                frontFacingClockwiseAssembly);
                            RenderShadowPasses(encoderPtr, indicatorEmitterUniforms, shadowConfig, _indicatorEmitterResources!);
                        }

                        if (drawKnob)
                        {
                            MetalPipelineManager.SetFrontFacingWinding(
                                new MTLRenderCommandEncoderHandle(encoderPtr),
                                frontFacingClockwiseKnob);
                            RenderShadowPasses(encoderPtr, knobUniforms, shadowConfig, _meshResources!);
                        }
                        }
                        pipelineManager.UseDepthWriteState(new MTLRenderCommandEncoderHandle(encoderPtr));
                    }

                    if (drawIndicatorAura && indicatorAuraStrength > 1e-3f)
                    {
                        pipelineManager.UseAdditivePipeline(new MTLRenderCommandEncoderHandle(encoderPtr), mainPassSampleCount);
                        MetalPipelineManager.SetBackfaceCulling(new MTLRenderCommandEncoderHandle(encoderPtr), false);
                        MetalPipelineManager.SetFrontFacingWinding(
                            new MTLRenderCommandEncoderHandle(encoderPtr),
                            frontFacingClockwiseAssembly);
                        ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
                            encoderPtr,
                            Selectors.SetVertexBufferOffsetAtIndex,
                            _indicatorAuraResources!.VertexBuffer.Handle,
                            0,
                            0);
                        UploadUniforms(encoderPtr, indicatorAuraUniforms);
                        ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
                            encoderPtr,
                            Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
                            3, // MTLPrimitiveTypeTriangle
                            (nuint)_indicatorAuraResources.IndexCount,
                            (nuint)_indicatorAuraResources.IndexType,
                            _indicatorAuraResources.IndexBuffer.Handle,
                            0);
                        MetalPipelineManager.SetBackfaceCulling(new MTLRenderCommandEncoderHandle(encoderPtr), true);
                        pipelineManager.UseDepthWriteState(new MTLRenderCommandEncoderHandle(encoderPtr));
                    }
                }
                finally
                {
                    ObjC.Void_objc_msgSend(encoderPtr, Selectors.EndEncoding);
                }

                IntPtr finalColorTexture = colorTexture;
                bool bloomEnabled = _project.EnvironmentBloomStrength > 0.001f && pipelineManager.HasBloomPipelines;
                if (bloomEnabled)
                {
                    IntPtr postDescriptor = ObjC.IntPtr_objc_msgSend_UInt_UInt_UInt_Bool(
                        ObjCClasses.MTLTextureDescriptor,
                        Selectors.Texture2DDescriptorWithPixelFormatWidthHeightMipmapped,
                        (nuint)MetalRendererContext.DefaultColorFormat,
                        (nuint)width,
                        (nuint)height,
                        false);
                    if (postDescriptor != IntPtr.Zero)
                    {
                        ObjC.Void_objc_msgSend_UInt(postDescriptor, Selectors.SetUsage, 5); // MTLTextureUsageShaderRead | MTLTextureUsageRenderTarget
                        ObjC.Void_objc_msgSend_UInt(postDescriptor, Selectors.SetStorageMode, 0); // MTLStorageModeShared
                        postColorTexture = ObjC.IntPtr_objc_msgSend_IntPtr(_context.Device.Handle, Selectors.NewTextureWithDescriptor, postDescriptor);
                    }

                    if (postColorTexture != IntPtr.Zero &&
                        RenderBloomPasses(
                            commandBuffer,
                            postColorTexture,
                            colorTexture,
                            (nuint)width,
                            (nuint)height,
                            postProcessUniforms))
                    {
                        finalColorTexture = postColorTexture;
                    }
                }

                ObjC.Void_objc_msgSend(commandBuffer, Selectors.Commit);
                ObjC.Void_objc_msgSend(commandBuffer, Selectors.WaitUntilCompleted);

                int bytesPerRow = width * 4;
                int byteCount = bytesPerRow * height;
                byte[] pixelBytes = new byte[byteCount];
                GCHandle pinned = GCHandle.Alloc(pixelBytes, GCHandleType.Pinned);
                try
                {
                    MTLRegion region = new(
                        new MTLOrigin(0, 0, 0),
                        new MTLSize((nuint)width, (nuint)height, 1));
                    ObjC.Void_objc_msgSend_IntPtr_UInt_MTLRegion_UInt(
                        finalColorTexture,
                        Selectors.GetBytesBytesPerRowFromRegionMipmapLevel,
                        pinned.AddrOfPinnedObject(),
                        (nuint)bytesPerRow,
                        region,
                        0);
                }
                finally
                {
                    pinned.Free();
                }

                // GPU blending writes premultiplied color/alpha; keep Premul to preserve soft shadow alpha on export.
                bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                IntPtr destination = bitmap.GetPixels();
                Marshal.Copy(pixelBytes, 0, destination, byteCount);
                return true;
            }
            finally
            {
                _orbitYawDeg = savedYaw;
                _orbitPitchDeg = savedPitch;
                _zoom = savedZoom;
                _panPx = savedPan;

                if (depthTexture != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend(depthTexture, Selectors.Release);
                }

                if (msaaDepthTexture != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend(msaaDepthTexture, Selectors.Release);
                }

                if (msaaColorTexture != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend(msaaColorTexture, Selectors.Release);
                }

                if (postColorTexture != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend(postColorTexture, Selectors.Release);
                }

                if (colorTexture != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend(colorTexture, Selectors.Release);
                }
            }
        }

    }
}
