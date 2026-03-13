using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KnobForge.Core;
using KnobForge.Core.Export;
using KnobForge.Core.Scene;
using KnobForge.Rendering.GPU;
using SkiaSharp;

namespace KnobForge.Rendering
{
    public readonly record struct KnobExportProgress(int CompletedFrames, int TotalFrames, string Stage);

    public sealed class KnobExportResult
    {
        public required string OutputDirectory { get; init; }
        public required string FirstFramePath { get; init; }
        public string? SpritesheetPath { get; init; }
        public int ExportedViewCount { get; init; } = 1;
        public int RenderedFrames { get; init; }
        public int SpritesheetWidth { get; init; }
        public int SpritesheetHeight { get; init; }
        public SpritesheetLayout? EffectiveSpritesheetLayout { get; init; }
    }

    public sealed partial class KnobExporter
    {
        private const int MaxFrames = 1440;
        private const int MaxSpritesheetDimension = 16384;
        private const long MaxSpritesheetPixels = 16384L * 16384L;
        private const int MaxSupersampleScale = 4;
        private const long MaxTransparentNormalizationBytes = 256L * 1024L * 1024L;

        private readonly KnobProject _project;
        private readonly OrientationDebug _orientation;
        private readonly ViewportCameraState? _cameraState;
        private readonly Func<int, int, ViewportCameraState, double?, SKBitmap?> _gpuFrameProvider;
        private readonly Action<int, int>? _frameStateApplier;
        private readonly float? _referenceRadiusOverride;

        public KnobExporter(
            KnobProject project,
            OrientationDebug orientation,
            ViewportCameraState? cameraState = null,
            Func<int, int, ViewportCameraState, double?, SKBitmap?>? gpuFrameProvider = null,
            Action<int, int>? frameStateApplier = null,
            float? referenceRadiusOverride = null)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _orientation = orientation ?? throw new ArgumentNullException(nameof(orientation));
            _cameraState = cameraState;
            _gpuFrameProvider = gpuFrameProvider
                ?? throw new ArgumentNullException(nameof(gpuFrameProvider), "GPU-only export requires an offscreen GPU frame provider.");
            _frameStateApplier = frameStateApplier;
            _referenceRadiusOverride = referenceRadiusOverride;
        }

        public Task<KnobExportResult> ExportAsync(
            KnobExportSettings settings,
            string outputPath,
            IProgress<KnobExportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var legacy = ResolveLegacyOutput(outputPath);
            return ExportAsync(settings, legacy.OutputRootFolder, legacy.BaseName, progress, cancellationToken);
        }

        public Task<KnobExportResult> ExportAsync(
            KnobExportSettings settings,
            string outputRootFolder,
            string baseName,
            IProgress<KnobExportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(outputRootFolder))
            {
                throw new ArgumentException("Output root folder is required.", nameof(outputRootFolder));
            }

            ValidateBaseName(baseName);

