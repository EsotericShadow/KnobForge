using KnobForge.Core.Scene;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;

namespace KnobForge.Core
{
    public enum LightType
    {
        Point,
        Directional
    }

    public enum LightingMode
    {
        Realistic,
        Artistic,
        Both
    }

    public enum ShadowLightMode
    {
        Selected,
        Dominant,
        Weighted
    }

    public enum BasisDebugMode
    {
        Off = 0,
        Normal = 1,
        Tangent = 2,
        Bitangent = 3
    }

    public enum TonemapOperator
    {
        Aces = 0,
        AgX = 1
    }

    public enum PaintBrushType
    {
        Spray,
        Stroke,
        Circle,
        Square,
        Splat
    }

    public enum ScratchAbrasionType
    {
        Needle,
        Chisel,
        Burr,
        Scuff
    }

    public enum PaintChannel
    {
        Rust = 0,
        Wear = 1,
        Gunk = 2,
        Scratch = 3,
        Erase = 4,
        Color = 5,
        Roughness = 6,
        Metallic = 7
    }

    public enum PaintBlendMode
    {
        Normal = 0,
        Multiply = 1,
        Screen = 2,
        Overlay = 3,
        Add = 4
    }

    public enum SliderAssemblyMode
    {
        Auto = 0,
        Enabled = 1,
        Disabled = 2
    }

    public enum SliderThumbProfile
    {
        Box = 0,
        Rounded = 1,
        Ridged = 2,
        Pointer = 3,
        BarHandle = 4
    }

    public enum SliderTrackStyle
    {
        None = 0,
        Channel = 1,
        VGroove = 2,
        Rail = 3
    }

    public enum ToggleAssemblyMode
    {
        Auto = 0,
        Enabled = 1,
        Disabled = 2
    }

    public enum ToggleAssemblyStateCount
    {
        TwoPosition = 2,
        ThreePosition = 3
    }

    public enum ToggleBushingShape
    {
        Hex = 0,
        Octagon = 1,
        Round = 2,
        Square = 3
    }

    public enum ToggleTipSleeveStyle
    {
        Round = 0,
        Hex = 1,
        Octagon = 2,
        Fluted = 3,
        KnurledSquare = 4,
        KnurledDiamond = 5
    }

    public enum ToggleTipSleeveTipStyle
    {
        Flat = 0,
        Bevel = 1,
        Rounded = 2
    }

    public enum PushButtonCapProfile
    {
        Flat = 0,
        Domed = 1,
        Concave = 2,
        Stepped = 3,
        Mushroom = 4
    }

    public enum PushButtonBezelProfile
    {
        Straight = 0,
        Chamfered = 1,
        Filleted = 2,
        Flared = 3
    }

    public enum PushButtonSkirtStyle
    {
        None = 0,
        Ring = 1,
        Collar = 2,
        Flange = 3
    }

    public enum RenderQualityTier
    {
        Draft = 0,
        Normal = 1,
        Production = 2
    }

    public enum InteractorProjectType
    {
        RotaryKnob = 0,
        FlipSwitch = 1,
        ThumbSlider = 2,
        PushButton = 3,
        IndicatorLight = 4
    }

    public sealed class PaintLayer
    {
        public string Name { get; set; } = "Layer";
        public float Opacity { get; set; } = 1.0f;
        public PaintBlendMode BlendMode { get; set; } = PaintBlendMode.Normal;
        public bool Visible { get; set; } = true;
        public byte[]? PixelData { get; set; }
        public byte[]? ColorPixelData { get; set; }
        public byte[]? PixelData2 { get; set; }
    }

    public sealed class KnobLight
    {
        public string Name { get; set; } = "Light";
        public LightType Type { get; set; } = LightType.Point;
        public float X { get; set; } = 0f;
        public float Y { get; set; } = 0f;
        public float Z { get; set; } = 0f;
        public float DirectionRadians { get; set; } = 0f;
        public SKColor Color { get; set; } = SKColors.White;
        public float Intensity { get; set; } = 1.0f;
        public float Falloff { get; set; } = 1.0f;
        public float DiffuseBoost { get; set; } = 1.0f;
        public float SpecularBoost { get; set; } = 1.0f;
        public float SpecularPower { get; set; } = 64f;
    }

    public class KnobProject
    {
        public const int DefaultPaintMaskSize = 1024;
        private const int LegacyIndicatorRadialSegments = 32;
        private const int LegacyIndicatorLensLatitudeSegments = 12;
        private const int LegacyIndicatorLensLongitudeSegments = 20;
        private const int DefaultIndicatorRadialSegments = 56;
        private const int DefaultIndicatorLensLatitudeSegments = 20;
        private const int DefaultIndicatorLensLongitudeSegments = 40;
        private float _spiralNormalLodFadeStart = 4.22f;
        private float _spiralNormalLodFadeEnd = 4.23f;
        private float _spiralRoughnessLodBoost = 0.78f;
        private float _shadowStrength = 1.0f;
        private float _shadowSoftness = 0.55f;
        private float _shadowDistance = 1.0f;
        private float _shadowScale = 1.0f;
        private float _shadowQuality = 0.65f;
        private float _shadowGray = 0.14f;
        private float _shadowDiffuseInfluence = 1.0f;
        private bool _brushPaintingEnabled;
        private PaintBrushType _brushType = PaintBrushType.Spray;
        private PaintChannel _brushChannel = PaintChannel.Rust;
        private ScratchAbrasionType _scratchAbrasionType = ScratchAbrasionType.Needle;
        private float _brushSizePx = 32f;
        private float _brushOpacity = 0.50f;
        private float _brushSpread = 0.35f;
        private float _brushDarkness = 0.58f;
        private float _paintCoatMetallic = 0.02f;
        private float _paintCoatRoughness = 0.56f;
        private float _roughnessPaintTarget = 0.20f;
        private float _metallicPaintTarget = 0.90f;
        private float _clearCoatAmount = 0f;
        private float _clearCoatRoughness = 0.18f;
        private float _anisotropyAngleDegrees = 0f;
        private Vector3 _paintColor = new(0.85f, 0.24f, 0.24f);
        private Vector3 _scratchExposeColor = new(0.88f, 0.88f, 0.90f);
        private float _scratchExposeMetallic = 0.92f;
        private float _scratchExposeRoughness = 0.20f;
        private float _scratchWidthPx = 20f;
        private float _scratchDepth = 0.45f;
        private float _scratchDragResistance = 0.38f;
        private float _scratchDepthRamp = 0.0015f;
        private float _sliderBackplateWidth;
        private float _sliderBackplateHeight;
        private float _sliderBackplateThickness;
        private float _sliderThumbWidth;
        private float _sliderThumbHeight;
        private float _sliderThumbDepth;
        private float _sliderThumbPositionNormalized = 0.5f;
        private SliderThumbProfile _sliderThumbProfile = SliderThumbProfile.Box;
        private SliderTrackStyle _sliderTrackStyle = SliderTrackStyle.None;
        private float _sliderTrackWidth;
        private float _sliderTrackDepth;
        private float _sliderRailHeight;
        private float _sliderRailSpacing;
        private int _sliderThumbRidgeCount;
        private float _sliderThumbRidgeDepth;
        private float _sliderThumbCornerRadius;
        private float _pushButtonPressAmountNormalized;
        private PushButtonCapProfile _pushButtonCapProfile = PushButtonCapProfile.Flat;
        private PushButtonBezelProfile _pushButtonBezelProfile = PushButtonBezelProfile.Straight;
        private PushButtonSkirtStyle _pushButtonSkirtStyle = PushButtonSkirtStyle.None;
        private float _pushButtonBezelChamferSize;
        private float _pushButtonCapOverhang;
        private int _pushButtonCapSegments;
        private int _pushButtonBezelSegments;
        private float _pushButtonSkirtHeight;
        private float _pushButtonSkirtRadius;
        private string _pushButtonBaseImportedMeshPath = string.Empty;
        private string _pushButtonCapImportedMeshPath = string.Empty;
        private bool _indicatorAssemblyEnabled = true;
        private float _indicatorBaseWidth;
        private float _indicatorBaseHeight;
        private float _indicatorBaseThickness;
        private float _indicatorHousingRadius;
        private float _indicatorHousingHeight;
        private float _indicatorLensRadius;
        private float _indicatorLensHeight;
        private float _indicatorLensTransmission = IndicatorLensMaterialPresets.Clear.Transmission;
        private float _indicatorLensIor = IndicatorLensMaterialPresets.Clear.Ior;
        private float _indicatorLensThickness = IndicatorLensMaterialPresets.Clear.Thickness;
        private Vector3 _indicatorLensTint = IndicatorLensMaterialPresets.Clear.Tint;
        private float _indicatorLensAbsorption = IndicatorLensMaterialPresets.Clear.Absorption;
        private float _indicatorLensSurfaceRoughness = IndicatorLensMaterialPresets.Clear.SurfaceRoughness;
        private float _indicatorLensSurfaceSpecularStrength = IndicatorLensMaterialPresets.Clear.SurfaceSpecularStrength;
        private float _indicatorReflectorBaseRadius;
        private float _indicatorReflectorTopRadius;
        private float _indicatorReflectorDepth;
        private float _indicatorEmitterRadius;
        private float _indicatorEmitterSpread;
        private float _indicatorEmitterDepth;
        private int _indicatorEmitterCount = 3;
        private int _indicatorRadialSegments = DefaultIndicatorRadialSegments;
        private int _indicatorLensLatitudeSegments = DefaultIndicatorLensLatitudeSegments;
        private int _indicatorLensLongitudeSegments = DefaultIndicatorLensLongitudeSegments;
        private string _sliderBackplateImportedMeshPath = string.Empty;
        private string _sliderThumbImportedMeshPath = string.Empty;
        private float _togglePlateWidth;
        private float _togglePlateHeight;
        private float _togglePlateThickness;
        private float _togglePlateOffsetY;
        private float _togglePlateOffsetZ;
        private float _toggleBushingRadius;
        private float _toggleBushingHeight;
        private int _toggleBushingSides = 6;
        private ToggleBushingShape _toggleLowerBushingShape = ToggleBushingShape.Hex;
        private ToggleBushingShape _toggleUpperBushingShape = ToggleBushingShape.Hex;
        private float _toggleLowerBushingRadiusScale = 1.22f;
        private float _toggleLowerBushingHeightRatio = 0.45f;
        private float _toggleUpperBushingRadiusScale = 1.00f;
        private float _toggleUpperBushingHeightRatio = 0.75f;
        private float _toggleUpperBushingKnurlAmount;
        private int _toggleUpperBushingKnurlDensity = 20;
        private float _toggleUpperBushingKnurlDepth = 0.22f;
        private float _toggleUpperBushingAnisotropyStrength;
        private float _toggleUpperBushingAnisotropyDensity = 48f;
        private float _toggleUpperBushingAnisotropyAngleDegrees;
        private float _toggleUpperBushingSurfaceCharacter = 0.55f;
        private float _togglePivotHousingRadius;
        private float _togglePivotHousingDepth;
        private float _togglePivotHousingBevel;
        private float _togglePivotBallRadius;
        private float _togglePivotClearance = 1.2f;
        private bool _toggleInvertBaseFrontFaceWinding;
        private bool _toggleInvertLeverFrontFaceWinding;
        private float _toggleLeverLength;
        private float _toggleLeverRadius;
        private float _toggleLeverTopRadius;
        private int _toggleLeverSides = 20;
        private float _toggleLeverPivotOffset;
        private float _toggleTipRadius;
        private int _toggleTipLatitudeSegments = 10;
        private int _toggleTipLongitudeSegments = 16;
        private bool _toggleTipSleeveEnabled = true;
        private float _toggleTipSleeveLength;
        private float _toggleTipSleeveThickness;
        private float _toggleTipSleeveOuterRadius;
        private float _toggleTipSleeveCoverage = 0.55f;
        private int _toggleTipSleeveSides = 24;
        private ToggleTipSleeveStyle _toggleTipSleeveStyle = ToggleTipSleeveStyle.Round;
        private ToggleTipSleeveTipStyle _toggleTipSleeveTipStyle = ToggleTipSleeveTipStyle.Rounded;
        private int _toggleTipSleevePatternCount = 14;
        private float _toggleTipSleevePatternDepth = 0.22f;
        private float _toggleTipSleeveTipAmount = 0.35f;
        private Vector3 _toggleTipSleeveColor = new(0.82f, 0.83f, 0.86f);
        private float _toggleTipSleeveMetallic = 0.94f;
        private float _toggleTipSleeveRoughness = 0.22f;
        private float _toggleTipSleevePearlescence = 0.03f;
        private float _toggleTipSleeveDiffuseStrength = 1f;
        private float _toggleTipSleeveSpecularStrength = 1f;
        private float _toggleTipSleeveRustAmount;
        private float _toggleTipSleeveWearAmount;
        private float _toggleTipSleeveGunkAmount;
        private string _toggleBaseImportedMeshPath = string.Empty;
        private string _toggleLeverImportedMeshPath = string.Empty;
        private float _toggleMaxAngleDeg = 24f;
        private ToggleAssemblyStateCount _toggleStateCount = ToggleAssemblyStateCount.TwoPosition;
        private int _toggleStateIndex;
        private float _toggleStateBlendPosition = float.NaN;
        private float _environmentExposure = 1f;
        private float _environmentBloomStrength = 0.40f;
        private float _environmentBloomThreshold = 1.10f;
        private float _environmentBloomKnee = 0.55f;
        private float _environmentHdriBlend;
        private float _environmentHdriRotationDegrees;
        private string _environmentHdriPath = string.Empty;
        private readonly List<PaintLayer> _paintLayers = new();
        private byte[] _paintMaskRgba8 = new byte[DefaultPaintMaskSize * DefaultPaintMaskSize * 4];
        private byte[] _paintColorRgba8 = new byte[DefaultPaintMaskSize * DefaultPaintMaskSize * 4];
        private byte[] _paintMask2Rgba8 = new byte[DefaultPaintMaskSize * DefaultPaintMaskSize * 4];
        private int _paintMaskVersion = 1;
        private int _paintColorVersion = 1;
        private int _paintMask2Version = 1;
        private int _paintRecomposeSuspendCount;
        private bool _paintRecomposeDeferred;
        private bool _paintRecomposeDeferredIncrementVersions;

