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
                _toggleStateCountCombo == null ||
                _toggleStateIndexSlider == null ||
                _toggleMaxAngleSlider == null ||
                _togglePlateWidthSlider == null ||
                _togglePlateHeightSlider == null ||
                _togglePlateThicknessSlider == null ||
                _toggleBushingRadiusSlider == null ||
                _toggleBushingHeightSlider == null ||
                _toggleLeverLengthSlider == null ||
                _toggleLeverRadiusSlider == null ||
                _toggleTipRadiusSlider == null)
            {
                return;
            }

            if (ReferenceEquals(sender, _toggleAssemblyModeCombo) ||
                ReferenceEquals(sender, _toggleStateCountCombo))
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

            ApplyToggleAssemblyUiToProject(requestHeavyRefresh: true);
        }

        private void ApplyToggleAssemblyUiToProject(bool requestHeavyRefresh)
        {
            if (_toggleAssemblyModeCombo == null ||
                _toggleStateCountCombo == null ||
                _toggleStateIndexSlider == null ||
                _toggleMaxAngleSlider == null ||
                _togglePlateWidthSlider == null ||
                _togglePlateHeightSlider == null ||
                _togglePlateThicknessSlider == null ||
                _toggleBushingRadiusSlider == null ||
                _toggleBushingHeightSlider == null ||
                _toggleLeverLengthSlider == null ||
                _toggleLeverRadiusSlider == null ||
                _toggleTipRadiusSlider == null)
            {
                return;
            }

            _project.ToggleMode = _toggleAssemblyModeCombo.SelectedItem is ToggleAssemblyMode mode
                ? mode
                : ToggleAssemblyMode.Auto;
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
            _project.ToggleBushingRadius = (float)_toggleBushingRadiusSlider.Value;
            _project.ToggleBushingHeight = (float)_toggleBushingHeightSlider.Value;
            _project.ToggleLeverLength = (float)_toggleLeverLengthSlider.Value;
            _project.ToggleLeverRadius = (float)_toggleLeverRadiusSlider.Value;
            _project.ToggleTipRadius = (float)_toggleTipRadiusSlider.Value;

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