            return Task.Run(() => ExportInternal(settings, outputRootFolder, baseName, progress, cancellationToken), cancellationToken);
        }

        private KnobExportResult ExportInternal(
            KnobExportSettings settings,
            string outputRootFolder,
            string baseName,
            IProgress<KnobExportProgress>? progress,
            CancellationToken cancellationToken)
        {
            ValidateSettings(settings);

            bool exportFrames = settings.ExportIndividualFrames;
            bool exportSpritesheet = settings.ExportSpritesheet;
            int frameCount = settings.FrameCount;
            int resolution = settings.Resolution;
            int supersampleScale = Math.Clamp(settings.SupersampleScale, 1, MaxSupersampleScale);
            int renderResolution = checked(resolution * supersampleScale);
            int paddingPx = Math.Max(0, (int)MathF.Round(settings.Padding));
            string frameOutputExtension = GetOutputExtension(settings.ImageFormat);
            const string spritesheetOutputExtension = "png";

            if (_project.ProjectType == InteractorProjectType.IndicatorLight && frameCount < 2)
            {
                throw new InvalidOperationException("Indicator Light export requires at least 2 frames (recommended: 24).");
            }

            int frameDigits = GetFrameNumberDigits(frameCount);
            ExportPathPlan paths = ResolveExportPaths(
                outputRootFolder,
                baseName,
                exportFrames,
                exportSpritesheet,
                frameDigits,
                frameOutputExtension,
                spritesheetOutputExtension);

            string outputDirectory = paths.OutputDirectory;
            Directory.CreateDirectory(outputDirectory);
            ValidateOutputPathWritable(outputDirectory);

            var modelNodes = _project.SceneRoot.Children.OfType<ModelNode>().ToList();
            var originalRotations = modelNodes
                .Select(model => (Model: model, Rotation: model.RotationRadians))
                .ToList();
            int originalToggleStateIndex = _project.ToggleStateIndex;
            float originalToggleStateBlendPosition = _project.ToggleStateBlendPosition;
            float originalSliderThumbPositionNormalized = _project.SliderThumbPositionNormalized;
            float originalPushButtonPressAmountNormalized = _project.PushButtonPressAmountNormalized;

            try
            {
                float referenceRadius = GetSceneReferenceRadius();
                ViewportCameraState baseExportViewportCamera = BuildExportViewportCameraState(
                    referenceRadius,
                    settings,
                    resolution,
                    renderResolution,
                    _cameraState);
                ExportViewpoint[] viewVariants = ExportViewpointResolver.ResolveViewpoints(settings);
                int totalFrames = checked(frameCount * viewVariants.Length);
                int completedFrames = 0;

                string? firstFramePath = null;
                string? firstSpritesheetPath = null;
                int spritesheetWidth = 0;
                int spritesheetHeight = 0;
                SpritesheetLayout? effectiveLayout = null;

                using var frameBitmap = new SKBitmap(new SKImageInfo(
                    resolution,
                    resolution,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul));
                using var frameCanvas = new SKCanvas(frameBitmap);
                SKSamplingOptions downsampleSampling = new(new SKCubicResampler(1f / 3f, 1f / 3f));
                SKSamplingOptions directSampling = new(SKFilterMode.Linear, SKMipmapMode.None);
                using var downsamplePaint = new SKPaint
                {
                    BlendMode = SKBlendMode.Src,
                    IsAntialias = true,
                    IsDither = true
                };

                float angleStep = 2f * MathF.PI / frameCount;
                for (int viewIndex = 0; viewIndex < viewVariants.Length; viewIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ExportViewpoint viewVariant = viewVariants[viewIndex];
                    ViewportCameraState exportViewportCamera = ExportViewpointResolver.ApplyViewpoint(baseExportViewportCamera, viewVariant);

                    SpritesheetPlan? spritesheetPlan = null;
                    SKBitmap? spritesheetBitmap = null;
                    SKCanvas? spritesheetCanvas = null;
                    SKPaint? spritesheetPaint = null;
                    try
                    {
                        var frameLumaSamples = new List<float>(frameCount);
                        if (exportSpritesheet)
                        {
                            spritesheetPlan = ResolveSpritesheetPlan(
                                frameCount,
                                resolution,
                                paddingPx,
                                settings.SpritesheetLayout,
                                progress);

                            spritesheetBitmap = new SKBitmap(new SKImageInfo(
                                spritesheetPlan.Value.Width,
                                spritesheetPlan.Value.Height,
                                SKColorType.Bgra8888,
                                SKAlphaType.Premul));
                            spritesheetCanvas = new SKCanvas(spritesheetBitmap);
                            spritesheetCanvas.Clear(new SKColor(0, 0, 0, 0));
                            spritesheetPaint = new SKPaint
                            {
                                BlendMode = SKBlendMode.Src,
                                IsAntialias = false
                            };

                            if (effectiveLayout == null)
                            {
                                spritesheetWidth = spritesheetPlan.Value.Width;
                                spritesheetHeight = spritesheetPlan.Value.Height;
                                effectiveLayout = spritesheetPlan.Value.Layout;
                            }
                        }

                        for (int i = 0; i < frameCount; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            progress?.Report(new KnobExportProgress(
                                completedFrames,
                                totalFrames,
                                $"Rendering {viewVariant.Name} {i + 1}/{frameCount}"));

                            if (_frameStateApplier != null)
                            {
                                _frameStateApplier(i, frameCount);
                            }
                            else
                            {
                                ApplyFrameState(
                                    i,
                                    frameCount,
                                    angleStep,
                                    originalRotations,
                                    originalToggleStateIndex,
                                    originalToggleStateBlendPosition,
                                    originalSliderThumbPositionNormalized,
                                    originalPushButtonPressAmountNormalized);
                            }

                            frameCanvas.Clear(new SKColor(0, 0, 0, 0));

                            double animationTimeSeconds = _project.ProjectType == InteractorProjectType.IndicatorLight
                                ? InteractorFrameTimeline.ResolveLoopAnimationTimeSeconds(i, frameCount)
                                : InteractorFrameTimeline.ResolveAnimationTimeSeconds(i, frameCount);
                            using SKBitmap? gpuFrame = _gpuFrameProvider(
                                renderResolution,
                                renderResolution,
                                exportViewportCamera,
                                animationTimeSeconds);
                            if (gpuFrame == null)
                            {
                                throw new InvalidOperationException("GPU frame provider returned null frame.");
                            }

                            using SKImage gpuImage = SKImage.FromBitmap(gpuFrame);
                            if (supersampleScale > 1 || gpuFrame.Width != resolution || gpuFrame.Height != resolution)
                            {
                                frameCanvas.DrawImage(
                                    gpuImage,
                                    new SKRect(0, 0, gpuFrame.Width, gpuFrame.Height),
                                    new SKRect(0, 0, resolution, resolution),
                                    downsampleSampling,
                                    downsamplePaint);
                            }
                            else
                            {
                                frameCanvas.DrawImage(
                                    gpuImage,
                                    new SKRect(0, 0, resolution, resolution),
                                    directSampling,
                                    downsamplePaint);
                            }
                            FrameAlphaMetrics frameMetrics = ComputeFrameAlphaMetrics(frameBitmap, 2);
                            if (!frameMetrics.HasOpaque)
                            {
                                throw new InvalidOperationException(
                                    $"Rendered viewpoint '{viewVariant.Name}' frame {i + 1}/{frameCount} was empty. Adjust camera/viewpoint framing and retry.");
                            }

                            frameLumaSamples.Add(frameMetrics.NormalizedLuma);

                            if (exportFrames)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                string framePath = settings.ImageFormat == ExportImageFormat.AutoLossless
                                    ? ResolveFrameBasePath(outputDirectory, baseName, viewVariant.FileTag, i, frameDigits)
                                    : ResolveFramePath(outputDirectory, baseName, viewVariant.FileTag, i, frameDigits, frameOutputExtension);
                                framePath = SaveBitmap(frameBitmap, framePath, settings);
                                firstFramePath ??= framePath;
                            }

                            if (exportSpritesheet && spritesheetCanvas != null && spritesheetPaint != null && spritesheetPlan.HasValue)
                            {
                                var origin = spritesheetPlan.Value.GetFrameOrigin(i);
                                spritesheetCanvas.DrawBitmap(frameBitmap, origin.X, origin.Y, spritesheetPaint);
                            }

                            completedFrames++;
                        }

                        if (_project.ProjectType == InteractorProjectType.IndicatorLight)
                        {
                            ValidateLoopEndpointContinuity(viewVariant.Name, frameLumaSamples);
                        }

                        if (exportSpritesheet && spritesheetBitmap != null)
                        {
                            progress?.Report(new KnobExportProgress(
                                completedFrames,
                                totalFrames,
                                $"Writing spritesheet ({viewVariant.Name})"));
                            cancellationToken.ThrowIfCancellationRequested();

                            string spritesheetPath = ResolveSpritesheetPath(
                                outputDirectory,
                                baseName,
                                viewVariant.FileTag,
                                spritesheetOutputExtension);
                            using SKBitmap? normalizedSpritesheetBitmap = CreateCompressionNormalizedBitmap(spritesheetBitmap);
                            SKBitmap spritesheetToWrite = normalizedSpritesheetBitmap ?? spritesheetBitmap;
                            if (settings.OptimizeSpritesheetPng)
                            {
                                SavePngOptimized(spritesheetToWrite, spritesheetPath, settings);
                            }
                            else
                            {
                                SavePngWithCompression(spritesheetToWrite, spritesheetPath, settings.PngCompressionLevel);
                            }
                            firstSpritesheetPath ??= spritesheetPath;
                        }
                    }
                    finally
                    {
                        spritesheetPaint?.Dispose();
                        spritesheetCanvas?.Dispose();
                        spritesheetBitmap?.Dispose();
                    }
                }

                progress?.Report(new KnobExportProgress(totalFrames, totalFrames, "Writing files"));

                string primaryFirstFramePath = firstFramePath
                    ?? firstSpritesheetPath
                    ?? ResolveFramePath(outputDirectory, baseName, 0, frameDigits, frameOutputExtension);
                string primarySpritesheetPath = firstSpritesheetPath
                    ?? ResolveSpritesheetPath(outputDirectory, baseName, spritesheetOutputExtension);

                return new KnobExportResult
                {
                    OutputDirectory = outputDirectory,
                    FirstFramePath = exportFrames
                        ? (firstFramePath ?? primaryFirstFramePath)
                        : (firstSpritesheetPath ?? primarySpritesheetPath),
                    SpritesheetPath = exportSpritesheet ? (firstSpritesheetPath ?? primarySpritesheetPath) : null,
                    ExportedViewCount = viewVariants.Length,
                    RenderedFrames = totalFrames,
                    SpritesheetWidth = spritesheetWidth,
                    SpritesheetHeight = spritesheetHeight,
                    EffectiveSpritesheetLayout = effectiveLayout
                };
            }
            finally
            {
                foreach (var entry in originalRotations)
                {
                    entry.Model.RotationRadians = entry.Rotation;
                }

                _project.ToggleStateIndex = originalToggleStateIndex;
                _project.ToggleStateBlendPosition = originalToggleStateBlendPosition;
                _project.SliderThumbPositionNormalized = originalSliderThumbPositionNormalized;
                _project.PushButtonPressAmountNormalized = originalPushButtonPressAmountNormalized;
            }
        }

        private static FrameAlphaMetrics ComputeFrameAlphaMetrics(SKBitmap bitmap, byte alphaThreshold)
        {
            bool hasOpaque = false;
            double weightedLuma = 0d;
            int pixelCount = bitmap.Width * bitmap.Height;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    SKColor pixel = bitmap.GetPixel(x, y);
                    if (pixel.Alpha <= alphaThreshold)
                    {
                        continue;
                    }

                    hasOpaque = true;
                    double alpha = pixel.Alpha / 255d;
                    double luma = (0.2126d * pixel.Red + 0.7152d * pixel.Green + 0.0722d * pixel.Blue) / 255d;
                    weightedLuma += luma * alpha;
                }
            }

            float normalizedLuma = pixelCount > 0
                ? (float)(weightedLuma / pixelCount)
                : 0f;
            return new FrameAlphaMetrics(hasOpaque, normalizedLuma);
        }

        private static void ValidateLoopEndpointContinuity(string viewpointName, IReadOnlyList<float> frameLumaSamples)
        {
            if (frameLumaSamples.Count < 3)
            {
                return;
            }

            float loopDelta = MathF.Abs(frameLumaSamples[0] - frameLumaSamples[frameLumaSamples.Count - 1]);
            float runningNeighborDelta = 0f;
            for (int i = 1; i < frameLumaSamples.Count; i++)
            {
                runningNeighborDelta += MathF.Abs(frameLumaSamples[i] - frameLumaSamples[i - 1]);
            }

            float averageNeighborDelta = runningNeighborDelta / MathF.Max(1, frameLumaSamples.Count - 1);
            float allowedDelta = MathF.Max(0.035f, (averageNeighborDelta * 3.25f) + 0.01f);
            if (loopDelta > allowedDelta)
            {
                throw new InvalidOperationException(
                    $"Rendered viewpoint '{viewpointName}' produced an unstable loop endpoint (first/last delta {loopDelta:0.###}, allowed {allowedDelta:0.###}). Increase frame count or reduce dynamic flicker/speed.");
            }
        }

        private void ApplyFrameState(
            int frameIndex,
            int frameCount,
            float angleStep,
            IReadOnlyList<(ModelNode Model, float Rotation)> originalRotations,
            int originalToggleStateIndex,
            float originalToggleStateBlendPosition,
            float originalSliderThumbPositionNormalized,
            float originalPushButtonPressAmountNormalized)
        {
            switch (_project.ProjectType)
            {
                case InteractorProjectType.RotaryKnob:
                {
                    float angle = frameIndex * angleStep;
                    for (int modelIndex = 0; modelIndex < originalRotations.Count; modelIndex++)
                    {
                        var entry = originalRotations[modelIndex];
                        entry.Model.RotationRadians = entry.Rotation + angle;
                    }

                    _project.ToggleStateIndex = originalToggleStateIndex;
                    _project.ToggleStateBlendPosition = originalToggleStateBlendPosition;
                    _project.SliderThumbPositionNormalized = originalSliderThumbPositionNormalized;
                    _project.PushButtonPressAmountNormalized = originalPushButtonPressAmountNormalized;
                    break;
                }
                case InteractorProjectType.FlipSwitch:
                {
                    for (int modelIndex = 0; modelIndex < originalRotations.Count; modelIndex++)
                    {
                        var entry = originalRotations[modelIndex];
                        entry.Model.RotationRadians = entry.Rotation;
                    }

                    float toggleBlendPosition = InteractorFrameTimeline.ResolveToggleBlendPosition(
                        frameIndex,
                        frameCount,
                        _project.ToggleStateCount);
                    _project.ToggleStateBlendPosition = toggleBlendPosition;
                    _project.ToggleStateIndex = InteractorFrameTimeline.ResolveToggleStateIndex(
                        frameIndex,
                        frameCount,
                        _project.ToggleStateCount);
                    _project.SliderThumbPositionNormalized = originalSliderThumbPositionNormalized;
                    _project.PushButtonPressAmountNormalized = originalPushButtonPressAmountNormalized;
                    break;
                }
                case InteractorProjectType.ThumbSlider:
                {
                    for (int modelIndex = 0; modelIndex < originalRotations.Count; modelIndex++)
                    {
                        var entry = originalRotations[modelIndex];
                        entry.Model.RotationRadians = entry.Rotation;
                    }

                    _project.ToggleStateIndex = originalToggleStateIndex;
                    _project.ToggleStateBlendPosition = originalToggleStateBlendPosition;
                    _project.SliderThumbPositionNormalized = InteractorFrameTimeline.ResolveNormalizedProgress(frameIndex, frameCount);
                    _project.PushButtonPressAmountNormalized = originalPushButtonPressAmountNormalized;
                    break;
                }
                case InteractorProjectType.PushButton:
                {
                    for (int modelIndex = 0; modelIndex < originalRotations.Count; modelIndex++)
                    {
                        var entry = originalRotations[modelIndex];
                        entry.Model.RotationRadians = entry.Rotation;
                    }

                    _project.ToggleStateIndex = originalToggleStateIndex;
                    _project.ToggleStateBlendPosition = originalToggleStateBlendPosition;
                    _project.SliderThumbPositionNormalized = originalSliderThumbPositionNormalized;
                    _project.PushButtonPressAmountNormalized = InteractorFrameTimeline.ResolveNormalizedProgress(frameIndex, frameCount);
                    break;
                }
                default:
                {
                    for (int modelIndex = 0; modelIndex < originalRotations.Count; modelIndex++)
                    {
                        var entry = originalRotations[modelIndex];
                        entry.Model.RotationRadians = entry.Rotation;
                    }

                    _project.ToggleStateIndex = originalToggleStateIndex;
                    _project.ToggleStateBlendPosition = originalToggleStateBlendPosition;
                    _project.SliderThumbPositionNormalized = originalSliderThumbPositionNormalized;
                    _project.PushButtonPressAmountNormalized = originalPushButtonPressAmountNormalized;
                    break;
                }
            }
        }

        private readonly record struct FrameAlphaMetrics(bool HasOpaque, float NormalizedLuma);

        private static string GetOutputExtension(ExportImageFormat imageFormat)
        {
            return imageFormat switch
            {
                ExportImageFormat.AutoLossless => "png",
                ExportImageFormat.PngOptimized => "png",
                ExportImageFormat.PngLossless => "png",
                ExportImageFormat.WebpLossless => "webp",
                ExportImageFormat.WebpLossy => "webp",
                _ => "png"
            };
        }

        private static string SaveBitmap(SKBitmap bitmap, string outputPath, KnobExportSettings settings)
        {
            using SKBitmap? normalizedBitmap = CreateCompressionNormalizedBitmap(bitmap);
            SKBitmap sourceBitmap = normalizedBitmap ?? bitmap;

            switch (settings.ImageFormat)
            {
                case ExportImageFormat.AutoLossless:
                    return SaveAutoLossless(sourceBitmap, outputPath, settings.PngCompressionLevel);
                case ExportImageFormat.WebpLossless:
                    SaveWebp(sourceBitmap, outputPath, lossless: true, settings.WebpLossyQuality);
                    return outputPath;
                case ExportImageFormat.WebpLossy:
                    SaveWebp(sourceBitmap, outputPath, lossless: false, settings.WebpLossyQuality);
                    return outputPath;
                case ExportImageFormat.PngOptimized:
                    SavePngOptimized(sourceBitmap, outputPath, settings);
                    return outputPath;
                case ExportImageFormat.PngLossless:
                default:
                    SavePngWithCompression(sourceBitmap, outputPath, settings.PngCompressionLevel);
                    return outputPath;
            }
        }

        private static string SaveAutoLossless(SKBitmap bitmap, string outputBasePath, int pngCompressionLevel)
        {
            using SKData pngData = EncodePng(bitmap, pngCompressionLevel);
            using SKData webpData = EncodeWebp(bitmap, lossless: true, quality: 100f);

            bool choosePng = pngData.Size <= webpData.Size;
            string chosenExtension = choosePng ? "png" : "webp";
            DeleteAlternateFormatOutputs(outputBasePath, chosenExtension);
            string outputPath = $"{outputBasePath}.{chosenExtension}";
            using FileStream output = File.Create(outputPath);
            (choosePng ? pngData : webpData).SaveTo(output);
            return outputPath;
        }

        private static void SavePngWithCompression(SKBitmap bitmap, string outputPath, int zlibLevel)
        {
            string outputBasePath = Path.Combine(
                Path.GetDirectoryName(outputPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(outputPath));
            DeleteAlternateFormatOutputs(outputBasePath, "png");
            using SKData pngData = EncodePng(bitmap, zlibLevel);
            using FileStream output = File.Create(outputPath);
            pngData.SaveTo(output);
        }

        public static SKData EncodePreviewSpritesheetPng(SKBitmap bitmap, KnobExportSettings settings, bool allowOptimization)
        {
            using SKBitmap? normalizedBitmap = CreateCompressionNormalizedBitmap(bitmap);
            SKBitmap sourceBitmap = normalizedBitmap ?? bitmap;
            return allowOptimization
                ? EncodeOptimizedPng(sourceBitmap, settings)
                : EncodePng(sourceBitmap, settings.PngCompressionLevel);
        }

        private static void SavePngOptimized(SKBitmap bitmap, string outputPath, KnobExportSettings settings)
        {
            string outputBasePath = Path.Combine(
                Path.GetDirectoryName(outputPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(outputPath));
            DeleteAlternateFormatOutputs(outputBasePath, "png");
            using SKData pngData = EncodeOptimizedPng(bitmap, settings);
            using FileStream output = File.Create(outputPath);
            pngData.SaveTo(output);
        }

        private static void SaveWebp(SKBitmap bitmap, string outputPath, bool lossless, float quality)
        {
            string outputBasePath = Path.Combine(
                Path.GetDirectoryName(outputPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(outputPath));
            DeleteAlternateFormatOutputs(outputBasePath, "webp");
            using SKData webpData = EncodeWebp(bitmap, lossless, quality);
            using FileStream output = File.Create(outputPath);
            webpData.SaveTo(output);
        }

        private static SKData EncodePng(SKBitmap bitmap, int zlibLevel)
        {
            int clampedLevel = Math.Clamp(zlibLevel, 0, 9);
            var options = new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, clampedLevel);
            using MemoryStream output = new();
            using SKPixmap? pixmap = bitmap.PeekPixels();
            if (pixmap != null && pixmap.Encode(output, options))
            {
                return SKData.CreateCopy(output.ToArray());
            }

            return bitmap.Encode(SKEncodedImageFormat.Png, quality: 100);
        }

        private static SKData EncodeWebp(SKBitmap bitmap, bool lossless, float quality)
        {
            float clampedQuality = Math.Clamp(quality, 0f, 100f);
            var options = new SKWebpEncoderOptions(
                lossless ? SKWebpEncoderCompression.Lossless : SKWebpEncoderCompression.Lossy,
                clampedQuality);
            using MemoryStream output = new();
            using SKPixmap? pixmap = bitmap.PeekPixels();
            if (pixmap != null && pixmap.Encode(output, options))
            {
                return SKData.CreateCopy(output.ToArray());
            }

            return bitmap.Encode(SKEncodedImageFormat.Webp, quality: (int)MathF.Round(clampedQuality));
        }

        private static SKData EncodeOptimizedPng(SKBitmap bitmap, KnobExportSettings settings)
        {
            SKData bestData = EncodePng(bitmap, settings.PngCompressionLevel);
            long bestSize = (long)bestData.Size;

            using SKBitmap? candidateBitmap = CreateNearLosslessQuantizedBitmap(bitmap, settings);
            if (candidateBitmap == null || !IsVisuallySafePngCandidate(bitmap, candidateBitmap, settings))
            {
                return bestData;
            }

            SKData candidateData = EncodePng(candidateBitmap, settings.PngCompressionLevel);
            long candidateSize = (long)candidateData.Size;
            if ((bestSize - candidateSize) < settings.PngOptimizationMinimumSavingsBytes)
            {
                candidateData.Dispose();
                return bestData;
            }

            bestData.Dispose();
            bestData = candidateData;
            bestSize = candidateSize;
            return bestData;
        }

        private static void DeleteAlternateFormatOutputs(string outputBasePath, string keepExtension)
        {
            string[] knownExtensions = { "png", "webp" };
            for (int i = 0; i < knownExtensions.Length; i++)
            {
                string extension = knownExtensions[i];
                if (string.Equals(extension, keepExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = $"{outputBasePath}.{extension}";
                if (File.Exists(candidate))
                {
                    File.Delete(candidate);
                }
            }
        }

        private static SKBitmap? CreateNearLosslessQuantizedBitmap(SKBitmap bitmap, KnobExportSettings settings)
        {
            if (!TryCopyBitmapBytes(bitmap, MaxTransparentNormalizationBytes, out byte[]? bytes) || bytes == null)
            {
                return null;
            }

            bool changed = false;
            int rowBytes = bitmap.RowBytes;
            int width = bitmap.Width;
            int height = bitmap.Height;

            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * rowBytes;
                for (int x = 0; x < width; x++)
                {
                    int pixelOffset = rowOffset + (x * 4);
                    if (pixelOffset + 3 >= bytes.Length)
                    {
                        break;
                    }

                    byte alpha = bytes[pixelOffset + 3];
                    if (alpha == 0)
                    {
                        continue;
                    }

                    bool lowAlpha = alpha <= settings.PngTranslucentAlphaThreshold;
                    int rgbStep = lowAlpha ? settings.PngTranslucentRgbStep : settings.PngOpaqueRgbStep;
                    int alphaStep = lowAlpha ? settings.PngTranslucentAlphaStep : settings.PngOpaqueAlphaStep;

                    byte quantizedAlpha = QuantizeByte(alpha, alphaStep);
                    byte quantizedBlue = QuantizeByte(bytes[pixelOffset], rgbStep);
                    byte quantizedGreen = QuantizeByte(bytes[pixelOffset + 1], rgbStep);
                    byte quantizedRed = QuantizeByte(bytes[pixelOffset + 2], rgbStep);

                    if (quantizedBlue > quantizedAlpha)
                    {
                        quantizedBlue = quantizedAlpha;
                    }

                    if (quantizedGreen > quantizedAlpha)
                    {
                        quantizedGreen = quantizedAlpha;
                    }

                    if (quantizedRed > quantizedAlpha)
                    {
                        quantizedRed = quantizedAlpha;
                    }

                    if (quantizedBlue == bytes[pixelOffset] &&
                        quantizedGreen == bytes[pixelOffset + 1] &&
                        quantizedRed == bytes[pixelOffset + 2] &&
                        quantizedAlpha == bytes[pixelOffset + 3])
                    {
                        continue;
                    }

                    bytes[pixelOffset] = quantizedBlue;
                    bytes[pixelOffset + 1] = quantizedGreen;
                    bytes[pixelOffset + 2] = quantizedRed;
                    bytes[pixelOffset + 3] = quantizedAlpha;
                    changed = true;
                }
            }

            if (!changed)
            {
                return null;
            }

            return CreateBitmapFromBytes(bitmap, bytes);
        }

        private static bool IsVisuallySafePngCandidate(SKBitmap sourceBitmap, SKBitmap candidateBitmap, KnobExportSettings settings)
        {
            if (sourceBitmap.Width != candidateBitmap.Width || sourceBitmap.Height != candidateBitmap.Height)
            {
                return false;
            }

            if (!TryCopyBitmapBytes(sourceBitmap, MaxTransparentNormalizationBytes, out byte[]? sourceBytes) || sourceBytes == null ||
                !TryCopyBitmapBytes(candidateBitmap, MaxTransparentNormalizationBytes, out byte[]? candidateBytes) || candidateBytes == null)
            {
                return false;
            }

            if (sourceBytes.Length != candidateBytes.Length)
            {
                return false;
            }

            byte maxOpaqueRgbDelta = 0;
            byte maxVisibleRgbDelta = 0;
            byte maxVisibleAlphaDelta = 0;
            double weightedLumaDelta = 0d;
            double weightedAlphaDelta = 0d;
            double visibleWeight = 0d;

            int rowBytes = sourceBitmap.RowBytes;
            int width = sourceBitmap.Width;
            int height = sourceBitmap.Height;

            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * rowBytes;
                for (int x = 0; x < width; x++)
                {
                    int pixelOffset = rowOffset + (x * 4);
                    if (pixelOffset + 3 >= sourceBytes.Length || pixelOffset + 3 >= candidateBytes.Length)
                    {
                        break;
                    }

                    byte sourceAlpha = sourceBytes[pixelOffset + 3];
                    byte candidateAlpha = candidateBytes[pixelOffset + 3];
                    byte visibleAlpha = Math.Max(sourceAlpha, candidateAlpha);

                    int alphaDelta = Math.Abs(sourceAlpha - candidateAlpha);
                    if (alphaDelta > maxVisibleAlphaDelta)
                    {
                        maxVisibleAlphaDelta = (byte)alphaDelta;
                    }

                    if (visibleAlpha == 0)
                    {
                        continue;
                    }

                    int blueDelta = Math.Abs(sourceBytes[pixelOffset] - candidateBytes[pixelOffset]);
                    int greenDelta = Math.Abs(sourceBytes[pixelOffset + 1] - candidateBytes[pixelOffset + 1]);
                    int redDelta = Math.Abs(sourceBytes[pixelOffset + 2] - candidateBytes[pixelOffset + 2]);
                    int visibleRgbDelta = Math.Max(redDelta, Math.Max(greenDelta, blueDelta));

                    if (visibleRgbDelta > maxVisibleRgbDelta)
                    {
                        maxVisibleRgbDelta = (byte)visibleRgbDelta;
                    }

                    if (sourceAlpha >= 224 && candidateAlpha >= 224 && visibleRgbDelta > maxOpaqueRgbDelta)
                    {
                        maxOpaqueRgbDelta = (byte)visibleRgbDelta;
                    }

                    double alphaWeight = visibleAlpha / 255d;
                    weightedLumaDelta += ((0.2126d * redDelta) + (0.7152d * greenDelta) + (0.0722d * blueDelta)) * alphaWeight;
                    weightedAlphaDelta += alphaDelta * alphaWeight;
                    visibleWeight += alphaWeight;
                }
            }

            if (visibleWeight <= double.Epsilon)
            {
                return true;
            }

            double meanVisibleLumaDelta = weightedLumaDelta / visibleWeight;
            double meanVisibleAlphaDelta = weightedAlphaDelta / visibleWeight;
            return maxOpaqueRgbDelta <= settings.PngMaxOpaqueRgbDelta &&
                maxVisibleRgbDelta <= settings.PngMaxVisibleRgbDelta &&
                maxVisibleAlphaDelta <= settings.PngMaxVisibleAlphaDelta &&
                meanVisibleLumaDelta <= settings.PngMeanVisibleLumaDelta &&
                meanVisibleAlphaDelta <= settings.PngMeanVisibleAlphaDelta;
        }

        private static byte QuantizeByte(byte value, int step)
        {
            if (step <= 1)
            {
                return value;
            }

            int quantized = ((value + (step / 2)) / step) * step;
            return (byte)Math.Clamp(quantized, 0, 255);
        }

        private static bool TryCopyBitmapBytes(SKBitmap bitmap, long maxBytes, out byte[]? bytes)
        {
            bytes = null;
            if (bitmap == null)
            {
                return false;
            }

            long byteCount = bitmap.ByteCount;
            if (byteCount <= 0 || byteCount > maxBytes || byteCount > int.MaxValue)
            {
                return false;
            }

            IntPtr sourcePixels = bitmap.GetPixels();
            if (sourcePixels == IntPtr.Zero)
            {
                return false;
            }

            bytes = new byte[byteCount];
            Marshal.Copy(sourcePixels, bytes, 0, (int)byteCount);
            return true;
        }

        private static SKBitmap? CreateBitmapFromBytes(SKBitmap referenceBitmap, byte[] bytes)
        {
            if (referenceBitmap == null || bytes == null)
            {
                return null;
            }

            var copy = new SKBitmap(referenceBitmap.Info);
            IntPtr targetPixels = copy.GetPixels();
            if (targetPixels == IntPtr.Zero ||
                copy.RowBytes != referenceBitmap.RowBytes ||
                copy.ByteCount != referenceBitmap.ByteCount)
            {
                copy.Dispose();
                return null;
            }

            Marshal.Copy(bytes, 0, targetPixels, bytes.Length);
            copy.NotifyPixelsChanged();
            return copy;
        }

        private static SKBitmap? CreateCompressionNormalizedBitmap(SKBitmap bitmap)
        {
            if (!TryCopyBitmapBytes(bitmap, MaxTransparentNormalizationBytes, out byte[]? bytes) || bytes == null)
            {
                return null;
            }

            bool changed = false;
            int rowBytes = bitmap.RowBytes;
            int width = bitmap.Width;
            int height = bitmap.Height;

            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * rowBytes;
                for (int x = 0; x < width; x++)
                {
                    int pixelOffset = rowOffset + (x * 4);
                    if (pixelOffset + 3 >= bytes.Length)
                    {
                        break;
                    }

                    if (bytes[pixelOffset + 3] != 0)
                    {
                        continue;
                    }

                    if (bytes[pixelOffset] == 0 &&
                        bytes[pixelOffset + 1] == 0 &&
                        bytes[pixelOffset + 2] == 0)
                    {
                        continue;
                    }

                    bytes[pixelOffset] = 0;
                    bytes[pixelOffset + 1] = 0;
                    bytes[pixelOffset + 2] = 0;
                    changed = true;
                }
            }

            if (!changed)
            {
                return null;
            }

            return CreateBitmapFromBytes(bitmap, bytes);
        }
    }
}
