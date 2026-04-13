using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void InitializeDebugAxesInspector()
        {
            if (_metalViewport == null)
            {
                return;
            }

            _metalViewport.DebugStateChanged -= OnViewportDebugStateChanged;
            _metalViewport.DebugStateChanged += OnViewportDebugStateChanged;

            if (_debugCameraInvertXCheckBox != null)
            {
                _debugCameraInvertXCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugCameraInvertYCheckBox != null)
            {
                _debugCameraInvertYCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugCameraInvertZCheckBox != null)
            {
                _debugCameraInvertZCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugCameraFlip180CheckBox != null)
            {
                _debugCameraFlip180CheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugLightEffectInvertXCheckBox != null)
            {
                _debugLightEffectInvertXCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugLightEffectInvertYCheckBox != null)
            {
                _debugLightEffectInvertYCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugLightEffectInvertZCheckBox != null)
            {
                _debugLightEffectInvertZCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugBloomCompositeInvertXCheckBox != null)
            {
                _debugBloomCompositeInvertXCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugBloomCompositeInvertYCheckBox != null)
            {
                _debugBloomCompositeInvertYCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugGizmoInvertXCheckBox != null)
            {
                _debugGizmoInvertXCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugGizmoInvertYCheckBox != null)
            {
                _debugGizmoInvertYCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugGizmoInvertZCheckBox != null)
            {
                _debugGizmoInvertZCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugInvertKnobWindingCheckBox != null)
            {
                _debugInvertKnobWindingCheckBox.PropertyChanged += OnDebugAxisToggleChanged;
            }

            if (_debugResetAxesButton != null)
            {
                _debugResetAxesButton.Click += OnDebugResetAxesButtonClicked;
            }

            if (_debugPrintStateButton != null)
            {
                _debugPrintStateButton.Click += OnDebugPrintStateButtonClicked;
            }

            RefreshDebugAxesInspectorFromViewport();
        }

        private void OnDebugAxisToggleChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi || _metalViewport == null || e.Property != ToggleButton.IsCheckedProperty)
            {
                return;
            }

            bool isChecked = sender is ToggleButton toggle && (toggle.IsChecked ?? false);
            if (ReferenceEquals(sender, _debugCameraInvertXCheckBox))
            {
                _metalViewport.CameraInvertX = isChecked;
            }
            else if (ReferenceEquals(sender, _debugCameraInvertYCheckBox))
            {
                _metalViewport.CameraInvertY = isChecked;
            }
            else if (ReferenceEquals(sender, _debugCameraInvertZCheckBox))
            {
                _metalViewport.CameraInvertZ = isChecked;
            }
            else if (ReferenceEquals(sender, _debugCameraFlip180CheckBox))
            {
                _metalViewport.CameraFlip180 = isChecked;
            }
            else if (ReferenceEquals(sender, _debugLightEffectInvertXCheckBox))
            {
                _metalViewport.LightEffectInvertX = isChecked;
            }
            else if (ReferenceEquals(sender, _debugLightEffectInvertYCheckBox))
            {
                _metalViewport.LightEffectInvertY = isChecked;
            }
            else if (ReferenceEquals(sender, _debugLightEffectInvertZCheckBox))
            {
                _metalViewport.LightEffectInvertZ = isChecked;
            }
            else if (ReferenceEquals(sender, _debugBloomCompositeInvertXCheckBox))
            {
                _metalViewport.BloomCompositeInvertX = isChecked;
            }
            else if (ReferenceEquals(sender, _debugBloomCompositeInvertYCheckBox))
            {
                _metalViewport.BloomCompositeInvertY = isChecked;
            }
            else if (ReferenceEquals(sender, _debugGizmoInvertXCheckBox))
            {
                _metalViewport.GizmoInvertX = isChecked;
            }
            else if (ReferenceEquals(sender, _debugGizmoInvertYCheckBox))
            {
                _metalViewport.GizmoInvertY = isChecked;
            }
            else if (ReferenceEquals(sender, _debugGizmoInvertZCheckBox))
            {
                _metalViewport.GizmoInvertZ = isChecked;
            }
            else if (ReferenceEquals(sender, _debugInvertKnobWindingCheckBox))
            {
                _metalViewport.InvertKnobWinding = isChecked;
            }
        }

        private void OnDebugResetAxesButtonClicked(object? sender, RoutedEventArgs e)
        {
            _metalViewport?.ResetDebugAxes();
            RefreshDebugAxesInspectorFromViewport();
        }

        private void OnDebugPrintStateButtonClicked(object? sender, RoutedEventArgs e)
        {
            _metalViewport?.PrintDebugState();
        }

        private void OnViewportDebugStateChanged()
        {
            Dispatcher.UIThread.Post(RefreshDebugAxesInspectorFromViewport, DispatcherPriority.Normal);
        }

        private void RefreshDebugAxesInspectorFromViewport()
        {
            if (_metalViewport == null)
            {
                return;
            }

            WithUiRefreshSuppressed(() =>
            {
                if (_debugCameraInvertXCheckBox != null)
                {
                    _debugCameraInvertXCheckBox.IsChecked = _metalViewport.CameraInvertX;
                }

                if (_debugCameraInvertYCheckBox != null)
                {
                    _debugCameraInvertYCheckBox.IsChecked = _metalViewport.CameraInvertY;
                }

                if (_debugCameraInvertZCheckBox != null)
                {
                    _debugCameraInvertZCheckBox.IsChecked = _metalViewport.CameraInvertZ;
                }

                if (_debugCameraFlip180CheckBox != null)
                {
                    _debugCameraFlip180CheckBox.IsChecked = _metalViewport.CameraFlip180;
                }

                if (_debugLightEffectInvertXCheckBox != null)
                {
                    _debugLightEffectInvertXCheckBox.IsChecked = _metalViewport.LightEffectInvertX;
                }

                if (_debugLightEffectInvertYCheckBox != null)
                {
                    _debugLightEffectInvertYCheckBox.IsChecked = _metalViewport.LightEffectInvertY;
                }

                if (_debugLightEffectInvertZCheckBox != null)
                {
                    _debugLightEffectInvertZCheckBox.IsChecked = _metalViewport.LightEffectInvertZ;
                }

                if (_debugBloomCompositeInvertXCheckBox != null)
                {
                    _debugBloomCompositeInvertXCheckBox.IsChecked = _metalViewport.BloomCompositeInvertX;
                }

                if (_debugBloomCompositeInvertYCheckBox != null)
                {
                    _debugBloomCompositeInvertYCheckBox.IsChecked = _metalViewport.BloomCompositeInvertY;
                }

                if (_debugGizmoInvertXCheckBox != null)
                {
                    _debugGizmoInvertXCheckBox.IsChecked = _metalViewport.GizmoInvertX;
                }

                if (_debugGizmoInvertYCheckBox != null)
                {
                    _debugGizmoInvertYCheckBox.IsChecked = _metalViewport.GizmoInvertY;
                }

                if (_debugGizmoInvertZCheckBox != null)
                {
                    _debugGizmoInvertZCheckBox.IsChecked = _metalViewport.GizmoInvertZ;
                }

                if (_debugInvertKnobWindingCheckBox != null)
                {
                    _debugInvertKnobWindingCheckBox.IsChecked = _metalViewport.InvertKnobWinding;
                }
            });
        }
    }
}
