using System;
using Avalonia;
using Avalonia.Controls;
using KnobForge.Core;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void OnToggleAssemblySettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi ||
                _toggleAssemblyModeCombo == null ||
                _toggleBaseMeshCombo == null ||
                _toggleLeverMeshCombo == null ||
                _toggleStateCountCombo == null ||
                _toggleStateIndexSlider == null ||
                _toggleMaxAngleSlider == null ||
                _togglePlateWidthSlider == null ||
                _togglePlateHeightSlider == null ||
                _togglePlateThicknessSlider == null ||
                _togglePlateOffsetYSlider == null ||
                _togglePlateOffsetZSlider == null ||
                _toggleBushingRadiusSlider == null ||
                _toggleBushingHeightSlider == null ||
                _toggleBushingSidesSlider == null ||
                _toggleLowerBushingShapeCombo == null ||
                _toggleUpperBushingShapeCombo == null ||
                _toggleLowerBushingRadiusScaleSlider == null ||
                _toggleLowerBushingHeightRatioSlider == null ||
                _toggleUpperBushingRadiusScaleSlider == null ||
                _toggleUpperBushingHeightRatioSlider == null ||
                _toggleUpperBushingKnurlAmountSlider == null ||
                _toggleUpperBushingKnurlDensitySlider == null ||
                _toggleUpperBushingKnurlDepthSlider == null ||
                _toggleUpperBushingAnisotropyStrengthSlider == null ||
                _toggleUpperBushingAnisotropyDensitySlider == null ||
                _toggleUpperBushingAnisotropyAngleSlider == null ||
                _toggleUpperBushingSurfaceCharacterSlider == null ||
                _togglePivotHousingRadiusSlider == null ||
                _togglePivotHousingDepthSlider == null ||
                _togglePivotHousingBevelSlider == null ||
                _togglePivotBallRadiusSlider == null ||
                _togglePivotClearanceSlider == null ||
                _toggleInvertBaseWindingCheckBox == null ||
                _toggleInvertLeverWindingCheckBox == null ||
                _toggleLeverLengthSlider == null ||
                _toggleLeverRadiusSlider == null ||
                _toggleLeverTopRadiusSlider == null ||
                _toggleLeverSidesSlider == null ||
                _toggleLeverPivotOffsetSlider == null ||
                _toggleTipRadiusSlider == null ||
                _toggleTipLatitudeSegmentsSlider == null ||
                _toggleTipLongitudeSegmentsSlider == null ||
                _toggleTipSleeveEnabledCheckBox == null ||
                _toggleTipSleeveLengthSlider == null ||
                _toggleTipSleeveThicknessSlider == null ||
                _toggleTipSleeveOuterRadiusSlider == null ||
                _toggleTipSleeveCoverageSlider == null ||
                _toggleTipSleeveSidesSlider == null ||
                _toggleTipSleeveStyleCombo == null ||
                _toggleTipSleeveTipStyleCombo == null ||
                _toggleTipSleevePatternCountSlider == null ||
                _toggleTipSleevePatternDepthSlider == null ||
                _toggleTipSleeveTipAmountSlider == null ||
                _toggleTipSleeveColorRSlider == null ||
                _toggleTipSleeveColorGSlider == null ||
                _toggleTipSleeveColorBSlider == null ||
                _toggleTipSleeveMetallicSlider == null ||
                _toggleTipSleeveRoughnessSlider == null ||
                _toggleTipSleevePearlescenceSlider == null ||
                _toggleTipSleeveDiffuseStrengthSlider == null ||
                _toggleTipSleeveSpecularStrengthSlider == null ||
                _toggleTipSleeveRustSlider == null ||
                _toggleTipSleeveWearSlider == null ||
                _toggleTipSleeveGunkSlider == null)
            {
                return;
            }

            if (ReferenceEquals(sender, _toggleAssemblyModeCombo) ||
                ReferenceEquals(sender, _toggleStateCountCombo) ||
                ReferenceEquals(sender, _toggleLowerBushingShapeCombo) ||
                ReferenceEquals(sender, _toggleUpperBushingShapeCombo) ||
                ReferenceEquals(sender, _toggleTipSleeveStyleCombo) ||
                ReferenceEquals(sender, _toggleTipSleeveTipStyleCombo))
            {
                if (e.Property != ComboBox.SelectedItemProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _toggleBaseMeshCombo) ||
                     ReferenceEquals(sender, _toggleLeverMeshCombo))
            {
                if (e.Property != ComboBox.SelectedItemProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _toggleInvertBaseWindingCheckBox))
            {
                if (e.Property != CheckBox.IsCheckedProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _toggleInvertLeverWindingCheckBox))
            {
                if (e.Property != CheckBox.IsCheckedProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _toggleTipSleeveEnabledCheckBox))
            {
                if (e.Property != CheckBox.IsCheckedProperty)
                {
                    return;
                }
            }
            else if (e.Property != Slider.ValueProperty)
            {
                return;
            }

            if (GetModelNode() == null)
            {
                return;
            }

            bool requestHeavyRefresh =
                !ReferenceEquals(sender, _toggleInvertBaseWindingCheckBox) &&
                !ReferenceEquals(sender, _toggleInvertLeverWindingCheckBox);
            ApplyToggleAssemblyUiToProject(requestHeavyRefresh);
        }

        private void ApplyToggleAssemblyUiToProject(bool requestHeavyRefresh)
        {
            if (_toggleAssemblyModeCombo == null ||
                _toggleBaseMeshCombo == null ||
                _toggleLeverMeshCombo == null ||
                _toggleStateCountCombo == null ||
                _toggleStateIndexSlider == null ||
                _toggleMaxAngleSlider == null ||
                _togglePlateWidthSlider == null ||
                _togglePlateHeightSlider == null ||
                _togglePlateThicknessSlider == null ||
                _togglePlateOffsetYSlider == null ||
                _togglePlateOffsetZSlider == null ||
                _toggleBushingRadiusSlider == null ||
                _toggleBushingHeightSlider == null ||
                _toggleBushingSidesSlider == null ||
                _toggleLowerBushingShapeCombo == null ||
                _toggleUpperBushingShapeCombo == null ||
                _toggleLowerBushingRadiusScaleSlider == null ||
                _toggleLowerBushingHeightRatioSlider == null ||
                _toggleUpperBushingRadiusScaleSlider == null ||
                _toggleUpperBushingHeightRatioSlider == null ||
                _toggleUpperBushingKnurlAmountSlider == null ||
                _toggleUpperBushingKnurlDensitySlider == null ||
                _toggleUpperBushingKnurlDepthSlider == null ||
                _toggleUpperBushingAnisotropyStrengthSlider == null ||
                _toggleUpperBushingAnisotropyDensitySlider == null ||
                _toggleUpperBushingAnisotropyAngleSlider == null ||
                _toggleUpperBushingSurfaceCharacterSlider == null ||
                _togglePivotHousingRadiusSlider == null ||
                _togglePivotHousingDepthSlider == null ||
                _togglePivotHousingBevelSlider == null ||
                _togglePivotBallRadiusSlider == null ||
                _togglePivotClearanceSlider == null ||
                _toggleInvertBaseWindingCheckBox == null ||
                _toggleInvertLeverWindingCheckBox == null ||
                _toggleLeverLengthSlider == null ||
                _toggleLeverRadiusSlider == null ||
                _toggleLeverTopRadiusSlider == null ||
                _toggleLeverSidesSlider == null ||
                _toggleLeverPivotOffsetSlider == null ||
                _toggleTipRadiusSlider == null ||
                _toggleTipLatitudeSegmentsSlider == null ||
                _toggleTipLongitudeSegmentsSlider == null ||
                _toggleTipSleeveEnabledCheckBox == null ||
                _toggleTipSleeveLengthSlider == null ||
                _toggleTipSleeveThicknessSlider == null ||
                _toggleTipSleeveOuterRadiusSlider == null ||
                _toggleTipSleeveCoverageSlider == null ||
                _toggleTipSleeveSidesSlider == null ||
                _toggleTipSleeveStyleCombo == null ||
                _toggleTipSleeveTipStyleCombo == null ||
                _toggleTipSleevePatternCountSlider == null ||
                _toggleTipSleevePatternDepthSlider == null ||
                _toggleTipSleeveTipAmountSlider == null ||
                _toggleTipSleeveColorRSlider == null ||
                _toggleTipSleeveColorGSlider == null ||
                _toggleTipSleeveColorBSlider == null ||
                _toggleTipSleeveMetallicSlider == null ||
                _toggleTipSleeveRoughnessSlider == null ||
                _toggleTipSleevePearlescenceSlider == null ||
                _toggleTipSleeveDiffuseStrengthSlider == null ||
                _toggleTipSleeveSpecularStrengthSlider == null ||
                _toggleTipSleeveRustSlider == null ||
                _toggleTipSleeveWearSlider == null ||
                _toggleTipSleeveGunkSlider == null)
            {
                return;
            }

            _project.ToggleMode = _toggleAssemblyModeCombo.SelectedItem is ToggleAssemblyMode mode
                ? mode
                : ToggleAssemblyMode.Auto;
            _project.ToggleBaseImportedMeshPath = ResolveSelectedToggleMeshPath(_toggleBaseMeshCombo.SelectedItem);
            _project.ToggleLeverImportedMeshPath = ResolveSelectedToggleMeshPath(_toggleLeverMeshCombo.SelectedItem);
            _project.ToggleStateCount = _toggleStateCountCombo.SelectedItem is ToggleAssemblyStateCount stateCount
                ? stateCount
                : ToggleAssemblyStateCount.TwoPosition;
            int maxStateIndex = _project.ToggleStateCount == ToggleAssemblyStateCount.ThreePosition ? 2 : 1;
            int stateIndex = Math.Clamp((int)Math.Round(_toggleStateIndexSlider.Value), 0, maxStateIndex);
            _toggleStateIndexSlider.Maximum = maxStateIndex;
            _toggleStateIndexSlider.Value = stateIndex;
            _project.ToggleStateIndex = stateIndex;
            _project.ToggleMaxAngleDeg = (float)_toggleMaxAngleSlider.Value;
            _project.TogglePlateWidth = (float)_togglePlateWidthSlider.Value;
            _project.TogglePlateHeight = (float)_togglePlateHeightSlider.Value;
            _project.TogglePlateThickness = (float)_togglePlateThicknessSlider.Value;
            _project.TogglePlateOffsetY = (float)_togglePlateOffsetYSlider.Value;
            _project.TogglePlateOffsetZ = (float)_togglePlateOffsetZSlider.Value;
            _project.ToggleBushingRadius = (float)_toggleBushingRadiusSlider.Value;
            _project.ToggleBushingHeight = (float)_toggleBushingHeightSlider.Value;
            int bushingSides = Math.Clamp((int)Math.Round(_toggleBushingSidesSlider.Value), 3, 32);
            _toggleBushingSidesSlider.Value = bushingSides;
            _project.ToggleBushingSides = bushingSides;
            _project.ToggleLowerBushingShape = _toggleLowerBushingShapeCombo.SelectedItem is ToggleBushingShape lowerBushingShape
                ? lowerBushingShape
                : ToggleBushingShape.Hex;
            _project.ToggleUpperBushingShape = _toggleUpperBushingShapeCombo.SelectedItem is ToggleBushingShape upperBushingShape
                ? upperBushingShape
                : ToggleBushingShape.Hex;
            _project.ToggleLowerBushingRadiusScale = (float)_toggleLowerBushingRadiusScaleSlider.Value;
            _project.ToggleLowerBushingHeightRatio = (float)_toggleLowerBushingHeightRatioSlider.Value;
            _project.ToggleUpperBushingRadiusScale = (float)_toggleUpperBushingRadiusScaleSlider.Value;
            _project.ToggleUpperBushingHeightRatio = (float)_toggleUpperBushingHeightRatioSlider.Value;
            _project.ToggleUpperBushingKnurlAmount = (float)_toggleUpperBushingKnurlAmountSlider.Value;
            int upperBushingKnurlDensity = Math.Clamp((int)Math.Round(_toggleUpperBushingKnurlDensitySlider.Value), 3, 96);
            _toggleUpperBushingKnurlDensitySlider.Value = upperBushingKnurlDensity;
            _project.ToggleUpperBushingKnurlDensity = upperBushingKnurlDensity;
            _project.ToggleUpperBushingKnurlDepth = (float)_toggleUpperBushingKnurlDepthSlider.Value;
            _project.ToggleUpperBushingAnisotropyStrength = (float)_toggleUpperBushingAnisotropyStrengthSlider.Value;
            _project.ToggleUpperBushingAnisotropyDensity = (float)_toggleUpperBushingAnisotropyDensitySlider.Value;
            _project.ToggleUpperBushingAnisotropyAngleDegrees = (float)_toggleUpperBushingAnisotropyAngleSlider.Value;
            _project.ToggleUpperBushingSurfaceCharacter = (float)_toggleUpperBushingSurfaceCharacterSlider.Value;
            _project.TogglePivotHousingRadius = (float)_togglePivotHousingRadiusSlider.Value;
            _project.TogglePivotHousingDepth = (float)_togglePivotHousingDepthSlider.Value;
            _project.TogglePivotHousingBevel = (float)_togglePivotHousingBevelSlider.Value;
            _project.TogglePivotBallRadius = (float)_togglePivotBallRadiusSlider.Value;
            _project.TogglePivotClearance = (float)_togglePivotClearanceSlider.Value;
            _project.ToggleInvertBaseFrontFaceWinding = _toggleInvertBaseWindingCheckBox.IsChecked == true;
            _project.ToggleInvertLeverFrontFaceWinding = _toggleInvertLeverWindingCheckBox.IsChecked == true;
            _project.ToggleLeverLength = (float)_toggleLeverLengthSlider.Value;
            _project.ToggleLeverRadius = (float)_toggleLeverRadiusSlider.Value;
            _project.ToggleLeverTopRadius = (float)_toggleLeverTopRadiusSlider.Value;
            int leverSides = Math.Clamp((int)Math.Round(_toggleLeverSidesSlider.Value), 6, 64);
            _toggleLeverSidesSlider.Value = leverSides;
            _project.ToggleLeverSides = leverSides;
            _project.ToggleLeverPivotOffset = (float)_toggleLeverPivotOffsetSlider.Value;
            _project.ToggleTipRadius = (float)_toggleTipRadiusSlider.Value;
            int tipLatitudeSegments = Math.Clamp((int)Math.Round(_toggleTipLatitudeSegmentsSlider.Value), 4, 64);
            _toggleTipLatitudeSegmentsSlider.Value = tipLatitudeSegments;
            _project.ToggleTipLatitudeSegments = tipLatitudeSegments;
            int tipLongitudeSegments = Math.Clamp((int)Math.Round(_toggleTipLongitudeSegmentsSlider.Value), 6, 128);
            _toggleTipLongitudeSegmentsSlider.Value = tipLongitudeSegments;
            _project.ToggleTipLongitudeSegments = tipLongitudeSegments;
            _project.ToggleTipSleeveEnabled = _toggleTipSleeveEnabledCheckBox.IsChecked == true;
            _project.ToggleTipSleeveLength = (float)_toggleTipSleeveLengthSlider.Value;
            _project.ToggleTipSleeveThickness = (float)_toggleTipSleeveThicknessSlider.Value;
            _project.ToggleTipSleeveOuterRadius = (float)_toggleTipSleeveOuterRadiusSlider.Value;
            _project.ToggleTipSleeveCoverage = (float)_toggleTipSleeveCoverageSlider.Value;
            int tipSleeveSides = Math.Clamp((int)Math.Round(_toggleTipSleeveSidesSlider.Value), 6, 64);
            _toggleTipSleeveSidesSlider.Value = tipSleeveSides;
            _project.ToggleTipSleeveSides = tipSleeveSides;
            _project.ToggleTipSleeveStyle = _toggleTipSleeveStyleCombo.SelectedItem is ToggleTipSleeveStyle tipSleeveStyle
                ? tipSleeveStyle
                : ToggleTipSleeveStyle.Round;
            _project.ToggleTipSleeveTipStyle = _toggleTipSleeveTipStyleCombo.SelectedItem is ToggleTipSleeveTipStyle tipSleeveTipStyle
                ? tipSleeveTipStyle
                : ToggleTipSleeveTipStyle.Rounded;
            int tipSleevePatternCount = Math.Clamp((int)Math.Round(_toggleTipSleevePatternCountSlider.Value), 3, 64);
            _toggleTipSleevePatternCountSlider.Value = tipSleevePatternCount;
            _project.ToggleTipSleevePatternCount = tipSleevePatternCount;
            _project.ToggleTipSleevePatternDepth = (float)_toggleTipSleevePatternDepthSlider.Value;
            _project.ToggleTipSleeveTipAmount = (float)_toggleTipSleeveTipAmountSlider.Value;
            _project.ToggleTipSleeveColor = new System.Numerics.Vector3(
                (float)_toggleTipSleeveColorRSlider.Value,
                (float)_toggleTipSleeveColorGSlider.Value,
                (float)_toggleTipSleeveColorBSlider.Value);
            _project.ToggleTipSleeveMetallic = (float)_toggleTipSleeveMetallicSlider.Value;
            _project.ToggleTipSleeveRoughness = (float)_toggleTipSleeveRoughnessSlider.Value;
            _project.ToggleTipSleevePearlescence = (float)_toggleTipSleevePearlescenceSlider.Value;
            _project.ToggleTipSleeveDiffuseStrength = (float)_toggleTipSleeveDiffuseStrengthSlider.Value;
            _project.ToggleTipSleeveSpecularStrength = (float)_toggleTipSleeveSpecularStrengthSlider.Value;
            _project.ToggleTipSleeveRustAmount = (float)_toggleTipSleeveRustSlider.Value;
            _project.ToggleTipSleeveWearAmount = (float)_toggleTipSleeveWearSlider.Value;
            _project.ToggleTipSleeveGunkAmount = (float)_toggleTipSleeveGunkSlider.Value;

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
    }
}
