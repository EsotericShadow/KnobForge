# Phase 12: Model, Material & Performance — Non-Rotary Project Types

## Your Role

You are implementing Phase 12 of the Monozukuri Material Tool Transformation. This phase expands the geometry generation, material preset system, and rendering performance for the four non-rotary project types: **PushButton**, **ThumbSlider**, **FlipSwitch (Toggle)**, and **IndicatorLight**. The RotaryKnob type is already mature and is NOT touched by this phase.

Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification. Do not refactor unrelated code.

## Project Context

Monozukuri (formerly KnobForge) is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs, switches, sliders, buttons, and indicator lights for audio plugin UIs. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–11 are complete. The rendering pipeline uses Metal via `MetalRendererContext`, with per-assembly mesh builders generating `MetalVertex[]` + `uint[]` arrays that are uploaded to GPU buffers. Shape-key caching in `MetalViewport.MeshAndUniforms.cs` avoids redundant mesh rebuilds. The export pipeline in `KnobExporter.cs` renders frame-by-frame into a spritesheet, iterating over frames with per-frame state mutations (rotation, slider position, toggle state, press depth, etc.).

**Current maturity by type:**
- **RotaryKnob**: Most mature. Complex procedural geometry (spiral grips, indicator grooves, body taper/bulge/crown profiles). Multi-material per-part system (top/bevel/side). Collar system with imported mesh support. NOT touched by Phase 12.
- **FlipSwitch (Toggle)**: Feature-rich geometry (66 config params). Bushing shapes (Hex/Octagon/Round/Square), knurling, pivot housing, lever with tapered cylinder, tip sleeve with 6 styles. Has imported mesh support for base+lever. **Gaps**: No material presets; lever is always a tapered cylinder (no profile options).
- **ThumbSlider**: Basic geometry (13 config params). Backplate and thumb are cuboids via `BuildPrimitiveCuboid()`. Has imported mesh support. Has mesh library catalog. **Gaps**: No track/channel geometry; no thumb cap profiles; no material presets.
- **PushButton**: Minimal geometry (9 config params). Plate (box) + bezel (cylinder) + cap (cylinder). **Gaps**: No cap profile options; no bezel chamfer; no skirt/ring geometry; no imported mesh support; no material presets; no mesh library catalog.
- **IndicatorLight**: Best-designed non-rotary type (18 config params, 6 mesh parts). Has lens material presets (Clear/Frosted/SaturatedLed), emitter spread system, reflector frustum, aura sphere. **Gaps**: Minor — housing is two-part frustum only; no imported mesh support.

## What Phase 12 Does

### Four Subphases (execute in order):

1. **12A — PushButton Geometry Expansion**: Transform PushButton from a 3-primitive stub into a parameterized procedural assembly with cap profiles, bezel chamfer, optional skirt/ring, and imported mesh support.

2. **12B — ThumbSlider Geometry Expansion**: Add track/channel groove geometry to the backplate, thumb cap profiles, and side rail options.

3. **12C — Assembly Material Preset System**: Create a unified material preset framework for all 4 non-rotary types, following the `IndicatorLensMaterialPresets` pattern but expanded to coordinate multi-part material sets.

4. **12D — Rendering Performance Optimizations**: Adaptive segment counts for preview vs. export, geometry cache to disk, and submesh merge for same-material draw calls.

**Explicitly deferred** (do NOT implement):
- Changes to RotaryKnob (already mature).
- Metal indirect command buffer / instanced filmstrip rendering (future phase).
- Color picker widgets for the UI (separate UI phase).
- Toggle lever profile variants (minor addition, future phase).
- Indicator Light imported mesh support (minor addition, future phase).

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT modify `App.axaml` or `App.axaml.cs`.** Design tokens and startup flow stay identical.
2. **Do NOT modify RotaryKnob rendering or geometry.** Only non-rotary types.
3. **All existing `MetalVertex` struct layout must be preserved.** Position, Normal, Tangent (Vector4), Texcoord (Vector2).
4. **All existing public API signatures in mesh builders must be preserved.** Add new overloads or parameters with defaults — do not break existing callers.
5. **Shape key structs must be updated** whenever new geometry parameters are added, so the caching system correctly detects changes.
6. **The app must compile and run identically after each subphase.** New features must default to values that produce the same visual output as before (backward compatibility).
7. **All new enums must be in `KnobForge.Core`** (same namespace as `InteractorProjectType`, `ToggleBushingShape`, etc.).
8. **All new project properties must be in `KnobProject.cs`** with sensible defaults that match current behavior.

---

## Existing Architecture (Read Before Coding)

### Mesh Builder Pattern

Each assembly type follows the same pattern:

```
KnobForge.Rendering/GPU/{Type}AssemblyMeshBuilder.cs
├── readonly record struct {Type}AssemblyConfig(...)     // All geometry parameters
├── sealed class {Type}PartMesh                          // Vertices + Indices + ReferenceRadius
├── static ResolveConfig(KnobProject?) → Config          // Extract project settings
├── static Build{Part}Mesh(in Config) → PartMesh         // Generate geometry per part
└── private helpers: AddBox, AddCylinder, AddFace, etc.  // Shared geometry primitives
```

### GPU Resource Pipeline (MetalViewport.MeshAndUniforms.cs)

```
RefreshMeshResources(project, modelNode)
  ├── For each assembly type:
  │   ├── ResolveConfig(project)
  │   ├── Build{Type}AssemblyShapeKey(config)
  │   ├── If shape changed or resources null:
  │   │   ├── Build{Part}Mesh(config) for each part
  │   │   └── CreateGpuResources(vertices, indices, referenceRadius)
  │   └── ReplaceMeshResources(ref field, newResources)
  └── Each type has dedicated _*Resources fields + _*ShapeKey fields
```

### Export Pipeline (KnobExporter.cs)

```
ExportInternal(settings, ...)
  ├── frameCount = settings.FrameCount (32-156 typical)
  ├── renderResolution = resolution × supersampleScale (256-1024 typical)
  ├── For each viewVariant:
  │   ├── For each frame 0..frameCount-1:
  │   │   ├── Apply frame state (rotation, slider pos, toggle state, press depth)
  │   │   ├── _gpuFrameProvider(renderResolution, renderResolution, camera, ...)
  │   │   ├── Downsample to final resolution
  │   │   ├── Optionally write individual frame PNG
  │   │   └── Blit to spritesheet canvas
  │   └── Encode + write spritesheet
  └── Restore original state
```