        public SKBitmap BaseTexture { get; private set; }
        public int Width => BaseTexture.Width;
        public int Height => BaseTexture.Height;
        public LightingMode Mode { get; set; } = LightingMode.Both;
        public Vector3 EnvironmentTopColor { get; set; } = new(0.34f, 0.36f, 0.37f);
        public Vector3 EnvironmentBottomColor { get; set; } = new(0f, 0f, 0f);
        public float EnvironmentIntensity { get; set; } = 0.36f;
        public float EnvironmentRoughnessMix { get; set; } = 1.0f;
        public EnvironmentPreset EnvironmentPreset { get; set; } = EnvironmentPreset.Custom;
        public TonemapOperator ToneMappingOperator { get; set; } = TonemapOperator.Aces;
        public BloomKernelShape BloomKernelShape { get; set; } = BloomKernelShape.Soft;
        public float EnvironmentExposure
        {
            get => _environmentExposure;
            set => _environmentExposure = ClampFinite(value, 1f, 0.10f, 4.00f);
        }
        public float EnvironmentBloomStrength
        {
            get => _environmentBloomStrength;
            set => _environmentBloomStrength = ClampFinite(value, 0.40f, 0f, 4.00f);
        }
        public float EnvironmentBloomThreshold
        {
            get => _environmentBloomThreshold;
            set => _environmentBloomThreshold = ClampFinite(value, 1.10f, 0f, 16.00f);
        }
        public float EnvironmentBloomKnee
        {
            get => _environmentBloomKnee;
            set => _environmentBloomKnee = ClampFinite(value, 0.55f, 0.001f, 8.00f);
        }
        public string EnvironmentHdriPath
        {
            get => _environmentHdriPath;
            set => _environmentHdriPath = NormalizeOptionalPath(value);
        }
        public float EnvironmentHdriBlend
        {
            get => _environmentHdriBlend;
            set => _environmentHdriBlend = ClampFinite(value, 0f, 0f, 1f);
        }
        public float EnvironmentHdriRotationDegrees
        {
            get => _environmentHdriRotationDegrees;
            set => _environmentHdriRotationDegrees = ClampFinite(value, 0f, -360f, 360f);
        }
        public BasisDebugMode BasisDebug { get; set; } = BasisDebugMode.Off;
        public bool ShadowsEnabled { get; set; } = true;
        public ShadowLightMode ShadowMode { get; set; } = ShadowLightMode.Weighted;
        public float ShadowStrength
        {
            get => _shadowStrength;
            set => _shadowStrength = Math.Clamp(value, 0f, 2.5f);
        }
        public float ShadowSoftness
        {
            get => _shadowSoftness;
            set => _shadowSoftness = Math.Clamp(value, 0f, 1f);
        }
        public float ShadowDistance
        {
            get => _shadowDistance;
            set => _shadowDistance = Math.Clamp(value, 0f, 2.5f);
        }
        public float ShadowScale
        {
            get => _shadowScale;
            set => _shadowScale = Math.Clamp(value, 0.7f, 1.6f);
        }
        public float ShadowQuality
        {
            get => _shadowQuality;
            set => _shadowQuality = Math.Clamp(value, 0f, 1f);
        }
        public float ShadowGray
        {
            get => _shadowGray;
            set => _shadowGray = Math.Clamp(value, 0f, 0.6f);
        }
        public float ShadowDiffuseInfluence
        {
            get => _shadowDiffuseInfluence;
            set => _shadowDiffuseInfluence = Math.Clamp(value, 0f, 2f);
        }
        public bool SpiralNormalInfluenceEnabled { get; set; } = true;
        public bool BrushPaintingEnabled
        {
            get => _brushPaintingEnabled;
            set => _brushPaintingEnabled = value;
        }
        public PaintBrushType BrushType
        {
            get => _brushType;
            set => _brushType = value;
        }
        public PaintChannel BrushChannel
        {
            get => _brushChannel;
            set => _brushChannel = value;
        }
        public ScratchAbrasionType ScratchAbrasionType
        {
            get => _scratchAbrasionType;
            set => _scratchAbrasionType = value;
        }
        public float BrushSizePx
        {
            get => _brushSizePx;
            set => _brushSizePx = Math.Clamp(value, 1f, 320f);
        }
        public float BrushOpacity
        {
            get => _brushOpacity;
            set => _brushOpacity = Math.Clamp(value, 0f, 1f);
        }
        public float BrushSpread
        {
            get => _brushSpread;
            set => _brushSpread = Math.Clamp(value, 0f, 1f);
        }
        public float BrushDarkness
        {
            get => _brushDarkness;
            set => _brushDarkness = Math.Clamp(value, 0f, 1f);
        }
        public float PaintCoatMetallic
        {
            get => _paintCoatMetallic;
            set => _paintCoatMetallic = Math.Clamp(value, 0f, 1f);
        }
        public float PaintCoatRoughness
        {
            get => _paintCoatRoughness;
            set => _paintCoatRoughness = Math.Clamp(value, 0.04f, 1f);
        }
        public float RoughnessPaintTarget
        {
            get => _roughnessPaintTarget;
            set => _roughnessPaintTarget = Math.Clamp(value, 0f, 1f);
        }
        public float MetallicPaintTarget
        {
            get => _metallicPaintTarget;
            set => _metallicPaintTarget = Math.Clamp(value, 0f, 1f);
        }
        public float ClearCoatAmount
        {
            get => _clearCoatAmount;
            set => _clearCoatAmount = Math.Clamp(value, 0f, 1f);
        }
        public float ClearCoatRoughness
        {
            get => _clearCoatRoughness;
            set => _clearCoatRoughness = Math.Clamp(value, 0.04f, 1f);
        }
        public float AnisotropyAngleDegrees
        {
            get => _anisotropyAngleDegrees;
            set => _anisotropyAngleDegrees = Math.Clamp(value, -180f, 180f);
        }
        public Vector3 PaintColor
        {
            get => _paintColor;
            set => _paintColor = new Vector3(
                Math.Clamp(value.X, 0f, 1f),
                Math.Clamp(value.Y, 0f, 1f),
                Math.Clamp(value.Z, 0f, 1f));
        }
        public Vector3 ScratchExposeColor
        {
            get => _scratchExposeColor;
            set => _scratchExposeColor = new Vector3(
                Math.Clamp(value.X, 0f, 1f),
                Math.Clamp(value.Y, 0f, 1f),
                Math.Clamp(value.Z, 0f, 1f));
        }
        public float ScratchExposeMetallic
        {
            get => _scratchExposeMetallic;
            set => _scratchExposeMetallic = Math.Clamp(value, 0f, 1f);
        }
        public float ScratchExposeRoughness
        {
            get => _scratchExposeRoughness;
            set => _scratchExposeRoughness = Math.Clamp(value, 0.04f, 1f);
        }
        public float ScratchWidthPx
        {
            get => _scratchWidthPx;
            set => _scratchWidthPx = Math.Clamp(value, 1f, 320f);
        }
        public float ScratchDepth
        {
            get => _scratchDepth;
            set => _scratchDepth = Math.Clamp(value, 0f, 1f);
        }
        public float ScratchDragResistance
        {
            get => _scratchDragResistance;
            set => _scratchDragResistance = Math.Clamp(value, 0f, 0.98f);
        }
        public float ScratchDepthRamp
        {
            get => _scratchDepthRamp;
            set => _scratchDepthRamp = Math.Clamp(value, 0f, 0.02f);
        }
        public int PaintMaskSize { get; private set; } = DefaultPaintMaskSize;
        public int PaintMaskVersion => _paintMaskVersion;
        public int PaintColorVersion => _paintColorVersion;
        public int PaintMask2Version => _paintMask2Version;
        public IReadOnlyList<PaintLayer> PaintLayers => _paintLayers;
        public float SpiralNormalLodFadeStart
        {
            get => _spiralNormalLodFadeStart;
            set
            {
                _spiralNormalLodFadeStart = Math.Clamp(value, 0.1f, 10f);
                if (_spiralNormalLodFadeEnd < _spiralNormalLodFadeStart + 0.01f)
                {
                    _spiralNormalLodFadeEnd = _spiralNormalLodFadeStart + 0.01f;
                }
            }
        }
        public float SpiralNormalLodFadeEnd
        {
            get => _spiralNormalLodFadeEnd;
            set => _spiralNormalLodFadeEnd = Math.Max(SpiralNormalLodFadeStart + 0.01f, Math.Clamp(value, 0.1f, 12f));
        }
        public float SpiralRoughnessLodBoost
        {
            get => _spiralRoughnessLodBoost;
            set => _spiralRoughnessLodBoost = Math.Clamp(value, 0f, 1f);
        }
        public SliderAssemblyMode SliderMode { get; set; } = SliderAssemblyMode.Auto;
        public float SliderBackplateWidth
        {
            get => _sliderBackplateWidth;
            set => _sliderBackplateWidth = ClampSliderDimensionOverride(value);
        }
        public float SliderBackplateHeight
        {
            get => _sliderBackplateHeight;
            set => _sliderBackplateHeight = ClampSliderDimensionOverride(value);
        }
        public float SliderBackplateThickness
        {
            get => _sliderBackplateThickness;
            set => _sliderBackplateThickness = ClampSliderDimensionOverride(value);
        }
        public float SliderThumbWidth
        {
            get => _sliderThumbWidth;
            set => _sliderThumbWidth = ClampSliderDimensionOverride(value);
        }
        public float SliderThumbHeight
        {
            get => _sliderThumbHeight;
            set => _sliderThumbHeight = ClampSliderDimensionOverride(value);
        }
        public float SliderThumbDepth
        {
            get => _sliderThumbDepth;
            set => _sliderThumbDepth = ClampSliderDimensionOverride(value);
        }
        public float SliderThumbPositionNormalized
        {
            get => _sliderThumbPositionNormalized;
            set => _sliderThumbPositionNormalized = Math.Clamp(value, 0f, 1f);
        }
        public SliderThumbProfile SliderThumbProfile
        {
            get => _sliderThumbProfile;
            set => _sliderThumbProfile = value;
        }
        public SliderTrackStyle SliderTrackStyle
        {
            get => _sliderTrackStyle;
            set => _sliderTrackStyle = value;
        }
        public float SliderTrackWidth
        {
            get => _sliderTrackWidth;
            set => _sliderTrackWidth = ClampSliderDimensionOverride(value);
        }
        public float SliderTrackDepth
        {
            get => _sliderTrackDepth;
            set => _sliderTrackDepth = ClampSliderDimensionOverride(value);
        }
        public float SliderRailHeight
        {
            get => _sliderRailHeight;
            set => _sliderRailHeight = ClampSliderDimensionOverride(value);
        }
        public float SliderRailSpacing
        {
            get => _sliderRailSpacing;
            set => _sliderRailSpacing = ClampSliderDimensionOverride(value);
        }
        public int SliderThumbRidgeCount
        {
            get => _sliderThumbRidgeCount;
            set => _sliderThumbRidgeCount = ClampSliderGeometrySegments(value, 3, 16);
        }
        public float SliderThumbRidgeDepth
        {
            get => _sliderThumbRidgeDepth;
            set => _sliderThumbRidgeDepth = ClampSliderDimensionOverride(value);
        }
        public float SliderThumbCornerRadius
        {
            get => _sliderThumbCornerRadius;
            set => _sliderThumbCornerRadius = ClampSliderDimensionOverride(value);
        }
        public float PushButtonPressAmountNormalized
        {
            get => _pushButtonPressAmountNormalized;
            set => _pushButtonPressAmountNormalized = Math.Clamp(value, 0f, 1f);
        }
        public PushButtonCapProfile PushButtonCapProfile
        {
            get => _pushButtonCapProfile;
            set => _pushButtonCapProfile = value;
        }
        public PushButtonBezelProfile PushButtonBezelProfile
        {
            get => _pushButtonBezelProfile;
            set => _pushButtonBezelProfile = value;
        }
        public PushButtonSkirtStyle PushButtonSkirtStyle
        {
            get => _pushButtonSkirtStyle;
            set => _pushButtonSkirtStyle = value;
        }
        public float PushButtonBezelChamferSize
        {
            get => _pushButtonBezelChamferSize;
            set => _pushButtonBezelChamferSize = ClampPushButtonDimensionOverride(value);
        }
        public float PushButtonCapOverhang
        {
            get => _pushButtonCapOverhang;
            set => _pushButtonCapOverhang = ClampPushButtonDimensionOverride(value);
        }
        public int PushButtonCapSegments
        {
            get => _pushButtonCapSegments;
            set => _pushButtonCapSegments = ClampPushButtonSegments(value, 28, 6, 128);
        }
        public int PushButtonBezelSegments
        {
            get => _pushButtonBezelSegments;
            set => _pushButtonBezelSegments = ClampPushButtonSegments(value, 28, 6, 128);
        }
        public float PushButtonSkirtHeight
        {
            get => _pushButtonSkirtHeight;
            set => _pushButtonSkirtHeight = ClampPushButtonDimensionOverride(value);
        }
        public float PushButtonSkirtRadius
        {
            get => _pushButtonSkirtRadius;
            set => _pushButtonSkirtRadius = ClampPushButtonDimensionOverride(value);
        }
        public string PushButtonBaseImportedMeshPath
        {
            get => _pushButtonBaseImportedMeshPath;
            set => _pushButtonBaseImportedMeshPath = NormalizeOptionalPath(value);
        }
        public string PushButtonCapImportedMeshPath
        {
            get => _pushButtonCapImportedMeshPath;
            set => _pushButtonCapImportedMeshPath = NormalizeOptionalPath(value);
        }
        public ToggleMaterialPresetId ToggleMaterialPreset { get; set; } = ToggleMaterialPresetId.Custom;
        public SliderMaterialPresetId SliderMaterialPreset { get; set; } = SliderMaterialPresetId.Custom;
        public PushButtonMaterialPresetId PushButtonMaterialPreset { get; set; } = PushButtonMaterialPresetId.Custom;
        public RenderQualityTier PreviewQuality { get; set; } = RenderQualityTier.Normal;
        public bool IndicatorAssemblyEnabled
        {
            get => _indicatorAssemblyEnabled;
            set => _indicatorAssemblyEnabled = value;
        }
        public float IndicatorBaseWidth
        {
            get => _indicatorBaseWidth;
            set => _indicatorBaseWidth = ClampIndicatorDimension(value);
        }
        public float IndicatorBaseHeight
        {
            get => _indicatorBaseHeight;
            set => _indicatorBaseHeight = ClampIndicatorDimension(value);
        }
        public float IndicatorBaseThickness
        {
            get => _indicatorBaseThickness;
            set => _indicatorBaseThickness = ClampIndicatorDimension(value);
        }
        public float IndicatorHousingRadius
        {
            get => _indicatorHousingRadius;
            set => _indicatorHousingRadius = ClampIndicatorDimension(value);
        }
        public float IndicatorHousingHeight
        {
            get => _indicatorHousingHeight;
            set => _indicatorHousingHeight = ClampIndicatorDimension(value);
        }
        public float IndicatorLensRadius
        {
            get => _indicatorLensRadius;
            set => _indicatorLensRadius = ClampIndicatorDimension(value);
        }
        public float IndicatorLensHeight
        {
            get => _indicatorLensHeight;
            set => _indicatorLensHeight = ClampIndicatorDimension(value);
        }
        public float IndicatorLensTransmission
        {
            get => _indicatorLensTransmission;
            set => _indicatorLensTransmission = Math.Clamp(value, 0f, 1f);
        }
        public float IndicatorLensIor
        {
            get => _indicatorLensIor;
            set => _indicatorLensIor = Math.Clamp(value, 1f, 2.5f);
        }
        public float IndicatorLensThickness
        {
            get => _indicatorLensThickness;
            set => _indicatorLensThickness = Math.Clamp(value, 0f, 10f);
        }
        public Vector3 IndicatorLensTint
        {
            get => _indicatorLensTint;
            set => _indicatorLensTint = new Vector3(
                Math.Clamp(value.X, 0f, 1f),
                Math.Clamp(value.Y, 0f, 1f),
                Math.Clamp(value.Z, 0f, 1f));
        }
        public float IndicatorLensAbsorption
        {
            get => _indicatorLensAbsorption;
            set => _indicatorLensAbsorption = Math.Clamp(value, 0f, 8f);
        }
        public float IndicatorLensSurfaceRoughness
        {
            get => _indicatorLensSurfaceRoughness;
            set => _indicatorLensSurfaceRoughness = Math.Clamp(value, 0.04f, 1f);
        }
        public float IndicatorLensSurfaceSpecularStrength
        {
            get => _indicatorLensSurfaceSpecularStrength;
            set => _indicatorLensSurfaceSpecularStrength = Math.Clamp(value, 0f, 2.5f);
        }
        public float IndicatorReflectorBaseRadius
        {
            get => _indicatorReflectorBaseRadius;
            set => _indicatorReflectorBaseRadius = ClampIndicatorDimension(value);
        }
        public float IndicatorReflectorTopRadius
        {
            get => _indicatorReflectorTopRadius;
            set => _indicatorReflectorTopRadius = ClampIndicatorDimension(value);
        }
        public float IndicatorReflectorDepth
        {
            get => _indicatorReflectorDepth;
            set => _indicatorReflectorDepth = ClampIndicatorDimension(value);
        }
        public float IndicatorEmitterRadius
        {
            get => _indicatorEmitterRadius;
            set => _indicatorEmitterRadius = ClampIndicatorDimension(value);
        }
        public float IndicatorEmitterSpread
        {
            get => _indicatorEmitterSpread;
            set => _indicatorEmitterSpread = ClampIndicatorDimension(value);
        }
        public float IndicatorEmitterDepth
        {
            get => _indicatorEmitterDepth;
            set => _indicatorEmitterDepth = ClampIndicatorOffset(value, -2048f, 2048f);
        }
        public int IndicatorEmitterCount
        {
            get => _indicatorEmitterCount;
            set => _indicatorEmitterCount = ClampIndicatorSegments(value, 3, 1, 8);
        }
        public int IndicatorRadialSegments
        {
            get => _indicatorRadialSegments;
            set => _indicatorRadialSegments = ClampIndicatorSegments(value, DefaultIndicatorRadialSegments, 8, 96);
        }
        public int IndicatorLensLatitudeSegments
        {
            get => _indicatorLensLatitudeSegments;
            set => _indicatorLensLatitudeSegments = ClampIndicatorSegments(value, DefaultIndicatorLensLatitudeSegments, 4, 64);
        }
        public int IndicatorLensLongitudeSegments
        {
            get => _indicatorLensLongitudeSegments;
            set => _indicatorLensLongitudeSegments = ClampIndicatorSegments(value, DefaultIndicatorLensLongitudeSegments, 6, 96);
        }
        public string SliderBackplateImportedMeshPath
        {
            get => _sliderBackplateImportedMeshPath;
            set => _sliderBackplateImportedMeshPath = NormalizeOptionalPath(value);
        }
        public string SliderThumbImportedMeshPath
        {
            get => _sliderThumbImportedMeshPath;
            set => _sliderThumbImportedMeshPath = NormalizeOptionalPath(value);
        }
        public ToggleAssemblyMode ToggleMode { get; set; } = ToggleAssemblyMode.Auto;
        public ToggleAssemblyStateCount ToggleStateCount
        {
            get => _toggleStateCount;
            set
            {
                _toggleStateCount = value == ToggleAssemblyStateCount.ThreePosition
                    ? ToggleAssemblyStateCount.ThreePosition
                    : ToggleAssemblyStateCount.TwoPosition;
                _toggleStateIndex = ClampToggleStateIndex(_toggleStateIndex, _toggleStateCount);
            }
        }
        public int ToggleStateIndex
        {
            get => _toggleStateIndex;
            set => _toggleStateIndex = ClampToggleStateIndex(value, _toggleStateCount);
        }
        [JsonIgnore]
        public float ToggleStateBlendPosition
        {
            get => _toggleStateBlendPosition;
            set
            {
                if (!float.IsFinite(value))
                {
                    _toggleStateBlendPosition = float.NaN;
                    return;
                }

                float max = _toggleStateCount == ToggleAssemblyStateCount.ThreePosition ? 2f : 1f;
                _toggleStateBlendPosition = Math.Clamp(value, 0f, max);
            }
        }
        public float ToggleMaxAngleDeg
        {
            get => _toggleMaxAngleDeg;
            set => _toggleMaxAngleDeg = Math.Clamp(value, 5f, 85f);
        }
        public float TogglePlateWidth
        {
            get => _togglePlateWidth;
            set => _togglePlateWidth = ClampSliderDimensionOverride(value);
        }
        public float TogglePlateHeight
        {
            get => _togglePlateHeight;
            set => _togglePlateHeight = ClampSliderDimensionOverride(value);
        }
        public float TogglePlateThickness
        {
            get => _togglePlateThickness;
            set => _togglePlateThickness = ClampSliderDimensionOverride(value);
        }
        public float TogglePlateOffsetY
        {
            get => _togglePlateOffsetY;
            set => _togglePlateOffsetY = ClampToggleOffset(value, -4096f, 4096f);
        }
        public float TogglePlateOffsetZ
        {
            get => _togglePlateOffsetZ;
            set => _togglePlateOffsetZ = ClampToggleOffset(value, -4096f, 4096f);
        }
        public float ToggleBushingRadius
        {
            get => _toggleBushingRadius;
            set => _toggleBushingRadius = ClampSliderDimensionOverride(value);
        }
        public float ToggleBushingHeight
        {
            get => _toggleBushingHeight;
            set => _toggleBushingHeight = ClampSliderDimensionOverride(value);
        }
        public int ToggleBushingSides
        {
            get => _toggleBushingSides;
            set => _toggleBushingSides = ClampToggleSegments(value, 6, 3, 32);
        }
        public ToggleBushingShape ToggleLowerBushingShape
        {
            get => _toggleLowerBushingShape;
            set => _toggleLowerBushingShape = value;
        }
        public ToggleBushingShape ToggleUpperBushingShape
        {
            get => _toggleUpperBushingShape;
            set => _toggleUpperBushingShape = value;
        }
        public float ToggleLowerBushingRadiusScale
        {
            get => _toggleLowerBushingRadiusScale;
            set => _toggleLowerBushingRadiusScale = ClampToggleScale(value, 1.22f, 0.25f, 4f);
        }
        public float ToggleLowerBushingHeightRatio
        {
            get => _toggleLowerBushingHeightRatio;
            set => _toggleLowerBushingHeightRatio = ClampToggleRatio(value, 0.45f, 0.05f, 2f);
        }
        public float ToggleUpperBushingRadiusScale
        {
            get => _toggleUpperBushingRadiusScale;
            set => _toggleUpperBushingRadiusScale = ClampToggleScale(value, 1.00f, 0.25f, 4f);
        }
        public float ToggleUpperBushingHeightRatio
        {
            get => _toggleUpperBushingHeightRatio;
            set => _toggleUpperBushingHeightRatio = ClampToggleRatio(value, 0.75f, 0.05f, 2f);
        }
        public float ToggleUpperBushingKnurlAmount
        {
            get => _toggleUpperBushingKnurlAmount;
            set => _toggleUpperBushingKnurlAmount = ClampToggleRatio(value, 0f, 0f, 1f);
        }
        public int ToggleUpperBushingKnurlDensity
        {
            get => _toggleUpperBushingKnurlDensity;
            set => _toggleUpperBushingKnurlDensity = ClampToggleSegments(value, 20, 3, 96);
        }
        public float ToggleUpperBushingKnurlDepth
        {
            get => _toggleUpperBushingKnurlDepth;
            set => _toggleUpperBushingKnurlDepth = ClampToggleRatio(value, 0.22f, 0f, 1f);
        }
        public float ToggleUpperBushingAnisotropyStrength
        {
            get => _toggleUpperBushingAnisotropyStrength;
            set => _toggleUpperBushingAnisotropyStrength = ClampToggleRatio(value, 0f, 0f, 1f);
        }
        public float ToggleUpperBushingAnisotropyDensity
        {
            get => _toggleUpperBushingAnisotropyDensity;
            set => _toggleUpperBushingAnisotropyDensity = ClampToggleScale(value, 48f, 3f, 128f);
        }
        public float ToggleUpperBushingAnisotropyAngleDegrees
        {
            get => _toggleUpperBushingAnisotropyAngleDegrees;
            set => _toggleUpperBushingAnisotropyAngleDegrees = ClampToggleOffset(value, -180f, 180f);
        }
        public float ToggleUpperBushingSurfaceCharacter
        {
            get => _toggleUpperBushingSurfaceCharacter;
            set => _toggleUpperBushingSurfaceCharacter = ClampToggleRatio(value, 0.55f, 0f, 1f);
        }
        public float TogglePivotHousingRadius
        {
            get => _togglePivotHousingRadius;
            set => _togglePivotHousingRadius = ClampSliderDimensionOverride(value);
        }
        public float TogglePivotHousingDepth
        {
            get => _togglePivotHousingDepth;
            set => _togglePivotHousingDepth = ClampSliderDimensionOverride(value);
        }
        public float TogglePivotHousingBevel
        {
            get => _togglePivotHousingBevel;
            set => _togglePivotHousingBevel = ClampSliderDimensionOverride(value);
        }
        public float TogglePivotBallRadius
        {
            get => _togglePivotBallRadius;
            set => _togglePivotBallRadius = ClampSliderDimensionOverride(value);
        }
        public float TogglePivotClearance
        {
            get => _togglePivotClearance;
            set => _togglePivotClearance = Math.Clamp(value, 0f, 128f);
        }
        public bool ToggleInvertBaseFrontFaceWinding
        {
            get => _toggleInvertBaseFrontFaceWinding;
            set => _toggleInvertBaseFrontFaceWinding = value;
        }
        public bool ToggleInvertLeverFrontFaceWinding
        {
            get => _toggleInvertLeverFrontFaceWinding;
            set => _toggleInvertLeverFrontFaceWinding = value;
        }
        public float ToggleLeverLength
        {
            get => _toggleLeverLength;
            set => _toggleLeverLength = ClampSliderDimensionOverride(value);
        }
        public float ToggleLeverRadius
        {
            get => _toggleLeverRadius;
            set => _toggleLeverRadius = ClampSliderDimensionOverride(value);
        }
        public float ToggleLeverTopRadius
        {
            get => _toggleLeverTopRadius;
            set => _toggleLeverTopRadius = ClampSliderDimensionOverride(value);
        }
        public int ToggleLeverSides
        {
            get => _toggleLeverSides;
            set => _toggleLeverSides = ClampToggleSegments(value, 20, 6, 64);
        }
        public float ToggleLeverPivotOffset
        {
            get => _toggleLeverPivotOffset;
            set => _toggleLeverPivotOffset = ClampToggleOffset(value, -4096f, 4096f);
        }
        public float ToggleTipRadius
        {
            get => _toggleTipRadius;
            set => _toggleTipRadius = ClampSliderDimensionOverride(value);
        }
        public int ToggleTipLatitudeSegments
        {
            get => _toggleTipLatitudeSegments;
            set => _toggleTipLatitudeSegments = ClampToggleSegments(value, 10, 4, 64);
        }
        public int ToggleTipLongitudeSegments
        {
            get => _toggleTipLongitudeSegments;
            set => _toggleTipLongitudeSegments = ClampToggleSegments(value, 16, 6, 128);
        }
        public bool ToggleTipSleeveEnabled
        {
            get => _toggleTipSleeveEnabled;
            set => _toggleTipSleeveEnabled = value;
        }
        public float ToggleTipSleeveLength
        {
            get => _toggleTipSleeveLength;
            set => _toggleTipSleeveLength = ClampSliderDimensionOverride(value);
        }
        public float ToggleTipSleeveThickness
        {
            get => _toggleTipSleeveThickness;
            set => _toggleTipSleeveThickness = ClampSliderDimensionOverride(value);
        }
        public float ToggleTipSleeveOuterRadius
        {
            get => _toggleTipSleeveOuterRadius;
            set => _toggleTipSleeveOuterRadius = ClampSliderDimensionOverride(value);
        }
        public float ToggleTipSleeveCoverage
        {
            get => _toggleTipSleeveCoverage;
            set => _toggleTipSleeveCoverage = ClampToggleRatio(value, 0.55f, 0f, 1f);
        }
        public int ToggleTipSleeveSides
        {
            get => _toggleTipSleeveSides;
            set => _toggleTipSleeveSides = ClampToggleSegments(value, 24, 6, 64);
        }
        public ToggleTipSleeveStyle ToggleTipSleeveStyle
        {
            get => _toggleTipSleeveStyle;
            set => _toggleTipSleeveStyle = value;
        }
        public ToggleTipSleeveTipStyle ToggleTipSleeveTipStyle
        {
            get => _toggleTipSleeveTipStyle;
            set => _toggleTipSleeveTipStyle = value;
        }
        public int ToggleTipSleevePatternCount
        {
            get => _toggleTipSleevePatternCount;
            set => _toggleTipSleevePatternCount = ClampToggleSegments(value, 14, 3, 64);
        }
        public float ToggleTipSleevePatternDepth
        {
            get => _toggleTipSleevePatternDepth;
            set => _toggleTipSleevePatternDepth = ClampToggleRatio(value, 0.22f, 0f, 0.9f);
        }
        public float ToggleTipSleeveTipAmount
        {
            get => _toggleTipSleeveTipAmount;
            set => _toggleTipSleeveTipAmount = ClampToggleRatio(value, 0.35f, 0f, 0.95f);
        }
        public Vector3 ToggleTipSleeveColor
        {
            get => _toggleTipSleeveColor;
            set => _toggleTipSleeveColor = ClampColor01(value);
        }
        public float ToggleTipSleeveMetallic
        {
            get => _toggleTipSleeveMetallic;
            set => _toggleTipSleeveMetallic = Math.Clamp(value, 0f, 1f);
        }
        public float ToggleTipSleeveRoughness
        {
            get => _toggleTipSleeveRoughness;
            set => _toggleTipSleeveRoughness = Math.Clamp(value, 0.04f, 1f);
        }
        public float ToggleTipSleevePearlescence
        {
            get => _toggleTipSleevePearlescence;
            set => _toggleTipSleevePearlescence = Math.Clamp(value, 0f, 1f);
        }
        public float ToggleTipSleeveDiffuseStrength
        {
            get => _toggleTipSleeveDiffuseStrength;
            set => _toggleTipSleeveDiffuseStrength = Math.Clamp(value, 0f, 4f);
        }
        public float ToggleTipSleeveSpecularStrength
        {
            get => _toggleTipSleeveSpecularStrength;
            set => _toggleTipSleeveSpecularStrength = Math.Clamp(value, 0f, 4f);
        }
        public float ToggleTipSleeveRustAmount
        {
            get => _toggleTipSleeveRustAmount;
            set => _toggleTipSleeveRustAmount = Math.Clamp(value, 0f, 1f);
        }
        public float ToggleTipSleeveWearAmount
        {
            get => _toggleTipSleeveWearAmount;
            set => _toggleTipSleeveWearAmount = Math.Clamp(value, 0f, 1f);
        }
        public float ToggleTipSleeveGunkAmount
        {
            get => _toggleTipSleeveGunkAmount;
            set => _toggleTipSleeveGunkAmount = Math.Clamp(value, 0f, 1f);
        }
        public string ToggleBaseImportedMeshPath
        {
            get => _toggleBaseImportedMeshPath;
            set => _toggleBaseImportedMeshPath = NormalizeOptionalPath(value);
        }
        public string ToggleLeverImportedMeshPath
        {
            get => _toggleLeverImportedMeshPath;
            set => _toggleLeverImportedMeshPath = NormalizeOptionalPath(value);
        }
        public SceneRootNode SceneRoot { get; } = new SceneRootNode();
        public InteractorProjectType ProjectType { get; set; } = InteractorProjectType.RotaryKnob;
        public DynamicLightRig DynamicLightRig { get; } = new();
        public SceneNode? SelectedNode { get; private set; }
        public List<KnobLight> Lights { get; } = new List<KnobLight>();
        public int SelectedLightIndex { get; private set; } = -1;
        public KnobLight? SelectedLight =>
            SelectedLightIndex >= 0 && SelectedLightIndex < Lights.Count
                ? Lights[SelectedLightIndex]
                : null;

