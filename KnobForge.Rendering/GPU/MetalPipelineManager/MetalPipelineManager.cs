using System;
using System.IO;
using System.Runtime.InteropServices;

namespace KnobForge.Rendering.GPU;

public sealed partial class MetalPipelineManager
{
    private static readonly Lazy<MetalPipelineManager> InstanceFactory =
        new(static () => new MetalPipelineManager(MetalRendererContext.Instance));

    private const string VertexFunctionName = "vertex_main";
    private const string FragmentFunctionName = "fragment_main";
    private const string FullscreenVertexFunctionName = "vertex_fullscreen";
    private const string FullscreenBlitFragmentFunctionName = "fragment_fullscreen_blit";
    private const string BloomExtractFragmentFunctionName = "fragment_bloom_extract";
    private const string BloomBlurFragmentFunctionName = "fragment_bloom_blur";
    private const string BloomCompositeFragmentFunctionName = "fragment_bloom_composite";
    private const nuint DepthPixelFormat = 252; // MTLPixelFormatDepth32Float
    private const nuint MsaaSampleCount = 4;

    private readonly MetalRendererContext _context;
    private readonly IMTLLibrary _library;
    private readonly IMTLFunction _vertexFunction;
    private readonly IMTLFunction _fragmentFunction;
    private readonly IMTLFunction _fullscreenVertexFunction;
    private readonly IMTLFunction _fullscreenBlitFragmentFunction;
    private readonly IMTLFunction? _fullscreenBloomExtractFragmentFunction;
    private readonly IMTLFunction? _fullscreenBloomBlurFragmentFunction;
    private readonly IMTLFunction? _fullscreenBloomCompositeFragmentFunction;
    private readonly IMTLRenderPipelineState _defaultPipeline;
    private readonly IMTLRenderPipelineState? _msaaPipeline;
    private readonly IMTLRenderPipelineState _additivePipeline;
    private readonly IMTLRenderPipelineState? _msaaAdditivePipeline;
    private readonly IMTLRenderPipelineState _fullscreenBlitPipeline;
    private readonly IMTLRenderPipelineState? _fullscreenBloomExtractPipeline;
    private readonly IMTLRenderPipelineState? _fullscreenBloomBlurPipeline;
    private readonly IMTLRenderPipelineState? _fullscreenBloomCompositePipeline;
    private readonly IntPtr _defaultDepthStencilState;
    private readonly IntPtr _shadowDepthStencilState;

    public static MetalPipelineManager Instance => InstanceFactory.Value;

    public MetalPipelineManager()
        : this(MetalRendererContext.Instance)
    {
    }

    public MetalPipelineManager(MetalRendererContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Metal pipeline manager is only supported on macOS.");
        }

        IntPtr device = _context.Device.Handle;
        if (device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Metal device is not available.");
        }

        bool loadedFromFile = TryLoadShaderSource(out string shaderSource, out string shaderPath);
        if (!loadedFromFile)
        {
            LogError($"Shader file not found. Falling back to embedded source. Searched base: {AppContext.BaseDirectory}");
        }

        IntPtr libraryPtr = CreateShaderLibrary(device, shaderSource);
        if (libraryPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to compile Metal shader library. Source: {shaderPath}");
        }

        _library = new MTLLibraryHandle(libraryPtr);

        IntPtr vertexFunctionPtr = CreateFunction(libraryPtr, VertexFunctionName);
        IntPtr fragmentFunctionPtr = CreateFunction(libraryPtr, FragmentFunctionName);
        if (vertexFunctionPtr == IntPtr.Zero || fragmentFunctionPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Failed to load shader functions '{VertexFunctionName}'/'{FragmentFunctionName}' from '{shaderPath}'.");
        }

        _vertexFunction = new MTLFunctionHandle(vertexFunctionPtr);
        _fragmentFunction = new MTLFunctionHandle(fragmentFunctionPtr);

        IntPtr fullscreenVertexFunctionPtr = CreateFunction(libraryPtr, FullscreenVertexFunctionName);
        IntPtr fullscreenBlitFragmentFunctionPtr = CreateFunction(libraryPtr, FullscreenBlitFragmentFunctionName);
        if (fullscreenVertexFunctionPtr == IntPtr.Zero || fullscreenBlitFragmentFunctionPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Failed to load shader functions '{FullscreenVertexFunctionName}'/'{FullscreenBlitFragmentFunctionName}' from '{shaderPath}'.");
        }

