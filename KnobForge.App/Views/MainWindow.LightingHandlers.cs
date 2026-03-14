using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KnobForge.App.Controls;
using KnobForge.Core;
using KnobForge.Core.Scene;
using SkiaSharp;
using System;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void OnLightingModeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (_lightingModeCombo == null || e.Property != ComboBox.SelectedItemProperty)
            {
                return;
            }

            if (_lightingModeCombo.SelectedItem is LightingMode mode)
            {
                _project.Mode = mode;
                NotifyProjectStateChanged();
            }
        }

        private void OnLightTypeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ComboBox.SelectedItemProperty, out var light))
            {
                return;
            }

            if (_lightTypeCombo!.SelectedItem is LightType type)
            {
                light.Type = type;
                NotifyProjectStateChanged();
            }
        }

        private void OnRotationChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (_rotationInput == null || e.Property != ValueInput.ValueProperty)
            {
                return;
            }

            var model = GetModelNode();
            if (model == null)
            {
                return;
            }

            model.RotationRadians = (float)DegreesToRadians(_rotationInput.Value);
            NotifyProjectStateChanged();
        }

        private void OnLightXChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != ValueInput.ValueProperty) return;
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ValueInput.ValueProperty, out var light) || _lightXInput == null)
            {
                return;
            }

            light.X = (float)_lightXInput.Value;
            NotifyProjectStateChanged();
        }

        private void OnLightYChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ValueInput.ValueProperty, out var light) || _lightYInput == null)
            {
                return;
            }

            light.Y = (float)_lightYInput.Value;
            NotifyProjectStateChanged();
        }

        private void OnLightZChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ValueInput.ValueProperty, out var light) || _lightZInput == null)
            {
                return;
            }

            light.Z = (float)_lightZInput.Value;
            NotifyProjectStateChanged();
        }

        private void OnDirectionChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ValueInput.ValueProperty, out var light) || _directionInput == null)
            {
                return;
            }

            light.DirectionRadians = (float)DegreesToRadians(_directionInput.Value);
            NotifyProjectStateChanged();
        }

        private void OnIntensityChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ValueInput.ValueProperty, out var light) || _intensityInput == null)
            {
                return;
            }

            light.Intensity = (float)_intensityInput.Value;
            NotifyProjectStateChanged();
        }

        private void OnFalloffChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ValueInput.ValueProperty, out var light) || _falloffInput == null)
            {
                return;
            }

            light.Falloff = (float)_falloffInput.Value;
            NotifyProjectStateChanged();
        }

        private void OnColorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ValueInput.ValueProperty, out var light) ||
                _lightRInput == null || _lightGInput == null || _lightBInput == null)
            {
                return;
            }

            light.Color = new SKColor(
                (byte)Math.Clamp((int)_lightRInput.Value, 0, 255),
                (byte)Math.Clamp((int)_lightGInput.Value, 0, 255),
                (byte)Math.Clamp((int)_lightBInput.Value, 0, 255));
            NotifyProjectStateChanged();
        }

        private void OnDiffuseBoostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ValueInput.ValueProperty, out var light) || _diffuseBoostInput == null)
            {
                return;
            }

            light.DiffuseBoost = (float)_diffuseBoostInput.Value;
            NotifyProjectStateChanged();
        }

        private void OnSpecularBoostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ValueInput.ValueProperty, out var light) || _specularBoostInput == null)
            {
                return;
            }

            light.SpecularBoost = (float)_specularBoostInput.Value;
            NotifyProjectStateChanged();
        }

        private void OnSpecularPowerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi) return;
            if (!CanMutateSelectedLight(e, ValueInput.ValueProperty, out var light) || _specularPowerInput == null)
            {
                return;
            }

            light.SpecularPower = (float)_specularPowerInput.Value;
            NotifyProjectStateChanged();
        }
        private bool CanMutateSelectedLight(AvaloniaPropertyChangedEventArgs e, AvaloniaProperty expectedProperty, out KnobLight light)
        {
            light = null!;
            if (_updatingUi || e.Property != expectedProperty)
            {
                return false;
            }

            var selected = _project.SelectedLight;
            if (selected == null)
            {
                return false;
            }

            light = selected;
            return true;
        }
        private static double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }
    }
}
