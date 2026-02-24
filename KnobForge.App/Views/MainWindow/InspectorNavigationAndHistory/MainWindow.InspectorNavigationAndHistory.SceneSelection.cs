using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using KnobForge.App.ProjectFiles;
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
        private void ApplyInspectorUndoSnapshot(InspectorUndoSnapshot snapshot)
        {
            _project.Mode = snapshot.Mode;
            _project.BasisDebug = snapshot.BasisDebug;
            _project.EnvironmentTopColor = new(
                snapshot.EnvironmentTopColorX,
                snapshot.EnvironmentTopColorY,
                snapshot.EnvironmentTopColorZ);
            _project.EnvironmentBottomColor = new(
                snapshot.EnvironmentBottomColorX,
                snapshot.EnvironmentBottomColorY,
                snapshot.EnvironmentBottomColorZ);
            _project.EnvironmentIntensity = snapshot.EnvironmentIntensity;
            _project.EnvironmentRoughnessMix = snapshot.EnvironmentRoughnessMix;
            _project.ToneMappingOperator = snapshot.ToneMappingOperator;
            _project.EnvironmentExposure = snapshot.EnvironmentExposure;
            _project.EnvironmentBloomStrength = snapshot.EnvironmentBloomStrength;
            _project.EnvironmentBloomThreshold = snapshot.EnvironmentBloomThreshold;
            _project.EnvironmentBloomKnee = snapshot.EnvironmentBloomKnee;
            _project.EnvironmentHdriPath = snapshot.EnvironmentHdriPath;
            _project.EnvironmentHdriBlend = snapshot.EnvironmentHdriBlend;
            _project.EnvironmentHdriRotationDegrees = snapshot.EnvironmentHdriRotationDegrees;

            _project.ShadowsEnabled = snapshot.ShadowsEnabled;
            _project.ShadowMode = snapshot.ShadowMode;
            _project.ShadowStrength = snapshot.ShadowStrength;
            _project.ShadowSoftness = snapshot.ShadowSoftness;
            _project.ShadowDistance = snapshot.ShadowDistance;
            _project.ShadowScale = snapshot.ShadowScale;
            _project.ShadowQuality = snapshot.ShadowQuality;
            _project.ShadowGray = snapshot.ShadowGray;
            _project.ShadowDiffuseInfluence = snapshot.ShadowDiffuseInfluence;

            _project.BrushPaintingEnabled = snapshot.BrushPaintingEnabled;
            _project.BrushType = snapshot.BrushType;
            _project.BrushChannel = snapshot.BrushChannel;
            _project.ScratchAbrasionType = snapshot.ScratchAbrasionType;
            _project.BrushSizePx = snapshot.BrushSizePx;
            _project.BrushOpacity = snapshot.BrushOpacity;
            _project.BrushSpread = snapshot.BrushSpread;
            _project.BrushDarkness = snapshot.BrushDarkness;
            _project.PaintCoatMetallic = snapshot.PaintCoatMetallic;
            _project.PaintCoatRoughness = snapshot.PaintCoatRoughness;
            _project.ClearCoatAmount = snapshot.ClearCoatAmount;
            _project.ClearCoatRoughness = snapshot.ClearCoatRoughness;
            _project.AnisotropyAngleDegrees = snapshot.AnisotropyAngleDegrees;
            _project.PaintColor = new(snapshot.PaintColorX, snapshot.PaintColorY, snapshot.PaintColorZ);
            _project.ScratchWidthPx = snapshot.ScratchWidthPx;
            _project.ScratchDepth = snapshot.ScratchDepth;
            _project.ScratchDragResistance = snapshot.ScratchDragResistance;
            _project.ScratchDepthRamp = snapshot.ScratchDepthRamp;
            _project.ScratchExposeColor = new(
                snapshot.ScratchExposeColorX,
                snapshot.ScratchExposeColorY,
                snapshot.ScratchExposeColorZ);
            _project.ScratchExposeMetallic = snapshot.ScratchExposeMetallic;
            _project.ScratchExposeRoughness = snapshot.ScratchExposeRoughness;
            _project.SpiralNormalInfluenceEnabled = snapshot.SpiralNormalInfluenceEnabled;
            _project.SpiralNormalLodFadeStart = snapshot.SpiralNormalLodFadeStart;
            _project.SpiralNormalLodFadeEnd = snapshot.SpiralNormalLodFadeEnd;
            _project.SpiralRoughnessLodBoost = snapshot.SpiralRoughnessLodBoost;
            _project.ProjectType = ResolveProjectTypeFromSnapshot(snapshot);
            _project.SliderMode = snapshot.SliderMode;
            _project.SliderBackplateWidth = snapshot.SliderBackplateWidth;
            _project.SliderBackplateHeight = snapshot.SliderBackplateHeight;
            _project.SliderBackplateThickness = snapshot.SliderBackplateThickness;
            _project.SliderThumbWidth = snapshot.SliderThumbWidth;
            _project.SliderThumbHeight = snapshot.SliderThumbHeight;
            _project.SliderThumbDepth = snapshot.SliderThumbDepth;
            _project.SliderBackplateImportedMeshPath = snapshot.SliderBackplateImportedMeshPath;
            _project.SliderThumbImportedMeshPath = snapshot.SliderThumbImportedMeshPath;
            _project.ToggleMode = snapshot.ToggleMode;
            _project.ToggleBaseImportedMeshPath = snapshot.ToggleBaseImportedMeshPath;
            _project.ToggleLeverImportedMeshPath = snapshot.ToggleLeverImportedMeshPath;
            _project.ToggleStateCount = snapshot.ToggleStateCount;
            _project.ToggleStateIndex = snapshot.ToggleStateIndex;
            _project.ToggleMaxAngleDeg = snapshot.ToggleMaxAngleDeg;
            _project.TogglePlateWidth = snapshot.TogglePlateWidth;
            _project.TogglePlateHeight = snapshot.TogglePlateHeight;
            _project.TogglePlateThickness = snapshot.TogglePlateThickness;
            _project.TogglePlateOffsetY = snapshot.TogglePlateOffsetY;
            _project.TogglePlateOffsetZ = snapshot.TogglePlateOffsetZ;
            _project.ToggleBushingRadius = snapshot.ToggleBushingRadius;
            _project.ToggleBushingHeight = snapshot.ToggleBushingHeight;
            _project.ToggleBushingSides = snapshot.ToggleBushingSides;
            _project.ToggleLowerBushingShape = snapshot.ToggleLowerBushingShape;
            _project.ToggleUpperBushingShape = snapshot.ToggleUpperBushingShape;
            _project.ToggleLowerBushingRadiusScale = snapshot.ToggleLowerBushingRadiusScale;
            _project.ToggleLowerBushingHeightRatio = snapshot.ToggleLowerBushingHeightRatio;
            _project.ToggleUpperBushingRadiusScale = snapshot.ToggleUpperBushingRadiusScale;
            _project.ToggleUpperBushingHeightRatio = snapshot.ToggleUpperBushingHeightRatio;
            _project.ToggleUpperBushingKnurlAmount = snapshot.ToggleUpperBushingKnurlAmount;
            _project.ToggleUpperBushingKnurlDensity = snapshot.ToggleUpperBushingKnurlDensity;
            _project.ToggleUpperBushingKnurlDepth = snapshot.ToggleUpperBushingKnurlDepth;
            _project.TogglePivotHousingRadius = snapshot.TogglePivotHousingRadius;
            _project.TogglePivotHousingDepth = snapshot.TogglePivotHousingDepth;
            _project.TogglePivotHousingBevel = snapshot.TogglePivotHousingBevel;
            _project.TogglePivotBallRadius = snapshot.TogglePivotBallRadius;
            _project.TogglePivotClearance = snapshot.TogglePivotClearance;
            _project.ToggleInvertBaseFrontFaceWinding = snapshot.ToggleInvertBaseFrontFaceWinding;
            _project.ToggleInvertLeverFrontFaceWinding = snapshot.ToggleInvertLeverFrontFaceWinding;
            _project.ToggleLeverLength = snapshot.ToggleLeverLength;
            _project.ToggleLeverRadius = snapshot.ToggleLeverRadius;
            _project.ToggleLeverTopRadius = snapshot.ToggleLeverTopRadius;
            _project.ToggleLeverSides = snapshot.ToggleLeverSides;
            _project.ToggleLeverPivotOffset = snapshot.ToggleLeverPivotOffset;
            _project.ToggleTipRadius = snapshot.ToggleTipRadius;
            _project.ToggleTipLatitudeSegments = snapshot.ToggleTipLatitudeSegments;
            _project.ToggleTipLongitudeSegments = snapshot.ToggleTipLongitudeSegments;
            _project.ToggleTipSleeveEnabled = snapshot.ToggleTipSleeveEnabled;
            _project.ToggleTipSleeveLength = snapshot.ToggleTipSleeveLength;
            _project.ToggleTipSleeveThickness = snapshot.ToggleTipSleeveThickness;
            _project.ToggleTipSleeveOuterRadius = snapshot.ToggleTipSleeveOuterRadius;
            _project.ToggleTipSleeveCoverage = snapshot.ToggleTipSleeveCoverage;
            _project.ToggleTipSleeveSides = snapshot.ToggleTipSleeveSides;
            _project.ToggleTipSleeveStyle = snapshot.ToggleTipSleeveStyle;
            _project.ToggleTipSleeveTipStyle = snapshot.ToggleTipSleeveTipStyle;
            _project.ToggleTipSleevePatternCount = snapshot.ToggleTipSleevePatternCount;
            _project.ToggleTipSleevePatternDepth = snapshot.ToggleTipSleevePatternDepth;
            _project.ToggleTipSleeveTipAmount = snapshot.ToggleTipSleeveTipAmount;
            _project.ToggleTipSleeveColor = new(
                snapshot.ToggleTipSleeveColorX,
                snapshot.ToggleTipSleeveColorY,
                snapshot.ToggleTipSleeveColorZ);
            _project.ToggleTipSleeveMetallic = snapshot.ToggleTipSleeveMetallic;
            _project.ToggleTipSleeveRoughness = snapshot.ToggleTipSleeveRoughness;
            _project.ToggleTipSleevePearlescence = snapshot.ToggleTipSleevePearlescence;
            _project.ToggleTipSleeveDiffuseStrength = snapshot.ToggleTipSleeveDiffuseStrength;
            _project.ToggleTipSleeveSpecularStrength = snapshot.ToggleTipSleeveSpecularStrength;
            _project.ToggleTipSleeveRustAmount = snapshot.ToggleTipSleeveRustAmount;
            _project.ToggleTipSleeveWearAmount = snapshot.ToggleTipSleeveWearAmount;
            _project.ToggleTipSleeveGunkAmount = snapshot.ToggleTipSleeveGunkAmount;
            _project.IndicatorAssemblyEnabled = snapshot.IndicatorAssemblyEnabled;
            _project.IndicatorBaseWidth = snapshot.IndicatorBaseWidth;
            _project.IndicatorBaseHeight = snapshot.IndicatorBaseHeight;
            _project.IndicatorBaseThickness = snapshot.IndicatorBaseThickness;
            _project.IndicatorHousingRadius = snapshot.IndicatorHousingRadius;
            _project.IndicatorHousingHeight = snapshot.IndicatorHousingHeight;
            _project.IndicatorLensRadius = snapshot.IndicatorLensRadius;
            _project.IndicatorLensHeight = snapshot.IndicatorLensHeight;
            _project.IndicatorLensTransmission = snapshot.IndicatorLensTransmission;
            _project.IndicatorLensIor = snapshot.IndicatorLensIor;
            _project.IndicatorLensThickness = snapshot.IndicatorLensThickness;
            _project.IndicatorLensTint = new(
                snapshot.IndicatorLensTintX,
                snapshot.IndicatorLensTintY,
                snapshot.IndicatorLensTintZ);
            _project.IndicatorLensAbsorption = snapshot.IndicatorLensAbsorption;
            _project.IndicatorLensSurfaceRoughness = snapshot.IndicatorLensSurfaceRoughness;
            _project.IndicatorLensSurfaceSpecularStrength = snapshot.IndicatorLensSurfaceSpecularStrength;
            _project.IndicatorReflectorBaseRadius = snapshot.IndicatorReflectorBaseRadius;
            _project.IndicatorReflectorTopRadius = snapshot.IndicatorReflectorTopRadius;
            _project.IndicatorReflectorDepth = snapshot.IndicatorReflectorDepth;
            _project.IndicatorEmitterRadius = snapshot.IndicatorEmitterRadius;
            _project.IndicatorEmitterSpread = snapshot.IndicatorEmitterSpread;
            _project.IndicatorEmitterDepth = snapshot.IndicatorEmitterDepth;
            _project.IndicatorEmitterCount = snapshot.IndicatorEmitterCount;
            _project.IndicatorRadialSegments = snapshot.IndicatorRadialSegments;
            _project.IndicatorLensLatitudeSegments = snapshot.IndicatorLensLatitudeSegments;
            _project.IndicatorLensLongitudeSegments = snapshot.IndicatorLensLongitudeSegments;
            if (_project.ProjectType == InteractorProjectType.IndicatorLight)
            {
                _project.EnsureIndicatorAssemblyDefaults(forceReset: false);
            }
            _project.EnsureInteractorModulesForProjectType(_project.ProjectType, pruneUnsupportedModules: true);

            if (_metalViewport != null)
            {
                _metalViewport.RestorePaintHistoryRevision(snapshot.PaintHistoryRevision);
                _metalViewport.SetActivePaintLayer(snapshot.ActivePaintLayerIndex);
                _metalViewport.SetFocusedPaintLayer(snapshot.FocusedPaintLayerIndex);
                RefreshPaintLayerListFromViewport(preferActiveSelection: true);
            }

            ApplyLightStates(snapshot.Lights, snapshot.SelectedLightIndex);
            ApplyDynamicLightRigSnapshot(snapshot.DynamicLightRig);
            SyncIndicatorDynamicLightSourcesToAssembly(recenterSources: false);

            ModelNode model = _project.EnsureModelNode();
            MaterialNode material = _project.EnsureMaterialNode();

            if (snapshot.HasModelMaterialSnapshot && snapshot.ModelMaterialSnapshot != null)
            {
                ApplyUserReferenceProfileSnapshot(_project, model, material, CloneSnapshot(snapshot.ModelMaterialSnapshot));
            }

            model.ReferenceStyle = snapshot.ModelReferenceStyle;
            _selectedUserReferenceProfileName = snapshot.SelectedUserReferenceProfileName;

            if (_project.ProjectType == InteractorProjectType.RotaryKnob && snapshot.CollarSnapshot != null)
            {
                CollarNode collar = EnsureCollarNode();
                ApplyCollarStateSnapshot(collar, snapshot.CollarSnapshot);
            }
            else
            {
                _project.RemoveCollarNode();
            }

            RebuildReferenceStyleOptions();
            SelectReferenceStyleOptionForModel(model);

            SceneNode selectedNode = ResolveSceneSelectionSnapshot(
                snapshot.Selection,
                model,
                material,
                model.Children.OfType<CollarNode>().FirstOrDefault());
            _project.SetSelectedNode(selectedNode);
        }

        private static InteractorProjectType ResolveProjectTypeFromSnapshot(InspectorUndoSnapshot snapshot)
        {
            return InteractorProjectTypeResolver.ResolveFromSnapshotHint(new ProjectTypeSnapshotHint(
                HasProjectType: snapshot.HasProjectType,
                ProjectType: snapshot.ProjectType,
                SliderMode: snapshot.SliderMode,
                ToggleMode: snapshot.ToggleMode,
                SliderBackplateImportedMeshPath: snapshot.SliderBackplateImportedMeshPath,
                SliderThumbImportedMeshPath: snapshot.SliderThumbImportedMeshPath,
                SliderBackplateWidth: snapshot.SliderBackplateWidth,
                SliderBackplateHeight: snapshot.SliderBackplateHeight,
                SliderBackplateThickness: snapshot.SliderBackplateThickness,
                SliderThumbWidth: snapshot.SliderThumbWidth,
                SliderThumbHeight: snapshot.SliderThumbHeight,
                SliderThumbDepth: snapshot.SliderThumbDepth,
                ToggleBaseImportedMeshPath: snapshot.ToggleBaseImportedMeshPath,
                ToggleLeverImportedMeshPath: snapshot.ToggleLeverImportedMeshPath,
                TogglePlateWidth: snapshot.TogglePlateWidth,
                TogglePlateHeight: snapshot.TogglePlateHeight,
                TogglePlateThickness: snapshot.TogglePlateThickness,
                ToggleBushingRadius: snapshot.ToggleBushingRadius,
                ToggleBushingHeight: snapshot.ToggleBushingHeight,
                ToggleLeverLength: snapshot.ToggleLeverLength,
                ToggleLeverRadius: snapshot.ToggleLeverRadius,
                ToggleLeverTopRadius: snapshot.ToggleLeverTopRadius,
                ToggleTipRadius: snapshot.ToggleTipRadius,
                ToggleStateCount: snapshot.ToggleStateCount,
                ToggleMaxAngleDeg: snapshot.ToggleMaxAngleDeg));
        }

        private static LightStateSnapshot CaptureLightState(KnobLight light)
        {
            return new LightStateSnapshot
            {
                Name = light.Name,
                Type = light.Type,
                X = light.X,
                Y = light.Y,
                Z = light.Z,
                DirectionRadians = light.DirectionRadians,
                ColorR = light.Color.Red,
                ColorG = light.Color.Green,
                ColorB = light.Color.Blue,
                ColorA = light.Color.Alpha,
                Intensity = light.Intensity,
                Falloff = light.Falloff,
                DiffuseBoost = light.DiffuseBoost,
                SpecularBoost = light.SpecularBoost,
                SpecularPower = light.SpecularPower
            };
        }

        private void ApplyLightStates(IReadOnlyList<LightStateSnapshot> lights, int selectedLightIndex)
        {
            foreach (LightNode lightNode in _project.SceneRoot.Children.OfType<LightNode>().ToList())
            {
                _project.SceneRoot.RemoveChild(lightNode);
            }

            _project.Lights.Clear();
            foreach (LightStateSnapshot light in lights)
            {
                KnobLight restoredLight = new()
                {
                    Name = light.Name,
                    Type = light.Type,
                    X = light.X,
                    Y = light.Y,
                    Z = light.Z,
                    DirectionRadians = light.DirectionRadians,
                    Color = new SKColor(light.ColorR, light.ColorG, light.ColorB, light.ColorA),
                    Intensity = light.Intensity,
                    Falloff = light.Falloff,
                    DiffuseBoost = light.DiffuseBoost,
                    SpecularBoost = light.SpecularBoost,
                    SpecularPower = light.SpecularPower
                };

                _project.Lights.Add(restoredLight);
                _project.SceneRoot.AddChild(new LightNode(restoredLight));
            }

            if (_project.Lights.Count == 0)
            {
                _project.AddLight();
                selectedLightIndex = _project.SelectedLightIndex;
            }

            int clampedIndex = Math.Clamp(selectedLightIndex, 0, _project.Lights.Count - 1);
            _project.SetSelectedLightIndex(clampedIndex);
        }

        private static DynamicLightRigSnapshot CaptureDynamicLightRigSnapshot(DynamicLightRig rig)
        {
            return new DynamicLightRigSnapshot
            {
                Enabled = rig.Enabled,
                MaxActiveLights = rig.MaxActiveLights,
                AnimationMode = rig.AnimationMode,
                AnimationSpeed = rig.AnimationSpeed,
                FlickerAmount = rig.FlickerAmount,
                FlickerDropoutChance = rig.FlickerDropoutChance,
                FlickerSmoothing = rig.FlickerSmoothing,
                FlickerSeed = rig.FlickerSeed,
                Sources = rig.Sources.Select(source => new DynamicLightSourceSnapshot
                {
                    Name = source.Name,
                    Enabled = source.Enabled,
                    AnimationPhaseOffsetDegrees = source.AnimationPhaseOffsetDegrees,
                    X = source.X,
                    Y = source.Y,
                    Z = source.Z,
                    ColorR = source.Color.Red,
                    ColorG = source.Color.Green,
                    ColorB = source.Color.Blue,
                    ColorA = source.Color.Alpha,
                    Intensity = source.Intensity,
                    Radius = source.Radius,
                    Falloff = source.Falloff
                }).ToList()
            };
        }

        private void ApplyDynamicLightRigSnapshot(DynamicLightRigSnapshot snapshot)
        {
            DynamicLightRig rig = _project.DynamicLightRig;
            rig.Enabled = snapshot.Enabled;
            rig.MaxActiveLights = snapshot.MaxActiveLights;
            rig.AnimationMode = snapshot.AnimationMode;
            rig.AnimationSpeed = snapshot.AnimationSpeed;
            rig.FlickerAmount = snapshot.FlickerAmount;
            rig.FlickerDropoutChance = snapshot.FlickerDropoutChance;
            rig.FlickerSmoothing = snapshot.FlickerSmoothing;
            rig.FlickerSeed = snapshot.FlickerSeed;

            rig.Sources.Clear();
            foreach (DynamicLightSourceSnapshot source in snapshot.Sources)
            {
                rig.Sources.Add(new DynamicLightSource
                {
                    Name = source.Name,
                    Enabled = source.Enabled,
                    AnimationPhaseOffsetDegrees = source.AnimationPhaseOffsetDegrees,
                    X = source.X,
                    Y = source.Y,
                    Z = source.Z,
                    Color = new SKColor(source.ColorR, source.ColorG, source.ColorB, source.ColorA),
                    Intensity = source.Intensity,
                    Radius = source.Radius,
                    Falloff = source.Falloff
                });
            }

            if (_project.ProjectType == InteractorProjectType.IndicatorLight)
            {
                rig.EnsureIndicatorDefaults();
                if (snapshot.Sources.Count == 0)
                {
                    rig.Enabled = true;
                }
            }
        }

        private static CollarStateSnapshot CaptureCollarStateSnapshot(CollarNode collar)
        {
            return new CollarStateSnapshot
            {
                Enabled = collar.Enabled,
                Preset = collar.Preset,
                InnerRadiusRatio = collar.InnerRadiusRatio,
                GapToKnobRatio = collar.GapToKnobRatio,
                ElevationRatio = collar.ElevationRatio,
                OverallRotationRadians = collar.OverallRotationRadians,
                BiteAngleRadians = collar.BiteAngleRadians,
                BodyRadiusRatio = collar.BodyRadiusRatio,
                BodyEllipseYScale = collar.BodyEllipseYScale,
                NeckTaper = collar.NeckTaper,
                TailTaper = collar.TailTaper,
                MassBias = collar.MassBias,
                TailUnderlap = collar.TailUnderlap,
                HeadScale = collar.HeadScale,
                JawBulge = collar.JawBulge,
                UvSeamFollowBite = collar.UvSeamFollowBite,
                UvSeamOffset = collar.UvSeamOffset,
                PathSegments = collar.PathSegments,
                CrossSegments = collar.CrossSegments,
                BaseColorX = collar.BaseColor.X,
                BaseColorY = collar.BaseColor.Y,
                BaseColorZ = collar.BaseColor.Z,
                Metallic = collar.Metallic,
                Roughness = collar.Roughness,
                Pearlescence = collar.Pearlescence,
                RustAmount = collar.RustAmount,
                WearAmount = collar.WearAmount,
                GunkAmount = collar.GunkAmount,
                NormalStrength = collar.NormalStrength,
                HeightStrength = collar.HeightStrength,
                ScaleDensity = collar.ScaleDensity,
                ScaleRelief = collar.ScaleRelief,
                ImportedMeshPath = collar.ImportedMeshPath,
                ImportedScale = collar.ImportedScale,
                ImportedRotationRadians = collar.ImportedRotationRadians,
                ImportedMirrorX = collar.ImportedMirrorX,
                ImportedMirrorY = collar.ImportedMirrorY,
                ImportedMirrorZ = collar.ImportedMirrorZ,
                ImportedHeadAngleOffsetRadians = collar.ImportedHeadAngleOffsetRadians,
                ImportedOffsetXRatio = collar.ImportedOffsetXRatio,
                ImportedOffsetYRatio = collar.ImportedOffsetYRatio,
                ImportedInflateRatio = collar.ImportedInflateRatio,
                ImportedBodyLengthScale = collar.ImportedBodyLengthScale,
                ImportedBodyThicknessScale = collar.ImportedBodyThicknessScale,
                ImportedHeadLengthScale = collar.ImportedHeadLengthScale,
                ImportedHeadThicknessScale = collar.ImportedHeadThicknessScale
            };
        }

        private static void ApplyCollarStateSnapshot(CollarNode collar, CollarStateSnapshot snapshot)
        {
            collar.Enabled = snapshot.Enabled;
            collar.Preset = snapshot.Preset;
            collar.InnerRadiusRatio = snapshot.InnerRadiusRatio;
            collar.GapToKnobRatio = snapshot.GapToKnobRatio;
            collar.ElevationRatio = snapshot.ElevationRatio;
            collar.OverallRotationRadians = snapshot.OverallRotationRadians;
            collar.BiteAngleRadians = snapshot.BiteAngleRadians;
            collar.BodyRadiusRatio = snapshot.BodyRadiusRatio;
            collar.BodyEllipseYScale = snapshot.BodyEllipseYScale;
            collar.NeckTaper = snapshot.NeckTaper;
            collar.TailTaper = snapshot.TailTaper;
            collar.MassBias = snapshot.MassBias;
            collar.TailUnderlap = snapshot.TailUnderlap;
            collar.HeadScale = snapshot.HeadScale;
            collar.JawBulge = snapshot.JawBulge;
            collar.UvSeamFollowBite = snapshot.UvSeamFollowBite;
            collar.UvSeamOffset = snapshot.UvSeamOffset;
            collar.PathSegments = snapshot.PathSegments;
            collar.CrossSegments = snapshot.CrossSegments;
            collar.BaseColor = new(snapshot.BaseColorX, snapshot.BaseColorY, snapshot.BaseColorZ);
            collar.Metallic = snapshot.Metallic;
            collar.Roughness = snapshot.Roughness;
            collar.Pearlescence = snapshot.Pearlescence;
            collar.RustAmount = snapshot.RustAmount;
            collar.WearAmount = snapshot.WearAmount;
            collar.GunkAmount = snapshot.GunkAmount;
            collar.NormalStrength = snapshot.NormalStrength;
            collar.HeightStrength = snapshot.HeightStrength;
            collar.ScaleDensity = snapshot.ScaleDensity;
            collar.ScaleRelief = snapshot.ScaleRelief;
            collar.ImportedMeshPath = snapshot.ImportedMeshPath;
            collar.ImportedScale = snapshot.ImportedScale;
            collar.ImportedRotationRadians = snapshot.ImportedRotationRadians;
            collar.ImportedMirrorX = snapshot.ImportedMirrorX;
            collar.ImportedMirrorY = snapshot.ImportedMirrorY;
            collar.ImportedMirrorZ = snapshot.ImportedMirrorZ;
            collar.ImportedHeadAngleOffsetRadians = snapshot.ImportedHeadAngleOffsetRadians;
            collar.ImportedOffsetXRatio = snapshot.ImportedOffsetXRatio;
            collar.ImportedOffsetYRatio = snapshot.ImportedOffsetYRatio;
            collar.ImportedInflateRatio = snapshot.ImportedInflateRatio;
            collar.ImportedBodyLengthScale = snapshot.ImportedBodyLengthScale;
            collar.ImportedBodyThicknessScale = snapshot.ImportedBodyThicknessScale;
            collar.ImportedHeadLengthScale = snapshot.ImportedHeadLengthScale;
            collar.ImportedHeadThicknessScale = snapshot.ImportedHeadThicknessScale;
        }

        private SceneSelectionSnapshot CaptureSceneSelectionSnapshot(SceneNode? node)
        {
            if (node is LightNode lightNode)
            {
                int index = _project.Lights.FindIndex(light => ReferenceEquals(light, lightNode.Light));
                return new SceneSelectionSnapshot
                {
                    Kind = SceneSelectionKind.Light,
                    LightIndex = index
                };
            }

            return node switch
            {
                SceneRootNode => new SceneSelectionSnapshot { Kind = SceneSelectionKind.SceneRoot },
                ModelNode => new SceneSelectionSnapshot { Kind = SceneSelectionKind.Model },
                MaterialNode => new SceneSelectionSnapshot { Kind = SceneSelectionKind.Material },
                CollarNode => new SceneSelectionSnapshot { Kind = SceneSelectionKind.Collar },
                _ => new SceneSelectionSnapshot { Kind = SceneSelectionKind.Unknown }
            };
        }

        private SceneNode ResolveSceneSelectionSnapshot(
            SceneSelectionSnapshot selection,
            ModelNode model,
            MaterialNode material,
            CollarNode? collar)
        {
            if (selection.Kind == SceneSelectionKind.Light &&
                selection.LightIndex >= 0 &&
                selection.LightIndex < _project.Lights.Count)
            {
                KnobLight light = _project.Lights[selection.LightIndex];
                return (SceneNode?)_project.SceneRoot.Children
                    .OfType<LightNode>()
                    .FirstOrDefault(node => ReferenceEquals(node.Light, light)) ?? _project.SceneRoot;
            }

            return selection.Kind switch
            {
                SceneSelectionKind.Model => model,
                SceneSelectionKind.Material => material,
                SceneSelectionKind.Collar when collar != null => collar,
                _ => _project.SceneRoot
            };
        }

        private bool TryAdoptSceneSelectionFromInspectorContext()
        {
            if (_updatingUi || _inspectorTabControl == null || _inspectorTabControl.SelectedItem is not TabItem selectedTab)
            {
                return false;
            }

            if (!IsInspectorTabSelectable(selectedTab))
            {
                return false;
            }

            SceneNode? desiredNode = null;
            if (ReferenceEquals(selectedTab, _lightingTabItem))
            {
                desiredNode = ResolveSelectedLightSceneNode();
            }
            else if (ReferenceEquals(selectedTab, _modelTabItem) || ReferenceEquals(selectedTab, _brushTabItem))
            {
                desiredNode = ResolvePreferredModelSceneNode();
            }

            if (desiredNode == null || _project.SelectedNode?.Id == desiredNode.Id)
            {
                return false;
            }

            _project.SetSelectedNode(desiredNode);
            if (desiredNode is LightNode lightNode)
            {
                int index = _project.Lights.FindIndex(light => ReferenceEquals(light, lightNode.Light));
                if (index >= 0)
                {
                    _project.SetSelectedLightIndex(index);
                }
            }

            return true;
        }

        private SceneNode? ResolveSelectedLightSceneNode()
        {
            int selectedIndex = _project.SelectedLightIndex;
            if (selectedIndex < 0 || selectedIndex >= _project.Lights.Count)
            {
                return null;
            }

            KnobLight selectedLight = _project.Lights[selectedIndex];
            return _project.SceneRoot.Children
                .OfType<LightNode>()
                .FirstOrDefault(node => ReferenceEquals(node.Light, selectedLight));
        }

        private SceneNode? ResolvePreferredModelSceneNode()
        {
            if (_project.SelectedNode is ModelNode ||
                _project.SelectedNode is MaterialNode ||
                _project.SelectedNode is CollarNode)
            {
                return _project.SelectedNode;
            }

            return GetModelNode();
        }

        private void SyncSceneListSelectionToProjectNode()
        {
            if (_sceneListBox == null)
            {
                return;
            }

            SceneNode? selectedNode = _project.SelectedNode;
            if (selectedNode == null)
            {
                return;
            }

            SceneNode? match = _sceneNodes.FirstOrDefault(node => node.Id == selectedNode.Id);
            if (match != null && !ReferenceEquals(_sceneListBox.SelectedItem, match))
            {
                _sceneListBox.SelectedItem = match;
            }
        }

        private void SyncInspectorForSelectedSceneNode(SceneNode node)
        {
            bool selectedLightChanged = false;
            if (node is LightNode lightNode)
            {
                int lightIndex = _project.Lights.FindIndex(light => ReferenceEquals(light, lightNode.Light));
                if (lightIndex >= 0 && _project.SetSelectedLightIndex(lightIndex))
                {
                    selectedLightChanged = true;
                }
            }

            RefreshInspectorFromProject(InspectorRefreshTabPolicy.FollowSceneSelection);
            if (selectedLightChanged)
            {
                _metalViewport?.InvalidateGpu();
            }
        }

        private void SelectInspectorTabForSceneNode(SceneNode? node)
        {
            if (_inspectorTabControl == null)
            {
                return;
            }

            TabItem? target = node switch
            {
                LightNode => _lightingTabItem,
                ModelNode => _modelTabItem,
                MaterialNode => _modelTabItem,
                CollarNode => _modelTabItem,
                SceneRootNode => _modelTabItem,
                _ => _modelTabItem
            };

            target = ResolvePreferredVisibleInspectorTab(target);
            if (target != null && !ReferenceEquals(_inspectorTabControl.SelectedItem, target))
            {
                _inspectorTabControl.SelectedItem = target;
            }
        }

        private void SelectInspectorTabForControl(Control control)
        {
            if (_inspectorTabControl == null)
            {
                return;
            }

            TabItem? tab = FindAncestorTabItem(control);
            tab = ResolvePreferredVisibleInspectorTab(tab);
            if (tab != null && !ReferenceEquals(_inspectorTabControl.SelectedItem, tab))
            {
                _inspectorTabControl.SelectedItem = tab;
            }
        }

        private static TabItem? FindAncestorTabItem(Control control)
        {
            Visual? visual = control;
            while (visual != null)
            {
                if (visual is TabItem tabItem)
                {
                    return tabItem;
                }

                visual = visual.GetVisualParent();
            }

            return null;
        }

    }
}