        _fullscreenVertexFunction = new MTLFunctionHandle(fullscreenVertexFunctionPtr);
        _fullscreenBlitFragmentFunction = new MTLFunctionHandle(fullscreenBlitFragmentFunctionPtr);

        IntPtr bloomExtractFunctionPtr = CreateFunction(libraryPtr, BloomExtractFragmentFunctionName);
        IntPtr bloomBlurFunctionPtr = CreateFunction(libraryPtr, BloomBlurFragmentFunctionName);
        IntPtr bloomCompositeFunctionPtr = CreateFunction(libraryPtr, BloomCompositeFragmentFunctionName);
        if (bloomExtractFunctionPtr == IntPtr.Zero ||
            bloomBlurFunctionPtr == IntPtr.Zero ||
            bloomCompositeFunctionPtr == IntPtr.Zero)
        {
            LogError(
                $"Bloom shader functions missing. Extract={bloomExtractFunctionPtr != IntPtr.Zero}, " +
                $"Blur={bloomBlurFunctionPtr != IntPtr.Zero}, Composite={bloomCompositeFunctionPtr != IntPtr.Zero}.");
        }

        _fullscreenBloomExtractFragmentFunction = bloomExtractFunctionPtr != IntPtr.Zero
            ? new MTLFunctionHandle(bloomExtractFunctionPtr)
            : null;
        _fullscreenBloomBlurFragmentFunction = bloomBlurFunctionPtr != IntPtr.Zero
            ? new MTLFunctionHandle(bloomBlurFunctionPtr)
            : null;
        _fullscreenBloomCompositeFragmentFunction = bloomCompositeFunctionPtr != IntPtr.Zero
            ? new MTLFunctionHandle(bloomCompositeFunctionPtr)
            : null;

        IntPtr pipelineStatePtr = CreateRenderPipelineState(
            device,
            _vertexFunction.Handle,
            _fragmentFunction.Handle);

        if (pipelineStatePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create default Metal render pipeline state.");
        }

        _defaultPipeline = new MTLRenderPipelineStateHandle(pipelineStatePtr);

        IntPtr msaaPipelineStatePtr = CreateRenderPipelineState(
            device,
            _vertexFunction.Handle,
            _fragmentFunction.Handle,
            MsaaSampleCount);
        if (msaaPipelineStatePtr != IntPtr.Zero)
        {
            _msaaPipeline = new MTLRenderPipelineStateHandle(msaaPipelineStatePtr);
        }
        else
        {
            LogError("MSAA pipeline creation failed. Falling back to non-MSAA rendering.");
            _msaaPipeline = null;
        }

        IntPtr additivePipelineStatePtr = CreateRenderPipelineState(
            device,
            _vertexFunction.Handle,
            _fragmentFunction.Handle,
            sampleCount: 1,
            additiveColorBlending: true);
        if (additivePipelineStatePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create additive Metal render pipeline state.");
        }

        _additivePipeline = new MTLRenderPipelineStateHandle(additivePipelineStatePtr);

        IntPtr msaaAdditivePipelineStatePtr = CreateRenderPipelineState(
            device,
            _vertexFunction.Handle,
            _fragmentFunction.Handle,
            sampleCount: MsaaSampleCount,
            additiveColorBlending: true);
        if (msaaAdditivePipelineStatePtr != IntPtr.Zero)
        {
            _msaaAdditivePipeline = new MTLRenderPipelineStateHandle(msaaAdditivePipelineStatePtr);
        }
        else
        {
            LogError("MSAA additive pipeline creation failed. Falling back to non-MSAA additive rendering.");
            _msaaAdditivePipeline = null;
        }

        _defaultDepthStencilState = CreateDepthStencilState(device, depthWriteEnabled: true);
        if (_defaultDepthStencilState == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create default Metal depth stencil state.");
        }

        _shadowDepthStencilState = CreateDepthStencilState(device, depthWriteEnabled: false);
        if (_shadowDepthStencilState == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create shadow Metal depth stencil state.");
        }

