using Avalonia.Controls;
using KnobForge.App.Controls;
using System;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private const double DefaultEnvIntensity = 0.36;
        private const double DefaultEnvRoughnessMix = 1.00;
        private const double DefaultShadowStrength = 1.00;
        private const double DefaultShadowSoftness = 0.55;
        private const double DefaultShadowQuality = 0.65;

        private void InitializePrecisionControls()
        {
            WireResetButton(_envIntensityResetButton, _envIntensityInput, DefaultEnvIntensity);
            WireResetButton(_envRoughnessMixResetButton, _envRoughnessMixInput, DefaultEnvRoughnessMix);
            WireResetButton(_shadowStrengthResetButton, _shadowStrengthInput, DefaultShadowStrength);
            WireResetButton(_shadowSoftnessResetButton, _shadowSoftnessInput, DefaultShadowSoftness);
            WireResetButton(_shadowQualityResetButton, _shadowQualityInput, DefaultShadowQuality);
        }

        private static void WireResetButton(Button? button, ValueInput? input, double defaultValue)
        {
            if (button == null || input == null)
            {
                return;
            }

            button.Click += (_, _) => input.Value = Math.Clamp(defaultValue, input.Minimum, input.Maximum);
        }
    }
}
