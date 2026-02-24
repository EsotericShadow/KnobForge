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
        public bool TryRenderFrameToBitmap(int widthPx, int heightPx, ViewportCameraState cameraState, out SKBitmap? bitmap)
        {
            bitmap = null;
            if (!CanRenderOffscreen || _context is null || _project is null)
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
            IntPtr depthTexture = IntPtr.Zero;

            try
            {
                _orbitYawDeg = cameraState.OrbitYawDeg;
                _orbitPitchDeg = cameraState.OrbitPitchDeg;
                _zoom = cameraState.Zoom;
                _panPx = new Vector2(cameraState.PanPx.X, cameraState.PanPx.Y);

                RefreshMeshResources(_project, modelNode);
                CollarNode? collarNode = modelNode.Children.OfType<CollarNode>().FirstOrDefault();

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
                colorTexture = ObjC.IntPtr_objc_msgSend_IntPtr(_context.Device.Handle, Selectors.NewTextureWithDescriptor, colorDescriptor);
                if (colorTexture == IntPtr.Zero)
                {
                    return false;
                }

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

                ObjC.Void_objc_msgSend_IntPtr(colorAttachment, Selectors.SetTexture, colorTexture);
                ObjC.Void_objc_msgSend_UInt(colorAttachment, Selectors.SetLoadAction, 2); // MTLLoadActionClear
                ObjC.Void_objc_msgSend_UInt(colorAttachment, Selectors.SetStoreAction, 1); // MTLStoreActionStore
                // Offscreen export should preserve transparency for PNG alpha compositing (e.g., shadows).
                ObjC.Void_objc_msgSend_MTLClearColor(colorAttachment, Selectors.SetClearColor, new MTLClearColor(0d, 0d, 0d, 0d));

                IntPtr depthAttachment = ObjC.IntPtr_objc_msgSend(passDescriptor, Selectors.DepthAttachment);
                if (depthAttachment != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend_IntPtr(depthAttachment, Selectors.SetTexture, depthTexture);
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
                if (!drawKnob &&
                    !drawCollar &&
                    !drawSliderBackplate &&
                    !drawSliderThumb &&
                    !drawToggleBase &&
                    !drawToggleLever &&
                    !drawToggleSleeve &&
                    !drawPushButtonBase &&
                    !drawPushButtonCap)
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
                GpuUniforms knobUniforms = BuildUniformsForPixels(_project, modelNode, sceneReferenceRadius, width, height);
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
                GpuUniforms toggleBaseUniforms = drawToggleBase
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        assemblyMaterialPalette.BaseColor,
                        assemblyMaterialPalette.BaseMetallic,
                        assemblyMaterialPalette.BaseRoughness,
                        assemblyMaterialPalette.Pearlescence,
                        surfaceBrushStrength: 0.72f,
                        surfaceBrushDensity: 82f,
                        surfaceCharacter: 0.58f,
                        anisotropyAngleRadians: 0f)
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
                EnsurePaintMaskTexture(_project);
                EnsurePaintColorTexture(_project);
                ApplyPendingPaintStamps(commandBuffer);

                IntPtr encoderPtr = ObjC.IntPtr_objc_msgSend_IntPtr(commandBuffer, Selectors.RenderCommandEncoderWithDescriptor, passDescriptor);
                if (encoderPtr == IntPtr.Zero)
                {
                    return false;
                }

                MetalPipelineManager pipelineManager = MetalPipelineManager.Instance;
                pipelineManager.UsePipeline(new MTLRenderCommandEncoderHandle(encoderPtr));
                if (drawCollar && _invertImportedCollarOrbit && IsImportedCollarPreset(collarNode))
                {
                    collarUniforms.ModelRotationCosSin.Y = -collarUniforms.ModelRotationCosSin.Y;
                }
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

                        // Skip lever shadow projection for export as well to avoid the same
                        // self-shadowing artifact seen in the interactive viewport.

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

                ObjC.Void_objc_msgSend(encoderPtr, Selectors.EndEncoding);
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
                        colorTexture,
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

                if (colorTexture != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend(colorTexture, Selectors.Release);
                }
            }
        }

    }
}