### Project Properties (KnobProject.cs)

Current properties by type:

**PushButton** (1 property):
- `PushButtonPressAmountNormalized` (float, 0-1)

**ThumbSlider** (9 properties):
- `SliderBackplateWidth/Height/Thickness` (float)
- `SliderThumbWidth/Height/Depth` (float)
- `SliderThumbPositionNormalized` (float, 0-1)
- `SliderBackplateImportedMeshPath`, `SliderThumbImportedMeshPath` (string)

**Toggle** (40+ properties — already extensive, see `ToggleAssemblyConfig` record):
- Full bushing/lever/tip/sleeve parameterization
- `ToggleMode`, `ToggleStateCount`, `ToggleStateIndex`, `ToggleStateBlendPosition`
- `ToggleBaseImportedMeshPath`, `ToggleLeverImportedMeshPath`

**IndicatorLight** (30+ properties):
- Full base/housing/lens/reflector/emitter parameterization
- Lens material presets (Transmission, IOR, Thickness, Tint, Absorption, SurfaceRoughness, SpecularStrength)
- `IndicatorRadialSegments`, `IndicatorLensLatitudeSegments`, `IndicatorLensLongitudeSegments`

### Material Draw Pipeline (MetalViewport.MaterialDraw.cs)

Each submesh resolves a `MaterialNode` via `ModelNode.GetMaterialByIndex(submeshIndex)`. Material properties applied per draw call:
- BaseColor (Vector3), Metallic, Roughness
- DiffuseStrength, SpecularStrength
- Optional per-part overrides (TopBaseColor, BevelBaseColor, SideBaseColor + separate metallic/roughness)
- Texture maps: Albedo (slot 4), Normal (5), Roughness (6), Metallic (7)
- Surface: RadialBrushStrength/Density, SurfaceCharacter
- Weathering: RustAmount, WearAmount, GunkAmount, Pearlescence

### Existing Preset Reference: IndicatorLensMaterialPresets

```csharp
// KnobForge.Core/IndicatorLensMaterialPresets.cs
public enum IndicatorLensMaterialPresetId { Clear = 0, Frosted = 1, SaturatedLed = 2 }

public readonly record struct IndicatorLensMaterialPresetDefinition(
    float Transmission, float Ior, float Thickness,
    Vector3 Tint, float Absorption,
    float SurfaceRoughness, float SurfaceSpecularStrength);

public static class IndicatorLensMaterialPresets
{
    public static readonly IndicatorLensMaterialPresetDefinition Clear = new(...);
    public static readonly IndicatorLensMaterialPresetDefinition Frosted = new(...);
    public static readonly IndicatorLensMaterialPresetDefinition SaturatedLed = new(...);
    public static IndicatorLensMaterialPresetDefinition Resolve(IndicatorLensMaterialPresetId preset) => ...;
    public static bool IsWithinSupportedRange(IndicatorLensMaterialPresetDefinition preset) => ...;
}
```

---

## Subphase 12A: PushButton Geometry Expansion

### Goal

Transform PushButton from a 3-primitive stub (plate box + bezel cylinder + cap cylinder) into a parameterized procedural assembly on par with the Toggle's complexity.

### Step 1: Add New Enums to KnobForge.Core

Add to the existing enum section in `KnobProject.cs` (near `ToggleBushingShape`, `ToggleTipSleeveStyle`):

```csharp
public enum PushButtonCapProfile
{
    Flat = 0,       // Current behavior — flat-top cylinder
    Domed = 1,      // Hemisphere dome (like a classic arcade button)
    Concave = 2,    // Inward-curved dish (like a vintage Neve button)
    Stepped = 3,    // Two-tier cylinder with a smaller raised center plateau
    Mushroom = 4    // Rounded overhanging cap wider than bezel (like emergency stops)
}

public enum PushButtonBezelProfile
{
    Straight = 0,   // Current behavior — straight cylinder wall
    Chamfered = 1,  // 45° angled bevel at top/bottom edges
    Filleted = 2,   // Rounded smooth transition at edges
    Flared = 3      // Outward taper from base to top (trumpet shape)
}

public enum PushButtonSkirtStyle
{
    None = 0,       // Current behavior — no skirt
    Ring = 1,       // Thin ring at base of bezel (decorative)
    Collar = 2,     // Taller collar around bezel base
    Flange = 3      // Wide flat flange extending from bezel base
}
```

### Step 2: Add New Properties to KnobProject.cs

Add after the existing `PushButtonPressAmountNormalized` property:

```csharp
// Push button geometry
public PushButtonCapProfile PushButtonCapProfile { get; set; } = PushButtonCapProfile.Flat;
public PushButtonBezelProfile PushButtonBezelProfile { get; set; } = PushButtonBezelProfile.Straight;
public PushButtonSkirtStyle PushButtonSkirtStyle { get; set; } = PushButtonSkirtStyle.None;
public float PushButtonBezelChamferSize { get; set; } // 0 = auto
public float PushButtonCapOverhang { get; set; }      // 0 = flush with bezel; only used by Mushroom profile
public int PushButtonCapSegments { get; set; }         // 0 = auto (28 for Flat/Stepped, 24 lat for Domed/Concave/Mushroom)
public int PushButtonBezelSegments { get; set; }       // 0 = auto (28)
public float PushButtonSkirtHeight { get; set; }       // 0 = auto
public float PushButtonSkirtRadius { get; set; }       // 0 = auto (bezelRadius + 2)
public string PushButtonBaseImportedMeshPath { get; set; } = string.Empty;
public string PushButtonCapImportedMeshPath { get; set; } = string.Empty;
```

### Step 3: Expand PushButtonAssemblyConfig

Update the record struct in `PushButtonAssemblyMeshBuilder.cs`:

```csharp
public readonly record struct PushButtonAssemblyConfig(
    bool Enabled,
    float PlateWidth,
    float PlateHeight,
    float PlateThickness,
    float BezelRadius,
    float BezelHeight,
    float CapRadius,
    float CapHeight,
    float PressDepth,
    // New fields (Phase 12A)
    PushButtonCapProfile CapProfile,
    PushButtonBezelProfile BezelProfile,
    PushButtonSkirtStyle SkirtStyle,
    float BezelChamferSize,
    float CapOverhang,
    int CapSegments,
    int BezelSegments,
    float SkirtHeight,
    float SkirtRadius,
    string BaseImportedMeshPath,
    long BaseImportedMeshTicks,
    string CapImportedMeshPath,
    long CapImportedMeshTicks);
```

