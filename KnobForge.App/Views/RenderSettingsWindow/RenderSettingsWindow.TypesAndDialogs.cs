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
        private readonly record struct PreviewRenderRequest(
            int FrameCount,
            int Resolution,
            int SupersampleScale,
            int RenderResolution,
            float Padding,
            ViewportCameraState CameraState);

        private readonly record struct RotaryPreviewSheet(
            string SpriteSheetPath,
            int FrameCount,
            int ColumnCount,
            int FrameSizePx,
            long EncodedBytes);

        private readonly record struct CompressionSettingsSnapshot(
            ExportImageFormat ImageFormat,
            int PngCompressionLevel,
            PngOptimizationPreset PngOptimizationPreset,
            int PngOptimizationMinimumSavingsBytes,
            int PngOpaqueRgbStep,
            int PngOpaqueAlphaStep,
            int PngTranslucentRgbStep,
            int PngTranslucentAlphaStep,
            byte PngTranslucentAlphaThreshold,
            byte PngMaxOpaqueRgbDelta,
            byte PngMaxVisibleRgbDelta,
            byte PngMaxVisibleAlphaDelta,
            float PngMeanVisibleLumaDelta,
            float PngMeanVisibleAlphaDelta,
            float WebpLossyQuality,
            bool OptimizeSpritesheetPng)
        {
            public void ApplyTo(KnobExportSettings settings)
            {
                settings.ImageFormat = ImageFormat;
                settings.PngCompressionLevel = PngCompressionLevel;
                settings.PngOptimizationPreset = PngOptimizationPreset;
                settings.PngOptimizationMinimumSavingsBytes = PngOptimizationMinimumSavingsBytes;
                settings.PngOpaqueRgbStep = PngOpaqueRgbStep;
                settings.PngOpaqueAlphaStep = PngOpaqueAlphaStep;
                settings.PngTranslucentRgbStep = PngTranslucentRgbStep;
                settings.PngTranslucentAlphaStep = PngTranslucentAlphaStep;
                settings.PngTranslucentAlphaThreshold = PngTranslucentAlphaThreshold;
                settings.PngMaxOpaqueRgbDelta = PngMaxOpaqueRgbDelta;
                settings.PngMaxVisibleRgbDelta = PngMaxVisibleRgbDelta;
                settings.PngMaxVisibleAlphaDelta = PngMaxVisibleAlphaDelta;
                settings.PngMeanVisibleLumaDelta = PngMeanVisibleLumaDelta;
                settings.PngMeanVisibleAlphaDelta = PngMeanVisibleAlphaDelta;
                settings.WebpLossyQuality = WebpLossyQuality;
                settings.OptimizeSpritesheetPng = OptimizeSpritesheetPng;
            }
        }

        private readonly record struct ModelRotationSnapshot(
            ModelNode Model,
            float RotationRadians);

        private readonly record struct PixelAlphaBounds(
            int MinX,
            int MinY,
            int MaxX,
            int MaxY);

        private sealed class PreviewVariantOption
        {
            public PreviewVariantOption(string fileTag, string displayName)
            {
                FileTag = fileTag;
                DisplayName = displayName;
            }

            public string FileTag { get; }

            public string DisplayName { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class ViewpointEditorItem
        {
            public Guid Id { get; } = Guid.NewGuid();
            public string Name { get; set; } = "Primary";
            public string FileTag { get; set; } = string.Empty;
            public bool Enabled { get; set; } = true;
            public int Order { get; set; }
            public bool UseAbsoluteCamera { get; set; }
            public float YawDeg { get; set; }
            public float PitchDeg { get; set; }
            public bool OverrideZoom { get; set; }
            public float Zoom { get; set; } = 1f;
            public bool OverridePan { get; set; }
            public float PanXPx { get; set; }
            public float PanYPx { get; set; }

            public ExportViewpoint ToExportViewpoint()
            {
                return new ExportViewpoint
                {
                    Name = Name,
                    FileTag = FileTag,
                    Enabled = Enabled,
                    Order = Order,
                    UseAbsoluteCamera = UseAbsoluteCamera,
                    OrbitYawDeg = UseAbsoluteCamera ? YawDeg : 0f,
                    OrbitPitchDeg = UseAbsoluteCamera ? PitchDeg : 0f,
                    YawOffsetDeg = UseAbsoluteCamera ? 0f : YawDeg,
                    PitchOffsetDeg = UseAbsoluteCamera ? 0f : PitchDeg,
                    OverrideZoom = OverrideZoom,
                    Zoom = Zoom,
                    OverridePan = OverridePan,
                    PanXPx = PanXPx,
                    PanYPx = PanYPx
                };
            }

            public static ViewpointEditorItem FromExportViewpoint(ExportViewpoint viewpoint, int order)
            {
                return new ViewpointEditorItem
                {
                    Name = string.IsNullOrWhiteSpace(viewpoint.Name) ? $"View {order + 1}" : viewpoint.Name,
                    FileTag = viewpoint.FileTag ?? string.Empty,
                    Enabled = viewpoint.Enabled,
                    Order = order,
                    UseAbsoluteCamera = viewpoint.UseAbsoluteCamera,
                    YawDeg = viewpoint.UseAbsoluteCamera ? viewpoint.OrbitYawDeg : viewpoint.YawOffsetDeg,
                    PitchDeg = viewpoint.UseAbsoluteCamera ? viewpoint.OrbitPitchDeg : viewpoint.PitchOffsetDeg,
                    OverrideZoom = viewpoint.OverrideZoom,
                    Zoom = viewpoint.OverrideZoom ? viewpoint.Zoom : 1f,
                    OverridePan = viewpoint.OverridePan,
                    PanXPx = viewpoint.OverridePan ? viewpoint.PanXPx : 0f,
                    PanYPx = viewpoint.OverridePan ? viewpoint.PanYPx : 0f
                };
            }

            public override string ToString()
            {
                string tag = string.IsNullOrWhiteSpace(FileTag) ? "<primary>" : FileTag;
                string mode = UseAbsoluteCamera ? "abs" : "offset";
                string state = Enabled ? "on" : "off";
                return $"{Name} [{state}] ({mode}, {tag})";
            }
        }

        private sealed class OutputStrategyOption
        {
            public OutputStrategyOption(ExportOutputStrategyDefinition definition)
            {
                Definition = definition;
            }

            public ExportOutputStrategyDefinition Definition { get; }

            public override string ToString()
            {
                return Definition.DisplayName;
            }
        }

        private static bool TryParseInt(
            string? text,
            int minInclusive,
            int maxInclusive,
            string fieldName,
            out int value,
            out string error)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = $"{fieldName} must be an integer.";
                return false;
            }

            if (value < minInclusive || value > maxInclusive)
            {
                error = $"{fieldName} must be between {minInclusive} and {maxInclusive}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryParseFloat(
            string? text,
            float minInclusive,
            float maxInclusive,
            string fieldName,
            out float value,
            out string error)
        {
            if (!float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
            {
                error = $"{fieldName} must be a number.";
                return false;
            }

            if (value < minInclusive || value > maxInclusive)
            {
                error = $"{fieldName} must be between {minInclusive} and {maxInclusive}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private async Task ShowInfoDialogAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 440,
                Height = 180,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var okButton = new Button
            {
                Content = "OK",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                MinWidth = 90
            };
            okButton.Click += (_, _) => dialog.Close();

            dialog.Content = new Grid
            {
                Margin = new Thickness(16),
                RowDefinitions = new RowDefinitions("*,Auto"),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { okButton },
                        [Grid.RowProperty] = 1
                    }
                }
            };

            await dialog.ShowDialog(this);
        }

        private async Task<bool> ShowConfirmDialogAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 460,
                Height = 190,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var openButton = new Button
            {
                Content = "Open Folder",
                MinWidth = 110
            };
            var closeButton = new Button
            {
                Content = "Close",
                MinWidth = 90
            };

            openButton.Click += (_, _) => dialog.Close(true);
            closeButton.Click += (_, _) => dialog.Close(false);

            dialog.Content = new Grid
            {
                Margin = new Thickness(16),
                RowDefinitions = new RowDefinitions("*,Auto"),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { closeButton, openButton },
                        [Grid.RowProperty] = 1
                    }
                }
            };

            return await dialog.ShowDialog<bool>(this);
        }

        private static void OpenFolder(string folderPath)
        {
            ProcessStartInfo startInfo;
            if (OperatingSystem.IsMacOS())
            {
                startInfo = new ProcessStartInfo("open");
            }
            else if (OperatingSystem.IsWindows())
            {
                startInfo = new ProcessStartInfo("explorer");
            }
            else
            {
                startInfo = new ProcessStartInfo("xdg-open");
            }

            startInfo.ArgumentList.Add(folderPath);
            startInfo.UseShellExecute = false;
            Process.Start(startInfo);
        }
    }
}
