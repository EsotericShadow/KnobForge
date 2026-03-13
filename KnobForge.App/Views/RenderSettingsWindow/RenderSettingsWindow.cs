using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using KnobForge.App.Controls;
using KnobForge.Core;
using KnobForge.Core.Export;
using KnobForge.Core.Scene;
using KnobForge.Rendering;
using KnobForge.Rendering.GPU;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KnobForge.App.Views
{
    public partial class RenderSettingsWindow : Window
    {
        private const int MinFrameCount = 1;
        private const int MaxFrameCount = 1440;
        private const int MinFlipSwitchFrameCount = 12;
        private const int MaxFlipSwitchFrameCount = 24;
        private const int DefaultFlipSwitchFrameCount = 18;
        private const int DefaultIndicatorLightFrameCount = 24;
        private const int MinResolution = 1;
        private const int MaxResolution = 16384;
        private const int MinSupersample = 1;
        private const int MaxSupersample = 4;
        private const float MinOrbitOffsetDeg = 0f;
        private const float MaxOrbitYawOffsetDeg = 180f;
        private const float MaxOrbitPitchOffsetDeg = 85f;

        private readonly KnobProject _project;
        private readonly OrientationDebug _orientation;
        private readonly ViewportCameraState _cameraState;
        private readonly MetalViewport? _gpuViewport;
        private readonly Control _settingsPanel;
        private readonly ComboBox _outputStrategyComboBox;
        private readonly TextBlock _outputStrategySummaryTextBlock;
        private readonly TextBlock _outputStrategyDescriptionTextBlock;
        private readonly TextBox _frameCountTextBox;
        private readonly TextBox _resolutionTextBox;
        private readonly ComboBox _supersampleComboBox;
        private readonly TextBox _paddingTextBox;
        private readonly TextBox _cameraDistanceScaleTextBox;
        private readonly CheckBox _exportOrbitVariantsCheckBox;
        private readonly TextBox _orbitYawOffsetTextBox;
        private readonly TextBox _orbitPitchOffsetTextBox;
        private readonly ComboBox _filterPresetComboBox;
        private readonly TextBox _baseNameTextBox;
        private readonly TextBox _outputFolderTextBox;
        private readonly Button _browseOutputButton;
        private readonly ComboBox _spritesheetLayoutComboBox;
        private readonly CheckBox _exportFramesCheckBox;
        private readonly CheckBox _exportSpritesheetCheckBox;
        private readonly CheckBox _optimizeSpritesheetPngCheckBox;
        private readonly ComboBox _outputImageFormatComboBox;
        private readonly TextBox _pngCompressionLevelTextBox;
        private readonly ComboBox _pngOptimizationPresetComboBox;
        private readonly TextBox _pngMinimumSavingsKbTextBox;
        private readonly TextBox _pngOpaqueRgbStepTextBox;
        private readonly TextBox _pngOpaqueAlphaStepTextBox;
        private readonly TextBox _pngTranslucentRgbStepTextBox;
        private readonly TextBox _pngTranslucentAlphaStepTextBox;
        private readonly TextBox _pngTranslucentAlphaThresholdTextBox;
        private readonly TextBox _pngMaxOpaqueRgbDeltaTextBox;
        private readonly TextBox _pngMaxVisibleRgbDeltaTextBox;
        private readonly TextBox _pngMaxVisibleAlphaDeltaTextBox;
        private readonly TextBox _pngMeanVisibleLumaDeltaTextBox;
        private readonly TextBox _pngMeanVisibleAlphaDeltaTextBox;
        private readonly Control _webpLossyQualityRow;
        private readonly TextBox _webpLossyQualityTextBox;
        private readonly Button _autoCorrectButton;
        private readonly Border _rotaryPreviewSection;
        private readonly TextBlock _interactivePreviewTitleTextBlock;
        private readonly TextBlock _compressionEstimateTextBlock;
        private readonly ListBox _viewpointsListBox;
        private readonly Button _addViewpointButton;
        private readonly Button _duplicateViewpointButton;
        private readonly Button _resetViewpointsFromOrbitButton;
        private readonly Button _removeViewpointButton;
        private readonly Button _moveViewpointUpButton;
        private readonly Button _moveViewpointDownButton;
        private readonly TextBox _previewBaseYawTextBox;
        private readonly TextBox _previewBasePitchTextBox;
        private readonly CheckBox _viewpointEnabledCheckBox;
        private readonly TextBox _viewpointNameTextBox;
        private readonly TextBox _viewpointFileTagTextBox;
        private readonly CheckBox _viewpointAbsoluteCameraCheckBox;
        private readonly TextBox _viewpointYawTextBox;
        private readonly TextBox _viewpointPitchTextBox;
        private readonly CheckBox _viewpointOverrideZoomCheckBox;
        private readonly TextBox _viewpointZoomTextBox;
        private readonly CheckBox _viewpointOverridePanCheckBox;
        private readonly TextBox _viewpointPanXTextBox;
        private readonly TextBox _viewpointPanYTextBox;
        private readonly ComboBox _rotaryPreviewVariantComboBox;
        private readonly Button _createRotaryPreviewButton;
        private readonly SpriteKnobSlider _rotaryPreviewKnob;
        private readonly TextBlock _rotaryPreviewInfoTextBlock;
        private readonly TextBlock _rotaryPreviewValueTextBlock;
        private readonly Button _startRenderButton;
        private readonly Button _cancelButton;
        private readonly ProgressBar _exportProgressBar;
        private readonly TextBlock _exportSummaryTextBlock;
        private readonly TextBlock _statusTextBlock;
        private readonly TextBlock _scratchParityNoteTextBlock;
        private readonly OutputStrategyOption[] _outputStrategyOptions;
        private PreviewVariantOption[] _previewVariantOptions;
        private readonly List<ViewpointEditorItem> _viewpointEditorItems = new();

        private CancellationTokenSource? _exportCts;
        private CancellationTokenSource? _rotaryPreviewCts;
        private string? _rotaryPreviewTempPath;
        private bool _isBuildingRotaryPreview;
        private bool _isRendering;
        private bool _isApplyingOutputStrategy;
        private bool _isApplyingPngOptimizationPreset;
        private bool _isUpdatingViewpointUi;
        private bool _viewpointsDirtyFromOrbit = true;
        private long? _lastPreviewEncodedSheetBytes;
        private bool CanUseGpuExport => _gpuViewport?.CanRenderOffscreen == true;

        public RenderSettingsWindow()
            : this(
                new KnobProject(),
                new OrientationDebug(),
                new ViewportCameraState(30f, -20f, 1f, SKPoint.Empty),
                null)
        {
        }

        public RenderSettingsWindow(KnobProject project, OrientationDebug orientation, ViewportCameraState cameraState, MetalViewport? gpuViewport)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _orientation = orientation ?? throw new ArgumentNullException(nameof(orientation));
            _cameraState = cameraState;
            _gpuViewport = gpuViewport;

            InitializeComponent();

            _settingsPanel = this.FindControl<Control>("SettingsPanel")
                ?? throw new InvalidOperationException("SettingsPanel not found.");
            _outputStrategyComboBox = this.FindControl<ComboBox>("OutputStrategyComboBox")
                ?? throw new InvalidOperationException("OutputStrategyComboBox not found.");
            _outputStrategySummaryTextBlock = this.FindControl<TextBlock>("OutputStrategySummaryTextBlock")
                ?? throw new InvalidOperationException("OutputStrategySummaryTextBlock not found.");
            _outputStrategyDescriptionTextBlock = this.FindControl<TextBlock>("OutputStrategyDescriptionTextBlock")
                ?? throw new InvalidOperationException("OutputStrategyDescriptionTextBlock not found.");
            _frameCountTextBox = this.FindControl<TextBox>("FrameCountTextBox")
                ?? throw new InvalidOperationException("FrameCountTextBox not found.");
            _resolutionTextBox = this.FindControl<TextBox>("ResolutionTextBox")
                ?? throw new InvalidOperationException("ResolutionTextBox not found.");
            _supersampleComboBox = this.FindControl<ComboBox>("SupersampleComboBox")
                ?? throw new InvalidOperationException("SupersampleComboBox not found.");
            _paddingTextBox = this.FindControl<TextBox>("PaddingTextBox")
                ?? throw new InvalidOperationException("PaddingTextBox not found.");
            _cameraDistanceScaleTextBox = this.FindControl<TextBox>("CameraDistanceScaleTextBox")
                ?? throw new InvalidOperationException("CameraDistanceScaleTextBox not found.");
            _exportOrbitVariantsCheckBox = this.FindControl<CheckBox>("ExportOrbitVariantsCheckBox")
                ?? throw new InvalidOperationException("ExportOrbitVariantsCheckBox not found.");
            _orbitYawOffsetTextBox = this.FindControl<TextBox>("OrbitYawOffsetTextBox")
                ?? throw new InvalidOperationException("OrbitYawOffsetTextBox not found.");
            _orbitPitchOffsetTextBox = this.FindControl<TextBox>("OrbitPitchOffsetTextBox")
                ?? throw new InvalidOperationException("OrbitPitchOffsetTextBox not found.");
            _filterPresetComboBox = this.FindControl<ComboBox>("FilterPresetComboBox")
                ?? throw new InvalidOperationException("FilterPresetComboBox not found.");
            _baseNameTextBox = this.FindControl<TextBox>("BaseNameTextBox")
                ?? throw new InvalidOperationException("BaseNameTextBox not found.");
            _outputFolderTextBox = this.FindControl<TextBox>("OutputFolderTextBox")
                ?? throw new InvalidOperationException("OutputFolderTextBox not found.");
            _browseOutputButton = this.FindControl<Button>("BrowseOutputButton")
                ?? throw new InvalidOperationException("BrowseOutputButton not found.");
            _spritesheetLayoutComboBox = this.FindControl<ComboBox>("SpritesheetLayoutComboBox")
                ?? throw new InvalidOperationException("SpritesheetLayoutComboBox not found.");
            _exportFramesCheckBox = this.FindControl<CheckBox>("ExportFramesCheckBox")
                ?? throw new InvalidOperationException("ExportFramesCheckBox not found.");
            _exportSpritesheetCheckBox = this.FindControl<CheckBox>("ExportSpritesheetCheckBox")
                ?? throw new InvalidOperationException("ExportSpritesheetCheckBox not found.");
            _optimizeSpritesheetPngCheckBox = this.FindControl<CheckBox>("OptimizeSpritesheetPngCheckBox")
                ?? throw new InvalidOperationException("OptimizeSpritesheetPngCheckBox not found.");
            _outputImageFormatComboBox = this.FindControl<ComboBox>("OutputImageFormatComboBox")
                ?? throw new InvalidOperationException("OutputImageFormatComboBox not found.");
            _pngCompressionLevelTextBox = this.FindControl<TextBox>("PngCompressionLevelTextBox")
                ?? throw new InvalidOperationException("PngCompressionLevelTextBox not found.");
            _pngOptimizationPresetComboBox = this.FindControl<ComboBox>("PngOptimizationPresetComboBox")
                ?? throw new InvalidOperationException("PngOptimizationPresetComboBox not found.");
            _pngMinimumSavingsKbTextBox = this.FindControl<TextBox>("PngMinimumSavingsKbTextBox")
                ?? throw new InvalidOperationException("PngMinimumSavingsKbTextBox not found.");
            _pngOpaqueRgbStepTextBox = this.FindControl<TextBox>("PngOpaqueRgbStepTextBox")
                ?? throw new InvalidOperationException("PngOpaqueRgbStepTextBox not found.");
            _pngOpaqueAlphaStepTextBox = this.FindControl<TextBox>("PngOpaqueAlphaStepTextBox")
                ?? throw new InvalidOperationException("PngOpaqueAlphaStepTextBox not found.");
            _pngTranslucentRgbStepTextBox = this.FindControl<TextBox>("PngTranslucentRgbStepTextBox")
                ?? throw new InvalidOperationException("PngTranslucentRgbStepTextBox not found.");
            _pngTranslucentAlphaStepTextBox = this.FindControl<TextBox>("PngTranslucentAlphaStepTextBox")
                ?? throw new InvalidOperationException("PngTranslucentAlphaStepTextBox not found.");
            _pngTranslucentAlphaThresholdTextBox = this.FindControl<TextBox>("PngTranslucentAlphaThresholdTextBox")
                ?? throw new InvalidOperationException("PngTranslucentAlphaThresholdTextBox not found.");
            _pngMaxOpaqueRgbDeltaTextBox = this.FindControl<TextBox>("PngMaxOpaqueRgbDeltaTextBox")
                ?? throw new InvalidOperationException("PngMaxOpaqueRgbDeltaTextBox not found.");
            _pngMaxVisibleRgbDeltaTextBox = this.FindControl<TextBox>("PngMaxVisibleRgbDeltaTextBox")
                ?? throw new InvalidOperationException("PngMaxVisibleRgbDeltaTextBox not found.");
            _pngMaxVisibleAlphaDeltaTextBox = this.FindControl<TextBox>("PngMaxVisibleAlphaDeltaTextBox")
                ?? throw new InvalidOperationException("PngMaxVisibleAlphaDeltaTextBox not found.");
            _pngMeanVisibleLumaDeltaTextBox = this.FindControl<TextBox>("PngMeanVisibleLumaDeltaTextBox")
                ?? throw new InvalidOperationException("PngMeanVisibleLumaDeltaTextBox not found.");
            _pngMeanVisibleAlphaDeltaTextBox = this.FindControl<TextBox>("PngMeanVisibleAlphaDeltaTextBox")
                ?? throw new InvalidOperationException("PngMeanVisibleAlphaDeltaTextBox not found.");
            _webpLossyQualityRow = this.FindControl<Control>("WebpLossyQualityRow")
                ?? throw new InvalidOperationException("WebpLossyQualityRow not found.");
            _webpLossyQualityTextBox = this.FindControl<TextBox>("WebpLossyQualityTextBox")
                ?? throw new InvalidOperationException("WebpLossyQualityTextBox not found.");
            _autoCorrectButton = this.FindControl<Button>("AutoCorrectButton")
                ?? throw new InvalidOperationException("AutoCorrectButton not found.");
            _rotaryPreviewSection = this.FindControl<Border>("RotaryPreviewSection")
                ?? throw new InvalidOperationException("RotaryPreviewSection not found.");
            _interactivePreviewTitleTextBlock = this.FindControl<TextBlock>("InteractivePreviewTitleTextBlock")
                ?? throw new InvalidOperationException("InteractivePreviewTitleTextBlock not found.");
            _compressionEstimateTextBlock = this.FindControl<TextBlock>("CompressionEstimateTextBlock")
                ?? throw new InvalidOperationException("CompressionEstimateTextBlock not found.");
            _viewpointsListBox = this.FindControl<ListBox>("ViewpointsListBox")
                ?? throw new InvalidOperationException("ViewpointsListBox not found.");
            _addViewpointButton = this.FindControl<Button>("AddViewpointButton")
                ?? throw new InvalidOperationException("AddViewpointButton not found.");
            _duplicateViewpointButton = this.FindControl<Button>("DuplicateViewpointButton")
                ?? throw new InvalidOperationException("DuplicateViewpointButton not found.");
            _resetViewpointsFromOrbitButton = this.FindControl<Button>("ResetViewpointsFromOrbitButton")
                ?? throw new InvalidOperationException("ResetViewpointsFromOrbitButton not found.");
            _removeViewpointButton = this.FindControl<Button>("RemoveViewpointButton")
                ?? throw new InvalidOperationException("RemoveViewpointButton not found.");
            _moveViewpointUpButton = this.FindControl<Button>("MoveViewpointUpButton")
                ?? throw new InvalidOperationException("MoveViewpointUpButton not found.");
            _moveViewpointDownButton = this.FindControl<Button>("MoveViewpointDownButton")
                ?? throw new InvalidOperationException("MoveViewpointDownButton not found.");
            _previewBaseYawTextBox = this.FindControl<TextBox>("PreviewBaseYawTextBox")
                ?? throw new InvalidOperationException("PreviewBaseYawTextBox not found.");
            _previewBasePitchTextBox = this.FindControl<TextBox>("PreviewBasePitchTextBox")
                ?? throw new InvalidOperationException("PreviewBasePitchTextBox not found.");
            _viewpointEnabledCheckBox = this.FindControl<CheckBox>("ViewpointEnabledCheckBox")
                ?? throw new InvalidOperationException("ViewpointEnabledCheckBox not found.");
            _viewpointNameTextBox = this.FindControl<TextBox>("ViewpointNameTextBox")
                ?? throw new InvalidOperationException("ViewpointNameTextBox not found.");
            _viewpointFileTagTextBox = this.FindControl<TextBox>("ViewpointFileTagTextBox")
                ?? throw new InvalidOperationException("ViewpointFileTagTextBox not found.");
            _viewpointAbsoluteCameraCheckBox = this.FindControl<CheckBox>("ViewpointAbsoluteCameraCheckBox")
                ?? throw new InvalidOperationException("ViewpointAbsoluteCameraCheckBox not found.");
            _viewpointYawTextBox = this.FindControl<TextBox>("ViewpointYawTextBox")
                ?? throw new InvalidOperationException("ViewpointYawTextBox not found.");
            _viewpointPitchTextBox = this.FindControl<TextBox>("ViewpointPitchTextBox")
                ?? throw new InvalidOperationException("ViewpointPitchTextBox not found.");
            _viewpointOverrideZoomCheckBox = this.FindControl<CheckBox>("ViewpointOverrideZoomCheckBox")
                ?? throw new InvalidOperationException("ViewpointOverrideZoomCheckBox not found.");
            _viewpointZoomTextBox = this.FindControl<TextBox>("ViewpointZoomTextBox")
                ?? throw new InvalidOperationException("ViewpointZoomTextBox not found.");
            _viewpointOverridePanCheckBox = this.FindControl<CheckBox>("ViewpointOverridePanCheckBox")
                ?? throw new InvalidOperationException("ViewpointOverridePanCheckBox not found.");
            _viewpointPanXTextBox = this.FindControl<TextBox>("ViewpointPanXTextBox")
                ?? throw new InvalidOperationException("ViewpointPanXTextBox not found.");
            _viewpointPanYTextBox = this.FindControl<TextBox>("ViewpointPanYTextBox")
                ?? throw new InvalidOperationException("ViewpointPanYTextBox not found.");
            _rotaryPreviewVariantComboBox = this.FindControl<ComboBox>("RotaryPreviewVariantComboBox")
                ?? throw new InvalidOperationException("RotaryPreviewVariantComboBox not found.");
            _createRotaryPreviewButton = this.FindControl<Button>("CreateRotaryPreviewButton")
                ?? throw new InvalidOperationException("CreateRotaryPreviewButton not found.");
            _rotaryPreviewKnob = this.FindControl<SpriteKnobSlider>("RotaryPreviewKnob")
                ?? throw new InvalidOperationException("RotaryPreviewKnob not found.");
            _rotaryPreviewInfoTextBlock = this.FindControl<TextBlock>("RotaryPreviewInfoTextBlock")
                ?? throw new InvalidOperationException("RotaryPreviewInfoTextBlock not found.");
            _rotaryPreviewValueTextBlock = this.FindControl<TextBlock>("RotaryPreviewValueTextBlock")
                ?? throw new InvalidOperationException("RotaryPreviewValueTextBlock not found.");
            _startRenderButton = this.FindControl<Button>("StartRenderButton")
                ?? throw new InvalidOperationException("StartRenderButton not found.");
            _cancelButton = this.FindControl<Button>("CancelButton")
                ?? throw new InvalidOperationException("CancelButton not found.");
            _exportProgressBar = this.FindControl<ProgressBar>("ExportProgressBar")
                ?? throw new InvalidOperationException("ExportProgressBar not found.");
            _exportSummaryTextBlock = this.FindControl<TextBlock>("ExportSummaryTextBlock")
                ?? throw new InvalidOperationException("ExportSummaryTextBlock not found.");
            _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock")
                ?? throw new InvalidOperationException("StatusTextBlock not found.");
            _scratchParityNoteTextBlock = this.FindControl<TextBlock>("ScratchParityNoteTextBlock")
                ?? throw new InvalidOperationException("ScratchParityNoteTextBlock not found.");

            _outputStrategyOptions = BuildOutputStrategyOptions();
            _outputStrategyComboBox.ItemsSource = _outputStrategyOptions;
            _previewVariantOptions = BuildPreviewVariantOptions();
            _rotaryPreviewVariantComboBox.ItemsSource = _previewVariantOptions;
            _rotaryPreviewVariantComboBox.SelectedIndex = 0;

            _supersampleComboBox.ItemsSource = new[] { "1", "2", "3", "4" };

            _spritesheetLayoutComboBox.ItemsSource = Enum.GetValues<SpritesheetLayout>();

            _filterPresetComboBox.ItemsSource = Enum.GetValues<ExportFilterPreset>();
            _outputImageFormatComboBox.ItemsSource = BuildFrameImageFormatOptions();
            _pngOptimizationPresetComboBox.ItemsSource = Enum.GetValues<PngOptimizationPreset>();

            _outputFolderTextBox.Text = GetDefaultOutputFolder();
            var defaultExportSettings = new KnobExportSettings();
            _exportOrbitVariantsCheckBox.IsChecked = defaultExportSettings.ExportOrbitVariants;
            _orbitYawOffsetTextBox.Text = defaultExportSettings.OrbitVariantYawOffsetDeg.ToString("0.###", CultureInfo.InvariantCulture);
            _orbitPitchOffsetTextBox.Text = defaultExportSettings.OrbitVariantPitchOffsetDeg.ToString("0.###", CultureInfo.InvariantCulture);
            _outputImageFormatComboBox.SelectedItem = defaultExportSettings.ImageFormat;
            _pngCompressionLevelTextBox.Text = defaultExportSettings.PngCompressionLevel.ToString(CultureInfo.InvariantCulture);
            _pngOptimizationPresetComboBox.SelectedItem = defaultExportSettings.PngOptimizationPreset;
            _webpLossyQualityTextBox.Text = defaultExportSettings.WebpLossyQuality.ToString("0.#", CultureInfo.InvariantCulture);
            _optimizeSpritesheetPngCheckBox.IsChecked = defaultExportSettings.OptimizeSpritesheetPng;
            ApplyPngOptimizationPreset(defaultExportSettings.PngOptimizationPreset);
            _previewBaseYawTextBox.Text = _cameraState.OrbitYawDeg.ToString("0.###", CultureInfo.InvariantCulture);
            _previewBasePitchTextBox.Text = _cameraState.OrbitPitchDeg.ToString("0.###", CultureInfo.InvariantCulture);
            _exportProgressBar.Value = 0d;
            _scratchParityNoteTextBlock.Text = "Render parity: export preview and final export both use the GPU viewport path. Expect a close match; tiny edge differences can still appear on ultra-thin strokes or fallback hits.";
            _exportSummaryTextBlock.Text = "Summary unavailable until export settings are valid.";
            _compressionEstimateTextBlock.Text = "Compression estimate unavailable until you create a preview.";
            _statusTextBlock.Text = CanUseGpuExport
                ? "Ready to export."
                : "GPU offscreen rendering is unavailable. Export is GPU-only.";

            _outputStrategyComboBox.SelectionChanged += OnOutputStrategySelectionChanged;
            _browseOutputButton.Click += OnBrowseOutputButtonClick;
            _autoCorrectButton.Click += OnAutoCorrectButtonClick;
            WireViewpointEditorHandlers();
            _createRotaryPreviewButton.Click += OnCreateRotaryPreviewButtonClick;
            _rotaryPreviewVariantComboBox.SelectionChanged += OnRotaryPreviewVariantSelectionChanged;
            _rotaryPreviewKnob.PropertyChanged += OnRotaryPreviewKnobPropertyChanged;
            _pngOptimizationPresetComboBox.SelectionChanged += OnPngOptimizationPresetSelectionChanged;
            _startRenderButton.Click += OnStartRenderButtonClick;
            _cancelButton.Click += OnCancelButtonClick;
            _exportSpritesheetCheckBox.IsCheckedChanged += OnExportSpritesheetCheckedChanged;
            _exportOrbitVariantsCheckBox.IsCheckedChanged += OnExportOrbitVariantsCheckedChanged;
            Closing += OnWindowClosing;
            WireLiveValidationHandlers();

            ApplyOutputStrategy(ExportOutputStrategies.Get(ExportOutputStrategy.JuceFilmstripBestDefault));
            ApplyProjectTypeExportDefaults();
            ResetViewpointsFromOrbit(useCurrentCameraForPrimary: false);
            UpdateSpritesheetLayoutEnabled();
            UpdateImageFormatControlsEnabled();
            UpdateOrbitVariantControlsEnabled();
            UpdateStartRenderAvailability();
            ConfigureRotaryPreviewAvailability();
        }

        private static OutputStrategyOption[] BuildOutputStrategyOptions()
        {
            var definitions = ExportOutputStrategies.All;
            OutputStrategyOption[] options = new OutputStrategyOption[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                options[i] = new OutputStrategyOption(definitions[i]);
            }

            return options;
        }

        private static PreviewVariantOption[] BuildPreviewVariantOptions()
        {
            return
            [
                new PreviewVariantOption(string.Empty, "Straight On"),
                new PreviewVariantOption("under_left", "Under Left"),
                new PreviewVariantOption("under_right", "Under Right"),
                new PreviewVariantOption("over_left", "Over Left"),
                new PreviewVariantOption("over_right", "Over Right")
            ];
        }

        private static ExportImageFormat[] BuildFrameImageFormatOptions()
        {
            return
            [
                ExportImageFormat.PngOptimized,
                ExportImageFormat.PngLossless,
                ExportImageFormat.WebpLossless,
                ExportImageFormat.WebpLossy
            ];
        }

        private void ApplyPngOptimizationPreset(PngOptimizationPreset preset)
        {
            if (preset == PngOptimizationPreset.Custom)
            {
                return;
            }

            PngOptimizationProfileDefinition profile = PngOptimizationProfiles.Get(preset);
            _isApplyingPngOptimizationPreset = true;
            try
            {
                _pngOptimizationPresetComboBox.SelectedItem = preset;
                _pngMinimumSavingsKbTextBox.Text = Math.Max(0, profile.MinimumSavingsBytes / 1024).ToString(CultureInfo.InvariantCulture);
                _pngOpaqueRgbStepTextBox.Text = profile.OpaqueRgbStep.ToString(CultureInfo.InvariantCulture);
                _pngOpaqueAlphaStepTextBox.Text = profile.OpaqueAlphaStep.ToString(CultureInfo.InvariantCulture);
                _pngTranslucentRgbStepTextBox.Text = profile.TranslucentRgbStep.ToString(CultureInfo.InvariantCulture);
                _pngTranslucentAlphaStepTextBox.Text = profile.TranslucentAlphaStep.ToString(CultureInfo.InvariantCulture);
                _pngTranslucentAlphaThresholdTextBox.Text = profile.TranslucentAlphaThreshold.ToString(CultureInfo.InvariantCulture);
                _pngMaxOpaqueRgbDeltaTextBox.Text = profile.MaxOpaqueRgbDelta.ToString(CultureInfo.InvariantCulture);
                _pngMaxVisibleRgbDeltaTextBox.Text = profile.MaxVisibleRgbDelta.ToString(CultureInfo.InvariantCulture);
                _pngMaxVisibleAlphaDeltaTextBox.Text = profile.MaxVisibleAlphaDelta.ToString(CultureInfo.InvariantCulture);
                _pngMeanVisibleLumaDeltaTextBox.Text = profile.MeanVisibleLumaDelta.ToString("0.###", CultureInfo.InvariantCulture);
                _pngMeanVisibleAlphaDeltaTextBox.Text = profile.MeanVisibleAlphaDelta.ToString("0.###", CultureInfo.InvariantCulture);
            }
            finally
            {
                _isApplyingPngOptimizationPreset = false;
            }
        }

        private void OnPngOptimizationPresetSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingPngOptimizationPreset)
            {
                return;
            }

            if (_pngOptimizationPresetComboBox.SelectedItem is PngOptimizationPreset preset)
            {
                ApplyPngOptimizationPreset(preset);
                UpdateStartRenderAvailability();
                MarkRotaryPreviewDirty();
            }
        }

        private void OnOutputStrategySelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingOutputStrategy)
            {
                return;
            }

            if (_outputStrategyComboBox.SelectedItem is OutputStrategyOption option)
            {
                ApplyOutputStrategy(option.Definition);
            }
        }

        private void ApplyOutputStrategy(ExportOutputStrategyDefinition definition)
        {
            _isApplyingOutputStrategy = true;
            try
            {
                for (int i = 0; i < _outputStrategyOptions.Length; i++)
                {
                    if (_outputStrategyOptions[i].Definition.Strategy == definition.Strategy)
                    {
                        _outputStrategyComboBox.SelectedItem = _outputStrategyOptions[i];
                        break;
                    }
                }

                string frameCountText = definition.FrameCount.ToString(CultureInfo.InvariantCulture);
                string resolutionText = definition.Resolution.ToString(CultureInfo.InvariantCulture);
                string supersampleText = definition.SupersampleScale.ToString(CultureInfo.InvariantCulture);

                _frameCountTextBox.Text = frameCountText;
                _resolutionTextBox.Text = resolutionText;
                _supersampleComboBox.SelectedItem = supersampleText;
                _supersampleComboBox.Text = supersampleText;
                _paddingTextBox.Text = definition.Padding.ToString("0.###", CultureInfo.InvariantCulture);
                _cameraDistanceScaleTextBox.Text = definition.CameraDistanceScale.ToString("0.###", CultureInfo.InvariantCulture);
                _spritesheetLayoutComboBox.SelectedItem = _project.ProjectType == InteractorProjectType.RotaryKnob
                    ? SpritesheetLayout.Grid
                    : definition.SpritesheetLayout;
                _filterPresetComboBox.SelectedItem = definition.FilterPreset;
                _exportFramesCheckBox.IsChecked = definition.ExportIndividualFrames;
                _exportSpritesheetCheckBox.IsChecked = definition.ExportSpritesheet;
                _outputStrategyDescriptionTextBlock.Text = definition.Description;
            }
            finally
            {
                _isApplyingOutputStrategy = false;
                UpdateSpritesheetLayoutEnabled();
                UpdateImageFormatControlsEnabled();
                UpdateOrbitVariantControlsEnabled();
                UpdateStartRenderAvailability();
            }
        }

        private void ApplyProjectTypeExportDefaults()
        {
            if (_project.ProjectType == InteractorProjectType.IndicatorLight)
            {
                _frameCountTextBox.Text = DefaultIndicatorLightFrameCount.ToString(CultureInfo.InvariantCulture);
            }

            if (_project.ProjectType == InteractorProjectType.RotaryKnob)
            {
                _spritesheetLayoutComboBox.SelectedItem = SpritesheetLayout.Grid;
                _exportSpritesheetCheckBox.IsChecked = true;
            }
        }

        private void WireLiveValidationHandlers()
        {
            _frameCountTextBox.TextChanged += OnLiveValidationTextChanged;
            _paddingTextBox.TextChanged += OnLiveValidationTextChanged;
            _resolutionTextBox.TextChanged += OnLiveValidationTextChanged;
            _cameraDistanceScaleTextBox.TextChanged += OnLiveValidationTextChanged;
            _orbitYawOffsetTextBox.TextChanged += OnLiveValidationTextChanged;
            _orbitPitchOffsetTextBox.TextChanged += OnLiveValidationTextChanged;
            _previewBaseYawTextBox.TextChanged += OnLiveValidationTextChanged;
            _previewBasePitchTextBox.TextChanged += OnLiveValidationTextChanged;
            _baseNameTextBox.TextChanged += OnLiveValidationTextChanged;
            _outputFolderTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngCompressionLevelTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngMinimumSavingsKbTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngOpaqueRgbStepTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngOpaqueAlphaStepTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngTranslucentRgbStepTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngTranslucentAlphaStepTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngTranslucentAlphaThresholdTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngMaxOpaqueRgbDeltaTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngMaxVisibleRgbDeltaTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngMaxVisibleAlphaDeltaTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngMeanVisibleLumaDeltaTextBox.TextChanged += OnLiveValidationTextChanged;
            _pngMeanVisibleAlphaDeltaTextBox.TextChanged += OnLiveValidationTextChanged;

            _supersampleComboBox.SelectionChanged += OnLiveValidationSelectionChanged;
            _spritesheetLayoutComboBox.SelectionChanged += OnLiveValidationSelectionChanged;
            _filterPresetComboBox.SelectionChanged += OnLiveValidationSelectionChanged;
            _outputStrategyComboBox.SelectionChanged += OnLiveValidationSelectionChanged;
            _outputImageFormatComboBox.SelectionChanged += OnLiveValidationSelectionChanged;
            _pngOptimizationPresetComboBox.SelectionChanged += OnLiveValidationSelectionChanged;

            _supersampleComboBox.PropertyChanged += OnLiveValidationComboPropertyChanged;

            _exportFramesCheckBox.IsCheckedChanged += OnLiveValidationCheckedChanged;
            _exportSpritesheetCheckBox.IsCheckedChanged += OnLiveValidationCheckedChanged;
            _optimizeSpritesheetPngCheckBox.IsCheckedChanged += OnLiveValidationCheckedChanged;
            _exportOrbitVariantsCheckBox.IsCheckedChanged += OnLiveValidationCheckedChanged;
            _webpLossyQualityTextBox.TextChanged += OnLiveValidationTextChanged;
        }

        private void OnLiveValidationTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (ReferenceEquals(sender, _orbitYawOffsetTextBox) || ReferenceEquals(sender, _orbitPitchOffsetTextBox))
            {
                TrySyncViewpointsFromOrbitBaseline();
            }

            if (!_isApplyingPngOptimizationPreset && IsCompressionTuningTextBox(sender))
            {
                _pngOptimizationPresetComboBox.SelectedItem = PngOptimizationPreset.Custom;
            }

            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
        }

        private void OnLiveValidationSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(sender, _outputImageFormatComboBox))
            {
                UpdateImageFormatControlsEnabled();
            }

            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
        }

        private void OnLiveValidationCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, _exportOrbitVariantsCheckBox))
            {
                TrySyncViewpointsFromOrbitBaseline();
            }

            UpdateImageFormatControlsEnabled();
            UpdateStartRenderAvailability();
            MarkRotaryPreviewDirty();
        }

        private void OnLiveValidationComboPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ComboBox.TextProperty)
            {
                UpdateStartRenderAvailability();
                MarkRotaryPreviewDirty();
            }
        }

        private bool IsCompressionTuningTextBox(object? sender)
        {
            return ReferenceEquals(sender, _pngMinimumSavingsKbTextBox) ||
                ReferenceEquals(sender, _pngOpaqueRgbStepTextBox) ||
                ReferenceEquals(sender, _pngOpaqueAlphaStepTextBox) ||
                ReferenceEquals(sender, _pngTranslucentRgbStepTextBox) ||
                ReferenceEquals(sender, _pngTranslucentAlphaStepTextBox) ||
                ReferenceEquals(sender, _pngTranslucentAlphaThresholdTextBox) ||
                ReferenceEquals(sender, _pngMaxOpaqueRgbDeltaTextBox) ||
                ReferenceEquals(sender, _pngMaxVisibleRgbDeltaTextBox) ||
                ReferenceEquals(sender, _pngMaxVisibleAlphaDeltaTextBox) ||
                ReferenceEquals(sender, _pngMeanVisibleLumaDeltaTextBox) ||
                ReferenceEquals(sender, _pngMeanVisibleAlphaDeltaTextBox);
        }

        private static string GetDefaultOutputFolder()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrWhiteSpace(desktop))
            {
                return desktop;
            }

            return Directory.GetCurrentDirectory();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