### Step 4: Update ResolveConfig

Add resolution of new fields from project properties. Default values must produce identical geometry to the current code:

```csharp
PushButtonCapProfile capProfile = project.PushButtonCapProfile;
PushButtonBezelProfile bezelProfile = project.PushButtonBezelProfile;
PushButtonSkirtStyle skirtStyle = project.PushButtonSkirtStyle;
float chamferSize = project.PushButtonBezelChamferSize > 0f
    ? project.PushButtonBezelChamferSize
    : bezelHeight * 0.12f;
float capOverhang = capProfile == PushButtonCapProfile.Mushroom
    ? (project.PushButtonCapOverhang > 0f ? project.PushButtonCapOverhang : bezelRadius * 0.15f)
    : 0f;
int capSegments = project.PushButtonCapSegments > 0 ? project.PushButtonCapSegments : 28;
int bezelSegments = project.PushButtonBezelSegments > 0 ? project.PushButtonBezelSegments : 28;
float skirtHeight = project.PushButtonSkirtHeight > 0f
    ? project.PushButtonSkirtHeight
    : bezelHeight * 0.18f;
float skirtRadius = project.PushButtonSkirtRadius > 0f
    ? project.PushButtonSkirtRadius
    : bezelRadius + 2f;
```

### Step 5: Implement Cap Profile Geometry

Modify `BuildCapMesh` to branch on `config.CapProfile`:

- **Flat**: Current behavior (cylinder with flat top disc). No change needed.
- **Domed**: Cylinder body (shorter) + hemisphere on top. Use `AddSphere()` (copy from IndicatorAssemblyMeshBuilder) but only emit the top hemisphere (latitude 0 to π/2). The dome radius = `config.CapRadius`. Dome center at top of shortened cylinder body.
- **Concave**: Cylinder body + inverted partial sphere inset into the top. Generate the sphere with inverted normals (negate the normal vectors). Concave depth = `capRadius * 0.15f`.
- **Stepped**: Two stacked cylinders. Lower: full `CapRadius`, height = `CapHeight * 0.6f`. Upper: `CapRadius * 0.7f`, height = `CapHeight * 0.4f`. Both affected by press depth.
- **Mushroom**: Like Domed but with `CapRadius + CapOverhang` for the dome, creating an overhang wider than the bezel.

### Step 6: Implement Bezel Profile Geometry

Modify `BuildBaseMesh` bezel generation to branch on `config.BezelProfile`:

- **Straight**: Current behavior (single `AddCylinder` call). No change.
- **Chamfered**: Replace single cylinder with 3-section frustum stack: bottom chamfer (wider→nominal), main body (nominal), top chamfer (nominal→narrower). Use `AddCylinderFrustum()` (copy from IndicatorAssemblyMeshBuilder).
- **Filleted**: Like chamfered but with more intermediate steps (5-section stack) for a smooth rounded transition. Use 3-4 frustum sections at top and bottom with gradual radius changes.
- **Flared**: Single frustum from `bezelRadius * 0.88f` at bottom to `bezelRadius` at top.

### Step 7: Implement Skirt Geometry

Add a `BuildSkirtMesh` method that returns a new `PushButtonPartMesh` for the skirt ring:

- **None**: Return empty mesh.
- **Ring**: Thin torus-like ring around bezel base. Approximate with a short cylinder frustum (outer cylinder + inner cylinder creating a tube cross-section). Radius = `config.SkirtRadius`, height = `config.SkirtHeight * 0.3f`.
- **Collar**: Taller cylinder around bezel base. Radius = `config.SkirtRadius`, height = `config.SkirtHeight`.
- **Flange**: Wide, flat disc extending from bezel base. Radius = `config.SkirtRadius * 1.3f`, height = `config.SkirtHeight * 0.2f`.

### Step 8: Add Skirt to GPU Resource Pipeline

In `MetalViewport.MeshAndUniforms.cs`:

1. Add field: `private MetalMeshGpuResources? _pushButtonSkirtResources;`
2. In `ClearMeshResources()`: Add `ReplaceMeshResources(ref _pushButtonSkirtResources, null);`
3. In the PushButton section of `RefreshMeshResources()`: Build and upload skirt mesh alongside base and cap.
4. Update `PushButtonAssemblyShapeKey` to include all new config fields.

In `MetalViewport.MaterialDraw.cs` (or wherever PushButton parts are drawn):
1. Draw the skirt mesh with its own material (submesh index 2 — after base=0 and cap=1).

### Step 9: Add Imported Mesh Support

Follow the exact pattern from `SliderAssemblyMeshBuilder.TryBuildImportedPart()`:

1. Copy the `TryBuildImportedPart` method into `PushButtonAssemblyMeshBuilder` (or extract to a shared static helper).
2. In `BuildBaseMesh`: Check `config.BaseImportedMeshPath` first; if valid, use imported mesh instead of procedural geometry.
3. In `BuildCapMesh`: Check `config.CapImportedMeshPath` first; if valid, use imported mesh.
4. Add mesh library resolution methods following `SliderAssemblyMeshBuilder`'s pattern (directory candidates: `"models/button_models"`, `"button_models"`; subdirs: `"base_models"`, `"cap_models"`).

### Step 10: Add PushButton Catalog

Create `KnobForge.App/Views/MainWindow.PushButtonAssemblyCatalog.cs` following the exact pattern of `MainWindow.SliderAssemblyCatalog.cs`:

- Library root: `Desktop/Monozukuri/button_models` (with fallback candidates)
- Subdirectories: `base_models/`, `cap_models/`
- Supported formats: `.glb`, `.stl`
- Populate combo boxes for base and cap mesh selection in the inspector

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Run the app, create a PushButton project:
- Default appearance must be **identical** to before (Flat cap, Straight bezel, no skirt).
- Changing CapProfile in inspector should update the viewport.
- Changing BezelProfile should update the viewport.
- Changing SkirtStyle should show/hide skirt ring geometry.
- All combinations must render without crashes.
- Export a filmstrip — output should be correct.

---

