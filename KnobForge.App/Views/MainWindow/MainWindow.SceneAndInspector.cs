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

            var dialog = new RenderSettingsWindow(_project, _metalViewport.CurrentOrientation, _metalViewport.CurrentCameraState, _metalViewport)
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
                _collarScaleSlider == null ||
                _collarBodyLengthSlider == null ||
                _collarBodyThicknessSlider == null ||
                _collarHeadLengthSlider == null ||
                _collarHeadThicknessSlider == null ||
                _collarRotateSlider == null ||
                _collarMirrorXCheckBox == null ||
                _collarMirrorYCheckBox == null ||
                _collarMirrorZCheckBox == null ||
                _collarOffsetXSlider == null ||
                _collarOffsetYSlider == null ||
                _collarElevationSlider == null ||
                _collarInflateSlider == null ||
                _collarMaterialBaseRSlider == null ||
                _collarMaterialBaseGSlider == null ||
                _collarMaterialBaseBSlider == null ||
                _collarMaterialMetallicSlider == null ||
                _collarMaterialRoughnessSlider == null ||
                _collarMaterialPearlescenceSlider == null ||
                _collarMaterialRustSlider == null ||
                _collarMaterialWearSlider == null ||
                _collarMaterialGunkSlider == null)
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
            _collarScaleSlider.IsEnabled = importedPreset;
            _collarBodyLengthSlider.IsEnabled = importedPreset;
            _collarBodyThicknessSlider.IsEnabled = importedPreset;
            _collarHeadLengthSlider.IsEnabled = importedPreset;
            _collarHeadThicknessSlider.IsEnabled = importedPreset;
            _collarRotateSlider.IsEnabled = importedPreset;
            _collarMirrorXCheckBox.IsEnabled = importedPreset;
            _collarMirrorYCheckBox.IsEnabled = importedPreset;
            _collarMirrorZCheckBox.IsEnabled = importedPreset;
            _collarOffsetXSlider.IsEnabled = importedPreset;
            _collarOffsetYSlider.IsEnabled = importedPreset;
            _collarElevationSlider.IsEnabled = hasModel;
            _collarInflateSlider.IsEnabled = importedPreset;
            if (_collarScaleInputTextBox != null)
            {
                _collarScaleInputTextBox.IsEnabled = _collarScaleSlider.IsEnabled;
            }

            if (_collarBodyLengthInputTextBox != null)
            {
                _collarBodyLengthInputTextBox.IsEnabled = _collarBodyLengthSlider.IsEnabled;
            }

            if (_collarBodyThicknessInputTextBox != null)
            {
                _collarBodyThicknessInputTextBox.IsEnabled = _collarBodyThicknessSlider.IsEnabled;
            }

            if (_collarHeadLengthInputTextBox != null)
            {
                _collarHeadLengthInputTextBox.IsEnabled = _collarHeadLengthSlider.IsEnabled;
            }

            if (_collarHeadThicknessInputTextBox != null)
            {
                _collarHeadThicknessInputTextBox.IsEnabled = _collarHeadThicknessSlider.IsEnabled;
            }

            if (_collarRotateInputTextBox != null)
            {
                _collarRotateInputTextBox.IsEnabled = _collarRotateSlider.IsEnabled;
            }

            if (_collarOffsetXInputTextBox != null)
            {
                _collarOffsetXInputTextBox.IsEnabled = _collarOffsetXSlider.IsEnabled;
            }

            if (_collarOffsetYInputTextBox != null)
            {
                _collarOffsetYInputTextBox.IsEnabled = _collarOffsetYSlider.IsEnabled;
            }

            if (_collarElevationInputTextBox != null)
            {
                _collarElevationInputTextBox.IsEnabled = _collarElevationSlider.IsEnabled;
            }

            if (_collarInflateInputTextBox != null)
            {
                _collarInflateInputTextBox.IsEnabled = _collarInflateSlider.IsEnabled;
            }

            bool collarMaterialEnabled = hasModel;
            _collarMaterialBaseRSlider.IsEnabled = collarMaterialEnabled;
            _collarMaterialBaseGSlider.IsEnabled = collarMaterialEnabled;
            _collarMaterialBaseBSlider.IsEnabled = collarMaterialEnabled;
            _collarMaterialMetallicSlider.IsEnabled = collarMaterialEnabled;
            _collarMaterialRoughnessSlider.IsEnabled = collarMaterialEnabled;
            _collarMaterialPearlescenceSlider.IsEnabled = collarMaterialEnabled;
            _collarMaterialRustSlider.IsEnabled = collarMaterialEnabled;
            _collarMaterialWearSlider.IsEnabled = collarMaterialEnabled;
            _collarMaterialGunkSlider.IsEnabled = collarMaterialEnabled;
            UpdateCollarMeshPathFeedback(preset, _collarMeshPathTextBox.Text, customImportedPreset);
        }

        private void RefreshInspectorFromProject(InspectorRefreshTabPolicy tabPolicy = InspectorRefreshTabPolicy.PreserveCurrentTab)
        {
            if (_lightingModeCombo == null || _lightListBox == null ||
                _removeLightButton == null || _rotationSlider == null || _lightTypeCombo == null ||
                _lightXSlider == null || _lightYSlider == null || _lightZSlider == null ||
                _directionSlider == null || _intensitySlider == null || _falloffSlider == null ||
                _lightRSlider == null || _lightGSlider == null || _lightBSlider == null ||
                _diffuseBoostSlider == null || _specularBoostSlider == null || _specularPowerSlider == null ||
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
                _scratchExposeMetallicSlider == null || _scratchExposeRoughnessSlider == null)
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
                var material = model?.Children.OfType<MaterialNode>().FirstOrDefault();
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
                    _rotationSlider.Value = model.RotationRadians;
                    _modelRadiusSlider.Value = model.Radius;
                    _modelHeightSlider.Value = model.Height;
                    _modelTopScaleSlider.Value = model.TopRadiusScale;
                    _modelBevelSlider.Value = model.Bevel;
                    _bevelCurveSlider.Value = model.BevelCurve;
                    _crownProfileSlider.Value = model.CrownProfile;
                    _bodyTaperSlider.Value = model.BodyTaper;
                    _bodyBulgeSlider.Value = model.BodyBulge;
                    _modelSegmentsSlider.Value = model.RadialSegments;
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

                    if (_sliderBackplateWidthSlider != null)
                    {
                        _sliderBackplateWidthSlider.Value = project.SliderBackplateWidth;
                    }

                    if (_sliderBackplateHeightSlider != null)
                    {
                        _sliderBackplateHeightSlider.Value = project.SliderBackplateHeight;
                    }

                    if (_sliderBackplateThicknessSlider != null)
                    {
                        _sliderBackplateThicknessSlider.Value = project.SliderBackplateThickness;
                    }

                    if (_sliderThumbWidthSlider != null)
                    {
                        _sliderThumbWidthSlider.Value = project.SliderThumbWidth;
                    }

                    if (_sliderThumbHeightSlider != null)
                    {
                        _sliderThumbHeightSlider.Value = project.SliderThumbHeight;
                    }

                    if (_sliderThumbDepthSlider != null)
                    {
                        _sliderThumbDepthSlider.Value = project.SliderThumbDepth;
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

                    if (_toggleStateIndexSlider != null)
                    {
                        int maxStateIndex = project.ToggleStateCount == ToggleAssemblyStateCount.ThreePosition ? 2 : 1;
                        _toggleStateIndexSlider.Maximum = maxStateIndex;
                        _toggleStateIndexSlider.Value = Math.Clamp(project.ToggleStateIndex, 0, maxStateIndex);
                    }

                    if (_toggleMaxAngleSlider != null)
                    {
                        _toggleMaxAngleSlider.Value = project.ToggleMaxAngleDeg;
                    }

                    if (_togglePlateWidthSlider != null)
                    {
                        _togglePlateWidthSlider.Value = project.TogglePlateWidth;
                    }

                    if (_togglePlateHeightSlider != null)
                    {
                        _togglePlateHeightSlider.Value = project.TogglePlateHeight;
                    }

                    if (_togglePlateThicknessSlider != null)
                    {
                        _togglePlateThicknessSlider.Value = project.TogglePlateThickness;
                    }

                    if (_togglePlateOffsetYSlider != null)
                    {
                        _togglePlateOffsetYSlider.Value = project.TogglePlateOffsetY;
                    }

                    if (_togglePlateOffsetZSlider != null)
                    {
                        _togglePlateOffsetZSlider.Value = project.TogglePlateOffsetZ;
                    }

                    if (_toggleBushingRadiusSlider != null)
                    {
                        _toggleBushingRadiusSlider.Value = project.ToggleBushingRadius;
                    }

                    if (_toggleBushingHeightSlider != null)
                    {
                        _toggleBushingHeightSlider.Value = project.ToggleBushingHeight;
                    }

                    if (_toggleBushingSidesSlider != null)
                    {
                        _toggleBushingSidesSlider.Value = project.ToggleBushingSides;
                    }

                    if (_toggleLowerBushingShapeCombo != null)
                    {
                        _toggleLowerBushingShapeCombo.SelectedItem = project.ToggleLowerBushingShape;
                    }

                    if (_toggleUpperBushingShapeCombo != null)
                    {
                        _toggleUpperBushingShapeCombo.SelectedItem = project.ToggleUpperBushingShape;
                    }

                    if (_toggleLowerBushingRadiusScaleSlider != null)
                    {
                        _toggleLowerBushingRadiusScaleSlider.Value = project.ToggleLowerBushingRadiusScale;
                    }

                    if (_toggleLowerBushingHeightRatioSlider != null)
                    {
                        _toggleLowerBushingHeightRatioSlider.Value = project.ToggleLowerBushingHeightRatio;
                    }

                    if (_toggleUpperBushingRadiusScaleSlider != null)
                    {
                        _toggleUpperBushingRadiusScaleSlider.Value = project.ToggleUpperBushingRadiusScale;
                    }

                    if (_toggleUpperBushingHeightRatioSlider != null)
                    {
                        _toggleUpperBushingHeightRatioSlider.Value = project.ToggleUpperBushingHeightRatio;
                    }

                    if (_toggleUpperBushingKnurlAmountSlider != null)
                    {
                        _toggleUpperBushingKnurlAmountSlider.Value = project.ToggleUpperBushingKnurlAmount;
                    }

                    if (_toggleUpperBushingKnurlDensitySlider != null)
                    {
                        _toggleUpperBushingKnurlDensitySlider.Value = project.ToggleUpperBushingKnurlDensity;
                    }

                    if (_toggleUpperBushingKnurlDepthSlider != null)
                    {
                        _toggleUpperBushingKnurlDepthSlider.Value = project.ToggleUpperBushingKnurlDepth;
                    }

                    if (_togglePivotHousingRadiusSlider != null)
                    {
                        _togglePivotHousingRadiusSlider.Value = project.TogglePivotHousingRadius;
                    }

                    if (_togglePivotHousingDepthSlider != null)
                    {
                        _togglePivotHousingDepthSlider.Value = project.TogglePivotHousingDepth;
                    }

                    if (_togglePivotHousingBevelSlider != null)
                    {
                        _togglePivotHousingBevelSlider.Value = project.TogglePivotHousingBevel;
                    }

                    if (_togglePivotBallRadiusSlider != null)
                    {
                        _togglePivotBallRadiusSlider.Value = project.TogglePivotBallRadius;
                    }

                    if (_togglePivotClearanceSlider != null)
                    {
                        _togglePivotClearanceSlider.Value = project.TogglePivotClearance;
                    }

                    if (_toggleInvertBaseWindingCheckBox != null)
                    {
                        _toggleInvertBaseWindingCheckBox.IsChecked = project.ToggleInvertBaseFrontFaceWinding;
                    }

                    if (_toggleInvertLeverWindingCheckBox != null)
                    {
                        _toggleInvertLeverWindingCheckBox.IsChecked = project.ToggleInvertLeverFrontFaceWinding;
                    }

                    if (_toggleLeverLengthSlider != null)
                    {
                        _toggleLeverLengthSlider.Value = project.ToggleLeverLength;
                    }

                    if (_toggleLeverRadiusSlider != null)
                    {
                        _toggleLeverRadiusSlider.Value = project.ToggleLeverRadius;
                    }

                    if (_toggleLeverTopRadiusSlider != null)
                    {
                        _toggleLeverTopRadiusSlider.Value = project.ToggleLeverTopRadius;
                    }

                    if (_toggleLeverSidesSlider != null)
                    {
                        _toggleLeverSidesSlider.Value = project.ToggleLeverSides;
                    }

                    if (_toggleLeverPivotOffsetSlider != null)
                    {
                        _toggleLeverPivotOffsetSlider.Value = project.ToggleLeverPivotOffset;
                    }

                    if (_toggleTipRadiusSlider != null)
                    {
                        _toggleTipRadiusSlider.Value = project.ToggleTipRadius;
                    }

                    if (_toggleTipLatitudeSegmentsSlider != null)
                    {
                        _toggleTipLatitudeSegmentsSlider.Value = project.ToggleTipLatitudeSegments;
                    }

                    if (_toggleTipLongitudeSegmentsSlider != null)
                    {
                        _toggleTipLongitudeSegmentsSlider.Value = project.ToggleTipLongitudeSegments;
                    }

                    if (_toggleTipSleeveEnabledCheckBox != null)
                    {
                        _toggleTipSleeveEnabledCheckBox.IsChecked = project.ToggleTipSleeveEnabled;
                    }

                    if (_toggleTipSleeveLengthSlider != null)
                    {
                        _toggleTipSleeveLengthSlider.Value = project.ToggleTipSleeveLength;
                    }

                    if (_toggleTipSleeveThicknessSlider != null)
                    {
                        _toggleTipSleeveThicknessSlider.Value = project.ToggleTipSleeveThickness;
                    }

                    if (_toggleTipSleeveOuterRadiusSlider != null)
                    {
                        _toggleTipSleeveOuterRadiusSlider.Value = project.ToggleTipSleeveOuterRadius;
                    }

                    if (_toggleTipSleeveCoverageSlider != null)
                    {
                        _toggleTipSleeveCoverageSlider.Value = project.ToggleTipSleeveCoverage;
                    }

                    if (_toggleTipSleeveSidesSlider != null)
                    {
                        _toggleTipSleeveSidesSlider.Value = project.ToggleTipSleeveSides;
                    }

                    if (_toggleTipSleeveStyleCombo != null)
                    {
                        _toggleTipSleeveStyleCombo.SelectedItem = project.ToggleTipSleeveStyle;
                    }

                    if (_toggleTipSleeveTipStyleCombo != null)
                    {
                        _toggleTipSleeveTipStyleCombo.SelectedItem = project.ToggleTipSleeveTipStyle;
                    }

                    if (_toggleTipSleevePatternCountSlider != null)
                    {
                        _toggleTipSleevePatternCountSlider.Value = project.ToggleTipSleevePatternCount;
                    }

                    if (_toggleTipSleevePatternDepthSlider != null)
                    {
                        _toggleTipSleevePatternDepthSlider.Value = project.ToggleTipSleevePatternDepth;
                    }

                    if (_toggleTipSleeveTipAmountSlider != null)
                    {
                        _toggleTipSleeveTipAmountSlider.Value = project.ToggleTipSleeveTipAmount;
                    }

                    if (_toggleTipSleeveColorRSlider != null)
                    {
                        _toggleTipSleeveColorRSlider.Value = project.ToggleTipSleeveColor.X;
                    }

                    if (_toggleTipSleeveColorGSlider != null)
                    {
                        _toggleTipSleeveColorGSlider.Value = project.ToggleTipSleeveColor.Y;
                    }

                    if (_toggleTipSleeveColorBSlider != null)
                    {
                        _toggleTipSleeveColorBSlider.Value = project.ToggleTipSleeveColor.Z;
                    }

                    if (_toggleTipSleeveMetallicSlider != null)
                    {
                        _toggleTipSleeveMetallicSlider.Value = project.ToggleTipSleeveMetallic;
                    }

                    if (_toggleTipSleeveRoughnessSlider != null)
                    {
                        _toggleTipSleeveRoughnessSlider.Value = project.ToggleTipSleeveRoughness;
                    }

                    if (_toggleTipSleevePearlescenceSlider != null)
                    {
                        _toggleTipSleevePearlescenceSlider.Value = project.ToggleTipSleevePearlescence;
                    }

                    if (_toggleTipSleeveDiffuseStrengthSlider != null)
                    {
                        _toggleTipSleeveDiffuseStrengthSlider.Value = project.ToggleTipSleeveDiffuseStrength;
                    }

                    if (_toggleTipSleeveSpecularStrengthSlider != null)
                    {
                        _toggleTipSleeveSpecularStrengthSlider.Value = project.ToggleTipSleeveSpecularStrength;
                    }

                    if (_toggleTipSleeveRustSlider != null)
                    {
                        _toggleTipSleeveRustSlider.Value = project.ToggleTipSleeveRustAmount;
                    }

                    if (_toggleTipSleeveWearSlider != null)
                    {
                        _toggleTipSleeveWearSlider.Value = project.ToggleTipSleeveWearAmount;
                    }

                    if (_toggleTipSleeveGunkSlider != null)
                    {
                        _toggleTipSleeveGunkSlider.Value = project.ToggleTipSleeveGunkAmount;
                    }
                    _spiralRidgeHeightSlider.Value = model.SpiralRidgeHeight;
                    _spiralRidgeWidthSlider.Value = model.SpiralRidgeWidth;
                    _spiralTurnsSlider.Value = model.SpiralTurns;
                    _gripStyleCombo.SelectedItem = model.GripStyle;
                    _gripTypeCombo.SelectedItem = model.GripType;
                    _gripStartSlider.Value = model.GripStart;
                    _gripHeightSlider.Value = model.GripHeight;
                    _gripDensitySlider.Value = model.GripDensity;
                    _gripPitchSlider.Value = model.GripPitch;
                    _gripDepthSlider.Value = model.GripDepth;
                    _gripWidthSlider.Value = model.GripWidth;
                    _gripSharpnessSlider.Value = model.GripSharpness;
                    if (collar != null)
                    {
                        _collarEnabledCheckBox.IsChecked = collar.Enabled;
                        CollarPresetOption collarOption = ResolveCollarPresetOptionForState(collar.Preset, collar.ImportedMeshPath);
                        _collarPresetCombo.SelectedItem = collarOption;
                        _lastSelectableCollarPresetOption = collarOption;
                        _collarMeshPathTextBox.Text = collarOption.ResolveImportedMeshPath(collar.ImportedMeshPath);
                        _collarScaleSlider.Value = collar.ImportedScale;
                        _collarBodyLengthSlider.Value = collar.ImportedBodyLengthScale;
                        _collarBodyThicknessSlider.Value = collar.ImportedBodyThicknessScale;
                        _collarHeadLengthSlider.Value = collar.ImportedHeadLengthScale;
                        _collarHeadThicknessSlider.Value = collar.ImportedHeadThicknessScale;
                        _collarRotateSlider.Value = RadiansToDegrees(collar.ImportedRotationRadians);
                        _collarMirrorXCheckBox.IsChecked = collar.ImportedMirrorX;
                        _collarMirrorYCheckBox.IsChecked = collar.ImportedMirrorY;
                        _collarMirrorZCheckBox.IsChecked = collar.ImportedMirrorZ;
                        _collarOffsetXSlider.Value = collar.ImportedOffsetXRatio;
                        _collarOffsetYSlider.Value = collar.ImportedOffsetYRatio;
                        _collarElevationSlider.Value = collar.ElevationRatio;
                        _collarInflateSlider.Value = collar.ImportedInflateRatio;
                        _collarMaterialBaseRSlider.Value = collar.BaseColor.X;
                        _collarMaterialBaseGSlider.Value = collar.BaseColor.Y;
                        _collarMaterialBaseBSlider.Value = collar.BaseColor.Z;
                        _collarMaterialMetallicSlider.Value = collar.Metallic;
                        _collarMaterialRoughnessSlider.Value = collar.Roughness;
                        _collarMaterialPearlescenceSlider.Value = collar.Pearlescence;
                        _collarMaterialRustSlider.Value = collar.RustAmount;
                        _collarMaterialWearSlider.Value = collar.WearAmount;
                        _collarMaterialGunkSlider.Value = collar.GunkAmount;
                    }
                    else
                    {
                        _collarEnabledCheckBox.IsChecked = false;
                        CollarPresetOption noneOption = ResolveCollarPresetOptionForState(CollarPreset.None, null);
                        _collarPresetCombo.SelectedItem = noneOption;
                        _lastSelectableCollarPresetOption = noneOption;
                        _collarMeshPathTextBox.Text = string.Empty;
                        _collarScaleSlider.Value = 1.0;
                        _collarBodyLengthSlider.Value = 1.0;
                        _collarBodyThicknessSlider.Value = 1.0;
                        _collarHeadLengthSlider.Value = 1.0;
                        _collarHeadThicknessSlider.Value = 1.0;
                        _collarRotateSlider.Value = 0.0;
                        _collarMirrorXCheckBox.IsChecked = false;
                        _collarMirrorYCheckBox.IsChecked = false;
                        _collarMirrorZCheckBox.IsChecked = false;
                        _collarOffsetXSlider.Value = 0.0;
                        _collarOffsetYSlider.Value = 0.0;
                        _collarElevationSlider.Value = 0.0;
                        _collarInflateSlider.Value = 0.0;
                        _collarMaterialBaseRSlider.Value = 0.74;
                        _collarMaterialBaseGSlider.Value = 0.74;
                        _collarMaterialBaseBSlider.Value = 0.70;
                        _collarMaterialMetallicSlider.Value = 0.96;
                        _collarMaterialRoughnessSlider.Value = 0.32;
                        _collarMaterialPearlescenceSlider.Value = 0.0;
                        _collarMaterialRustSlider.Value = 0.0;
                        _collarMaterialWearSlider.Value = 0.0;
                        _collarMaterialGunkSlider.Value = 0.0;
                    }
                    _indicatorEnabledCheckBox.IsChecked = model.IndicatorEnabled;
                    if (_indicatorCadWallsCheckBox != null)
                    {
                        _indicatorCadWallsCheckBox.IsChecked = model.IndicatorCadWallsEnabled;
                    }
                    _indicatorShapeCombo.SelectedItem = model.IndicatorShape;
                    _indicatorReliefCombo.SelectedItem = model.IndicatorRelief;
                    _indicatorProfileCombo.SelectedItem = model.IndicatorProfile;
                    _indicatorWidthSlider.Value = model.IndicatorWidthRatio;
                    _indicatorLengthSlider.Value = model.IndicatorLengthRatioTop;
                    _indicatorPositionSlider.Value = model.IndicatorPositionRatio;
                    _indicatorThicknessSlider.Value = model.IndicatorThicknessRatio;
                    _indicatorRoundnessSlider.Value = model.IndicatorRoundness;
                    _indicatorColorBlendSlider.Value = model.IndicatorColorBlend;
                    _indicatorColorRSlider.Value = model.IndicatorColor.X;
                    _indicatorColorGSlider.Value = model.IndicatorColor.Y;
                    _indicatorColorBSlider.Value = model.IndicatorColor.Z;
                    if (_indicatorAssemblyEnabledCheckBox != null)
                    {
                        _indicatorAssemblyEnabledCheckBox.IsChecked = project.IndicatorAssemblyEnabled;
                    }
                    if (_indicatorBaseWidthSlider != null)
                    {
                        _indicatorBaseWidthSlider.Value = project.IndicatorBaseWidth;
                    }
                    if (_indicatorBaseHeightSlider != null)
                    {
                        _indicatorBaseHeightSlider.Value = project.IndicatorBaseHeight;
                    }
                    if (_indicatorBaseThicknessSlider != null)
                    {
                        _indicatorBaseThicknessSlider.Value = project.IndicatorBaseThickness;
                    }
                    if (_indicatorHousingRadiusSlider != null)
                    {
                        _indicatorHousingRadiusSlider.Value = project.IndicatorHousingRadius;
                    }
                    if (_indicatorHousingHeightSlider != null)
                    {
                        _indicatorHousingHeightSlider.Value = project.IndicatorHousingHeight;
                    }
                    if (_indicatorLensRadiusSlider != null)
                    {
                        _indicatorLensRadiusSlider.Value = project.IndicatorLensRadius;
                    }
                    if (_indicatorLensHeightSlider != null)
                    {
                        _indicatorLensHeightSlider.Value = project.IndicatorLensHeight;
                    }
                    if (_indicatorLensTransmissionSlider != null)
                    {
                        _indicatorLensTransmissionSlider.Value = project.IndicatorLensTransmission;
                    }
                    if (_indicatorLensIorSlider != null)
                    {
                        _indicatorLensIorSlider.Value = project.IndicatorLensIor;
                    }
                    if (_indicatorLensThicknessSlider != null)
                    {
                        _indicatorLensThicknessSlider.Value = project.IndicatorLensThickness;
                    }
                    if (_indicatorLensAbsorptionSlider != null)
                    {
                        _indicatorLensAbsorptionSlider.Value = project.IndicatorLensAbsorption;
                    }
                    if (_indicatorLensSurfaceRoughnessSlider != null)
                    {
                        _indicatorLensSurfaceRoughnessSlider.Value = project.IndicatorLensSurfaceRoughness;
                    }
                    if (_indicatorLensSurfaceSpecularSlider != null)
                    {
                        _indicatorLensSurfaceSpecularSlider.Value = project.IndicatorLensSurfaceSpecularStrength;
                    }
                    if (_indicatorLensTintRSlider != null)
                    {
                        _indicatorLensTintRSlider.Value = project.IndicatorLensTint.X;
                    }
                    if (_indicatorLensTintGSlider != null)
                    {
                        _indicatorLensTintGSlider.Value = project.IndicatorLensTint.Y;
                    }
                    if (_indicatorLensTintBSlider != null)
                    {
                        _indicatorLensTintBSlider.Value = project.IndicatorLensTint.Z;
                    }
                    if (_indicatorReflectorBaseRadiusSlider != null)
                    {
                        _indicatorReflectorBaseRadiusSlider.Value = project.IndicatorReflectorBaseRadius;
                    }
                    if (_indicatorReflectorTopRadiusSlider != null)
                    {
                        _indicatorReflectorTopRadiusSlider.Value = project.IndicatorReflectorTopRadius;
                    }
                    if (_indicatorReflectorDepthSlider != null)
                    {
                        _indicatorReflectorDepthSlider.Value = project.IndicatorReflectorDepth;
                    }
                    if (_indicatorEmitterRadiusSlider != null)
                    {
                        _indicatorEmitterRadiusSlider.Value = project.IndicatorEmitterRadius;
                    }
                    if (_indicatorEmitterSpreadSlider != null)
                    {
                        _indicatorEmitterSpreadSlider.Value = project.IndicatorEmitterSpread;
                    }
                    if (_indicatorEmitterDepthSlider != null)
                    {
                        _indicatorEmitterDepthSlider.Value = project.IndicatorEmitterDepth;
                    }
                    if (_indicatorEmitterCountSlider != null)
                    {
                        _indicatorEmitterCountSlider.Value = project.IndicatorEmitterCount;
                    }
                    if (_indicatorRadialSegmentsSlider != null)
                    {
                        _indicatorRadialSegmentsSlider.Value = project.IndicatorRadialSegments;
                    }
                    if (_indicatorLensLatitudeSegmentsSlider != null)
                    {
                        _indicatorLensLatitudeSegmentsSlider.Value = project.IndicatorLensLatitudeSegments;
                    }
                    if (_indicatorLensLongitudeSegmentsSlider != null)
                    {
                        _indicatorLensLongitudeSegmentsSlider.Value = project.IndicatorLensLongitudeSegments;
                    }
                    if (_indicatorDynamicLightsEnabledCheckBox != null)
                    {
                        _indicatorDynamicLightsEnabledCheckBox.IsChecked = project.DynamicLightRig.Enabled;
                    }
                    if (_indicatorLightAnimationModeCombo != null)
                    {
                        _indicatorLightAnimationModeCombo.SelectedItem = project.DynamicLightRig.AnimationMode;
                    }
                    if (_indicatorLightAnimationSpeedSlider != null)
                    {
                        _indicatorLightAnimationSpeedSlider.Value = project.DynamicLightRig.AnimationSpeed;
                    }
                    if (_indicatorLightFlickerAmountSlider != null)
                    {
                        _indicatorLightFlickerAmountSlider.Value = project.DynamicLightRig.FlickerAmount;
                    }
                    if (_indicatorLightFlickerDropoutSlider != null)
                    {
                        _indicatorLightFlickerDropoutSlider.Value = project.DynamicLightRig.FlickerDropoutChance;
                    }
                    if (_indicatorLightFlickerSmoothingSlider != null)
                    {
                        _indicatorLightFlickerSmoothingSlider.Value = project.DynamicLightRig.FlickerSmoothing;
                    }
                    if (_indicatorLightFlickerSeedSlider != null)
                    {
                        _indicatorLightFlickerSeedSlider.Value = project.DynamicLightRig.FlickerSeed;
                    }
                    RefreshIndicatorEmitterSourceControlsFromProject();
                }

                bool hasModel = model != null;
                bool indicatorProject = project.ProjectType == InteractorProjectType.IndicatorLight;
                UpdateIndicatorPanelVisibility();
                UpdateCollarControlEnablement(hasModel, collar?.Preset ?? CollarPreset.None);
                _indicatorEnabledCheckBox.IsEnabled = hasModel && !indicatorProject;
                if (_indicatorCadWallsCheckBox != null)
                {
                    _indicatorCadWallsCheckBox.IsEnabled = hasModel && !indicatorProject;
                }
                _indicatorShapeCombo.IsEnabled = hasModel && !indicatorProject;
                _indicatorReliefCombo.IsEnabled = hasModel && !indicatorProject;
                _indicatorProfileCombo.IsEnabled = hasModel && !indicatorProject;
                _indicatorWidthSlider.IsEnabled = hasModel && !indicatorProject;
                _indicatorLengthSlider.IsEnabled = hasModel && !indicatorProject;
                _indicatorPositionSlider.IsEnabled = hasModel && !indicatorProject;
                _indicatorThicknessSlider.IsEnabled = hasModel && !indicatorProject;
                _indicatorRoundnessSlider.IsEnabled = hasModel && !indicatorProject;
                _indicatorColorBlendSlider.IsEnabled = hasModel && !indicatorProject;
                _indicatorColorRSlider.IsEnabled = hasModel && !indicatorProject;
                _indicatorColorGSlider.IsEnabled = hasModel && !indicatorProject;
                _indicatorColorBSlider.IsEnabled = hasModel && !indicatorProject;
                if (_indicatorAssemblyEnabledCheckBox != null)
                {
                    _indicatorAssemblyEnabledCheckBox.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorBaseWidthSlider != null)
                {
                    _indicatorBaseWidthSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorBaseHeightSlider != null)
                {
                    _indicatorBaseHeightSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorBaseThicknessSlider != null)
                {
                    _indicatorBaseThicknessSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorHousingRadiusSlider != null)
                {
                    _indicatorHousingRadiusSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorHousingHeightSlider != null)
                {
                    _indicatorHousingHeightSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLensRadiusSlider != null)
                {
                    _indicatorLensRadiusSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLensHeightSlider != null)
                {
                    _indicatorLensHeightSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorReflectorBaseRadiusSlider != null)
                {
                    _indicatorReflectorBaseRadiusSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorReflectorTopRadiusSlider != null)
                {
                    _indicatorReflectorTopRadiusSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorReflectorDepthSlider != null)
                {
                    _indicatorReflectorDepthSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorEmitterRadiusSlider != null)
                {
                    _indicatorEmitterRadiusSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorEmitterSpreadSlider != null)
                {
                    _indicatorEmitterSpreadSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorEmitterDepthSlider != null)
                {
                    _indicatorEmitterDepthSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorEmitterCountSlider != null)
                {
                    _indicatorEmitterCountSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorRadialSegmentsSlider != null)
                {
                    _indicatorRadialSegmentsSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLensLatitudeSegmentsSlider != null)
                {
                    _indicatorLensLatitudeSegmentsSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLensLongitudeSegmentsSlider != null)
                {
                    _indicatorLensLongitudeSegmentsSlider.IsEnabled = hasModel && indicatorProject;
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
                if (_indicatorLightAnimationSpeedSlider != null)
                {
                    _indicatorLightAnimationSpeedSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightFlickerAmountSlider != null)
                {
                    _indicatorLightFlickerAmountSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightFlickerDropoutSlider != null)
                {
                    _indicatorLightFlickerDropoutSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightFlickerSmoothingSlider != null)
                {
                    _indicatorLightFlickerSmoothingSlider.IsEnabled = hasModel && indicatorProject;
                }
                if (_indicatorLightFlickerSeedSlider != null)
                {
                    _indicatorLightFlickerSeedSlider.IsEnabled = hasModel && indicatorProject;
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
                if (_indicatorEmitterSourcePhaseOffsetSlider != null)
                {
                    _indicatorEmitterSourcePhaseOffsetSlider.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceXSlider != null)
                {
                    _indicatorEmitterSourceXSlider.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceYSlider != null)
                {
                    _indicatorEmitterSourceYSlider.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceZSlider != null)
                {
                    _indicatorEmitterSourceZSlider.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceIntensitySlider != null)
                {
                    _indicatorEmitterSourceIntensitySlider.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceRadiusSlider != null)
                {
                    _indicatorEmitterSourceRadiusSlider.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceFalloffSlider != null)
                {
                    _indicatorEmitterSourceFalloffSlider.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceRSlider != null)
                {
                    _indicatorEmitterSourceRSlider.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceGSlider != null)
                {
                    _indicatorEmitterSourceGSlider.IsEnabled = hasModel && hasEmitterSources;
                }
                if (_indicatorEmitterSourceBSlider != null)
                {
                    _indicatorEmitterSourceBSlider.IsEnabled = hasModel && hasEmitterSources;
                }

                bool hasMaterial = material != null;
                _materialBaseRSlider.IsEnabled = hasMaterial;
                _materialBaseGSlider.IsEnabled = hasMaterial;
                _materialBaseBSlider.IsEnabled = hasMaterial;
                _materialRegionCombo.IsEnabled = hasMaterial;
                _materialMetallicSlider.IsEnabled = hasMaterial;
                _materialRoughnessSlider.IsEnabled = hasMaterial;
                _materialPearlescenceSlider.IsEnabled = hasMaterial;
                _materialRustSlider.IsEnabled = hasMaterial;
                _materialWearSlider.IsEnabled = hasMaterial;
                _materialGunkSlider.IsEnabled = hasMaterial;
                _materialBrushStrengthSlider.IsEnabled = hasMaterial;
                _materialBrushDensitySlider.IsEnabled = hasMaterial;
                _materialCharacterSlider.IsEnabled = hasMaterial;
                _spiralNormalInfluenceCheckBox.IsEnabled = hasMaterial;
                _basisDebugModeCombo.IsEnabled = hasMaterial;
                _microLodFadeStartSlider.IsEnabled = hasMaterial;
                _microLodFadeEndSlider.IsEnabled = hasMaterial;
                _microRoughnessLodBoostSlider.IsEnabled = hasMaterial;
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

                if (_sliderBackplateWidthSlider != null)
                {
                    _sliderBackplateWidthSlider.IsEnabled = hasModel;
                }

                if (_sliderBackplateHeightSlider != null)
                {
                    _sliderBackplateHeightSlider.IsEnabled = hasModel;
                }

                if (_sliderBackplateThicknessSlider != null)
                {
                    _sliderBackplateThicknessSlider.IsEnabled = hasModel;
                }

                if (_sliderThumbWidthSlider != null)
                {
                    _sliderThumbWidthSlider.IsEnabled = hasModel;
                }

                if (_sliderThumbHeightSlider != null)
                {
                    _sliderThumbHeightSlider.IsEnabled = hasModel;
                }

                if (_sliderThumbDepthSlider != null)
                {
                    _sliderThumbDepthSlider.IsEnabled = hasModel;
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

                if (_toggleStateIndexSlider != null)
                {
                    _toggleStateIndexSlider.IsEnabled = hasModel;
                }

                if (_toggleMaxAngleSlider != null)
                {
                    _toggleMaxAngleSlider.IsEnabled = hasModel;
                }

                if (_togglePlateWidthSlider != null)
                {
                    _togglePlateWidthSlider.IsEnabled = hasModel;
                }

                if (_togglePlateHeightSlider != null)
                {
                    _togglePlateHeightSlider.IsEnabled = hasModel;
                }

                if (_togglePlateThicknessSlider != null)
                {
                    _togglePlateThicknessSlider.IsEnabled = hasModel;
                }

                if (_togglePlateOffsetYSlider != null)
                {
                    _togglePlateOffsetYSlider.IsEnabled = hasModel;
                }

                if (_togglePlateOffsetZSlider != null)
                {
                    _togglePlateOffsetZSlider.IsEnabled = hasModel;
                }

                if (_toggleBushingRadiusSlider != null)
                {
                    _toggleBushingRadiusSlider.IsEnabled = hasModel;
                }

                if (_toggleBushingHeightSlider != null)
                {
                    _toggleBushingHeightSlider.IsEnabled = hasModel;
                }

                if (_toggleBushingSidesSlider != null)
                {
                    _toggleBushingSidesSlider.IsEnabled = hasModel;
                }

                if (_toggleLowerBushingShapeCombo != null)
                {
                    _toggleLowerBushingShapeCombo.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingShapeCombo != null)
                {
                    _toggleUpperBushingShapeCombo.IsEnabled = hasModel;
                }

                if (_toggleLowerBushingRadiusScaleSlider != null)
                {
                    _toggleLowerBushingRadiusScaleSlider.IsEnabled = hasModel;
                }

                if (_toggleLowerBushingHeightRatioSlider != null)
                {
                    _toggleLowerBushingHeightRatioSlider.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingRadiusScaleSlider != null)
                {
                    _toggleUpperBushingRadiusScaleSlider.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingHeightRatioSlider != null)
                {
                    _toggleUpperBushingHeightRatioSlider.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingKnurlAmountSlider != null)
                {
                    _toggleUpperBushingKnurlAmountSlider.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingKnurlDensitySlider != null)
                {
                    _toggleUpperBushingKnurlDensitySlider.IsEnabled = hasModel;
                }

                if (_toggleUpperBushingKnurlDepthSlider != null)
                {
                    _toggleUpperBushingKnurlDepthSlider.IsEnabled = hasModel;
                }

                if (_togglePivotHousingRadiusSlider != null)
                {
                    _togglePivotHousingRadiusSlider.IsEnabled = hasModel;
                }

                if (_togglePivotHousingDepthSlider != null)
                {
                    _togglePivotHousingDepthSlider.IsEnabled = hasModel;
                }

                if (_togglePivotHousingBevelSlider != null)
                {
                    _togglePivotHousingBevelSlider.IsEnabled = hasModel;
                }

                if (_togglePivotBallRadiusSlider != null)
                {
                    _togglePivotBallRadiusSlider.IsEnabled = hasModel;
                }

                if (_togglePivotClearanceSlider != null)
                {
                    _togglePivotClearanceSlider.IsEnabled = hasModel;
                }

                if (_toggleLeverLengthSlider != null)
                {
                    _toggleLeverLengthSlider.IsEnabled = hasModel;
                }

                if (_toggleLeverRadiusSlider != null)
                {
                    _toggleLeverRadiusSlider.IsEnabled = hasModel;
                }

                if (_toggleLeverTopRadiusSlider != null)
                {
                    _toggleLeverTopRadiusSlider.IsEnabled = hasModel;
                }

                if (_toggleLeverSidesSlider != null)
                {
                    _toggleLeverSidesSlider.IsEnabled = hasModel;
                }

                if (_toggleLeverPivotOffsetSlider != null)
                {
                    _toggleLeverPivotOffsetSlider.IsEnabled = hasModel;
                }

                if (_toggleTipRadiusSlider != null)
                {
                    _toggleTipRadiusSlider.IsEnabled = hasModel;
                }

                if (_toggleTipLatitudeSegmentsSlider != null)
                {
                    _toggleTipLatitudeSegmentsSlider.IsEnabled = hasModel;
                }

                if (_toggleTipLongitudeSegmentsSlider != null)
                {
                    _toggleTipLongitudeSegmentsSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveEnabledCheckBox != null)
                {
                    _toggleTipSleeveEnabledCheckBox.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveLengthSlider != null)
                {
                    _toggleTipSleeveLengthSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveThicknessSlider != null)
                {
                    _toggleTipSleeveThicknessSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveOuterRadiusSlider != null)
                {
                    _toggleTipSleeveOuterRadiusSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveCoverageSlider != null)
                {
                    _toggleTipSleeveCoverageSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveSidesSlider != null)
                {
                    _toggleTipSleeveSidesSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveStyleCombo != null)
                {
                    _toggleTipSleeveStyleCombo.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveTipStyleCombo != null)
                {
                    _toggleTipSleeveTipStyleCombo.IsEnabled = hasModel;
                }

                if (_toggleTipSleevePatternCountSlider != null)
                {
                    _toggleTipSleevePatternCountSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleevePatternDepthSlider != null)
                {
                    _toggleTipSleevePatternDepthSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveTipAmountSlider != null)
                {
                    _toggleTipSleeveTipAmountSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveColorRSlider != null)
                {
                    _toggleTipSleeveColorRSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveColorGSlider != null)
                {
                    _toggleTipSleeveColorGSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveColorBSlider != null)
                {
                    _toggleTipSleeveColorBSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveMetallicSlider != null)
                {
                    _toggleTipSleeveMetallicSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveRoughnessSlider != null)
                {
                    _toggleTipSleeveRoughnessSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleevePearlescenceSlider != null)
                {
                    _toggleTipSleevePearlescenceSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveDiffuseStrengthSlider != null)
                {
                    _toggleTipSleeveDiffuseStrengthSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveSpecularStrengthSlider != null)
                {
                    _toggleTipSleeveSpecularStrengthSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveRustSlider != null)
                {
                    _toggleTipSleeveRustSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveWearSlider != null)
                {
                    _toggleTipSleeveWearSlider.IsEnabled = hasModel;
                }

                if (_toggleTipSleeveGunkSlider != null)
                {
                    _toggleTipSleeveGunkSlider.IsEnabled = hasModel;
                }
                _brushSizeSlider.IsEnabled = hasModel;
                _brushOpacitySlider.IsEnabled = hasModel;
                _brushDarknessSlider.IsEnabled = hasModel;
                _brushSpreadSlider.IsEnabled = hasModel;
                _paintCoatMetallicSlider.IsEnabled = hasModel;
                _paintCoatRoughnessSlider.IsEnabled = hasModel;
                _clearCoatAmountSlider.IsEnabled = hasModel;
                _clearCoatRoughnessSlider.IsEnabled = hasModel;
                _anisotropyAngleSlider.IsEnabled = hasModel;
                _scratchWidthSlider.IsEnabled = hasModel;
                _scratchDepthSlider.IsEnabled = hasModel;
                _scratchResistanceSlider.IsEnabled = hasModel;
                _scratchDepthRampSlider.IsEnabled = hasModel;
                _scratchExposeColorRSlider.IsEnabled = hasModel;
                _scratchExposeColorGSlider.IsEnabled = hasModel;
                _scratchExposeColorBSlider.IsEnabled = hasModel;
                _scratchExposeMetallicSlider.IsEnabled = hasModel;
                _scratchExposeRoughnessSlider.IsEnabled = hasModel;
                if (_brushSizeInputTextBox != null)
                {
                    _brushSizeInputTextBox.IsEnabled = hasModel;
                }

                if (_brushOpacityInputTextBox != null)
                {
                    _brushOpacityInputTextBox.IsEnabled = hasModel;
                }

                if (_brushDarknessInputTextBox != null)
                {
                    _brushDarknessInputTextBox.IsEnabled = hasModel;
                }

                if (_brushSpreadInputTextBox != null)
                {
                    _brushSpreadInputTextBox.IsEnabled = hasModel;
                }

                if (_paintCoatMetallicInputTextBox != null)
                {
                    _paintCoatMetallicInputTextBox.IsEnabled = hasModel;
                }

                if (_paintCoatRoughnessInputTextBox != null)
                {
                    _paintCoatRoughnessInputTextBox.IsEnabled = hasModel;
                }

                if (_clearCoatAmountInputTextBox != null)
                {
                    _clearCoatAmountInputTextBox.IsEnabled = hasModel;
                }

                if (_clearCoatRoughnessInputTextBox != null)
                {
                    _clearCoatRoughnessInputTextBox.IsEnabled = hasModel;
                }

                if (_anisotropyAngleInputTextBox != null)
                {
                    _anisotropyAngleInputTextBox.IsEnabled = hasModel;
                }

                if (_scratchWidthInputTextBox != null)
                {
                    _scratchWidthInputTextBox.IsEnabled = hasModel;
                }

                if (_scratchDepthInputTextBox != null)
                {
                    _scratchDepthInputTextBox.IsEnabled = hasModel;
                }

                if (_scratchResistanceInputTextBox != null)
                {
                    _scratchResistanceInputTextBox.IsEnabled = hasModel;
                }

                if (_scratchDepthRampInputTextBox != null)
                {
                    _scratchDepthRampInputTextBox.IsEnabled = hasModel;
                }

                if (_scratchExposeColorRInputTextBox != null)
                {
                    _scratchExposeColorRInputTextBox.IsEnabled = hasModel;
                }

                if (_scratchExposeColorGInputTextBox != null)
                {
                    _scratchExposeColorGInputTextBox.IsEnabled = hasModel;
                }

                if (_scratchExposeColorBInputTextBox != null)
                {
                    _scratchExposeColorBInputTextBox.IsEnabled = hasModel;
                }

                if (_scratchExposeMetallicInputTextBox != null)
                {
                    _scratchExposeMetallicInputTextBox.IsEnabled = hasModel;
                }

                if (_scratchExposeRoughnessInputTextBox != null)
                {
                    _scratchExposeRoughnessInputTextBox.IsEnabled = hasModel;
                }
                if (_clearPaintMaskButton != null)
                {
                    _clearPaintMaskButton.IsEnabled = hasModel;
                }

                if (material != null)
                {
                    ApplyMaterialRegionValuesToSliders(material);
                    _materialPearlescenceSlider.Value = material.Pearlescence;
                    _materialRustSlider.Value = material.RustAmount;
                    _materialWearSlider.Value = material.WearAmount;
                    _materialGunkSlider.Value = material.GunkAmount;
                    _materialBrushStrengthSlider.Value = material.RadialBrushStrength;
                    _materialBrushDensitySlider.Value = material.RadialBrushDensity;
                    _materialCharacterSlider.Value = material.SurfaceCharacter;
                }

                _spiralNormalInfluenceCheckBox.IsChecked = project.SpiralNormalInfluenceEnabled;
                _basisDebugModeCombo.SelectedItem = project.BasisDebug;
                _microLodFadeStartSlider.Value = project.SpiralNormalLodFadeStart;
                _microLodFadeEndSlider.Value = project.SpiralNormalLodFadeEnd;
                _microRoughnessLodBoostSlider.Value = project.SpiralRoughnessLodBoost;

                _envIntensitySlider.Value = project.EnvironmentIntensity;
                _envRoughnessMixSlider.Value = project.EnvironmentRoughnessMix;
                _envTopRSlider.Value = project.EnvironmentTopColor.X;
                _envTopGSlider.Value = project.EnvironmentTopColor.Y;
                _envTopBSlider.Value = project.EnvironmentTopColor.Z;
                _envBottomRSlider.Value = project.EnvironmentBottomColor.X;
                _envBottomGSlider.Value = project.EnvironmentBottomColor.Y;
                _envBottomBSlider.Value = project.EnvironmentBottomColor.Z;
                _shadowEnabledCheckBox.IsChecked = project.ShadowsEnabled;
                _shadowSourceModeCombo.SelectedItem = project.ShadowMode;
                _shadowStrengthSlider.Value = project.ShadowStrength;
                _shadowSoftnessSlider.Value = project.ShadowSoftness;
                _shadowDistanceSlider.Value = project.ShadowDistance;
                _shadowScaleSlider.Value = project.ShadowScale;
                _shadowQualitySlider.Value = project.ShadowQuality;
                _shadowGraySlider.Value = project.ShadowGray;
                _shadowDiffuseInfluenceSlider.Value = project.ShadowDiffuseInfluence;
                _brushPaintEnabledCheckBox.IsChecked = project.BrushPaintingEnabled;
                _brushPaintChannelCombo.SelectedItem = project.BrushChannel;
                _brushTypeCombo.SelectedItem = project.BrushType;
                _brushPaintColorPicker.Color = ToAvaloniaColor(project.PaintColor);
                _scratchAbrasionTypeCombo.SelectedItem = project.ScratchAbrasionType;
                _brushSizeSlider.Value = project.BrushSizePx;
                _brushOpacitySlider.Value = project.BrushOpacity;
                _brushDarknessSlider.Value = project.BrushDarkness;
                _brushSpreadSlider.Value = project.BrushSpread;
                _paintCoatMetallicSlider.Value = project.PaintCoatMetallic;
                _paintCoatRoughnessSlider.Value = project.PaintCoatRoughness;
                _clearCoatAmountSlider.Value = project.ClearCoatAmount;
                _clearCoatRoughnessSlider.Value = project.ClearCoatRoughness;
                _anisotropyAngleSlider.Value = project.AnisotropyAngleDegrees;
                _scratchWidthSlider.Value = project.ScratchWidthPx;
                _scratchDepthSlider.Value = project.ScratchDepth;
                _scratchResistanceSlider.Value = project.ScratchDragResistance;
                _scratchDepthRampSlider.Value = project.ScratchDepthRamp;
                _scratchExposeColorRSlider.Value = project.ScratchExposeColor.X;
                _scratchExposeColorGSlider.Value = project.ScratchExposeColor.Y;
                _scratchExposeColorBSlider.Value = project.ScratchExposeColor.Z;
                _scratchExposeMetallicSlider.Value = project.ScratchExposeMetallic;
                _scratchExposeRoughnessSlider.Value = project.ScratchExposeRoughness;
                UpdateBrushContextUi();
                _metalViewport?.RefreshPaintHud();

                var selectedLight = project.SelectedLight;
                bool hasLight = selectedLight != null;

                _lightTypeCombo.IsEnabled = hasLight;
                _lightXSlider.IsEnabled = hasLight;
                _lightYSlider.IsEnabled = hasLight;
                _lightZSlider.IsEnabled = hasLight;
                _directionSlider.IsEnabled = hasLight;
                _intensitySlider.IsEnabled = hasLight;
                _falloffSlider.IsEnabled = hasLight;
                _lightRSlider.IsEnabled = hasLight;
                _lightGSlider.IsEnabled = hasLight;
                _lightBSlider.IsEnabled = hasLight;
                _diffuseBoostSlider.IsEnabled = hasLight;
                _specularBoostSlider.IsEnabled = hasLight;
                _specularPowerSlider.IsEnabled = hasLight;

                if (selectedLight != null)
                {
                    _lightTypeCombo.SelectedItem = selectedLight.Type;
                    _lightXSlider.Value = selectedLight.X;
                    _lightYSlider.Value = selectedLight.Y;
                    _lightZSlider.Value = selectedLight.Z;
                    _directionSlider.Value = RadiansToDegrees(selectedLight.DirectionRadians);
                    _intensitySlider.Value = selectedLight.Intensity;
                    _falloffSlider.Value = selectedLight.Falloff;
                    _lightRSlider.Value = selectedLight.Color.Red;
                    _lightGSlider.Value = selectedLight.Color.Green;
                    _lightBSlider.Value = selectedLight.Color.Blue;
                    _diffuseBoostSlider.Value = selectedLight.DiffuseBoost;
                    _specularBoostSlider.Value = selectedLight.SpecularBoost;
                    _specularPowerSlider.Value = selectedLight.SpecularPower;
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
