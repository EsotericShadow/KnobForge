using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KnobForge.App.Controls;
using KnobForge.Core;
using KnobForge.Core.Scene;
using System;
using System.Numerics;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void OnEnvironmentChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi ||
                _envIntensityInput == null || _envRoughnessMixInput == null ||
                _envTopRInput == null || _envTopGInput == null || _envTopBInput == null ||
                _envBottomRInput == null || _envBottomGInput == null || _envBottomBInput == null)
            {
                return;
            }

            bool isCombo =
                ReferenceEquals(sender, _envTonemapCombo) ||
                ReferenceEquals(sender, _envPresetCombo) ||
                ReferenceEquals(sender, _envBloomKernelShapeCombo);
            if (isCombo)
            {
                if (e.Property != ComboBox.SelectedItemProperty)
                {
                    return;
                }
            }
            else if (e.Property != ValueInput.ValueProperty)
            {
                return;
            }

            CommitEnvironmentStateFromUi(sender);

            NotifyRenderOnly();
        }

        private void CommitEnvironmentStateFromUi(object? sender = null)
        {
            if (_envIntensityInput == null || _envRoughnessMixInput == null ||
                _envTopRInput == null || _envTopGInput == null || _envTopBInput == null ||
                _envBottomRInput == null || _envBottomGInput == null || _envBottomBInput == null)
            {
                return;
            }

            EnvironmentPreset selectedPreset = ResolveSelectedEnvironmentPreset();
            if (IsEnvironmentManualAppearanceSender(sender) && selectedPreset != EnvironmentPreset.Custom)
            {
                selectedPreset = EnvironmentPreset.Custom;
                WithUiRefreshSuppressed(() => SelectEnvironmentPresetOption(EnvironmentPreset.Custom));
            }

            _project.EnvironmentPreset = selectedPreset;
            if (selectedPreset != EnvironmentPreset.Custom)
            {
                ApplyEnvironmentPresetToProject(selectedPreset);
                if (TryGetEnvironmentPresetDefinition(selectedPreset, out EnvironmentPresetDefinition preset))
                {
                    WithUiRefreshSuppressed(() =>
                    {
                        ApplyEnvironmentValuesToInputs(
                            preset.TopColor,
                            preset.BottomColor,
                            preset.Intensity,
                            preset.RoughnessMix);
                    });
                }
            }
            else
            {
                _project.EnvironmentIntensity = (float)_envIntensityInput.Value;
                _project.EnvironmentRoughnessMix = (float)_envRoughnessMixInput.Value;
                _project.EnvironmentTopColor = new Vector3(
                    (float)_envTopRInput.Value,
                    (float)_envTopGInput.Value,
                    (float)_envTopBInput.Value);
                _project.EnvironmentBottomColor = new Vector3(
                    (float)_envBottomRInput.Value,
                    (float)_envBottomGInput.Value,
                    (float)_envBottomBInput.Value);
            }

            if (_envTonemapCombo?.SelectedItem is TonemapOperator tonemapOperator)
            {
                _project.ToneMappingOperator = tonemapOperator;
            }

            if (_envExposureInput != null)
            {
                _project.EnvironmentExposure = (float)_envExposureInput.Value;
            }

            if (_envBloomStrengthInput != null)
            {
                _project.EnvironmentBloomStrength = (float)_envBloomStrengthInput.Value;
            }

            if (_envBloomThresholdInput != null)
            {
                _project.EnvironmentBloomThreshold = (float)_envBloomThresholdInput.Value;
            }

            if (_envBloomKneeInput != null)
            {
                _project.EnvironmentBloomKnee = (float)_envBloomKneeInput.Value;
            }

            _project.BloomKernelShape = ResolveSelectedBloomKernelShape();

            if (_envHdriBlendInput != null)
            {
                _project.EnvironmentHdriBlend = (float)_envHdriBlendInput.Value;
            }

            if (_envHdriRotationInput != null)
            {
                _project.EnvironmentHdriRotationDegrees = (float)_envHdriRotationInput.Value;
            }

            UpdateEnvironmentManualControlsAppearance(_project.EnvironmentPreset);
        }

        private void RebuildEnvironmentPresetOptions()
        {
            _environmentPresetOptions.Clear();
            _environmentPresetOptions.Add(new EnvironmentPresetOption
            {
                Preset = EnvironmentPreset.Custom,
                Name = "Custom"
            });

            foreach (EnvironmentPresetDefinition definition in EnvironmentPresets.All)
            {
                _environmentPresetOptions.Add(new EnvironmentPresetOption
                {
                    Preset = definition.Preset,
                    Name = definition.DisplayName
                });
            }
        }

        private void RebuildBloomKernelShapeOptions()
        {
            _bloomKernelShapeOptions.Clear();
            _bloomKernelShapeOptions.Add(new BloomKernelShapeOption { Shape = BloomKernelShape.Soft, Name = "Soft" });
            _bloomKernelShapeOptions.Add(new BloomKernelShapeOption { Shape = BloomKernelShape.Star4, Name = "Star 4" });
            _bloomKernelShapeOptions.Add(new BloomKernelShapeOption { Shape = BloomKernelShape.Star6, Name = "Star 6" });
            _bloomKernelShapeOptions.Add(new BloomKernelShapeOption { Shape = BloomKernelShape.AnamorphicStreak, Name = "Anamorphic streak" });
        }

        private EnvironmentPreset ResolveSelectedEnvironmentPreset()
        {
            if (_envPresetCombo?.SelectedItem is EnvironmentPresetOption option)
            {
                return option.Preset;
            }

            return EnvironmentPreset.Custom;
        }

        private BloomKernelShape ResolveSelectedBloomKernelShape()
        {
            if (_envBloomKernelShapeCombo?.SelectedItem is BloomKernelShapeOption option)
            {
                return option.Shape;
            }

            return BloomKernelShape.Soft;
        }

        private void SelectEnvironmentPresetOption(EnvironmentPreset preset)
        {
            if (_envPresetCombo == null)
            {
                return;
            }

            EnvironmentPresetOption? option = _environmentPresetOptions.Find(candidate => candidate.Preset == preset);
            _envPresetCombo.SelectedItem = option ?? _environmentPresetOptions.Find(candidate => candidate.Preset == EnvironmentPreset.Custom);
        }

        private void SelectBloomKernelShapeOption(BloomKernelShape shape)
        {
            if (_envBloomKernelShapeCombo == null)
            {
                return;
            }

            BloomKernelShapeOption? option = _bloomKernelShapeOptions.Find(candidate => candidate.Shape == shape);
            _envBloomKernelShapeCombo.SelectedItem = option ?? _bloomKernelShapeOptions.Find(candidate => candidate.Shape == BloomKernelShape.Soft);
        }

        private static bool TryGetEnvironmentPresetDefinition(EnvironmentPreset preset, out EnvironmentPresetDefinition definition)
        {
            EnvironmentPresetDefinition? resolved = EnvironmentPresets.Get(preset);
            if (resolved.HasValue)
            {
                definition = resolved.Value;
                return true;
            }

            definition = default;
            return false;
        }

        private void ApplyEnvironmentPresetToProject(EnvironmentPreset preset)
        {
            if (!TryGetEnvironmentPresetDefinition(preset, out EnvironmentPresetDefinition definition))
            {
                return;
            }

            _project.EnvironmentTopColor = definition.TopColor;
            _project.EnvironmentBottomColor = definition.BottomColor;
            _project.EnvironmentIntensity = definition.Intensity;
            _project.EnvironmentRoughnessMix = definition.RoughnessMix;
        }

        private void ApplyEnvironmentValuesToInputs(Vector3 topColor, Vector3 bottomColor, float intensity, float roughnessMix)
        {
            if (_envIntensityInput == null || _envRoughnessMixInput == null ||
                _envTopRInput == null || _envTopGInput == null || _envTopBInput == null ||
                _envBottomRInput == null || _envBottomGInput == null || _envBottomBInput == null)
            {
                return;
            }

            _envIntensityInput.Value = intensity;
            _envRoughnessMixInput.Value = roughnessMix;
            _envTopRInput.Value = topColor.X;
            _envTopGInput.Value = topColor.Y;
            _envTopBInput.Value = topColor.Z;
            _envBottomRInput.Value = bottomColor.X;
            _envBottomGInput.Value = bottomColor.Y;
            _envBottomBInput.Value = bottomColor.Z;
        }

        private void UpdateEnvironmentManualControlsAppearance(EnvironmentPreset preset)
        {
            if (_environmentManualSettingsPanel == null)
            {
                return;
            }

            _environmentManualSettingsPanel.Opacity = preset == EnvironmentPreset.Custom ? 1.0 : 0.62;
        }

        private bool IsEnvironmentManualAppearanceSender(object? sender)
        {
            return ReferenceEquals(sender, _envIntensityInput) ||
                   ReferenceEquals(sender, _envRoughnessMixInput) ||
                   ReferenceEquals(sender, _envTopRInput) ||
                   ReferenceEquals(sender, _envTopGInput) ||
                   ReferenceEquals(sender, _envTopBInput) ||
                   ReferenceEquals(sender, _envBottomRInput) ||
                   ReferenceEquals(sender, _envBottomGInput) ||
                   ReferenceEquals(sender, _envBottomBInput);
        }

        private void ApplyEnvironmentHdriPathFromUi()
        {
            if (_envHdriPathTextBox == null)
            {
                return;
            }

            _project.EnvironmentHdriPath = _envHdriPathTextBox.Text ?? string.Empty;
            NotifyRenderOnly();
        }

        private void ClearEnvironmentHdriPathFromUi()
        {
            _project.EnvironmentHdriPath = string.Empty;
            if (_envHdriPathTextBox != null)
            {
                _envHdriPathTextBox.Text = string.Empty;
            }

            NotifyRenderOnly();
        }

        private void OnShadowSettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi ||
                _shadowEnabledCheckBox == null ||
                _shadowSourceModeCombo == null ||
                _shadowStrengthInput == null ||
                _shadowSoftnessInput == null ||
                _shadowDistanceInput == null ||
                _shadowScaleInput == null ||
                _shadowQualityInput == null ||
                _shadowGrayInput == null ||
                _shadowDiffuseInfluenceInput == null)
            {
                return;
            }

            if (ReferenceEquals(sender, _shadowEnabledCheckBox))
            {
                if (e.Property != ToggleButton.IsCheckedProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _shadowSourceModeCombo))
            {
                if (e.Property != ComboBox.SelectedItemProperty)
                {
                    return;
                }
            }
            else if (e.Property != ValueInput.ValueProperty)
            {
                return;
            }

            _project.ShadowsEnabled = _shadowEnabledCheckBox.IsChecked ?? true;
            _project.ShadowMode = _shadowSourceModeCombo.SelectedItem is ShadowLightMode shadowMode
                ? shadowMode
                : ShadowLightMode.Weighted;
            _project.ShadowStrength = (float)_shadowStrengthInput.Value;
            _project.ShadowSoftness = (float)_shadowSoftnessInput.Value;
            _project.ShadowDistance = (float)_shadowDistanceInput.Value;
            _project.ShadowScale = (float)_shadowScaleInput.Value;
            _project.ShadowQuality = (float)_shadowQualityInput.Value;
            _project.ShadowGray = (float)_shadowGrayInput.Value;
            _project.ShadowDiffuseInfluence = (float)_shadowDiffuseInfluenceInput.Value;
            NotifyRenderOnly();
        }
        private void UpdateReadouts()
        {
        }

        private static string FormatSliderDimensionValue(double value)
        {
            return value <= 0.0001
                ? "Auto"
                : $"{value:0.0}px";
        }

        private static string FormatToggleStateValue(int stateIndex, int stateCount)
        {
            if (stateCount <= 2)
            {
                return stateIndex <= 0
                    ? "State 1 (Down)"
                    : "State 2 (Up)";
            }

            return stateIndex switch
            {
                <= 0 => "State 1 (Down)",
                1 => "State 2 (Center)",
                _ => "State 3 (Up)"
            };
        }
    }
}