## Subphase 12B: ThumbSlider Geometry Expansion

### Goal

Add track/channel groove, thumb cap profiles, and side rail geometry to the ThumbSlider, which currently generates flat cuboids.

### Step 1: Add New Enums

```csharp
public enum SliderThumbProfile
{
    Box = 0,        // Current behavior — rectangular cuboid
    Rounded = 1,    // Box with rounded top edge (filleted)
    Ridged = 2,     // Box with horizontal ridges/grooves for grip
    Pointer = 3,    // Triangular pointer/wedge shape (center line indicator)
    BarHandle = 4   // Tall narrow bar with wider grip area at top
}

public enum SliderTrackStyle
{
    None = 0,       // Current behavior — flat backplate surface
    Channel = 1,    // Recessed rectangular channel running vertically
    VGroove = 2,    // V-shaped groove cut into backplate surface
    Rail = 3        // Raised rail pair flanking the thumb path
}
```

### Step 2: Add New Properties to KnobProject.cs

```csharp
// Slider geometry expansion
public SliderThumbProfile SliderThumbProfile { get; set; } = SliderThumbProfile.Box;
public SliderTrackStyle SliderTrackStyle { get; set; } = SliderTrackStyle.None;
public float SliderTrackWidth { get; set; }        // 0 = auto (thumbWidth * 0.35)
public float SliderTrackDepth { get; set; }        // 0 = auto (backplateThickness * 0.15)
public float SliderRailHeight { get; set; }        // 0 = auto (thumbHeight * 0.08) — only for Rail style
public float SliderRailSpacing { get; set; }       // 0 = auto (thumbWidth * 1.1) — only for Rail style
public int SliderThumbRidgeCount { get; set; }     // 0 = auto (5) — only for Ridged profile
public float SliderThumbRidgeDepth { get; set; }   // 0 = auto (thumbDepth * 0.06)
public float SliderThumbCornerRadius { get; set; } // 0 = auto (min(thumbWidth, thumbHeight) * 0.12) — for Rounded
```

### Step 3: Expand SliderAssemblyConfig

```csharp
public readonly record struct SliderAssemblyConfig(
    bool Enabled,
    float BackplateWidth,
    float BackplateHeight,
    float BackplateThickness,
    float ThumbWidth,
    float ThumbHeight,
    float ThumbDepth,
    float ThumbPositionNormalized,
    string BackplateImportedMeshPath,
    long BackplateImportedMeshTicks,
    string ThumbImportedMeshPath,
    long ThumbImportedMeshTicks,
    // New fields (Phase 12B)
    SliderThumbProfile ThumbProfile,
    SliderTrackStyle TrackStyle,
    float TrackWidth,
    float TrackDepth,
    float RailHeight,
    float RailSpacing,
    int ThumbRidgeCount,
    float ThumbRidgeDepth,
    float ThumbCornerRadius);
```

### Step 4: Implement Track Geometry

Modify `BuildBackplateMesh` to add track geometry on top of (or cut into) the backplate when `config.TrackStyle != None`:

- **Channel**: Add a recessed box (negative Z offset) running the full height of the backplate, centered on X. Width = `config.TrackWidth`. Depth = `config.TrackDepth` below backplate surface. Generate the channel as additional geometry (floor + two side walls + two end caps).
- **VGroove**: Similar to channel but with triangulated V cross-section. Two angled faces meeting at center bottom instead of a flat floor.
- **Rail**: Two narrow raised boxes (positive Z from backplate surface) running full height, spaced at ±`config.RailSpacing / 2` from center X. Height = `config.RailHeight`. Width = `config.TrackWidth * 0.25`.

Track geometry uses the **same** `AddBox` / `AddFace` primitives already in the file.

### Step 5: Implement Thumb Profile Geometry

Modify `BuildThumbMesh` to branch on `config.ThumbProfile` instead of always calling `BuildPrimitiveCuboid`:

- **Box**: Current behavior. No change.
- **Rounded**: Cuboid body with the top 4 edges replaced by quarter-cylinder fillets. Generate fillet geometry using short cylinder arcs (4-8 segments per fillet). Fillet radius = `config.ThumbCornerRadius`.
- **Ridged**: Cuboid body with horizontal groove cuts on the front face. Generate `config.ThumbRidgeCount` evenly-spaced rectangular recesses on the Z+ face. Each recess: full width of thumb, depth = `config.ThumbRidgeDepth`, height = `thumbHeight / (ridgeCount * 2 + 1)`.
- **Pointer**: Triangular prism running along the X axis. Front face converges to a center line. Bottom and back faces are flat rectangles. The pointer tip extends `thumbDepth * 0.3` beyond the box depth.
- **BarHandle**: Narrower lower shaft (`thumbWidth * 0.6`) with wider top grip section (`thumbWidth`). Two stacked cuboids with a short transition frustum between them.

### Step 6: Update Shape Key and GPU Resources

Update `SliderAssemblyShapeKey` to include all new config fields so caching detects changes.

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Run the app, create a ThumbSlider project:
- Default appearance must be **identical** to before (Box thumb, no track).
- Changing ThumbProfile should update thumb geometry in viewport.
- Changing TrackStyle should add visible track geometry to backplate.
- Thumb position slider should still work correctly with all profiles.
- Rail style should show two raised rails flanking the thumb path.
- Export a filmstrip — all frames should render correctly with thumb at varying positions.

---

## Subphase 12C: Assembly Material Preset System

### Goal

Create a unified material preset system for all 4 non-rotary types, allowing one-click application of coordinated multi-part material sets. Follow the `IndicatorLensMaterialPresets` pattern but expanded to support multiple submeshes per preset.

### Step 1: Define the Shared Material Preset Infrastructure

Create `KnobForge.Core/AssemblyMaterialPresets.cs`:

```csharp
using System.Numerics;

namespace KnobForge.Core;

/// <summary>
/// Material values for a single submesh/part within an assembly.
/// </summary>
public readonly record struct AssemblyPartMaterial(
    Vector3 BaseColor,
    float Metallic,
    float Roughness,
    float DiffuseStrength,
    float SpecularStrength);

/// <summary>
/// A coordinated set of materials for all parts in an assembly.
/// Key = submesh index (0 = base/plate, 1 = primary moving part, 2+ = accessories).
/// </summary>
public readonly record struct AssemblyMaterialPresetDefinition(
    string Name,
    string Description,
    AssemblyPartMaterial[] PartMaterials);
```

