using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using KnobForge.App.Controls;
using System;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private const int HeavyGeometryDebounceMs = 70;
        private DispatcherTimer? _heavyGeometryDebounceTimer;

        private void InitializeUpdatePolicy()
        {
            WireHeavyGeometryFlushOnRelease(_modelRadiusInput);
            WireHeavyGeometryFlushOnRelease(_modelHeightInput);
            WireHeavyGeometryFlushOnRelease(_modelTopScaleInput);
            WireHeavyGeometryFlushOnRelease(_modelBevelInput);
            WireHeavyGeometryFlushOnRelease(_bevelCurveInput);
            WireHeavyGeometryFlushOnRelease(_crownProfileInput);
            WireHeavyGeometryFlushOnRelease(_bodyTaperInput);
            WireHeavyGeometryFlushOnRelease(_bodyBulgeInput);
            WireHeavyGeometryFlushOnRelease(_modelSegmentsInput);
            WireHeavyGeometryFlushOnRelease(_sliderBackplateWidthInput);
            WireHeavyGeometryFlushOnRelease(_sliderBackplateHeightInput);
            WireHeavyGeometryFlushOnRelease(_sliderBackplateThicknessInput);
            WireHeavyGeometryFlushOnRelease(_sliderThumbWidthInput);
            WireHeavyGeometryFlushOnRelease(_sliderThumbHeightInput);
            WireHeavyGeometryFlushOnRelease(_sliderThumbDepthInput);
            WireHeavyGeometryFlushOnRelease(_toggleStateIndexInput);
            WireHeavyGeometryFlushOnRelease(_toggleMaxAngleInput);
            WireHeavyGeometryFlushOnRelease(_togglePlateWidthInput);
            WireHeavyGeometryFlushOnRelease(_togglePlateHeightInput);
            WireHeavyGeometryFlushOnRelease(_togglePlateThicknessInput);
            WireHeavyGeometryFlushOnRelease(_togglePlateOffsetYInput);
            WireHeavyGeometryFlushOnRelease(_togglePlateOffsetZInput);
            WireHeavyGeometryFlushOnRelease(_toggleBushingRadiusInput);
            WireHeavyGeometryFlushOnRelease(_toggleBushingHeightInput);
            WireHeavyGeometryFlushOnRelease(_toggleBushingSidesInput);
            WireHeavyGeometryFlushOnRelease(_toggleLowerBushingRadiusScaleInput);
            WireHeavyGeometryFlushOnRelease(_toggleLowerBushingHeightRatioInput);
            WireHeavyGeometryFlushOnRelease(_toggleUpperBushingRadiusScaleInput);
            WireHeavyGeometryFlushOnRelease(_toggleUpperBushingHeightRatioInput);
            WireHeavyGeometryFlushOnRelease(_toggleUpperBushingKnurlAmountInput);
            WireHeavyGeometryFlushOnRelease(_toggleUpperBushingKnurlDensityInput);
            WireHeavyGeometryFlushOnRelease(_toggleUpperBushingKnurlDepthInput);
            WireHeavyGeometryFlushOnRelease(_togglePivotHousingRadiusInput);
            WireHeavyGeometryFlushOnRelease(_togglePivotHousingDepthInput);
            WireHeavyGeometryFlushOnRelease(_togglePivotHousingBevelInput);
            WireHeavyGeometryFlushOnRelease(_togglePivotBallRadiusInput);
            WireHeavyGeometryFlushOnRelease(_togglePivotClearanceInput);
            WireHeavyGeometryFlushOnRelease(_toggleLeverLengthInput);
            WireHeavyGeometryFlushOnRelease(_toggleLeverRadiusInput);
            WireHeavyGeometryFlushOnRelease(_toggleLeverTopRadiusInput);
            WireHeavyGeometryFlushOnRelease(_toggleLeverSidesInput);
            WireHeavyGeometryFlushOnRelease(_toggleLeverPivotOffsetInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipRadiusInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipLatitudeSegmentsInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipLongitudeSegmentsInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveLengthInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveThicknessInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveOuterRadiusInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveCoverageInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveSidesInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleevePatternCountInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleevePatternDepthInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveTipAmountInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveColorRInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveColorGInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveColorBInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveMetallicInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveRoughnessInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleevePearlescenceInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveDiffuseStrengthInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveSpecularStrengthInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveRustInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveWearInput);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveGunkInput);
            WireHeavyGeometryFlushOnRelease(_spiralRidgeHeightInput);
            WireHeavyGeometryFlushOnRelease(_spiralRidgeWidthInput);
            WireHeavyGeometryFlushOnRelease(_spiralTurnsInput);
            WireHeavyGeometryFlushOnRelease(_gripStartInput);
            WireHeavyGeometryFlushOnRelease(_gripHeightInput);
            WireHeavyGeometryFlushOnRelease(_gripDensityInput);
            WireHeavyGeometryFlushOnRelease(_gripPitchInput);
            WireHeavyGeometryFlushOnRelease(_gripDepthInput);
            WireHeavyGeometryFlushOnRelease(_gripWidthInput);
            WireHeavyGeometryFlushOnRelease(_gripSharpnessInput);
            WireHeavyGeometryFlushOnRelease(_collarScaleInput);
            WireHeavyGeometryFlushOnRelease(_collarBodyLengthInput);
            WireHeavyGeometryFlushOnRelease(_collarBodyThicknessInput);
            WireHeavyGeometryFlushOnRelease(_collarHeadLengthInput);
            WireHeavyGeometryFlushOnRelease(_collarHeadThicknessInput);
            WireHeavyGeometryFlushOnRelease(_collarRotateInput);
            WireHeavyGeometryFlushOnRelease(_collarOffsetXInput);
            WireHeavyGeometryFlushOnRelease(_collarOffsetYInput);
            WireHeavyGeometryFlushOnRelease(_collarElevationInput);
            WireHeavyGeometryFlushOnRelease(_collarInflateInput);
            WireHeavyGeometryFlushOnRelease(_indicatorBaseWidthInput);
            WireHeavyGeometryFlushOnRelease(_indicatorBaseHeightInput);
            WireHeavyGeometryFlushOnRelease(_indicatorBaseThicknessInput);
            WireHeavyGeometryFlushOnRelease(_indicatorHousingRadiusInput);
            WireHeavyGeometryFlushOnRelease(_indicatorHousingHeightInput);
            WireHeavyGeometryFlushOnRelease(_indicatorLensRadiusInput);
            WireHeavyGeometryFlushOnRelease(_indicatorLensHeightInput);
            WireHeavyGeometryFlushOnRelease(_indicatorReflectorBaseRadiusInput);
            WireHeavyGeometryFlushOnRelease(_indicatorReflectorTopRadiusInput);
            WireHeavyGeometryFlushOnRelease(_indicatorReflectorDepthInput);
            WireHeavyGeometryFlushOnRelease(_indicatorEmitterRadiusInput);
            WireHeavyGeometryFlushOnRelease(_indicatorEmitterSpreadInput);
            WireHeavyGeometryFlushOnRelease(_indicatorEmitterDepthInput);
            WireHeavyGeometryFlushOnRelease(_indicatorEmitterCountInput);
            WireHeavyGeometryFlushOnRelease(_indicatorRadialSegmentsInput);
            WireHeavyGeometryFlushOnRelease(_indicatorLensLatitudeSegmentsInput);
            WireHeavyGeometryFlushOnRelease(_indicatorLensLongitudeSegmentsInput);
        }

        private void RequestHeavyGeometryRefresh()
        {
            _heavyGeometryDebounceTimer ??= new DispatcherTimer(
                TimeSpan.FromMilliseconds(HeavyGeometryDebounceMs),
                DispatcherPriority.Background,
                (_, _) =>
                {
                    _heavyGeometryDebounceTimer?.Stop();
                    NotifyProjectStateChanged();
                });

            _heavyGeometryDebounceTimer.Stop();
            _heavyGeometryDebounceTimer.Start();
        }

        private void FlushHeavyGeometryRefresh()
        {
            if (_heavyGeometryDebounceTimer == null || !_heavyGeometryDebounceTimer.IsEnabled)
            {
                return;
            }

            _heavyGeometryDebounceTimer.Stop();
            NotifyProjectStateChanged();
        }

        private void WireHeavyGeometryFlushOnRelease(ValueInput? slider)
        {
            if (slider == null)
            {
                return;
            }

            slider.PointerReleased += OnHeavyGeometrySliderReleased;
            slider.AddHandler(InputElement.PointerCaptureLostEvent, OnHeavyGeometrySliderLostCapture, RoutingStrategies.Tunnel);
        }

        private void OnHeavyGeometrySliderReleased(object? sender, PointerReleasedEventArgs e)
        {
            FlushHeavyGeometryRefresh();
        }

        private void OnHeavyGeometrySliderLostCapture(object? sender, PointerCaptureLostEventArgs e)
        {
            FlushHeavyGeometryRefresh();
        }
    }
}
