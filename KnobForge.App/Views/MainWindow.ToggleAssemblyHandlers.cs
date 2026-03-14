using System;
using Avalonia;
using Avalonia.Controls;
using KnobForge.App.Controls;
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
                _toggleStateIndexInput == null ||
                _toggleMaxAngleInput == null ||
                _togglePlateWidthInput == null ||
                _togglePlateHeightInput == null ||
                _togglePlateThicknessInput == null ||
                _togglePlateOffsetYInput == null ||
                _togglePlateOffsetZInput == null ||
                _toggleBushingRadiusInput == null ||
                _toggleBushingHeightInput == null ||
                _toggleBushingSidesInput == null ||
                _toggleLowerBushingShapeCombo == null ||
                _toggleUpperBushingShapeCombo == null ||
                _toggleLowerBushingRadiusScaleInput == null ||
                _toggleLowerBushingHeightRatioInput == null ||
                _toggleUpperBushingRadiusScaleInput == null ||
                _toggleUpperBushingHeightRatioInput == null ||
                _toggleUpperBushingKnurlAmountInput == null ||
                _toggleUpperBushingKnurlDensityInput == null ||
                _toggleUpperBushingKnurlDepthInput == null ||
                _toggleUpperBushingAnisotropyStrengthInput == null ||
                _toggleUpperBushingAnisotropyDensityInput == null ||
                _toggleUpperBushingAnisotropyAngleInput == null ||
                _toggleUpperBushingSurfaceCharacterInput == null ||
                _togglePivotHousingRadiusInput == null ||
                _togglePivotHousingDepthInput == null ||
                _togglePivotHousingBevelInput == null ||
                _togglePivotBallRadiusInput == null ||
                _togglePivotClearanceInput == null ||
                _toggleInvertBaseWindingCheckBox == null ||
                _toggleInvertLeverWindingCheckBox == null ||
                _toggleLeverLengthInput == null ||
                _toggleLeverRadiusInput == null ||
                _toggleLeverTopRadiusInput == null ||
                _toggleLeverSidesInput == null ||
                _toggleLeverPivotOffsetInput == null ||
                _toggleTipRadiusInput == null ||
                _toggleTipLatitudeSegmentsInput == null ||
                _toggleTipLongitudeSegmentsInput == null ||
                _toggleTipSleeveEnabledCheckBox == null ||
                _toggleTipSleeveLengthInput == null ||
                _toggleTipSleeveThicknessInput == null ||
                _toggleTipSleeveOuterRadiusInput == null ||
                _toggleTipSleeveCoverageInput == null ||
                _toggleTipSleeveSidesInput == null ||
                _toggleTipSleeveStyleCombo == null ||
                _toggleTipSleeveTipStyleCombo == null ||
                _toggleTipSleevePatternCountInput == null ||
                _toggleTipSleevePatternDepthInput == null ||
                _toggleTipSleeveTipAmountInput == null ||
                _toggleTipSleeveColorRInput == null ||
                _toggleTipSleeveColorGInput == null ||
                _toggleTipSleeveColorBInput == null ||
                _toggleTipSleeveMetallicInput == null ||
                _toggleTipSleeveRoughnessInput == null ||
                _toggleTipSleevePearlescenceInput == null ||
                _toggleTipSleeveDiffuseStrengthInput == null ||
                _toggleTipSleeveSpecularStrengthInput == null ||
                _toggleTipSleeveRustInput == null ||
                _toggleTipSleeveWearInput == null ||
                _toggleTipSleeveGunkInput == null)
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
            else if (e.Property != ValueInput.ValueProperty)
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
                _toggleStateIndexInput == null ||
                _toggleMaxAngleInput == null ||
                _togglePlateWidthInput == null ||
                _togglePlateHeightInput == null ||
                _togglePlateThicknessInput == null ||
                _togglePlateOffsetYInput == null ||
                _togglePlateOffsetZInput == null ||
                _toggleBushingRadiusInput == null ||
                _toggleBushingHeightInput == null ||
                _toggleBushingSidesInput == null ||
                _toggleLowerBushingShapeCombo == null ||
                _toggleUpperBushingShapeCombo == null ||
                _toggleLowerBushingRadiusScaleInput == null ||
                _toggleLowerBushingHeightRatioInput == null ||
                _toggleUpperBushingRadiusScaleInput == null ||
                _toggleUpperBushingHeightRatioInput == null ||
                _toggleUpperBushingKnurlAmountInput == null ||
                _toggleUpperBushingKnurlDensityInput == null ||
                _toggleUpperBushingKnurlDepthInput == null ||
                _toggleUpperBushingAnisotropyStrengthInput == null ||
                _toggleUpperBushingAnisotropyDensityInput == null ||
                _toggleUpperBushingAnisotropyAngleInput == null ||
                _toggleUpperBushingSurfaceCharacterInput == null ||
                _togglePivotHousingRadiusInput == null ||
                _togglePivotHousingDepthInput == null ||
                _togglePivotHousingBevelInput == null ||
                _togglePivotBallRadiusInput == null ||
                _togglePivotClearanceInput == null ||
                _toggleInvertBaseWindingCheckBox == null ||
                _toggleInvertLeverWindingCheckBox == null ||
                _toggleLeverLengthInput == null ||
                _toggleLeverRadiusInput == null ||
                _toggleLeverTopRadiusInput == null ||
                _toggleLeverSidesInput == null ||
                _toggleLeverPivotOffsetInput == null ||
                _toggleTipRadiusInput == null ||
                _toggleTipLatitudeSegmentsInput == null ||
                _toggleTipLongitudeSegmentsInput == null ||
                _toggleTipSleeveEnabledCheckBox == null ||
                _toggleTipSleeveLengthInput == null ||
                _toggleTipSleeveThicknessInput == null ||
                _toggleTipSleeveOuterRadiusInput == null ||
                _toggleTipSleeveCoverageInput == null ||
                _toggleTipSleeveSidesInput == null ||
                _toggleTipSleeveStyleCombo == null ||
                _toggleTipSleeveTipStyleCombo == null ||
                _toggleTipSleevePatternCountInput == null ||
                _toggleTipSleevePatternDepthInput == null ||
                _toggleTipSleeveTipAmountInput == null ||
                _toggleTipSleeveColorRInput == null ||
                _toggleTipSleeveColorGInput == null ||
                _toggleTipSleeveColorBInput == null ||
                _toggleTipSleeveMetallicInput == null ||
                _toggleTipSleeveRoughnessInput == null ||
                _toggleTipSleevePearlescenceInput == null ||
                _toggleTipSleeveDiffuseStrengthInput == null ||
                _toggleTipSleeveSpecularStrengthInput == null ||
                _toggleTipSleeveRustInput == null ||
                _toggleTipSleeveWearInput == null ||
                _toggleTipSleeveGunkInput == null)
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
            int stateIndex = Math.Clamp((int)Math.Round(_toggleStateIndexInput.Value), 0, maxStateIndex);
            _toggleStateIndexInput.Maximum = maxStateIndex;
            _toggleStateIndexInput.Value = stateIndex;
            _project.ToggleStateIndex = stateIndex;
            _project.ToggleMaxAngleDeg = (float)_toggleMaxAngleInput.Value;
            _project.TogglePlateWidth = (float)_togglePlateWidthInput.Value;
            _project.TogglePlateHeight = (float)_togglePlateHeightInput.Value;
            _project.TogglePlateThickness = (float)_togglePlateThicknessInput.Value;
            _project.TogglePlateOffsetY = (float)_togglePlateOffsetYInput.Value;
            _project.TogglePlateOffsetZ = (float)_togglePlateOffsetZInput.Value;
            _project.ToggleBushingRadius = (float)_toggleBushingRadiusInput.Value;
            _project.ToggleBushingHeight = (float)_toggleBushingHeightInput.Value;
            int bushingSides = Math.Clamp((int)Math.Round(_toggleBushingSidesInput.Value), 3, 32);
            _toggleBushingSidesInput.Value = bushingSides;
            _project.ToggleBushingSides = bushingSides;
            _project.ToggleLowerBushingShape = _toggleLowerBushingShapeCombo.SelectedItem is ToggleBushingShape lowerBushingShape
                ? lowerBushingShape
                : ToggleBushingShape.Hex;
            _project.ToggleUpperBushingShape = _toggleUpperBushingShapeCombo.SelectedItem is ToggleBushingShape upperBushingShape
                ? upperBushingShape
                : ToggleBushingShape.Hex;
            _project.ToggleLowerBushingRadiusScale = (float)_toggleLowerBushingRadiusScaleInput.Value;
            _project.ToggleLowerBushingHeightRatio = (float)_toggleLowerBushingHeightRatioInput.Value;
            _project.ToggleUpperBushingRadiusScale = (float)_toggleUpperBushingRadiusScaleInput.Value;
            _project.ToggleUpperBushingHeightRatio = (float)_toggleUpperBushingHeightRatioInput.Value;
            _project.ToggleUpperBushingKnurlAmount = (float)_toggleUpperBushingKnurlAmountInput.Value;
            int upperBushingKnurlDensity = Math.Clamp((int)Math.Round(_toggleUpperBushingKnurlDensityInput.Value), 3, 96);
            _toggleUpperBushingKnurlDensityInput.Value = upperBushingKnurlDensity;
            _project.ToggleUpperBushingKnurlDensity = upperBushingKnurlDensity;
            _project.ToggleUpperBushingKnurlDepth = (float)_toggleUpperBushingKnurlDepthInput.Value;
            _project.ToggleUpperBushingAnisotropyStrength = (float)_toggleUpperBushingAnisotropyStrengthInput.Value;
            _project.ToggleUpperBushingAnisotropyDensity = (float)_toggleUpperBushingAnisotropyDensityInput.Value;
            _project.ToggleUpperBushingAnisotropyAngleDegrees = (float)_toggleUpperBushingAnisotropyAngleInput.Value;
            _project.ToggleUpperBushingSurfaceCharacter = (float)_toggleUpperBushingSurfaceCharacterInput.Value;
            _project.TogglePivotHousingRadius = (float)_togglePivotHousingRadiusInput.Value;
            _project.TogglePivotHousingDepth = (float)_togglePivotHousingDepthInput.Value;
            _project.TogglePivotHousingBevel = (float)_togglePivotHousingBevelInput.Value;
            _project.TogglePivotBallRadius = (float)_togglePivotBallRadiusInput.Value;
            _project.TogglePivotClearance = (float)_togglePivotClearanceInput.Value;
            _project.ToggleInvertBaseFrontFaceWinding = _toggleInvertBaseWindingCheckBox.IsChecked == true;
            _project.ToggleInvertLeverFrontFaceWinding = _toggleInvertLeverWindingCheckBox.IsChecked == true;
            _project.ToggleLeverLength = (float)_toggleLeverLengthInput.Value;
            _project.ToggleLeverRadius = (float)_toggleLeverRadiusInput.Value;
            _project.ToggleLeverTopRadius = (float)_toggleLeverTopRadiusInput.Value;
            int leverSides = Math.Clamp((int)Math.Round(_toggleLeverSidesInput.Value), 6, 64);
            _toggleLeverSidesInput.Value = leverSides;
            _project.ToggleLeverSides = leverSides;
            _project.ToggleLeverPivotOffset = (float)_toggleLeverPivotOffsetInput.Value;
            _project.ToggleTipRadius = (float)_toggleTipRadiusInput.Value;
            int tipLatitudeSegments = Math.Clamp((int)Math.Round(_toggleTipLatitudeSegmentsInput.Value), 4, 64);
            _toggleTipLatitudeSegmentsInput.Value = tipLatitudeSegments;
            _project.ToggleTipLatitudeSegments = tipLatitudeSegments;
            int tipLongitudeSegments = Math.Clamp((int)Math.Round(_toggleTipLongitudeSegmentsInput.Value), 6, 128);
            _toggleTipLongitudeSegmentsInput.Value = tipLongitudeSegments;
            _project.ToggleTipLongitudeSegments = tipLongitudeSegments;
            _project.ToggleTipSleeveEnabled = _toggleTipSleeveEnabledCheckBox.IsChecked == true;
            _project.ToggleTipSleeveLength = (float)_toggleTipSleeveLengthInput.Value;
            _project.ToggleTipSleeveThickness = (float)_toggleTipSleeveThicknessInput.Value;
            _project.ToggleTipSleeveOuterRadius = (float)_toggleTipSleeveOuterRadiusInput.Value;
            _project.ToggleTipSleeveCoverage = (float)_toggleTipSleeveCoverageInput.Value;
            int tipSleeveSides = Math.Clamp((int)Math.Round(_toggleTipSleeveSidesInput.Value), 6, 64);
            _toggleTipSleeveSidesInput.Value = tipSleeveSides;
            _project.ToggleTipSleeveSides = tipSleeveSides;
            _project.ToggleTipSleeveStyle = _toggleTipSleeveStyleCombo.SelectedItem is ToggleTipSleeveStyle tipSleeveStyle
                ? tipSleeveStyle
                : ToggleTipSleeveStyle.Round;
            _project.ToggleTipSleeveTipStyle = _toggleTipSleeveTipStyleCombo.SelectedItem is ToggleTipSleeveTipStyle tipSleeveTipStyle
                ? tipSleeveTipStyle
                : ToggleTipSleeveTipStyle.Rounded;
            int tipSleevePatternCount = Math.Clamp((int)Math.Round(_toggleTipSleevePatternCountInput.Value), 3, 64);
            _toggleTipSleevePatternCountInput.Value = tipSleevePatternCount;
            _project.ToggleTipSleevePatternCount = tipSleevePatternCount;
            _project.ToggleTipSleevePatternDepth = (float)_toggleTipSleevePatternDepthInput.Value;
            _project.ToggleTipSleeveTipAmount = (float)_toggleTipSleeveTipAmountInput.Value;
            _project.ToggleTipSleeveColor = new System.Numerics.Vector3(
                (float)_toggleTipSleeveColorRInput.Value,
                (float)_toggleTipSleeveColorGInput.Value,
                (float)_toggleTipSleeveColorBInput.Value);
            _project.ToggleTipSleeveMetallic = (float)_toggleTipSleeveMetallicInput.Value;
            _project.ToggleTipSleeveRoughness = (float)_toggleTipSleeveRoughnessInput.Value;
            _project.ToggleTipSleevePearlescence = (float)_toggleTipSleevePearlescenceInput.Value;
            _project.ToggleTipSleeveDiffuseStrength = (float)_toggleTipSleeveDiffuseStrengthInput.Value;
            _project.ToggleTipSleeveSpecularStrength = (float)_toggleTipSleeveSpecularStrengthInput.Value;
            _project.ToggleTipSleeveRustAmount = (float)_toggleTipSleeveRustInput.Value;
            _project.ToggleTipSleeveWearAmount = (float)_toggleTipSleeveWearInput.Value;
            _project.ToggleTipSleeveGunkAmount = (float)_toggleTipSleeveGunkInput.Value;

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