### Step 2: Create Toggle Material Presets

Create `KnobForge.Core/ToggleMaterialPresets.cs`:

```csharp
public enum ToggleMaterialPresetId
{
    Custom = -1,          // User-defined (no preset applied)
    StudioChrome = 0,     // Polished chrome base + chrome lever + chrome tip
    VintageBakelite = 1,  // Dark brown Bakelite base + cream lever + ivory tip
    MilSpec = 2,          // Olive drab matte base + black lever + red safety tip
    BrushedBrass = 3      // Brushed brass base + brass lever + black rubber tip
}

public static class ToggleMaterialPresets
{
    // 3 parts: Base (0), Lever (1), Sleeve/Tip (2)
    public static readonly AssemblyMaterialPresetDefinition StudioChrome = new(
        "Studio Chrome",
        "Classic chrome toggle switch for studio hardware.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.85f, 0.85f, 0.88f), 1.0f, 0.15f, 0.9f, 1.8f),  // Base: polished chrome
            new AssemblyPartMaterial(new Vector3(0.82f, 0.82f, 0.85f), 1.0f, 0.12f, 0.9f, 1.9f),  // Lever: slightly brighter chrome
            new AssemblyPartMaterial(new Vector3(0.80f, 0.80f, 0.84f), 1.0f, 0.10f, 0.9f, 2.0f)   // Tip: polished chrome
        });

    public static readonly AssemblyMaterialPresetDefinition VintageBakelite = new(
        "Vintage Bakelite",
        "Warm Bakelite toggle for vintage-style gear.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.18f, 0.10f, 0.06f), 0.0f, 0.65f, 0.85f, 0.6f), // Base: dark brown Bakelite
            new AssemblyPartMaterial(new Vector3(0.90f, 0.85f, 0.72f), 0.0f, 0.55f, 0.90f, 0.5f), // Lever: cream
            new AssemblyPartMaterial(new Vector3(0.95f, 0.92f, 0.82f), 0.0f, 0.50f, 0.92f, 0.5f)  // Tip: ivory
        });

    public static readonly AssemblyMaterialPresetDefinition MilSpec = new(
        "Mil-Spec",
        "Military-specification toggle switch with safety markings.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.28f, 0.30f, 0.22f), 0.1f, 0.75f, 0.80f, 0.4f), // Base: olive drab
            new AssemblyPartMaterial(new Vector3(0.08f, 0.08f, 0.08f), 0.05f, 0.80f, 0.85f, 0.3f), // Lever: matte black
            new AssemblyPartMaterial(new Vector3(0.82f, 0.12f, 0.08f), 0.0f, 0.60f, 0.90f, 0.6f)  // Tip: safety red
        });

    public static readonly AssemblyMaterialPresetDefinition BrushedBrass = new(
        "Brushed Brass",
        "Warm brushed brass toggle for boutique hardware.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.78f, 0.62f, 0.30f), 0.95f, 0.35f, 0.85f, 1.4f), // Base: brushed brass
            new AssemblyPartMaterial(new Vector3(0.80f, 0.65f, 0.32f), 0.95f, 0.30f, 0.85f, 1.5f), // Lever: brass
            new AssemblyPartMaterial(new Vector3(0.06f, 0.06f, 0.06f), 0.0f, 0.85f, 0.90f, 0.3f)   // Tip: black rubber
        });

    public static AssemblyMaterialPresetDefinition Resolve(ToggleMaterialPresetId id) => id switch
    {
        ToggleMaterialPresetId.VintageBakelite => VintageBakelite,
        ToggleMaterialPresetId.MilSpec => MilSpec,
        ToggleMaterialPresetId.BrushedBrass => BrushedBrass,
        _ => StudioChrome
    };
}
```

### Step 3: Create Slider Material Presets

Create `KnobForge.Core/SliderMaterialPresets.cs`:

```csharp
public enum SliderMaterialPresetId
{
    Custom = -1,
    ConsoleStrip = 0,    // Dark gray track + white pointer thumb
    ModularSynth = 1,    // Black anodized track + colored pointer
    VintageConsole = 2,  // Cream/beige track + chrome thumb
    StudioFader = 3      // Brushed aluminum track + soft-touch black cap
}

public static class SliderMaterialPresets
{
    // 2 parts: Backplate (0), Thumb (1)
    public static readonly AssemblyMaterialPresetDefinition ConsoleStrip = new(
        "Console Strip",
        "Classic console channel strip fader look.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.12f, 0.12f, 0.14f), 0.1f, 0.70f, 0.85f, 0.4f),  // Backplate: dark gray
            new AssemblyPartMaterial(new Vector3(0.92f, 0.92f, 0.92f), 0.0f, 0.45f, 0.95f, 0.6f)   // Thumb: white polymer
        });

    public static readonly AssemblyMaterialPresetDefinition ModularSynth = new(
        "Modular Synth",
        "Eurorack-style anodized slider.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.02f, 0.02f, 0.02f), 0.9f, 0.25f, 0.80f, 1.6f),  // Backplate: black anodized
            new AssemblyPartMaterial(new Vector3(0.85f, 0.25f, 0.15f), 0.0f, 0.55f, 0.90f, 0.5f)   // Thumb: colored plastic
        });

    public static readonly AssemblyMaterialPresetDefinition VintageConsole = new(
        "Vintage Console",
        "Warm vintage mixing console fader.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.88f, 0.84f, 0.72f), 0.0f, 0.60f, 0.90f, 0.5f),  // Backplate: cream
            new AssemblyPartMaterial(new Vector3(0.80f, 0.80f, 0.82f), 0.95f, 0.18f, 0.85f, 1.7f)  // Thumb: chrome
        });

    public static readonly AssemblyMaterialPresetDefinition StudioFader = new(
        "Studio Fader",
        "Modern studio fader with brushed aluminum.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.72f, 0.72f, 0.75f), 0.95f, 0.40f, 0.85f, 1.2f), // Backplate: brushed aluminum
            new AssemblyPartMaterial(new Vector3(0.05f, 0.05f, 0.05f), 0.0f, 0.82f, 0.90f, 0.3f)   // Thumb: soft-touch black
        });

    public static AssemblyMaterialPresetDefinition Resolve(SliderMaterialPresetId id) => id switch
    {
        SliderMaterialPresetId.ModularSynth => ModularSynth,
        SliderMaterialPresetId.VintageConsole => VintageConsole,
        SliderMaterialPresetId.StudioFader => StudioFader,
        _ => ConsoleStrip
    };
}
```

