using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using KnobForge.App.Controls;
using KnobForge.Core;
using System;
using System.Numerics;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void OnPaintBrushSettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi ||
                _brushPaintEnabledCheckBox == null ||
                _brushPaintChannelCombo == null ||
                _brushTypeCombo == null ||
                _brushPaintColorPicker == null ||
                _paintChannelTargetValueInput == null ||
                _scratchAbrasionTypeCombo == null ||
                _brushSizeInput == null ||
                _brushOpacityInput == null ||
                _brushDarknessInput == null ||
                _brushSpreadInput == null ||
                _paintCoatMetallicInput == null ||
                _paintCoatRoughnessInput == null ||
                _clearCoatAmountInput == null ||
                _clearCoatRoughnessInput == null ||
                _anisotropyAngleInput == null ||
                _scratchWidthInput == null ||
                _scratchDepthInput == null ||
                _scratchResistanceInput == null ||
                _scratchDepthRampInput == null ||
                _scratchExposeColorRInput == null ||
                _scratchExposeColorGInput == null ||
                _scratchExposeColorBInput == null ||
                _scratchExposeMetallicInput == null ||
                _scratchExposeRoughnessInput == null)
            {
                return;
            }

            if (ReferenceEquals(sender, _brushPaintEnabledCheckBox))
            {
                if (e.Property != ToggleButton.IsCheckedProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _brushPaintChannelCombo) || ReferenceEquals(sender, _brushTypeCombo) || ReferenceEquals(sender, _scratchAbrasionTypeCombo))
            {
                if (e.Property != ComboBox.SelectedItemProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _brushPaintColorPicker))
            {
                if (!string.Equals(e.Property?.Name, "Color", StringComparison.Ordinal))
                {
                    return;
                }
            }
            else if (e.Property != ValueInput.ValueProperty)
            {
                return;
            }

            _project.BrushPaintingEnabled = _brushPaintEnabledCheckBox.IsChecked ?? false;
            _project.BrushChannel = _brushPaintChannelCombo.SelectedItem is PaintChannel channel
                ? channel
                : PaintChannel.Rust;
            _project.BrushType = _brushTypeCombo.SelectedItem is PaintBrushType brushType
                ? brushType
                : PaintBrushType.Spray;
            _project.ScratchAbrasionType = _scratchAbrasionTypeCombo.SelectedItem is ScratchAbrasionType abrasionType
                ? abrasionType
                : ScratchAbrasionType.Needle;
            _project.BrushSizePx = (float)_brushSizeInput.Value;
            _project.BrushOpacity = (float)_brushOpacityInput.Value;
            _project.BrushDarkness = (float)_brushDarknessInput.Value;
            _project.BrushSpread = (float)_brushSpreadInput.Value;
            _project.PaintCoatMetallic = (float)_paintCoatMetallicInput.Value;
            _project.PaintCoatRoughness = (float)_paintCoatRoughnessInput.Value;
            float targetValue = (float)_paintChannelTargetValueInput.Value;
            if (_project.BrushChannel == PaintChannel.Roughness)
            {
                _project.RoughnessPaintTarget = targetValue;
            }
            else if (_project.BrushChannel == PaintChannel.Metallic)
            {
                _project.MetallicPaintTarget = targetValue;
            }
            _project.ClearCoatAmount = (float)_clearCoatAmountInput.Value;
            _project.ClearCoatRoughness = (float)_clearCoatRoughnessInput.Value;
            _project.AnisotropyAngleDegrees = (float)_anisotropyAngleInput.Value;
            _project.PaintColor = ToVector3(_brushPaintColorPicker.Color);
            _project.ScratchWidthPx = (float)_scratchWidthInput.Value;
            _project.ScratchDepth = (float)_scratchDepthInput.Value;
            _project.ScratchDragResistance = (float)_scratchResistanceInput.Value;
            _project.ScratchDepthRamp = (float)_scratchDepthRampInput.Value;
            _project.ScratchExposeColor = new Vector3(
                (float)_scratchExposeColorRInput.Value,
                (float)_scratchExposeColorGInput.Value,
                (float)_scratchExposeColorBInput.Value);
            _project.ScratchExposeMetallic = (float)_scratchExposeMetallicInput.Value;
            _project.ScratchExposeRoughness = (float)_scratchExposeRoughnessInput.Value;
            UpdateBrushContextUi();
            NotifyRenderOnly();
            _metalViewport?.RefreshPaintHud();
        }

        private void OnClearPaintMask()
        {
            _project.ClearPaintMask();
            if (_metalViewport != null)
            {
                _metalViewport.DiscardPendingPaintStamps();
                _metalViewport.RequestClearPaintColorTexture();
                _metalViewport.ClearPaintToRevisionZero();
                _metalViewport.InvalidateGpu();
            }
            NotifyRenderOnly();
        }

        private static Vector3 ToVector3(Color color)
        {
            return new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        }

        private static Color ToAvaloniaColor(Vector3 color)
        {
            byte r = (byte)Math.Clamp((int)MathF.Round(color.X * 255f), 0, 255);
            byte g = (byte)Math.Clamp((int)MathF.Round(color.Y * 255f), 0, 255);
            byte b = (byte)Math.Clamp((int)MathF.Round(color.Z * 255f), 0, 255);
            return Color.FromRgb(r, g, b);
        }
    }
}
