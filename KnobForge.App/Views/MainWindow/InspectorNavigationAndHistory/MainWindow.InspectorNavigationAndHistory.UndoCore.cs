using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using KnobForge.Core;
using KnobForge.Core.Scene;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private const int MaxUndoSnapshots = 64;
        private static readonly JsonSerializerOptions UndoFingerprintJsonOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly List<InspectorUndoSnapshot> _undoSnapshots = new();
        private readonly List<InspectorUndoSnapshot> _redoSnapshots = new();
        private InspectorUndoSnapshot? _currentUndoSnapshot;
        private string _currentUndoFingerprint = string.Empty;
        private bool _undoRedoInitialized;
        private bool _applyingUndoRedo;

        private void InitializeUndoRedoSupport()
        {
            AddHandler(InputElement.KeyDownEvent, OnUndoRedoKeyDown, RoutingStrategies.Tunnel);
            InitializeUndoRedoHistory(resetStacks: true);
            UpdateUndoRedoButtonState();
        }

        private void OnUndoRedoKeyDown(object? sender, KeyEventArgs e)
        {
            if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
            {
                return;
            }

            bool commandDown = e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (commandDown && e.Key == Key.Z)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    ExecuteRedo();
                }
                else
                {
                    ExecuteUndo();
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ExecuteRedo();
                e.Handled = true;
            }
        }

        private void ExecuteUndo()
        {
            if (_undoSnapshots.Count == 0)
            {
                return;
            }

            InspectorUndoSnapshot currentSnapshot = CaptureInspectorUndoSnapshot();
            InspectorUndoSnapshot targetSnapshot = _undoSnapshots[^1];
            _undoSnapshots.RemoveAt(_undoSnapshots.Count - 1);
            _redoSnapshots.Add(currentSnapshot);
            ApplyUndoSnapshot(targetSnapshot);
        }

        private void ExecuteRedo()
        {
            if (_redoSnapshots.Count == 0)
            {
                return;
            }

            InspectorUndoSnapshot currentSnapshot = CaptureInspectorUndoSnapshot();
            InspectorUndoSnapshot targetSnapshot = _redoSnapshots[^1];
            _redoSnapshots.RemoveAt(_redoSnapshots.Count - 1);
            _undoSnapshots.Add(currentSnapshot);
            ApplyUndoSnapshot(targetSnapshot);
        }

        private void ApplyUndoSnapshot(InspectorUndoSnapshot snapshot)
        {
            _applyingUndoRedo = true;
            try
            {
                ApplyInspectorUndoSnapshot(snapshot);
                NotifyProjectStateChanged();
            }
            finally
            {
                _applyingUndoRedo = false;
            }

            InitializeUndoRedoHistory(resetStacks: false);
            UpdateUndoRedoButtonState();
        }

        private void CaptureUndoSnapshotIfChanged()
        {
            if (_applyingUndoRedo)
            {
                return;
            }

            InspectorUndoSnapshot snapshot = CaptureInspectorUndoSnapshot();
            string fingerprint = ComputeUndoFingerprint(snapshot);

            if (!_undoRedoInitialized || _currentUndoSnapshot == null)
            {
                _currentUndoSnapshot = snapshot;
                _currentUndoFingerprint = fingerprint;
                _undoRedoInitialized = true;
                UpdateUndoRedoButtonState();
                return;
            }

            if (string.Equals(fingerprint, _currentUndoFingerprint, StringComparison.Ordinal))
            {
                UpdateUndoRedoButtonState();
                return;
            }

            _undoSnapshots.Add(_currentUndoSnapshot);
            if (_undoSnapshots.Count > MaxUndoSnapshots)
            {
                _undoSnapshots.RemoveAt(0);
            }

            _redoSnapshots.Clear();
            _currentUndoSnapshot = snapshot;
            _currentUndoFingerprint = fingerprint;
            UpdateUndoRedoButtonState();
        }

        private void InitializeUndoRedoHistory(bool resetStacks)
        {
            if (resetStacks)
            {
                _undoSnapshots.Clear();
                _redoSnapshots.Clear();
            }

            InspectorUndoSnapshot snapshot = CaptureInspectorUndoSnapshot();
            _currentUndoSnapshot = snapshot;
            _currentUndoFingerprint = ComputeUndoFingerprint(snapshot);
            _undoRedoInitialized = true;
        }

        private void UpdateUndoRedoButtonState()
        {
            if (_undoButton != null)
            {
                _undoButton.IsEnabled = _undoSnapshots.Count > 0;
            }

            if (_redoButton != null)
            {
                _redoButton.IsEnabled = _redoSnapshots.Count > 0;
            }
        }

        private static string ComputeUndoFingerprint(InspectorUndoSnapshot snapshot)
        {
            return JsonSerializer.Serialize(snapshot, UndoFingerprintJsonOptions);
        }

        private InspectorUndoSnapshot CaptureInspectorUndoSnapshot()
        {
            ModelNode? model = GetModelNode();
            MaterialNode? material = model?.Children.OfType<MaterialNode>().FirstOrDefault();
            CollarNode? collar = model?.Children.OfType<CollarNode>().FirstOrDefault();

            UserReferenceProfileSnapshot? modelMaterialSnapshot = null;
            if (model != null && material != null)
            {
                modelMaterialSnapshot = CaptureUserReferenceProfileSnapshot(_project, model, material);
            }

            return new InspectorUndoSnapshot
            {
                Mode = _project.Mode,
                BasisDebug = _project.BasisDebug,
                EnvironmentTopColorX = _project.EnvironmentTopColor.X,
                EnvironmentTopColorY = _project.EnvironmentTopColor.Y,
                EnvironmentTopColorZ = _project.EnvironmentTopColor.Z,
                EnvironmentBottomColorX = _project.EnvironmentBottomColor.X,
                EnvironmentBottomColorY = _project.EnvironmentBottomColor.Y,
                EnvironmentBottomColorZ = _project.EnvironmentBottomColor.Z,
                EnvironmentIntensity = _project.EnvironmentIntensity,
                EnvironmentRoughnessMix = _project.EnvironmentRoughnessMix,
                ToneMappingOperator = _project.ToneMappingOperator,
                EnvironmentExposure = _project.EnvironmentExposure,
                EnvironmentBloomStrength = _project.EnvironmentBloomStrength,
                EnvironmentBloomThreshold = _project.EnvironmentBloomThreshold,
                EnvironmentBloomKnee = _project.EnvironmentBloomKnee,
                EnvironmentHdriPath = _project.EnvironmentHdriPath,
                EnvironmentHdriBlend = _project.EnvironmentHdriBlend,
                EnvironmentHdriRotationDegrees = _project.EnvironmentHdriRotationDegrees,
                ShadowsEnabled = _project.ShadowsEnabled,
                ShadowMode = _project.ShadowMode,
                ShadowStrength = _project.ShadowStrength,
                ShadowSoftness = _project.ShadowSoftness,
                ShadowDistance = _project.ShadowDistance,
                ShadowScale = _project.ShadowScale,
                ShadowQuality = _project.ShadowQuality,
                ShadowGray = _project.ShadowGray,
                ShadowDiffuseInfluence = _project.ShadowDiffuseInfluence,
                PaintMaskSize = _project.PaintMaskSize,
                PaintHistoryRevision = _metalViewport?.PaintHistoryRevision ?? 0,
                ActivePaintLayerIndex = _metalViewport?.ActivePaintLayerIndex ?? 0,
                FocusedPaintLayerIndex = _metalViewport?.FocusedPaintLayerIndex ?? -1,
                BrushPaintingEnabled = _project.BrushPaintingEnabled,
                BrushType = _project.BrushType,
                BrushChannel = _project.BrushChannel,
                ScratchAbrasionType = _project.ScratchAbrasionType,
                BrushSizePx = _project.BrushSizePx,
                BrushOpacity = _project.BrushOpacity,
                BrushSpread = _project.BrushSpread,
                BrushDarkness = _project.BrushDarkness,
                PaintCoatMetallic = _project.PaintCoatMetallic,
                PaintCoatRoughness = _project.PaintCoatRoughness,
                RoughnessPaintTarget = _project.RoughnessPaintTarget,
                MetallicPaintTarget = _project.MetallicPaintTarget,
                ClearCoatAmount = _project.ClearCoatAmount,
                ClearCoatRoughness = _project.ClearCoatRoughness,
                AnisotropyAngleDegrees = _project.AnisotropyAngleDegrees,
                PaintColorX = _project.PaintColor.X,
                PaintColorY = _project.PaintColor.Y,
                PaintColorZ = _project.PaintColor.Z,
                ScratchWidthPx = _project.ScratchWidthPx,
                ScratchDepth = _project.ScratchDepth,
                ScratchDragResistance = _project.ScratchDragResistance,
                ScratchDepthRamp = _project.ScratchDepthRamp,
                ScratchExposeColorX = _project.ScratchExposeColor.X,
                ScratchExposeColorY = _project.ScratchExposeColor.Y,
                ScratchExposeColorZ = _project.ScratchExposeColor.Z,
                ScratchExposeMetallic = _project.ScratchExposeMetallic,
                ScratchExposeRoughness = _project.ScratchExposeRoughness,
                SpiralNormalInfluenceEnabled = _project.SpiralNormalInfluenceEnabled,
                SpiralNormalLodFadeStart = _project.SpiralNormalLodFadeStart,
                SpiralNormalLodFadeEnd = _project.SpiralNormalLodFadeEnd,
                SpiralRoughnessLodBoost = _project.SpiralRoughnessLodBoost,
                HasProjectType = true,
                ProjectType = _project.ProjectType,
                Lights = _project.Lights.Select(CaptureLightState).ToList(),
                SelectedLightIndex = _project.SelectedLightIndex,
                DynamicLightRig = CaptureDynamicLightRigSnapshot(_project.DynamicLightRig),
                HasModelMaterialSnapshot = modelMaterialSnapshot != null,
                ModelMaterialSnapshot = modelMaterialSnapshot != null ? CloneSnapshot(modelMaterialSnapshot) : null,
                ModelReferenceStyle = model?.ReferenceStyle ?? ReferenceKnobStyle.Custom,
                SelectedUserReferenceProfileName = _selectedUserReferenceProfileName,
                CollarSnapshot = collar != null ? CaptureCollarStateSnapshot(collar) : null,
                SliderMode = _project.SliderMode,
                SliderBackplateWidth = _project.SliderBackplateWidth,
                SliderBackplateHeight = _project.SliderBackplateHeight,
                SliderBackplateThickness = _project.SliderBackplateThickness,
                SliderThumbWidth = _project.SliderThumbWidth,
                SliderThumbHeight = _project.SliderThumbHeight,
                SliderThumbDepth = _project.SliderThumbDepth,
                SliderBackplateImportedMeshPath = _project.SliderBackplateImportedMeshPath,
                SliderThumbImportedMeshPath = _project.SliderThumbImportedMeshPath,
                ToggleMode = _project.ToggleMode,
                ToggleBaseImportedMeshPath = _project.ToggleBaseImportedMeshPath,
                ToggleLeverImportedMeshPath = _project.ToggleLeverImportedMeshPath,
                ToggleStateCount = _project.ToggleStateCount,
                ToggleStateIndex = _project.ToggleStateIndex,
                ToggleMaxAngleDeg = _project.ToggleMaxAngleDeg,
                TogglePlateWidth = _project.TogglePlateWidth,
                TogglePlateHeight = _project.TogglePlateHeight,
                TogglePlateThickness = _project.TogglePlateThickness,
                TogglePlateOffsetY = _project.TogglePlateOffsetY,
                TogglePlateOffsetZ = _project.TogglePlateOffsetZ,
                ToggleBushingRadius = _project.ToggleBushingRadius,
                ToggleBushingHeight = _project.ToggleBushingHeight,
                ToggleBushingSides = _project.ToggleBushingSides,
                ToggleLowerBushingShape = _project.ToggleLowerBushingShape,
                ToggleUpperBushingShape = _project.ToggleUpperBushingShape,
                ToggleLowerBushingRadiusScale = _project.ToggleLowerBushingRadiusScale,
                ToggleLowerBushingHeightRatio = _project.ToggleLowerBushingHeightRatio,
                ToggleUpperBushingRadiusScale = _project.ToggleUpperBushingRadiusScale,
                ToggleUpperBushingHeightRatio = _project.ToggleUpperBushingHeightRatio,
                ToggleUpperBushingKnurlAmount = _project.ToggleUpperBushingKnurlAmount,
                ToggleUpperBushingKnurlDensity = _project.ToggleUpperBushingKnurlDensity,
                ToggleUpperBushingKnurlDepth = _project.ToggleUpperBushingKnurlDepth,
                ToggleUpperBushingAnisotropyStrength = _project.ToggleUpperBushingAnisotropyStrength,
                ToggleUpperBushingAnisotropyDensity = _project.ToggleUpperBushingAnisotropyDensity,
                ToggleUpperBushingAnisotropyAngleDegrees = _project.ToggleUpperBushingAnisotropyAngleDegrees,
                ToggleUpperBushingSurfaceCharacter = _project.ToggleUpperBushingSurfaceCharacter,
                TogglePivotHousingRadius = _project.TogglePivotHousingRadius,
                TogglePivotHousingDepth = _project.TogglePivotHousingDepth,
                TogglePivotHousingBevel = _project.TogglePivotHousingBevel,
                TogglePivotBallRadius = _project.TogglePivotBallRadius,
                TogglePivotClearance = _project.TogglePivotClearance,
                ToggleInvertBaseFrontFaceWinding = _project.ToggleInvertBaseFrontFaceWinding,
                ToggleInvertLeverFrontFaceWinding = _project.ToggleInvertLeverFrontFaceWinding,
                ToggleLeverLength = _project.ToggleLeverLength,
                ToggleLeverRadius = _project.ToggleLeverRadius,
                ToggleLeverTopRadius = _project.ToggleLeverTopRadius,
                ToggleLeverSides = _project.ToggleLeverSides,
                ToggleLeverPivotOffset = _project.ToggleLeverPivotOffset,
                ToggleTipRadius = _project.ToggleTipRadius,
                ToggleTipLatitudeSegments = _project.ToggleTipLatitudeSegments,
                ToggleTipLongitudeSegments = _project.ToggleTipLongitudeSegments,
                ToggleTipSleeveEnabled = _project.ToggleTipSleeveEnabled,
                ToggleTipSleeveLength = _project.ToggleTipSleeveLength,
                ToggleTipSleeveThickness = _project.ToggleTipSleeveThickness,
                ToggleTipSleeveOuterRadius = _project.ToggleTipSleeveOuterRadius,
                ToggleTipSleeveCoverage = _project.ToggleTipSleeveCoverage,
                ToggleTipSleeveSides = _project.ToggleTipSleeveSides,
                ToggleTipSleeveStyle = _project.ToggleTipSleeveStyle,
                ToggleTipSleeveTipStyle = _project.ToggleTipSleeveTipStyle,
                ToggleTipSleevePatternCount = _project.ToggleTipSleevePatternCount,
                ToggleTipSleevePatternDepth = _project.ToggleTipSleevePatternDepth,
                ToggleTipSleeveTipAmount = _project.ToggleTipSleeveTipAmount,
                ToggleTipSleeveColorX = _project.ToggleTipSleeveColor.X,
                ToggleTipSleeveColorY = _project.ToggleTipSleeveColor.Y,
                ToggleTipSleeveColorZ = _project.ToggleTipSleeveColor.Z,
                ToggleTipSleeveMetallic = _project.ToggleTipSleeveMetallic,
                ToggleTipSleeveRoughness = _project.ToggleTipSleeveRoughness,
                ToggleTipSleevePearlescence = _project.ToggleTipSleevePearlescence,
                ToggleTipSleeveDiffuseStrength = _project.ToggleTipSleeveDiffuseStrength,
                ToggleTipSleeveSpecularStrength = _project.ToggleTipSleeveSpecularStrength,
                ToggleTipSleeveRustAmount = _project.ToggleTipSleeveRustAmount,
                ToggleTipSleeveWearAmount = _project.ToggleTipSleeveWearAmount,
                ToggleTipSleeveGunkAmount = _project.ToggleTipSleeveGunkAmount,
                IndicatorAssemblyEnabled = _project.IndicatorAssemblyEnabled,
                IndicatorBaseWidth = _project.IndicatorBaseWidth,
                IndicatorBaseHeight = _project.IndicatorBaseHeight,
                IndicatorBaseThickness = _project.IndicatorBaseThickness,
                IndicatorHousingRadius = _project.IndicatorHousingRadius,
                IndicatorHousingHeight = _project.IndicatorHousingHeight,
                IndicatorLensRadius = _project.IndicatorLensRadius,
                IndicatorLensHeight = _project.IndicatorLensHeight,
                IndicatorLensTransmission = _project.IndicatorLensTransmission,
                IndicatorLensIor = _project.IndicatorLensIor,
                IndicatorLensThickness = _project.IndicatorLensThickness,
                IndicatorLensTintX = _project.IndicatorLensTint.X,
                IndicatorLensTintY = _project.IndicatorLensTint.Y,
                IndicatorLensTintZ = _project.IndicatorLensTint.Z,
                IndicatorLensAbsorption = _project.IndicatorLensAbsorption,
                IndicatorLensSurfaceRoughness = _project.IndicatorLensSurfaceRoughness,
                IndicatorLensSurfaceSpecularStrength = _project.IndicatorLensSurfaceSpecularStrength,
                IndicatorReflectorBaseRadius = _project.IndicatorReflectorBaseRadius,
                IndicatorReflectorTopRadius = _project.IndicatorReflectorTopRadius,
                IndicatorReflectorDepth = _project.IndicatorReflectorDepth,
                IndicatorEmitterRadius = _project.IndicatorEmitterRadius,
                IndicatorEmitterSpread = _project.IndicatorEmitterSpread,
                IndicatorEmitterDepth = _project.IndicatorEmitterDepth,
                IndicatorEmitterCount = _project.IndicatorEmitterCount,
                IndicatorRadialSegments = _project.IndicatorRadialSegments,
                IndicatorLensLatitudeSegments = _project.IndicatorLensLatitudeSegments,
                IndicatorLensLongitudeSegments = _project.IndicatorLensLongitudeSegments,
                Selection = CaptureSceneSelectionSnapshot(_project.SelectedNode)
            };
        }


    }
}
