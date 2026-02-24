using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using KnobForge.App.Controls;
using KnobForge.Core;
using KnobForge.Core.Export;
using KnobForge.Core.Scene;
using KnobForge.Rendering;
using KnobForge.Rendering.GPU;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace KnobForge.App.Views
{
    public partial class RenderSettingsWindow : Window
    {
        private bool SupportsRotaryPreview =>
            _project.ProjectType == InteractorProjectType.RotaryKnob ||
            _project.ProjectType == InteractorProjectType.ThumbSlider ||
            _project.ProjectType == InteractorProjectType.FlipSwitch ||
            _project.ProjectType == InteractorProjectType.PushButton ||
            _project.ProjectType == InteractorProjectType.IndicatorLight;

        private string InteractivePreviewModeDisplayName => _project.ProjectType switch
        {
            InteractorProjectType.RotaryKnob => "Rotary",
            InteractorProjectType.ThumbSlider => "Slider",
            InteractorProjectType.FlipSwitch => "Switch",
            InteractorProjectType.PushButton => "Button",
            InteractorProjectType.IndicatorLight => "Indicator",
            _ => "Interactor"
        };

        private string BuildInteractivePreviewHelpText()
        {
            return _project.ProjectType switch
            {
                InteractorProjectType.RotaryKnob => "Choose perspective, then click Create Rotary Preview. Drag to spin through output frames.",
                InteractorProjectType.ThumbSlider => "Choose perspective, then click Create Slider Preview. Drag to scrub thumb travel frames.",
                InteractorProjectType.FlipSwitch => "Choose perspective, then click Create Switch Preview. Drag to scrub state frames.",
                InteractorProjectType.PushButton => "Choose perspective, then click Create Button Preview. Drag to scrub press-depth frames.",
                InteractorProjectType.IndicatorLight => "Choose perspective, then click Create Indicator Preview. Drag to scrub the loop and validate emissive timing/framing.",
                _ => "Interactive preview is unavailable for this project type."
            };
        }

        private void ConfigureRotaryPreviewAvailability()
        {
            bool supportsRotaryPreview = SupportsRotaryPreview;
            _rotaryPreviewSection.IsVisible = supportsRotaryPreview;
            _interactivePreviewTitleTextBlock.Text = supportsRotaryPreview
                ? $"Interactive {InteractivePreviewModeDisplayName} Preview (1:1)"
                : "Interactive Preview (1:1)";
            _createRotaryPreviewButton.Content = supportsRotaryPreview
                ? $"Create {InteractivePreviewModeDisplayName} Preview"
                : "Create Preview";
            _createRotaryPreviewButton.IsEnabled = supportsRotaryPreview && !_isRendering && !_isBuildingRotaryPreview;
            _rotaryPreviewVariantComboBox.IsEnabled = supportsRotaryPreview && !_isRendering && !_isBuildingRotaryPreview;
            _rotaryPreviewKnob.IsEnabled = false;
            _rotaryPreviewValueTextBlock.Text = supportsRotaryPreview ? "Frame 1 / 1" : "Not available for this project type";
            _rotaryPreviewInfoTextBlock.Text = supportsRotaryPreview
                ? BuildInteractivePreviewHelpText()
                : "Interactive preview is only shown for supported interactor project types.";
        }

        private void MarkRotaryPreviewDirty()
        {
            if (!SupportsRotaryPreview)
            {
                return;
            }

            if (_isBuildingRotaryPreview || !string.IsNullOrWhiteSpace(_rotaryPreviewTempPath))
            {
                _rotaryPreviewInfoTextBlock.Text = $"Settings changed. Click Create {InteractivePreviewModeDisplayName} Preview to refresh.";
            }
        }

        private void OnRotaryPreviewVariantSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            MarkRotaryPreviewDirty();
        }

        private void OnRotaryPreviewKnobPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == RangeBase.ValueProperty ||
                e.Property == RangeBase.MinimumProperty ||
                e.Property == RangeBase.MaximumProperty)
            {
                UpdateRotaryPreviewValueText();
            }
        }

        private async void OnCreateRotaryPreviewButtonClick(object? sender, RoutedEventArgs e)
        {
            if (!SupportsRotaryPreview)
            {
                _rotaryPreviewInfoTextBlock.Text = "Interactive preview is unavailable for this project type.";
                return;
            }

            if (_isBuildingRotaryPreview)
            {
                return;
            }

            if (!CanUseGpuExport)
            {
                _rotaryPreviewInfoTextBlock.Text = "Interactive preview unavailable: GPU offscreen rendering is unavailable.";
                return;
            }

            if (!TryBuildPreviewRequest(out PreviewRenderRequest request, out string validationError))
            {
                _rotaryPreviewInfoTextBlock.Text = $"Cannot build interactive preview: {validationError}";
                return;
            }

            var variant = _rotaryPreviewVariantComboBox.SelectedItem as PreviewVariantOption
                ?? _previewVariantOptions[0];

            _rotaryPreviewCts?.Cancel();
            _rotaryPreviewCts?.Dispose();
            _rotaryPreviewCts = new CancellationTokenSource();

            SetRotaryPreviewBusy(true, $"Generating {variant.DisplayName} preview...");

            try
            {
                RotaryPreviewSheet previewSheet = await BuildRotaryPreviewSheetAsync(request, variant, _rotaryPreviewCts.Token);
                CleanupRotaryPreviewTempPath();
                _rotaryPreviewTempPath = previewSheet.SpriteSheetPath;
                ApplyRotaryPreviewSheet(previewSheet);
                _rotaryPreviewInfoTextBlock.Text = $"Ready: {variant.DisplayName}, {previewSheet.FrameCount} frames at {previewSheet.FrameSizePx}px. Drag to scrub frames.";
            }
            catch (OperationCanceledException)
            {
                _rotaryPreviewInfoTextBlock.Text = "Interactive preview canceled.";
            }
            catch (Exception ex)
            {
                _rotaryPreviewInfoTextBlock.Text = $"Interactive preview failed: {ex.Message}";
            }
            finally
            {
                _rotaryPreviewCts?.Dispose();
                _rotaryPreviewCts = null;
                SetRotaryPreviewBusy(false);
            }
        }

        private void SetRotaryPreviewBusy(bool isBusy, string? status = null)
        {
            _isBuildingRotaryPreview = isBusy;
            _createRotaryPreviewButton.Content = SupportsRotaryPreview
                ? $"Create {InteractivePreviewModeDisplayName} Preview"
                : "Create Preview";
            _createRotaryPreviewButton.IsEnabled = SupportsRotaryPreview && !isBusy && !_isRendering;
            _rotaryPreviewVariantComboBox.IsEnabled = SupportsRotaryPreview && !isBusy && !_isRendering;
            if (!string.IsNullOrWhiteSpace(status))
            {
                _rotaryPreviewInfoTextBlock.Text = status;
            }
        }

        private async Task<RotaryPreviewSheet> BuildRotaryPreviewSheetAsync(
            PreviewRenderRequest request,
            PreviewVariantOption variant,
            CancellationToken cancellationToken)
        {
            if (_gpuViewport == null)
            {
                throw new InvalidOperationException("GPU viewport is unavailable.");
            }

            if (!TryBuildViewpointsFromEditor(out ExportViewpoint[] previewViewpoints, out string viewpointError))
            {
                throw new InvalidOperationException(viewpointError);
            }

            ExportViewpoint selectedViewpoint = previewViewpoints
                .FirstOrDefault(v => string.Equals(v.FileTag, variant.FileTag, StringComparison.OrdinalIgnoreCase))
                ?? previewViewpoints[0];

            ViewportCameraState cameraState = ExportViewpointResolver.ApplyViewpoint(request.CameraState, selectedViewpoint);

            int frameCount = request.FrameCount;
            int resolution = request.Resolution;
            int columns = (int)Math.Ceiling(Math.Sqrt(frameCount));
            int rows = (int)Math.Ceiling(frameCount / (double)columns);

            using var sheetBitmap = new SKBitmap(new SKImageInfo(
                checked(columns * resolution),
                checked(rows * resolution),
                SKColorType.Bgra8888,
                SKAlphaType.Premul));
            using var sheetCanvas = new SKCanvas(sheetBitmap);
            sheetCanvas.Clear(new SKColor(0, 0, 0, 0));

            using var frameBitmap = new SKBitmap(new SKImageInfo(
                resolution,
                resolution,
                SKColorType.Bgra8888,
                SKAlphaType.Premul));
            using var frameCanvas = new SKCanvas(frameBitmap);
            using var downsamplePaint = new SKPaint
            {
                BlendMode = SKBlendMode.Src,
                IsAntialias = true,
                IsDither = true
            };
            using var sheetPaint = new SKPaint
            {
                BlendMode = SKBlendMode.Src,
                IsAntialias = false
            };
            SKSamplingOptions downsampleSampling = new(new SKCubicResampler(1f / 3f, 1f / 3f));
            SKSamplingOptions directSampling = new(SKFilterMode.Linear, SKMipmapMode.None);

            var stateSnapshot = await Dispatcher.UIThread.InvokeAsync(
                () => (
                    ModelRotations: CaptureModelRotations(),
                    ToggleStateIndex: _project.ToggleStateIndex,
                    ToggleStateBlendPosition: _project.ToggleStateBlendPosition,
                    SliderThumbPositionNormalized: _project.SliderThumbPositionNormalized,
                    PushButtonPressAmountNormalized: _project.PushButtonPressAmountNormalized),
                DispatcherPriority.Background);

            float angleStep = 2f * MathF.PI / frameCount;
            int progressStep = Math.Max(1, frameCount / 8);
            bool anyOpaqueFrame = false;
            try
            {
                int fittingSamples = Math.Clamp(Math.Min(frameCount, 12), 4, 12);
                cameraState = await FitRotaryPreviewCameraAsync(
                    request,
                    cameraState,
                    stateSnapshot.ModelRotations,
                    stateSnapshot.ToggleStateIndex,
                    stateSnapshot.ToggleStateBlendPosition,
                    stateSnapshot.SliderThumbPositionNormalized,
                    stateSnapshot.PushButtonPressAmountNormalized,
                    fittingSamples,
                    cancellationToken);

                for (int i = 0; i < frameCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (i == 0 || i == frameCount - 1 || ((i + 1) % progressStep) == 0)
                    {
                        int progress = i + 1;
                        await Dispatcher.UIThread.InvokeAsync(
                            () => _rotaryPreviewInfoTextBlock.Text = $"Generating {variant.DisplayName} preview... {progress}/{frameCount}",
                            DispatcherPriority.Background);
                    }

                    await Dispatcher.UIThread.InvokeAsync(
                        () => ApplyPreviewFrameState(
                            i,
                            frameCount,
                            angleStep,
                            stateSnapshot.ModelRotations,
                            stateSnapshot.ToggleStateIndex,
                            stateSnapshot.ToggleStateBlendPosition,
                            stateSnapshot.SliderThumbPositionNormalized,
                            stateSnapshot.PushButtonPressAmountNormalized),
                        DispatcherPriority.Render);

                    double animationTimeSeconds = _project.ProjectType == InteractorProjectType.IndicatorLight
                        ? InteractorFrameTimeline.ResolveLoopAnimationTimeSeconds(i, frameCount)
                        : InteractorFrameTimeline.ResolveAnimationTimeSeconds(i, frameCount);
                    SKBitmap? gpuFrame = await Dispatcher.UIThread.InvokeAsync(
                        () =>
                        {
                            if (_gpuViewport.TryRenderFrameToBitmap(
                                request.RenderResolution,
                                request.RenderResolution,
                                cameraState,
                                out SKBitmap? frame,
                                animationTimeSeconds))
                            {
                                return frame;
                            }

                            return null;
                        },
                        DispatcherPriority.Render);

                    if (gpuFrame == null)
                    {
                        throw new InvalidOperationException("GPU frame capture failed while building rotary preview.");
                    }

                    using (gpuFrame)
                    using (SKImage sourceImage = SKImage.FromBitmap(gpuFrame))
                    {
                        frameCanvas.Clear(new SKColor(0, 0, 0, 0));
                        if (request.SupersampleScale > 1 ||
                            gpuFrame.Width != resolution ||
                            gpuFrame.Height != resolution)
                        {
                            frameCanvas.DrawImage(
                                sourceImage,
                                new SKRect(0, 0, gpuFrame.Width, gpuFrame.Height),
                                new SKRect(0, 0, resolution, resolution),
                                downsampleSampling,
                                downsamplePaint);
                        }
                        else
                        {
                            frameCanvas.DrawImage(
                                sourceImage,
                                new SKRect(0, 0, resolution, resolution),
                                directSampling,
                                downsamplePaint);
                        }
                    }

                    int col = i % columns;
                    int row = i / columns;
                    sheetCanvas.DrawBitmap(frameBitmap, col * resolution, row * resolution, sheetPaint);
                    anyOpaqueFrame |= TryGetOpaqueBounds(frameBitmap, 2, out _);
                }

                if (!anyOpaqueFrame)
                {
                    throw new InvalidOperationException(
                        "Interactive preview captured empty frames. Recenter the preview camera and try again.");
                }
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(
                    () =>
                    {
                        RestoreModelRotations(stateSnapshot.ModelRotations);
                        _project.ToggleStateIndex = stateSnapshot.ToggleStateIndex;
                        _project.ToggleStateBlendPosition = stateSnapshot.ToggleStateBlendPosition;
                        _project.SliderThumbPositionNormalized = stateSnapshot.SliderThumbPositionNormalized;
                        _project.PushButtonPressAmountNormalized = stateSnapshot.PushButtonPressAmountNormalized;
                    },
                    DispatcherPriority.Render);
            }

            string outputPath = CreateRotaryPreviewTempPath();
            using SKData pngData = sheetBitmap.Encode(SKEncodedImageFormat.Png, 100);
            using FileStream outputStream = File.Create(outputPath);
            pngData.SaveTo(outputStream);

            return new RotaryPreviewSheet(outputPath, frameCount, columns, resolution);
        }

        private async Task<ViewportCameraState> FitRotaryPreviewCameraAsync(
            PreviewRenderRequest request,
            ViewportCameraState cameraState,
            ModelRotationSnapshot[] snapshots,
            int originalToggleStateIndex,
            float originalToggleStateBlendPosition,
            float originalSliderThumbPositionNormalized,
            float originalPushButtonPressAmountNormalized,
            int sampleCount,
            CancellationToken cancellationToken)
        {
            if (_gpuViewport == null || snapshots.Length == 0)
            {
                return cameraState;
            }

            float marginPx = MathF.Max(4f, request.Resolution * 0.04f);
            const int maxFitIterations = 5;

            try
            {
                float angleStep = 2f * MathF.PI / Math.Max(1, request.FrameCount);
                for (int iteration = 0; iteration < maxFitIterations; iteration++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    float fitScale = 1f;
                    bool iterationSawOpaque = false;
                    for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int sampleFrameIndex = sampleCount <= 1
                            ? 0
                            : (int)MathF.Round((sampleIndex / MathF.Max(1f, sampleCount - 1f)) * MathF.Max(0, request.FrameCount - 1));

                        await Dispatcher.UIThread.InvokeAsync(
                            () => ApplyPreviewFrameState(
                                sampleFrameIndex,
                                request.FrameCount,
                                angleStep,
                                snapshots,
                                originalToggleStateIndex,
                                originalToggleStateBlendPosition,
                                originalSliderThumbPositionNormalized,
                                originalPushButtonPressAmountNormalized),
                            DispatcherPriority.Render);

                        double animationTimeSeconds = _project.ProjectType == InteractorProjectType.IndicatorLight
                            ? InteractorFrameTimeline.ResolveLoopAnimationTimeSeconds(sampleFrameIndex, request.FrameCount)
                            : InteractorFrameTimeline.ResolveAnimationTimeSeconds(sampleFrameIndex, request.FrameCount);
                        SKBitmap? sampleBitmap = await Dispatcher.UIThread.InvokeAsync(
                            () =>
                            {
                                if (_gpuViewport.TryRenderFrameToBitmap(
                                    request.Resolution,
                                    request.Resolution,
                                    cameraState,
                                    out SKBitmap? frame,
                                    animationTimeSeconds))
                                {
                                    return frame;
                                }

                                return null;
                            },
                            DispatcherPriority.Render);

                        if (sampleBitmap == null)
                        {
                            continue;
                        }

                        using (sampleBitmap)
                        {
                            if (!TryGetOpaqueBounds(sampleBitmap, 2, out PixelAlphaBounds bounds))
                            {
                                continue;
                            }
                            iterationSawOpaque = true;

                            float frameMin = 0f;
                            float frameMaxX = request.Resolution - 1f;
                            float frameMaxY = request.Resolution - 1f;
                            float centerX = (frameMin + frameMaxX) * 0.5f;
                            float centerY = (frameMin + frameMaxY) * 0.5f;

                            float availableLeft = MathF.Max(1f, centerX - marginPx);
                            float availableRight = MathF.Max(1f, (frameMaxX - marginPx) - centerX);
                            float availableTop = MathF.Max(1f, centerY - marginPx);
                            float availableBottom = MathF.Max(1f, (frameMaxY - marginPx) - centerY);

                            float usedLeft = MathF.Max(1f, centerX - bounds.MinX);
                            float usedRight = MathF.Max(1f, bounds.MaxX - centerX);
                            float usedTop = MathF.Max(1f, centerY - bounds.MinY);
                            float usedBottom = MathF.Max(1f, bounds.MaxY - centerY);

                            float scaleLeft = availableLeft / usedLeft;
                            float scaleRight = availableRight / usedRight;
                            float scaleTop = availableTop / usedTop;
                            float scaleBottom = availableBottom / usedBottom;
                            float frameScale = MathF.Min(MathF.Min(scaleLeft, scaleRight), MathF.Min(scaleTop, scaleBottom));
                            fitScale = MathF.Min(fitScale, frameScale);
                        }
                    }

                    if (!iterationSawOpaque)
                    {
                        float paddingScale = request.RenderResolution / (float)Math.Max(1, request.Resolution);
                        float paddingPx = MathF.Max(0f, request.Padding) * paddingScale;
                        float safeZoom = ComputeSafeZoomForFrame(
                            GetSceneReferenceRadius(),
                            request.RenderResolution,
                            paddingPx,
                            new SKPoint(0f, 0f));
                        cameraState = cameraState with
                        {
                            Zoom = Math.Clamp(safeZoom, 0.2f, 32f),
                            PanPx = new SKPoint(0f, 0f)
                        };
                        continue;
                    }

                    if (fitScale >= 0.998f)
                    {
                        break;
                    }

                    float appliedScale = Math.Clamp(fitScale * 0.985f, 0.65f, 0.995f);
                    cameraState = cameraState with
                    {
                        Zoom = MathF.Max(0.2f, cameraState.Zoom * appliedScale)
                    };
                }
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(
                    () =>
                    {
                        RestoreModelRotations(snapshots);
                        _project.ToggleStateIndex = originalToggleStateIndex;
                        _project.ToggleStateBlendPosition = originalToggleStateBlendPosition;
                        _project.SliderThumbPositionNormalized = originalSliderThumbPositionNormalized;
                        _project.PushButtonPressAmountNormalized = originalPushButtonPressAmountNormalized;
                    },
                    DispatcherPriority.Render);
            }

            return cameraState;
        }

        private static bool TryGetOpaqueBounds(SKBitmap bitmap, byte alphaThreshold, out PixelAlphaBounds bounds)
        {
            int minX = bitmap.Width;
            int minY = bitmap.Height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).Alpha <= alphaThreshold)
                    {
                        continue;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
            {
                bounds = default;
                return false;
            }

            bounds = new PixelAlphaBounds(minX, minY, maxX, maxY);
            return true;
        }

        private ModelRotationSnapshot[] CaptureModelRotations()
        {
            return _project.SceneRoot.Children
                .OfType<ModelNode>()
                .Select(model => new ModelRotationSnapshot(model, model.RotationRadians))
                .ToArray();
        }

        private void ApplyModelRotationDelta(ModelRotationSnapshot[] snapshots, float angleDeltaRadians)
        {
            for (int i = 0; i < snapshots.Length; i++)
            {
                snapshots[i].Model.RotationRadians = snapshots[i].RotationRadians + angleDeltaRadians;
            }
        }

        private void RestoreModelRotations(ModelRotationSnapshot[] snapshots)
        {
            for (int i = 0; i < snapshots.Length; i++)
            {
                snapshots[i].Model.RotationRadians = snapshots[i].RotationRadians;
            }
        }

        private void ApplyPreviewFrameState(
            int frameIndex,
            int frameCount,
            float angleStep,
            ModelRotationSnapshot[] snapshots,
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
                    ApplyModelRotationDelta(snapshots, angle);
                    _project.ToggleStateIndex = originalToggleStateIndex;
                    _project.ToggleStateBlendPosition = originalToggleStateBlendPosition;
                    _project.SliderThumbPositionNormalized = originalSliderThumbPositionNormalized;
                    _project.PushButtonPressAmountNormalized = originalPushButtonPressAmountNormalized;
                    break;
                }
                case InteractorProjectType.FlipSwitch:
                {
                    RestoreModelRotations(snapshots);
                    float toggleBlendPosition = InteractorFrameTimeline.ResolveToggleBlendPosition(frameIndex, frameCount, _project.ToggleStateCount);
                    _project.ToggleStateBlendPosition = toggleBlendPosition;
                    _project.ToggleStateIndex = InteractorFrameTimeline.ResolveToggleStateIndex(frameIndex, frameCount, _project.ToggleStateCount);
                    _project.SliderThumbPositionNormalized = originalSliderThumbPositionNormalized;
                    _project.PushButtonPressAmountNormalized = originalPushButtonPressAmountNormalized;
                    break;
                }
                case InteractorProjectType.ThumbSlider:
                {
                    RestoreModelRotations(snapshots);
                    _project.ToggleStateIndex = originalToggleStateIndex;
                    _project.ToggleStateBlendPosition = originalToggleStateBlendPosition;
                    _project.SliderThumbPositionNormalized = InteractorFrameTimeline.ResolveNormalizedProgress(frameIndex, frameCount);
                    _project.PushButtonPressAmountNormalized = originalPushButtonPressAmountNormalized;
                    break;
                }
                case InteractorProjectType.PushButton:
                {
                    RestoreModelRotations(snapshots);
                    _project.ToggleStateIndex = originalToggleStateIndex;
                    _project.ToggleStateBlendPosition = originalToggleStateBlendPosition;
                    _project.SliderThumbPositionNormalized = originalSliderThumbPositionNormalized;
                    _project.PushButtonPressAmountNormalized = InteractorFrameTimeline.ResolveNormalizedProgress(frameIndex, frameCount);
                    break;
                }
                default:
                {
                    RestoreModelRotations(snapshots);
                    _project.ToggleStateIndex = originalToggleStateIndex;
                    _project.ToggleStateBlendPosition = originalToggleStateBlendPosition;
                    _project.SliderThumbPositionNormalized = originalSliderThumbPositionNormalized;
                    _project.PushButtonPressAmountNormalized = originalPushButtonPressAmountNormalized;
                    break;
                }
            }
        }

        private void ApplyRotaryPreviewSheet(RotaryPreviewSheet sheet)
        {
            _rotaryPreviewKnob.SpriteSheetPath = sheet.SpriteSheetPath;
            _rotaryPreviewKnob.FrameCount = sheet.FrameCount;
            _rotaryPreviewKnob.ColumnCount = sheet.ColumnCount;
            _rotaryPreviewKnob.FrameWidth = sheet.FrameSizePx;
            _rotaryPreviewKnob.FrameHeight = sheet.FrameSizePx;
            _rotaryPreviewKnob.FramePadding = 0;
            _rotaryPreviewKnob.FrameStartX = 0;
            _rotaryPreviewKnob.FrameStartY = 0;
            _rotaryPreviewKnob.Minimum = 0d;
            _rotaryPreviewKnob.Maximum = Math.Max(1d, sheet.FrameCount - 1d);
            _rotaryPreviewKnob.KnobDiameter = sheet.FrameSizePx;
            _rotaryPreviewKnob.Value = 0d;
            _rotaryPreviewKnob.IsEnabled = true;
            UpdateRotaryPreviewValueText();
        }

        private void UpdateRotaryPreviewValueText()
        {
            int maxFrame = (int)Math.Max(1, Math.Round(_rotaryPreviewKnob.Maximum));
            int frameIndex = (int)Math.Round(Math.Clamp(_rotaryPreviewKnob.Value, _rotaryPreviewKnob.Minimum, _rotaryPreviewKnob.Maximum)) + 1;
            frameIndex = Math.Clamp(frameIndex, 1, maxFrame + 1);
            _rotaryPreviewValueTextBlock.Text = $"Frame {frameIndex} / {maxFrame + 1}";
        }

        private static string CreateRotaryPreviewTempPath()
        {
            string folder = Path.Combine(Path.GetTempPath(), "KnobForge", "rotary-preview");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"rotary_preview_{Guid.NewGuid():N}.png");
        }

        private void CleanupRotaryPreviewTempPath()
        {
            if (string.IsNullOrWhiteSpace(_rotaryPreviewTempPath))
            {
                return;
            }

            try
            {
                if (File.Exists(_rotaryPreviewTempPath))
                {
                    File.Delete(_rotaryPreviewTempPath);
                }
            }
            catch
            {
            }

            _rotaryPreviewTempPath = null;
        }
    }
}
