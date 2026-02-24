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
        private async void OnBrowseOutputButtonClick(object? sender, RoutedEventArgs e)
        {
            FolderPickerOpenOptions options = new()
            {
                AllowMultiple = false,
                Title = "Select output folder"
            };

            if (Directory.Exists(_outputFolderTextBox.Text))
            {
                IStorageFolder? suggested = await StorageProvider.TryGetFolderFromPathAsync(_outputFolderTextBox.Text);
                if (suggested != null)
                {
                    options.SuggestedStartLocation = suggested;
                }
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Count == 0)
            {
                return;
            }

            string? selectedPath = folders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                selectedPath = folders[0].Path.LocalPath;
            }

            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                _outputFolderTextBox.Text = selectedPath;
            }
        }

        private void OnAutoCorrectButtonClick(object? sender, RoutedEventArgs e)
        {
            string resolutionText = (_resolutionComboBox.Text ?? _resolutionComboBox.SelectedItem?.ToString() ?? string.Empty).Trim();
            if (!TryParseInt(resolutionText, MinResolution, MaxResolution, "Resolution", out int resolution, out string resolutionError))
            {
                _statusTextBlock.Text = $"Auto-correct skipped: {resolutionError}";
                return;
            }

            if (!TryParseInt(_frameCountTextBox.Text, MinFrameCount, MaxFrameCount, "FrameCount", out int frameCount, out string frameError))
            {
                _statusTextBlock.Text = $"Auto-correct skipped: {frameError}";
                return;
            }

            bool switchFrameAdjusted = false;
            if (_project.ProjectType == InteractorProjectType.FlipSwitch &&
                (frameCount < MinFlipSwitchFrameCount || frameCount > MaxFlipSwitchFrameCount))
            {
                frameCount = DefaultFlipSwitchFrameCount;
                _frameCountTextBox.Text = frameCount.ToString(CultureInfo.InvariantCulture);
                switchFrameAdjusted = true;
            }

            bool indicatorFrameAdjusted = false;
            if (_project.ProjectType == InteractorProjectType.IndicatorLight &&
                frameCount != DefaultIndicatorLightFrameCount)
            {
                frameCount = DefaultIndicatorLightFrameCount;
                _frameCountTextBox.Text = frameCount.ToString(CultureInfo.InvariantCulture);
                indicatorFrameAdjusted = true;
            }

            int supersample = Math.Clamp(GetMinimumSupersampleScaleForResolution(resolution), MinSupersample, MaxSupersample);
            int maxSupersampleForDimension = Math.Max(1, MaxResolution / Math.Max(1, resolution));
            supersample = Math.Min(supersample, Math.Clamp(maxSupersampleForDimension, MinSupersample, MaxSupersample));

            string supersampleText = supersample.ToString(CultureInfo.InvariantCulture);
            _supersampleComboBox.SelectedItem = supersampleText;
            _supersampleComboBox.Text = supersampleText;

            if (!TryParseFloat(_paddingTextBox.Text, 0f, float.MaxValue, "Padding", out float padding, out _))
            {
                padding = 0f;
                _paddingTextBox.Text = "0";
            }

            int paddingPx = Math.Max(0, (int)MathF.Round(padding));
            if (_exportSpritesheetCheckBox.IsChecked == true &&
                (_spritesheetLayoutComboBox.SelectedItem as SpritesheetLayout? ?? SpritesheetLayout.Horizontal) == SpritesheetLayout.Horizontal &&
                WouldHorizontalLayoutOverflow(frameCount, resolution, paddingPx))
            {
                _spritesheetLayoutComboBox.SelectedItem = SpritesheetLayout.Grid;
            }

            _filterPresetComboBox.SelectedItem = ExportFilterPreset.None;

            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
            string switchFrameNote = switchFrameAdjusted
                ? $", switch frame count set to {DefaultFlipSwitchFrameCount}"
                : string.Empty;
            string indicatorFrameNote = indicatorFrameAdjusted
                ? $", indicator frame count set to {DefaultIndicatorLightFrameCount}"
                : string.Empty;
            _statusTextBlock.Text = $"Applied clean settings: {supersample}x supersampling with export-safe layout{switchFrameNote}{indicatorFrameNote}.";
        }

        private async void OnStartRenderButtonClick(object? sender, RoutedEventArgs e)
        {
            if (_isRendering)
            {
                return;
            }

            if (!CanUseGpuExport)
            {
                await ShowInfoDialogAsync(
                    "GPU export unavailable",
                    "Offscreen GPU rendering is unavailable, and export is currently GPU-only.");
                return;
            }

            if (!TryBuildRequest(out KnobExportSettings settings, out string outputRootFolder, out string baseName, out string validationError))
            {
                await ShowInfoDialogAsync("Invalid export settings", validationError);
                return;
            }

            _exportProgressBar.Value = 0d;
            _statusTextBlock.Text = "Preparing export...";
            SetRenderingState(true);

            _exportCts = new CancellationTokenSource();

            try
            {
                Func<int, int, ViewportCameraState, double?, SKBitmap?> gpuFrameProvider = (width, height, cameraState, dynamicLightAnimationTimeSeconds) =>
                    Dispatcher.UIThread
                        .InvokeAsync(
                            () =>
                            {
                                if (_gpuViewport != null &&
                                    _gpuViewport.TryRenderFrameToBitmap(
                                        width,
                                        height,
                                        cameraState,
                                        out SKBitmap? frame,
                                        dynamicLightAnimationTimeSeconds))
                                {
                                    return frame;
                                }

                                return null;
                            },
                            DispatcherPriority.Render)
                        .GetAwaiter()
                        .GetResult();

                if (!TryBuildManualOrbitAngles(out float baseYawDeg, out float basePitchDeg, out string orbitError))
                {
                    throw new InvalidOperationException(orbitError);
                }

                ViewportCameraState exportCameraState = _cameraState with
                {
                    OrbitYawDeg = baseYawDeg,
                    OrbitPitchDeg = basePitchDeg
                };
                Action<int, int>? frameStateApplier = null;

                // For non-rotary interactors, match export framing to the interactive preview fit pass.
                // This avoids blank sheets when manual camera pan/zoom no longer encloses the assembly.
                if (_project.ProjectType != InteractorProjectType.RotaryKnob)
                {
                    var stateSnapshot = await Dispatcher.UIThread.InvokeAsync(
                        () => (
                            ModelRotations: CaptureModelRotations(),
                            ToggleStateIndex: _project.ToggleStateIndex,
                            ToggleStateBlendPosition: _project.ToggleStateBlendPosition,
                            SliderThumbPositionNormalized: _project.SliderThumbPositionNormalized,
                            PushButtonPressAmountNormalized: _project.PushButtonPressAmountNormalized),
                        DispatcherPriority.Background);

                    var fitRequest = new PreviewRenderRequest(
                        FrameCount: settings.FrameCount,
                        Resolution: settings.Resolution,
                        SupersampleScale: settings.SupersampleScale,
                        RenderResolution: checked(settings.Resolution * settings.SupersampleScale),
                        Padding: settings.Padding,
                        CameraState: exportCameraState);

                    int fittingSamples = Math.Clamp(Math.Min(settings.FrameCount, 12), 4, 12);
                    ExportViewpoint[] resolvedViewpoints = ExportViewpointResolver.ResolveViewpoints(settings);
                    var fittedViewpoints = new System.Collections.Generic.List<ExportViewpoint>(resolvedViewpoints.Length);
                    for (int viewpointIndex = 0; viewpointIndex < resolvedViewpoints.Length; viewpointIndex++)
                    {
                        _exportCts.Token.ThrowIfCancellationRequested();
                        ExportViewpoint sourceViewpoint = resolvedViewpoints[viewpointIndex];
                        ViewportCameraState viewpointCameraState = ExportViewpointResolver.ApplyViewpoint(exportCameraState, sourceViewpoint);
                        ViewportCameraState fittedViewpointCameraState = await FitRotaryPreviewCameraAsync(
                            fitRequest,
                            viewpointCameraState,
                            stateSnapshot.ModelRotations,
                            stateSnapshot.ToggleStateIndex,
                            stateSnapshot.ToggleStateBlendPosition,
                            stateSnapshot.SliderThumbPositionNormalized,
                            stateSnapshot.PushButtonPressAmountNormalized,
                            fittingSamples,
                            _exportCts.Token);

                        fittedViewpoints.Add(new ExportViewpoint
                        {
                            Name = sourceViewpoint.Name,
                            FileTag = sourceViewpoint.FileTag,
                            Enabled = sourceViewpoint.Enabled,
                            Order = sourceViewpoint.Order,
                            UseAbsoluteCamera = true,
                            OrbitYawDeg = fittedViewpointCameraState.OrbitYawDeg,
                            OrbitPitchDeg = fittedViewpointCameraState.OrbitPitchDeg,
                            YawOffsetDeg = 0f,
                            PitchOffsetDeg = 0f,
                            OverrideZoom = true,
                            Zoom = fittedViewpointCameraState.Zoom,
                            OverridePan = true,
                            PanXPx = fittedViewpointCameraState.PanPx.X,
                            PanYPx = fittedViewpointCameraState.PanPx.Y
                        });
                    }

                    settings.ExportViewpoints = fittedViewpoints;

                    if (_project.ProjectType == InteractorProjectType.IndicatorLight)
                    {
                        // Preflight indicator framing with the exact fitted export cameras so we fail fast
                        // with a clear message instead of producing a blank spritesheet after a full pass.
                        float probeAngleStep = (2f * MathF.PI) / Math.Max(1, settings.FrameCount);
                        try
                        {
                            await Dispatcher.UIThread.InvokeAsync(
                                () => ApplyPreviewFrameState(
                                    0,
                                    settings.FrameCount,
                                    probeAngleStep,
                                    stateSnapshot.ModelRotations,
                                    stateSnapshot.ToggleStateIndex,
                                    stateSnapshot.ToggleStateBlendPosition,
                                    stateSnapshot.SliderThumbPositionNormalized,
                                    stateSnapshot.PushButtonPressAmountNormalized),
                                DispatcherPriority.Render);

                            double probeAnimationTimeSeconds = InteractorFrameTimeline.ResolveLoopAnimationTimeSeconds(0, settings.FrameCount);
                            foreach (ExportViewpoint fittedViewpoint in fittedViewpoints)
                            {
                                _exportCts.Token.ThrowIfCancellationRequested();
                                ViewportCameraState probeCamera = ExportViewpointResolver.ApplyViewpoint(exportCameraState, fittedViewpoint);
                                SKBitmap? probeFrame = await Dispatcher.UIThread.InvokeAsync(
                                    () =>
                                    {
                                        if (_gpuViewport != null &&
                                            _gpuViewport.TryRenderFrameToBitmap(
                                            settings.Resolution,
                                            settings.Resolution,
                                            probeCamera,
                                            out SKBitmap? frame,
                                            probeAnimationTimeSeconds))
                                        {
                                            return frame;
                                        }

                                        return null;
                                    },
                                    DispatcherPriority.Render);

                                if (probeFrame == null)
                                {
                                    throw new InvalidOperationException(
                                        $"Unable to capture indicator export probe for viewpoint '{fittedViewpoint.Name}'.");
                                }

                                using (probeFrame)
                                {
                                    if (!TryGetOpaqueBounds(probeFrame, 2, out _))
                                    {
                                        throw new InvalidOperationException(
                                            $"Rendered viewpoint '{fittedViewpoint.Name}' produced empty frames during indicator export preflight. Adjust camera/viewpoint framing and retry.");
                                    }
                                }
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
                    }

                    frameStateApplier = (frameIndex, frameCount) =>
                    {
                        float angleStep = (2f * MathF.PI) / Math.Max(1, frameCount);
                        Dispatcher.UIThread
                            .InvokeAsync(
                                () => ApplyPreviewFrameState(
                                    frameIndex,
                                    frameCount,
                                    angleStep,
                                    stateSnapshot.ModelRotations,
                                    stateSnapshot.ToggleStateIndex,
                                    stateSnapshot.ToggleStateBlendPosition,
                                    stateSnapshot.SliderThumbPositionNormalized,
                                    stateSnapshot.PushButtonPressAmountNormalized),
                                DispatcherPriority.Render)
                            .GetAwaiter()
                            .GetResult();
                    };
                }

                var exporter = new KnobExporter(_project, _orientation, exportCameraState, gpuFrameProvider, frameStateApplier);
                var progress = new Progress<KnobExportProgress>(UpdateProgress);
                KnobExportResult result = await exporter.ExportAsync(
                    settings,
                    outputRootFolder,
                    baseName,
                    progress,
                    _exportCts.Token);

                _exportProgressBar.Value = 1d;
                _statusTextBlock.Text = "Export complete.";

                bool shouldOpen = await ShowConfirmDialogAsync(
                    "Export complete",
                    "Export complete.\nOpen folder?");
                if (shouldOpen)
                {
                    try
                    {
                        OpenFolder(result.OutputDirectory);
                    }
                    catch (Exception ex)
                    {
                        await ShowInfoDialogAsync("Open folder failed", ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _statusTextBlock.Text = "Export cancelled.";
            }
            catch (Exception ex)
            {
                _statusTextBlock.Text = "Export failed.";
                await ShowInfoDialogAsync("Export failed", ex.Message);
            }
            finally
            {
                _exportCts?.Dispose();
                _exportCts = null;
                SetRenderingState(false);
            }
        }

        private void OnCancelButtonClick(object? sender, RoutedEventArgs e)
        {
            if (_isRendering)
            {
                _statusTextBlock.Text = "Cancelling...";
                _exportCts?.Cancel();
                return;
            }

            Close();
        }

        private void OnExportSpritesheetCheckedChanged(object? sender, RoutedEventArgs e)
        {
            UpdateSpritesheetLayoutEnabled();
            UpdateStartRenderAvailability();
        }

        private void OnExportOrbitVariantsCheckedChanged(object? sender, RoutedEventArgs e)
        {
            UpdateOrbitVariantControlsEnabled();
            UpdateStartRenderAvailability();
        }

        private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_isRendering)
            {
                _statusTextBlock.Text = "Cancelling...";
                _exportCts?.Cancel();
                e.Cancel = true;
                return;
            }

            _rotaryPreviewCts?.Cancel();
            _rotaryPreviewCts?.Dispose();
            _rotaryPreviewCts = null;
            CleanupRotaryPreviewTempPath();
        }

        private void UpdateProgress(KnobExportProgress progress)
        {
            double value = 0d;
            if (progress.TotalFrames > 0)
            {
                value = Math.Clamp((double)progress.CompletedFrames / progress.TotalFrames, 0d, 1d);
            }

            _exportProgressBar.Value = value;
            _statusTextBlock.Text = progress.Stage;
        }

        private void SetRenderingState(bool isRendering)
        {
            _isRendering = isRendering;
            _settingsPanel.IsEnabled = !isRendering;
            _autoCorrectButton.IsEnabled = !isRendering;
            bool enableRotaryPreviewControls =
                SupportsRotaryPreview &&
                !isRendering &&
                !_isBuildingRotaryPreview;
            _createRotaryPreviewButton.IsEnabled = enableRotaryPreviewControls;
            _rotaryPreviewVariantComboBox.IsEnabled = enableRotaryPreviewControls;
            _cancelButton.Content = isRendering ? "Cancel Render" : "Cancel";
            UpdateOrbitVariantControlsEnabled();

            if (!isRendering)
            {
                UpdateSpritesheetLayoutEnabled();
                UpdateStartRenderAvailability(preserveCurrentNonErrorStatus: true);
            }
            else
            {
                _startRenderButton.IsEnabled = false;
                _startRenderButton.Opacity = 0.55;
                ToolTip.SetTip(_startRenderButton, "Rendering in progress.");
            }
        }

        private void UpdateStartRenderAvailability(bool preserveCurrentNonErrorStatus = false)
        {
            if (_isRendering)
            {
                _startRenderButton.IsEnabled = false;
                _startRenderButton.Opacity = 0.55;
                ToolTip.SetTip(_startRenderButton, "Rendering in progress.");
                return;
            }

            const string gpuUnavailableMessage = "GPU offscreen rendering is unavailable. Export is GPU-only.";
            if (!CanUseGpuExport)
            {
                _startRenderButton.IsEnabled = false;
                _startRenderButton.Opacity = 0.55;
                _statusTextBlock.Text = gpuUnavailableMessage;
                ToolTip.SetTip(_startRenderButton, gpuUnavailableMessage);
                return;
            }

            if (!TryBuildRequest(out _, out _, out _, out string validationError))
            {
                string message = $"Cannot render: {validationError}";
                _startRenderButton.IsEnabled = false;
                _startRenderButton.Opacity = 0.55;
                _statusTextBlock.Text = message;
                ToolTip.SetTip(_startRenderButton, message);
                return;
            }

            _startRenderButton.IsEnabled = true;
            _startRenderButton.Opacity = 1.0;
            ToolTip.SetTip(_startRenderButton, "Ready to render.");

            if (preserveCurrentNonErrorStatus &&
                !string.IsNullOrWhiteSpace(_statusTextBlock.Text) &&
                !_statusTextBlock.Text.StartsWith("Cannot render:", StringComparison.Ordinal))
            {
                return;
            }

            _statusTextBlock.Text = "Ready to render.";
        }

        private void UpdateSpritesheetLayoutEnabled()
        {
            _spritesheetLayoutComboBox.IsEnabled = !_isRendering && _exportSpritesheetCheckBox.IsChecked == true;
        }

        private void UpdateOrbitVariantControlsEnabled()
        {
            bool enabled = !_isRendering && _exportOrbitVariantsCheckBox.IsChecked == true;
            _orbitYawOffsetTextBox.IsEnabled = enabled;
            _orbitPitchOffsetTextBox.IsEnabled = enabled;
        }
    }
}
