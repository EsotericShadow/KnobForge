using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KnobForge.App.Controls;
using KnobForge.Core;
using KnobForge.Core.Scene;
using System;
using System.Linq;
using System.Reflection;

#pragma warning disable CS8602

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private bool HasRequiredControls()
        {
            if (_metalViewport == null || _viewportOverlay == null || _sceneListBox == null || _lightingModeCombo == null || _lightListBox == null ||
                _addLightButton == null || _removeLightButton == null || _resetViewButton == null ||
                _rotationSlider == null || _lightTypeCombo == null || _lightXSlider == null ||
                _lightYSlider == null || _lightZSlider == null || _directionSlider == null ||
                _intensitySlider == null || _falloffSlider == null || _lightRSlider == null ||
                _lightGSlider == null || _lightBSlider == null || _diffuseBoostSlider == null ||
                _specularBoostSlider == null || _specularPowerSlider == null ||
                _modelRadiusSlider == null || _modelHeightSlider == null || _modelTopScaleSlider == null ||
                _modelBevelSlider == null || _referenceStyleCombo == null || _referenceStyleSaveNameTextBox == null || _saveReferenceProfileButton == null || _bodyStyleCombo == null || _bevelCurveSlider == null || _crownProfileSlider == null ||
                _bodyTaperSlider == null || _bodyBulgeSlider == null || _modelSegmentsSlider == null ||
                _spiralRidgeHeightSlider == null || _spiralRidgeWidthSlider == null || _spiralTurnsSlider == null ||
                _gripStyleCombo == null || _gripTypeCombo == null || _gripStartSlider == null || _gripHeightSlider == null ||
                _gripDensitySlider == null || _gripPitchSlider == null || _gripDepthSlider == null ||
                _gripWidthSlider == null || _gripSharpnessSlider == null ||
                _collarEnabledCheckBox == null || _collarPresetCombo == null || _collarMeshPathTextBox == null ||
                _collarScaleSlider == null || _collarBodyLengthSlider == null || _collarBodyThicknessSlider == null ||
                _collarHeadLengthSlider == null || _collarHeadThicknessSlider == null ||
                _collarRotateSlider == null || _collarMirrorXCheckBox == null || _collarMirrorYCheckBox == null || _collarMirrorZCheckBox == null ||
                _collarOffsetXSlider == null || _collarOffsetYSlider == null || _collarElevationSlider == null || _collarInflateSlider == null ||
                _collarMaterialBaseRSlider == null || _collarMaterialBaseGSlider == null || _collarMaterialBaseBSlider == null ||
                _collarMaterialMetallicSlider == null || _collarMaterialRoughnessSlider == null || _collarMaterialPearlescenceSlider == null ||
                _collarMaterialRustSlider == null || _collarMaterialWearSlider == null || _collarMaterialGunkSlider == null ||
                _indicatorEnabledCheckBox == null || _indicatorShapeCombo == null || _indicatorReliefCombo == null ||
                _indicatorProfileCombo == null || _indicatorWidthSlider == null || _indicatorLengthSlider == null ||
                _indicatorPositionSlider == null ||
                _indicatorThicknessSlider == null || _indicatorRoundnessSlider == null || _indicatorColorBlendSlider == null ||
                _indicatorColorRSlider == null || _indicatorColorGSlider == null || _indicatorColorBSlider == null ||
                _materialBaseRSlider == null || _materialBaseGSlider == null || _materialBaseBSlider == null || _materialRegionCombo == null ||
                _materialMetallicSlider == null || _materialRoughnessSlider == null || _materialPearlescenceSlider == null ||
                _materialRustSlider == null || _materialWearSlider == null || _materialGunkSlider == null ||
                _materialAlbedoMapBrowseButton == null || _materialAlbedoMapClearButton == null ||
                _materialNormalMapBrowseButton == null || _materialNormalMapClearButton == null ||
                _materialRoughnessMapBrowseButton == null || _materialRoughnessMapClearButton == null ||
                _materialMetallicMapBrowseButton == null || _materialMetallicMapClearButton == null ||
                _materialNormalMapStrengthSlider == null || _materialNormalMapStrengthPanel == null ||
                _materialAlbedoMapPathText == null || _materialNormalMapPathText == null ||
                _materialRoughnessMapPathText == null || _materialMetallicMapPathText == null ||
                _materialNormalMapStrengthValueText == null ||
                _materialBrushStrengthSlider == null || _materialBrushDensitySlider == null || _materialCharacterSlider == null ||
                _spiralNormalInfluenceCheckBox == null || _basisDebugModeCombo == null || _microLodFadeStartSlider == null || _microLodFadeEndSlider == null || _microRoughnessLodBoostSlider == null ||
                _envIntensitySlider == null || _envRoughnessMixSlider == null ||
                _envTopRSlider == null || _envTopGSlider == null || _envTopBSlider == null ||
                _envBottomRSlider == null || _envBottomGSlider == null || _envBottomBSlider == null ||
                _shadowEnabledCheckBox == null || _shadowSourceModeCombo == null || _shadowStrengthSlider == null || _shadowSoftnessSlider == null ||
                _shadowDistanceSlider == null || _shadowScaleSlider == null || _shadowQualitySlider == null ||
                _shadowGraySlider == null || _shadowDiffuseInfluenceSlider == null ||
                _brushPaintEnabledCheckBox == null || _brushPaintChannelCombo == null || _brushTypeCombo == null || _brushPaintColorPicker == null || _scratchAbrasionTypeCombo == null ||
                _brushSizeSlider == null || _brushOpacitySlider == null || _brushDarknessSlider == null || _brushSpreadSlider == null ||
                _paintCoatMetallicSlider == null || _paintCoatRoughnessSlider == null ||
                _clearCoatAmountSlider == null || _clearCoatRoughnessSlider == null || _anisotropyAngleSlider == null ||
                _scratchWidthSlider == null || _scratchDepthSlider == null || _scratchResistanceSlider == null || _scratchDepthRampSlider == null ||
                _scratchExposeColorRSlider == null || _scratchExposeColorGSlider == null || _scratchExposeColorBSlider == null ||
                _scratchExposeMetallicSlider == null || _scratchExposeRoughnessSlider == null ||
                _clearPaintMaskButton == null ||
                _renderButton == null ||
                _rotationValueText == null || _lightXValueText == null || _lightYValueText == null ||
                _lightZValueText == null || _directionValueText == null ||
                _intensityValueText == null || _falloffValueText == null ||
                _lightRValueText == null || _lightGValueText == null || _lightBValueText == null ||
                _diffuseBoostValueText == null || _specularBoostValueText == null ||
                _specularPowerValueText == null || _centerLightButton == null ||
                _modelRadiusValueText == null || _modelHeightValueText == null ||
                _modelTopScaleValueText == null || _modelBevelValueText == null || _bevelCurveValueText == null ||
                _crownProfileValueText == null || _bodyTaperValueText == null || _bodyBulgeValueText == null ||
                _modelSegmentsValueText == null ||
                _spiralRidgeHeightValueText == null || _spiralRidgeWidthValueText == null || _spiralTurnsValueText == null ||
                _gripStartValueText == null || _gripHeightValueText == null || _gripDensityValueText == null ||
                _gripPitchValueText == null || _gripDepthValueText == null || _gripWidthValueText == null ||
                _gripSharpnessValueText == null ||
                _collarScaleValueText == null || _collarBodyLengthValueText == null || _collarBodyThicknessValueText == null ||
                _collarHeadLengthValueText == null || _collarHeadThicknessValueText == null ||
                _collarRotateValueText == null || _collarOffsetXValueText == null || _collarOffsetYValueText == null || _collarElevationValueText == null || _collarInflateValueText == null ||
                _collarMaterialBaseRValueText == null || _collarMaterialBaseGValueText == null || _collarMaterialBaseBValueText == null ||
                _collarMaterialMetallicValueText == null || _collarMaterialRoughnessValueText == null || _collarMaterialPearlescenceValueText == null ||
                _collarMaterialRustValueText == null || _collarMaterialWearValueText == null || _collarMaterialGunkValueText == null ||
                _indicatorWidthValueText == null || _indicatorLengthValueText == null || _indicatorPositionValueText == null || _indicatorThicknessValueText == null ||
                _indicatorRoundnessValueText == null || _indicatorColorBlendValueText == null ||
                _indicatorColorRValueText == null || _indicatorColorGValueText == null || _indicatorColorBValueText == null ||
                _materialBaseRValueText == null || _materialBaseGValueText == null || _materialBaseBValueText == null ||
                _materialMetallicValueText == null || _materialRoughnessValueText == null || _materialPearlescenceValueText == null ||
                _materialRustValueText == null || _materialWearValueText == null || _materialGunkValueText == null ||
                _materialBrushStrengthValueText == null || _materialBrushDensityValueText == null || _materialCharacterValueText == null ||
                _microLodFadeStartValueText == null || _microLodFadeEndValueText == null || _microRoughnessLodBoostValueText == null ||
                _envIntensityValueText == null || _envRoughnessMixValueText == null ||
                _envTopRValueText == null || _envTopGValueText == null || _envTopBValueText == null ||
                _envBottomRValueText == null || _envBottomGValueText == null || _envBottomBValueText == null ||
                _shadowStrengthValueText == null || _shadowSoftnessValueText == null || _shadowDistanceValueText == null ||
                _shadowScaleValueText == null || _shadowQualityValueText == null || _shadowGrayValueText == null ||
                _shadowDiffuseInfluenceValueText == null ||
                _brushSizeValueText == null || _brushOpacityValueText == null || _brushDarknessValueText == null || _brushSpreadValueText == null ||
                _paintCoatMetallicValueText == null || _paintCoatRoughnessValueText == null ||
                _clearCoatAmountValueText == null || _clearCoatRoughnessValueText == null || _anisotropyAngleValueText == null ||
                _scratchWidthValueText == null || _scratchDepthValueText == null || _scratchResistanceValueText == null || _scratchDepthRampValueText == null ||
                _scratchExposeColorRValueText == null || _scratchExposeColorGValueText == null || _scratchExposeColorBValueText == null ||
                _scratchExposeMetallicValueText == null || _scratchExposeRoughnessValueText == null)
            {
                return false;
            }
            return true;
        }

        private void InitializeViewportAndSceneBindings()
        {
            _metalViewport.Project = _project;
            _metalViewport.InvalidateGpu();
            _metalViewport.RuntimeDiagnosticsUpdated += OnViewportRuntimeDiagnosticsUpdated;

            _viewportOverlay.Focusable = true;
            _viewportOverlay.IsHitTestVisible = true;
            _viewportOverlay.AddHandler(InputElement.PointerPressedEvent, ViewportOverlay_PointerPressed, RoutingStrategies.Tunnel);
            _viewportOverlay.AddHandler(InputElement.PointerMovedEvent, ViewportOverlay_PointerMoved, RoutingStrategies.Tunnel);
            _viewportOverlay.AddHandler(InputElement.PointerReleasedEvent, ViewportOverlay_PointerReleased, RoutingStrategies.Tunnel);
            _viewportOverlay.AddHandler(InputElement.PointerWheelChangedEvent, ViewportOverlay_PointerWheelChanged, RoutingStrategies.Tunnel);
            _viewportOverlay.AddHandler(InputElement.KeyDownEvent, ViewportOverlay_KeyDown, RoutingStrategies.Tunnel);
            _viewportOverlay.AddHandler(InputElement.KeyUpEvent, ViewportOverlay_KeyUp, RoutingStrategies.Tunnel);
            _sceneListBox.ItemsSource = _sceneNodes;
            _lightingModeCombo.ItemsSource = Enum.GetValues<LightingMode>().Cast<LightingMode>().ToList();
            _lightTypeCombo.ItemsSource = Enum.GetValues<LightType>().Cast<LightType>().ToList();
            RebuildReferenceStyleOptions();
            _bodyStyleCombo.ItemsSource = Enum.GetValues<BodyStyle>().Cast<BodyStyle>().ToList();
            if (_sliderAssemblyModeCombo != null)
            {
                _sliderAssemblyModeCombo.ItemsSource = Enum.GetValues<SliderAssemblyMode>().Cast<SliderAssemblyMode>().ToList();
            }
            if (_toggleAssemblyModeCombo != null)
            {
                _toggleAssemblyModeCombo.ItemsSource = Enum.GetValues<ToggleAssemblyMode>().Cast<ToggleAssemblyMode>().ToList();
            }
            if (_toggleStateCountCombo != null)
            {
                _toggleStateCountCombo.ItemsSource = Enum.GetValues<ToggleAssemblyStateCount>().Cast<ToggleAssemblyStateCount>().ToList();
            }
            if (_toggleLowerBushingShapeCombo != null)
            {
                _toggleLowerBushingShapeCombo.ItemsSource = Enum.GetValues<ToggleBushingShape>().Cast<ToggleBushingShape>().ToList();
            }
            if (_toggleUpperBushingShapeCombo != null)
            {
                _toggleUpperBushingShapeCombo.ItemsSource = Enum.GetValues<ToggleBushingShape>().Cast<ToggleBushingShape>().ToList();
            }
            if (_toggleTipSleeveStyleCombo != null)
            {
                _toggleTipSleeveStyleCombo.ItemsSource = Enum.GetValues<ToggleTipSleeveStyle>().Cast<ToggleTipSleeveStyle>().ToList();
            }
            if (_toggleTipSleeveTipStyleCombo != null)
            {
                _toggleTipSleeveTipStyleCombo.ItemsSource = Enum.GetValues<ToggleTipSleeveTipStyle>().Cast<ToggleTipSleeveTipStyle>().ToList();
            }
            RebuildSliderMeshOptions();
            RebuildToggleMeshOptions();
            _gripStyleCombo.ItemsSource = Enum.GetValues<GripStyle>().Cast<GripStyle>().ToList();
            _gripTypeCombo.ItemsSource = Enum.GetValues<GripType>().Cast<GripType>().ToList();
            RebuildCollarPresetOptions();
            InitializeCollarLibraryHotReload();
            _indicatorShapeCombo.ItemsSource = Enum.GetValues<IndicatorShape>().Cast<IndicatorShape>().ToList();
            _indicatorReliefCombo.ItemsSource = Enum.GetValues<IndicatorRelief>().Cast<IndicatorRelief>().ToList();
            _indicatorProfileCombo.ItemsSource = Enum.GetValues<IndicatorProfile>().Cast<IndicatorProfile>().ToList();
            if (_indicatorLightAnimationModeCombo != null)
            {
                _indicatorLightAnimationModeCombo.ItemsSource = Enum.GetValues<DynamicLightAnimationMode>().Cast<DynamicLightAnimationMode>().ToList();
            }
            if (_indicatorEmitterSourceCombo != null)
            {
                _indicatorEmitterSourceCombo.ItemsSource = Array.Empty<string>();
            }
            _materialRegionCombo.ItemsSource = Enum.GetValues<MaterialRegionTarget>().Cast<MaterialRegionTarget>().ToList();
            _materialRegionCombo.SelectedItem = MaterialRegionTarget.WholeKnob;
            _basisDebugModeCombo.ItemsSource = Enum.GetValues<BasisDebugMode>().Cast<BasisDebugMode>().ToList();
            if (_envTonemapCombo != null)
            {
                _envTonemapCombo.ItemsSource = Enum.GetValues<TonemapOperator>().Cast<TonemapOperator>().ToList();
            }
            _shadowSourceModeCombo.ItemsSource = Enum.GetValues<ShadowLightMode>().Cast<ShadowLightMode>().ToList();
            _brushPaintChannelCombo.ItemsSource = Enum.GetValues<PaintChannel>().Cast<PaintChannel>().ToList();
            _brushTypeCombo.ItemsSource = Enum.GetValues<PaintBrushType>().Cast<PaintBrushType>().ToList();
            _scratchAbrasionTypeCombo.ItemsSource = Enum.GetValues<ScratchAbrasionType>().Cast<ScratchAbrasionType>().ToList();

            _sceneListBox.SelectionChanged += (_, _) =>
            {
                if (_sceneListBox == null)
                {
                    return;
                }

                var selectedNode = _sceneListBox.SelectedItem as SceneNode;
                if (IsUiRefreshing)
                {
                    return;
                }

                if (selectedNode is SceneNode node)
                {
                    if (_project.SelectedNode?.Id == node.Id)
                    {
                        SyncInspectorForSelectedSceneNode(node);
                        return;
                    }

                    _project.SetSelectedNode(node);
                    SyncInspectorForSelectedSceneNode(node);
                }
            };
            _lightListBox.SelectionChanged += OnLightListSelectionChanged;
        }

        private void WireButtonHandlers()
        {
            _addLightButton.Click += (_, _) => AddLight();
            _removeLightButton.Click += (_, _) => RemoveSelectedLight();
            _centerLightButton.Click += (_, _) => CenterLight();
            _saveReferenceProfileButton.Click += OnSaveReferenceProfileClicked;
            if (_newProjectButton != null)
            {
                _newProjectButton.Click += (_, _) => OpenNewProjectWindowFromMenu();
            }

            if (_changeProjectTypeButton != null)
            {
                _changeProjectTypeButton.Click += (_, _) => ChangeProjectTypeFromMenu();
            }

            if (_openProjectButton != null)
            {
                _openProjectButton.Click += OnOpenProjectButtonClicked;
            }

            if (_saveProjectButton != null)
            {
                _saveProjectButton.Click += OnSaveProjectButtonClicked;
            }

            if (_saveProjectAsButton != null)
            {
                _saveProjectAsButton.Click += OnSaveProjectAsButtonClicked;
            }

            if (_overwriteReferenceProfileButton != null)
            {
                _overwriteReferenceProfileButton.Click += OnOverwriteReferenceProfileClicked;
            }
            if (_renameReferenceProfileButton != null)
            {
                _renameReferenceProfileButton.Click += OnRenameReferenceProfileClicked;
            }
            if (_duplicateReferenceProfileButton != null)
            {
                _duplicateReferenceProfileButton.Click += OnDuplicateReferenceProfileClicked;
            }
            if (_deleteReferenceProfileButton != null)
            {
                _deleteReferenceProfileButton.Click += OnDeleteReferenceProfileClicked;
            }
            if (_refreshCollarLibraryButton != null)
            {
                _refreshCollarLibraryButton.Click += OnRefreshCollarLibraryButtonClicked;
            }
            if (_refreshSliderLibraryButton != null)
            {
                _refreshSliderLibraryButton.Click += OnRefreshSliderLibraryButtonClicked;
            }
            if (_refreshToggleLibraryButton != null)
            {
                _refreshToggleLibraryButton.Click += OnRefreshToggleLibraryButtonClicked;
            }
            if (_indicatorAssemblyResetDefaultsButton != null)
            {
                _indicatorAssemblyResetDefaultsButton.Click += (_, _) => ResetIndicatorAssemblyDefaultsFromUi();
            }
            if (_indicatorLightPresetNeutralButton != null)
            {
                _indicatorLightPresetNeutralButton.Click += (_, _) => ApplyIndicatorLightPreset(IndicatorLightPreset.Neutral);
            }
            if (_indicatorLightPresetPulseButton != null)
            {
                _indicatorLightPresetPulseButton.Click += (_, _) => ApplyIndicatorLightPreset(IndicatorLightPreset.Pulse);
            }
            if (_indicatorLightPresetFlickerButton != null)
            {
                _indicatorLightPresetFlickerButton.Click += (_, _) => ApplyIndicatorLightPreset(IndicatorLightPreset.Flicker);
            }
            if (_indicatorLensPresetClearButton != null)
            {
                _indicatorLensPresetClearButton.Click += (_, _) => ApplyIndicatorLensMaterialPreset(IndicatorLensMaterialPreset.ClearLens);
            }
            if (_indicatorLensPresetFrostedButton != null)
            {
                _indicatorLensPresetFrostedButton.Click += (_, _) => ApplyIndicatorLensMaterialPreset(IndicatorLensMaterialPreset.FrostedLens);
            }
            if (_indicatorLensPresetSaturatedButton != null)
            {
                _indicatorLensPresetSaturatedButton.Click += (_, _) => ApplyIndicatorLensMaterialPreset(IndicatorLensMaterialPreset.SaturatedLedLens);
            }
            if (_indicatorEmitterSourceMoveUpButton != null)
            {
                _indicatorEmitterSourceMoveUpButton.Click += (_, _) => MoveSelectedIndicatorEmitterSource(-1);
            }
            if (_indicatorEmitterSourceMoveDownButton != null)
            {
                _indicatorEmitterSourceMoveDownButton.Click += (_, _) => MoveSelectedIndicatorEmitterSource(1);
            }
            if (_indicatorEmitterSourceAutoPhaseButton != null)
            {
                _indicatorEmitterSourceAutoPhaseButton.Click += (_, _) => AutoDistributeIndicatorEmitterPhases();
            }
            if (_envHdriApplyButton != null)
            {
                _envHdriApplyButton.Click += (_, _) => ApplyEnvironmentHdriPathFromUi();
            }
            if (_envHdriClearButton != null)
            {
                _envHdriClearButton.Click += (_, _) => ClearEnvironmentHdriPathFromUi();
            }
            if (_materialAlbedoMapBrowseButton != null)
            {
                _materialAlbedoMapBrowseButton.Click += OnMaterialAlbedoMapBrowseClicked;
            }
            if (_materialAlbedoMapClearButton != null)
            {
                _materialAlbedoMapClearButton.Click += OnMaterialAlbedoMapClearClicked;
            }
            if (_materialNormalMapBrowseButton != null)
            {
                _materialNormalMapBrowseButton.Click += OnMaterialNormalMapBrowseClicked;
            }
            if (_materialNormalMapClearButton != null)
            {
                _materialNormalMapClearButton.Click += OnMaterialNormalMapClearClicked;
            }
            if (_materialRoughnessMapBrowseButton != null)
            {
                _materialRoughnessMapBrowseButton.Click += OnMaterialRoughnessMapBrowseClicked;
            }
            if (_materialRoughnessMapClearButton != null)
            {
                _materialRoughnessMapClearButton.Click += OnMaterialRoughnessMapClearClicked;
            }
            if (_materialMetallicMapBrowseButton != null)
            {
                _materialMetallicMapBrowseButton.Click += OnMaterialMetallicMapBrowseClicked;
            }
            if (_materialMetallicMapClearButton != null)
            {
                _materialMetallicMapClearButton.Click += OnMaterialMetallicMapClearClicked;
            }
            _resetViewButton.Click += (_, _) => _metalViewport?.ResetCamera();
            _clearPaintMaskButton.Click += (_, _) => OnClearPaintMask();
            _renderButton.Click += OnRenderButtonClick;
            if (_undoButton != null)
            {
                _undoButton.Click += (_, _) => ExecuteUndo();
            }

            if (_redoButton != null)
            {
                _redoButton.Click += (_, _) => ExecuteRedo();
            }

            WireBrushQuickToolbarButtons();
        }

        private void WireControlPropertyHandlers()
        {
            _lightingModeCombo.PropertyChanged += OnLightingModeChanged;
            _lightTypeCombo.PropertyChanged += OnLightTypeChanged;
            _rotationSlider.PropertyChanged += OnRotationChanged;
            _lightXSlider.PropertyChanged += OnLightXChanged;
            _lightYSlider.PropertyChanged += OnLightYChanged;
            _lightZSlider.PropertyChanged += OnLightZChanged;
            _directionSlider.PropertyChanged += OnDirectionChanged;
            _intensitySlider.PropertyChanged += OnIntensityChanged;
            _falloffSlider.PropertyChanged += OnFalloffChanged;
            _lightRSlider.PropertyChanged += OnColorChanged;
            _lightGSlider.PropertyChanged += OnColorChanged;
            _lightBSlider.PropertyChanged += OnColorChanged;
            _diffuseBoostSlider.PropertyChanged += OnDiffuseBoostChanged;
            _specularBoostSlider.PropertyChanged += OnSpecularBoostChanged;
            _specularPowerSlider.PropertyChanged += OnSpecularPowerChanged;
            _modelRadiusSlider.PropertyChanged += OnModelRadiusChanged;
            _modelHeightSlider.PropertyChanged += OnModelHeightChanged;
            _modelTopScaleSlider.PropertyChanged += OnModelTopScaleChanged;
            _modelBevelSlider.PropertyChanged += OnModelBevelChanged;
            _referenceStyleCombo.PropertyChanged += OnReferenceStyleChanged;
            _bodyStyleCombo.PropertyChanged += OnBodyStyleChanged;
            _bevelCurveSlider.PropertyChanged += OnBodyDesignChanged;
            _crownProfileSlider.PropertyChanged += OnBodyDesignChanged;
            _bodyTaperSlider.PropertyChanged += OnBodyDesignChanged;
            _bodyBulgeSlider.PropertyChanged += OnBodyDesignChanged;
            _modelSegmentsSlider.PropertyChanged += OnModelSegmentsChanged;
            if (_sliderAssemblyModeCombo != null)
            {
                _sliderAssemblyModeCombo.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderBackplateMeshCombo != null)
            {
                _sliderBackplateMeshCombo.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderThumbMeshCombo != null)
            {
                _sliderThumbMeshCombo.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderBackplateWidthSlider != null)
            {
                _sliderBackplateWidthSlider.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderBackplateHeightSlider != null)
            {
                _sliderBackplateHeightSlider.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderBackplateThicknessSlider != null)
            {
                _sliderBackplateThicknessSlider.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderThumbWidthSlider != null)
            {
                _sliderThumbWidthSlider.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderThumbHeightSlider != null)
            {
                _sliderThumbHeightSlider.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderThumbDepthSlider != null)
            {
                _sliderThumbDepthSlider.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_pushButtonPressAmountSlider != null)
            {
                _pushButtonPressAmountSlider.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_toggleAssemblyModeCombo != null)
            {
                _toggleAssemblyModeCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleBaseMeshCombo != null)
            {
                _toggleBaseMeshCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverMeshCombo != null)
            {
                _toggleLeverMeshCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleStateCountCombo != null)
            {
                _toggleStateCountCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleStateIndexSlider != null)
            {
                _toggleStateIndexSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleMaxAngleSlider != null)
            {
                _toggleMaxAngleSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePlateWidthSlider != null)
            {
                _togglePlateWidthSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePlateHeightSlider != null)
            {
                _togglePlateHeightSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePlateThicknessSlider != null)
            {
                _togglePlateThicknessSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePlateOffsetYSlider != null)
            {
                _togglePlateOffsetYSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePlateOffsetZSlider != null)
            {
                _togglePlateOffsetZSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleBushingRadiusSlider != null)
            {
                _toggleBushingRadiusSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleBushingHeightSlider != null)
            {
                _toggleBushingHeightSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleBushingSidesSlider != null)
            {
                _toggleBushingSidesSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLowerBushingShapeCombo != null)
            {
                _toggleLowerBushingShapeCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingShapeCombo != null)
            {
                _toggleUpperBushingShapeCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLowerBushingRadiusScaleSlider != null)
            {
                _toggleLowerBushingRadiusScaleSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLowerBushingHeightRatioSlider != null)
            {
                _toggleLowerBushingHeightRatioSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingRadiusScaleSlider != null)
            {
                _toggleUpperBushingRadiusScaleSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingHeightRatioSlider != null)
            {
                _toggleUpperBushingHeightRatioSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingKnurlAmountSlider != null)
            {
                _toggleUpperBushingKnurlAmountSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingKnurlDensitySlider != null)
            {
                _toggleUpperBushingKnurlDensitySlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingKnurlDepthSlider != null)
            {
                _toggleUpperBushingKnurlDepthSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingAnisotropyStrengthSlider != null)
            {
                _toggleUpperBushingAnisotropyStrengthSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingAnisotropyDensitySlider != null)
            {
                _toggleUpperBushingAnisotropyDensitySlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingAnisotropyAngleSlider != null)
            {
                _toggleUpperBushingAnisotropyAngleSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingSurfaceCharacterSlider != null)
            {
                _toggleUpperBushingSurfaceCharacterSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePivotHousingRadiusSlider != null)
            {
                _togglePivotHousingRadiusSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePivotHousingDepthSlider != null)
            {
                _togglePivotHousingDepthSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePivotHousingBevelSlider != null)
            {
                _togglePivotHousingBevelSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePivotBallRadiusSlider != null)
            {
                _togglePivotBallRadiusSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePivotClearanceSlider != null)
            {
                _togglePivotClearanceSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleInvertBaseWindingCheckBox != null)
            {
                _toggleInvertBaseWindingCheckBox.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleInvertLeverWindingCheckBox != null)
            {
                _toggleInvertLeverWindingCheckBox.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverLengthSlider != null)
            {
                _toggleLeverLengthSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverRadiusSlider != null)
            {
                _toggleLeverRadiusSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverTopRadiusSlider != null)
            {
                _toggleLeverTopRadiusSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverSidesSlider != null)
            {
                _toggleLeverSidesSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverPivotOffsetSlider != null)
            {
                _toggleLeverPivotOffsetSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipRadiusSlider != null)
            {
                _toggleTipRadiusSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipLatitudeSegmentsSlider != null)
            {
                _toggleTipLatitudeSegmentsSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipLongitudeSegmentsSlider != null)
            {
                _toggleTipLongitudeSegmentsSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveEnabledCheckBox != null)
            {
                _toggleTipSleeveEnabledCheckBox.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveLengthSlider != null)
            {
                _toggleTipSleeveLengthSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveThicknessSlider != null)
            {
                _toggleTipSleeveThicknessSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveOuterRadiusSlider != null)
            {
                _toggleTipSleeveOuterRadiusSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveCoverageSlider != null)
            {
                _toggleTipSleeveCoverageSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveSidesSlider != null)
            {
                _toggleTipSleeveSidesSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveStyleCombo != null)
            {
                _toggleTipSleeveStyleCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveTipStyleCombo != null)
            {
                _toggleTipSleeveTipStyleCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleevePatternCountSlider != null)
            {
                _toggleTipSleevePatternCountSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleevePatternDepthSlider != null)
            {
                _toggleTipSleevePatternDepthSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveTipAmountSlider != null)
            {
                _toggleTipSleeveTipAmountSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveColorRSlider != null)
            {
                _toggleTipSleeveColorRSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveColorGSlider != null)
            {
                _toggleTipSleeveColorGSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveColorBSlider != null)
            {
                _toggleTipSleeveColorBSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveMetallicSlider != null)
            {
                _toggleTipSleeveMetallicSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveRoughnessSlider != null)
            {
                _toggleTipSleeveRoughnessSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleevePearlescenceSlider != null)
            {
                _toggleTipSleevePearlescenceSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveDiffuseStrengthSlider != null)
            {
                _toggleTipSleeveDiffuseStrengthSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveSpecularStrengthSlider != null)
            {
                _toggleTipSleeveSpecularStrengthSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveRustSlider != null)
            {
                _toggleTipSleeveRustSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveWearSlider != null)
            {
                _toggleTipSleeveWearSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveGunkSlider != null)
            {
                _toggleTipSleeveGunkSlider.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            _spiralRidgeHeightSlider.PropertyChanged += OnSpiralGeometryChanged;
            _spiralRidgeWidthSlider.PropertyChanged += OnSpiralGeometryChanged;
            _spiralTurnsSlider.PropertyChanged += OnSpiralGeometryChanged;
            _gripStyleCombo.PropertyChanged += OnGripStyleChanged;
            _gripTypeCombo.PropertyChanged += OnGripSettingsChanged;
            _gripStartSlider.PropertyChanged += OnGripSettingsChanged;
            _gripHeightSlider.PropertyChanged += OnGripSettingsChanged;
            _gripDensitySlider.PropertyChanged += OnGripSettingsChanged;
            _gripPitchSlider.PropertyChanged += OnGripSettingsChanged;
            _gripDepthSlider.PropertyChanged += OnGripSettingsChanged;
            _gripWidthSlider.PropertyChanged += OnGripSettingsChanged;
            _gripSharpnessSlider.PropertyChanged += OnGripSettingsChanged;
            _collarEnabledCheckBox.PropertyChanged += OnCollarSettingsChanged;
            _collarPresetCombo.PropertyChanged += OnCollarSettingsChanged;
            _collarMeshPathTextBox.PropertyChanged += OnCollarSettingsChanged;
            _collarScaleSlider.PropertyChanged += OnCollarSettingsChanged;
            _collarBodyLengthSlider.PropertyChanged += OnCollarSettingsChanged;
            _collarBodyThicknessSlider.PropertyChanged += OnCollarSettingsChanged;
            _collarHeadLengthSlider.PropertyChanged += OnCollarSettingsChanged;
            _collarHeadThicknessSlider.PropertyChanged += OnCollarSettingsChanged;
            _collarRotateSlider.PropertyChanged += OnCollarSettingsChanged;
            _collarMirrorXCheckBox.PropertyChanged += OnCollarSettingsChanged;
            _collarMirrorYCheckBox.PropertyChanged += OnCollarSettingsChanged;
            _collarMirrorZCheckBox.PropertyChanged += OnCollarSettingsChanged;
            _collarOffsetXSlider.PropertyChanged += OnCollarSettingsChanged;
            _collarOffsetYSlider.PropertyChanged += OnCollarSettingsChanged;
            _collarElevationSlider.PropertyChanged += OnCollarSettingsChanged;
            _collarInflateSlider.PropertyChanged += OnCollarSettingsChanged;
            _collarMaterialBaseRSlider.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialBaseGSlider.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialBaseBSlider.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialMetallicSlider.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialRoughnessSlider.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialPearlescenceSlider.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialRustSlider.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialWearSlider.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialGunkSlider.PropertyChanged += OnCollarMaterialChanged;
            _indicatorEnabledCheckBox.PropertyChanged += OnIndicatorSettingsChanged;
            if (_indicatorCadWallsCheckBox != null)
            {
                _indicatorCadWallsCheckBox.PropertyChanged += OnIndicatorSettingsChanged;
            }
            _indicatorShapeCombo.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorReliefCombo.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorProfileCombo.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorWidthSlider.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorLengthSlider.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorPositionSlider.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorThicknessSlider.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorRoundnessSlider.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorColorBlendSlider.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorColorRSlider.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorColorGSlider.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorColorBSlider.PropertyChanged += OnIndicatorSettingsChanged;
            if (_indicatorAssemblyEnabledCheckBox != null)
            {
                _indicatorAssemblyEnabledCheckBox.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorQuickLightOnCheckBox != null)
            {
                _indicatorQuickLightOnCheckBox.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorQuickBrightnessSlider != null)
            {
                _indicatorQuickBrightnessSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorQuickGlowSlider != null)
            {
                _indicatorQuickGlowSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorBaseWidthSlider != null)
            {
                _indicatorBaseWidthSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorBaseHeightSlider != null)
            {
                _indicatorBaseHeightSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorBaseThicknessSlider != null)
            {
                _indicatorBaseThicknessSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorHousingRadiusSlider != null)
            {
                _indicatorHousingRadiusSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorHousingHeightSlider != null)
            {
                _indicatorHousingHeightSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensRadiusSlider != null)
            {
                _indicatorLensRadiusSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensHeightSlider != null)
            {
                _indicatorLensHeightSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensTransmissionSlider != null)
            {
                _indicatorLensTransmissionSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensIorSlider != null)
            {
                _indicatorLensIorSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensThicknessSlider != null)
            {
                _indicatorLensThicknessSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensAbsorptionSlider != null)
            {
                _indicatorLensAbsorptionSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensSurfaceRoughnessSlider != null)
            {
                _indicatorLensSurfaceRoughnessSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensSurfaceSpecularSlider != null)
            {
                _indicatorLensSurfaceSpecularSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensTintRSlider != null)
            {
                _indicatorLensTintRSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensTintGSlider != null)
            {
                _indicatorLensTintGSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensTintBSlider != null)
            {
                _indicatorLensTintBSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorReflectorBaseRadiusSlider != null)
            {
                _indicatorReflectorBaseRadiusSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorReflectorTopRadiusSlider != null)
            {
                _indicatorReflectorTopRadiusSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorReflectorDepthSlider != null)
            {
                _indicatorReflectorDepthSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorEmitterRadiusSlider != null)
            {
                _indicatorEmitterRadiusSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorEmitterSpreadSlider != null)
            {
                _indicatorEmitterSpreadSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorEmitterDepthSlider != null)
            {
                _indicatorEmitterDepthSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorEmitterCountSlider != null)
            {
                _indicatorEmitterCountSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorRadialSegmentsSlider != null)
            {
                _indicatorRadialSegmentsSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensLatitudeSegmentsSlider != null)
            {
                _indicatorLensLatitudeSegmentsSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensLongitudeSegmentsSlider != null)
            {
                _indicatorLensLongitudeSegmentsSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorDynamicLightsEnabledCheckBox != null)
            {
                _indicatorDynamicLightsEnabledCheckBox.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightAnimationModeCombo != null)
            {
                _indicatorLightAnimationModeCombo.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightAnimationSpeedSlider != null)
            {
                _indicatorLightAnimationSpeedSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightFlickerAmountSlider != null)
            {
                _indicatorLightFlickerAmountSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightFlickerDropoutSlider != null)
            {
                _indicatorLightFlickerDropoutSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightFlickerSmoothingSlider != null)
            {
                _indicatorLightFlickerSmoothingSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightFlickerSeedSlider != null)
            {
                _indicatorLightFlickerSeedSlider.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorEmitterSourceCombo != null)
            {
                _indicatorEmitterSourceCombo.PropertyChanged += OnIndicatorLightEmitterSelectionChanged;
            }
            if (_indicatorEmitterSourceNameTextBox != null)
            {
                _indicatorEmitterSourceNameTextBox.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceEnabledCheckBox != null)
            {
                _indicatorEmitterSourceEnabledCheckBox.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourcePhaseOffsetSlider != null)
            {
                _indicatorEmitterSourcePhaseOffsetSlider.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceXSlider != null)
            {
                _indicatorEmitterSourceXSlider.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceYSlider != null)
            {
                _indicatorEmitterSourceYSlider.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceZSlider != null)
            {
                _indicatorEmitterSourceZSlider.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceIntensitySlider != null)
            {
                _indicatorEmitterSourceIntensitySlider.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceRadiusSlider != null)
            {
                _indicatorEmitterSourceRadiusSlider.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceFalloffSlider != null)
            {
                _indicatorEmitterSourceFalloffSlider.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceRSlider != null)
            {
                _indicatorEmitterSourceRSlider.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceGSlider != null)
            {
                _indicatorEmitterSourceGSlider.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceBSlider != null)
            {
                _indicatorEmitterSourceBSlider.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            _materialBaseRSlider.PropertyChanged += OnMaterialBaseColorChanged;
            _materialBaseGSlider.PropertyChanged += OnMaterialBaseColorChanged;
            _materialBaseBSlider.PropertyChanged += OnMaterialBaseColorChanged;
            _materialRegionCombo.PropertyChanged += OnMaterialRegionChanged;
            _materialMetallicSlider.PropertyChanged += OnMaterialMetallicChanged;
            _materialRoughnessSlider.PropertyChanged += OnMaterialRoughnessChanged;
            _materialPearlescenceSlider.PropertyChanged += OnMaterialPearlescenceChanged;
            _materialRustSlider.PropertyChanged += OnMaterialAgingChanged;
            _materialWearSlider.PropertyChanged += OnMaterialAgingChanged;
            _materialGunkSlider.PropertyChanged += OnMaterialAgingChanged;
            _materialNormalMapStrengthSlider.PropertyChanged += OnMaterialNormalMapStrengthChanged;
            _materialBrushStrengthSlider.PropertyChanged += OnMaterialSurfaceCharacterChanged;
            _materialBrushDensitySlider.PropertyChanged += OnMaterialSurfaceCharacterChanged;
            _materialCharacterSlider.PropertyChanged += OnMaterialSurfaceCharacterChanged;
            _spiralNormalInfluenceCheckBox.PropertyChanged += OnMicroDetailSettingsChanged;
            _basisDebugModeCombo.PropertyChanged += OnMicroDetailSettingsChanged;
            _microLodFadeStartSlider.PropertyChanged += OnMicroDetailSettingsChanged;
            _microLodFadeEndSlider.PropertyChanged += OnMicroDetailSettingsChanged;
            _microRoughnessLodBoostSlider.PropertyChanged += OnMicroDetailSettingsChanged;
            _envIntensitySlider.PropertyChanged += OnEnvironmentChanged;
            _envRoughnessMixSlider.PropertyChanged += OnEnvironmentChanged;
            _envTopRSlider.PropertyChanged += OnEnvironmentChanged;
            _envTopGSlider.PropertyChanged += OnEnvironmentChanged;
            _envTopBSlider.PropertyChanged += OnEnvironmentChanged;
            _envBottomRSlider.PropertyChanged += OnEnvironmentChanged;
            _envBottomGSlider.PropertyChanged += OnEnvironmentChanged;
            _envBottomBSlider.PropertyChanged += OnEnvironmentChanged;
            if (_envTonemapCombo != null)
            {
                _envTonemapCombo.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envExposureSlider != null)
            {
                _envExposureSlider.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envBloomStrengthSlider != null)
            {
                _envBloomStrengthSlider.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envBloomThresholdSlider != null)
            {
                _envBloomThresholdSlider.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envBloomKneeSlider != null)
            {
                _envBloomKneeSlider.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envHdriBlendSlider != null)
            {
                _envHdriBlendSlider.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envHdriRotationSlider != null)
            {
                _envHdriRotationSlider.PropertyChanged += OnEnvironmentChanged;
            }
            _shadowEnabledCheckBox.PropertyChanged += OnShadowSettingsChanged;
            _shadowSourceModeCombo.PropertyChanged += OnShadowSettingsChanged;
            _shadowStrengthSlider.PropertyChanged += OnShadowSettingsChanged;
            _shadowSoftnessSlider.PropertyChanged += OnShadowSettingsChanged;
            _shadowDistanceSlider.PropertyChanged += OnShadowSettingsChanged;
            _shadowScaleSlider.PropertyChanged += OnShadowSettingsChanged;
            _shadowQualitySlider.PropertyChanged += OnShadowSettingsChanged;
            _shadowGraySlider.PropertyChanged += OnShadowSettingsChanged;
            _shadowDiffuseInfluenceSlider.PropertyChanged += OnShadowSettingsChanged;
            _brushPaintEnabledCheckBox.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushPaintChannelCombo.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushTypeCombo.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushPaintColorPicker.PropertyChanged += OnPaintBrushSettingsChanged;
            if (_paintChannelTargetValueSlider != null)
            {
                _paintChannelTargetValueSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            }
            _scratchAbrasionTypeCombo.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushSizeSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushOpacitySlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushDarknessSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushSpreadSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _paintCoatMetallicSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _paintCoatRoughnessSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _clearCoatAmountSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _clearCoatRoughnessSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _anisotropyAngleSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchWidthSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchDepthSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchResistanceSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchDepthRampSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchExposeColorRSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchExposeColorGSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchExposeColorBSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchExposeMetallicSlider.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchExposeRoughnessSlider.PropertyChanged += OnPaintBrushSettingsChanged;
        }

        private void WireOpenedHandlers()
        {
            Opened += (_, __) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    RefreshSceneTree();
                    RefreshInspectorFromProject(InspectorRefreshTabPolicy.FollowSceneSelection);
                }, DispatcherPriority.Loaded);
            };
            Closed += (_, __) =>
            {
                DisposeCollarLibraryHotReload();
                if (_metalViewport != null)
                {
                    _metalViewport.RuntimeDiagnosticsUpdated -= OnViewportRuntimeDiagnosticsUpdated;
                }
            };
        }

        private static TextBlock? FindFirstTextBlock(Visual? root)
        {
            if (root is TextBlock tb)
            {
                return tb;
            }

            if (root == null)
            {
                return null;
            }

            foreach (var child in root.GetVisualChildren())
            {
                var found = FindFirstTextBlock(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static ContentPresenter? FindFirstContentPresenter(Visual? root)
        {
            if (root is ContentPresenter presenter)
            {
                return presenter;
            }

            if (root == null)
            {
                return null;
            }

            foreach (var child in root.GetVisualChildren())
            {
                var found = FindFirstContentPresenter(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void InvalidateSceneList(string phase)
        {
            _sceneListBox?.InvalidateVisual();
            _sceneListBox?.InvalidateMeasure();
            _sceneListBox?.InvalidateArrange();
            (_sceneListBox?.ContainerFromIndex(0) as Control)?.InvalidateVisual();
            _ = phase;
        }

        private void DumpSceneListVisualState(string prefix)
        {
            var sceneList = _sceneListBox;
            var firstContainer = sceneList?.ContainerFromIndex(0) as ListBoxItem;
            var firstTextBlock = FindFirstTextBlock(firstContainer);
            var presenter = FindFirstContentPresenter(firstContainer);

            string sceneThemeVariant = GetPropertyString(sceneList, "ActualThemeVariant");
            string sceneBackground = GetPropertyString(sceneList, "Background");
            string sceneForeground = GetPropertyString(sceneList, "Foreground");
            string itemBackground = GetPropertyString(firstContainer, "Background");
            string itemForeground = GetPropertyString(firstContainer, "Foreground");
            string itemPseudoClasses = GetPropertyString(firstContainer, "PseudoClasses");
            string tbOpacityMask = GetPropertyString(firstTextBlock, "OpacityMask");
            _ = prefix;
            _ = sceneThemeVariant;
            _ = sceneBackground;
            _ = sceneForeground;
            _ = itemBackground;
            _ = itemForeground;
            _ = itemPseudoClasses;
            _ = tbOpacityMask;
            _ = sceneList;
            _ = firstContainer;
            _ = firstTextBlock;
            _ = presenter;
        }

        private static string GetPropertyString(object? target, string propertyName)
        {
            if (target == null)
            {
                return "<null>";
            }

            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                return "<unavailable>";
            }

            var value = property.GetValue(target);
            return value?.ToString() ?? "<null>";
        }

        private void DetachLightListHandler()
        {
            _lightListBox!.SelectionChanged -= OnLightListSelectionChanged;
        }

        private void AttachLightListHandler()
        {
            _lightListBox!.SelectionChanged += OnLightListSelectionChanged;
        }

        private void OnLightListSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_updatingUi || _lightListBox == null)
            {
                return;
            }

            if (_project.SetSelectedLightIndex(_lightListBox.SelectedIndex))
            {
                NotifyProjectStateChanged();
            }
        }

        private void ViewportOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_metalViewport == null || _viewportOverlay == null)
            {
                return;
            }

            _viewportOverlay.Focus();
            _metalViewport.HandlePointerPressedFromOverlay(e, _viewportOverlay);
            e.Handled = true;
        }

        private void ViewportOverlay_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_metalViewport == null || _viewportOverlay == null)
            {
                return;
            }

            _metalViewport.HandlePointerMovedFromOverlay(e, _viewportOverlay);
            e.Handled = true;
        }

        private void ViewportOverlay_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_metalViewport == null || _viewportOverlay == null)
            {
                return;
            }

            _metalViewport.HandlePointerReleasedFromOverlay(e, _viewportOverlay);
            e.Handled = true;
        }

        private void ViewportOverlay_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_metalViewport == null || _viewportOverlay == null)
            {
                return;
            }

            _metalViewport.HandlePointerWheelFromOverlay(e, _viewportOverlay);
            e.Handled = true;
        }

        private void ViewportOverlay_KeyDown(object? sender, KeyEventArgs e)
        {
            _metalViewport?.HandleKeyDownFromOverlay(e);
        }

        private void ViewportOverlay_KeyUp(object? sender, KeyEventArgs e)
        {
            _metalViewport?.HandleKeyUpFromOverlay(e);
        }
    }
}

#pragma warning restore CS8602