        IntPtr fullscreenBlitPipelineStatePtr = CreateRenderPipelineState(
            device,
            _fullscreenVertexFunction.Handle,
            _fullscreenBlitFragmentFunction.Handle,
            sampleCount: 1,
            additiveColorBlending: false,
            enableDepth: false,
            enableBlending: false);
        if (fullscreenBlitPipelineStatePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create fullscreen blit Metal render pipeline state.");
        }

        _fullscreenBlitPipeline = new MTLRenderPipelineStateHandle(fullscreenBlitPipelineStatePtr);

        _fullscreenBloomExtractPipeline = CreateFullscreenPipeline(
            device,
            _fullscreenVertexFunction,
            _fullscreenBloomExtractFragmentFunction,
            "bloom extract");
        _fullscreenBloomBlurPipeline = CreateFullscreenPipeline(
            device,
            _fullscreenVertexFunction,
            _fullscreenBloomBlurFragmentFunction,
            "bloom blur");
        _fullscreenBloomCompositePipeline = CreateFullscreenPipeline(
            device,
            _fullscreenVertexFunction,
            _fullscreenBloomCompositeFragmentFunction,
            "bloom composite");
    }

    private IMTLRenderPipelineState? CreateFullscreenPipeline(
        IntPtr device,
        IMTLFunction vertexFunction,
        IMTLFunction? fragmentFunction,
        string label)
    {
        if (fragmentFunction is null || fragmentFunction.Handle == IntPtr.Zero)
        {
            return null;
        }

        IntPtr pipelineStatePtr = CreateRenderPipelineState(
            device,
            vertexFunction.Handle,
            fragmentFunction.Handle,
            sampleCount: 1,
            additiveColorBlending: false,
            enableDepth: false,
            enableBlending: false);
        if (pipelineStatePtr == IntPtr.Zero)
        {
            LogError($"Failed to create fullscreen {label} Metal render pipeline state.");
            return null;
        }

        return new MTLRenderPipelineStateHandle(pipelineStatePtr);
    }

    public IMTLRenderPipelineState GetDefaultPipeline()
    {
        return _defaultPipeline;
    }

    public nuint ResolveSupportedSampleCount(nuint requestedSampleCount)
    {
        bool hasMsaaPipeline = _msaaPipeline != null && _msaaPipeline.Handle != IntPtr.Zero;
        if (requestedSampleCount > 1 &&
            requestedSampleCount == MsaaSampleCount &&
            hasMsaaPipeline)
        {
            return MsaaSampleCount;
        }

        return 1;
    }

    public void UsePipeline(IMTLRenderCommandEncoder encoder, nuint sampleCount = 1)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        nuint resolvedSampleCount = ResolveSupportedSampleCount(sampleCount);
        bool hasMsaaPipeline = _msaaPipeline != null && _msaaPipeline.Handle != IntPtr.Zero;
        IMTLRenderPipelineState pipeline = resolvedSampleCount > 1 && hasMsaaPipeline
            ? _msaaPipeline!
            : _defaultPipeline;

        if (encoder.Handle == IntPtr.Zero || pipeline.Handle == IntPtr.Zero)
        {
            return;
        }

        ObjC.Void_objc_msgSend_IntPtr(encoder.Handle, Selectors.SetRenderPipelineState, pipeline.Handle);
        UseDepthWriteState(encoder);

        SetBackfaceCulling(encoder, true);
        SetFrontFacingWinding(encoder, clockwise: true);
    }

    public void UseAdditivePipeline(IMTLRenderCommandEncoder encoder, nuint sampleCount = 1)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        nuint resolvedSampleCount = ResolveSupportedSampleCount(sampleCount);
        bool hasMsaaAdditivePipeline = _msaaAdditivePipeline != null && _msaaAdditivePipeline.Handle != IntPtr.Zero;
        IMTLRenderPipelineState pipeline = resolvedSampleCount > 1 && hasMsaaAdditivePipeline
            ? _msaaAdditivePipeline!
            : _additivePipeline;

        if (encoder.Handle == IntPtr.Zero || pipeline.Handle == IntPtr.Zero)
        {
            return;
        }

        ObjC.Void_objc_msgSend_IntPtr(encoder.Handle, Selectors.SetRenderPipelineState, pipeline.Handle);
        UseDepthReadOnlyState(encoder);
        SetBackfaceCulling(encoder, true);
        SetFrontFacingWinding(encoder, clockwise: true);
    }

    public bool HasFullscreenBlitPipeline => _fullscreenBlitPipeline.Handle != IntPtr.Zero;

    public bool HasBloomPipelines =>
        _fullscreenBloomExtractPipeline is { Handle: not 0 } &&
        _fullscreenBloomBlurPipeline is { Handle: not 0 } &&
        _fullscreenBloomCompositePipeline is { Handle: not 0 };

    public void UseFullscreenBlitPipeline(IMTLRenderCommandEncoder encoder)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        if (encoder.Handle == IntPtr.Zero || _fullscreenBlitPipeline.Handle == IntPtr.Zero)
        {
            return;
        }

        ObjC.Void_objc_msgSend_IntPtr(encoder.Handle, Selectors.SetRenderPipelineState, _fullscreenBlitPipeline.Handle);
        UseDepthReadOnlyState(encoder);
        SetBackfaceCulling(encoder, false);
        SetFrontFacingWinding(encoder, clockwise: true);
    }

    public void UseBloomExtractPipeline(IMTLRenderCommandEncoder encoder)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        if (encoder.Handle == IntPtr.Zero || _fullscreenBloomExtractPipeline is null || _fullscreenBloomExtractPipeline.Handle == IntPtr.Zero)
        {
            return;
        }

        ObjC.Void_objc_msgSend_IntPtr(encoder.Handle, Selectors.SetRenderPipelineState, _fullscreenBloomExtractPipeline.Handle);
        UseDepthReadOnlyState(encoder);
        SetBackfaceCulling(encoder, false);
        SetFrontFacingWinding(encoder, clockwise: true);
    }

    public void UseBloomBlurPipeline(IMTLRenderCommandEncoder encoder)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        if (encoder.Handle == IntPtr.Zero || _fullscreenBloomBlurPipeline is null || _fullscreenBloomBlurPipeline.Handle == IntPtr.Zero)
        {
            return;
        }

        ObjC.Void_objc_msgSend_IntPtr(encoder.Handle, Selectors.SetRenderPipelineState, _fullscreenBloomBlurPipeline.Handle);
        UseDepthReadOnlyState(encoder);
        SetBackfaceCulling(encoder, false);
        SetFrontFacingWinding(encoder, clockwise: true);
    }

    public void UseBloomCompositePipeline(IMTLRenderCommandEncoder encoder)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        if (encoder.Handle == IntPtr.Zero || _fullscreenBloomCompositePipeline is null || _fullscreenBloomCompositePipeline.Handle == IntPtr.Zero)
        {
            return;
        }

        ObjC.Void_objc_msgSend_IntPtr(encoder.Handle, Selectors.SetRenderPipelineState, _fullscreenBloomCompositePipeline.Handle);
        UseDepthReadOnlyState(encoder);
        SetBackfaceCulling(encoder, false);
        SetFrontFacingWinding(encoder, clockwise: true);
    }

    public void UseDepthWriteState(IMTLRenderCommandEncoder encoder)
    {
        SetDepthStencilState(encoder, _defaultDepthStencilState);
    }

    public void UseDepthReadOnlyState(IMTLRenderCommandEncoder encoder)
    {
        IntPtr depthStencilState = _shadowDepthStencilState != IntPtr.Zero
            ? _shadowDepthStencilState
            : _defaultDepthStencilState;
        SetDepthStencilState(encoder, depthStencilState);
    }

    public static void SetBackfaceCulling(IMTLRenderCommandEncoder encoder, bool enabled)
    {
        if (encoder is null || encoder.Handle == IntPtr.Zero)
        {
            return;
        }

        ObjC.Void_objc_msgSend_UInt(
            encoder.Handle,
            Selectors.SetCullMode,
            enabled ? (nuint)2 : (nuint)0); // MTLCullModeBack / MTLCullModeNone
    }

    public static void SetFrontFacingWinding(IMTLRenderCommandEncoder encoder, bool clockwise)
    {
        if (encoder is null || encoder.Handle == IntPtr.Zero)
        {
            return;
        }

        ObjC.Void_objc_msgSend_UInt(
            encoder.Handle,
            Selectors.SetFrontFacingWinding,
            clockwise ? (nuint)0 : (nuint)1); // MTLWindingClockwise / MTLWindingCounterClockwise
    }
}
