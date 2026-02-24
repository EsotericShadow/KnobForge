using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KnobForge.Core;
using SkiaSharp;
using System;

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
            else if (e.Property != Slider.ValueProperty)
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
                !ReferenceEquals(sender, _indicatorQuickBrightnessSlider) &&
                !ReferenceEquals(sender, _indicatorQuickGlowSlider) &&
                !ReferenceEquals(sender, _indicatorLightAnimationModeCombo) &&
                !ReferenceEquals(sender, _indicatorLensTransmissionSlider) &&
                !ReferenceEquals(sender, _indicatorLensIorSlider) &&
                !ReferenceEquals(sender, _indicatorLensThicknessSlider) &&
                !ReferenceEquals(sender, _indicatorLensAbsorptionSlider) &&
                !ReferenceEquals(sender, _indicatorLensSurfaceRoughnessSlider) &&
                !ReferenceEquals(sender, _indicatorLensSurfaceSpecularSlider) &&
                !ReferenceEquals(sender, _indicatorLensTintRSlider) &&
                !ReferenceEquals(sender, _indicatorLensTintGSlider) &&
                !ReferenceEquals(sender, _indicatorLensTintBSlider) &&
                !ReferenceEquals(sender, _indicatorLightAnimationSpeedSlider) &&
                !ReferenceEquals(sender, _indicatorLightFlickerAmountSlider) &&
                !ReferenceEquals(sender, _indicatorLightFlickerDropoutSlider) &&
                !ReferenceEquals(sender, _indicatorLightFlickerSmoothingSlider) &&
                !ReferenceEquals(sender, _indicatorLightFlickerSeedSlider);

            ApplyIndicatorLightUiToProject(requestHeavyRefresh);
        }

        private void ApplyIndicatorLightUiToProject(bool requestHeavyRefresh)
        {
            if (_indicatorAssemblyEnabledCheckBox == null ||
                _indicatorBaseWidthSlider == null ||
                _indicatorBaseHeightSlider == null ||
                _indicatorBaseThicknessSlider == null ||
                _indicatorHousingRadiusSlider == null ||
                _indicatorHousingHeightSlider == null ||
                _indicatorLensRadiusSlider == null ||
                _indicatorLensHeightSlider == null ||
                _indicatorLensTransmissionSlider == null ||
                _indicatorLensIorSlider == null ||
                _indicatorLensThicknessSlider == null ||
                _indicatorLensAbsorptionSlider == null ||
                _indicatorLensSurfaceRoughnessSlider == null ||
                _indicatorLensSurfaceSpecularSlider == null ||
                _indicatorLensTintRSlider == null ||
                _indicatorLensTintGSlider == null ||
                _indicatorLensTintBSlider == null ||
                _indicatorReflectorBaseRadiusSlider == null ||
                _indicatorReflectorTopRadiusSlider == null ||
                _indicatorReflectorDepthSlider == null ||
                _indicatorEmitterRadiusSlider == null ||
                _indicatorEmitterSpreadSlider == null ||
                _indicatorEmitterDepthSlider == null ||
                _indicatorEmitterCountSlider == null ||
                _indicatorRadialSegmentsSlider == null ||
                _indicatorLensLatitudeSegmentsSlider == null ||
                _indicatorLensLongitudeSegmentsSlider == null ||
                _indicatorDynamicLightsEnabledCheckBox == null ||
                _indicatorLightAnimationModeCombo == null ||
                _indicatorLightAnimationSpeedSlider == null ||
                _indicatorLightFlickerAmountSlider == null ||
                _indicatorLightFlickerDropoutSlider == null ||
                _indicatorLightFlickerSmoothingSlider == null ||
                _indicatorLightFlickerSeedSlider == null)
            {
                return;
            }

            _project.IndicatorAssemblyEnabled = _indicatorAssemblyEnabledCheckBox.IsChecked == true;
            _project.IndicatorBaseWidth = (float)_indicatorBaseWidthSlider.Value;
            _project.IndicatorBaseHeight = (float)_indicatorBaseHeightSlider.Value;
            _project.IndicatorBaseThickness = (float)_indicatorBaseThicknessSlider.Value;
            _project.IndicatorHousingRadius = (float)_indicatorHousingRadiusSlider.Value;
            _project.IndicatorHousingHeight = (float)_indicatorHousingHeightSlider.Value;
            _project.IndicatorLensRadius = (float)_indicatorLensRadiusSlider.Value;
            _project.IndicatorLensHeight = (float)_indicatorLensHeightSlider.Value;
            _project.IndicatorLensTransmission = (float)_indicatorLensTransmissionSlider.Value;
            _project.IndicatorLensIor = (float)_indicatorLensIorSlider.Value;
            _project.IndicatorLensThickness = (float)_indicatorLensThicknessSlider.Value;
            _project.IndicatorLensAbsorption = (float)_indicatorLensAbsorptionSlider.Value;
            _project.IndicatorLensSurfaceRoughness = (float)_indicatorLensSurfaceRoughnessSlider.Value;
            _project.IndicatorLensSurfaceSpecularStrength = (float)_indicatorLensSurfaceSpecularSlider.Value;
            _project.IndicatorLensTint = new System.Numerics.Vector3(
                (float)_indicatorLensTintRSlider.Value,
                (float)_indicatorLensTintGSlider.Value,
                (float)_indicatorLensTintBSlider.Value);
            _project.IndicatorReflectorBaseRadius = (float)_indicatorReflectorBaseRadiusSlider.Value;
            _project.IndicatorReflectorTopRadius = (float)_indicatorReflectorTopRadiusSlider.Value;
            _project.IndicatorReflectorDepth = (float)_indicatorReflectorDepthSlider.Value;
            _project.IndicatorEmitterRadius = (float)_indicatorEmitterRadiusSlider.Value;
            _project.IndicatorEmitterSpread = (float)_indicatorEmitterSpreadSlider.Value;
            _project.IndicatorEmitterDepth = (float)_indicatorEmitterDepthSlider.Value;

            int emitterCount = Math.Clamp((int)Math.Round(_indicatorEmitterCountSlider.Value), 1, 8);
            int radialSegments = Math.Clamp((int)Math.Round(_indicatorRadialSegmentsSlider.Value), 8, 96);
            int lensLatitudeSegments = Math.Clamp((int)Math.Round(_indicatorLensLatitudeSegmentsSlider.Value), 4, 64);
            int lensLongitudeSegments = Math.Clamp((int)Math.Round(_indicatorLensLongitudeSegmentsSlider.Value), 6, 96);

            _indicatorEmitterCountSlider.Value = emitterCount;
            _indicatorRadialSegmentsSlider.Value = radialSegments;
            _indicatorLensLatitudeSegmentsSlider.Value = lensLatitudeSegments;
            _indicatorLensLongitudeSegmentsSlider.Value = lensLongitudeSegments;

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
            rig.AnimationSpeed = (float)_indicatorLightAnimationSpeedSlider.Value;
            rig.FlickerAmount = (float)_indicatorLightFlickerAmountSlider.Value;
            rig.FlickerDropoutChance = (float)_indicatorLightFlickerDropoutSlider.Value;
            rig.FlickerSmoothing = (float)_indicatorLightFlickerSmoothingSlider.Value;
            rig.MasterIntensity = _indicatorQuickBrightnessSlider != null
                ? (float)_indicatorQuickBrightnessSlider.Value
                : 1f;
            rig.EmissiveGlow = _indicatorQuickGlowSlider != null
                ? (float)_indicatorQuickGlowSlider.Value
                : 1f;
            int seed = Math.Clamp((int)Math.Round(_indicatorLightFlickerSeedSlider.Value), 1, 100000);
            _indicatorLightFlickerSeedSlider.Value = seed;
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
                _indicatorLightAnimationSpeedSlider == null ||
                _indicatorLightFlickerAmountSlider == null ||
                _indicatorLightFlickerDropoutSlider == null ||
                _indicatorLightFlickerSmoothingSlider == null ||
                _indicatorLightFlickerSeedSlider == null)
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
                        _indicatorLightAnimationSpeedSlider.Value = 1.35;
                        _indicatorLightFlickerAmountSlider.Value = 0.20;
                        _indicatorLightFlickerDropoutSlider.Value = 0.00;
                        _indicatorLightFlickerSmoothingSlider.Value = 0.62;
                        _indicatorLightFlickerSeedSlider.Value = 1337;
                        if (_indicatorQuickBrightnessSlider != null)
                        {
                            _indicatorQuickBrightnessSlider.Value = 1.55;
                        }
                        if (_indicatorQuickGlowSlider != null)
                        {
                            _indicatorQuickGlowSlider.Value = 1.65;
                        }
                        break;
                    case IndicatorLightPreset.Flicker:
                        _indicatorLightAnimationModeCombo.SelectedItem = DynamicLightAnimationMode.Flicker;
                        _indicatorLightAnimationSpeedSlider.Value = 2.60;
                        _indicatorLightFlickerAmountSlider.Value = 0.55;
                        _indicatorLightFlickerDropoutSlider.Value = 0.14;
                        _indicatorLightFlickerSmoothingSlider.Value = 0.22;
                        _indicatorLightFlickerSeedSlider.Value = 4242;
                        if (_indicatorQuickBrightnessSlider != null)
                        {
                            _indicatorQuickBrightnessSlider.Value = 1.80;
                        }
                        if (_indicatorQuickGlowSlider != null)
                        {
                            _indicatorQuickGlowSlider.Value = 2.00;
                        }
                        break;
                    default:
                        _indicatorLightAnimationModeCombo.SelectedItem = DynamicLightAnimationMode.Steady;
                        _indicatorLightAnimationSpeedSlider.Value = 1.00;
                        _indicatorLightFlickerAmountSlider.Value = 0.00;
                        _indicatorLightFlickerDropoutSlider.Value = 0.00;
                        _indicatorLightFlickerSmoothingSlider.Value = 0.50;
                        _indicatorLightFlickerSeedSlider.Value = 1337;
                        if (_indicatorQuickBrightnessSlider != null)
                        {
                            _indicatorQuickBrightnessSlider.Value = 1.25;
                        }
                        if (_indicatorQuickGlowSlider != null)
                        {
                            _indicatorQuickGlowSlider.Value = 1.20;
                        }
                        break;
                }
            });

            ApplyIndicatorLightUiToProject(requestHeavyRefresh: false);
        }

        private void ApplyIndicatorLensMaterialPreset(IndicatorLensMaterialPreset preset)
        {
            if (_indicatorLensTransmissionSlider == null ||
                _indicatorLensIorSlider == null ||
                _indicatorLensThicknessSlider == null ||
                _indicatorLensAbsorptionSlider == null ||
                _indicatorLensSurfaceRoughnessSlider == null ||
                _indicatorLensSurfaceSpecularSlider == null ||
                _indicatorLensTintRSlider == null ||
                _indicatorLensTintGSlider == null ||
                _indicatorLensTintBSlider == null)
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
                _indicatorLensTransmissionSlider.Value = values.Transmission;
                _indicatorLensIorSlider.Value = values.Ior;
                _indicatorLensThicknessSlider.Value = values.Thickness;
                _indicatorLensAbsorptionSlider.Value = values.Absorption;
                _indicatorLensSurfaceRoughnessSlider.Value = values.SurfaceRoughness;
                _indicatorLensSurfaceSpecularSlider.Value = values.SurfaceSpecularStrength;
                _indicatorLensTintRSlider.Value = values.Tint.X;
                _indicatorLensTintGSlider.Value = values.Tint.Y;
                _indicatorLensTintBSlider.Value = values.Tint.Z;
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
            else if (e.Property != Slider.ValueProperty)
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

            if (_indicatorEmitterSourcePhaseOffsetSlider != null)
            {
                source.AnimationPhaseOffsetDegrees = (float)_indicatorEmitterSourcePhaseOffsetSlider.Value;
            }

            if (_indicatorEmitterSourceXSlider != null)
            {
                source.X = (float)_indicatorEmitterSourceXSlider.Value;
            }

            if (_indicatorEmitterSourceYSlider != null)
            {
                source.Y = (float)_indicatorEmitterSourceYSlider.Value;
            }

            if (_indicatorEmitterSourceZSlider != null)
            {
                source.Z = (float)_indicatorEmitterSourceZSlider.Value;
            }

            if (_indicatorEmitterSourceIntensitySlider != null)
            {
                source.Intensity = (float)_indicatorEmitterSourceIntensitySlider.Value;
            }

            if (_indicatorEmitterSourceRadiusSlider != null)
            {
                source.Radius = (float)_indicatorEmitterSourceRadiusSlider.Value;
            }

            if (_indicatorEmitterSourceFalloffSlider != null)
            {
                source.Falloff = (float)_indicatorEmitterSourceFalloffSlider.Value;
            }

            if (_indicatorEmitterSourceRSlider != null &&
                _indicatorEmitterSourceGSlider != null &&
                _indicatorEmitterSourceBSlider != null)
            {
                byte r = (byte)Math.Clamp((int)Math.Round(_indicatorEmitterSourceRSlider.Value * 255d), 0, 255);
                byte g = (byte)Math.Clamp((int)Math.Round(_indicatorEmitterSourceGSlider.Value * 255d), 0, 255);
                byte b = (byte)Math.Clamp((int)Math.Round(_indicatorEmitterSourceBSlider.Value * 255d), 0, 255);
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
                if (_indicatorEmitterSourcePhaseOffsetSlider != null)
                {
                    _indicatorEmitterSourcePhaseOffsetSlider.IsEnabled = false;
                    _indicatorEmitterSourcePhaseOffsetSlider.Value = 0d;
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

                if (_indicatorEmitterSourcePhaseOffsetSlider != null)
                {
                    _indicatorEmitterSourcePhaseOffsetSlider.IsEnabled = true;
                    _indicatorEmitterSourcePhaseOffsetSlider.Value = source.AnimationPhaseOffsetDegrees;
                }

                if (_indicatorEmitterSourceXSlider != null)
                {
                    _indicatorEmitterSourceXSlider.Value = source.X;
                }

                if (_indicatorEmitterSourceYSlider != null)
                {
                    _indicatorEmitterSourceYSlider.Value = source.Y;
                }

                if (_indicatorEmitterSourceZSlider != null)
                {
                    _indicatorEmitterSourceZSlider.Value = source.Z;
                }

                if (_indicatorEmitterSourceIntensitySlider != null)
                {
                    _indicatorEmitterSourceIntensitySlider.Value = source.Intensity;
                }

                if (_indicatorEmitterSourceRadiusSlider != null)
                {
                    _indicatorEmitterSourceRadiusSlider.Value = source.Radius;
                }

                if (_indicatorEmitterSourceFalloffSlider != null)
                {
                    _indicatorEmitterSourceFalloffSlider.Value = source.Falloff;
                }

                if (_indicatorEmitterSourceRSlider != null)
                {
                    _indicatorEmitterSourceRSlider.Value = source.Color.Red / 255d;
                }

                if (_indicatorEmitterSourceGSlider != null)
                {
                    _indicatorEmitterSourceGSlider.Value = source.Color.Green / 255d;
                }

                if (_indicatorEmitterSourceBSlider != null)
                {
                    _indicatorEmitterSourceBSlider.Value = source.Color.Blue / 255d;
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
            float emitterZ = baseTop + housingHeight + emitterDepth;

            for (int i = 0; i < rig.Sources.Count; i++)
            {
                float t = rig.Sources.Count == 1 ? 0.5f : i / (float)(rig.Sources.Count - 1);
                float x = (t - 0.5f) * spread;
                DynamicLightSource source = rig.Sources[i];
                if (recenterSources)
                {
                    source.X = x;
                    source.Y = centerY;
                    source.Z = emitterZ;
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