        public KnobProject(string? path = null)
        {
            EnsureDefaultPaintLayer();

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                BaseTexture = SKBitmap.Decode(path);
            }
            else
            {
                // Fallback: A Red Circle so you know it's working
                BaseTexture = new SKBitmap(512, 512);
                using (var canvas = new SKCanvas(BaseTexture))
                {
                    canvas.Clear(SKColors.Transparent);
                    var paint = new SKPaint { Color = SKColors.DarkRed, IsAntialias = true };
                    canvas.DrawCircle(256, 256, 200, paint);
                }
            }

            var light1 = AddLight(757f, 761f, -180f);
            light1.Type = LightType.Point;
            light1.Intensity = 2.71f;
            light1.Falloff = 1.0f;
            light1.DiffuseBoost = 1.0f;
            light1.SpecularBoost = 1.0f;
            light1.SpecularPower = 64.0f;

            var light2 = AddLight(-536f, 486f, -233f);
            light2.Type = LightType.Point;
            light2.Intensity = 3.0f;
            light2.Falloff = 0.71f;
            light2.DiffuseBoost = 1.0f;
            light2.SpecularBoost = 1.0f;
            light2.SpecularPower = 64.0f;

            var light3 = AddLight(483f, -568f, -1303f);
            light3.Type = LightType.Point;
            light3.Intensity = 2.27f;
            light3.Falloff = 1.0f;
            light3.DiffuseBoost = 1.0f;
            light3.SpecularBoost = 1.0f;
            light3.SpecularPower = 64.0f;

