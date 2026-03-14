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
                _rotationInput == null || _lightTypeCombo == null || _lightXInput == null ||
                _lightYInput == null || _lightZInput == null || _directionInput == null ||
                _intensityInput == null || _falloffInput == null || _lightRInput == null ||
                _lightGInput == null || _lightBInput == null || _diffuseBoostInput == null ||
                _specularBoostInput == null || _specularPowerInput == null ||
                _modelRadiusInput == null || _modelHeightInput == null || _modelTopScaleInput == null ||
                _modelBevelInput == null || _referenceStyleCombo == null || _referenceStyleSaveNameTextBox == null || _saveReferenceProfileButton == null || _bodyStyleCombo == null || _bevelCurveInput == null || _crownProfileInput == null ||
                _bodyTaperInput == null || _bodyBulgeInput == null || _modelSegmentsInput == null ||
                _spiralRidgeHeightInput == null || _spiralRidgeWidthInput == null || _spiralTurnsInput == null ||
                _gripStyleCombo == null || _gripTypeCombo == null || _gripStartInput == null || _gripHeightInput == null ||
                _gripDensityInput == null || _gripPitchInput == null || _gripDepthInput == null ||
                _gripWidthInput == null || _gripSharpnessInput == null ||
                _collarEnabledCheckBox == null || _collarPresetCombo == null || _collarMeshPathTextBox == null ||
                _collarScaleInput == null || _collarBodyLengthInput == null || _collarBodyThicknessInput == null ||
                _collarHeadLengthInput == null || _collarHeadThicknessInput == null ||
                _collarRotateInput == null || _collarMirrorXCheckBox == null || _collarMirrorYCheckBox == null || _collarMirrorZCheckBox == null ||
                _collarOffsetXInput == null || _collarOffsetYInput == null || _collarElevationInput == null || _collarInflateInput == null ||
                _collarMaterialBaseRInput == null || _collarMaterialBaseGInput == null || _collarMaterialBaseBInput == null ||
                _collarMaterialMetallicInput == null || _collarMaterialRoughnessInput == null || _collarMaterialPearlescenceInput == null ||
                _collarMaterialRustInput == null || _collarMaterialWearInput == null || _collarMaterialGunkInput == null ||
                _indicatorEnabledCheckBox == null || _indicatorShapeCombo == null || _indicatorReliefCombo == null ||
                _indicatorProfileCombo == null || _indicatorWidthInput == null || _indicatorLengthInput == null ||
                _indicatorPositionInput == null ||
                _indicatorThicknessInput == null || _indicatorRoundnessInput == null || _indicatorColorBlendInput == null ||
                _indicatorColorRInput == null || _indicatorColorGInput == null || _indicatorColorBInput == null ||
                _materialBaseRInput == null || _materialBaseGInput == null || _materialBaseBInput == null ||
                _materialListPanel == null || _materialListBox == null || _materialNameTextBox == null || _materialRegionPanel == null || _materialRegionCombo == null ||
                _assemblyMaterialPresetPanel == null || _assemblyMaterialPresetCombo == null || _assemblyMaterialPresetHintText == null || _materialManualControlsPanel == null ||
                _materialMetallicInput == null || _materialRoughnessInput == null || _materialPearlescenceInput == null ||
                _materialRustInput == null || _materialWearInput == null || _materialGunkInput == null ||
                _materialAlbedoMapBrowseButton == null || _materialAlbedoMapClearButton == null ||
                _materialNormalMapBrowseButton == null || _materialNormalMapClearButton == null ||
                _materialRoughnessMapBrowseButton == null || _materialRoughnessMapClearButton == null ||
                _materialMetallicMapBrowseButton == null || _materialMetallicMapClearButton == null ||
                _materialNormalMapStrengthInput == null || _materialNormalMapStrengthPanel == null ||
                _materialAlbedoMapPathText == null || _materialNormalMapPathText == null ||
                _materialRoughnessMapPathText == null || _materialMetallicMapPathText == null ||
                _materialBrushStrengthInput == null || _materialBrushDensityInput == null || _materialCharacterInput == null ||
                _spiralNormalInfluenceCheckBox == null || _basisDebugModeCombo == null || _microLodFadeStartInput == null || _microLodFadeEndInput == null || _microRoughnessLodBoostInput == null ||
                _envIntensityInput == null || _envRoughnessMixInput == null ||
                _envTopRInput == null || _envTopGInput == null || _envTopBInput == null ||
                _envBottomRInput == null || _envBottomGInput == null || _envBottomBInput == null ||
                _envPresetCombo == null || _envBloomKernelShapeCombo == null || _environmentManualSettingsPanel == null ||
                _shadowEnabledCheckBox == null || _shadowSourceModeCombo == null || _shadowStrengthInput == null || _shadowSoftnessInput == null ||
                _shadowDistanceInput == null || _shadowScaleInput == null || _shadowQualityInput == null ||
                _shadowGrayInput == null || _shadowDiffuseInfluenceInput == null ||
                _brushPaintEnabledCheckBox == null || _brushPaintChannelCombo == null || _brushTypeCombo == null || _brushPaintColorPicker == null || _scratchAbrasionTypeCombo == null ||
                _brushSizeInput == null || _brushOpacityInput == null || _brushDarknessInput == null || _brushSpreadInput == null ||
                _paintCoatMetallicInput == null || _paintCoatRoughnessInput == null ||
                _clearCoatAmountInput == null || _clearCoatRoughnessInput == null || _anisotropyAngleInput == null ||
                _scratchWidthInput == null || _scratchDepthInput == null || _scratchResistanceInput == null || _scratchDepthRampInput == null ||
                _scratchExposeColorRInput == null || _scratchExposeColorGInput == null || _scratchExposeColorBInput == null ||
                _scratchExposeMetallicInput == null || _scratchExposeRoughnessInput == null ||
                _clearPaintMaskButton == null ||
                _renderButton == null || _centerLightButton == null)
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
            if (_sliderThumbProfileCombo != null)
            {
                _sliderThumbProfileCombo.ItemsSource = Enum.GetValues<SliderThumbProfile>().Cast<SliderThumbProfile>().ToList();
            }
            if (_sliderTrackStyleCombo != null)
            {
                _sliderTrackStyleCombo.ItemsSource = Enum.GetValues<SliderTrackStyle>().Cast<SliderTrackStyle>().ToList();
            }
            if (_pushButtonCapProfileCombo != null)
            {
                _pushButtonCapProfileCombo.ItemsSource = Enum.GetValues<PushButtonCapProfile>().Cast<PushButtonCapProfile>().ToList();
            }
            if (_pushButtonBezelProfileCombo != null)
            {
                _pushButtonBezelProfileCombo.ItemsSource = Enum.GetValues<PushButtonBezelProfile>().Cast<PushButtonBezelProfile>().ToList();
            }
            if (_pushButtonSkirtStyleCombo != null)
            {
                _pushButtonSkirtStyleCombo.ItemsSource = Enum.GetValues<PushButtonSkirtStyle>().Cast<PushButtonSkirtStyle>().ToList();
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
            RebuildPushButtonMeshOptions();
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
            _materialRegionCombo.ItemsSource = _materialRegionOptions;
            RebuildMaterialRegionOptions(_project.ProjectType);
            SelectMaterialRegionOption(MaterialRegionTarget.WholeKnob);
            _assemblyMaterialPresetCombo.ItemsSource = _assemblyMaterialPresetOptions;
            _materialListBox.ItemsSource = _materialItems;
            _basisDebugModeCombo.ItemsSource = Enum.GetValues<BasisDebugMode>().Cast<BasisDebugMode>().ToList();
            if (_envTonemapCombo != null)
            {
                _envTonemapCombo.ItemsSource = Enum.GetValues<TonemapOperator>().Cast<TonemapOperator>().ToList();
            }
            RebuildEnvironmentPresetOptions();
            if (_envPresetCombo != null)
            {
                _envPresetCombo.ItemsSource = _environmentPresetOptions;
            }
            RebuildBloomKernelShapeOptions();
            if (_envBloomKernelShapeCombo != null)
            {
                _envBloomKernelShapeCombo.ItemsSource = _bloomKernelShapeOptions;
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
            _materialListBox.SelectionChanged += OnMaterialListSelectionChanged;
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
            if (_refreshPushButtonLibraryButton != null)
            {
                _refreshPushButtonLibraryButton.Click += OnRefreshPushButtonLibraryButtonClicked;
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
            WireBrushDockDrag();
        }

        private void WireControlPropertyHandlers()
        {
            _lightingModeCombo.PropertyChanged += OnLightingModeChanged;
            _lightTypeCombo.PropertyChanged += OnLightTypeChanged;
            _rotationInput.PropertyChanged += OnRotationChanged;
            _lightXInput.PropertyChanged += OnLightXChanged;
            _lightYInput.PropertyChanged += OnLightYChanged;
            _lightZInput.PropertyChanged += OnLightZChanged;
            _directionInput.PropertyChanged += OnDirectionChanged;
            _intensityInput.PropertyChanged += OnIntensityChanged;
            _falloffInput.PropertyChanged += OnFalloffChanged;
            _lightRInput.PropertyChanged += OnColorChanged;
            _lightGInput.PropertyChanged += OnColorChanged;
            _lightBInput.PropertyChanged += OnColorChanged;
            _diffuseBoostInput.PropertyChanged += OnDiffuseBoostChanged;
            _specularBoostInput.PropertyChanged += OnSpecularBoostChanged;
            _specularPowerInput.PropertyChanged += OnSpecularPowerChanged;
            _modelRadiusInput.PropertyChanged += OnModelRadiusChanged;
            _modelHeightInput.PropertyChanged += OnModelHeightChanged;
            _modelTopScaleInput.PropertyChanged += OnModelTopScaleChanged;
            _modelBevelInput.PropertyChanged += OnModelBevelChanged;
            _referenceStyleCombo.PropertyChanged += OnReferenceStyleChanged;
            _bodyStyleCombo.PropertyChanged += OnBodyStyleChanged;
            _bevelCurveInput.PropertyChanged += OnBodyDesignChanged;
            _crownProfileInput.PropertyChanged += OnBodyDesignChanged;
            _bodyTaperInput.PropertyChanged += OnBodyDesignChanged;
            _bodyBulgeInput.PropertyChanged += OnBodyDesignChanged;
            _modelSegmentsInput.PropertyChanged += OnModelSegmentsChanged;
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

            if (_sliderBackplateWidthInput != null)
            {
                _sliderBackplateWidthInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderBackplateHeightInput != null)
            {
                _sliderBackplateHeightInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderBackplateThicknessInput != null)
            {
                _sliderBackplateThicknessInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderThumbWidthInput != null)
            {
                _sliderThumbWidthInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderThumbHeightInput != null)
            {
                _sliderThumbHeightInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }

            if (_sliderThumbDepthInput != null)
            {
                _sliderThumbDepthInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_sliderThumbProfileCombo != null)
            {
                _sliderThumbProfileCombo.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_sliderTrackStyleCombo != null)
            {
                _sliderTrackStyleCombo.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_sliderTrackWidthInput != null)
            {
                _sliderTrackWidthInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_sliderTrackDepthInput != null)
            {
                _sliderTrackDepthInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_sliderRailHeightInput != null)
            {
                _sliderRailHeightInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_sliderRailSpacingInput != null)
            {
                _sliderRailSpacingInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_sliderThumbRidgeCountInput != null)
            {
                _sliderThumbRidgeCountInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_sliderThumbRidgeDepthInput != null)
            {
                _sliderThumbRidgeDepthInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_sliderThumbCornerRadiusInput != null)
            {
                _sliderThumbCornerRadiusInput.PropertyChanged += OnSliderAssemblySettingsChanged;
            }
            if (_pushButtonPressAmountInput != null)
            {
                _pushButtonPressAmountInput.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonBaseMeshCombo != null)
            {
                _pushButtonBaseMeshCombo.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonCapMeshCombo != null)
            {
                _pushButtonCapMeshCombo.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonCapProfileCombo != null)
            {
                _pushButtonCapProfileCombo.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonBezelProfileCombo != null)
            {
                _pushButtonBezelProfileCombo.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonSkirtStyleCombo != null)
            {
                _pushButtonSkirtStyleCombo.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonBezelChamferSizeInput != null)
            {
                _pushButtonBezelChamferSizeInput.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonCapOverhangInput != null)
            {
                _pushButtonCapOverhangInput.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonCapSegmentsInput != null)
            {
                _pushButtonCapSegmentsInput.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonBezelSegmentsInput != null)
            {
                _pushButtonBezelSegmentsInput.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonSkirtHeightInput != null)
            {
                _pushButtonSkirtHeightInput.PropertyChanged += OnPushButtonAssemblySettingsChanged;
            }
            if (_pushButtonSkirtRadiusInput != null)
            {
                _pushButtonSkirtRadiusInput.PropertyChanged += OnPushButtonAssemblySettingsChanged;
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
            if (_toggleStateIndexInput != null)
            {
                _toggleStateIndexInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleMaxAngleInput != null)
            {
                _toggleMaxAngleInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePlateWidthInput != null)
            {
                _togglePlateWidthInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePlateHeightInput != null)
            {
                _togglePlateHeightInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePlateThicknessInput != null)
            {
                _togglePlateThicknessInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePlateOffsetYInput != null)
            {
                _togglePlateOffsetYInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePlateOffsetZInput != null)
            {
                _togglePlateOffsetZInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleBushingRadiusInput != null)
            {
                _toggleBushingRadiusInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleBushingHeightInput != null)
            {
                _toggleBushingHeightInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleBushingSidesInput != null)
            {
                _toggleBushingSidesInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLowerBushingShapeCombo != null)
            {
                _toggleLowerBushingShapeCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingShapeCombo != null)
            {
                _toggleUpperBushingShapeCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLowerBushingRadiusScaleInput != null)
            {
                _toggleLowerBushingRadiusScaleInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLowerBushingHeightRatioInput != null)
            {
                _toggleLowerBushingHeightRatioInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingRadiusScaleInput != null)
            {
                _toggleUpperBushingRadiusScaleInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingHeightRatioInput != null)
            {
                _toggleUpperBushingHeightRatioInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingKnurlAmountInput != null)
            {
                _toggleUpperBushingKnurlAmountInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingKnurlDensityInput != null)
            {
                _toggleUpperBushingKnurlDensityInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingKnurlDepthInput != null)
            {
                _toggleUpperBushingKnurlDepthInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingAnisotropyStrengthInput != null)
            {
                _toggleUpperBushingAnisotropyStrengthInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingAnisotropyDensityInput != null)
            {
                _toggleUpperBushingAnisotropyDensityInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingAnisotropyAngleInput != null)
            {
                _toggleUpperBushingAnisotropyAngleInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleUpperBushingSurfaceCharacterInput != null)
            {
                _toggleUpperBushingSurfaceCharacterInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePivotHousingRadiusInput != null)
            {
                _togglePivotHousingRadiusInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePivotHousingDepthInput != null)
            {
                _togglePivotHousingDepthInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePivotHousingBevelInput != null)
            {
                _togglePivotHousingBevelInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePivotBallRadiusInput != null)
            {
                _togglePivotBallRadiusInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_togglePivotClearanceInput != null)
            {
                _togglePivotClearanceInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleInvertBaseWindingCheckBox != null)
            {
                _toggleInvertBaseWindingCheckBox.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleInvertLeverWindingCheckBox != null)
            {
                _toggleInvertLeverWindingCheckBox.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverLengthInput != null)
            {
                _toggleLeverLengthInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverRadiusInput != null)
            {
                _toggleLeverRadiusInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverTopRadiusInput != null)
            {
                _toggleLeverTopRadiusInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverSidesInput != null)
            {
                _toggleLeverSidesInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleLeverPivotOffsetInput != null)
            {
                _toggleLeverPivotOffsetInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipRadiusInput != null)
            {
                _toggleTipRadiusInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipLatitudeSegmentsInput != null)
            {
                _toggleTipLatitudeSegmentsInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipLongitudeSegmentsInput != null)
            {
                _toggleTipLongitudeSegmentsInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveEnabledCheckBox != null)
            {
                _toggleTipSleeveEnabledCheckBox.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveLengthInput != null)
            {
                _toggleTipSleeveLengthInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveThicknessInput != null)
            {
                _toggleTipSleeveThicknessInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveOuterRadiusInput != null)
            {
                _toggleTipSleeveOuterRadiusInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveCoverageInput != null)
            {
                _toggleTipSleeveCoverageInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveSidesInput != null)
            {
                _toggleTipSleeveSidesInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveStyleCombo != null)
            {
                _toggleTipSleeveStyleCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveTipStyleCombo != null)
            {
                _toggleTipSleeveTipStyleCombo.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleevePatternCountInput != null)
            {
                _toggleTipSleevePatternCountInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleevePatternDepthInput != null)
            {
                _toggleTipSleevePatternDepthInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveTipAmountInput != null)
            {
                _toggleTipSleeveTipAmountInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveColorRInput != null)
            {
                _toggleTipSleeveColorRInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveColorGInput != null)
            {
                _toggleTipSleeveColorGInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveColorBInput != null)
            {
                _toggleTipSleeveColorBInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveMetallicInput != null)
            {
                _toggleTipSleeveMetallicInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveRoughnessInput != null)
            {
                _toggleTipSleeveRoughnessInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleevePearlescenceInput != null)
            {
                _toggleTipSleevePearlescenceInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveDiffuseStrengthInput != null)
            {
                _toggleTipSleeveDiffuseStrengthInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveSpecularStrengthInput != null)
            {
                _toggleTipSleeveSpecularStrengthInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveRustInput != null)
            {
                _toggleTipSleeveRustInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveWearInput != null)
            {
                _toggleTipSleeveWearInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            if (_toggleTipSleeveGunkInput != null)
            {
                _toggleTipSleeveGunkInput.PropertyChanged += OnToggleAssemblySettingsChanged;
            }
            _spiralRidgeHeightInput.PropertyChanged += OnSpiralGeometryChanged;
            _spiralRidgeWidthInput.PropertyChanged += OnSpiralGeometryChanged;
            _spiralTurnsInput.PropertyChanged += OnSpiralGeometryChanged;
            _gripStyleCombo.PropertyChanged += OnGripStyleChanged;
            _gripTypeCombo.PropertyChanged += OnGripSettingsChanged;
            _gripStartInput.PropertyChanged += OnGripSettingsChanged;
            _gripHeightInput.PropertyChanged += OnGripSettingsChanged;
            _gripDensityInput.PropertyChanged += OnGripSettingsChanged;
            _gripPitchInput.PropertyChanged += OnGripSettingsChanged;
            _gripDepthInput.PropertyChanged += OnGripSettingsChanged;
            _gripWidthInput.PropertyChanged += OnGripSettingsChanged;
            _gripSharpnessInput.PropertyChanged += OnGripSettingsChanged;
            _collarEnabledCheckBox.PropertyChanged += OnCollarSettingsChanged;
            _collarPresetCombo.PropertyChanged += OnCollarSettingsChanged;
            _collarMeshPathTextBox.PropertyChanged += OnCollarSettingsChanged;
            _collarScaleInput.PropertyChanged += OnCollarSettingsChanged;
            _collarBodyLengthInput.PropertyChanged += OnCollarSettingsChanged;
            _collarBodyThicknessInput.PropertyChanged += OnCollarSettingsChanged;
            _collarHeadLengthInput.PropertyChanged += OnCollarSettingsChanged;
            _collarHeadThicknessInput.PropertyChanged += OnCollarSettingsChanged;
            _collarRotateInput.PropertyChanged += OnCollarSettingsChanged;
            _collarMirrorXCheckBox.PropertyChanged += OnCollarSettingsChanged;
            _collarMirrorYCheckBox.PropertyChanged += OnCollarSettingsChanged;
            _collarMirrorZCheckBox.PropertyChanged += OnCollarSettingsChanged;
            _collarOffsetXInput.PropertyChanged += OnCollarSettingsChanged;
            _collarOffsetYInput.PropertyChanged += OnCollarSettingsChanged;
            _collarElevationInput.PropertyChanged += OnCollarSettingsChanged;
            _collarInflateInput.PropertyChanged += OnCollarSettingsChanged;
            _collarMaterialBaseRInput.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialBaseGInput.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialBaseBInput.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialMetallicInput.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialRoughnessInput.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialPearlescenceInput.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialRustInput.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialWearInput.PropertyChanged += OnCollarMaterialChanged;
            _collarMaterialGunkInput.PropertyChanged += OnCollarMaterialChanged;
            _indicatorEnabledCheckBox.PropertyChanged += OnIndicatorSettingsChanged;
            if (_indicatorCadWallsCheckBox != null)
            {
                _indicatorCadWallsCheckBox.PropertyChanged += OnIndicatorSettingsChanged;
            }
            _indicatorShapeCombo.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorReliefCombo.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorProfileCombo.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorWidthInput.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorLengthInput.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorPositionInput.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorThicknessInput.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorRoundnessInput.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorColorBlendInput.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorColorRInput.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorColorGInput.PropertyChanged += OnIndicatorSettingsChanged;
            _indicatorColorBInput.PropertyChanged += OnIndicatorSettingsChanged;
            if (_indicatorAssemblyEnabledCheckBox != null)
            {
                _indicatorAssemblyEnabledCheckBox.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorQuickLightOnCheckBox != null)
            {
                _indicatorQuickLightOnCheckBox.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorQuickBrightnessInput != null)
            {
                _indicatorQuickBrightnessInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorQuickGlowInput != null)
            {
                _indicatorQuickGlowInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorBaseWidthInput != null)
            {
                _indicatorBaseWidthInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorBaseHeightInput != null)
            {
                _indicatorBaseHeightInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorBaseThicknessInput != null)
            {
                _indicatorBaseThicknessInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorHousingRadiusInput != null)
            {
                _indicatorHousingRadiusInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorHousingHeightInput != null)
            {
                _indicatorHousingHeightInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensRadiusInput != null)
            {
                _indicatorLensRadiusInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensHeightInput != null)
            {
                _indicatorLensHeightInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensTransmissionInput != null)
            {
                _indicatorLensTransmissionInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensIorInput != null)
            {
                _indicatorLensIorInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensThicknessInput != null)
            {
                _indicatorLensThicknessInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensAbsorptionInput != null)
            {
                _indicatorLensAbsorptionInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensSurfaceRoughnessInput != null)
            {
                _indicatorLensSurfaceRoughnessInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensSurfaceSpecularInput != null)
            {
                _indicatorLensSurfaceSpecularInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensTintRInput != null)
            {
                _indicatorLensTintRInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensTintGInput != null)
            {
                _indicatorLensTintGInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensTintBInput != null)
            {
                _indicatorLensTintBInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorReflectorBaseRadiusInput != null)
            {
                _indicatorReflectorBaseRadiusInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorReflectorTopRadiusInput != null)
            {
                _indicatorReflectorTopRadiusInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorReflectorDepthInput != null)
            {
                _indicatorReflectorDepthInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorEmitterRadiusInput != null)
            {
                _indicatorEmitterRadiusInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorEmitterSpreadInput != null)
            {
                _indicatorEmitterSpreadInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorEmitterDepthInput != null)
            {
                _indicatorEmitterDepthInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorEmitterCountInput != null)
            {
                _indicatorEmitterCountInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorRadialSegmentsInput != null)
            {
                _indicatorRadialSegmentsInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensLatitudeSegmentsInput != null)
            {
                _indicatorLensLatitudeSegmentsInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLensLongitudeSegmentsInput != null)
            {
                _indicatorLensLongitudeSegmentsInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorDynamicLightsEnabledCheckBox != null)
            {
                _indicatorDynamicLightsEnabledCheckBox.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightAnimationModeCombo != null)
            {
                _indicatorLightAnimationModeCombo.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightAnimationSpeedInput != null)
            {
                _indicatorLightAnimationSpeedInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightFlickerAmountInput != null)
            {
                _indicatorLightFlickerAmountInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightFlickerDropoutInput != null)
            {
                _indicatorLightFlickerDropoutInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightFlickerSmoothingInput != null)
            {
                _indicatorLightFlickerSmoothingInput.PropertyChanged += OnIndicatorLightSettingsChanged;
            }
            if (_indicatorLightFlickerSeedInput != null)
            {
                _indicatorLightFlickerSeedInput.PropertyChanged += OnIndicatorLightSettingsChanged;
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
            if (_indicatorEmitterSourcePhaseOffsetInput != null)
            {
                _indicatorEmitterSourcePhaseOffsetInput.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceXInput != null)
            {
                _indicatorEmitterSourceXInput.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceYInput != null)
            {
                _indicatorEmitterSourceYInput.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceZInput != null)
            {
                _indicatorEmitterSourceZInput.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceIntensityInput != null)
            {
                _indicatorEmitterSourceIntensityInput.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceRadiusInput != null)
            {
                _indicatorEmitterSourceRadiusInput.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceFalloffInput != null)
            {
                _indicatorEmitterSourceFalloffInput.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceRInput != null)
            {
                _indicatorEmitterSourceRInput.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceGInput != null)
            {
                _indicatorEmitterSourceGInput.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            if (_indicatorEmitterSourceBInput != null)
            {
                _indicatorEmitterSourceBInput.PropertyChanged += OnIndicatorLightEmitterSettingsChanged;
            }
            _materialBaseRInput.PropertyChanged += OnMaterialBaseColorChanged;
            _materialBaseGInput.PropertyChanged += OnMaterialBaseColorChanged;
            _materialBaseBInput.PropertyChanged += OnMaterialBaseColorChanged;
            _materialNameTextBox.PropertyChanged += OnMaterialNameTextChanged;
            _materialRegionCombo.PropertyChanged += OnMaterialRegionChanged;
            _assemblyMaterialPresetCombo.PropertyChanged += OnAssemblyMaterialPresetChanged;
            _materialMetallicInput.PropertyChanged += OnMaterialMetallicChanged;
            _materialRoughnessInput.PropertyChanged += OnMaterialRoughnessChanged;
            _materialPearlescenceInput.PropertyChanged += OnMaterialPearlescenceChanged;
            _materialRustInput.PropertyChanged += OnMaterialAgingChanged;
            _materialWearInput.PropertyChanged += OnMaterialAgingChanged;
            _materialGunkInput.PropertyChanged += OnMaterialAgingChanged;
            _materialNormalMapStrengthInput.PropertyChanged += OnMaterialNormalMapStrengthChanged;
            _materialBrushStrengthInput.PropertyChanged += OnMaterialSurfaceCharacterChanged;
            _materialBrushDensityInput.PropertyChanged += OnMaterialSurfaceCharacterChanged;
            _materialCharacterInput.PropertyChanged += OnMaterialSurfaceCharacterChanged;
            _spiralNormalInfluenceCheckBox.PropertyChanged += OnMicroDetailSettingsChanged;
            _basisDebugModeCombo.PropertyChanged += OnMicroDetailSettingsChanged;
            _microLodFadeStartInput.PropertyChanged += OnMicroDetailSettingsChanged;
            _microLodFadeEndInput.PropertyChanged += OnMicroDetailSettingsChanged;
            _microRoughnessLodBoostInput.PropertyChanged += OnMicroDetailSettingsChanged;
            _envIntensityInput.PropertyChanged += OnEnvironmentChanged;
            _envRoughnessMixInput.PropertyChanged += OnEnvironmentChanged;
            _envTopRInput.PropertyChanged += OnEnvironmentChanged;
            _envTopGInput.PropertyChanged += OnEnvironmentChanged;
            _envTopBInput.PropertyChanged += OnEnvironmentChanged;
            _envBottomRInput.PropertyChanged += OnEnvironmentChanged;
            _envBottomGInput.PropertyChanged += OnEnvironmentChanged;
            _envBottomBInput.PropertyChanged += OnEnvironmentChanged;
            if (_envTonemapCombo != null)
            {
                _envTonemapCombo.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envPresetCombo != null)
            {
                _envPresetCombo.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envExposureInput != null)
            {
                _envExposureInput.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envBloomStrengthInput != null)
            {
                _envBloomStrengthInput.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envBloomThresholdInput != null)
            {
                _envBloomThresholdInput.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envBloomKneeInput != null)
            {
                _envBloomKneeInput.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envBloomKernelShapeCombo != null)
            {
                _envBloomKernelShapeCombo.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envHdriBlendInput != null)
            {
                _envHdriBlendInput.PropertyChanged += OnEnvironmentChanged;
            }
            if (_envHdriRotationInput != null)
            {
                _envHdriRotationInput.PropertyChanged += OnEnvironmentChanged;
            }
            _shadowEnabledCheckBox.PropertyChanged += OnShadowSettingsChanged;
            _shadowSourceModeCombo.PropertyChanged += OnShadowSettingsChanged;
            _shadowStrengthInput.PropertyChanged += OnShadowSettingsChanged;
            _shadowSoftnessInput.PropertyChanged += OnShadowSettingsChanged;
            _shadowDistanceInput.PropertyChanged += OnShadowSettingsChanged;
            _shadowScaleInput.PropertyChanged += OnShadowSettingsChanged;
            _shadowQualityInput.PropertyChanged += OnShadowSettingsChanged;
            _shadowGrayInput.PropertyChanged += OnShadowSettingsChanged;
            _shadowDiffuseInfluenceInput.PropertyChanged += OnShadowSettingsChanged;
            _brushPaintEnabledCheckBox.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushPaintChannelCombo.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushTypeCombo.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushPaintColorPicker.PropertyChanged += OnPaintBrushSettingsChanged;
            if (_paintChannelTargetValueInput != null)
            {
                _paintChannelTargetValueInput.PropertyChanged += OnPaintBrushSettingsChanged;
            }
            _scratchAbrasionTypeCombo.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushSizeInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushOpacityInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushDarknessInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _brushSpreadInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _paintCoatMetallicInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _paintCoatRoughnessInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _clearCoatAmountInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _clearCoatRoughnessInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _anisotropyAngleInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchWidthInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchDepthInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchResistanceInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchDepthRampInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchExposeColorRInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchExposeColorGInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchExposeColorBInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchExposeMetallicInput.PropertyChanged += OnPaintBrushSettingsChanged;
            _scratchExposeRoughnessInput.PropertyChanged += OnPaintBrushSettingsChanged;
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
            if (e.Key == Key.B &&
                !e.KeyModifiers.HasFlag(KeyModifiers.Meta) &&
                !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ToggleBrushDockVisibility();
                e.Handled = true;
                return;
            }

            _metalViewport?.HandleKeyDownFromOverlay(e);
        }

        private void ViewportOverlay_KeyUp(object? sender, KeyEventArgs e)
        {
            _metalViewport?.HandleKeyUpFromOverlay(e);
        }
    }
}

#pragma warning restore CS8602