### Step 4: Create PushButton Material Presets

Create `KnobForge.Core/PushButtonMaterialPresets.cs`:

```csharp
public enum PushButtonMaterialPresetId
{
    Custom = -1,
    NeveGray = 0,        // Classic matte gray button
    MoogBlack = 1,       // Soft-touch black rubber
    ArcadeGlow = 2,      // Translucent colored plastic (backlit look)
    BrushedMetal = 3     // Brushed stainless button on dark bezel
}

public static class PushButtonMaterialPresets
{
    // 3 parts: Base/Bezel (0), Cap (1), Skirt (2)
    public static readonly AssemblyMaterialPresetDefinition NeveGray = new(
        "Neve Gray",
        "Classic recording console button in warm gray.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.25f, 0.25f, 0.28f), 0.15f, 0.65f, 0.85f, 0.5f), // Bezel: dark gray metal
            new AssemblyPartMaterial(new Vector3(0.55f, 0.55f, 0.58f), 0.0f, 0.60f, 0.90f, 0.4f),  // Cap: warm gray plastic
            new AssemblyPartMaterial(new Vector3(0.20f, 0.20f, 0.22f), 0.15f, 0.70f, 0.85f, 0.4f)  // Skirt: dark gray metal
        });

    public static readonly AssemblyMaterialPresetDefinition MoogBlack = new(
        "Moog Black",
        "Soft-touch rubber button inspired by classic synths.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.04f, 0.04f, 0.04f), 0.0f, 0.88f, 0.90f, 0.2f),  // Bezel: matte black
            new AssemblyPartMaterial(new Vector3(0.06f, 0.06f, 0.06f), 0.0f, 0.92f, 0.92f, 0.15f), // Cap: soft rubber
            new AssemblyPartMaterial(new Vector3(0.03f, 0.03f, 0.03f), 0.0f, 0.90f, 0.90f, 0.2f)   // Skirt: black
        });

    public static readonly AssemblyMaterialPresetDefinition ArcadeGlow = new(
        "Arcade Glow",
        "Translucent colored button with backlit appearance.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.08f, 0.08f, 0.10f), 0.2f, 0.55f, 0.85f, 0.8f),  // Bezel: dark housing
            new AssemblyPartMaterial(new Vector3(0.30f, 0.72f, 0.90f), 0.0f, 0.35f, 0.95f, 1.2f),  // Cap: translucent blue
            new AssemblyPartMaterial(new Vector3(0.06f, 0.06f, 0.08f), 0.2f, 0.60f, 0.85f, 0.6f)   // Skirt: dark housing
        });

    public static readonly AssemblyMaterialPresetDefinition BrushedMetal = new(
        "Brushed Metal",
        "Industrial brushed stainless steel button.",
        new[]
        {
            new AssemblyPartMaterial(new Vector3(0.15f, 0.15f, 0.18f), 0.85f, 0.50f, 0.82f, 1.0f), // Bezel: dark steel
            new AssemblyPartMaterial(new Vector3(0.70f, 0.70f, 0.73f), 0.95f, 0.38f, 0.85f, 1.4f), // Cap: brushed stainless
            new AssemblyPartMaterial(new Vector3(0.15f, 0.15f, 0.18f), 0.85f, 0.55f, 0.82f, 0.9f)  // Skirt: dark steel
        });

    public static AssemblyMaterialPresetDefinition Resolve(PushButtonMaterialPresetId id) => id switch
    {
        PushButtonMaterialPresetId.MoogBlack => MoogBlack,
        PushButtonMaterialPresetId.ArcadeGlow => ArcadeGlow,
        PushButtonMaterialPresetId.BrushedMetal => BrushedMetal,
        _ => NeveGray
    };
}
```

### Step 5: Add Preset Properties to KnobProject.cs

```csharp
// Assembly material preset selection
public ToggleMaterialPresetId ToggleMaterialPreset { get; set; } = ToggleMaterialPresetId.Custom;
public SliderMaterialPresetId SliderMaterialPreset { get; set; } = SliderMaterialPresetId.Custom;
public PushButtonMaterialPresetId PushButtonMaterialPreset { get; set; } = PushButtonMaterialPresetId.Custom;
```

**Note**: `Custom = -1` means "no preset — use per-material manual values." This preserves backward compatibility for existing projects that have manually configured materials.

### Step 6: Wire Preset Application into Material Resolution

In `MetalViewport.MaterialDraw.cs` (or wherever `DrawMeshWithMaterials` resolves material properties), add a check:

When drawing assembly parts for a non-rotary type, if the project has a non-Custom preset selected, override the `MaterialNode` values with the preset's `PartMaterials[submeshIndex]` values. The preset takes precedence over manual material settings when active.

```csharp
// Pseudocode for the material resolution path:
if (projectType == InteractorProjectType.FlipSwitch && project.ToggleMaterialPreset != ToggleMaterialPresetId.Custom)
{
    var preset = ToggleMaterialPresets.Resolve(project.ToggleMaterialPreset);
    if (submeshIndex < preset.PartMaterials.Length)
    {
        var pm = preset.PartMaterials[submeshIndex];
        // Override: baseColor = pm.BaseColor, metallic = pm.Metallic, etc.
    }
}
// Similar for Slider, PushButton
```

### Step 7: Add Inspector UI for Preset Selection

In `MainWindow.axaml`, add a ComboBox for each type's material preset at the top of the material section of each inspector. The ComboBox should list all preset names plus "Custom" (which is the default and means manual control). When a preset is selected, the manual material controls below can be dimmed (reduced opacity) to indicate they're overridden.

**Important**: Keep all existing x:Name values. Add the preset ComboBox as a new control above the existing material controls. Wire the SelectionChanged handler in the corresponding `*Handlers.cs` file.

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Run the app with each non-rotary type:
- Default preset should be "Custom" (no visual change from current behavior).
- Selecting a named preset should immediately update material appearance in viewport.
- Switching back to "Custom" should restore the manual material values.
- The IndicatorLight's existing lens presets should continue to work independently.
- Export a filmstrip with a preset active — materials should render correctly.

---