            var light4 = AddLight(-710f, 791f, -715f);
            light4.Type = LightType.Point;
            light4.Intensity = 1.11f;
            light4.Falloff = 0.30f;
            light4.DiffuseBoost = 1.0f;
            light4.SpecularBoost = 1.0f;
            light4.SpecularPower = 64.0f;

            SetSelectedLightIndex(0);
            EnsureInteractorModulesForProjectType(ProjectType, pruneUnsupportedModules: false);
            SetSelectedNode(EnsureModelNode());
        }

        public void ApplyInteractorProjectTypeDefaults(InteractorProjectType projectType)
        {
            ProjectType = projectType;
            SliderThumbPositionNormalized = 0.5f;
            SliderThumbProfile = SliderThumbProfile.Box;
            SliderTrackStyle = SliderTrackStyle.None;
            SliderTrackWidth = 0f;
            SliderTrackDepth = 0f;
            SliderRailHeight = 0f;
            SliderRailSpacing = 0f;
            SliderThumbRidgeCount = 0;
            SliderThumbRidgeDepth = 0f;
            SliderThumbCornerRadius = 0f;
            PushButtonPressAmountNormalized = 0f;
            PushButtonCapProfile = PushButtonCapProfile.Flat;
            PushButtonBezelProfile = PushButtonBezelProfile.Straight;
            PushButtonSkirtStyle = PushButtonSkirtStyle.None;
            PushButtonBezelChamferSize = 0f;
            PushButtonCapOverhang = 0f;
            PushButtonCapSegments = 0;
            PushButtonBezelSegments = 0;
            PushButtonSkirtHeight = 0f;
            PushButtonSkirtRadius = 0f;
            PushButtonBaseImportedMeshPath = string.Empty;
            PushButtonCapImportedMeshPath = string.Empty;
            ToggleMaterialPreset = ToggleMaterialPresetId.Custom;
            SliderMaterialPreset = SliderMaterialPresetId.Custom;
            PushButtonMaterialPreset = PushButtonMaterialPresetId.Custom;
            ToggleStateBlendPosition = float.NaN;
            DynamicLightRig.Enabled = false;
            DynamicLightRig.AnimationMode = DynamicLightAnimationMode.Steady;

            switch (projectType)
            {
                case InteractorProjectType.ThumbSlider:
                    SliderMode = SliderAssemblyMode.Enabled;
                    ToggleMode = ToggleAssemblyMode.Disabled;
                    break;
                case InteractorProjectType.FlipSwitch:
                    SliderMode = SliderAssemblyMode.Disabled;
                    ToggleMode = ToggleAssemblyMode.Enabled;
                    ToggleLowerBushingShape = ToggleBushingShape.Hex;
                    ToggleUpperBushingShape = ToggleBushingShape.Hex;
                    ToggleUpperBushingKnurlAmount = 0f;
                    ToggleUpperBushingKnurlDensity = 20;
                    ToggleUpperBushingKnurlDepth = 0.22f;
                    ToggleUpperBushingAnisotropyStrength = 0.72f;
                    ToggleUpperBushingAnisotropyDensity = 82f;
                    ToggleUpperBushingAnisotropyAngleDegrees = 0f;
                    ToggleUpperBushingSurfaceCharacter = 0.58f;
                    TogglePivotHousingRadius = 0f;
                    TogglePivotHousingDepth = 0f;
                    TogglePivotHousingBevel = 0f;
                    TogglePivotBallRadius = 0f;
                    TogglePivotClearance = 1.2f;
                    ToggleInvertBaseFrontFaceWinding = true;
                    ToggleInvertLeverFrontFaceWinding = true;
                    ToggleTipSleeveEnabled = true;
                    ToggleTipSleeveStyle = ToggleTipSleeveStyle.Round;
                    ToggleTipSleeveTipStyle = ToggleTipSleeveTipStyle.Rounded;
                    break;
                case InteractorProjectType.PushButton:
                    SliderMode = SliderAssemblyMode.Disabled;
                    ToggleMode = ToggleAssemblyMode.Disabled;
                    break;
                case InteractorProjectType.IndicatorLight:
                    SliderMode = SliderAssemblyMode.Disabled;
                    ToggleMode = ToggleAssemblyMode.Disabled;
                    DynamicLightRig.Enabled = true;
                    EnsureIndicatorAssemblyDefaults(forceReset: true);
                    DynamicLightRig.EnsureIndicatorDefaults();
                    break;
                default:
                    SliderMode = SliderAssemblyMode.Disabled;
                    ToggleMode = ToggleAssemblyMode.Disabled;
                    break;
            }

            EnsureInteractorModulesForProjectType(projectType, pruneUnsupportedModules: true);
        }

