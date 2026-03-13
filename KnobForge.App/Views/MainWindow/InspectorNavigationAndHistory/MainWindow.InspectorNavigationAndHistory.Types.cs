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
        private sealed class InspectorUndoSnapshot
        {
            public LightingMode Mode { get; set; }
            public BasisDebugMode BasisDebug { get; set; }
            public float EnvironmentTopColorX { get; set; }
            public float EnvironmentTopColorY { get; set; }
            public float EnvironmentTopColorZ { get; set; }
            public float EnvironmentBottomColorX { get; set; }
            public float EnvironmentBottomColorY { get; set; }
            public float EnvironmentBottomColorZ { get; set; }
            public float EnvironmentIntensity { get; set; }
            public float EnvironmentRoughnessMix { get; set; }
            public TonemapOperator ToneMappingOperator { get; set; } = TonemapOperator.Aces;
            public float EnvironmentExposure { get; set; } = 1f;
            public float EnvironmentBloomStrength { get; set; } = 0.40f;
            public float EnvironmentBloomThreshold { get; set; } = 1.10f;
            public float EnvironmentBloomKnee { get; set; } = 0.55f;
            public string EnvironmentHdriPath { get; set; } = string.Empty;
            public float EnvironmentHdriBlend { get; set; }
            public float EnvironmentHdriRotationDegrees { get; set; }
            public bool ShadowsEnabled { get; set; }
            public ShadowLightMode ShadowMode { get; set; } = ShadowLightMode.Weighted;
            public float ShadowStrength { get; set; }
            public float ShadowSoftness { get; set; }
            public float ShadowDistance { get; set; }
            public float ShadowScale { get; set; }
            public float ShadowQuality { get; set; }
            public float ShadowGray { get; set; }
            public float ShadowDiffuseInfluence { get; set; }
            public int PaintMaskSize { get; set; } = KnobProject.DefaultPaintMaskSize;
            public int PaintHistoryRevision { get; set; }
            public int ActivePaintLayerIndex { get; set; }
            public int FocusedPaintLayerIndex { get; set; } = -1;
            public bool BrushPaintingEnabled { get; set; }
            public PaintBrushType BrushType { get; set; }
            public PaintChannel BrushChannel { get; set; }
            public ScratchAbrasionType ScratchAbrasionType { get; set; }
            public float BrushSizePx { get; set; }
            public float BrushOpacity { get; set; }
            public float BrushSpread { get; set; }
            public float BrushDarkness { get; set; }
            public float PaintCoatMetallic { get; set; } = 0.02f;
            public float PaintCoatRoughness { get; set; } = 0.56f;
            public float RoughnessPaintTarget { get; set; } = 0.20f;
            public float MetallicPaintTarget { get; set; } = 0.90f;
            public float ClearCoatAmount { get; set; }
            public float ClearCoatRoughness { get; set; } = 0.18f;
            public float AnisotropyAngleDegrees { get; set; }
            public float PaintColorX { get; set; }
            public float PaintColorY { get; set; }
            public float PaintColorZ { get; set; }
            public float ScratchWidthPx { get; set; }
            public float ScratchDepth { get; set; }
            public float ScratchDragResistance { get; set; }
            public float ScratchDepthRamp { get; set; }
            public float ScratchExposeColorX { get; set; }
            public float ScratchExposeColorY { get; set; }
            public float ScratchExposeColorZ { get; set; }
            public float ScratchExposeMetallic { get; set; } = 0.92f;
            public float ScratchExposeRoughness { get; set; } = 0.20f;
            public bool SpiralNormalInfluenceEnabled { get; set; }
            public float SpiralNormalLodFadeStart { get; set; }
            public float SpiralNormalLodFadeEnd { get; set; }
            public float SpiralRoughnessLodBoost { get; set; }
            public bool HasProjectType { get; set; }
            public InteractorProjectType ProjectType { get; set; } = InteractorProjectType.RotaryKnob;
            public List<LightStateSnapshot> Lights { get; set; } = new();
            public int SelectedLightIndex { get; set; }
            public DynamicLightRigSnapshot DynamicLightRig { get; set; } = new();
            public bool HasModelMaterialSnapshot { get; set; }
            public UserReferenceProfileSnapshot? ModelMaterialSnapshot { get; set; }
            public List<MaterialNodeSnapshot> MaterialSnapshots { get; set; } = new();
            public ReferenceKnobStyle ModelReferenceStyle { get; set; } = ReferenceKnobStyle.Custom;
            public string? SelectedUserReferenceProfileName { get; set; }
            public CollarStateSnapshot? CollarSnapshot { get; set; }
            public SliderAssemblyMode SliderMode { get; set; } = SliderAssemblyMode.Auto;
            public float SliderBackplateWidth { get; set; }
            public float SliderBackplateHeight { get; set; }
            public float SliderBackplateThickness { get; set; }
            public float SliderThumbWidth { get; set; }
            public float SliderThumbHeight { get; set; }
            public float SliderThumbDepth { get; set; }
            public string SliderBackplateImportedMeshPath { get; set; } = string.Empty;
            public string SliderThumbImportedMeshPath { get; set; } = string.Empty;
            public ToggleAssemblyMode ToggleMode { get; set; } = ToggleAssemblyMode.Auto;
            public string ToggleBaseImportedMeshPath { get; set; } = string.Empty;
            public string ToggleLeverImportedMeshPath { get; set; } = string.Empty;
            public ToggleAssemblyStateCount ToggleStateCount { get; set; } = ToggleAssemblyStateCount.TwoPosition;
            public int ToggleStateIndex { get; set; }
            public float ToggleMaxAngleDeg { get; set; } = 24f;
            public float TogglePlateWidth { get; set; }
            public float TogglePlateHeight { get; set; }
            public float TogglePlateThickness { get; set; }
            public float TogglePlateOffsetY { get; set; }
            public float TogglePlateOffsetZ { get; set; }
            public float ToggleBushingRadius { get; set; }
            public float ToggleBushingHeight { get; set; }
            public int ToggleBushingSides { get; set; } = 6;
            public ToggleBushingShape ToggleLowerBushingShape { get; set; } = ToggleBushingShape.Hex;
            public ToggleBushingShape ToggleUpperBushingShape { get; set; } = ToggleBushingShape.Hex;
            public float ToggleLowerBushingRadiusScale { get; set; } = 1.22f;
            public float ToggleLowerBushingHeightRatio { get; set; } = 0.45f;
            public float ToggleUpperBushingRadiusScale { get; set; } = 1.00f;
            public float ToggleUpperBushingHeightRatio { get; set; } = 0.75f;
            public float ToggleUpperBushingKnurlAmount { get; set; }
            public int ToggleUpperBushingKnurlDensity { get; set; } = 20;
            public float ToggleUpperBushingKnurlDepth { get; set; } = 0.22f;
            public float ToggleUpperBushingAnisotropyStrength { get; set; }
            public float ToggleUpperBushingAnisotropyDensity { get; set; } = 48f;
            public float ToggleUpperBushingAnisotropyAngleDegrees { get; set; }
            public float ToggleUpperBushingSurfaceCharacter { get; set; } = 0.55f;
            public float TogglePivotHousingRadius { get; set; }
            public float TogglePivotHousingDepth { get; set; }
            public float TogglePivotHousingBevel { get; set; }
            public float TogglePivotBallRadius { get; set; }
            public float TogglePivotClearance { get; set; } = 1.2f;
            public bool ToggleInvertBaseFrontFaceWinding { get; set; }
            public bool ToggleInvertLeverFrontFaceWinding { get; set; }
            public float ToggleLeverLength { get; set; }
            public float ToggleLeverRadius { get; set; }
            public float ToggleLeverTopRadius { get; set; }
            public int ToggleLeverSides { get; set; } = 20;
            public float ToggleLeverPivotOffset { get; set; }
            public float ToggleTipRadius { get; set; }
            public int ToggleTipLatitudeSegments { get; set; } = 10;
            public int ToggleTipLongitudeSegments { get; set; } = 16;
            public bool ToggleTipSleeveEnabled { get; set; } = true;
            public float ToggleTipSleeveLength { get; set; }
            public float ToggleTipSleeveThickness { get; set; }
            public float ToggleTipSleeveOuterRadius { get; set; }
            public float ToggleTipSleeveCoverage { get; set; } = 0.55f;
            public int ToggleTipSleeveSides { get; set; } = 24;
            public ToggleTipSleeveStyle ToggleTipSleeveStyle { get; set; } = ToggleTipSleeveStyle.Round;
            public ToggleTipSleeveTipStyle ToggleTipSleeveTipStyle { get; set; } = ToggleTipSleeveTipStyle.Rounded;
            public int ToggleTipSleevePatternCount { get; set; } = 14;
            public float ToggleTipSleevePatternDepth { get; set; } = 0.22f;
            public float ToggleTipSleeveTipAmount { get; set; } = 0.35f;
            public float ToggleTipSleeveColorX { get; set; } = 0.82f;
            public float ToggleTipSleeveColorY { get; set; } = 0.83f;
            public float ToggleTipSleeveColorZ { get; set; } = 0.86f;
            public float ToggleTipSleeveMetallic { get; set; } = 0.94f;
            public float ToggleTipSleeveRoughness { get; set; } = 0.22f;
            public float ToggleTipSleevePearlescence { get; set; } = 0.03f;
            public float ToggleTipSleeveDiffuseStrength { get; set; } = 1f;
            public float ToggleTipSleeveSpecularStrength { get; set; } = 1f;
            public float ToggleTipSleeveRustAmount { get; set; }
            public float ToggleTipSleeveWearAmount { get; set; }
            public float ToggleTipSleeveGunkAmount { get; set; }
            public bool IndicatorAssemblyEnabled { get; set; } = true;
            public float IndicatorBaseWidth { get; set; }
            public float IndicatorBaseHeight { get; set; }
            public float IndicatorBaseThickness { get; set; }
            public float IndicatorHousingRadius { get; set; }
            public float IndicatorHousingHeight { get; set; }
            public float IndicatorLensRadius { get; set; }
            public float IndicatorLensHeight { get; set; }
            public float IndicatorLensTransmission { get; set; } = 0.88f;
            public float IndicatorLensIor { get; set; } = 1.49f;
            public float IndicatorLensThickness { get; set; } = 1.0f;
            public float IndicatorLensTintX { get; set; } = 0.78f;
            public float IndicatorLensTintY { get; set; } = 0.92f;
            public float IndicatorLensTintZ { get; set; } = 0.84f;
            public float IndicatorLensAbsorption { get; set; } = 1.2f;
            public float IndicatorLensSurfaceRoughness { get; set; } = 0.14f;
            public float IndicatorLensSurfaceSpecularStrength { get; set; } = 1.25f;
            public float IndicatorReflectorBaseRadius { get; set; }
            public float IndicatorReflectorTopRadius { get; set; }
            public float IndicatorReflectorDepth { get; set; }
            public float IndicatorEmitterRadius { get; set; }
            public float IndicatorEmitterSpread { get; set; }
            public float IndicatorEmitterDepth { get; set; }
            public int IndicatorEmitterCount { get; set; } = 3;
            public int IndicatorRadialSegments { get; set; } = 56;
            public int IndicatorLensLatitudeSegments { get; set; } = 20;
            public int IndicatorLensLongitudeSegments { get; set; } = 40;
            public SceneSelectionSnapshot Selection { get; set; } = new();
        }

        private sealed class MaterialNodeSnapshot
        {
            public string Name { get; set; } = "Material";
            public float BaseColorX { get; set; } = 0.55f;
            public float BaseColorY { get; set; } = 0.16f;
            public float BaseColorZ { get; set; } = 0.16f;
            public float Metallic { get; set; } = 1f;
            public float Roughness { get; set; } = 0.04f;
            public float Pearlescence { get; set; }
            public float RustAmount { get; set; }
            public float WearAmount { get; set; }
            public float GunkAmount { get; set; }
            public float RadialBrushStrength { get; set; } = 0.65f;
            public float RadialBrushDensity { get; set; } = 280.5f;
            public float SurfaceCharacter { get; set; } = 1f;
            public float SpecularPower { get; set; } = 64f;
            public float DiffuseStrength { get; set; } = 1f;
            public float SpecularStrength { get; set; } = 1f;
            public bool PartMaterialsEnabled { get; set; }
            public float TopBaseColorX { get; set; } = 0.55f;
            public float TopBaseColorY { get; set; } = 0.16f;
            public float TopBaseColorZ { get; set; } = 0.16f;
            public float TopMetallic { get; set; } = 1f;
            public float TopRoughness { get; set; } = 0.04f;
            public float BevelBaseColorX { get; set; } = 0.55f;
            public float BevelBaseColorY { get; set; } = 0.16f;
            public float BevelBaseColorZ { get; set; } = 0.16f;
            public float BevelMetallic { get; set; } = 1f;
            public float BevelRoughness { get; set; } = 0.04f;
            public float SideBaseColorX { get; set; } = 0.55f;
            public float SideBaseColorY { get; set; } = 0.16f;
            public float SideBaseColorZ { get; set; } = 0.16f;
            public float SideMetallic { get; set; } = 1f;
            public float SideRoughness { get; set; } = 0.04f;
            public string? AlbedoMapPath { get; set; }
            public string? NormalMapPath { get; set; }
            public string? RoughnessMapPath { get; set; }
            public string? MetallicMapPath { get; set; }
            public float NormalMapStrength { get; set; } = 1f;
        }

        private sealed class LightStateSnapshot
        {
            public string Name { get; set; } = "Light";
            public LightType Type { get; set; } = LightType.Point;
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float DirectionRadians { get; set; }
            public byte ColorR { get; set; }
            public byte ColorG { get; set; }
            public byte ColorB { get; set; }
            public byte ColorA { get; set; } = byte.MaxValue;
            public float Intensity { get; set; }
            public float Falloff { get; set; }
            public float DiffuseBoost { get; set; }
            public float SpecularBoost { get; set; }
            public float SpecularPower { get; set; }
        }

        private sealed class DynamicLightRigSnapshot
        {
            public bool Enabled { get; set; }
            public int MaxActiveLights { get; set; } = DynamicLightRig.DefaultMaxActiveLights;
            public DynamicLightAnimationMode AnimationMode { get; set; } = DynamicLightAnimationMode.Steady;
            public float AnimationSpeed { get; set; } = 1f;
            public float FlickerAmount { get; set; }
            public float FlickerDropoutChance { get; set; }
            public float FlickerSmoothing { get; set; } = 0.5f;
            public int FlickerSeed { get; set; } = 1337;
            public List<DynamicLightSourceSnapshot> Sources { get; set; } = new();
        }

        private sealed class DynamicLightSourceSnapshot
        {
            public string Name { get; set; } = "Emitter";
            public bool Enabled { get; set; } = true;
            public float AnimationPhaseOffsetDegrees { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public byte ColorR { get; set; } = 180;
            public byte ColorG { get; set; } = 255;
            public byte ColorB { get; set; } = 210;
            public byte ColorA { get; set; } = byte.MaxValue;
            public float Intensity { get; set; } = 1f;
            public float Radius { get; set; } = 220f;
            public float Falloff { get; set; } = 1f;
        }

        private sealed class CollarStateSnapshot
        {
            public bool Enabled { get; set; }
            public CollarPreset Preset { get; set; }
            public float InnerRadiusRatio { get; set; }
            public float GapToKnobRatio { get; set; }
            public float ElevationRatio { get; set; }
            public float OverallRotationRadians { get; set; }
            public float BiteAngleRadians { get; set; }
            public float BodyRadiusRatio { get; set; }
            public float BodyEllipseYScale { get; set; }
            public float NeckTaper { get; set; }
            public float TailTaper { get; set; }
            public float MassBias { get; set; }
            public float TailUnderlap { get; set; }
            public float HeadScale { get; set; }
            public float JawBulge { get; set; }
            public bool UvSeamFollowBite { get; set; }
            public float UvSeamOffset { get; set; }
            public int PathSegments { get; set; }
            public int CrossSegments { get; set; }
            public float BaseColorX { get; set; }
            public float BaseColorY { get; set; }
            public float BaseColorZ { get; set; }
            public float Metallic { get; set; }
            public float Roughness { get; set; }
            public float Pearlescence { get; set; }
            public float RustAmount { get; set; }
            public float WearAmount { get; set; }
            public float GunkAmount { get; set; }
            public float NormalStrength { get; set; }
            public float HeightStrength { get; set; }
            public float ScaleDensity { get; set; }
            public float ScaleRelief { get; set; }
            public string ImportedMeshPath { get; set; } = string.Empty;
            public float ImportedScale { get; set; }
            public float ImportedRotationRadians { get; set; }
            public bool ImportedMirrorX { get; set; }
            public bool ImportedMirrorY { get; set; }
            public bool ImportedMirrorZ { get; set; }
            public float ImportedHeadAngleOffsetRadians { get; set; }
            public float ImportedOffsetXRatio { get; set; }
            public float ImportedOffsetYRatio { get; set; }
            public float ImportedInflateRatio { get; set; }
            public float ImportedBodyLengthScale { get; set; }
            public float ImportedBodyThicknessScale { get; set; }
            public float ImportedHeadLengthScale { get; set; }
            public float ImportedHeadThicknessScale { get; set; }
        }

        private sealed class SceneSelectionSnapshot
        {
            public SceneSelectionKind Kind { get; set; } = SceneSelectionKind.Unknown;
            public int LightIndex { get; set; } = -1;
            public int MaterialIndex { get; set; } = -1;
        }

        private enum SceneSelectionKind
        {
            Unknown = 0,
            SceneRoot = 1,
            Model = 2,
            Material = 3,
            Collar = 4,
            Light = 5
        }
    }
}
