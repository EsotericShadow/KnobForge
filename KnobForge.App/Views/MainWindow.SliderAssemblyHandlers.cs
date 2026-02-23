using Avalonia;
using Avalonia.Controls;
using KnobForge.Core;

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
                _sliderBackplateWidthSlider == null ||
                _sliderBackplateHeightSlider == null ||
                _sliderBackplateThicknessSlider == null ||
                _sliderThumbWidthSlider == null ||
                _sliderThumbHeightSlider == null ||
                _sliderThumbDepthSlider == null)
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
                     ReferenceEquals(sender, _sliderThumbMeshCombo))
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
                _sliderBackplateWidthSlider == null ||
                _sliderBackplateHeightSlider == null ||
                _sliderBackplateThicknessSlider == null ||
                _sliderThumbWidthSlider == null ||
                _sliderThumbHeightSlider == null ||
                _sliderThumbDepthSlider == null)
            {
                return;
            }

            _project.SliderMode = _sliderAssemblyModeCombo.SelectedItem is SliderAssemblyMode mode
                ? mode
                : SliderAssemblyMode.Auto;
            _project.SliderBackplateImportedMeshPath = ResolveSelectedSliderMeshPath(_sliderBackplateMeshCombo.SelectedItem);
            _project.SliderThumbImportedMeshPath = ResolveSelectedSliderMeshPath(_sliderThumbMeshCombo.SelectedItem);
            _project.SliderBackplateWidth = (float)_sliderBackplateWidthSlider.Value;
            _project.SliderBackplateHeight = (float)_sliderBackplateHeightSlider.Value;
            _project.SliderBackplateThickness = (float)_sliderBackplateThicknessSlider.Value;
            _project.SliderThumbWidth = (float)_sliderThumbWidthSlider.Value;
            _project.SliderThumbHeight = (float)_sliderThumbHeightSlider.Value;
            _project.SliderThumbDepth = (float)_sliderThumbDepthSlider.Value;

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