        public void EnsureIndicatorAssemblyDefaults(bool forceReset = false)
        {
            ModelNode model = EnsureModelNode();
            float knobRadius = MathF.Max(40f, model.Radius);
            float knobHeight = MathF.Max(20f, model.Height);

            float baseWidth = knobRadius * 1.55f;
            float baseHeight = knobRadius * 1.55f;
            float baseThickness = MathF.Max(10f, knobHeight * 0.25f);
            float housingRadius = knobRadius * 0.56f;
            float housingHeight = MathF.Max(8f, knobHeight * 0.46f);
            float lensRadius = housingRadius * 0.78f;
            float lensHeight = MathF.Max(6f, housingHeight * 0.78f);
            IndicatorLensMaterialPresetDefinition clearLensPreset = IndicatorLensMaterialPresets.Clear;
            float reflectorBaseRadius = housingRadius * 0.70f;
            float reflectorTopRadius = reflectorBaseRadius * 0.36f;
            float reflectorDepth = MathF.Max(3f, housingHeight * 0.52f);
            float emitterRadius = MathF.Max(1.4f, lensRadius * 0.12f);
            float emitterSpread = lensRadius * 0.85f;
            float emitterDepth = -MathF.Max(0.8f, lensHeight * 0.38f);

            bool geometryMissing =
                _indicatorBaseWidth <= 0f ||
                _indicatorBaseHeight <= 0f ||
                _indicatorBaseThickness <= 0f ||
                _indicatorHousingRadius <= 0f ||
                _indicatorHousingHeight <= 0f ||
                _indicatorLensRadius <= 0f ||
                _indicatorLensHeight <= 0f ||
                _indicatorReflectorBaseRadius <= 0f ||
                _indicatorReflectorTopRadius <= 0f ||
                _indicatorReflectorDepth <= 0f ||
                _indicatorEmitterRadius <= 0f ||
                _indicatorEmitterSpread <= 0f;

            if (forceReset || geometryMissing)
            {
                IndicatorAssemblyEnabled = true;
                IndicatorBaseWidth = baseWidth;
                IndicatorBaseHeight = baseHeight;
                IndicatorBaseThickness = baseThickness;
                IndicatorHousingRadius = housingRadius;
                IndicatorHousingHeight = housingHeight;
                IndicatorLensRadius = lensRadius;
                IndicatorLensHeight = lensHeight;
                IndicatorLensTransmission = clearLensPreset.Transmission;
                IndicatorLensIor = clearLensPreset.Ior;
                IndicatorLensThickness = clearLensPreset.Thickness;
                IndicatorLensTint = clearLensPreset.Tint;
                IndicatorLensAbsorption = clearLensPreset.Absorption;
                IndicatorLensSurfaceRoughness = clearLensPreset.SurfaceRoughness;
                IndicatorLensSurfaceSpecularStrength = clearLensPreset.SurfaceSpecularStrength;
                IndicatorReflectorBaseRadius = reflectorBaseRadius;
                IndicatorReflectorTopRadius = reflectorTopRadius;
                IndicatorReflectorDepth = reflectorDepth;
                IndicatorEmitterRadius = emitterRadius;
                IndicatorEmitterSpread = emitterSpread;
                IndicatorEmitterDepth = emitterDepth;
            }

            if (forceReset || _indicatorEmitterCount <= 0)
            {
                IndicatorEmitterCount = 3;
            }

            bool usesLegacyIndicatorMeshResolution =
                _indicatorRadialSegments == LegacyIndicatorRadialSegments &&
                _indicatorLensLatitudeSegments == LegacyIndicatorLensLatitudeSegments &&
                _indicatorLensLongitudeSegments == LegacyIndicatorLensLongitudeSegments;

            if (forceReset || _indicatorRadialSegments <= 0 || usesLegacyIndicatorMeshResolution)
            {
                IndicatorRadialSegments = DefaultIndicatorRadialSegments;
            }

            if (forceReset || _indicatorLensLatitudeSegments <= 0 || usesLegacyIndicatorMeshResolution)
            {
                IndicatorLensLatitudeSegments = DefaultIndicatorLensLatitudeSegments;
            }

            if (forceReset || _indicatorLensLongitudeSegments <= 0 || usesLegacyIndicatorMeshResolution)
            {
                IndicatorLensLongitudeSegments = DefaultIndicatorLensLongitudeSegments;
            }

            if (forceReset || !float.IsFinite(_indicatorLensTransmission))
            {
                IndicatorLensTransmission = clearLensPreset.Transmission;
            }

            if (forceReset || !float.IsFinite(_indicatorLensIor))
            {
                IndicatorLensIor = clearLensPreset.Ior;
            }

            if (forceReset || _indicatorLensThickness < 0f || !float.IsFinite(_indicatorLensThickness))
            {
                IndicatorLensThickness = clearLensPreset.Thickness;
            }

            if (forceReset || !float.IsFinite(_indicatorLensTint.X) || !float.IsFinite(_indicatorLensTint.Y) || !float.IsFinite(_indicatorLensTint.Z))
            {
                IndicatorLensTint = clearLensPreset.Tint;
            }

            if (forceReset || _indicatorLensAbsorption < 0f || !float.IsFinite(_indicatorLensAbsorption))
            {
                IndicatorLensAbsorption = clearLensPreset.Absorption;
            }

            if (forceReset || !float.IsFinite(_indicatorLensSurfaceRoughness))
            {
                IndicatorLensSurfaceRoughness = clearLensPreset.SurfaceRoughness;
            }

            if (forceReset || !float.IsFinite(_indicatorLensSurfaceSpecularStrength))
            {
                IndicatorLensSurfaceSpecularStrength = clearLensPreset.SurfaceSpecularStrength;
            }
        }