## Subphase 12D: Rendering Performance Optimizations

### Goal

Implement three performance optimizations that reduce render time for preview interaction and filmstrip export: adaptive segment counts, geometry disk cache, and same-material submesh merging.

### Step 1: Adaptive Segment Counts (RenderQuality Tier)

Add to `KnobForge.Core`:

```csharp
public enum RenderQualityTier
{
    Draft = 0,       // Fast preview: reduced segments (50% of normal)
    Normal = 1,      // Default interactive: current segment counts
    Production = 2   // Export quality: higher segments (150% of normal)
}
```

Add to `KnobProject.cs`:
```csharp
public RenderQualityTier PreviewQuality { get; set; } = RenderQualityTier.Normal;
```

**Implementation**:

In each `ResolveConfig` method, add a `RenderQualityTier` parameter (defaulting to `Normal` for backward compatibility):

```csharp
public static IndicatorAssemblyConfig ResolveConfig(KnobProject? project, RenderQualityTier quality = RenderQualityTier.Normal)
```

Apply quality scaling to segment counts:

```csharp
float qualityScale = quality switch
{
    RenderQualityTier.Draft => 0.5f,
    RenderQualityTier.Production => 1.5f,
    _ => 1.0f
};

int radialSegments = (int)(baseRadialSegments * qualityScale);
int latitudeSegments = (int)(baseLatitudeSegments * qualityScale);
int longitudeSegments = (int)(baseLongitudeSegments * qualityScale);
```

**Clamp minimums**: Draft mode must never go below: radialSegments=8, latitudeSegments=4, longitudeSegments=6.

**Wire into MetalViewport**: Pass `project.PreviewQuality` when calling `ResolveConfig` during interactive rendering. Pass `RenderQualityTier.Production` when rendering for export (in `KnobExporter`).

**Wire into PushButton and Slider**: Even though they currently use fixed segment counts (28 sides), parameterize these via the quality tier so Draft mode uses fewer segments.

### Step 2: Geometry Disk Cache

Create `KnobForge.Rendering/GPU/MeshDiskCache.cs`:

```csharp
using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;

namespace KnobForge.Rendering.GPU;

/// <summary>
/// Binary cache for generated mesh geometry, keyed by shape configuration hash.
/// Cache files stored in ~/Library/Caches/Monozukuri/MeshCache/
/// </summary>
public static class MeshDiskCache
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Caches", "Monozukuri", "MeshCache");

    /// <summary>
    /// Try to load cached mesh data for the given shape key hash.
    /// Returns true if cache hit; populates vertices and indices arrays.
    /// </summary>
    public static bool TryLoad(string shapeKeyHash, out MetalVertex[] vertices, out uint[] indices, out float referenceRadius);

    /// <summary>
    /// Save mesh data to disk cache for the given shape key hash.
    /// </summary>
    public static void Save(string shapeKeyHash, MetalVertex[] vertices, uint[] indices, float referenceRadius);

    /// <summary>
    /// Compute a SHA256 hash string from a shape key record's ToString().
    /// </summary>
    public static string ComputeHash<T>(T shapeKey) where T : struct;

    /// <summary>
    /// Evict cache entries older than maxAge. Call on startup.
    /// </summary>
    public static void EvictStale(TimeSpan maxAge);
}
```

**Binary format** (simple, fast):
```
[4 bytes: magic "MZCM"]
[4 bytes: version = 1]
[4 bytes: vertex count]
[4 bytes: index count]
[4 bytes: reference radius (float)]
[N * sizeof(MetalVertex) bytes: vertex data]
[M * sizeof(uint) bytes: index data]
```

Use `Marshal.SizeOf<MetalVertex>()` for vertex stride. Write/read with `BinaryWriter`/`BinaryReader` using `MemoryMarshal.AsBytes` for bulk array I/O.

**Integration**: In `RefreshMeshResources`, before calling `Build*Mesh`, check the disk cache. If hit, skip mesh generation entirely. If miss, build the mesh and save to cache.

**Cache eviction**: Call `MeshDiskCache.EvictStale(TimeSpan.FromDays(30))` once at app startup (in `App.axaml.cs` → `OnFrameworkInitializationCompleted`, or in `MetalViewport` initialization).

### Step 3: Same-Material Submesh Merge

This optimization reduces draw calls when multiple submeshes in an assembly share the same material.

Create a helper in `MetalViewport.MaterialDraw.cs`:

```csharp
/// <summary>
/// Pre-pass that groups assembly submeshes by effective material identity.
/// Returns a list of (materialNode, mergedIndexBuffer) pairs.
/// When two submeshes share identical material properties, their index
/// buffers are concatenated and drawn in a single DrawIndexedPrimitives call.
/// </summary>
private static List<(MaterialNode Material, uint[] MergedIndices)> MergeSubmeshesByMaterial(
    MetalMeshGpuResources resources,
    ModelNode modelNode)
```

**Material identity**: Two `MaterialNode` instances are "identical" if their BaseColor, Metallic, Roughness, DiffuseStrength, and SpecularStrength values are all within epsilon (1e-4f) of each other and they share the same texture map paths.

**Integration**: Use the merge pass in `DrawMeshWithMaterials` when drawing assemblies. For the common case (each part has a unique material), the merge is a no-op and produces the same draw call sequence as before. When materials match (e.g., PushButton bezel and skirt with same material), it saves a draw call.

**Constraints**: Only merge within a single assembly's resource set. Do NOT merge across different assemblies or the main knob mesh. The merged index buffer must preserve winding order.

### Verification

```bash
dotnet build KnobForge.App/KnobForge.App.csproj
```

Test each optimization:

1. **Adaptive segments**: Switch to Draft quality, verify viewport is noticeably faster with visible (but acceptable) segment reduction. Switch to Normal, verify identical to before. Export with Production quality, verify smoother curves.

2. **Disk cache**: Open a project, note first-load time. Close and reopen — second load should be faster (mesh generation skipped). Verify `~/Library/Caches/Monozukuri/MeshCache/` contains `.mzcm` files. Delete cache dir, reopen — should rebuild without error.

3. **Submesh merge**: Set two assembly parts to the same material (e.g., PushButton bezel and skirt both chrome). Verify rendering is identical to non-merged. Performance improvement is most visible in profiling (fewer `DrawIndexedPrimitives` calls).

---

## File Touchpoints

