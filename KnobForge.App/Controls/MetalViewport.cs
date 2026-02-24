using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using KnobForge.Core;
using KnobForge.Core.Scene;
using KnobForge.Rendering;
using KnobForge.Rendering.GPU;
using SkiaSharp;

namespace KnobForge.App.Controls
{
    public readonly struct MTLRenderCommandEncoder
    {
        public IntPtr Handle { get; }

        internal MTLRenderCommandEncoder(IntPtr handle)
        {
            Handle = handle;
        }
    }

    public sealed partial class MetalViewport : NativeControlHost
    {
        private static readonly JsonSerializerOptions ProjectStateJsonOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public enum PaintHitMode
        {
            Idle,
            MeshHit,
            Fallback
        }

        public readonly struct PaintHudSnapshot
        {
            public PaintHudSnapshot(
                bool paintEnabled,
                bool isPainting,
                PaintChannel channel,
                PaintBrushType brushType,
                ScratchAbrasionType abrasionType,
                float activeSizePx,
                float activeOpacity,
                float liveScratchDepth,
                bool optionDepthRampActive,
                PaintHitMode hitMode)
            {
                PaintEnabled = paintEnabled;
                IsPainting = isPainting;
                Channel = channel;
                BrushType = brushType;
                AbrasionType = abrasionType;
                ActiveSizePx = activeSizePx;
                ActiveOpacity = activeOpacity;
                LiveScratchDepth = liveScratchDepth;
                OptionDepthRampActive = optionDepthRampActive;
                HitMode = hitMode;
            }

            public bool PaintEnabled { get; }
            public bool IsPainting { get; }
            public PaintChannel Channel { get; }
            public PaintBrushType BrushType { get; }
            public ScratchAbrasionType AbrasionType { get; }
            public float ActiveSizePx { get; }
            public float ActiveOpacity { get; }
            public float LiveScratchDepth { get; }
            public bool OptionDepthRampActive { get; }
            public PaintHitMode HitMode { get; }
        }

        public readonly struct RuntimeDiagnosticsSnapshot
        {
            public RuntimeDiagnosticsSnapshot(
                double cpuFrameMs,
                double smoothedCpuFrameMs,
                double smoothedFps,
                double paintStampCpuMs,
                int pendingPaintStamps,
                PaintHitMode lastHitMode,
                bool isPainting)
            {
                CpuFrameMs = cpuFrameMs;
                SmoothedCpuFrameMs = smoothedCpuFrameMs;
                SmoothedFps = smoothedFps;
                PaintStampCpuMs = paintStampCpuMs;
                PendingPaintStamps = pendingPaintStamps;
                LastHitMode = lastHitMode;
                IsPainting = isPainting;
            }

            public double CpuFrameMs { get; }
            public double SmoothedCpuFrameMs { get; }
            public double SmoothedFps { get; }
            public double PaintStampCpuMs { get; }
            public int PendingPaintStamps { get; }
            public PaintHitMode LastHitMode { get; }
            public bool IsPainting { get; }
        }

        public readonly struct LightGizmoSnapshot
        {
            public LightGizmoSnapshot(
                Point positionDip,
                Point originDip,
                Point directionTipDip,
                bool hasDirectionTip,
                byte colorR,
                byte colorG,
                byte colorB,
                bool isSelected,
                double radiusDip,
                double selectedRingRadiusDip,
                double directionTipRadiusDip,
                byte fillAlpha,
                byte lineAlpha)
            {
                PositionDip = positionDip;
                OriginDip = originDip;
                DirectionTipDip = directionTipDip;
                HasDirectionTip = hasDirectionTip;
                ColorR = colorR;
                ColorG = colorG;
                ColorB = colorB;
                IsSelected = isSelected;
                RadiusDip = radiusDip;
                SelectedRingRadiusDip = selectedRingRadiusDip;
                DirectionTipRadiusDip = directionTipRadiusDip;
                FillAlpha = fillAlpha;
                LineAlpha = lineAlpha;
            }

            public Point PositionDip { get; }
            public Point OriginDip { get; }
            public Point DirectionTipDip { get; }
            public bool HasDirectionTip { get; }
            public byte ColorR { get; }
            public byte ColorG { get; }
            public byte ColorB { get; }
            public bool IsSelected { get; }
            public double RadiusDip { get; }
            public double SelectedRingRadiusDip { get; }
            public double DirectionTipRadiusDip { get; }
            public byte FillAlpha { get; }
            public byte LineAlpha { get; }
        }