        public ModelNode EnsureModelNode()
        {
            ModelNode? model = SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
            if (model != null)
            {
                return model;
            }

            model = new ModelNode("KnobModel");
            SceneRoot.AddChild(model);
            return model;
        }

        public MaterialNode EnsureMaterialNode()
        {
            ModelNode model = EnsureModelNode();
            MaterialNode? material = model.GetMaterialByIndex(0);
            if (material != null)
            {
                return material;
            }

            material = new MaterialNode("DefaultMaterial");
            model.AddChild(material);
            return material;
        }

        public IReadOnlyList<MaterialNode> GetMaterialNodes()
        {
            return EnsureModelNode().GetMaterialNodes();
        }

        public void SetMaterialNodes(IEnumerable<MaterialNode> materials)
        {
            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            ModelNode model = EnsureModelNode();
            MaterialNode[] existingMaterials = model.GetMaterialNodes();
            for (int i = 0; i < existingMaterials.Length; i++)
            {
                model.RemoveChild(existingMaterials[i]);
            }

            bool addedAny = false;
            foreach (MaterialNode material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                model.AddChild(material);
                addedAny = true;
            }

            if (!addedAny)
            {
                model.AddChild(new MaterialNode("DefaultMaterial"));
            }
        }

        public CollarNode EnsureCollarNode()
        {
            ModelNode model = EnsureModelNode();
            CollarNode? collar = model.Children.OfType<CollarNode>().FirstOrDefault();
            if (collar != null)
            {
                return collar;
            }

            collar = CreateDefaultCollarNode();
            model.AddChild(collar);
            return collar;
        }

        public bool RemoveCollarNode()
        {
            ModelNode? model = SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
            if (model == null)
            {
                return false;
            }

            CollarNode? collar = model.Children.OfType<CollarNode>().FirstOrDefault();
            if (collar == null)
            {
                return false;
            }

            bool removed = model.RemoveChild(collar);
            if (removed && SelectedNode != null && SelectedNode.Id == collar.Id)
            {
                SetSelectedNode(model);
            }

            return removed;
        }

        public void EnsureInteractorModulesForProjectType(InteractorProjectType projectType, bool pruneUnsupportedModules)
        {
            EnsureMaterialNode();

            if (projectType == InteractorProjectType.RotaryKnob)
            {
                EnsureCollarNode();
            }
            else if (pruneUnsupportedModules)
            {
                RemoveCollarNode();
            }
        }

        public void SetSelectedNode(SceneNode? node)
        {
            SelectedNode = node;
        }

        public KnobLight AddLight(float x = 0f, float y = 0f, float z = 0f)
        {
            var light = new KnobLight
            {
                Name = $"Light {Lights.Count + 1}",
                X = x,
                Y = y,
                Z = z,
                Color = Lights.Count == 0 ? SKColors.White : SKColors.Wheat,
                Intensity = 1.0f,
                Falloff = 1.0f,
                DiffuseBoost = 1.0f,
                SpecularBoost = 1.0f,
                SpecularPower = 64f
            };
            Lights.Add(light);
            SelectedLightIndex = Lights.Count - 1;
            SceneRoot.AddChild(new KnobForge.Core.Scene.LightNode(light));
            return light;
        }

        public bool RemoveSelectedLight()
        {
            if (Lights.Count <= 1 || SelectedLightIndex < 0 || SelectedLightIndex >= Lights.Count)
            {
                return false;
            }

            var removedLight = Lights[SelectedLightIndex];
            Lights.RemoveAt(SelectedLightIndex);
            var nodeToRemove = SceneRoot.Children
                .OfType<KnobForge.Core.Scene.LightNode>()
                .FirstOrDefault(n => n.Light == removedLight);

            if (nodeToRemove != null)
            {
                SceneRoot.RemoveChild(nodeToRemove);
            }
            if (SelectedLightIndex >= Lights.Count)
            {
                SelectedLightIndex = Lights.Count - 1;
            }

            return true;
        }

        public bool SetSelectedLightIndex(int index)
        {
            if (index < 0 || index >= Lights.Count)
            {
                return false;
            }

            SelectedLightIndex = index;
            return true;
        }

        public void EnsureSelection()
        {
            if (Lights.Count == 0)
            {
                SelectedLightIndex = -1;
                return;
            }

            if (SelectedLightIndex < 0 || SelectedLightIndex >= Lights.Count)
            {
                SelectedLightIndex = 0;
            }

            if (SelectedNode == null)
            {
                SetSelectedNode(EnsureModelNode());
            }
        }

        private static CollarNode CreateDefaultCollarNode()
        {
            (CollarPreset preset, string importedPath) = ResolveDefaultCollarPresetAndPath();
            bool hasImportedMesh = preset != CollarPreset.SnakeOuroboros;

            return new CollarNode("SnakeOuroborosCollar")
            {
                Enabled = true,
                Preset = hasImportedMesh ? preset : CollarPreset.SnakeOuroboros,
                ImportedMeshPath = importedPath,
                ImportedScale = 1.09f,
                ImportedBodyLengthScale = 1.00f,
                ImportedBodyThicknessScale = 0.79f,
                ImportedHeadLengthScale = 1.00f,
                ImportedHeadThicknessScale = 0.79f,
                ImportedRotationRadians = MathF.PI,
                ImportedOffsetXRatio = -0.13f,
                ImportedOffsetYRatio = 0.24f,
                ImportedInflateRatio = 0.00f,
                BaseColor = new Vector3(0.31f, 0.08f, 0.07f),
                Metallic = 1.00f,
                Roughness = 0.46f,
                Pearlescence = 0.00f
            };
        }

        private static (CollarPreset Preset, string ImportedPath) ResolveDefaultCollarPresetAndPath()
        {
            string meshyRingPath = CollarNode.ResolveImportedMeshPath(CollarPreset.MeshyOuroborosRing, null);
            if (File.Exists(meshyRingPath))
            {
                return (CollarPreset.MeshyOuroborosRing, meshyRingPath);
            }

            string meshyRingTexturedPath = CollarNode.ResolveImportedMeshPath(CollarPreset.MeshyOuroborosRingTextured, null);
            if (File.Exists(meshyRingTexturedPath))
            {
                return (CollarPreset.MeshyOuroborosRingTextured, meshyRingTexturedPath);
            }

            string legacyImportedStlPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Monozukuri",
                "ouroboros.stl");
            if (File.Exists(legacyImportedStlPath))
            {
                return (CollarPreset.ImportedStl, legacyImportedStlPath);
            }