### New Files

| File | Subphase | Purpose |
|------|----------|---------|
| `KnobForge.Core/AssemblyMaterialPresets.cs` | 12C | Shared preset infrastructure (records) |
| `KnobForge.Core/ToggleMaterialPresets.cs` | 12C | Toggle material preset definitions |
| `KnobForge.Core/SliderMaterialPresets.cs` | 12C | Slider material preset definitions |
| `KnobForge.Core/PushButtonMaterialPresets.cs` | 12C | PushButton material preset definitions |
| `KnobForge.Rendering/GPU/MeshDiskCache.cs` | 12D | Binary mesh disk cache |
| `KnobForge.App/Views/MainWindow.PushButtonAssemblyCatalog.cs` | 12A | PushButton mesh library catalog |

### Modified Files

| File | Subphases | Changes |
|------|-----------|---------|
| `KnobForge.Core/KnobProject.cs` | 12A, 12B, 12C, 12D | New enums, new properties, preset selection properties, quality tier |
| `KnobForge.Rendering/GPU/PushButtonAssemblyMeshBuilder.cs` | 12A | Expanded config, cap profiles, bezel profiles, skirt, imported mesh |
| `KnobForge.Rendering/GPU/SliderAssemblyMeshBuilder.cs` | 12B | Expanded config, track geometry, thumb profiles |
| `KnobForge.Rendering/GPU/IndicatorAssemblyMeshBuilder.cs` | 12D | Quality tier parameter in ResolveConfig |
| `KnobForge.Rendering/GPU/ToggleAssemblyMeshBuilder.cs` | 12D | Quality tier parameter in ResolveConfig |
| `KnobForge.App/Controls/MetalViewport/MetalViewport.MeshAndUniforms.cs` | 12A, 12B, 12D | New GPU resource fields, shape key updates, disk cache integration |
| `KnobForge.App/Controls/MetalViewport/MetalViewport.MaterialDraw.cs` | 12C, 12D | Preset override in material resolution, submesh merge |
| `MainWindow.axaml` | 12A, 12B, 12C | New inspector controls (cap profile, thumb profile, track style, preset ComboBox, etc.) |
| `MainWindow.PushButtonAssemblyHandlers.cs` | 12A, 12C | Handlers for new PushButton controls |
| `MainWindow.SliderAssemblyHandlers.cs` | 12B, 12C | Handlers for new Slider controls |
| `MainWindow.ToggleAssemblyHandlers.cs` | 12C | Handler for Toggle material preset ComboBox |

### Untouched Files

| File | Reason |
|------|--------|
| `App.axaml`, `App.axaml.cs` | Design tokens and startup unchanged |
| `ProjectLauncherWindow.*` | Launcher is separate |
| `MainWindow.ProjectTypeCommands.cs` | Type change dialog unchanged |
| All RotaryKnob-specific code | Not in scope |
| `KnobForge.Core/IndicatorLensMaterialPresets.cs` | Existing lens presets remain independent |
| `KnobExporter.cs` | Export logic unchanged (but calls ResolveConfig with Production tier) |

---

## Appendix A: MetalVertex Struct Reference

```csharp
public struct MetalVertex
{
    public Vector3 Position;  // 12 bytes
    public Vector3 Normal;    // 12 bytes
    public Vector4 Tangent;   // 16 bytes (xyz = tangent direction, w = handedness)
    public Vector2 Texcoord;  // 8 bytes
}
// Total: 48 bytes per vertex
```

## Appendix B: Geometry Helper Methods Available

All mesh builders share these patterns (copy between files as needed):

```csharp
private static void AddBox(vertices, indices, width, height, depth, center)
private static void AddFace(vertices, indices, p0, p1, p2, p3, normal, tangentDirection)
private static void AddCylinder(vertices, indices, start, end, radius, sides)
private static void AddCylinderFrustum(vertices, indices, start, end, startRadius, endRadius, segments, capStart, capEnd)
private static void AddSphere(vertices, indices, center, radius, latitudeSegments, longitudeSegments)
private static void AddDisc(vertices, indices, center, radius, sides, normal)
private static void AddCurvedFace(vertices, indices, p0, p1, p2, p3, n0, n1, t0, t1)
private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
```

`AddCylinderFrustum`, `AddSphere`, and `AddCurvedFace` currently exist only in `IndicatorAssemblyMeshBuilder`. Copy them into `PushButtonAssemblyMeshBuilder` and `SliderAssemblyMeshBuilder` as needed (do NOT extract to a shared base class — each builder is a standalone static class).

## Appendix C: Shape Key Pattern

Each assembly type has a shape key record struct used for caching. Example:

```csharp
private readonly record struct PushButtonAssemblyShapeKey(
    float PlateWidth, float PlateHeight, float PlateThickness,
    float BezelRadius, float BezelHeight,
    float CapRadius, float CapHeight,
    float PressDepth,
    // Add ALL new geometry params here when expanding
    PushButtonCapProfile CapProfile,
    PushButtonBezelProfile BezelProfile,
    PushButtonSkirtStyle SkirtStyle,
    float BezelChamferSize,
    float CapOverhang,
    int CapSegments,
    int BezelSegments,
    float SkirtHeight,
    float SkirtRadius,
    string BaseImportedMeshPath,
    long BaseImportedMeshTicks,
    string CapImportedMeshPath,
    long CapImportedMeshTicks);
```

**CRITICAL**: If you add a geometry parameter to the config but forget to add it to the shape key, the cache will serve stale geometry when that parameter changes. Always keep the shape key and config in sync.

## Appendix D: Assembly Part → Submesh Index Mapping

For material preset application, the submesh indices are:

| Type | Index 0 | Index 1 | Index 2 | Index 3 | Index 4 | Index 5 |
|------|---------|---------|---------|---------|---------|---------|
| Toggle | Base (plate+bushing+pivot) | Lever | Sleeve/Tip | — | — | — |
| Slider | Backplate | Thumb | — | — | — | — |
| PushButton | Base (plate+bezel) | Cap | Skirt (new) | — | — | — |
| IndicatorLight | Base | Housing | Lens | Reflector | EmitterCore | Aura |

These indices correspond to the order parts are drawn in `DrawAssembly*` methods in `MetalViewport.MaterialDraw.cs` and determine which `AssemblyPartMaterial` from a preset is applied to which mesh.