        public readonly struct PaintLayerInfo
        {
            public PaintLayerInfo(int index, string name, bool isActive, bool isFocused)
            {
                Index = index;
                Name = name ?? string.Empty;
                IsActive = isActive;
                IsFocused = isFocused;
            }

            public int Index { get; }
            public string Name { get; }
            public bool IsActive { get; }
            public bool IsFocused { get; }
        }

        private sealed class PaintLayerState
        {
            public PaintLayerState(string name)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Layer" : name.Trim();
            }

            public string Name { get; set; }
        }

        private const int MaxGpuLights = 8;
        private const nuint DepthPixelFormat = 252; // MTLPixelFormatDepth32Float
        private const nuint NormalMapPixelFormat = 70; // MTLPixelFormatRGBA8Unorm
        private const nuint PaintMaskPixelFormat = 70; // MTLPixelFormatRGBA8Unorm
        private const nuint PaintMaskTextureUsage = 5; // MTLTextureUsageShaderRead | MTLTextureUsageRenderTarget
        private const nuint MTLPrimitiveTypeTriangle = 3;
        private const nuint MTLLoadActionLoad = 1; // MTLoadActionLoad
        private const nuint MTLLoadActionClear = 2;
        private const nuint MTLStoreActionStore = 1;
        private const nuint MTLBlendFactorZero = 0;
        private const nuint MTLBlendFactorOne = 1;
        private const nuint MTLBlendFactorSourceAlpha = 4;
        private const nuint MTLBlendFactorOneMinusSourceAlpha = 5;
        private const nuint MTLColorWriteMaskNone = 0;
        private const nuint MTLColorWriteMaskAlpha = 1;
        private const nuint MTLColorWriteMaskBlue = 2;
        private const nuint MTLColorWriteMaskGreen = 4;
        private const nuint MTLColorWriteMaskRed = 8;
        private const nuint MTLColorWriteMaskAll = 15;
        private const int MaxPendingPaintStamps = 8192;
        private const int MaxShadowPassLights = 4;
        private const int SpiralNormalMapSize = 1024;

        private IntPtr _nativeView;
        private IntPtr _metalLayer;
        private IntPtr _depthTexture;
        private IntPtr _spiralNormalTexture;
        private IntPtr _paintMaskTexture;
        private IntPtr _paintColorTexture;
        private IntPtr _paintStampLibrary;
        private IntPtr _paintStampVertexFunction;
        private IntPtr _paintStampFragmentFunction;
        private IntPtr _paintStampPipelineRust;
        private IntPtr _paintStampPipelineWear;
        private IntPtr _paintStampPipelineGunk;
        private IntPtr _paintStampPipelineScratch;
        private IntPtr _paintStampPipelineErase;
        private IntPtr _paintStampPipelineColor;
        private IntPtr _paintPickLibrary;
        private IntPtr _paintPickVertexFunction;
        private IntPtr _paintPickFragmentFunction;
        private IntPtr _paintPickPipeline;
        private IntPtr _paintPickDepthStencilState;
        private IntPtr _paintPickTexture;
        private IntPtr _paintPickDepthTexture;
        private nuint _paintPickTextureWidth;
        private nuint _paintPickTextureHeight;
        private IntPtr _lightGizmoLibrary;
        private IntPtr _lightGizmoVertexFunction;
        private IntPtr _lightGizmoFragmentFunction;
        private IntPtr _lightGizmoPipeline;
        private IntPtr _gpuUniformUploadScratch;
        private int _gpuUniformUploadScratchSize;
        private IntPtr _paintStampUniformUploadScratch;
        private int _paintStampUniformUploadScratchSize;
        private nuint _depthTextureWidth;
        private nuint _depthTextureHeight;
        private MetalRendererContext? _context;
        private DispatcherTimer? _renderTimer;
        private MeshShapeKey _meshShapeKey;
        private CollarShapeKey _collarShapeKey;
        private SliderAssemblyShapeKey _sliderAssemblyShapeKey;
        private ToggleAssemblyShapeKey _toggleAssemblyShapeKey;
        private PushButtonAssemblyShapeKey _pushButtonAssemblyShapeKey;
        private SpiralNormalMapKey _spiralNormalMapKey;
        private MetalMeshGpuResources? _meshResources;
        private MetalMeshGpuResources? _collarResources;
        private MetalMeshGpuResources? _sliderBackplateResources;
        private MetalMeshGpuResources? _sliderThumbResources;
        private MetalMeshGpuResources? _toggleBaseResources;
        private MetalMeshGpuResources? _toggleLeverResources;
        private MetalMeshGpuResources? _toggleSleeveResources;
        private MetalMeshGpuResources? _pushButtonBaseResources;
        private MetalMeshGpuResources? _pushButtonCapResources;
        private bool _viewportCollarStateLogged;
        private bool _offscreenCollarStateLogged;
        private int _paintMaskTextureVersion = -1;
        private bool _paintColorTextureNeedsClear = true;

