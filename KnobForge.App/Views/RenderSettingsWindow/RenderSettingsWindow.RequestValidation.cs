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
        private bool TryBuildPreviewRequest(out PreviewRenderRequest request, out string error)
        {
            request = default;
            error = string.Empty;

            if (!TryParseInt(_frameCountTextBox.Text, MinFrameCount, MaxFrameCount, "FrameCount", out int frameCount, out error))
            {
                return false;
            }
            if (!TryValidateProjectTypeFrameCount(frameCount, out error))
            {
                return false;
            }

            string resolutionText = (_resolutionTextBox.Text ?? string.Empty).Trim();
            if (!TryParseInt(resolutionText, MinResolution, MaxResolution, "Resolution", out int resolution, out error))
            {
                return false;
            }

            string supersampleText = (_supersampleComboBox.Text ?? _supersampleComboBox.SelectedItem?.ToString() ?? string.Empty).Trim();
            if (!TryParseInt(supersampleText, MinSupersample, MaxSupersample, "Supersampling", out int supersampleScale, out error))
            {
                return false;
            }

            int minimumSupersample = GetMinimumSupersampleScaleForResolution(resolution);
            if (supersampleScale < minimumSupersample)
            {
                error = $"Supersampling must be at least {minimumSupersample}x at {resolution}px for clean output.";
                return false;
            }

            int renderResolution = checked(resolution * supersampleScale);
            if (renderResolution > MaxResolution)
            {
                error = $"Resolution x Supersampling exceeds max {MaxResolution}px.";
                return false;
            }

            int columns = (int)Math.Ceiling(Math.Sqrt(frameCount));
            int rows = (int)Math.Ceiling(frameCount / (double)columns);
            long sheetWidth = (long)columns * resolution;
            long sheetHeight = (long)rows * resolution;
            if (sheetWidth > MaxResolution || sheetHeight > MaxResolution)
            {
                error = $"Interactive preview sheet would be {sheetWidth}x{sheetHeight}px. Reduce frame count or resolution for preview.";
                return false;
            }

            if (!TryParseFloat(_paddingTextBox.Text, 0f, float.MaxValue, "Padding", out float padding, out error))
            {
                return false;
            }

            if (!TryParseFloat(_cameraDistanceScaleTextBox.Text, 0.0001f, float.MaxValue, "CameraDistanceScale", out float cameraDistanceScale, out error))
            {
                return false;
            }

            if (!TryBuildManualOrbitAngles(out float baseYawDeg, out float basePitchDeg, out error))
            {
                return false;
            }

            float referenceRadius = GetSceneReferenceRadius();
            ViewportCameraState previewCamera = BuildPreviewCameraState(
                referenceRadius,
                resolution,
                renderResolution,
                padding,
                cameraDistanceScale,
                baseYawDeg,
                basePitchDeg);

            request = new PreviewRenderRequest(
                frameCount,
                resolution,
                supersampleScale,
                renderResolution,
                padding,
                previewCamera);
            return true;
        }

        private ViewportCameraState BuildPreviewCameraState(
            float referenceRadius,
            int outputResolution,
            int renderResolution,
            float padding,
            float cameraDistanceScale,
            float baseYawDeg,
            float basePitchDeg)
        {
            float resolutionScale = renderResolution / (float)Math.Max(1, outputResolution);
            float zoom = Math.Clamp(_cameraState.Zoom * resolutionScale, 0.2f, 32f);
            SKPoint pan = new(_cameraState.PanPx.X * resolutionScale, _cameraState.PanPx.Y * resolutionScale);
            zoom = MathF.Min(zoom, ComputeSafeZoomForFrame(referenceRadius, renderResolution, padding * resolutionScale, pan));

            if (zoom <= 0.0001f)
            {
                float contentPixels = MathF.Max(1f, renderResolution - (MathF.Max(0f, padding) * 2f));
                float fallbackZoom = contentPixels / MathF.Max(1f, MathF.Max(referenceRadius, cameraDistanceScale) * 2f);
                zoom = Math.Clamp(fallbackZoom, 0.2f, 32f);
            }

            return new ViewportCameraState(baseYawDeg, basePitchDeg, zoom, pan);
        }

        private float GetSceneReferenceRadius()
        {
            if (_gpuViewport != null)
            {
                float viewportReferenceRadius = _gpuViewport.GetCurrentSceneReferenceRadius();
                if (viewportReferenceRadius > 1f)
                {
                    return viewportReferenceRadius;
                }
            }

            float maxReferenceRadius = 1f;
            ModelNode? modelNode = _project.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
            maxReferenceRadius = MathF.Max(maxReferenceRadius, modelNode?.Radius ?? 1f);

            MetalMesh? mesh = MetalMeshBuilder.TryBuildFromProject(_project);
            if (mesh != null)
            {
                maxReferenceRadius = MathF.Max(maxReferenceRadius, mesh.ReferenceRadius);
            }

            CollarMesh? collarMesh = CollarMeshBuilder.TryBuildFromProject(_project);
            if (collarMesh != null)
            {
                maxReferenceRadius = MathF.Max(maxReferenceRadius, collarMesh.ReferenceRadius);
            }

            return maxReferenceRadius;
        }

        private static float ComputeSafeZoomForFrame(
            float referenceRadius,
            int renderResolution,
            float paddingPx,
            SKPoint panPx)
        {
            float radius = MathF.Max(1f, referenceRadius);
            float halfWidthAvailable = MathF.Max(1f, (renderResolution * 0.5f) - paddingPx - MathF.Abs(panPx.X));
            float halfHeightAvailable = MathF.Max(1f, (renderResolution * 0.5f) - paddingPx - MathF.Abs(panPx.Y));
            float halfSpan = MathF.Min(halfWidthAvailable, halfHeightAvailable);
            return MathF.Max(0.2f, (halfSpan * 0.96f) / radius);
        }

        private static bool WouldHorizontalLayoutOverflow(int frameCount, int resolution, int paddingPx)
        {
            long width = ((long)frameCount * resolution) + (((long)frameCount + 1L) * paddingPx);
            return width > MaxResolution;
        }

        private bool TryBuildCompressionSettings(out CompressionSettingsSnapshot snapshot, out string error)
        {
            snapshot = default;
            error = string.Empty;

            var selectedImageFormat = _outputImageFormatComboBox.SelectedItem as ExportImageFormat?;
            ExportImageFormat imageFormat = selectedImageFormat ?? ExportImageFormat.PngOptimized;
            var selectedPreset = _pngOptimizationPresetComboBox.SelectedItem as PngOptimizationPreset?;
            PngOptimizationPreset pngOptimizationPreset = selectedPreset ?? PngOptimizationPreset.Custom;

            if (!TryParseInt(_pngCompressionLevelTextBox.Text, 0, 9, "PNG Deflate Level", out int pngCompressionLevel, out error))
            {
                return false;
            }

            if (!TryParseInt(_pngMinimumSavingsKbTextBox.Text, 0, 1024 * 1024, "PNG Min Savings (KB)", out int minimumSavingsKb, out error))
            {
                return false;
            }

            if (!TryParseInt(_pngOpaqueRgbStepTextBox.Text, 1, 64, "Opaque RGB Step", out int opaqueRgbStep, out error) ||
                !TryParseInt(_pngOpaqueAlphaStepTextBox.Text, 1, 64, "Opaque Alpha Step", out int opaqueAlphaStep, out error) ||
                !TryParseInt(_pngTranslucentRgbStepTextBox.Text, 1, 64, "Translucent RGB Step", out int translucentRgbStep, out error) ||
                !TryParseInt(_pngTranslucentAlphaStepTextBox.Text, 1, 64, "Translucent Alpha Step", out int translucentAlphaStep, out error))
            {
                return false;
            }

            if (!TryParseInt(_pngTranslucentAlphaThresholdTextBox.Text, 0, 255, "Translucent Alpha Threshold", out int translucentAlphaThreshold, out error) ||
                !TryParseInt(_pngMaxOpaqueRgbDeltaTextBox.Text, 0, 255, "Max Opaque RGB Delta", out int maxOpaqueRgbDelta, out error) ||
                !TryParseInt(_pngMaxVisibleRgbDeltaTextBox.Text, 0, 255, "Max Visible RGB Delta", out int maxVisibleRgbDelta, out error) ||
                !TryParseInt(_pngMaxVisibleAlphaDeltaTextBox.Text, 0, 255, "Max Visible Alpha Delta", out int maxVisibleAlphaDelta, out error))
            {
                return false;
            }

            if (!TryParseFloat(_pngMeanVisibleLumaDeltaTextBox.Text, 0f, 255f, "Mean Visible Luma Delta", out float meanVisibleLumaDelta, out error) ||
                !TryParseFloat(_pngMeanVisibleAlphaDeltaTextBox.Text, 0f, 255f, "Mean Visible Alpha Delta", out float meanVisibleAlphaDelta, out error))
            {
                return false;
            }

            float webpLossyQuality = 90f;
            if ((_exportFramesCheckBox.IsChecked == true) &&
                imageFormat == ExportImageFormat.WebpLossy &&
                !TryParseFloat(_webpLossyQualityTextBox.Text, 0f, 100f, "WebP Lossy Quality", out webpLossyQuality, out error))
            {
                return false;
            }

            snapshot = new CompressionSettingsSnapshot(
                imageFormat,
                pngCompressionLevel,
                pngOptimizationPreset,
                checked(minimumSavingsKb * 1024),
                opaqueRgbStep,
                opaqueAlphaStep,
                translucentRgbStep,
                translucentAlphaStep,
                (byte)translucentAlphaThreshold,
                (byte)maxOpaqueRgbDelta,
                (byte)maxVisibleRgbDelta,
                (byte)maxVisibleAlphaDelta,
                meanVisibleLumaDelta,
                meanVisibleAlphaDelta,
                webpLossyQuality,
                _optimizeSpritesheetPngCheckBox.IsChecked == true);
            return true;
        }

        private bool TryBuildRequest(
            out KnobExportSettings settings,
            out string outputRootFolder,
            out string baseName,
            out string error)
        {
            settings = null!;
            outputRootFolder = string.Empty;
            baseName = string.Empty;
            error = string.Empty;

            if (!TryParseInt(_frameCountTextBox.Text, MinFrameCount, MaxFrameCount, "FrameCount", out int frameCount, out error))
            {
                return false;
            }
            if (!TryValidateProjectTypeFrameCount(frameCount, out error))
            {
                return false;
            }

            string resolutionText = (_resolutionTextBox.Text ?? string.Empty).Trim();
            if (!TryParseInt(resolutionText, MinResolution, MaxResolution, "Resolution", out int resolution, out error))
            {
                return false;
            }

            string supersampleText = (_supersampleComboBox.Text ?? _supersampleComboBox.SelectedItem?.ToString() ?? string.Empty).Trim();
            if (!TryParseInt(supersampleText, MinSupersample, MaxSupersample, "Supersampling", out int supersampleScale, out error))
            {
                return false;
            }

            int minimumSupersample = GetMinimumSupersampleScaleForResolution(resolution);
            if (supersampleScale < minimumSupersample)
            {
                error = $"Supersampling {supersampleScale}x is too low for {resolution}px output and will cause visible aliasing. Use {minimumSupersample}x or higher.";
                return false;
            }

            if (!TryParseFloat(_paddingTextBox.Text, 0f, float.MaxValue, "Padding", out float padding, out error))
            {
                return false;
            }

            if (!TryParseFloat(_cameraDistanceScaleTextBox.Text, 0.0001f, float.MaxValue, "CameraDistanceScale", out float cameraDistanceScale, out error))
            {
                return false;
            }

            if (!TryBuildManualOrbitAngles(out _, out _, out error))
            {
                return false;
            }

            bool exportOrbitVariants = _exportOrbitVariantsCheckBox.IsChecked == true;
            var orbitVariantDefaults = new KnobExportSettings();
            float orbitYawOffsetDeg = orbitVariantDefaults.OrbitVariantYawOffsetDeg;
            float orbitPitchOffsetDeg = orbitVariantDefaults.OrbitVariantPitchOffsetDeg;

            if (exportOrbitVariants)
            {
                if (!TryParseFloat(
                        _orbitYawOffsetTextBox.Text,
                        MinOrbitOffsetDeg,
                        MaxOrbitYawOffsetDeg,
                        "Orbit yaw offset",
                        out orbitYawOffsetDeg,
                        out error))
                {
                    return false;
                }

                if (!TryParseFloat(
                        _orbitPitchOffsetTextBox.Text,
                        MinOrbitOffsetDeg,
                        MaxOrbitPitchOffsetDeg,
                        "Orbit pitch offset",
                        out orbitPitchOffsetDeg,
                        out error))
                {
                    return false;
                }
            }

            bool exportFrames = _exportFramesCheckBox.IsChecked == true;
            bool exportSpritesheet = _exportSpritesheetCheckBox.IsChecked == true;
            if (!exportFrames && !exportSpritesheet)
            {
                error = "Enable at least one output type: frames and/or spritesheet.";
                return false;
            }

            baseName = (_baseNameTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseName))
            {
                error = "Base Name is required.";
                return false;
            }

            if (baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = "Base Name contains invalid file name characters.";
                return false;
            }

            outputRootFolder = (_outputFolderTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputRootFolder))
            {
                error = "Output folder is required.";
                return false;
            }

            try
            {
                outputRootFolder = Path.GetFullPath(outputRootFolder);
            }
            catch (Exception ex)
            {
                error = $"Output folder path is invalid: {ex.Message}";
                return false;
            }

            if (!Directory.Exists(outputRootFolder))
            {
                error = "Selected output folder does not exist.";
                return false;
            }

            var selectedLayout = _spritesheetLayoutComboBox.SelectedItem as SpritesheetLayout?;
            SpritesheetLayout layout = selectedLayout ?? SpritesheetLayout.Horizontal;

            var selectedFilter = _filterPresetComboBox.SelectedItem as ExportFilterPreset?;
            ExportFilterPreset filterPreset = selectedFilter ?? ExportFilterPreset.None;
            if (!TryBuildCompressionSettings(out CompressionSettingsSnapshot compressionSettings, out error))
            {
                return false;
            }

            ExportOutputStrategy strategy = _outputStrategyComboBox.SelectedItem is OutputStrategyOption option
                ? option.Definition.Strategy
                : ExportOutputStrategy.JuceFilmstripBestDefault;

            if (!TryBuildViewpointsFromEditor(out ExportViewpoint[] configuredViewpoints, out error))
            {
                return false;
            }

            settings = new KnobExportSettings
            {
                Strategy = strategy,
                FrameCount = frameCount,
                Resolution = resolution,
                SupersampleScale = supersampleScale,
                ExportIndividualFrames = exportFrames,
                ExportSpritesheet = exportSpritesheet,
                SpritesheetLayout = layout,
                Padding = padding,
                CameraDistanceScale = cameraDistanceScale,
                FilterPreset = filterPreset,
                ExportOrbitVariants = exportOrbitVariants,
                OrbitVariantYawOffsetDeg = orbitYawOffsetDeg,
                OrbitVariantPitchOffsetDeg = orbitPitchOffsetDeg,
                ExportViewpoints = configuredViewpoints.ToList()
            };
            compressionSettings.ApplyTo(settings);

            return true;
        }

        private bool TryValidateProjectTypeFrameCount(int frameCount, out string error)
        {
            error = string.Empty;
            switch (_project.ProjectType)
            {
                case InteractorProjectType.FlipSwitch:
                    if (frameCount >= MinFlipSwitchFrameCount && frameCount <= MaxFlipSwitchFrameCount)
                    {
                        return true;
                    }

                    error = $"Flip Switch exports require {MinFlipSwitchFrameCount}-{MaxFlipSwitchFrameCount} frames for snapped motion (recommended: {DefaultFlipSwitchFrameCount}).";
                    return false;
                case InteractorProjectType.IndicatorLight:
                    if (frameCount >= 2)
                    {
                        return true;
                    }

                    error = "Indicator Light exports require at least 2 frames to form a valid animation loop (recommended: 24).";
                    return false;
                default:
                    return true;
            }
        }

        private static ExportViewpoint[] BuildLegacyUiViewpoints(
            bool exportOrbitVariants,
            float orbitYawOffsetDeg,
            float orbitPitchOffsetDeg)
        {
            return ExportViewpointResolver.CreateLegacyViewpoints(
                exportOrbitVariants,
                orbitYawOffsetDeg,
                orbitPitchOffsetDeg);
        }

        private static int GetMinimumSupersampleScaleForResolution(int resolution)
        {
            if (resolution <= 128)
            {
                return 4;
            }

            if (resolution <= 512)
            {
                return 2;
            }

            return 1;
        }

        private bool TryBuildManualOrbitAngles(out float baseYawDeg, out float basePitchDeg, out string error)
        {
            if (!TryParseFloat(_previewBaseYawTextBox.Text, -180f, 180f, "Preview base yaw", out baseYawDeg, out error))
            {
                basePitchDeg = 0f;
                return false;
            }

            if (!TryParseFloat(_previewBasePitchTextBox.Text, -85f, 85f, "Preview base pitch", out basePitchDeg, out error))
            {
                return false;
            }

            return true;
        }
    }
}
