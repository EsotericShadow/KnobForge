using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private const int HeavyGeometryDebounceMs = 70;
        private DispatcherTimer? _heavyGeometryDebounceTimer;

        private void InitializeUpdatePolicy()
        {
            WireHeavyGeometryFlushOnRelease(_modelRadiusSlider);
            WireHeavyGeometryFlushOnRelease(_modelHeightSlider);
            WireHeavyGeometryFlushOnRelease(_modelTopScaleSlider);
            WireHeavyGeometryFlushOnRelease(_modelBevelSlider);
            WireHeavyGeometryFlushOnRelease(_bevelCurveSlider);
            WireHeavyGeometryFlushOnRelease(_crownProfileSlider);
            WireHeavyGeometryFlushOnRelease(_bodyTaperSlider);
            WireHeavyGeometryFlushOnRelease(_bodyBulgeSlider);
            WireHeavyGeometryFlushOnRelease(_modelSegmentsSlider);
            WireHeavyGeometryFlushOnRelease(_sliderBackplateWidthSlider);
            WireHeavyGeometryFlushOnRelease(_sliderBackplateHeightSlider);
            WireHeavyGeometryFlushOnRelease(_sliderBackplateThicknessSlider);
            WireHeavyGeometryFlushOnRelease(_sliderThumbWidthSlider);
            WireHeavyGeometryFlushOnRelease(_sliderThumbHeightSlider);
            WireHeavyGeometryFlushOnRelease(_sliderThumbDepthSlider);
            WireHeavyGeometryFlushOnRelease(_toggleStateIndexSlider);
            WireHeavyGeometryFlushOnRelease(_toggleMaxAngleSlider);
            WireHeavyGeometryFlushOnRelease(_togglePlateWidthSlider);
            WireHeavyGeometryFlushOnRelease(_togglePlateHeightSlider);
            WireHeavyGeometryFlushOnRelease(_togglePlateThicknessSlider);
            WireHeavyGeometryFlushOnRelease(_togglePlateOffsetYSlider);
            WireHeavyGeometryFlushOnRelease(_togglePlateOffsetZSlider);
            WireHeavyGeometryFlushOnRelease(_toggleBushingRadiusSlider);
            WireHeavyGeometryFlushOnRelease(_toggleBushingHeightSlider);
            WireHeavyGeometryFlushOnRelease(_toggleBushingSidesSlider);
            WireHeavyGeometryFlushOnRelease(_toggleLowerBushingRadiusScaleSlider);
            WireHeavyGeometryFlushOnRelease(_toggleLowerBushingHeightRatioSlider);
            WireHeavyGeometryFlushOnRelease(_toggleUpperBushingRadiusScaleSlider);
            WireHeavyGeometryFlushOnRelease(_toggleUpperBushingHeightRatioSlider);
            WireHeavyGeometryFlushOnRelease(_toggleLeverLengthSlider);
            WireHeavyGeometryFlushOnRelease(_toggleLeverRadiusSlider);
            WireHeavyGeometryFlushOnRelease(_toggleLeverTopRadiusSlider);
            WireHeavyGeometryFlushOnRelease(_toggleLeverSidesSlider);
            WireHeavyGeometryFlushOnRelease(_toggleLeverPivotOffsetSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipRadiusSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipLatitudeSegmentsSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipLongitudeSegmentsSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveLengthSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveThicknessSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveOuterRadiusSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveCoverageSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveSidesSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleevePatternCountSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleevePatternDepthSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveTipAmountSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveColorRSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveColorGSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveColorBSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveMetallicSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveRoughnessSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleevePearlescenceSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveDiffuseStrengthSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveSpecularStrengthSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveRustSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveWearSlider);
            WireHeavyGeometryFlushOnRelease(_toggleTipSleeveGunkSlider);
            WireHeavyGeometryFlushOnRelease(_spiralRidgeHeightSlider);
            WireHeavyGeometryFlushOnRelease(_spiralRidgeWidthSlider);
            WireHeavyGeometryFlushOnRelease(_spiralTurnsSlider);
            WireHeavyGeometryFlushOnRelease(_gripStartSlider);
            WireHeavyGeometryFlushOnRelease(_gripHeightSlider);
            WireHeavyGeometryFlushOnRelease(_gripDensitySlider);
            WireHeavyGeometryFlushOnRelease(_gripPitchSlider);
            WireHeavyGeometryFlushOnRelease(_gripDepthSlider);
            WireHeavyGeometryFlushOnRelease(_gripWidthSlider);
            WireHeavyGeometryFlushOnRelease(_gripSharpnessSlider);
            WireHeavyGeometryFlushOnRelease(_collarScaleSlider);
            WireHeavyGeometryFlushOnRelease(_collarBodyLengthSlider);
            WireHeavyGeometryFlushOnRelease(_collarBodyThicknessSlider);
            WireHeavyGeometryFlushOnRelease(_collarHeadLengthSlider);
            WireHeavyGeometryFlushOnRelease(_collarHeadThicknessSlider);
            WireHeavyGeometryFlushOnRelease(_collarRotateSlider);
            WireHeavyGeometryFlushOnRelease(_collarOffsetXSlider);
            WireHeavyGeometryFlushOnRelease(_collarOffsetYSlider);
            WireHeavyGeometryFlushOnRelease(_collarElevationSlider);
            WireHeavyGeometryFlushOnRelease(_collarInflateSlider);
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

        private void WireHeavyGeometryFlushOnRelease(Slider? slider)
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