        private KnobProject? _project;
        private bool _dirty = true;

        private float _orbitYawDeg = 30f;
        private float _orbitPitchDeg = -20f;
        private float _zoom = 1.0f;
        private Vector2 _panPx = Vector2.Zero;
        private bool _isOrbiting;
        private bool _isPanning;
        private bool _isPainting;
        private Point _lastPointer;
        private Point _lastPaintPointer;
        private int _activeStrokeLayerIndex;
        private Point _scratchVirtualPointer;
        private bool _scratchVirtualPointerInitialized;
        private float _scratchCurrentDepth;
        private uint _paintStrokeSeed;
        private KeyModifiers _lastKnownModifiers;
        private bool _optionDepthRampActive;
        private PaintHitMode _lastPaintHitMode = PaintHitMode.Idle;
        private bool _paintPickMapDirty = true;
        private readonly byte[] _paintPickReadbackPixel = new byte[4];
        private readonly List<PaintStampCommand> _pendingPaintStampCommands = new();
        private readonly List<PaintStampCommand> _activeStrokeCommands = new();
        private readonly List<PaintStrokeRecord> _committedPaintStrokes = new();
        private readonly List<PaintLayerState> _paintLayers = new();
        private readonly List<LightGizmoSnapshot> _lightGizmoSnapshots = new(16);
        private readonly List<ShadowPassConfig> _resolvedShadowPasses = new(MaxShadowPassLights);
        private readonly List<ShadowLightContribution> _shadowLightContributions = new(8);
        private int _paintHistoryRevision;
        private int _activePaintLayerIndex;
        private int _focusedPaintLayerIndex = -1;
        private bool _paintRebuildRequested = true;
        private OrientationDebug _orientation = new()
        {
            InvertX = true,
            InvertY = true,
            InvertZ = true,
            FlipCamera180 = true
        };
        private bool _gizmoInvertX;
        private bool _gizmoInvertY = true;
        private bool _gizmoInvertZ;
        private bool _brushInvertX;
        private bool _brushInvertY = true;
        private bool _brushInvertZ;
        private bool _invertImportedCollarOrbit;
        private bool _invertKnobFrontFaceWinding = true;
        private bool _invertImportedStlFrontFaceWinding = true;
        private ContextMenu? _debugContextMenu;
        private long _diagnosticsLastFrameStartTimestamp;
        private double _diagnosticsLastFrameCpuMs;
        private double _diagnosticsSmoothedFrameCpuMs = 16.67d;
        private double _diagnosticsSmoothedFps = 60d;
        private double _diagnosticsLastPaintStampCpuMs;
        private long _diagnosticsLastPublishTimestamp;
        private const double DiagnosticsSmoothingAlpha = 0.16d;
        private const double DiagnosticsPublishMinIntervalMs = 120d;

        public KnobProject? Project
        {
            get => _project;
            set
            {
                if (ReferenceEquals(_project, value))
                {
                    return;
                }

                _project = value;
                ClearMeshResources();
                _meshShapeKey = default;
                _collarShapeKey = default;
                _sliderAssemblyShapeKey = default;
                _toggleAssemblyShapeKey = default;
                _pushButtonAssemblyShapeKey = default;
                ReleaseSpiralNormalTexture();
                _spiralNormalMapKey = default;
                ReleasePaintMaskTexture();
                ReleasePaintColorTexture();
                ReleasePaintStampResources();
                ReleasePaintPickResources();
                ReleaseLightGizmoResources();
                ReleaseUniformUploadScratchBuffers();
                _paintMaskTextureVersion = -1;
                _paintColorTextureNeedsClear = true;
                _paintPickMapDirty = true;
                _pendingPaintStampCommands.Clear();
                _activeStrokeCommands.Clear();
                _committedPaintStrokes.Clear();
                _paintLayers.Clear();
                _paintHistoryRevision = 0;
                _activePaintLayerIndex = 0;
                _focusedPaintLayerIndex = -1;
                _activeStrokeLayerIndex = 0;
                EnsureDefaultPaintLayer();
                _paintRebuildRequested = true;
                _viewportCollarStateLogged = false;
                _offscreenCollarStateLogged = false;
                InvalidateGpu();
                RaisePaintLayersChanged();
                RaisePaintHistoryRevisionChanged();
                PublishPaintHudSnapshot();
            }
        }

