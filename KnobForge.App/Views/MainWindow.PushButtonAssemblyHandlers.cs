using Avalonia;
using Avalonia.Controls;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void OnPushButtonAssemblySettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi || _pushButtonPressAmountSlider == null)
            {
                return;
            }

            if (e.Property != Slider.ValueProperty)
            {
                return;
            }

            if (GetModelNode() == null)
            {
                return;
            }

            ApplyPushButtonAssemblyUiToProject(requestHeavyRefresh: true);
        }

        private void ApplyPushButtonAssemblyUiToProject(bool requestHeavyRefresh)
        {
            if (_pushButtonPressAmountSlider == null)
            {
                return;
            }

            _project.PushButtonPressAmountNormalized = (float)_pushButtonPressAmountSlider.Value;
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
