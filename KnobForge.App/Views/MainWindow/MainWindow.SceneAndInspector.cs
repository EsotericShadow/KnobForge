using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KnobForge.App.Controls;
using KnobForge.Core;
using KnobForge.Core.Scene;
using KnobForge.Rendering;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace KnobForge.App.Views
{
        public partial class MainWindow : Window
        {
        private async void OnRenderButtonClick(object? sender, RoutedEventArgs e)
        {
            if (_metalViewport == null)
            {
                return;
            }

            var dialog = new RenderSettingsWindow(_project, _metalViewport.CurrentOrientation, _metalViewport.CurrentCameraState, _metalViewport, _currentProjectFilePath)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            await dialog.ShowDialog(this);
        }

        private void AddLight()
        {
            float offset = 120f * _project.Lights.Count;
            _project.AddLight(offset, offset * 0.25f, 0f);
            NotifyProjectStateChanged();
        }

        private void RemoveSelectedLight()
        {
            if (_project.RemoveSelectedLight())
            {
                NotifyProjectStateChanged();
            }
        }

        private void CenterLight()
        {
            _project.EnsureSelection();
            KnobLight? light = _project.SelectedLight;
            if (light == null)
            {
                return;
            }

            light.X = 0f;
            light.Y = 0f;
            light.Z = 0f;
            NotifyProjectStateChanged();
        }

        private void NotifyProjectStateChanged(
            InspectorRefreshTabPolicy tabPolicy = InspectorRefreshTabPolicy.PreserveCurrentTab,
            bool syncSelectionFromInspectorContext = true)
        {
            _metalViewport?.InvalidateGpu();
            if (syncSelectionFromInspectorContext)
            {
                TryAdoptSceneSelectionFromInspectorContext();
            }

            RefreshSceneTree();
            RefreshInspectorFromProject(tabPolicy);
            CaptureUndoSnapshotIfChanged();
        }

        private void NotifyRenderOnly(bool syncSelectionFromInspectorContext = true)
        {
            _metalViewport?.InvalidateGpu();
            if (syncSelectionFromInspectorContext && TryAdoptSceneSelectionFromInspectorContext())
            {
                RefreshSceneTree();
            }

            UpdateReadouts();
            CaptureUndoSnapshotIfChanged();
        }

        private void RefreshSceneTree()
        {
            if (_sceneListBox == null)
            {
                return;
            }

            if (_sceneListBox.Bounds.Height <= 0)
            {
                if (!_sceneRefreshDeferredPending)
                {
                    _sceneRefreshDeferredPending = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        _sceneRefreshDeferredPending = false;
                        RefreshSceneTree();
                    }, DispatcherPriority.Loaded);

                    Dispatcher.UIThread.Post(() =>
                    {
                        _sceneRefreshDeferredPending = false;
                        RefreshSceneTree();
                    }, DispatcherPriority.Render);
                }

                return;
            }

            WithUiRefreshSuppressed(() =>
            {
                var flatList = new List<SceneNode>();

                void Traverse(SceneNode node)
                {
                    flatList.Add(node);
                    foreach (var child in node.Children)
                    {
                        Traverse(child);
                    }
                }

                Traverse(_project.SceneRoot);

                if (_sceneNodes.Count == flatList.Count)
                {
                    bool identical = true;
                    for (int i = 0; i < flatList.Count; i++)
                    {
                        if (_sceneNodes[i].Id != flatList[i].Id)
                        {
                            identical = false;
                            break;
                        }
                    }

                    if (identical)
                    {
                        SyncSceneListSelectionToProjectNode();
                        return;
                    }
                }

                int sharedCount = Math.Min(_sceneNodes.Count, flatList.Count);
                for (int i = 0; i < sharedCount; i++)
                {
                    if (_sceneNodes[i].Id != flatList[i].Id)
                    {
                        _sceneNodes[i] = flatList[i];
                    }
                }

                if (_sceneNodes.Count > flatList.Count)
                {
                    for (int i = _sceneNodes.Count - 1; i >= flatList.Count; i--)
                    {
                        _sceneNodes.RemoveAt(i);
                    }
                }
                else if (_sceneNodes.Count < flatList.Count)
                {
                    for (int i = _sceneNodes.Count; i < flatList.Count; i++)
                    {
                        _sceneNodes.Add(flatList[i]);
                    }
                }

                SyncSceneListSelectionToProjectNode();

                if (_sceneNodes.Count > 0)
                {
                    _sceneListBox.ScrollIntoView(_sceneNodes[0]);
                    _sceneListBox.ScrollIntoView(_sceneNodes[_sceneNodes.Count - 1]);
                    _sceneListBox.ScrollIntoView(_sceneNodes[0]);

                    _sceneListBox.InvalidateVisual();
                    _sceneListBox.InvalidateMeasure();
                    _sceneListBox.InvalidateArrange();
                    (_sceneListBox.ContainerFromIndex(0) as Control)?.InvalidateVisual();
                }
            });
        }

        private ModelNode? GetModelNode()
        {
            return _project.SceneRoot.Children
                .OfType<ModelNode>()
                .FirstOrDefault();
        }

        private CollarNode? GetCollarNode()
        {
            return GetModelNode()?
                .Children
                .OfType<CollarNode>()
                .FirstOrDefault();
        }

        private CollarNode EnsureCollarNode()
        {
            return _project.EnsureCollarNode();
        }

        private void UpdateCollarControlEnablement(bool hasModel, CollarPreset preset)
        {
            if (_collarEnabledCheckBox == null ||
                _collarPresetCombo == null ||
                _collarMeshPathTextBox == null ||
                _collarScaleInput == null ||
                _collarBodyLengthInput == null ||
                _collarBodyThicknessInput == null ||
                _collarHeadLengthInput == null ||
                _collarHeadThicknessInput == null ||
                _collarRotateInput == null ||
                _collarMirrorXCheckBox == null ||
                _collarMirrorYCheckBox == null ||
                _collarMirrorZCheckBox == null ||
                _collarOffsetXInput == null ||
                _collarOffsetYInput == null ||
                _collarElevationInput == null ||
                _collarInflateInput == null ||
                _collarMaterialBaseRInput == null ||
                _collarMaterialBaseGInput == null ||
                _collarMaterialBaseBInput == null ||
                _collarMaterialMetallicInput == null ||
                _collarMaterialRoughnessInput == null ||
                _collarMaterialPearlescenceInput == null ||
                _collarMaterialRustInput == null ||
                _collarMaterialWearInput == null ||
                _collarMaterialGunkInput == null)
            {
                return;
            }

            _collarEnabledCheckBox.IsEnabled = hasModel;
            _collarPresetCombo.IsEnabled = hasModel;

            bool importedPreset = hasModel && CollarNode.IsImportedMeshPreset(preset);
            CollarPresetOption selectedOption = ResolveSelectedCollarPresetOption();
            bool customImportedPreset = importedPreset && selectedOption.AllowsCustomPathEntry;
            _collarMeshPathTextBox.IsEnabled = importedPreset;
            _collarMeshPathTextBox.IsReadOnly = importedPreset && !customImportedPreset;
            _collarScaleInput.IsEnabled = importedPreset;
            _collarBodyLengthInput.IsEnabled = importedPreset;
            _collarBodyThicknessInput.IsEnabled = importedPreset;
            _collarHeadLengthInput.IsEnabled = importedPreset;
            _collarHeadThicknessInput.IsEnabled = importedPreset;
            _collarRotateInput.IsEnabled = importedPreset;
            _collarMirrorXCheckBox.IsEnabled = importedPreset;
            _collarMirrorYCheckBox.IsEnabled = importedPreset;
            _collarMirrorZCheckBox.IsEnabled = importedPreset;
            _collarOffsetXInput.IsEnabled = importedPreset;
            _collarOffsetYInput.IsEnabled = importedPreset;
            _collarElevationInput.IsEnabled = hasModel;
            _collarInflateInput.IsEnabled = importedPreset;

            bool collarMaterialEnabled = hasModel;
            _collarMaterialBaseRInput.IsEnabled = collarMaterialEnabled;
            _collarMaterialBaseGInput.IsEnabled = collarMaterialEnabled;
            _collarMaterialBaseBInput.IsEnabled = collarMaterialEnabled;
            _collarMaterialMetallicInput.IsEnabled = collarMaterialEnabled;
            _collarMaterialRoughnessInput.IsEnabled = collarMaterialEnabled;
            _collarMaterialPearlescenceInput.IsEnabled = collarMaterialEnabled;
            _collarMaterialRustInput.IsEnabled = collarMaterialEnabled;
            _collarMaterialWearInput.IsEnabled = collarMaterialEnabled;
            _collarMaterialGunkInput.IsEnabled = collarMaterialEnabled;
            UpdateCollarMeshPathFeedback(preset, _collarMeshPathTextBox.Text, customImportedPreset);
        }

        private void RefreshInspectorFromProject(InspectorRefreshTabPolicy tabPolicy = InspectorRefreshTabPolicy.PreserveCurrentTab)
        {
            if (_lightingModeCombo == null || _lightListBox == null ||
                _removeLightButton == null || _rotationInput == null || _lightTypeCombo == null ||
                _lightXInput == null || _lightYInput == null || _lightZInput == null ||
                _directionInput == null || _intensityInput == null || _falloffInput == null ||
                _lightRInput == null || _lightGInput == null || _lightBInput == null ||
                _diffuseBoostInput == null || _specularBoostInput == null || _specularPowerInput == null ||
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
                _materialBaseRInput == null || _materialBaseGInput == null || _materialBaseBInput == null || _materialRegionCombo == null ||
                _materialMetallicInput == null || _materialRoughnessInput == null || _materialPearlescenceInput == null ||
                _materialRustInput == null || _materialWearInput == null || _materialGunkInput == null ||
                _materialBrushStrengthInput == null || _materialBrushDensityInput == null || _materialCharacterInput == null ||
                _spiralNormalInfluenceCheckBox == null || _basisDebugModeCombo == null || _microLodFadeStartInput == null || _microLodFadeEndInput == null || _microRoughnessLodBoostInput == null ||
                _envIntensityInput == null || _envRoughnessMixInput == null ||
                _envTopRInput == null || _envTopGInput == null || _envTopBInput == null ||
                _envBottomRInput == null || _envBottomGInput == null || _envBottomBInput == null ||
                _shadowEnabledCheckBox == null || _shadowSourceModeCombo == null || _shadowStrengthInput == null || _shadowSoftnessInput == null ||
                _shadowDistanceInput == null || _shadowScaleInput == null || _shadowQualityInput == null ||
                _shadowGrayInput == null || _shadowDiffuseInfluenceInput == null ||
                _brushPaintEnabledCheckBox == null || _brushPaintChannelCombo == null || _brushTypeCombo == null || _brushPaintColorPicker == null || _paintChannelTargetValueInput == null || _scratchAbrasionTypeCombo == null ||
                _paintLayerVisibleCheckBox == null || _paintLayerBlendModeCombo == null || _paintLayerOpacityInput == null ||
                _paintMaskResolutionCombo == null || _paintMaskResolutionMemoryText == null ||
                _brushSizeInput == null || _brushOpacityInput == null || _brushDarknessInput == null || _brushSpreadInput == null ||
                _paintCoatMetallicInput == null || _paintCoatRoughnessInput == null ||
                _clearCoatAmountInput == null || _clearCoatRoughnessInput == null || _anisotropyAngleInput == null ||
                _scratchWidthInput == null || _scratchDepthInput == null || _scratchResistanceInput == null || _scratchDepthRampInput == null ||
                _scratchExposeColorRInput == null || _scratchExposeColorGInput == null || _scratchExposeColorBInput == null ||
                _scratchExposeMetallicInput == null || _scratchExposeRoughnessInput == null)
            {
                return;
            }

            RememberInspectorPresentationStateForCurrentTab();
            InspectorFocusState? preservedFocus = CaptureInspectorFocusStateForCurrentTab();
            TabItem? preservedTab = _inspectorTabControl?.SelectedItem as TabItem;
            _updatingUi = true;
            try
            {
                var project = _project;
                project.EnsureSelection();
                if (project.ProjectType == InteractorProjectType.IndicatorLight)
                {
                    project.EnsureIndicatorAssemblyDefaults(forceReset: false);
                    SyncIndicatorDynamicLightSourcesToAssembly(recenterSources: false);
                }
                ApplyProjectTypeInspectorVisibility();
                var model = GetModelNode();
                MaterialNode[] materials = model?.GetMaterialNodes() ?? Array.Empty<MaterialNode>();
                int selectedMaterialIndex = ClampSelectedMaterialIndex(materials);
                MaterialNode? material = selectedMaterialIndex >= 0 && selectedMaterialIndex < materials.Length
                    ? materials[selectedMaterialIndex]
                    : null;
                var collar = model?.Children.OfType<CollarNode>().FirstOrDefault();

                _lightingModeCombo.SelectedItem = project.Mode;

                var lightLabels = new List<string>();
                for (int i = 0; i < project.Lights.Count; i++)
                {
                    var l = project.Lights[i];
                    lightLabels.Add($"{i + 1}. {l.Name} [{l.Type}]");
                }

                DetachLightListHandler();
                _lightListBox.ItemsSource = lightLabels;
                _lightListBox.SelectedIndex = project.SelectedLightIndex;
                AttachLightListHandler();
                _removeLightButton.IsEnabled = project.Lights.Count > 1;

                if (model != null)
                {
                    SelectReferenceStyleOptionForModel(model);
                    _bodyStyleCombo.SelectedItem = model.BodyStyle;
                    _rotationInput.Value = RadiansToDegrees(model.RotationRadians);
                    _modelRadiusInput.Value = model.Radius;
                    _modelHeightInput.Value = model.Height;
                    _modelTopScaleInput.Value = model.TopRadiusScale;
                    _modelBevelInput.Value = model.Bevel;
                    _bevelCurveInput.Value = model.BevelCurve;
                    _crownProfileInput.Value = model.CrownProfile;
                    _bodyTaperInput.Value = model.BodyTaper;
                    _bodyBulgeInput.Value = model.BodyBulge;
                    _modelSegmentsInput.Value = model.RadialSegments;
                    RebuildSliderMeshOptions();
                    RebuildToggleMeshOptions();
                    if (_sliderAssemblyModeCombo != null)
                    {
                        _sliderAssemblyModeCombo.SelectedItem = project.SliderMode;
                    }

                    if (_sliderBackplateMeshCombo != null)
                    {
                        _sliderBackplateMeshCombo.SelectedItem =
                            ResolveSliderMeshOption(_sliderBackplateMeshOptions, project.SliderBackplateImportedMeshPath);
                    }

                    if (_sliderThumbMeshCombo != null)
                    {
                        _sliderThumbMeshCombo.SelectedItem =
                            ResolveSliderMeshOption(_sliderThumbMeshOptions, project.SliderThumbImportedMeshPath);
                    }

                    if (_sliderBackplateWidthInput != null)
                    {
                        _sliderBackplateWidthInput.Value = project.SliderBackplateWidth;
                    }

                    if (_sliderBackplateHeightInput != null)
                    {
                        _sliderBackplateHeightInput.Value = project.SliderBackplateHeight;
                    }

                    if (_sliderBackplateThicknessInput != null)
                    {
                        _sliderBackplateThicknessInput.Value = project.SliderBackplateThickness;
                    }

                    if (_sliderThumbWidthInput != null)
                    {
                        _sliderThumbWidthInput.Value = project.SliderThumbWidth;
                    }

                    if (_sliderThumbHeightInput != null)
                    {
                        _sliderThumbHeightInput.Value = project.SliderThumbHeight;
                    }

                    if (_sliderThumbDepthInput != null)
                    {
                        _sliderThumbDepthInput.Value = project.SliderThumbDepth;
                    }
                    if (_sliderThumbProfileCombo != null)
                    {
                        _sliderThumbProfileCombo.SelectedItem = project.SliderThumbProfile;
                    }
                    if (_sliderTrackStyleCombo != null)
                    {
                        _sliderTrackStyleCombo.SelectedItem = project.SliderTrackStyle;
                    }
                    if (_sliderTrackWidthInput != null)
                    {
                        _sliderTrackWidthInput.Value = project.SliderTrackWidth;
                    }
                    if (_sliderTrackDepthInput != null)
                    {
                        _sliderTrackDepthInput.Value = project.SliderTrackDepth;
                    }
                    if (_sliderRailHeightInput != null)
                    {
                        _sliderRailHeightInput.Value = project.SliderRailHeight;
                    }
                    if (_sliderRailSpacingInput != null)
                    {
                        _sliderRailSpacingInput.Value = project.SliderRailSpacing;
                    }
                    if (_sliderThumbRidgeCountInput != null)
                    {
                        _sliderThumbRidgeCountInput.Value = project.SliderThumbRidgeCount;
                    }
                    if (_sliderThumbRidgeDepthInput != null)
                    {
                        _sliderThumbRidgeDepthInput.Value = project.SliderThumbRidgeDepth;
                    }
                    if (_sliderThumbCornerRadiusInput != null)
                    {
                        _sliderThumbCornerRadiusInput.Value = project.SliderThumbCornerRadius;
                    }
                    if (_pushButtonPressAmountInput != null)
                    {
                        _pushButtonPressAmountInput.Value = project.PushButtonPressAmountNormalized;
                    }
                    if (_pushButtonBaseMeshCombo != null)
                    {
                        _pushButtonBaseMeshCombo.SelectedItem =
                            ResolvePushButtonMeshOption(_pushButtonBaseMeshOptions, project.PushButtonBaseImportedMeshPath);
                    }
                    if (_pushButtonCapMeshCombo != null)
                    {
                        _pushButtonCapMeshCombo.SelectedItem =
                            ResolvePushButtonMeshOption(_pushButtonCapMeshOptions, project.PushButtonCapImportedMeshPath);
                    }
                    if (_pushButtonCapProfileCombo != null)
                    {
                        _pushButtonCapProfileCombo.SelectedItem = project.PushButtonCapProfile;
                    }
                    if (_pushButtonBezelProfileCombo != null)
                    {
                        _pushButtonBezelProfileCombo.SelectedItem = project.PushButtonBezelProfile;
                    }
                    if (_pushButtonSkirtStyleCombo != null)
                    {
                        _pushButtonSkirtStyleCombo.SelectedItem = project.PushButtonSkirtStyle;
                    }
                    if (_pushButtonBezelChamferSizeInput != null)
                    {
                        _pushButtonBezelChamferSizeInput.Value = project.PushButtonBezelChamferSize;
                    }
                    if (_pushButtonCapOverhangInput != null)
                    {
                        _pushButtonCapOverhangInput.Value = project.PushButtonCapOverhang;
                    }
                    if (_pushButtonCapSegmentsInput != null)
                    {
                        _pushButtonCapSegmentsInput.Value = project.PushButtonCapSegments;
                    }
                    if (_pushButtonBezelSegmentsInput != null)
                    {
                        _pushButtonBezelSegmentsInput.Value = project.PushButtonBezelSegments;
                    }
                    if (_pushButtonSkirtHeightInput != null)
                    {
                        _pushButtonSkirtHeightInput.Value = project.PushButtonSkirtHeight;
                    }
                    if (_pushButtonSkirtRadiusInput != null)
                    {
                        _pushButtonSkirtRadiusInput.Value = project.PushButtonSkirtRadius;
                    }
                    if (_toggleAssemblyModeCombo != null)
                    {
                        _toggleAssemblyModeCombo.SelectedItem = project.ToggleMode;
                    }

                    if (_toggleBaseMeshCombo != null)
                    {
                        _toggleBaseMeshCombo.SelectedItem =
                            ResolveToggleMeshOption(_toggleBaseMeshOptions, project.ToggleBaseImportedMeshPath);
                    }

                    if (_toggleLeverMeshCombo != null)
                    {
                        _toggleLeverMeshCombo.SelectedItem =
                            ResolveToggleMeshOption(_toggleLeverMeshOptions, project.ToggleLeverImportedMeshPath);
                    }

                    if (_toggleStateCountCombo != null)
                    {
                        _toggleStateCountCombo.SelectedItem = project.ToggleStateCount;
                    }

                    if (_toggleStateIndexInput != null)
                    {
                        int maxStateIndex = project.ToggleStateCount == ToggleAssemblyStateCount.ThreePosition ? 2 : 1;
                        _toggleStateIndexInput.Maximum = maxStateIndex;
                        _toggleStateIndexInput.Value = Math.Clamp(project.ToggleStateIndex, 0, maxStateIndex);
                    }

                    if (_toggleMaxAngleInput != null)
                    {
                        _toggleMaxAngleInput.Value = project.ToggleMaxAngleDeg;
                    }

                    if (_togglePlateWidthInput != null)
                    {
                        _togglePlateWidthInput.Value = project.TogglePlateWidth;
                    }

                    if (_togglePlateHeightInput != null)
                    {
                        _togglePlateHeightInput.Value = project.TogglePlateHeight;
                    }

                    if (_togglePlateThicknessInput != null)
                    {
                        _togglePlateThicknessInput.Value = project.TogglePlateThickness;
                    }

                    if (_togglePlateOffsetYInput != null)
                    {
                        _togglePlateOffsetYInput.Value = project.TogglePlateOffsetY;
                    }

                    if (_togglePlateOffsetZInput != null)
                    {
                        _togglePlateOffsetZInput.Value = project.TogglePlateOffsetZ;
                    }

                    if (_toggleBushingRadiusInput != null)
                    {
                        _toggleBushingRadiusInput.Value = project.ToggleBushingRadius;
                    }

                    if (_toggleBushingHeightInput != null)
                    {
                        _toggleBushingHeightInput.Value = project.ToggleBushingHeight;
                    }

                    if (_toggleBushingSidesInput != null)
                    {
                        _toggleBushingSidesInput.Value = project.ToggleBushingSides;
                    }

                    if (_toggleLowerBushingShapeCombo != null)
                    {
                        _toggleLowerBushingShapeCombo.SelectedItem = project.ToggleLowerBushingShape;
                    }

                    if (_toggleUpperBushingShapeCombo != null)
                    {
                        _toggleUpperBushingShapeCombo.SelectedItem = project.ToggleUpperBushingShape;
                    }

                    if (_toggleLowerBushingRadiusScaleInput != null)
                    {
                        _toggleLowerBushingRadiusScaleInput.Value = project.ToggleLowerBushingRadiusScale;
                    }

                    if (_toggleLowerBushingHeightRatioInput != null)
                    {
                        _toggleLowerBushingHeightRatioInput.Value = project.ToggleLowerBushingHeightRatio;
                    }

                    if (_toggleUpperBushingRadiusScaleInput != null)
                    {
                        _toggleUpperBushingRadiusScaleInput.Value = project.ToggleUpperBushingRadiusScale;
                    }

                    if (_toggleUpperBushingHeightRatioInput != null)
                    {
                        _toggleUpperBushingHeightRatioInput.Value = project.ToggleUpperBushingHeightRatio;
                    }

                    if (_toggleUpperBushingKnurlAmountInput != null)
                    {
                        _toggleUpperBushingKnurlAmountInput.Value = project.ToggleUpperBushingKnurlAmount;
                    }

                    if (_toggleUpperBushingKnurlDensityInput != null)
                    {
                        _toggleUpperBushingKnurlDensityInput.Value = project.ToggleUpperBushingKnurlDensity;
                    }

                    if (_toggleUpperBushingKnurlDepthInput != null)
                    {
                        _toggleUpperBushingKnurlDepthInput.Value = project.ToggleUpperBushingKnurlDepth;
                    }

                    if (_toggleUpperBushingAnisotropyStrengthInput != null)
                    {
                        _toggleUpperBushingAnisotropyStrengthInput.Value = project.ToggleUpperBushingAnisotropyStrength;
                    }

                    if (_toggleUpperBushingAnisotropyDensityInput != null)
                    {
                        _toggleUpperBushingAnisotropyDensityInput.Value = project.ToggleUpperBushingAnisotropyDensity;
                    }

                    if (_toggleUpperBushingAnisotropyAngleInput != null)
                    {
                        _toggleUpperBushingAnisotropyAngleInput.Value = project.ToggleUpperBushingAnisotropyAngleDegrees;
                    }

                    if (_toggleUpperBushingSurfaceCharacterInput != null)
                    {
                        _toggleUpperBushingSurfaceCharacterInput.Value = project.ToggleUpperBushingSurfaceCharacter;
                    }

                    if (_togglePivotHousingRadiusInput != null)
                    {
                        _togglePivotHousingRadiusInput.Value = project.TogglePivotHousingRadius;
                    }

                    if (_togglePivotHousingDepthInput != null)
                    {
                        _togglePivotHousingDepthInput.Value = project.TogglePivotHousingDepth;
                    }

                    if (_togglePivotHousingBevelInput != null)
                    {
                        _togglePivotHousingBevelInput.Value = project.TogglePivotHousingBevel;
                    }

                    if (_togglePivotBallRadiusInput != null)
                    {
                        _togglePivotBallRadiusInput.Value = project.TogglePivotBallRadius;
                    }

                    if (_togglePivotClearanceInput != null)
                    {
                        _togglePivotClearanceInput.Value = project.TogglePivotClearance;
                    }

                    if (_toggleInvertBaseWindingCheckBox != null)
                    {
                        _toggleInvertBaseWindingCheckBox.IsChecked = project.ToggleInvertBaseFrontFaceWinding;
                    }

                    if (_toggleInvertLeverWindingCheckBox != null)
                    {
                        _toggleInvertLeverWindingCheckBox.IsChecked = project.ToggleInvertLeverFrontFaceWinding;
                    }

                    if (_toggleLeverLengthInput != null)
                    {
                        _toggleLeverLengthInput.Value = project.ToggleLeverLength;
                    }

                    if (_toggleLeverRadiusInput != null)
                    {
                        _toggleLeverRadiusInput.Value = project.ToggleLeverRadius;
                    }

                    if (_toggleLeverTopRadiusInput != null)
                    {
                        _toggleLeverTopRadiusInput.Value = project.ToggleLeverTopRadius;
                    }

                    if (_toggleLeverSidesInput != null)
                    {
                        _toggleLeverSidesInput.Value = project.ToggleLeverSides;
                    }

                    if (_toggleLeverPivotOffsetInput != null)
                    {
                        _toggleLeverPivotOffsetInput.Value = project.ToggleLeverPivotOffset;
                    }

                    if (_toggleTipRadiusInput != null)
                    {
                        _toggleTipRadiusInput.Value = project.ToggleTipRadius;
                    }

                    if (_toggleTipLatitudeSegmentsInput != null)
                    {
                        _toggleTipLatitudeSegmentsInput.Value = project.ToggleTipLatitudeSegments;
                    }

                    if (_toggleTipLongitudeSegmentsInput != null)
                    {
                        _toggleTipLongitudeSegmentsInput.Value = project.ToggleTipLongitudeSegments;
                    }

                    if (_toggleTipSleeveEnabledCheckBox != null)
                    {
                        _toggleTipSleeveEnabledCheckBox.IsChecked = project.ToggleTipSleeveEnabled;
                    }

                    if (_toggleTipSleeveLengthInput != null)
                    {
                        _toggleTipSleeveLengthInput.Value = project.ToggleTipSleeveLength;
                    }

                    if (_toggleTipSleeveThicknessInput != null)
                    {
                        _toggleTipSleeveThicknessInput.Value = project.ToggleTipSleeveThickness;
                    }

                    if (_toggleTipSleeveOuterRadiusInput != null)
                    {
                        _toggleTipSleeveOuterRadiusInput.Value = project.ToggleTipSleeveOuterRadius;
                    }

                    if (_toggleTipSleeveCoverageInput != null)
                    {
                        _toggleTipSleeveCoverageInput.Value = project.ToggleTipSleeveCoverage;
                    }

                    if (_toggleTipSleeveSidesInput != null)
                    {
                        _toggleTipSleeveSidesInput.Value = project.ToggleTipSleeveSides;
                    }

                    if (_toggleTipSleeveStyleCombo != null)
                    {
                        _toggleTipSleeveStyleCombo.SelectedItem = project.ToggleTipSleeveStyle;
                    }

                    if (_toggleTipSleeveTipStyleCombo != null)
                    {
                        _toggleTipSleeveTipStyleCombo.SelectedItem = project.ToggleTipSleeveTipStyle;
                    }

                    if (_toggleTipSleevePatternCountInput != null)
                    {
                        _toggleTipSleevePatternCountInput.Value = project.ToggleTipSleevePatternCount;
                    }

                    if (_toggleTipSleevePatternDepthInput != null)
                    {
                        _toggleTipSleevePatternDepthInput.Value = project.ToggleTipSleevePatternDepth;
                    }

                    if (_toggleTipSleeveTipAmountInput != null)
                    {
                        _toggleTipSleeveTipAmountInput.Value = project.ToggleTipSleeveTipAmount;
                    }

                    if (_toggleTipSleeveColorRInput != null)
                    {
                        _toggleTipSleeveColorRInput.Value = project.ToggleTipSleeveColor.X;
                    }

                    if (_toggleTipSleeveColorGInput != null)
                    {
                        _toggleTipSleeveColorGInput.Value = project.ToggleTipSleeveColor.Y;
                    }

                    if (_toggleTipSleeveColorBInput != null)
                    {
                        _toggleTipSleeveColorBInput.Value = project.ToggleTipSleeveColor.Z;
                    }

                    if (_toggleTipSleeveMetallicInput != null)
                    {
                        _toggleTipSleeveMetallicInput.Value = project.ToggleTipSleeveMetallic;
                    }

                    if (_toggleTipSleeveRoughnessInput != null)
                    {
                        _toggleTipSleeveRoughnessInput.Value = project.ToggleTipSleeveRoughness;
                    }

                    if (_toggleTipSleevePearlescenceInput != null)
                    {
                        _toggleTipSleevePearlescenceInput.Value = project.ToggleTipSleevePearlescence;
                    }

                    if (_toggleTipSleeveDiffuseStrengthInput != null)
                    {
                        _toggleTipSleeveDiffuseStrengthInput.Value = project.ToggleTipSleeveDiffuseStrength;
                    }

                    if (_toggleTipSleeveSpecularStrengthInput != null)
                    {
                        _toggleTipSleeveSpecularStrengthInput.Value = project.ToggleTipSleeveSpecularStrength;
                    }

                    if (_toggleTipSleeveRustInput != null)
                    {
                        _toggleTipSleeveRustInput.Value = project.ToggleTipSleeveRustAmount;
                    }

                    if (_toggleTipSleeveWearInput != null)
                    {
                        _toggleTipSleeveWearInput.Value = project.ToggleTipSleeveWearAmount;
                    }

                    if (_toggleTipSleeveGunkInput != null)
                    {
                        _toggleTipSleeveGunkInput.Value = project.ToggleTipSleeveGunkAmount;
                    }
                    _spiralRidgeHeightInput.Value = model.SpiralRidgeHeight;
                    _spiralRidgeWidthInput.Value = model.SpiralRidgeWidth;
                    _spiralTurnsInput.Value = model.SpiralTurns;
                    _gripStyleCombo.SelectedItem = model.GripStyle;
                    _gripTypeCombo.SelectedItem = model.GripType;
                    _gripStartInput.Value = model.GripStart;
                    _gripHeightInput.Value = model.GripHeight;
                    _gripDensityInput.Value = model.GripDensity;
                    _gripPitchInput.Value = model.GripPitch;
                    _gripDepthInput.Value = model.GripDepth;
                    _gripWidthInput.Value = model.GripWidth;
                    _gripSharpnessInput.Value = model.GripSharpness;
                    if (collar != null)
                    {
                        bool importedCollarUsesMaterialNode =
                            CollarNode.IsImportedMeshPreset(collar.Preset) &&
                            material != null;
                        Vector3 collarBaseColor = importedCollarUsesMaterialNode ? material!.BaseColor : collar.BaseColor;
                        float collarMetallic = importedCollarUsesMaterialNode ? material!.Metallic : collar.Metallic;
                        float collarRoughness = importedCollarUsesMaterialNode ? material!.Roughness : collar.Roughness;
                        float collarPearlescence = importedCollarUsesMaterialNode ? material!.Pearlescence : collar.Pearlescence;
                        float collarRust = importedCollarUsesMaterialNode ? material!.RustAmount : collar.RustAmount;
                        float collarWear = importedCollarUsesMaterialNode ? material!.WearAmount : collar.WearAmount;
                        float collarGunk = importedCollarUsesMaterialNode ? material!.GunkAmount : collar.GunkAmount;

                        _collarEnabledCheckBox.IsChecked = collar.Enabled;
                        CollarPresetOption collarOption = ResolveCollarPresetOptionForState(collar.Preset, collar.ImportedMeshPath);
                        _collarPresetCombo.SelectedItem = collarOption;
                        _lastSelectableCollarPresetOption = collarOption;
                        string resolvedImportedMeshPath = ResolveBestImportedCollarPath(
                            collarOption.Preset,
                            collarOption.ResolveImportedMeshPath(collar.ImportedMeshPath));
                        collar.ImportedMeshPath = resolvedImportedMeshPath;
                        _collarMeshPathTextBox.Text = resolvedImportedMeshPath;
                        _collarScaleInput.Value = collar.ImportedScale;
                        _collarBodyLengthInput.Value = collar.ImportedBodyLengthScale;
                        _collarBodyThicknessInput.Value = collar.ImportedBodyThicknessScale;
                        _collarHeadLengthInput.Value = collar.ImportedHeadLengthScale;
                        _collarHeadThicknessInput.Value = collar.ImportedHeadThicknessScale;
                        _collarRotateInput.Value = RadiansToDegrees(collar.ImportedRotationRadians);
                        _collarMirrorXCheckBox.IsChecked = collar.ImportedMirrorX;
                        _collarMirrorYCheckBox.IsChecked = collar.ImportedMirrorY;
                        _collarMirrorZCheckBox.IsChecked = collar.ImportedMirrorZ;
                        _collarOffsetXInput.Value = collar.ImportedOffsetXRatio;
                        _collarOffsetYInput.Value = collar.ImportedOffsetYRatio;
                        _collarElevationInput.Value = collar.ElevationRatio;
                        _collarInflateInput.Value = collar.ImportedInflateRatio;
                        _collarMaterialBaseRInput.Value = collarBaseColor.X;
                        _collarMaterialBaseGInput.Value = collarBaseColor.Y;
                        _collarMaterialBaseBInput.Value = collarBaseColor.Z;
                        _collarMaterialMetallicInput.Value = collarMetallic;
                        _collarMaterialRoughnessInput.Value = collarRoughness;
                        _collarMaterialPearlescenceInput.Value = collarPearlescence;
                        _collarMaterialRustInput.Value = collarRust;
                        _collarMaterialWearInput.Value = collarWear;
                        _collarMaterialGunkInput.Value = collarGunk;
                    }
                    else
                    {
                        _collarEnabledCheckBox.IsChecked = false;
                        CollarPresetOption noneOption = ResolveCollarPresetOptionForState(CollarPreset.None, null);
                        _collarPresetCombo.SelectedItem = noneOption;
                        _lastSelectableCollarPresetOption = noneOption;
                        _collarMeshPathTextBox.Text = string.Empty;
                        _collarScaleInput.Value = 1.0;
                        _collarBodyLengthInput.Value = 1.0;
                        _collarBodyThicknessInput.Value = 1.0;
                        _collarHeadLengthInput.Value = 1.0;
                        _collarHeadThicknessInput.Value = 1.0;
                        _collarRotateInput.Value = 0.0;
                        _collarMirrorXCheckBox.IsChecked = false;
                        _collarMirrorYCheckBox.IsChecked = false;
                        _collarMirrorZCheckBox.IsChecked = false;
                        _collarOffsetXInput.Value = 0.0;
                        _collarOffsetYInput.Value = 0.0;
                        _collarElevationInput.Value = 0.0;
                        _collarInflateInput.Value = 0.0;
                        _collarMaterialBaseRInput.Value = 0.74;
                        _collarMaterialBaseGInput.Value = 0.74;
                        _collarMaterialBaseBInput.Value = 0.70;
                        _collarMaterialMetallicInput.Value = 0.96;
                        _collarMaterialRoughnessInput.Value = 0.32;
                        _collarMaterialPearlescenceInput.Value = 0.0;
                        _collarMaterialRustInput.Value = 0.0;
                        _collarMaterialWearInput.Value = 0.0;
                        _collarMaterialGunkInput.Value = 0.0;
                    }
                    _indicatorEnabledCheckBox.IsChecked = model.IndicatorEnabled;
                    if (_indicatorCadWallsCheckBox != null)
                    {
                        _indicatorCadWallsCheckBox.IsChecked = model.IndicatorCadWallsEnabled;
                    }
                    _indicatorShapeCombo.SelectedItem = model.IndicatorShape;
                    _indicatorReliefCombo.SelectedItem = model.IndicatorRelief;
                    _indicatorProfileCombo.SelectedItem = model.IndicatorProfile;
                    _indicatorWidthInput.Value = model.IndicatorWidthRatio;
                    _indicatorLengthInput.Value = model.IndicatorLengthRatioTop;
                    _indicatorPositionInput.Value = model.IndicatorPositionRatio;
                    _indicatorThicknessInput.Value = model.IndicatorThicknessRatio;
                    _indicatorRoundnessInput.Value = model.IndicatorRoundness;
                    _indicatorColorBlendInput.Value = model.IndicatorColorBlend;
                    _indicatorColorRInput.Value = model.IndicatorColor.X;
                    _indicatorColorGInput.Value = model.IndicatorColor.Y;
                    _indicatorColorBInput.Value = model.IndicatorColor.Z;
                    if (_indicatorAssemblyEnabledCheckBox != null)
                    {
                        _indicatorAssemblyEnabledCheckBox.IsChecked = project.IndicatorAssemblyEnabled;
                    }
                    if (_indicatorBaseWidthInput != null)
                    {
                        _indicatorBaseWidthInput.Value = project.IndicatorBaseWidth;
                    }
                    if (_indicatorBaseHeightInput != null)
                    {
                        _indicatorBaseHeightInput.Value = project.IndicatorBaseHeight;
                    }
                    if (_indicatorBaseThicknessInput != null)
                    {
                        _indicatorBaseThicknessInput.Value = project.IndicatorBaseThickness;
                    }
                    if (_indicatorHousingRadiusInput != null)
                    {
                        _indicatorHousingRadiusInput.Value = project.IndicatorHousingRadius;
                    }
                    if (_indicatorHousingHeightInput != null)
                    {
                        _indicatorHousingHeightInput.Value = project.IndicatorHousingHeight;
                    }
                    if (_indicatorLensRadiusInput != null)
                    {
                        _indicatorLensRadiusInput.Value = project.IndicatorLensRadius;
                    }
                    if (_indicatorLensHeightInput != null)
                    {
                        _indicatorLensHeightInput.Value = project.IndicatorLensHeight;
                    }
                    if (_indicatorLensTransmissionInput != null)
                    {
                        _indicatorLensTransmissionInput.Value = project.IndicatorLensTransmission;
                    }
                    if (_indicatorLensIorInput != null)
                    {
                        _indicatorLensIorInput.Value = project.IndicatorLensIor;
                    }
                    if (_indicatorLensThicknessInput != null)
                    {
                        _indicatorLensThicknessInput.Value = project.IndicatorLensThickness;
                    }
                    if (_indicatorLensAbsorptionInput != null)
                    {
                        _indicatorLensAbsorptionInput.Value = project.IndicatorLensAbsorption;
                    }
                    if (_indicatorLensSurfaceRoughnessInput != null)
                    {
                        _indicatorLensSurfaceRoughnessInput.Value = project.IndicatorLensSurfaceRoughness;
                    }
                    if (_indicatorLensSurfaceSpecularInput != null)
                    {
                        _indicatorLensSurfaceSpecularInput.Value = project.IndicatorLensSurfaceSpecularStrength;
                    }
                    if (_indicatorLensTintRInput != null)
                    {
                        _indicatorLensTintRInput.Value = project.IndicatorLensTint.X;
                    }
                    if (_indicatorLensTintGInput != null)
                    {
                        _indicatorLensTintGInput.Value = project.IndicatorLensTint.Y;
                    }
                    if (_indicatorLensTintBInput != null)
                    {
                        _indicatorLensTintBInput.Value = project.IndicatorLensTint.Z;
                    }
                    if (_indicatorReflectorBaseRadiusInput != null)
                    {
                        _indicatorReflectorBaseRadiusInput.Value = project.IndicatorReflectorBaseRadius;
                    }
                    if (_indicatorReflectorTopRadiusInput != null)
                    {
                        _indicatorReflectorTopRadiusInput.Value = project.IndicatorReflectorTopRadius;
                    }
                    if (_indicatorReflectorDepthInput != null)
                    {
                        _indicatorReflectorDepthInput.Value = project.IndicatorReflectorDepth;
                    }
                    if (_indicatorEmitterRadiusInput != null)
                    {
                        _indicatorEmitterRadiusInput.Value = project.IndicatorEmitterRadius;
                    }
                    if (_indicatorEmitterSpreadInput != null)
                    {
                        _indicatorEmitterSpreadInput.Value = project.IndicatorEmitterSpread;
                    }
                    if (_indicatorEmitterDepthInput != null)
                    {
                        _indicatorEmitterDepthInput.Value = project.IndicatorEmitterDepth;
                    }
                    if (_indicatorEmitterCountInput != null)
                    {
                        _indicatorEmitterCountInput.Value = project.IndicatorEmitterCount;
                    }
                    if (_indicatorRadialSegmentsInput != null)
                    {
                        _indicatorRadialSegmentsInput.Value = project.IndicatorRadialSegments;
                    }
                    if (_indicatorLensLatitudeSegmentsInput != null)
                    {
                        _indicatorLensLatitudeSegmentsInput.Value = project.IndicatorLensLatitudeSegments;
                    }
                    if (_indicatorLensLongitudeSegmentsInput != null)
                    {
                        _indicatorLensLongitudeSegmentsInput.Value = project.IndicatorLensLongitudeSegments;
                    }
                    if (_indicatorQuickLightOnCheckBox != null)
                    {
                        _indicatorQuickLightOnCheckBox.IsChecked = project.DynamicLightRig.Enabled;
                    }
                    if (_indicatorQuickBrightnessInput != null)
                    {
                        _indicatorQuickBrightnessInput.Value = project.DynamicLightRig.MasterIntensity;
                    }
                    if (_indicatorQuickGlowInput != null)
                    {
                        _indicatorQuickGlowInput.Value = project.DynamicLightRig.EmissiveGlow;
                    }
                    if (_indicatorDynamicLightsEnabledCheckBox != null)
                    {
                        _indicatorDynamicLightsEnabledCheckBox.IsChecked = project.DynamicLightRig.Enabled;
                    }
                    if (_indicatorLightAnimationModeCombo != null)
                    {
                        _indicatorLightAnimationModeCombo.SelectedItem = project.DynamicLightRig.AnimationMode;
                    }
                    if (_indicatorLightAnimationSpeedInput != null)
                    {
                        _indicatorLightAnimationSpeedInput.Value = project.DynamicLightRig.AnimationSpeed;
                    }
                    if (_indicatorLightFlickerAmountInput != null)
                    {
                        _indicatorLightFlickerAmountInput.Value = project.DynamicLightRig.FlickerAmount;
                    }
                    if (_indicatorLightFlickerDropoutInput != null)
                    {
                        _indicatorLightFlickerDropoutInput.Value = project.DynamicLightRig.FlickerDropoutChance;
                    }
                    if (_indicatorLightFlickerSmoothingInput != null)
                    {
                        _indicatorLightFlickerSmoothingInput.Value = project.DynamicLightRig.FlickerSmoothing;
                    }
                    if (_indicatorLightFlickerSeedInput != null)
                    {
                        _indicatorLightFlickerSeedInput.Value = project.DynamicLightRig.FlickerSeed;
                    }
                    RefreshIndicatorEmitterSourceControlsFromProject();
                }

                bool hasModel = model != null;
                bool indicatorProject = project.ProjectType == InteractorProjectType.IndicatorLight;
                UpdateCollarControlEnablement(hasModel, collar?.Preset ?? CollarPreset.None);
                _indicatorEnabledCheckBox.IsEnabled = hasModel && !indicatorProject;
                if (_indicatorCadWallsCheckBox != null)
                {
                    _indicatorCadWallsCheckBox.IsEnabled = hasModel && !indicatorProject;
                }
                _indicatorShapeCombo.IsEnabled = hasModel && !indicatorProject;
                _indicatorReliefCombo.IsEnabled = hasModel && !indicatorProject;
                _indicatorProfileCombo.IsEnabled = hasModel && !indicatorProject;
                _indicatorWidthInput.IsEnabled = hasModel && !indicatorProject;
                _indicatorLengthInput.IsEnabled = hasModel && !indicatorProject;
                _indicatorPositionInput.IsEnabled = hasModel && !indicatorProject;
                _indicatorThicknessInput.IsEnabled = hasModel && !indicatorProject;
                _indicatorRoundnessInput.IsEnabled = hasModel && !indicatorProject;
                _indicatorColorBlendInput.IsEnabled = hasModel && !indicatorProject;
                _indicatorColorRInput.IsEnabled = hasModel && !indicatorProject;
                _indicatorColorGInput.IsEnabled = hasModel && !indicatorProject;
                _indicatorColorBInput.IsEnabled = hasModel && !indicatorProject;
                if (_indicatorAssemblyEnabledCheckBox != null)
                {
                    _indicatorAssemblyEnabledCheckBox.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorQuickLightOnCheckBox != null)
                {
                    _indicatorQuickLightOnCheckBox.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorQuickBrightnessInput != null)
                {
                    _indicatorQuickBrightnessInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorQuickGlowInput != null)
                {
                    _indicatorQuickGlowInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorBaseWidthInput != null)
                {
                    _indicatorBaseWidthInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorBaseHeightInput != null)
                {
                    _indicatorBaseHeightInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorBaseThicknessInput != null)
                {
                    _indicatorBaseThicknessInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorHousingRadiusInput != null)
                {
                    _indicatorHousingRadiusInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorHousingHeightInput != null)
                {
                    _indicatorHousingHeightInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLensRadiusInput != null)
                {
                    _indicatorLensRadiusInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLensHeightInput != null)
                {
                    _indicatorLensHeightInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorReflectorBaseRadiusInput != null)
                {
                    _indicatorReflectorBaseRadiusInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorReflectorTopRadiusInput != null)
                {
                    _indicatorReflectorTopRadiusInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorReflectorDepthInput != null)
                {
                    _indicatorReflectorDepthInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorEmitterRadiusInput != null)
                {
                    _indicatorEmitterRadiusInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorEmitterSpreadInput != null)
                {
                    _indicatorEmitterSpreadInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorEmitterDepthInput != null)
                {
                    _indicatorEmitterDepthInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorEmitterCountInput != null)
                {
                    _indicatorEmitterCountInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorRadialSegmentsInput != null)
                {
                    _indicatorRadialSegmentsInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLensLatitudeSegmentsInput != null)
                {
                    _indicatorLensLatitudeSegmentsInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLensLongitudeSegmentsInput != null)
                {
                    _indicatorLensLongitudeSegmentsInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorAssemblyResetDefaultsButton != null)
                {
                    _indicatorAssemblyResetDefaultsButton.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorDynamicLightsEnabledCheckBox != null)
                {
                    _indicatorDynamicLightsEnabledCheckBox.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightAnimationModeCombo != null)
                {
                    _indicatorLightAnimationModeCombo.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightAnimationSpeedInput != null)
                {
                    _indicatorLightAnimationSpeedInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightFlickerAmountInput != null)
                {
                    _indicatorLightFlickerAmountInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightFlickerDropoutInput != null)
                {
                    _indicatorLightFlickerDropoutInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightFlickerSmoothingInput != null)
                {
                    _indicatorLightFlickerSmoothingInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightFlickerSeedInput != null)
                {
                    _indicatorLightFlickerSeedInput.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightPresetNeutralButton != null)
                {
                    _indicatorLightPresetNeutralButton.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightPresetPulseButton != null)
                {
                    _indicatorLightPresetPulseButton.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightPresetFlickerButton != null)
                {
                    _indicatorLightPresetFlickerButton.IsEnabled = hasModel && indicatorProject;
                }
                bool hasEmitterSources = indicatorProject && project.DynamicLightRig.Sources.Count > 0;
                int emitterSourceCount = project.DynamicLightRig.Sources.Count;
                int emitterSelectedIndex = _indicatorEmitterSourceCombo?.SelectedIndex ?? -1;
                if (_indicatorEmitterSourceCombo != null)
                {
                    _indicatorEmitterSourceCombo.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorEmitterSourceMoveUpButton != null)
                {
                    _indicatorEmitterSourceMoveUpButton.IsEnabled =
                        hasModel &&
                        hasEmitterSources &&
                        emitterSourceCount > 1 &&
                        emitterSelectedIndex > 0;
                }
                if (_indicatorEmitterSourceMoveDownButton != null)
                {
                    _indicatorEmitterSourceMoveDownButton.IsEnabled =
                        hasModel &&
                        hasEmitterSources &&
                        emitterSourceCount > 1 &&
                        emitterSelectedIndex >= 0 &&
                        emitterSelectedIndex < emitterSourceCount - 1;
                }
                if (_indicatorEmitterSourceAutoPhaseButton != null)
                {
                    _indicatorEmitterSourceAutoPhaseButton.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceEnabledCheckBox != null)
                {
                    _indicatorEmitterSourceEnabledCheckBox.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceNameTextBox != null)
                {
                    _indicatorEmitterSourceNameTextBox.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourcePhaseOffsetInput != null)
                {
                    _indicatorEmitterSourcePhaseOffsetInput.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceXInput != null)
                {
                    _indicatorEmitterSourceXInput.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceYInput != null)
                {
                    _indicatorEmitterSourceYInput.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceZInput != null)
                {
                    _indicatorEmitterSourceZInput.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceIntensityInput != null)
                {
                    _indicatorEmitterSourceIntensityInput.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceRadiusInput != null)
                {
                    _indicatorEmitterSourceRadiusInput.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceFalloffInput != null)
                {
                    _indicatorEmitterSourceFalloffInput.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceRInput != null)
                {
                    _indicatorEmitterSourceRInput.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceGInput != null)
                {
                    _indicatorEmitterSourceGInput.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceBInput != null)
                {
                    _indicatorEmitterSourceBInput.IsEnabled = hasModel && hasEmitterSources;
                }

                bool hasMaterial = material != null;
                if (_assemblyMaterialPresetCombo != null)
                {
                    _assemblyMaterialPresetCombo.IsEnabled = hasMaterial;
                }
                _materialBaseRInput.IsEnabled = hasMaterial;
                _materialBaseGInput.IsEnabled = hasMaterial;
                _materialBaseBInput.IsEnabled = hasMaterial;
                _materialRegionCombo.IsEnabled = hasMaterial;
                _materialMetallicInput.IsEnabled = hasMaterial;
                _materialRoughnessInput.IsEnabled = hasMaterial;
                _materialPearlescenceInput.IsEnabled = hasMaterial;
                _materialRustInput.IsEnabled = hasMaterial;
                _materialWearInput.IsEnabled = hasMaterial;
                _materialGunkInput.IsEnabled = hasMaterial;
                _materialAlbedoMapBrowseButton!.IsEnabled = hasMaterial;
                _materialAlbedoMapClearButton!.IsEnabled = hasMaterial;
                _materialNormalMapBrowseButton!.IsEnabled = hasMaterial;
                _materialNormalMapClearButton!.IsEnabled = hasMaterial;
                _materialRoughnessMapBrowseButton!.IsEnabled = hasMaterial;
                _materialRoughnessMapClearButton!.IsEnabled = hasMaterial;
                _materialMetallicMapBrowseButton!.IsEnabled = hasMaterial;
                _materialMetallicMapClearButton!.IsEnabled = hasMaterial;
                _materialNormalMapStrengthInput!.IsEnabled = hasMaterial && material?.HasNormalMap == true;
                _materialBrushStrengthInput.IsEnabled = hasMaterial;
                _materialBrushDensityInput.IsEnabled = hasMaterial;
                _materialCharacterInput.IsEnabled = hasMaterial;
                _spiralNormalInfluenceCheckBox.IsEnabled = hasMaterial;
                _basisDebugModeCombo.IsEnabled = hasMaterial;
                _microLodFadeStartInput.IsEnabled = hasMaterial;
                _microLodFadeEndInput.IsEnabled = hasMaterial;
                _microRoughnessLodBoostInput.IsEnabled = hasMaterial;
                _brushPaintEnabledCheckBox.IsEnabled = hasModel;
                _brushPaintChannelCombo.IsEnabled = hasModel;
                _brushTypeCombo.IsEnabled = hasModel;
                _brushPaintColorPicker.IsEnabled = hasModel;
                _scratchAbrasionTypeCombo.IsEnabled = hasModel;
                UpdateReferenceProfileActionEnablement(hasModel);
                if (_sliderAssemblyModeCombo != null)
                {
                    _sliderAssemblyModeCombo.IsEnabled = hasModel;
                }

                if (_refreshSliderLibraryButton != null)
                {
                    _refreshSliderLibraryButton.IsEnabled = hasModel;
                }

                if (_sliderBackplateMeshCombo != null)
                {
                    _sliderBackplateMeshCombo.IsEnabled = hasModel;
                }

                if (_sliderThumbMeshCombo != null)
                {
                    _sliderThumbMeshCombo.IsEnabled = hasModel;
                }

                if (_sliderBackplateWidthInput != null)
                {
                    _sliderBackplateWidthInput.IsEnabled = hasModel;
                }

                if (_sliderBackplateHeightInput != null)
                {
                    _sliderBackplateHeightInput.IsEnabled = hasModel;
                }

                if (_sliderBackplateThicknessInput != null)
                {
                    _sliderBackplateThicknessInput.IsEnabled = hasModel;
                }

                if (_sliderThumbWidthInput != null)
                {
                    _sliderThumbWidthInput.IsEnabled = hasModel;
                }

                if (_sliderThumbHeightInput != null)
                {
                    _sliderThumbHeightInput.IsEnabled = hasModel;
                }

                if (_sliderThumbDepthInput != null)
                {
                    _sliderThumbDepthInput.IsEnabled = hasModel;
                }
                if (_sliderThumbProfileCombo != null)
                {
                    _sliderThumbProfileCombo.IsEnabled = hasModel;
                }
                if (_sliderTrackStyleCombo != null)
                {
                    _sliderTrackStyleCombo.IsEnabled = hasModel;
                }
                if (_sliderTrackWidthInput != null)
                {
                    _sliderTrackWidthInput.IsEnabled = hasModel;
                }
                if (_sliderTrackDepthInput != null)
                {
                    _sliderTrackDepthInput.IsEnabled = hasModel;
                }
                if (_sliderRailHeightInput != null)
                {
                    _sliderRailHeightInput.IsEnabled = hasModel;
                }
                if (_sliderRailSpacingInput != null)
                {
                    _sliderRailSpacingInput.IsEnabled = hasModel;
                }
                if (_sliderThumbRidgeCountInput != null)
                {
                    _sliderThumbRidgeCountInput.IsEnabled = hasModel;
                }
                if (_sliderThumbRidgeDepthInput != null)
                {
                    _sliderThumbRidgeDepthInput.IsEnabled = hasModel;
                }
                if (_sliderThumbCornerRadiusInput != null)
                {
                    _sliderThumbCornerRadiusInput.IsEnabled = hasModel;
                }
                if (_pushButtonPressAmountInput != null)
                {
                    _pushButtonPressAmountInput.IsEnabled = hasModel;
                }
                if (_refreshPushButtonLibraryButton != null)
                {
                    _refreshPushButtonLibraryButton.IsEnabled = hasModel;
                }
                if (_pushButtonBaseMeshCombo != null)
                {
                    _pushButtonBaseMeshCombo.IsEnabled = hasModel;
                }
                if (_pushButtonCapMeshCombo != null)
                {
                    _pushButtonCapMeshCombo.IsEnabled = hasModel;
                }
                if (_pushButtonCapProfileCombo != null)
                {
                    _pushButtonCapProfileCombo.IsEnabled = hasModel;
                }
                if (_pushButtonBezelProfileCombo != null)
                {
                    _pushButtonBezelProfileCombo.IsEnabled = hasModel;
                }
                if (_pushButtonSkirtStyleCombo != null)
                {
                    _pushButtonSkirtStyleCombo.IsEnabled = hasModel;
                }
                if (_pushButtonBezelChamferSizeInput != null)
                {
                    _pushButtonBezelChamferSizeInput.IsEnabled = hasModel;
                }
                if (_pushButtonCapOverhangInput != null)
                {
                    _pushButtonCapOverhangInput.IsEnabled = hasModel;
                }
                if (_pushButtonCapSegmentsInput != null)
                {
                    _pushButtonCapSegmentsInput.IsEnabled = hasModel;
                }
                if (_pushButtonBezelSegmentsInput != null)
                {
                    _pushButtonBezelSegmentsInput.IsEnabled = hasModel;
                }
                if (_pushButtonSkirtHeightInput != null)
                {
                    _pushButtonSkirtHeightInput.IsEnabled = hasModel;
                }
                if (_pushButtonSkirtRadiusInput != null)
                {
                    _pushButtonSkirtRadiusInput.IsEnabled = hasModel;
                }
                if (_toggleAssemblyModeCombo != null)
                {
                    _toggleAssemblyModeCombo.IsEnabled = hasModel;
                }

                if (_refreshToggleLibraryButton != null)
                {
                    _refreshToggleLibraryButton.IsEnabled = hasModel;
                }

                if (_toggleBaseMeshCombo != null)
                {
                    _toggleBaseMeshCombo.IsEnabled = hasModel;
                }

                if (_toggleLeverMeshCombo != null)
                {
                    _toggleLeverMeshCombo.IsEnabled = hasModel;
                }

                if (_toggleStateCountCombo != null)
                {
                    _toggleStateCountCombo.IsEnabled = hasModel;
                }

                if (_toggleStateIndexInput != null)
                {
                    _toggleStateIndexInput.IsEnabled = hasModel;
                }

                if (_toggleMaxAngleInput != null)
                {
                    _toggleMaxAngleInput.IsEnabled = hasModel;
                }

                if (_togglePlateWidthInput != null)
                {
                    _togglePlateWidthInput.IsEnabled = hasModel;
                }

                if (_togglePlateHeightInput != null)
                {
                    _togglePlateHeightInput.IsEnabled = hasModel;
                }

                if (_togglePlateThicknessInput != null)
                {
                    _togglePlateThicknessInput.IsEnabled = hasModel;
                }

                if (_togglePlateOffsetYInput != null)
                {
                    _togglePlateOffsetYInput.IsEnabled = hasModel;
                }

                if (_togglePlateOffsetZInput != null)
                {
                    _togglePlateOffsetZInput.IsEnabled = hasModel;
                }

                if (_toggleBushingRadiusInput != null)
                {
                    _toggleBushingRadiusInput.IsEnabled = hasModel;
                }

                if (_toggleBushingHeightInput != null)
                {
                    _toggleBushingHeightInput.IsEnabled = hasModel;
                }

                if (_toggleBushingSidesInput != null)
                {
                    _toggleBushingSidesInput.IsEnabled = hasModel;
                }

                if (_toggleLowerBushingShapeCombo != null)
                {
                    _toggleLowerBushingShapeCombo.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingShapeCombo != null)
                {
                    _toggleUpperBushingShapeCombo.IsEnabled = hasModel;
                }

                if (_toggleLowerBushingRadiusScaleInput != null)
                {
                    _toggleLowerBushingRadiusScaleInput.IsEnabled = hasModel;
                }

                if (_toggleLowerBushingHeightRatioInput != null)
                {
                    _toggleLowerBushingHeightRatioInput.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingRadiusScaleInput != null)
                {
                    _toggleUpperBushingRadiusScaleInput.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingHeightRatioInput != null)
                {
                    _toggleUpperBushingHeightRatioInput.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingKnurlAmountInput != null)
                {
                    _toggleUpperBushingKnurlAmountInput.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingKnurlDensityInput != null)
                {
                    _toggleUpperBushingKnurlDensityInput.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingKnurlDepthInput != null)
                {
                    _toggleUpperBushingKnurlDepthInput.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingAnisotropyStrengthInput != null)
                {
                    _toggleUpperBushingAnisotropyStrengthInput.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingAnisotropyDensityInput != null)
                {
                    _toggleUpperBushingAnisotropyDensityInput.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingAnisotropyAngleInput != null)
                {
                    _toggleUpperBushingAnisotropyAngleInput.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingSurfaceCharacterInput != null)
                {
                    _toggleUpperBushingSurfaceCharacterInput.IsEnabled = hasModel;
                }

                if (_togglePivotHousingRadiusInput != null)
                {
                    _togglePivotHousingRadiusInput.IsEnabled = hasModel;
                }

                if (_togglePivotHousingDepthInput != null)
                {
                    _togglePivotHousingDepthInput.IsEnabled = hasModel;
                }

                if (_togglePivotHousingBevelInput != null)
                {
                    _togglePivotHousingBevelInput.IsEnabled = hasModel;
                }

                if (_togglePivotBallRadiusInput != null)
                {
                    _togglePivotBallRadiusInput.IsEnabled = hasModel;
                }

                if (_togglePivotClearanceInput != null)
                {
                    _togglePivotClearanceInput.IsEnabled = hasModel;
                }

                if (_toggleLeverLengthInput != null)
                {
                    _toggleLeverLengthInput.IsEnabled = hasModel;
                }

                if (_toggleLeverRadiusInput != null)
                {
                    _toggleLeverRadiusInput.IsEnabled = hasModel;
                }

                if (_toggleLeverTopRadiusInput != null)
                {
                    _toggleLeverTopRadiusInput.IsEnabled = hasModel;
                }

                if (_toggleLeverSidesInput != null)
                {
                    _toggleLeverSidesInput.IsEnabled = hasModel;
                }

                if (_toggleLeverPivotOffsetInput != null)
                {
                    _toggleLeverPivotOffsetInput.IsEnabled = hasModel;
                }

                if (_toggleTipRadiusInput != null)
                {
                    _toggleTipRadiusInput.IsEnabled = hasModel;
                }

                if (_toggleTipLatitudeSegmentsInput != null)
                {
                    _toggleTipLatitudeSegmentsInput.IsEnabled = hasModel;
                }

                if (_toggleTipLongitudeSegmentsInput != null)
                {
                    _toggleTipLongitudeSegmentsInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveEnabledCheckBox != null)
                {
                    _toggleTipSleeveEnabledCheckBox.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveLengthInput != null)
                {
                    _toggleTipSleeveLengthInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveThicknessInput != null)
                {
                    _toggleTipSleeveThicknessInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveOuterRadiusInput != null)
                {
                    _toggleTipSleeveOuterRadiusInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveCoverageInput != null)
                {
                    _toggleTipSleeveCoverageInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveSidesInput != null)
                {
                    _toggleTipSleeveSidesInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveStyleCombo != null)
                {
                    _toggleTipSleeveStyleCombo.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveTipStyleCombo != null)
                {
                    _toggleTipSleeveTipStyleCombo.IsEnabled = hasModel;
                }

                if (_toggleTipSleevePatternCountInput != null)
                {
                    _toggleTipSleevePatternCountInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleevePatternDepthInput != null)
                {
                    _toggleTipSleevePatternDepthInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveTipAmountInput != null)
                {
                    _toggleTipSleeveTipAmountInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveColorRInput != null)
                {
                    _toggleTipSleeveColorRInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveColorGInput != null)
                {
                    _toggleTipSleeveColorGInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveColorBInput != null)
                {
                    _toggleTipSleeveColorBInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveMetallicInput != null)
                {
                    _toggleTipSleeveMetallicInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveRoughnessInput != null)
                {
                    _toggleTipSleeveRoughnessInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleevePearlescenceInput != null)
                {
                    _toggleTipSleevePearlescenceInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveDiffuseStrengthInput != null)
                {
                    _toggleTipSleeveDiffuseStrengthInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveSpecularStrengthInput != null)
                {
                    _toggleTipSleeveSpecularStrengthInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveRustInput != null)
                {
                    _toggleTipSleeveRustInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveWearInput != null)
                {
                    _toggleTipSleeveWearInput.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveGunkInput != null)
                {
                    _toggleTipSleeveGunkInput.IsEnabled = hasModel;
                }
                _brushSizeInput.IsEnabled = hasModel;
                _brushOpacityInput.IsEnabled = hasModel;
                _brushDarknessInput.IsEnabled = hasModel;
                _brushSpreadInput.IsEnabled = hasModel;
                _paintCoatMetallicInput.IsEnabled = hasModel;
                _paintCoatRoughnessInput.IsEnabled = hasModel;
                _clearCoatAmountInput.IsEnabled = hasModel;
                _clearCoatRoughnessInput.IsEnabled = hasModel;
                _anisotropyAngleInput.IsEnabled = hasModel;
                _scratchWidthInput.IsEnabled = hasModel;
                _scratchDepthInput.IsEnabled = hasModel;
                _scratchResistanceInput.IsEnabled = hasModel;
                _scratchDepthRampInput.IsEnabled = hasModel;
                _scratchExposeColorRInput.IsEnabled = hasModel;
                _scratchExposeColorGInput.IsEnabled = hasModel;
                _scratchExposeColorBInput.IsEnabled = hasModel;
                _scratchExposeMetallicInput.IsEnabled = hasModel;
                _scratchExposeRoughnessInput.IsEnabled = hasModel;
                if (_clearPaintMaskButton != null)
                {
                    _clearPaintMaskButton.IsEnabled = hasModel;
                }

                RefreshMaterialInspectorUi(model, collar, materials);

                if (material != null)
                {
                    ApplyMaterialRegionValuesToSliders(material);
                    _materialPearlescenceInput.Value = material.Pearlescence;
                    _materialRustInput.Value = material.RustAmount;
                    _materialWearInput.Value = material.WearAmount;
                    _materialGunkInput.Value = material.GunkAmount;
                    _materialBrushStrengthInput.Value = material.RadialBrushStrength;
                    _materialBrushDensityInput.Value = material.RadialBrushDensity;
                    _materialCharacterInput.Value = material.SurfaceCharacter;
                }

                RefreshMaterialGraphEditorUi(material);

                _spiralNormalInfluenceCheckBox.IsChecked = project.SpiralNormalInfluenceEnabled;
                _basisDebugModeCombo.SelectedItem = project.BasisDebug;
                _microLodFadeStartInput.Value = project.SpiralNormalLodFadeStart;
                _microLodFadeEndInput.Value = project.SpiralNormalLodFadeEnd;
                _microRoughnessLodBoostInput.Value = project.SpiralRoughnessLodBoost;

                Vector3 effectiveEnvTop = project.EnvironmentTopColor;
                Vector3 effectiveEnvBottom = project.EnvironmentBottomColor;
                float effectiveEnvIntensity = project.EnvironmentIntensity;
                float effectiveEnvRoughnessMix = project.EnvironmentRoughnessMix;
                if (project.EnvironmentPreset != EnvironmentPreset.Custom &&
                    TryGetEnvironmentPresetDefinition(project.EnvironmentPreset, out EnvironmentPresetDefinition environmentPreset))
                {
                    effectiveEnvTop = environmentPreset.TopColor;
                    effectiveEnvBottom = environmentPreset.BottomColor;
                    effectiveEnvIntensity = environmentPreset.Intensity;
                    effectiveEnvRoughnessMix = environmentPreset.RoughnessMix;
                }

                _envIntensityInput.Value = effectiveEnvIntensity;
                _envRoughnessMixInput.Value = effectiveEnvRoughnessMix;
                _envTopRInput.Value = effectiveEnvTop.X;
                _envTopGInput.Value = effectiveEnvTop.Y;
                _envTopBInput.Value = effectiveEnvTop.Z;
                _envBottomRInput.Value = effectiveEnvBottom.X;
                _envBottomGInput.Value = effectiveEnvBottom.Y;
                _envBottomBInput.Value = effectiveEnvBottom.Z;
                SelectEnvironmentPresetOption(project.EnvironmentPreset);
                if (_envTonemapCombo != null)
                {
                    _envTonemapCombo.SelectedItem = project.ToneMappingOperator;
                }
                if (_envExposureInput != null)
                {
                    _envExposureInput.Value = project.EnvironmentExposure;
                }
                if (_envBloomStrengthInput != null)
                {
                    _envBloomStrengthInput.Value = project.EnvironmentBloomStrength;
                }
                if (_envBloomThresholdInput != null)
                {
                    _envBloomThresholdInput.Value = project.EnvironmentBloomThreshold;
                }
                if (_envBloomKneeInput != null)
                {
                    _envBloomKneeInput.Value = project.EnvironmentBloomKnee;
                }
                SelectBloomKernelShapeOption(project.BloomKernelShape);
                UpdateEnvironmentManualControlsAppearance(project.EnvironmentPreset);
                if (_envHdriPathTextBox != null)
                {
                    _envHdriPathTextBox.Text = project.EnvironmentHdriPath;
                }
                if (_envHdriBlendInput != null)
                {
                    _envHdriBlendInput.Value = project.EnvironmentHdriBlend;
                }
                if (_envHdriRotationInput != null)
                {
                    _envHdriRotationInput.Value = project.EnvironmentHdriRotationDegrees;
                }
                _shadowEnabledCheckBox.IsChecked = project.ShadowsEnabled;
                _shadowSourceModeCombo.SelectedItem = project.ShadowMode;
                _shadowStrengthInput.Value = project.ShadowStrength;
                _shadowSoftnessInput.Value = project.ShadowSoftness;
                _shadowDistanceInput.Value = project.ShadowDistance;
                _shadowScaleInput.Value = project.ShadowScale;
                _shadowQualityInput.Value = project.ShadowQuality;
                _shadowGrayInput.Value = project.ShadowGray;
                _shadowDiffuseInfluenceInput.Value = project.ShadowDiffuseInfluence;
                _brushPaintEnabledCheckBox.IsChecked = project.BrushPaintingEnabled;
                _paintMaskResolutionCombo.SelectedItem = project.PaintMaskSize;
                _brushPaintChannelCombo.SelectedItem = project.BrushChannel;
                _brushTypeCombo.SelectedItem = project.BrushType;
                _brushPaintColorPicker.Color = ToAvaloniaColor(project.PaintColor);
                _paintChannelTargetValueInput.Value = project.BrushChannel == PaintChannel.Roughness
                    ? project.RoughnessPaintTarget
                    : project.BrushChannel == PaintChannel.Metallic
                        ? project.MetallicPaintTarget
                        : 0d;
                _scratchAbrasionTypeCombo.SelectedItem = project.ScratchAbrasionType;
                _brushSizeInput.Value = project.BrushSizePx;
                _brushOpacityInput.Value = project.BrushOpacity;
                _brushDarknessInput.Value = project.BrushDarkness;
                _brushSpreadInput.Value = project.BrushSpread;
                _paintCoatMetallicInput.Value = project.PaintCoatMetallic;
                _paintCoatRoughnessInput.Value = project.PaintCoatRoughness;
                _clearCoatAmountInput.Value = project.ClearCoatAmount;
                _clearCoatRoughnessInput.Value = project.ClearCoatRoughness;
                _anisotropyAngleInput.Value = project.AnisotropyAngleDegrees;
                _scratchWidthInput.Value = project.ScratchWidthPx;
                _scratchDepthInput.Value = project.ScratchDepth;
                _scratchResistanceInput.Value = project.ScratchDragResistance;
                _scratchDepthRampInput.Value = project.ScratchDepthRamp;
                _scratchExposeColorRInput.Value = project.ScratchExposeColor.X;
                _scratchExposeColorGInput.Value = project.ScratchExposeColor.Y;
                _scratchExposeColorBInput.Value = project.ScratchExposeColor.Z;
                _scratchExposeMetallicInput.Value = project.ScratchExposeMetallic;
                _scratchExposeRoughnessInput.Value = project.ScratchExposeRoughness;
                UpdatePaintResolutionUi();
                UpdateBrushContextUi();
                _metalViewport?.RefreshPaintHud();

                var selectedLight = project.SelectedLight;
                bool hasLight = selectedLight != null;

                _lightTypeCombo.IsEnabled = hasLight;
                _lightXInput.IsEnabled = hasLight;
                _lightYInput.IsEnabled = hasLight;
                _lightZInput.IsEnabled = hasLight;
                _directionInput.IsEnabled = hasLight;
                _intensityInput.IsEnabled = hasLight;
                _falloffInput.IsEnabled = hasLight;
                _lightRInput.IsEnabled = hasLight;
                _lightGInput.IsEnabled = hasLight;
                _lightBInput.IsEnabled = hasLight;
                _diffuseBoostInput.IsEnabled = hasLight;
                _specularBoostInput.IsEnabled = hasLight;
                _specularPowerInput.IsEnabled = hasLight;

                if (selectedLight != null)
                {
                    _lightTypeCombo.SelectedItem = selectedLight.Type;
                    _lightXInput.Value = selectedLight.X;
                    _lightYInput.Value = selectedLight.Y;
                    _lightZInput.Value = selectedLight.Z;
                    _directionInput.Value = RadiansToDegrees(selectedLight.DirectionRadians);
                    _intensityInput.Value = selectedLight.Intensity;
                    _falloffInput.Value = selectedLight.Falloff;
                    _lightRInput.Value = selectedLight.Color.Red;
                    _lightGInput.Value = selectedLight.Color.Green;
                    _lightBInput.Value = selectedLight.Color.Blue;
                    _diffuseBoostInput.Value = selectedLight.DiffuseBoost;
                    _specularBoostInput.Value = selectedLight.SpecularBoost;
                    _specularPowerInput.Value = selectedLight.SpecularPower;
                }

                ApplyInspectorTabPolicy(tabPolicy, preservedTab);
                UpdateNodeInspectorForSelection(_project.SelectedNode);
                RestoreInspectorPresentationStateForCurrentTab();
                RestoreInspectorFocusStateForCurrentTab(preservedFocus);
                UpdateReadouts();
            }
            finally
            {
                _updatingUi = false;
            }
        }

        private void ApplyInspectorTabPolicy(InspectorRefreshTabPolicy tabPolicy, TabItem? preservedTab)
        {
            if (_inspectorTabControl == null)
            {
                return;
            }

            if (tabPolicy == InspectorRefreshTabPolicy.FollowSceneSelection || preservedTab == null)
            {
                SelectInspectorTabForSceneNode(_project.SelectedNode);
                EnsureSelectedInspectorTabIsVisible();
                return;
            }

            TabItem? preferred = ResolvePreferredVisibleInspectorTab(preservedTab);
            if (preferred != null && !ReferenceEquals(_inspectorTabControl.SelectedItem, preferred))
            {
                _inspectorTabControl.SelectedItem = preferred;
            }

            EnsureSelectedInspectorTabIsVisible();
        }

    }
}