        public OrientationDebug CurrentOrientation => _orientation;
        public ViewportCameraState CurrentCameraState =>
            new(_orbitYawDeg, _orbitPitchDeg, _zoom, new SKPoint(_panPx.X, _panPx.Y));
        public bool CanRenderOffscreen =>
            OperatingSystem.IsMacOS() &&
            _context is not null &&
            _metalLayer != IntPtr.Zero &&
            _project is not null;
        public int PaintHistoryRevision => _paintHistoryRevision;
        public int ActivePaintLayerIndex => _activePaintLayerIndex;
        public int FocusedPaintLayerIndex => _focusedPaintLayerIndex;
        public event Action<PaintHudSnapshot>? PaintHudUpdated;
        public event Action<RuntimeDiagnosticsSnapshot>? RuntimeDiagnosticsUpdated;
        public event Action? ViewportFrameRendered;
        public event Action? PaintLayersChanged;
        public event Action<int>? PaintHistoryRevisionChanged;

        private static double SmoothValue(double previous, double current)
        {
            return previous + ((current - previous) * DiagnosticsSmoothingAlpha);
        }

        private void RecordPaintStampDiagnostics(double cpuMs)
        {
            _diagnosticsLastPaintStampCpuMs = cpuMs;
            PublishRuntimeDiagnosticsSnapshot(force: false);
        }

        private void RecordFrameDiagnostics(long frameStartTimestamp, long frameEndTimestamp)
        {
            double frameCpuMs = Stopwatch.GetElapsedTime(frameStartTimestamp, frameEndTimestamp).TotalMilliseconds;
            _diagnosticsLastFrameCpuMs = frameCpuMs;
            _diagnosticsSmoothedFrameCpuMs = SmoothValue(_diagnosticsSmoothedFrameCpuMs, frameCpuMs);

            if (_diagnosticsLastFrameStartTimestamp != 0)
            {
                double frameIntervalMs = Stopwatch.GetElapsedTime(_diagnosticsLastFrameStartTimestamp, frameStartTimestamp).TotalMilliseconds;
                if (frameIntervalMs > 1e-4d)
                {
                    double fps = 1000d / frameIntervalMs;
                    _diagnosticsSmoothedFps = SmoothValue(_diagnosticsSmoothedFps, fps);
                }
            }

            _diagnosticsLastFrameStartTimestamp = frameStartTimestamp;
        }

        private void PublishRuntimeDiagnosticsSnapshot(bool force)
        {
            Action<RuntimeDiagnosticsSnapshot>? handler = RuntimeDiagnosticsUpdated;
            if (handler == null)
            {
                return;
            }

            long now = Stopwatch.GetTimestamp();
            if (!force && _diagnosticsLastPublishTimestamp != 0)
            {
                double elapsedMs = Stopwatch.GetElapsedTime(_diagnosticsLastPublishTimestamp, now).TotalMilliseconds;
                if (elapsedMs < DiagnosticsPublishMinIntervalMs)
                {
                    return;
                }
            }

            _diagnosticsLastPublishTimestamp = now;
            handler(new RuntimeDiagnosticsSnapshot(
                _diagnosticsLastFrameCpuMs,
                _diagnosticsSmoothedFrameCpuMs,
                _diagnosticsSmoothedFps,
                _diagnosticsLastPaintStampCpuMs,
                _pendingPaintStampCommands.Count,
                _lastPaintHitMode,
                _isPainting));
        }

        public MetalViewport()
        {
            Focusable = true;
            IsHitTestVisible = true;
            EnsureDefaultPaintLayer();
        }

        public void InvalidateGpu()
        {
            _dirty = true;
            if (!_isPainting)
            {
                _paintPickMapDirty = true;
            }

            if (_metalLayer != IntPtr.Zero)
            {
                Render(_ => { });
            }
        }

