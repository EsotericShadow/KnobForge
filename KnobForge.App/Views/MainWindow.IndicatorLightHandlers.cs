using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KnobForge.App.Controls;
using KnobForge.Core;
using SkiaSharp;
using System;
using System.Linq;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private enum IndicatorLightPreset
        {
            Neutral = 0,
            Pulse = 1,
            Flicker = 2
        }

        private enum IndicatorLensMaterialPreset
        {
            ClearLens = 0,
            FrostedLens = 1,
            SaturatedLedLens = 2
        }

        private int _selectedIndicatorEmitterSourceIndex;

        private void OnIndicatorLightSettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi)
            {
                return;
            }

            if (ReferenceEquals(sender, _indicatorAssemblyEnabledCheckBox) ||
                ReferenceEquals(sender, _indicatorDynamicLightsEnabledCheckBox) ||
                ReferenceEquals(sender, _indicatorQuickLightOnCheckBox))
            {
                if (e.Property != ToggleButton.IsCheckedProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _indicatorLightAnimationModeCombo))
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

            if (ReferenceEquals(sender, _indicatorQuickLightOnCheckBox) &&
                _indicatorDynamicLightsEnabledCheckBox != null &&
                _indicatorQuickLightOnCheckBox != null)
            {
                WithUiRefreshSuppressed(() =>
                {
                    _indicatorDynamicLightsEnabledCheckBox.IsChecked = _indicatorQuickLightOnCheckBox.IsChecked;
                });
            }
            else if (ReferenceEquals(sender, _indicatorDynamicLightsEnabledCheckBox) &&
                _indicatorQuickLightOnCheckBox != null &&
                _indicatorDynamicLightsEnabledCheckBox != null)
            {
                WithUiRefreshSuppressed(() =>
                {
                    _indicatorQuickLightOnCheckBox.IsChecked = _indicatorDynamicLightsEnabledCheckBox.IsChecked;
                });
            }

            bool requestHeavyRefresh =
                !ReferenceEquals(sender, _indicatorDynamicLightsEnabledCheckBox) &&
                !ReferenceEquals(sender, _indicatorQuickLightOnCheckBox) &&
                !ReferenceEquals(sender, _indicatorQuickBrightnessInput) &&
                !ReferenceEquals(sender, _indicatorQuickGlowInput) &&
                !ReferenceEquals(sender, _indicatorLightAnimationModeCombo) &&
                !ReferenceEquals(sender, _indicatorLensTransmissionInput) &&
                !ReferenceEquals(sender, _indicatorLensIorInput) &&
                !ReferenceEquals(sender, _indicatorLensThicknessInput) &&
                !ReferenceEquals(sender, _indicatorLensAbsorptionInput) &&
                !ReferenceEquals(sender, _indicatorLensSurfaceRoughnessInput) &&
                !ReferenceEquals(sender, _indicatorLensSurfaceSpecularInput) &&
                !ReferenceEquals(sender, _indicatorLensTintRInput) &&
                !ReferenceEquals(sender, _indicatorLensTintGInput) &&
                !ReferenceEquals(sender, _indicatorLensTintBInput) &&
                !ReferenceEquals(sender, _indicatorLightAnimationSpeedInput) &&
                !ReferenceEquals(sender, _indicatorLightFlickerAmountInput) &&
                !ReferenceEquals(sender, _indicatorLightFlickerDropoutInput) &&
                !ReferenceEquals(sender, _indicatorLightFlickerSmoothingInput) &&
                !ReferenceEquals(sender, _indicatorLightFlickerSeedInput);

            ApplyIndicatorLightUiToProject(requestHeavyRefresh);
        }

        private void ApplyIndicatorLightUiToProject(bool requestHeavyRefresh)
        {
            if (_indicatorAssemblyEnabledCheckBox == null ||
                _indicatorBaseWidthInput == null ||
                _indicatorBaseHeightInput == null ||
                _indicatorBaseThicknessInput == null ||
                _indicatorHousingRadiusInput == null ||
                _indicatorHousingHeightInput == null ||
                _indicatorLensRadiusInput == null ||
                _indicatorLensHeightInput == null ||
                _indicatorLensTransmissionInput == null ||
                _indicatorLensIorInput == null ||
                _indicatorLensThicknessInput == null ||
                _indicatorLensAbsorptionInput == null ||
                _indicatorLensSurfaceRoughnessInput == null ||
                _indicatorLensSurfaceSpecularInput == null ||
                _indicatorLensTintRInput == null ||
                _indicatorLensTintGInput == null ||
                _indicatorLensTintBInput == null ||
                _indicatorReflectorBaseRadiusInput == null ||
                _indicatorReflectorTopRadiusInput == null ||
                _indicatorReflectorDepthInput == null ||
                _indicatorEmitterRadiusInput == null ||
                _indicatorEmitterSpreadInput == null ||
                _indicatorEmitterDepthInput == null ||
                _indicatorEmitterCountInput == null ||
                _indicatorRadialSegmentsInput == null ||
                _indicatorLensLatitudeSegmentsInput == null ||
                _indicatorLensLongitudeSegmentsInput == null ||
                _indicatorDynamicLightsEnabledCheckBox == null ||
                _indicatorLightAnimationModeCombo == null ||
                _indicatorLightAnimationSpeedInput == null ||
                _indicatorLightFlickerAmountInput == null ||
                _indicatorLightFlickerDropoutInput == null ||
                _indicatorLightFlickerSmoothingInput == null ||
                _indicatorLightFlickerSeedInput == null)
            {
                return;
            }

            _project.IndicatorAssemblyEnabled = _indicatorAssemblyEnabledCheckBox.IsChecked == true;
            _project.IndicatorBaseWidth = (float)_indicatorBaseWidthInput.Value;
            _project.IndicatorBaseHeight = (float)_indicatorBaseHeightInput.Value;
            _project.IndicatorBaseThickness = (float)_indicatorBaseThicknessInput.Value;
            _project.IndicatorHousingRadius = (float)_indicatorHousingRadiusInput.Value;
            _project.IndicatorHousingHeight = (float)_indicatorHousingHeightInput.Value;
            _project.IndicatorLensRadius = (float)_indicatorLensRadiusInput.Value;
            _project.IndicatorLensHeight = (float)_indicatorLensHeightInput.Value;
            _project.IndicatorLensTransmission = (float)_indicatorLensTransmissionInput.Value;
            _project.IndicatorLensIor = (float)_indicatorLensIorInput.Value;
            _project.IndicatorLensThickness = (float)_indicatorLensThicknessInput.Value;
            _project.IndicatorLensAbsorption = (float)_indicatorLensAbsorptionInput.Value;
            _project.IndicatorLensSurfaceRoughness = (float)_indicatorLensSurfaceRoughnessInput.Value;
            _project.IndicatorLensSurfaceSpecularStrength = (float)_indicatorLensSurfaceSpecularInput.Value;
            _project.IndicatorLensTint = new System.Numerics.Vector3(
                (float)_indicatorLensTintRInput.Value,
                (float)_indicatorLensTintGInput.Value,
                (float)_indicatorLensTintBInput.Value);
            _project.IndicatorReflectorBaseRadius = (float)_indicatorReflectorBaseRadiusInput.Value;
            _project.IndicatorReflectorTopRadius = (float)_indicatorReflectorTopRadiusInput.Value;
            _project.IndicatorReflectorDepth = (float)_indicatorReflectorDepthInput.Value;
            _project.IndicatorEmitterRadius = (float)_indicatorEmitterRadiusInput.Value;
            _project.IndicatorEmitterSpread = (float)_indicatorEmitterSpreadInput.Value;
            _project.IndicatorEmitterDepth = (float)_indicatorEmitterDepthInput.Value;

            int emitterCount = Math.Clamp((int)Math.Round(_indicatorEmitterCountInput.Value), 1, 8);
            int radialSegments = Math.Clamp((int)Math.Round(_indicatorRadialSegmentsInput.Value), 8, 96);
            int lensLatitudeSegments = Math.Clamp((int)Math.Round(_indicatorLensLatitudeSegmentsInput.Value), 4, 64);
            int lensLongitudeSegments = Math.Clamp((int)Math.Round(_indicatorLensLongitudeSegmentsInput.Value), 6, 96);

            _indicatorEmitterCountInput.Value = emitterCount;
            _indicatorRadialSegmentsInput.Value = radialSegments;
            _indicatorLensLatitudeSegmentsInput.Value = lensLatitudeSegments;
            _indicatorLensLongitudeSegmentsInput.Value = lensLongitudeSegments;

            _project.IndicatorEmitterCount = emitterCount;
            _project.IndicatorRadialSegments = radialSegments;
            _project.IndicatorLensLatitudeSegments = lensLatitudeSegments;
            _project.IndicatorLensLongitudeSegments = lensLongitudeSegments;

            DynamicLightRig rig = _project.DynamicLightRig;
            bool lightOn = _indicatorQuickLightOnCheckBox?.IsChecked
                ?? _indicatorDynamicLightsEnabledCheckBox.IsChecked
                ?? false;
            rig.Enabled = lightOn;
            rig.AnimationMode = _indicatorLightAnimationModeCombo.SelectedItem is DynamicLightAnimationMode mode
                ? mode
                : DynamicLightAnimationMode.Steady;
            rig.AnimationSpeed = (float)_indicatorLightAnimationSpeedInput.Value;
            rig.FlickerAmount = (float)_indicatorLightFlickerAmountInput.Value;
            rig.FlickerDropoutChance = (float)_indicatorLightFlickerDropoutInput.Value;
            rig.FlickerSmoothing = (float)_indicatorLightFlickerSmoothingInput.Value;
            rig.MasterIntensity = _indicatorQuickBrightnessInput != null
                ? (float)_indicatorQuickBrightnessInput.Value
                : 1f;
            rig.EmissiveGlow = _indicatorQuickGlowInput != null
                ? (float)_indicatorQuickGlowInput.Value
                : 1f;
            int seed = Math.Clamp((int)Math.Round(_indicatorLightFlickerSeedInput.Value), 1, 100000);
            _indicatorLightFlickerSeedInput.Value = seed;
            rig.FlickerSeed = seed;
            SyncIndicatorDynamicLightSourcesToAssembly(recenterSources: requestHeavyRefresh);
            RefreshIndicatorEmitterSourceControlsFromProject();

            UpdateReadouts();
            if (requestHeavyRefresh)
            {
                RequestHeavyGeometryRefresh();
            }
            else
            {
                NotifyProjectStateChanged();
            }
        }

        private void ResetIndicatorAssemblyDefaultsFromUi()
        {
            _project.EnsureIndicatorAssemblyDefaults(forceReset: true);
            _project.DynamicLightRig.EnsureIndicatorDefaults();
            SyncIndicatorDynamicLightSourcesToAssembly(recenterSources: true);
            RefreshIndicatorEmitterSourceControlsFromProject();
            RefreshInspectorFromProject(InspectorRefreshTabPolicy.PreserveCurrentTab);
            NotifyProjectStateChanged();
        }

        private void ApplyIndicatorLightPreset(IndicatorLightPreset preset)
        {
            if (_indicatorDynamicLightsEnabledCheckBox == null ||
                _indicatorLightAnimationModeCombo == null ||
                _indicatorLightAnimationSpeedInput == null ||
                _indicatorLightFlickerAmountInput == null ||
                _indicatorLightFlickerDropoutInput == null ||
                _indicatorLightFlickerSmoothingInput == null ||
                _indicatorLightFlickerSeedInput == null)
            {
                return;
            }

            WithUiRefreshSuppressed(() =>
            {
                _indicatorDynamicLightsEnabledCheckBox.IsChecked = true;
                if (_indicatorQuickLightOnCheckBox != null)
                {
                    _indicatorQuickLightOnCheckBox.IsChecked = true;
                }
                switch (preset)
                {
                    case IndicatorLightPreset.Pulse:
                        _indicatorLightAnimationModeCombo.SelectedItem = DynamicLightAnimationMode.Pulse;
                        _indicatorLightAnimationSpeedInput.Value = 1.35;
                        _indicatorLightFlickerAmountInput.Value = 0.20;
                        _indicatorLightFlickerDropoutInput.Value = 0.00;
                        _indicatorLightFlickerSmoothingInput.Value = 0.62;
                        _indicatorLightFlickerSeedInput.Value = 1337;
                        if (_indicatorQuickBrightnessInput != null)
                        {
                            _indicatorQuickBrightnessInput.Value = 1.55;
                        }
                        if (_indicatorQuickGlowInput != null)
                        {
                            _indicatorQuickGlowInput.Value = 1.65;
                        }
                        break;
                    case IndicatorLightPreset.Flicker:
                        _indicatorLightAnimationModeCombo.SelectedItem = DynamicLightAnimationMode.Flicker;
                        _indicatorLightAnimationSpeedInput.Value = 2.60;
                        _indicatorLightFlickerAmountInput.Value = 0.55;
                        _indicatorLightFlickerDropoutInput.Value = 0.14;
                        _indicatorLightFlickerSmoothingInput.Value = 0.22;
                        _indicatorLightFlickerSeedInput.Value = 4242;
                        if (_indicatorQuickBrightnessInput != null)
                        {
                            _indicatorQuickBrightnessInput.Value = 1.80;
                        }
                        if (_indicatorQuickGlowInput != null)
                        {
                            _indicatorQuickGlowInput.Value = 2.00;
                        }
                        break;
                    default:
                        _indicatorLightAnimationModeCombo.SelectedItem = DynamicLightAnimationMode.Steady;
                        _indicatorLightAnimationSpeedInput.Value = 1.00;
                        _indicatorLightFlickerAmountInput.Value = 0.00;
                        _indicatorLightFlickerDropoutInput.Value = 0.00;
                        _indicatorLightFlickerSmoothingInput.Value = 0.50;
                        _indicatorLightFlickerSeedInput.Value = 1337;
                        if (_indicatorQuickBrightnessInput != null)
                        {
                            _indicatorQuickBrightnessInput.Value = 1.25;
                        }
                        if (_indicatorQuickGlowInput != null)
                        {
                            _indicatorQuickGlowInput.Value = 1.20;
                        }
                        break;
                }
            });

            ApplyIndicatorLightUiToProject(requestHeavyRefresh: false);
        }

        private void ApplyIndicatorLensMaterialPreset(IndicatorLensMaterialPreset preset)
        {
            if (_indicatorLensTransmissionInput == null ||
                _indicatorLensIorInput == null ||
                _indicatorLensThicknessInput == null ||
                _indicatorLensAbsorptionInput == null ||
                _indicatorLensSurfaceRoughnessInput == null ||
                _indicatorLensSurfaceSpecularInput == null ||
                _indicatorLensTintRInput == null ||
                _indicatorLensTintGInput == null ||
                _indicatorLensTintBInput == null)
            {
                return;
            }

            IndicatorLensMaterialPresetId corePreset = preset switch
            {
                IndicatorLensMaterialPreset.FrostedLens => IndicatorLensMaterialPresetId.Frosted,
                IndicatorLensMaterialPreset.SaturatedLedLens => IndicatorLensMaterialPresetId.SaturatedLed,
                _ => IndicatorLensMaterialPresetId.Clear
            };

            IndicatorLensMaterialPresetDefinition values = IndicatorLensMaterialPresets.Resolve(corePreset);

            WithUiRefreshSuppressed(() =>
            {
                _indicatorLensTransmissionInput.Value = values.Transmission;
                _indicatorLensIorInput.Value = values.Ior;
                _indicatorLensThicknessInput.Value = values.Thickness;
                _indicatorLensAbsorptionInput.Value = values.Absorption;
                _indicatorLensSurfaceRoughnessInput.Value = values.SurfaceRoughness;
                _indicatorLensSurfaceSpecularInput.Value = values.SurfaceSpecularStrength;
                _indicatorLensTintRInput.Value = values.Tint.X;
                _indicatorLensTintGInput.Value = values.Tint.Y;
                _indicatorLensTintBInput.Value = values.Tint.Z;
            });

            ApplyIndicatorLightUiToProject(requestHeavyRefresh: false);
        }

        private void OnIndicatorLightEmitterSelectionChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi || _indicatorEmitterSourceCombo == null)
            {
                return;
            }

            if (e.Property != ComboBox.SelectedIndexProperty && e.Property != ComboBox.SelectedItemProperty)
            {
                return;
            }

            _selectedIndicatorEmitterSourceIndex = Math.Max(0, _indicatorEmitterSourceCombo.SelectedIndex);
            LoadSelectedEmitterSourceIntoUi();
            UpdateReadouts();
        }

        private void OnIndicatorLightEmitterSettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi || _project.ProjectType != InteractorProjectType.IndicatorLight)
            {
                return;
            }

            if (ReferenceEquals(sender, _indicatorEmitterSourceEnabledCheckBox))
            {
                if (e.Property != ToggleButton.IsCheckedProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _indicatorEmitterSourceNameTextBox))
            {
                if (e.Property != TextBox.TextProperty)
                {
                    return;
                }
            }
            else if (e.Property != ValueInput.ValueProperty)
            {
                return;
            }

            if (!TryGetSelectedEmitterSource(out DynamicLightSource? source) || source == null)
            {
                return;
            }

            if (_indicatorEmitterSourceEnabledCheckBox != null)
            {
                source.Enabled = _indicatorEmitterSourceEnabledCheckBox.IsChecked == true;
            }

            if (_indicatorEmitterSourceNameTextBox != null)
            {
                source.Name = NormalizeIndicatorEmitterName(_indicatorEmitterSourceNameTextBox.Text, _selectedIndicatorEmitterSourceIndex);
            }

            if (_indicatorEmitterSourcePhaseOffsetInput != null)
            {
                source.AnimationPhaseOffsetDegrees = (float)_indicatorEmitterSourcePhaseOffsetInput.Value;
            }

            if (_indicatorEmitterSourceXInput != null)
            {
                source.X = (float)_indicatorEmitterSourceXInput.Value;
            }

            if (_indicatorEmitterSourceYInput != null)
            {
                source.Y = (float)_indicatorEmitterSourceYInput.Value;
            }

            if (_indicatorEmitterSourceZInput != null)
            {
                source.Z = (float)_indicatorEmitterSourceZInput.Value;
            }

            if (_indicatorEmitterSourceIntensityInput != null)
            {
                source.Intensity = (float)_indicatorEmitterSourceIntensityInput.Value;
            }

            if (_indicatorEmitterSourceRadiusInput != null)
            {
                source.Radius = (float)_indicatorEmitterSourceRadiusInput.Value;
            }

            if (_indicatorEmitterSourceFalloffInput != null)
            {
                source.Falloff = (float)_indicatorEmitterSourceFalloffInput.Value;
            }

            if (_indicatorEmitterSourceRInput != null &&
                _indicatorEmitterSourceGInput != null &&
                _indicatorEmitterSourceBInput != null)
            {
                byte r = (byte)Math.Clamp((int)Math.Round(_indicatorEmitterSourceRInput.Value * 255d), 0, 255);
                byte g = (byte)Math.Clamp((int)Math.Round(_indicatorEmitterSourceGInput.Value * 255d), 0, 255);
                byte b = (byte)Math.Clamp((int)Math.Round(_indicatorEmitterSourceBInput.Value * 255d), 0, 255);
                source.Color = new SKColor(r, g, b, 255);
            }

            if (ReferenceEquals(sender, _indicatorEmitterSourceNameTextBox))
            {
                RefreshIndicatorEmitterSourceControlsFromProject(loadSelectedEmitterIntoUi: false);
            }

            UpdateReadouts();
            NotifyProjectStateChanged();
        }

        private void RefreshIndicatorEmitterSourceControlsFromProject(bool loadSelectedEmitterIntoUi = true)
        {
            if (_indicatorEmitterSourceCombo == null)
            {
                return;
            }

            DynamicLightRig rig = _project.DynamicLightRig;
            if (_selectedIndicatorEmitterSourceIndex < 0)
            {
                _selectedIndicatorEmitterSourceIndex = 0;
            }

            if (rig.Sources.Count == 0)
            {
                _selectedIndicatorEmitterSourceIndex = 0;
            }
            else
            {
                _selectedIndicatorEmitterSourceIndex = Math.Clamp(_selectedIndicatorEmitterSourceIndex, 0, rig.Sources.Count - 1);
            }

            var labels = new string[rig.Sources.Count];
            for (int i = 0; i < labels.Length; i++)
            {
                DynamicLightSource source = rig.Sources[i];
                DynamicLightRig.NormalizeSourceIdentity(source, i, labels.Length);
                labels[i] = source.Name;
            }

            int selectedIndex = _selectedIndicatorEmitterSourceIndex;
            WithUiRefreshSuppressed(() =>
            {
                _indicatorEmitterSourceCombo.ItemsSource = labels;
                _indicatorEmitterSourceCombo.SelectedIndex = labels.Length == 0 ? -1 : selectedIndex;
            });

            if (_indicatorEmitterSourceMoveUpButton != null)
            {
                _indicatorEmitterSourceMoveUpButton.IsEnabled = labels.Length > 1 && selectedIndex > 0;
            }
            if (_indicatorEmitterSourceMoveDownButton != null)
            {
                _indicatorEmitterSourceMoveDownButton.IsEnabled = labels.Length > 1 && selectedIndex >= 0 && selectedIndex < labels.Length - 1;
            }
            if (_indicatorEmitterSourceAutoPhaseButton != null)
            {
                _indicatorEmitterSourceAutoPhaseButton.IsEnabled = labels.Length > 0;
            }

            if (loadSelectedEmitterIntoUi)
            {
                LoadSelectedEmitterSourceIntoUi();
            }
        }

        private void LoadSelectedEmitterSourceIntoUi()
        {
            if (!TryGetSelectedEmitterSource(out DynamicLightSource? source) || source == null)
            {
                if (_indicatorEmitterSourceEnabledCheckBox != null)
                {
                    _indicatorEmitterSourceEnabledCheckBox.IsEnabled = false;
                }
                if (_indicatorEmitterSourceNameTextBox != null)
                {
                    _indicatorEmitterSourceNameTextBox.IsEnabled = false;
                    _indicatorEmitterSourceNameTextBox.Text = string.Empty;
                }
                if (_indicatorEmitterSourcePhaseOffsetInput != null)
                {
                    _indicatorEmitterSourcePhaseOffsetInput.IsEnabled = false;
                    _indicatorEmitterSourcePhaseOffsetInput.Value = 0d;
                }

                return;
            }

            WithUiRefreshSuppressed(() =>
            {
                if (_indicatorEmitterSourceEnabledCheckBox != null)
                {
                    _indicatorEmitterSourceEnabledCheckBox.IsEnabled = true;
                    _indicatorEmitterSourceEnabledCheckBox.IsChecked = source.Enabled;
                }

                if (_indicatorEmitterSourceNameTextBox != null)
                {
                    _indicatorEmitterSourceNameTextBox.IsEnabled = true;
                    _indicatorEmitterSourceNameTextBox.Text = source.Name;
                }

                if (_indicatorEmitterSourcePhaseOffsetInput != null)
                {
                    _indicatorEmitterSourcePhaseOffsetInput.IsEnabled = true;
                    _indicatorEmitterSourcePhaseOffsetInput.Value = source.AnimationPhaseOffsetDegrees;
                }

                if (_indicatorEmitterSourceXInput != null)
                {
                    _indicatorEmitterSourceXInput.Value = source.X;
                }

                if (_indicatorEmitterSourceYInput != null)
                {
                    _indicatorEmitterSourceYInput.Value = source.Y;
                }

                if (_indicatorEmitterSourceZInput != null)
                {
                    _indicatorEmitterSourceZInput.Value = source.Z;
                }

                if (_indicatorEmitterSourceIntensityInput != null)
                {
                    _indicatorEmitterSourceIntensityInput.Value = source.Intensity;
                }

                if (_indicatorEmitterSourceRadiusInput != null)
                {
                    _indicatorEmitterSourceRadiusInput.Value = source.Radius;
                }

                if (_indicatorEmitterSourceFalloffInput != null)
                {
                    _indicatorEmitterSourceFalloffInput.Value = source.Falloff;
                }

                if (_indicatorEmitterSourceRInput != null)
                {
                    _indicatorEmitterSourceRInput.Value = source.Color.Red / 255d;
                }

                if (_indicatorEmitterSourceGInput != null)
                {
                    _indicatorEmitterSourceGInput.Value = source.Color.Green / 255d;
                }

                if (_indicatorEmitterSourceBInput != null)
                {
                    _indicatorEmitterSourceBInput.Value = source.Color.Blue / 255d;
                }
            });
        }

        private bool TryGetSelectedEmitterSource(out DynamicLightSource? source)
        {
            source = null;
            DynamicLightRig rig = _project.DynamicLightRig;
            if (rig.Sources.Count == 0)
            {
                return false;
            }

            int index = _indicatorEmitterSourceCombo?.SelectedIndex ?? _selectedIndicatorEmitterSourceIndex;
            index = Math.Clamp(index, 0, rig.Sources.Count - 1);
            _selectedIndicatorEmitterSourceIndex = index;
            source = rig.Sources[index];
            return true;
        }

        private void MoveSelectedIndicatorEmitterSource(int direction)
        {
            if (_project.ProjectType != InteractorProjectType.IndicatorLight || direction == 0)
            {
                return;
            }

            DynamicLightRig rig = _project.DynamicLightRig;
            if (rig.Sources.Count < 2)
            {
                return;
            }

            int selectedIndex = _indicatorEmitterSourceCombo?.SelectedIndex ?? _selectedIndicatorEmitterSourceIndex;
            selectedIndex = Math.Clamp(selectedIndex, 0, rig.Sources.Count - 1);
            int targetIndex = selectedIndex + direction;
            if (targetIndex < 0 || targetIndex >= rig.Sources.Count)
            {
                return;
            }

            (rig.Sources[selectedIndex], rig.Sources[targetIndex]) = (rig.Sources[targetIndex], rig.Sources[selectedIndex]);
            _selectedIndicatorEmitterSourceIndex = targetIndex;
            RefreshIndicatorEmitterSourceControlsFromProject(loadSelectedEmitterIntoUi: true);
            UpdateReadouts();
            NotifyProjectStateChanged();
        }

        private void AutoDistributeIndicatorEmitterPhases()
        {
            if (_project.ProjectType != InteractorProjectType.IndicatorLight)
            {
                return;
            }

            DynamicLightRig rig = _project.DynamicLightRig;
            if (rig.Sources.Count == 0)
            {
                return;
            }

            for (int i = 0; i < rig.Sources.Count; i++)
            {
                rig.Sources[i].AnimationPhaseOffsetDegrees = DynamicLightRig.BuildDefaultPhaseOffsetDegrees(i, rig.Sources.Count);
            }

            LoadSelectedEmitterSourceIntoUi();
            UpdateReadouts();
            NotifyProjectStateChanged();
        }

        private void SyncIndicatorDynamicLightSourcesToAssembly(bool recenterSources = true)
        {
            if (_project.ProjectType != InteractorProjectType.IndicatorLight)
            {
                return;
            }

            DynamicLightRig rig = _project.DynamicLightRig;
            int emitterCount = Math.Clamp(_project.IndicatorEmitterCount, 1, 8);
            while (rig.Sources.Count < emitterCount)
            {
                int sourceIndex = rig.Sources.Count;
                rig.Sources.Add(new DynamicLightSource
                {
                    Name = DynamicLightRig.BuildDefaultSourceName(sourceIndex),
                    Enabled = true,
                    Color = new SKColor(180, 255, 210, 255),
                    Intensity = 1f,
                    Radius = 220f,
                    Falloff = 1f,
                    AnimationPhaseOffsetDegrees = DynamicLightRig.BuildDefaultPhaseOffsetDegrees(sourceIndex, emitterCount)
                });
            }

            if (rig.Sources.Count > emitterCount)
            {
                rig.Sources.RemoveRange(emitterCount, rig.Sources.Count - emitterCount);
            }

            float spread = MathF.Max(0f, _project.IndicatorEmitterSpread);
            float baseHeight = MathF.Max(1f, _project.IndicatorBaseHeight);
            float baseThickness = MathF.Max(1f, _project.IndicatorBaseThickness);
            float housingHeight = MathF.Max(1f, _project.IndicatorHousingHeight);
            float emitterDepth = _project.IndicatorEmitterDepth;

            float centerY = -baseHeight * 0.45f;
            float baseTop = (-(baseThickness * 0.5f) - 8f) + (baseThickness * 0.5f);
            float lensRadius = _project.IndicatorLensRadius > 0f
                ? _project.IndicatorLensRadius
                : MathF.Max(8f, _project.IndicatorHousingRadius * 0.78f);
            float emitterZ = baseTop + housingHeight + emitterDepth + (lensRadius * 0.35f);
            float defaultEmitterLightRadius = Math.Clamp(MathF.Max(24f, lensRadius * 1.10f), 24f, 512f);
            bool looksLikeLegacyPlacement =
                !recenterSources &&
                rig.Sources.Count > 0 &&
                rig.Sources.All(source => MathF.Abs(source.Y) <= 1f && MathF.Abs(source.Z + 28f) <= 8f);
            bool shouldRecenter = recenterSources || looksLikeLegacyPlacement;

            for (int i = 0; i < rig.Sources.Count; i++)
            {
                float t = rig.Sources.Count == 1 ? 0.5f : i / (float)(rig.Sources.Count - 1);
                float x = (t - 0.5f) * spread;
                DynamicLightSource source = rig.Sources[i];
                if (shouldRecenter)
                {
                    source.X = x;
                    source.Y = centerY;
                    source.Z = emitterZ;
                }

                bool usesLegacyEmitterLightDefaults =
                    source.Radius >= 160f &&
                    source.Falloff <= 1.05f;
                if (looksLikeLegacyPlacement || usesLegacyEmitterLightDefaults)
                {
                    source.Radius = defaultEmitterLightRadius;
                    source.Falloff = 6f;
                }

                DynamicLightRig.NormalizeSourceIdentity(source, i, rig.Sources.Count);
            }
        }

        private static string NormalizeIndicatorEmitterName(string? candidate, int sourceIndex)
        {
            string trimmed = (candidate ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(trimmed)
                ? DynamicLightRig.BuildDefaultSourceName(sourceIndex)
                : trimmed;
        }
    }
}