            return (CollarPreset.SnakeOuroboros, string.Empty);
        }

        private static float ClampSliderDimensionOverride(float value)
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                return 0f;
            }

            return Math.Clamp(value, 1f, 4096f);
        }

        private static int ClampSliderGeometrySegments(int value, int min, int max)
        {
            if (value <= 0)
            {
                return 0;
            }

            return Math.Clamp(value, min, max);
        }

        private static float ClampPushButtonDimensionOverride(float value)
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                return 0f;
            }

            return Math.Clamp(value, 0.1f, 4096f);
        }

        private static int ClampPushButtonSegments(int value, int fallback, int min, int max)
        {
            if (value <= 0)
            {
                return 0;
            }

            return Math.Clamp(value, min, max);
        }

        private static float ClampIndicatorDimension(float value)
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                return 0f;
            }

            return Math.Clamp(value, 0.5f, 4096f);
        }

        private static float ClampIndicatorOffset(float value, float min, float max)
        {
            if (!float.IsFinite(value))
            {
                return 0f;
            }

            return Math.Clamp(value, min, max);
        }

        private static int ClampIndicatorSegments(int value, int fallback, int min, int max)
        {
            if (value <= 0)
            {
                return fallback;
            }

            return Math.Clamp(value, min, max);
        }

        private static int ClampToggleStateIndex(int value, ToggleAssemblyStateCount count)
        {
            int max = count == ToggleAssemblyStateCount.ThreePosition ? 2 : 1;
            return Math.Clamp(value, 0, max);
        }

        private static float ClampToggleOffset(float value, float min, float max)
        {
            if (!float.IsFinite(value))
            {
                return 0f;
            }

            return Math.Clamp(value, min, max);
        }

        private static int ClampToggleSegments(int value, int fallback, int min, int max)
        {
            if (value <= 0)
            {
                return fallback;
            }

            return Math.Clamp(value, min, max);
        }

        private static float ClampToggleRatio(float value, float fallback, float min, float max)
        {
            if (!float.IsFinite(value))
            {
                return fallback;
            }

            return Math.Clamp(value, min, max);
        }

        private static float ClampToggleScale(float value, float fallback, float min, float max)
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                return fallback;
            }

            return Math.Clamp(value, min, max);
        }

        private static Vector3 ClampColor01(Vector3 value)
        {
            return new Vector3(
                Math.Clamp(value.X, 0f, 1f),
                Math.Clamp(value.Y, 0f, 1f),
                Math.Clamp(value.Z, 0f, 1f));
        }

        private static float ClampFinite(float value, float fallback, float min, float max)
        {
            if (!float.IsFinite(value))
            {
                return fallback;
            }

            return Math.Clamp(value, min, max);
        }

        private static string NormalizeOptionalPath(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        public byte[] GetPaintMaskRgba8()
        {
            return _paintMaskRgba8;
        }

        public byte[] GetPaintColorRgba8()
        {
            return _paintColorRgba8;
        }

        public byte[] GetPaintMask2Rgba8()
        {
            return _paintMask2Rgba8;
        }

        public void SetPaintMaskResolution(int size)
        {
            if (size != 512 && size != 1024 && size != 2048 && size != 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Paint mask size must be 512, 1024, 2048, or 4096.");
            }

            if (PaintMaskSize == size &&
                _paintMaskRgba8.Length == size * size * 4 &&
                _paintColorRgba8.Length == size * size * 4 &&
                _paintMask2Rgba8.Length == size * size * 4)
            {
                return;
            }

            PaintMaskSize = size;
            _paintMaskRgba8 = new byte[size * size * 4];
            _paintColorRgba8 = new byte[size * size * 4];
            _paintMask2Rgba8 = new byte[size * size * 4];
            for (int i = 0; i < _paintLayers.Count; i++)
            {
                _paintLayers[i].PixelData = null;
                _paintLayers[i].ColorPixelData = null;
                _paintLayers[i].PixelData2 = null;
            }

            _paintMaskVersion++;
            _paintColorVersion++;
            _paintMask2Version++;
        }

        public void ClearPaintMask()
        {
            EnsureDefaultPaintLayer();
            for (int i = 0; i < _paintLayers.Count; i++)
            {
                _paintLayers[i].PixelData = null;
                _paintLayers[i].ColorPixelData = null;
                _paintLayers[i].PixelData2 = null;
            }

            Array.Clear(_paintMaskRgba8, 0, _paintMaskRgba8.Length);
            Array.Clear(_paintColorRgba8, 0, _paintColorRgba8.Length);
            Array.Clear(_paintMask2Rgba8, 0, _paintMask2Rgba8.Length);
            _paintMaskVersion++;
            _paintColorVersion++;
            _paintMask2Version++;
        }

        public void BeginPaintRecomposeBatch()
        {
            _paintRecomposeSuspendCount++;
        }

        public void EndPaintRecomposeBatch()
        {
            if (_paintRecomposeSuspendCount <= 0)
            {
                _paintRecomposeSuspendCount = 0;
                return;
            }

            _paintRecomposeSuspendCount--;
            if (_paintRecomposeSuspendCount == 0 && _paintRecomposeDeferred)
            {
                bool incrementVersions = _paintRecomposeDeferredIncrementVersions;
                _paintRecomposeDeferred = false;
                _paintRecomposeDeferredIncrementVersions = false;
                RecomposePaintBuffersCore(incrementVersions);
            }
        }

        public void EnsurePaintLayerCount(int count)
        {
            int clampedCount = Math.Max(1, count);
            EnsureDefaultPaintLayer();
            bool structureChanged = false;
            while (_paintLayers.Count < clampedCount)
            {
                _paintLayers.Add(new PaintLayer
                {
                    Name = $"Layer {_paintLayers.Count + 1}"
                });
                structureChanged = true;
            }

            while (_paintLayers.Count > clampedCount)
            {
                _paintLayers.RemoveAt(_paintLayers.Count - 1);
                structureChanged = true;
            }

            if (structureChanged)
            {
                RecomposePaintBuffers(incrementVersions: true);
            }
        }

        public void SetPaintLayerProperties(int index, string name, float opacity, PaintBlendMode blendMode, bool visible)
        {
            EnsureDefaultPaintLayer();
            if ((uint)index >= (uint)_paintLayers.Count)
            {
                return;
            }

            PaintLayer layer = _paintLayers[index];
            layer.Name = string.IsNullOrWhiteSpace(name) ? $"Layer {index + 1}" : name.Trim();
            layer.Opacity = Math.Clamp(opacity, 0f, 1f);
            layer.BlendMode = blendMode;
            layer.Visible = visible;
            RecomposePaintBuffers(incrementVersions: true);
        }

        public Vector4 SamplePaintMaskBilinear(float u, float v)
        {
            float uc = Math.Clamp(u, 0f, 1f);
            float vc = Math.Clamp(v, 0f, 1f);
            int size = PaintMaskSize;
            float x = uc * (size - 1);
            float y = vc * (size - 1);
            int x0 = (int)MathF.Floor(x);
            int y0 = (int)MathF.Floor(y);
            int x1 = Math.Min(x0 + 1, size - 1);
            int y1 = Math.Min(y0 + 1, size - 1);
            float tx = x - x0;
            float ty = y - y0;

            Vector4 n00 = ReadMaskRgba(x0, y0);
            Vector4 n10 = ReadMaskRgba(x1, y0);
            Vector4 n01 = ReadMaskRgba(x0, y1);
            Vector4 n11 = ReadMaskRgba(x1, y1);
            Vector4 nx0 = Vector4.Lerp(n00, n10, tx);
            Vector4 nx1 = Vector4.Lerp(n01, n11, tx);
            return Vector4.Lerp(nx0, nx1, ty);
        }

        public Vector4 SamplePaintMask2Bilinear(float u, float v)
        {
            float uc = Math.Clamp(u, 0f, 1f);
            float vc = Math.Clamp(v, 0f, 1f);
            int size = PaintMaskSize;
            float x = uc * (size - 1);
            float y = vc * (size - 1);
            int x0 = (int)MathF.Floor(x);
            int y0 = (int)MathF.Floor(y);
            int x1 = Math.Min(x0 + 1, size - 1);
            int y1 = Math.Min(y0 + 1, size - 1);
            float tx = x - x0;
            float ty = y - y0;

            Vector4 n00 = ReadMask2Rgba(x0, y0);
            Vector4 n10 = ReadMask2Rgba(x1, y0);
            Vector4 n01 = ReadMask2Rgba(x0, y1);
            Vector4 n11 = ReadMask2Rgba(x1, y1);
            Vector4 nx0 = Vector4.Lerp(n00, n10, tx);
            Vector4 nx1 = Vector4.Lerp(n01, n11, tx);
            return Vector4.Lerp(nx0, nx1, ty);
        }

        public bool StampPaintMaskUv(
            Vector2 uvCenter,
            float uvRadius,
            PaintBrushType brushType,
            ScratchAbrasionType scratchAbrasionType,
            PaintChannel channel,
            float opacity,
            float spread,
            uint seed)
        {
            return StampPaintMaskUv(
                0,
                uvCenter,
                uvRadius,
                brushType,
                scratchAbrasionType,
                channel,
                opacity,
                spread,
                seed,
                PaintColor,
                ResolveBrushTargetValue(channel));
        }

        public bool StampPaintMaskUv(
            int layerIndex,
            Vector2 uvCenter,
            float uvRadius,
            PaintBrushType brushType,
            ScratchAbrasionType scratchAbrasionType,
            PaintChannel channel,
            float opacity,
            float spread,
            uint seed,
            Vector3 paintColor)
        {
            return StampPaintMaskUv(
                layerIndex,
                uvCenter,
                uvRadius,
                brushType,
                scratchAbrasionType,
                channel,
                opacity,
                spread,
                seed,
                paintColor,
                ResolveBrushTargetValue(channel));
        }

        public bool StampPaintMaskUv(
            int layerIndex,
            Vector2 uvCenter,
            float uvRadius,
            PaintBrushType brushType,
            ScratchAbrasionType scratchAbrasionType,
            PaintChannel channel,
            float opacity,
            float spread,
            uint seed,
            Vector3 paintColor,
            float targetValue)
        {
            if (uvRadius <= 1e-6f || opacity <= 1e-6f)
            {
                return false;
            }

            EnsureDefaultPaintLayer();
            int clampedLayerIndex = Math.Clamp(layerIndex, 0, _paintLayers.Count - 1);
            PaintLayer layer = _paintLayers[clampedLayerIndex];
            byte[] layerMask = EnsureLayerPixelBuffer(layer, colorBuffer: false);
            byte[]? layerMask2 = channel switch
            {
                PaintChannel.Roughness or PaintChannel.Metallic => EnsureLayerPixelBuffer2(layer),
                PaintChannel.Erase => layer.PixelData2,
                _ => null
            };
            byte[]? layerColor = channel is PaintChannel.Color or PaintChannel.Erase
                ? EnsureLayerPixelBuffer(layer, colorBuffer: true)
                : null;
            Vector3 clampedPaintColor = ClampColor01(paintColor);
            float clampedTargetValue = Math.Clamp(targetValue, 0f, 1f);
            int size = PaintMaskSize;
            float radiusPx = MathF.Max(0.5f, uvRadius * size);
            int xMin = Math.Clamp((int)MathF.Floor((uvCenter.X * size) - radiusPx - 1f), 0, size - 1);
            int xMax = Math.Clamp((int)MathF.Ceiling((uvCenter.X * size) + radiusPx + 1f), 0, size - 1);
            int yMin = Math.Clamp((int)MathF.Floor((uvCenter.Y * size) - radiusPx - 1f), 0, size - 1);
            int yMax = Math.Clamp((int)MathF.Ceiling((uvCenter.Y * size) + radiusPx + 1f), 0, size - 1);
            float invRadius = 1f / MathF.Max(1e-6f, uvRadius);

            bool changed = false;
            float spreadClamped = Math.Clamp(spread, 0f, 1f);
            float opacityClamped = Math.Clamp(opacity, 0f, 1f);

            for (int y = yMin; y <= yMax; y++)
            {
                float py = ((y + 0.5f) / size - uvCenter.Y) * invRadius;
                for (int x = xMin; x <= xMax; x++)
                {
                    float px = ((x + 0.5f) / size - uvCenter.X) * invRadius;
                    float weight = channel == PaintChannel.Scratch
                        ? ComputeScratchWeight(scratchAbrasionType, px, py, spreadClamped, seed, x, y)
                        : ComputeBrushWeight(brushType, px, py, spreadClamped, seed, x, y);
                    if (weight <= 1e-6f)
                    {
                        continue;
                    }

                    float alpha = Math.Clamp(opacityClamped * weight, 0f, 1f);
                    if (channel == PaintChannel.Scratch)
                    {
                        alpha = Math.Clamp(alpha * 1.70f, 0f, 1f);
                    }
                    if (alpha <= 1e-6f)
                    {
                        continue;
                    }

                    int idx = ((y * size) + x) * 4;
                    if (channel == PaintChannel.Erase)
                    {
                        changed |= LerpByte(ref layerMask[idx + 0], 0, alpha);
                        changed |= LerpByte(ref layerMask[idx + 1], 0, alpha);
                        changed |= LerpByte(ref layerMask[idx + 2], 0, alpha);
                        changed |= LerpByte(ref layerMask[idx + 3], 0, alpha);
                        if (layerColor != null)
                        {
                            changed |= LerpByte(ref layerColor[idx + 0], 0, alpha);
                            changed |= LerpByte(ref layerColor[idx + 1], 0, alpha);
                            changed |= LerpByte(ref layerColor[idx + 2], 0, alpha);
                            changed |= LerpByte(ref layerColor[idx + 3], 0, alpha);
                        }
                        if (layerMask2 != null)
                        {
                            changed |= LerpByte(ref layerMask2[idx + 0], 0, alpha);
                            changed |= LerpByte(ref layerMask2[idx + 1], 0, alpha);
                            changed |= LerpByte(ref layerMask2[idx + 2], 0, alpha);
                            changed |= LerpByte(ref layerMask2[idx + 3], 0, alpha);
                        }
                    }
                    else
                    {
                        if (channel == PaintChannel.Color)
                        {
                            if (layerColor == null)
                            {
                                continue;
                            }

                            changed |= LerpByte(ref layerColor[idx + 0], ToByte(clampedPaintColor.X), alpha);
                            changed |= LerpByte(ref layerColor[idx + 1], ToByte(clampedPaintColor.Y), alpha);
                            changed |= LerpByte(ref layerColor[idx + 2], ToByte(clampedPaintColor.Z), alpha);
                            changed |= LerpByte(ref layerColor[idx + 3], 255, alpha);
                            continue;
                        }

                        if (channel == PaintChannel.Roughness && layerMask2 != null)
                        {
                            changed |= LerpByte(ref layerMask2[idx + 0], ToByte(clampedTargetValue), alpha);
                            changed |= LerpByte(ref layerMask2[idx + 2], 255, alpha);
                            continue;
                        }

                        if (channel == PaintChannel.Metallic && layerMask2 != null)
                        {
                            changed |= LerpByte(ref layerMask2[idx + 1], ToByte(clampedTargetValue), alpha);
                            changed |= LerpByte(ref layerMask2[idx + 3], 255, alpha);
                            continue;
                        }

                        int channelIndex = channel switch
                        {
                            PaintChannel.Rust => 0,
                            PaintChannel.Wear => 1,
                            PaintChannel.Gunk => 2,
                            PaintChannel.Scratch => 3,
                            _ => -1
                        };
                        if (channelIndex >= 0)
                        {
                            changed |= LerpByte(ref layerMask[idx + channelIndex], 255, alpha);
                        }
                    }
                }
            }

            if (changed)
            {
                RecomposePaintBuffers(incrementVersions: true);
            }

            return changed;
        }

        private Vector4 ReadMaskRgba(int x, int y)
        {
            int idx = ((y * PaintMaskSize) + x) * 4;
            return new Vector4(
                _paintMaskRgba8[idx + 0] / 255f,
                _paintMaskRgba8[idx + 1] / 255f,
                _paintMaskRgba8[idx + 2] / 255f,
                _paintMaskRgba8[idx + 3] / 255f);
        }

        private Vector4 ReadMask2Rgba(int x, int y)
        {
            int idx = ((y * PaintMaskSize) + x) * 4;
            return new Vector4(
                _paintMask2Rgba8[idx + 0] / 255f,
                _paintMask2Rgba8[idx + 1] / 255f,
                _paintMask2Rgba8[idx + 2] / 255f,
                _paintMask2Rgba8[idx + 3] / 255f);
        }

        private void EnsureDefaultPaintLayer()
        {
            if (_paintLayers.Count > 0)
            {
                return;
            }

            _paintLayers.Add(new PaintLayer
            {
                Name = "Layer 1"
            });
        }

        private byte[] EnsureLayerPixelBuffer(PaintLayer layer, bool colorBuffer)
        {
            int length = PaintMaskSize * PaintMaskSize * 4;
            if (colorBuffer)
            {
                layer.ColorPixelData ??= new byte[length];
                if (layer.ColorPixelData.Length != length)
                {
                    layer.ColorPixelData = new byte[length];
                }

                return layer.ColorPixelData;
            }

            layer.PixelData ??= new byte[length];
            if (layer.PixelData.Length != length)
            {
                layer.PixelData = new byte[length];
            }

            return layer.PixelData;
        }

        private byte[] EnsureLayerPixelBuffer2(PaintLayer layer)
        {
            int length = PaintMaskSize * PaintMaskSize * 4;
            layer.PixelData2 ??= new byte[length];
            if (layer.PixelData2.Length != length)
            {
                layer.PixelData2 = new byte[length];
            }

            return layer.PixelData2;
        }

        private void RecomposePaintBuffers(bool incrementVersions)
        {
            if (_paintRecomposeSuspendCount > 0)
            {
                _paintRecomposeDeferred = true;
                _paintRecomposeDeferredIncrementVersions |= incrementVersions;
                return;
            }

            RecomposePaintBuffersCore(incrementVersions);
        }

        private void RecomposePaintBuffersCore(bool incrementVersions)
        {
            EnsureDefaultPaintLayer();
            int expectedLength = PaintMaskSize * PaintMaskSize * 4;
            if (_paintMaskRgba8.Length != expectedLength)
            {
                _paintMaskRgba8 = new byte[expectedLength];
            }

            if (_paintColorRgba8.Length != expectedLength)
            {
                _paintColorRgba8 = new byte[expectedLength];
            }

            if (_paintMask2Rgba8.Length != expectedLength)
            {
                _paintMask2Rgba8 = new byte[expectedLength];
            }

            PaintLayerCompositor.Composite(_paintLayers, _paintMaskRgba8, PaintMaskSize);
            PaintLayerCompositor.CompositeColor(_paintLayers, _paintColorRgba8, PaintMaskSize, -1, 1f);
            PaintLayerCompositor.CompositeMask2(_paintLayers, _paintMask2Rgba8, PaintMaskSize, -1, 1f);

            if (incrementVersions)
            {
                _paintMaskVersion++;
                _paintColorVersion++;
                _paintMask2Version++;
            }
        }

        private float ResolveBrushTargetValue(PaintChannel channel)
        {
            return channel switch
            {
                PaintChannel.Roughness => RoughnessPaintTarget,
                PaintChannel.Metallic => MetallicPaintTarget,
                _ => 1f
            };
        }

        private static bool LerpByte(ref byte target, byte toValue, float alpha)
        {
            byte before = target;
            float blended = before + ((toValue - before) * alpha);
            byte after = (byte)Math.Clamp((int)MathF.Round(blended), 0, 255);
            target = after;
            return after != before;
        }

        private static byte ToByte(float value)
        {
            return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
        }

        private static float ComputeBrushWeight(
            PaintBrushType brushType,
            float xNorm,
            float yNorm,
            float spread,
            uint seed,
            int px,
            int py)
        {
            float ax = MathF.Abs(xNorm);
            float ay = MathF.Abs(yNorm);
            float dist = MathF.Sqrt((xNorm * xNorm) + (yNorm * yNorm));

            return brushType switch
            {
                PaintBrushType.Square => ComputeSquareWeight(ax, ay),
                PaintBrushType.Spray => ComputeSprayWeight(dist, spread, seed, px, py),
                PaintBrushType.Splat => ComputeSplatWeight(xNorm, yNorm, dist, spread, seed, px, py),
                PaintBrushType.Stroke => ComputeStrokeWeight(dist),
                _ => ComputeCircleWeight(dist)
            };
        }

        private static float ComputeScratchWeight(
            ScratchAbrasionType abrasionType,
            float xNorm,
            float yNorm,
            float spread,
            uint seed,
            int px,
            int py)
        {
            float dist = MathF.Sqrt((xNorm * xNorm) + (yNorm * yNorm));
            return abrasionType switch
            {
                ScratchAbrasionType.Chisel => ComputeScratchChiselWeight(dist),
                ScratchAbrasionType.Burr => ComputeScratchBurrWeight(xNorm, yNorm, dist, spread, seed, px, py),
                ScratchAbrasionType.Scuff => ComputeScratchScuffWeight(dist, spread, seed, px, py),
                _ => ComputeScratchNeedleWeight(dist)
            };
        }

        private static float ComputeCircleWeight(float dist)
        {
            if (dist >= 1f)
            {
                return 0f;
            }

            return 1f - SmoothStep(0.86f, 1f, dist);
        }

        private static float ComputeScratchNeedleWeight(float dist)
        {
            if (dist >= 1f)
            {
                return 0f;
            }

            float core = 1f - dist;
            return MathF.Pow(core, 1.35f);
        }

        private static float ComputeScratchChiselWeight(float dist)
        {
            if (dist >= 1f)
            {
                return 0f;
            }

            float plateau = 1f - SmoothStep(0.58f, 1f, dist);
            return MathF.Pow(Math.Clamp(plateau, 0f, 1f), 0.72f);
        }

        private static float ComputeScratchBurrWeight(
            float xNorm,
            float yNorm,
            float dist,
            float spread,
            uint seed,
            int px,
            int py)
        {
            if (dist >= 1.22f)
            {
                return 0f;
            }

            float radialNoise = Hash01((uint)(px * 5 + 17), (uint)(py * 9 + 29), seed ^ 0xA17F9D3Bu);
            float angularNoise = Hash01((uint)(px * 13 + 43), (uint)(py * 7 + 61), seed ^ 0xD1B54A32u);
            float boundary = 0.78f + (radialNoise * (0.24f + (0.34f * spread)));
            float warpedDist = dist / MathF.Max(0.28f, boundary);
            if (warpedDist >= 1f)
            {
                return 0f;
            }

            float core = 1f - warpedDist;
            float tooth = 0.68f + (0.32f * MathF.Sin((xNorm * 10.7f) + (yNorm * 9.3f) + (angularNoise * 6.28318f)));
            float micro = 0.35f + (0.65f * angularNoise);
            return Math.Clamp(core * tooth * micro, 0f, 1f);
        }

        private static float ComputeScratchScuffWeight(float dist, float spread, uint seed, int px, int py)
        {
            if (dist >= 1f)
            {
                return 0f;
            }

            float grain = Hash01((uint)(px * 3 + 5), (uint)(py * 7 + 11), seed ^ 0x9E3779B9u);
            float keepThreshold = 0.98f + ((0.42f - 0.98f) * spread);
            if (grain > keepThreshold)
            {
                return 0f;
            }

            float soft = 1f - SmoothStep(0.32f, 1f, dist);
            return soft * (0.55f + (0.45f * grain));
        }

        private static float ComputeStrokeWeight(float dist)
        {
            if (dist >= 1f)
            {
                return 0f;
            }

            return MathF.Pow(1f - dist, 0.55f);
        }

        private static float ComputeSquareWeight(float ax, float ay)
        {
            float edge = MathF.Max(ax, ay);
            if (edge >= 1f)
            {
                return 0f;
            }

            return 1f - SmoothStep(0.86f, 1f, edge);
        }

        private static float ComputeSprayWeight(float dist, float spread, uint seed, int px, int py)
        {
            if (dist >= 1f)
            {
                return 0f;
            }

            float noise = Hash01((uint)px, (uint)py, seed);
            float keepThreshold = 0.90f + ((0.20f - 0.90f) * spread);
            if (noise > keepThreshold)
            {
                return 0f;
            }

            return (1f - dist) * (0.45f + (noise * 0.55f));
        }

        private static float ComputeSplatWeight(float xNorm, float yNorm, float dist, float spread, uint seed, int px, int py)
        {
            if (dist >= 1.35f)
            {
                return 0f;
            }

            float radialNoise = Hash01((uint)(px * 7 + 31), (uint)(py * 11 + 19), seed ^ 0xA5A5A5A5u);
            float angularNoise = Hash01((uint)(px * 13 + 23), (uint)(py * 17 + 47), seed ^ 0x3C6EF372u);
            float splatBoundary = 1f + ((radialNoise - 0.5f) * (0.25f + (0.55f * spread)));
            float angularWarp = 1f + ((angularNoise - 0.5f) * (0.10f + (0.35f * spread)));
            float warpedDist = dist / MathF.Max(0.3f, splatBoundary * angularWarp);
            if (warpedDist >= 1f)
            {
                return 0f;
            }

            float core = 1f - warpedDist;
            float lobes = 0.82f + (0.18f * MathF.Sin((xNorm * 5.1f) + (yNorm * 4.3f)));
            return Math.Clamp(core * lobes, 0f, 1f);
        }

        private static float Hash01(uint x, uint y, uint seed)
        {
            unchecked
            {
                uint h = x * 374761393u;
                h += y * 668265263u;
                h ^= seed + 0x9E3779B9u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            if (edge1 <= edge0)
            {
                return x < edge0 ? 0f : 1f;
            }

            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - (2f * t));
        }
    }
}