        public void ResetCamera()
        {
            _orbitYawDeg = 30f;
            _orbitPitchDeg = -20f;
            _zoom = 1.0f;
            _panPx = Vector2.Zero;
            InvalidateGpu();
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            if (!OperatingSystem.IsMacOS())
            {
                return base.CreateNativeControlCore(parent);
            }

            _context = MetalRendererContext.Instance;
            IntPtr device = _context.Device.Handle;
            if (device == IntPtr.Zero)
            {
                return base.CreateNativeControlCore(parent);
            }

            _nativeView = ObjC.IntPtr_objc_msgSend(ObjC.IntPtr_objc_msgSend(ObjCClasses.NSView, Selectors.Alloc), Selectors.Init);
            _metalLayer = ObjC.IntPtr_objc_msgSend(ObjC.IntPtr_objc_msgSend(ObjCClasses.CAMetalLayer, Selectors.Alloc), Selectors.Init);

            ObjC.Void_objc_msgSend_IntPtr(_metalLayer, Selectors.SetDevice, device);
            ObjC.Void_objc_msgSend_UInt(_metalLayer, Selectors.SetPixelFormat, (nuint)MetalRendererContext.DefaultColorFormat);
            ObjC.Void_objc_msgSend_Bool(_metalLayer, Selectors.SetFramebufferOnly, false);
            ObjC.Void_objc_msgSend_Double(_metalLayer, Selectors.SetContentsScale, GetRenderScale());

            ObjC.Void_objc_msgSend_Bool(_nativeView, Selectors.SetWantsLayer, true);
            ObjC.Void_objc_msgSend_IntPtr(_nativeView, Selectors.SetLayer, _metalLayer);

            UpdateDrawableSize(Bounds.Size);
            StartRenderLoop();
            InvalidateGpu();

            return new PlatformHandle(_nativeView, "NSView");
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            if (OperatingSystem.IsMacOS())
            {
                StopRenderLoop();

                if (_metalLayer != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend(_metalLayer, Selectors.Release);
                    _metalLayer = IntPtr.Zero;
                }

                if (_nativeView != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend(_nativeView, Selectors.Release);
                    _nativeView = IntPtr.Zero;
                }

                if (_depthTexture != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend(_depthTexture, Selectors.Release);
                    _depthTexture = IntPtr.Zero;
                    _depthTextureWidth = 0;
                    _depthTextureHeight = 0;
                }

                ReleaseSpiralNormalTexture();
                _spiralNormalMapKey = default;
                ReleasePaintMaskTexture();
                ReleasePaintColorTexture();
                ReleasePaintStampResources();
                ReleasePaintPickResources();
                ReleaseLightGizmoResources();
                ReleaseUniformUploadScratchBuffers();
                _paintMaskTextureVersion = -1;
                _paintColorTextureNeedsClear = true;
                _paintPickMapDirty = true;
                _pendingPaintStampCommands.Clear();
                _activeStrokeCommands.Clear();
                _committedPaintStrokes.Clear();
                _paintHistoryRevision = 0;
                _paintRebuildRequested = true;

                ClearMeshResources();
                _meshShapeKey = default;
                _collarShapeKey = default;
                _sliderAssemblyShapeKey = default;
                _toggleAssemblyShapeKey = default;
                _pushButtonAssemblyShapeKey = default;
                _context = null;
            }

            base.DestroyNativeControlCore(control);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Size arranged = base.ArrangeOverride(finalSize);
            UpdateDrawableSize(finalSize);
            _dirty = true;
            _paintPickMapDirty = true;
            return arranged;
        }

