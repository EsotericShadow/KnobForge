using Avalonia;
using Avalonia.Controls;
using KnobForge.App.Controls;
using KnobForge.Core;
using System;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void OnSliderAssemblySettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi ||
                _sliderAssemblyModeCombo == null ||
                _sliderBackplateMeshCombo == null ||
                _sliderThumbMeshCombo == null ||
                _sliderThumbProfileCombo == null ||
                _sliderTrackStyleCombo == null ||
                _sliderBackplateWidthInput == null ||
                _sliderBackplateHeightInput == null ||
                _sliderBackplateThicknessInput == null ||
                _sliderThumbWidthInput == null ||
                _sliderThumbHeightInput == null ||
                _sliderThumbDepthInput == null ||
                _sliderTrackWidthInput == null ||
                _sliderTrackDepthInput == null ||
                _sliderRailHeightInput == null ||
                _sliderRailSpacingInput == null ||
                _sliderThumbRidgeCountInput == null ||
                _sliderThumbRidgeDepthInput == null ||
                _sliderThumbCornerRadiusInput == null)
            {
                return;
            }

            if (ReferenceEquals(sender, _sliderAssemblyModeCombo))
            {
                if (e.Property != ComboBox.SelectedItemProperty)
                {
                    return;
                }
            }
            else if (ReferenceEquals(sender, _sliderBackplateMeshCombo) ||
                     ReferenceEquals(sender, _sliderThumbMeshCombo) ||
                     ReferenceEquals(sender, _sliderThumbProfileCombo) ||
                     ReferenceEquals(sender, _sliderTrackStyleCombo))
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

            if (GetModelNode() == null)
            {
                return;
            }

            ApplySliderAssemblyUiToProject(requestHeavyRefresh: true);
        }

        private void ApplySliderAssemblyUiToProject(bool requestHeavyRefresh)
        {
            if (_sliderAssemblyModeCombo == null ||
                _sliderBackplateMeshCombo == null ||
                _sliderThumbMeshCombo == null ||
                _sliderThumbProfileCombo == null ||
                _sliderTrackStyleCombo == null ||
                _sliderBackplateWidthInput == null ||
                _sliderBackplateHeightInput == null ||
                _sliderBackplateThicknessInput == null ||
                _sliderThumbWidthInput == null ||
                _sliderThumbHeightInput == null ||
                _sliderThumbDepthInput == null ||
                _sliderTrackWidthInput == null ||
                _sliderTrackDepthInput == null ||
                _sliderRailHeightInput == null ||
                _sliderRailSpacingInput == null ||
                _sliderThumbRidgeCountInput == null ||
                _sliderThumbRidgeDepthInput == null ||
                _sliderThumbCornerRadiusInput == null)
            {
                return;
            }

            _project.SliderMode = _sliderAssemblyModeCombo.SelectedItem is SliderAssemblyMode mode
                ? mode
                : SliderAssemblyMode.Auto;
            _project.SliderBackplateImportedMeshPath = ResolveSelectedSliderMeshPath(_sliderBackplateMeshCombo.SelectedItem);
            _project.SliderThumbImportedMeshPath = ResolveSelectedSliderMeshPath(_sliderThumbMeshCombo.SelectedItem);
            _project.SliderBackplateWidth = (float)_sliderBackplateWidthInput.Value;
            _project.SliderBackplateHeight = (float)_sliderBackplateHeightInput.Value;
            _project.SliderBackplateThickness = (float)_sliderBackplateThicknessInput.Value;
            _project.SliderThumbWidth = (float)_sliderThumbWidthInput.Value;
            _project.SliderThumbHeight = (float)_sliderThumbHeightInput.Value;
            _project.SliderThumbDepth = (float)_sliderThumbDepthInput.Value;
            _project.SliderThumbProfile = _sliderThumbProfileCombo.SelectedItem is SliderThumbProfile thumbProfile
                ? thumbProfile
                : SliderThumbProfile.Box;
            _project.SliderTrackStyle = _sliderTrackStyleCombo.SelectedItem is SliderTrackStyle trackStyle
                ? trackStyle
                : SliderTrackStyle.None;
            _project.SliderTrackWidth = (float)_sliderTrackWidthInput.Value;
            _project.SliderTrackDepth = (float)_sliderTrackDepthInput.Value;
            _project.SliderRailHeight = (float)_sliderRailHeightInput.Value;
            _project.SliderRailSpacing = (float)_sliderRailSpacingInput.Value;
            _project.SliderThumbRidgeCount = (int)Math.Round(_sliderThumbRidgeCountInput.Value);
            _project.SliderThumbRidgeDepth = (float)_sliderThumbRidgeDepthInput.Value;
            _project.SliderThumbCornerRadius = (float)_sliderThumbCornerRadiusInput.Value;

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