        public void Render(Action<MTLRenderCommandEncoder> encode)
        {
            if (!OperatingSystem.IsMacOS() || _context is null || _metalLayer == IntPtr.Zero)
            {
                return;
            }

            long frameStartTimestamp = Stopwatch.GetTimestamp();

            IntPtr drawable = ObjC.IntPtr_objc_msgSend(_metalLayer, Selectors.NextDrawable);
            if (drawable == IntPtr.Zero)
            {
                return;
            }

            IntPtr texture = ObjC.IntPtr_objc_msgSend(drawable, Selectors.Texture);
            if (texture == IntPtr.Zero)
            {
                return;
            }

            nuint drawableWidth = ObjC.UInt_objc_msgSend(texture, Selectors.Width);
            nuint drawableHeight = ObjC.UInt_objc_msgSend(texture, Selectors.Height);
            EnsureDepthTexture(drawableWidth, drawableHeight);

            IntPtr passDescriptor = ObjC.IntPtr_objc_msgSend(ObjCClasses.MTLRenderPassDescriptor, Selectors.RenderPassDescriptor);
            IntPtr colorAttachments = ObjC.IntPtr_objc_msgSend(passDescriptor, Selectors.ColorAttachments);
            IntPtr attachment = ObjC.IntPtr_objc_msgSend_UInt(colorAttachments, Selectors.ObjectAtIndexedSubscript, 0);
            if (attachment == IntPtr.Zero)
            {
                return;
            }

            ObjC.Void_objc_msgSend_IntPtr(attachment, Selectors.SetTexture, texture);
            ObjC.Void_objc_msgSend_UInt(attachment, Selectors.SetLoadAction, 2); // MTLLoadActionClear
            ObjC.Void_objc_msgSend_UInt(attachment, Selectors.SetStoreAction, 1); // MTLStoreActionStore
            ObjC.Void_objc_msgSend_MTLClearColor(attachment, Selectors.SetClearColor, new MTLClearColor(0.07d, 0.07d, 0.09d, 1d));

            if (_depthTexture != IntPtr.Zero)
            {
                IntPtr depthAttachment = ObjC.IntPtr_objc_msgSend(passDescriptor, Selectors.DepthAttachment);
                if (depthAttachment != IntPtr.Zero)
                {
                    ObjC.Void_objc_msgSend_IntPtr(depthAttachment, Selectors.SetTexture, _depthTexture);
                    ObjC.Void_objc_msgSend_UInt(depthAttachment, Selectors.SetLoadAction, 2); // MTLLoadActionClear
                    ObjC.Void_objc_msgSend_UInt(depthAttachment, Selectors.SetStoreAction, 0); // MTLStoreActionDontCare
                    ObjC.Void_objc_msgSend_Double(depthAttachment, Selectors.SetClearDepth, 1.0);
                }
            }

            IntPtr commandBuffer = _context.CreateCommandBuffer().Handle;
            if (commandBuffer == IntPtr.Zero)
            {
                return;
            }

            ModelNode? modelNode = _project?.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
            RefreshMeshResources(_project, modelNode);
            CollarNode? collarNode = modelNode?.Children.OfType<CollarNode>().FirstOrDefault();

            bool drawKnob =
                _project?.ProjectType == InteractorProjectType.RotaryKnob &&
                IsRenderableMesh(_meshResources);
            bool drawCollar =
                drawKnob &&
                collarNode is { Enabled: true } &&
                IsRenderableMesh(_collarResources);
            bool drawSliderBackplate =
                _project?.ProjectType == InteractorProjectType.ThumbSlider &&
                IsRenderableMesh(_sliderBackplateResources);
            bool drawSliderThumb =
                _project?.ProjectType == InteractorProjectType.ThumbSlider &&
                IsRenderableMesh(_sliderThumbResources);
            bool drawToggleBase =
                _project?.ProjectType == InteractorProjectType.FlipSwitch &&
                IsRenderableMesh(_toggleBaseResources);
            bool drawToggleLever =
                _project?.ProjectType == InteractorProjectType.FlipSwitch &&
                IsRenderableMesh(_toggleLeverResources);
            bool drawToggleSleeve =
                _project?.ProjectType == InteractorProjectType.FlipSwitch &&
                IsRenderableMesh(_toggleSleeveResources);
            bool drawPushButtonBase =
                _project?.ProjectType == InteractorProjectType.PushButton &&
                IsRenderableMesh(_pushButtonBaseResources);
            bool drawPushButtonCap =
                _project?.ProjectType == InteractorProjectType.PushButton &&
                IsRenderableMesh(_pushButtonCapResources);

            if (drawKnob ||
                drawCollar ||
                drawSliderBackplate ||
                drawSliderThumb ||
                drawToggleBase ||
                drawToggleLever ||
                drawToggleSleeve ||
                drawPushButtonBase ||
                drawPushButtonCap)
            {
                if (!_viewportCollarStateLogged)
                {
                    _viewportCollarStateLogged = true;
                    LogCollarState("viewport", collarNode, _collarResources);
                }

                float sceneReferenceRadius = MathF.Max(1f, modelNode?.Radius ?? 220f);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawKnob ? _meshResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawCollar ? _collarResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawSliderBackplate ? _sliderBackplateResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawSliderThumb ? _sliderThumbResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawToggleBase ? _toggleBaseResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawToggleLever ? _toggleLeverResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawToggleSleeve ? _toggleSleeveResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawPushButtonBase ? _pushButtonBaseResources : null);
                sceneReferenceRadius = IncludeReferenceRadius(sceneReferenceRadius, drawPushButtonCap ? _pushButtonCapResources : null);

                GpuUniforms knobUniforms = BuildUniforms(_project, modelNode, sceneReferenceRadius, Bounds.Size);
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
                Vector3 toggleSleeveColor = _project?.ToggleTipSleeveColor ?? assemblyMaterialPalette.AccentColor;
                float toggleSleeveMetallic = _project != null
                    ? Math.Clamp(_project.ToggleTipSleeveMetallic, 0f, 1f)
                    : assemblyMaterialPalette.AccentMetallic;
                float toggleSleeveRoughness = _project != null
                    ? Math.Clamp(_project.ToggleTipSleeveRoughness, 0.04f, 1f)
                    : assemblyMaterialPalette.AccentRoughness;
                float toggleSleevePearlescence = _project != null
                    ? Math.Clamp(_project.ToggleTipSleevePearlescence, 0f, 1f)
                    : assemblyMaterialPalette.Pearlescence;
                float toggleSleeveDiffuseStrength = _project?.ToggleTipSleeveDiffuseStrength ?? 1f;
                float toggleSleeveSpecularStrength = _project?.ToggleTipSleeveSpecularStrength ?? 1f;
                float toggleSleeveRust = _project?.ToggleTipSleeveRustAmount ?? 0f;
                float toggleSleeveWear = _project?.ToggleTipSleeveWearAmount ?? 0f;
                float toggleSleeveGunk = _project?.ToggleTipSleeveGunkAmount ?? 0f;
                GpuUniforms toggleBaseUniforms = drawToggleBase
                    ? BuildSliderPartUniforms(
                        knobUniforms,
                        assemblyMaterialPalette.BaseColor,
                        assemblyMaterialPalette.BaseMetallic,
                        assemblyMaterialPalette.BaseRoughness,
                        assemblyMaterialPalette.Pearlescence)
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
                if (drawCollar && _invertImportedCollarOrbit && IsImportedCollarPreset(collarNode))
                {
                    collarUniforms.ModelRotationCosSin.Y = -collarUniforms.ModelRotationCosSin.Y;
                }

                IntPtr encoderPtr = ObjC.IntPtr_objc_msgSend_IntPtr(commandBuffer, Selectors.RenderCommandEncoderWithDescriptor, passDescriptor);
                if (encoderPtr != IntPtr.Zero)
                {
                    MetalPipelineManager pipelineManager = MetalPipelineManager.Instance;
                    pipelineManager.UsePipeline(new MTLRenderCommandEncoderHandle(encoderPtr));

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

                    Size viewportDip = Bounds.Size;
                    float renderScale = GetRenderScale();
                    float viewportWidthPx = MathF.Max(1f, (float)viewportDip.Width * renderScale);
                    float viewportHeightPx = MathF.Max(1f, (float)viewportDip.Height * renderScale);
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
                    IReadOnlyList<ShadowPassConfig> shadowConfigs = ResolveShadowPassConfigs(_project, right, up, forward, viewportWidthPx, viewportHeightPx);

                    encode(new MTLRenderCommandEncoder(encoderPtr));

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
                    }

                    if (drawKnob)
                    {
                        MetalPipelineManager.SetFrontFacingWinding(
                            new MTLRenderCommandEncoderHandle(encoderPtr),
                            frontFacingClockwiseKnob);
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

                            // Skip lever shadow projection: this screen-space shadow pass can self-project
                            // onto the lever and produce mid-shaft "floating plane" artifacts.

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
                }
            }

            RenderLightGizmoOverlayPass(commandBuffer, texture, drawableWidth, drawableHeight);
            ObjC.Void_objc_msgSend_IntPtr(commandBuffer, Selectors.PresentDrawable, drawable);
            ObjC.Void_objc_msgSend(commandBuffer, Selectors.Commit);
            _dirty = false;
            RecordFrameDiagnostics(frameStartTimestamp, Stopwatch.GetTimestamp());
            PublishRuntimeDiagnosticsSnapshot(force: false);
            ViewportFrameRendered?.Invoke();
        }
        public void Dispose()
        {
        }
    }
}
