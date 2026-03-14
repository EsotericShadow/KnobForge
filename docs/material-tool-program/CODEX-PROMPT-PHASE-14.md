# Phase 14: Reflection & Glare Controls, Bloom Tuning, and Debug Axis Exposure

## Your Role

You are implementing Phase 14 of the Monozukuri Material Tool Transformation. This phase extends the reflection and post-processing controls so that all five project types benefit from the physically-based environment pipeline built in Phase 13, adds finer-grained bloom and glare parameters to the inspector, and exposes the existing per-subsystem X/Y/Z inversion debug toggles in a dedicated inspector section (currently they are only accessible via the right-click context menu, which most users never discover).

Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification. Do not refactor unrelated code.

## Project Context

Monozukuri (formerly KnobForge) is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs, switches, sliders, buttons, and indicator lights for audio plugin UIs. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–13 are complete. The rendering pipeline uses Metal via `MetalRendererContext`, with per-assembly mesh builders generating `MetalVertex[]` + `uint[]` arrays uploaded to GPU buffers. The Metal shader source lives in `MetalPipelineManager.Shaders.cs` as the C# string constant `FallbackShaderSource`. All shader modifications happen by editing this string.

**Current rendering pipeline state (post-Phase 13):**
- **BRDF**: Anisotropic Beckmann NDF with Schlick-GGX geometry term and Schlick Fresnel. Clear coat uses isotropic GGX. BRDF integration LUT (256×256 RG16Float) for energy-correct specular at grazing angles.
- **Environment lighting**: Procedural gradient (hemisphere blend + horizon band + sky hotspot) with optional equirectangular HDR map blended via `envMapBlend`. Roughness-based mip sampling (`level(roughness * maxMipLevel)`) for physically blurred reflections.
- **Environment presets**: 4 bundled presets (Studio, Rack, Showroom, Dark) selectable from inspector. Manual edits auto-revert to Custom.
- **Bloom pipeline**: 3-stage (extract → directional blur → composite). Shaped kernels: Soft (2-pass H+V), Star4 (4 directions), Star6 (6 directions), AnamorphicStreak (3 directions). Composite scale normalization per shape.
- **Tone mapping**: ACES Fitted (mode 0) and AGX-Like (mode 1) with per-project exposure control.
- **Post-process uniforms**: `PostProcessParams` (exposure, bloomThreshold, bloomKnee, bloomStrength) and `PostProcessParams2` (texelX, texelY, blurDirX/blurDirY or bloomCompositeScale).

**Current bloom inspector controls** (Scene tab → "Bloom · Advanced" expander):
- Kernel shape (ComboBox: Soft, Star 4, Star 6, Anamorphic streak)
- Bloom strength (0–4)
- Bloom threshold (0–16)
- Bloom knee (0.001–8)

**Current environment inspector controls** (Scene tab → "Environment" expander):
- Preset (ComboBox: Custom, Studio, Rack, Showroom, Dark)
- Intensity (0–3)
- Roughness response (0–1)
- Sky color (RGB)
- Ground color (RGB)
- HDRI path, blend, rotation (in "HDRI · Advanced" sub-expander)

**Current "Reflections" expander**: Placeholder text only — "Coming next: screen-space reflections plus environment probes for shiny glare and bounce."

**Current right-click context menu** (`MetalViewport.CameraAndOrientation.cs` lines 99–193): Contains 8 submenus with per-subsystem X/Y/Z inversion toggles for Camera Basis, Gizmo Overlay, Brush/Paint Mapping, Light Effects/Env Lookup, Bloom/Post-Process, Collar Mesh/Compensation, Geometry/Winding, and Debug Actions. These are powerful but effectively hidden from most users.

**Project types**: RotaryKnob, ThumbSlider, FlipSwitch, PushButton, IndicatorLight — all share the same environment/bloom pipeline with no per-type differentiation.

## What Phase 14 Does

### Four Subphases (execute in order):

1. **14A — Extended Bloom & Glare Controls**: Add bloom radius, bloom tint, glare rotation angle, and per-shape intensity bias controls to the inspector and wire them through to the shader/dispatch.

2. **14B — Reflection Controls**: Replace the placeholder "Reflections" expander with real controls: environment reflection strength, Fresnel bias, clear coat reflection override, and a reflection-only preview toggle.

3. **14C — Per-Project-Type Environment Defaults**: When switching project types via `ApplyInteractorProjectTypeDefaults()`, apply type-appropriate environment and bloom starting values (e.g., IndicatorLight gets higher bloom strength, PushButton gets lower exposure).

4. **14D — Debug Axis Inspector Section**: Expose the existing X/Y/Z inversion debug toggles in a collapsible inspector section under the Scene tab, so users can flip light/bloom/camera axes without needing the right-click context menu.

**Explicitly deferred** (do NOT implement):
- Screen-space reflections (SSR) — placeholder text was aspirational, not a Phase 14 deliverable.
- Environment probes / cubemap baking — out of scope.
- Bloom downscale chain (progressive downsample cascade) — future optimization.
- Per-light bloom contribution controls.

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT modify `App.axaml` or `App.axaml.cs`.** Design tokens and startup flow stay identical.
2. **All existing `GpuUniforms` fields must be preserved.** Add new fields at the END of the struct (both C# and Metal sides). Alignment must match.
3. **The app must compile and run identically after each subphase.** New features must default to values that produce the same visual output as before (backward compatibility).
4. **All new enums must be in `KnobForge.Core`** (same namespace as existing enums).
5. **All new project properties must be in `KnobProject.cs`** with sensible defaults that match current behavior.
6. **Do NOT change handler wiring patterns.** Follow existing `OnEnvironmentChanged` / `CommitEnvironmentStateFromUi` patterns.
7. **Keep all existing `x:Name` values.** Code-behind resolves controls by name.
8. **The `FallbackShaderSource` string must remain a single valid Metal shader.** Test compilation by running the app.
9. **The right-click context menu must continue to work.** The new inspector section is an alternative access path, not a replacement. Both must stay in sync.

---

## Existing Architecture (Read Before Coding)

### Shader Source Location

All Metal shader code is in a single C# string constant:

```
KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs
└── private const string FallbackShaderSource = @"...";
```

### Key Shader Sections (Line References)

| Section | Lines | Purpose |
|---|---|---|
| `EvaluateEnvironmentLighting` | 144–175 | Gradient + equirect map blend with roughness-mip sampling |
| `fragment_main` — env specular block | 911–950 | Environment reflection accumulation with BRDF LUT |
| `fragment_main` — clear coat env | 955–969 | Clear coat environment term with separate roughness lookup |
| `fragment_bloom_extract` | 1060–1074 | Luminance threshold extraction |
| `fragment_bloom_blur` | 1076–1092 | 5-tap Gaussian blur with direction vector from `PostProcessParams2.zw` |
| `fragment_bloom_composite` | 1094–1150 | Additive bloom blend onto source; line 1145: `bloomScale` from `PostProcessParams2.z`; line 1146: `bloom = sample * bloomScale` |

### GpuUniforms Layout (C# Side)

Defined in `MetalViewport.ProjectTypesAndBvh.cs`. Fields are `Vector4` packed. The Metal struct must match exactly.

**Current uniform slots used for post-processing:**
- `PostProcessParams` = `(exposure, bloomThreshold, bloomKnee, bloomStrength)`
- `PostProcessParams2` = `(texelX, texelY, blurDirXY_or_bloomCompositeScale, 0)`
- `EnvironmentMapParams` = `(hdriBlend, envMapAvailable, hdriRotationRadians, 0)`
- `EnvironmentMapParams2` = `(lightEffectInvertX, lightEffectInvertY, lightEffectInvertZ, 0)`
- `EnvironmentMapParams3` = `(maxMipLevel, brdfLutAvailable, 0, 0)`

### Bloom Dispatch (MetalViewport.cs)

`RenderBloomPasses()` at lines 1730–1801:
- Gets directions from `GetBloomDirections(shape)` — returns tuples per shape
- Soft path: 2-pass ping-pong (H then V) between `_bloomExtractTexture` / `_bloomBlurTexture`
- Multi-directional path: iterates directions, blurs from `_bloomExtractTexture` into `_bloomAccumTexture` with additive blending (`i > 0`)
- Composite: applies `GetBloomCompositeScale(shape)` — Star4/Star6 = 0.50, Anamorphic = 0.80, Soft = 1.0

### Right-Click Context Menu (MetalViewport.CameraAndOrientation.cs)

`ShowOrientationContextMenu()` at lines 99–193. Uses helper methods:
- `CreateSubmenu(string header, params Control[] items)` — creates a `MenuItem` with `Items` list
- `CreateToggleMenuItem(string label, bool isChecked, Action toggle)` — creates toggle with checkmark
- `CreateActionMenuItem(string label, Action action, bool invalidateGpu = true)` — creates clickable action
- `CreateReadOnlyToggleMenuItem(string label, bool isChecked)` — read-only display toggle

### Inspector Event Pattern

All Scene tab controls use `OnEnvironmentChanged` as their change handler. It delegates to `CommitEnvironmentStateFromUi()` which reads all controls and writes to `_project`. New controls must follow this same pattern:
1. Declare the field in `MainWindow.cs`
2. Resolve by `x:Name` in `MainWindow.Initialization.cs`
3. Wire `OnEnvironmentChanged` handler in initialization
4. Read value in `CommitEnvironmentStateFromUi()`
5. Populate from project in the UI refresh path

### Environment Preset Application

In `MetalViewport.MeshAndUniforms.cs` lines 826–836: when `project.EnvironmentPreset != Custom`, preset values override `envTop`, `envBottom`, `envIntensity`, `envRoughMix`. The override happens at uniform upload time, not in the project model.

---

## Subphase 14A — Extended Bloom & Glare Controls

### Goal

Add finer-grained bloom controls to the inspector: bloom radius (blur spread multiplier), bloom tint color, glare rotation angle (rotates star/streak kernel directions), and per-shape composite intensity bias.

### Step 1: Add Project Properties

**File**: `KnobProject.cs`

Add these properties near the existing bloom properties (after `BloomKernelShape`):

```csharp
public float BloomRadius { get; set; } = 1.0f;
public float BloomTintR { get; set; } = 1.0f;
public float BloomTintG { get; set; } = 1.0f;
public float BloomTintB { get; set; } = 1.0f;
public float GlareRotationDegrees { get; set; } = 0f;
public float BloomCompositeIntensity { get; set; } = 1.0f;
```

**Defaults**: All default to values that produce identical output to current behavior (radius 1.0 = no change, tint white = no color shift, rotation 0 = current directions, composite intensity 1.0 = current scale).

### Step 2: Add Inspector Controls

**File**: `MainWindow.axaml`

In the "Bloom · Advanced" expander (after the existing Bloom knee control at line 2273), add:

```xml
<Grid ColumnDefinitions="120,*" ColumnSpacing="8">
    <TextBlock Grid.Column="0" Classes="param-label" Text="Bloom radius"/>
    <controls:ValueInput Grid.Column="1" x:Name="EnvBloomRadiusInput" Minimum="0.25" Maximum="4" Step="0.01" DecimalPlaces="2" Suffix="x"/>
</Grid>
<Grid ColumnDefinitions="120,*" ColumnSpacing="8">
    <TextBlock Grid.Column="0" Classes="param-label" Text="Composite intensity"/>
    <controls:ValueInput Grid.Column="1" x:Name="EnvBloomCompositeIntensityInput" Minimum="0" Maximum="4" Step="0.01" DecimalPlaces="2"/>
</Grid>
<Grid ColumnDefinitions="120,*" ColumnSpacing="8">
    <TextBlock Grid.Column="0" Classes="param-label" Text="Glare rotation"/>
    <controls:ValueInput Grid.Column="1" x:Name="EnvGlareRotationInput" Minimum="-180" Maximum="180" Step="0.1" DecimalPlaces="1" Suffix=" deg"/>
</Grid>
<Grid ColumnDefinitions="120,*" RowDefinitions="Auto" ColumnSpacing="8">
    <TextBlock Grid.Column="0"
               Classes="param-label"
               Text="Bloom tint"
               VerticalAlignment="Top"
               Margin="0,6,0,0"/>
    <StackPanel Grid.Column="1" Spacing="3">
        <Grid ColumnDefinitions="18,*">
            <TextBlock Grid.Column="0" Text="R" Classes="channel-r"/>
            <controls:ValueInput Grid.Column="1" x:Name="EnvBloomTintRInput" Minimum="0" Maximum="2" Step="0.01" DecimalPlaces="2"/>
        </Grid>
        <Grid ColumnDefinitions="18,*">
            <TextBlock Grid.Column="0" Text="G" Classes="channel-g"/>
            <controls:ValueInput Grid.Column="1" x:Name="EnvBloomTintGInput" Minimum="0" Maximum="2" Step="0.01" DecimalPlaces="2"/>
        </Grid>
        <Grid ColumnDefinitions="18,*">
            <TextBlock Grid.Column="0" Text="B" Classes="channel-b"/>
            <controls:ValueInput Grid.Column="1" x:Name="EnvBloomTintBInput" Minimum="0" Maximum="2" Step="0.01" DecimalPlaces="2"/>
        </Grid>
    </StackPanel>
</Grid>
```

### Step 3: Wire Inspector Controls

Follow the existing pattern exactly:

1. **`MainWindow.cs`**: Add field declarations:
   ```csharp
   private ValueInput? _envBloomRadiusInput;
   private ValueInput? _envBloomCompositeIntensityInput;
   private ValueInput? _envGlareRotationInput;
   private ValueInput? _envBloomTintRInput;
   private ValueInput? _envBloomTintGInput;
   private ValueInput? _envBloomTintBInput;
   ```

2. **`MainWindow.Initialization.cs`**: Resolve by name and wire `OnEnvironmentChanged`:
   ```csharp
   _envBloomRadiusInput = this.FindControl<ValueInput>("EnvBloomRadiusInput");
   _envBloomCompositeIntensityInput = this.FindControl<ValueInput>("EnvBloomCompositeIntensityInput");
   _envGlareRotationInput = this.FindControl<ValueInput>("EnvGlareRotationInput");
   _envBloomTintRInput = this.FindControl<ValueInput>("EnvBloomTintRInput");
   _envBloomTintGInput = this.FindControl<ValueInput>("EnvBloomTintGInput");
   _envBloomTintBInput = this.FindControl<ValueInput>("EnvBloomTintBInput");
   ```
   Wire each to `OnEnvironmentChanged` using the same `PropertyChanged +=` pattern as the existing bloom controls.

3. **`MainWindow.EnvironmentShadowReadouts.cs`**: In `CommitEnvironmentStateFromUi()`, after the existing bloom knee commit (line 113), add:
   ```csharp
   if (_envBloomRadiusInput != null)
       _project.BloomRadius = (float)_envBloomRadiusInput.Value;
   if (_envBloomCompositeIntensityInput != null)
       _project.BloomCompositeIntensity = (float)_envBloomCompositeIntensityInput.Value;
   if (_envGlareRotationInput != null)
       _project.GlareRotationDegrees = (float)_envGlareRotationInput.Value;
   if (_envBloomTintRInput != null)
       _project.BloomTintR = (float)_envBloomTintRInput.Value;
   if (_envBloomTintGInput != null)
       _project.BloomTintG = (float)_envBloomTintGInput.Value;
   if (_envBloomTintBInput != null)
       _project.BloomTintB = (float)_envBloomTintBInput.Value;
   ```

4. **UI refresh path**: In the method that populates Scene tab controls from `_project` (look for where `_envBloomStrengthInput.Value` is set), add matching reads for the new controls.

### Step 4: Apply Bloom Radius to Blur Dispatch

**File**: `MetalViewport.cs`

In `RenderBloomPasses()`, the blur direction vectors control spread. Multiply each direction by `BloomRadius`:

```csharp
float bloomRadius = Math.Clamp(_project?.BloomRadius ?? 1f, 0.25f, 4f);
// In the blur dispatch loop:
uniforms.PostProcessParams2 = new Vector4(
    texelX, texelY,
    direction.dirX * bloomRadius,
    direction.dirY * bloomRadius);
```

This makes the 5-tap kernel sample farther apart (radius > 1) or closer together (radius < 1), effectively controlling blur spread without shader changes.

### Step 5: Apply Glare Rotation to Kernel Directions

**File**: `MetalViewport.cs`

In `GetBloomDirections()`, after building the base directions, rotate all direction vectors by `GlareRotationDegrees` using a 2D rotation matrix:

```csharp
private (float dirX, float dirY)[] GetBloomDirections(BloomKernelShape shape, float rotationDegrees)
{
    var baseDirections = shape switch { /* existing switch body */ };

    if (MathF.Abs(rotationDegrees) < 0.01f)
        return baseDirections;

    float rad = rotationDegrees * (MathF.PI / 180f);
    float cos = MathF.Cos(rad);
    float sin = MathF.Sin(rad);

    var rotated = new (float, float)[baseDirections.Length];
    for (int i = 0; i < baseDirections.Length; i++)
    {
        float x = baseDirections[i].dirX;
        float y = baseDirections[i].dirY;
        rotated[i] = (x * cos - y * sin, x * sin + y * cos);
    }
    return rotated;
}
```

Update the call site in `RenderBloomPasses()` to pass `_project?.GlareRotationDegrees ?? 0f`.

**Note**: For Soft shape (2-pass H+V), rotation has no effect — the H+V decomposition is always axis-aligned. Only apply rotation for Star4, Star6, and AnamorphicStreak.

### Step 6: Apply Bloom Tint and Composite Intensity to Bloom Composite

The bloom composite shader (`fragment_bloom_composite`) currently multiplies bloom by `bloomScale` from `PostProcessParams2.z`. We need to also multiply by the tint color and composite intensity.

**Option A (uniform-based, no shader change)**: Pack tint and intensity into existing or new uniform slots and read them in `fragment_bloom_composite`.

**Option B (simpler, recommended)**: Add a new `Vector4 BloomTintAndIntensity` field to GpuUniforms. Set it to `(tintR * compositeIntensity, tintG * compositeIntensity, tintB * compositeIntensity, 0)`. In the bloom composite shader, multiply the bloom sample by this vector's `.rgb`.

**C# side** — `MetalViewport.ProjectTypesAndBvh.cs`:
Add `public Vector4 BloomTintAndIntensity;` at the end of the `GpuUniforms` struct.

**Metal side** — `MetalPipelineManager.Shaders.cs`:
Add `float4 bloomTintAndIntensity;` at the matching position in the Metal `GpuUniforms` struct.

**Bloom composite shader modification** — in `fragment_bloom_composite` (lines 1145–1146):
```metal
// Current:
float bloomScale = max(0.0, uniforms.postProcessParams2.z);
float3 bloom = bloomTexture.sample(blitSampler, bloomUv).rgb * bloomScale;
// New:
float bloomScale = max(0.0, uniforms.postProcessParams2.z);
float3 bloomTint = uniforms.bloomTintAndIntensity.rgb;
float3 bloom = bloomTexture.sample(blitSampler, bloomUv).rgb * bloomScale * bloomTint;
```

Note: `bloomStrength` (from `postProcessParams.w`) is applied earlier in `fragment_main` during the lighting pass, NOT in the composite shader. The composite only applies `bloomScale` (the per-shape normalization factor from `PostProcessParams2.z`). The tint multiplication goes here alongside `bloomScale`.

**C# uniform upload** — in `RenderBloomPasses()` or the composite uniform setup:
```csharp
float ci = Math.Clamp(_project?.BloomCompositeIntensity ?? 1f, 0f, 4f);
float tR = Math.Clamp(_project?.BloomTintR ?? 1f, 0f, 2f);
float tG = Math.Clamp(_project?.BloomTintG ?? 1f, 0f, 2f);
float tB = Math.Clamp(_project?.BloomTintB ?? 1f, 0f, 2f);
compositeUniforms.BloomTintAndIntensity = new Vector4(tR * ci, tG * ci, tB * ci, 0f);
```

Default `(1, 1, 1, 0)` = no tint, no intensity change = identical to current behavior.

### Step 7: Add Undo/Reference Profile Support

Add `BloomRadius`, `BloomTintR/G/B`, `GlareRotationDegrees`, and `BloomCompositeIntensity` to the undo snapshot and reference profile systems, following the same pattern used for the Phase 13 bloom/environment properties:
- `MainWindow.InspectorNavigationAndHistory.Types.cs`
- `MainWindow.InspectorNavigationAndHistory.UndoCore.cs`
- `MainWindow.InspectorNavigationAndHistory.SceneSelection.cs`
- `MainWindow.ReferenceProfiles.Types.cs`
- `MainWindow.ReferenceProfiles.Snapshot.cs`
- `MainWindow.ReferenceProfiles.Core.cs`

### Verification

- All defaults produce identical output to Phase 13 (bloom radius 1.0, tint white, rotation 0, composite intensity 1.0).
- Bloom radius 2.0 produces visibly wider/softer bloom. Bloom radius 0.5 produces tighter bloom.
- Glare rotation 45° rotates Star4 pattern by 45°. Soft shape is unaffected.
- Bloom tint (1, 0.8, 0.6) produces warm-tinted bloom. (0.6, 0.8, 1.0) produces cool-tinted bloom.
- Composite intensity 2.0 doubles bloom brightness. 0.5 halves it.
- Undo/redo correctly reverts all new controls.
- App compiles and runs without assertion failures.

---

## Subphase 14B — Reflection Controls

### Goal

Replace the placeholder "Reflections" expander with real controls for environment reflection strength, Fresnel bias, clear coat reflection override, and a reflection-only preview toggle.

### Step 1: Add Project Properties

**File**: `KnobProject.cs`

```csharp
public float ReflectionStrength { get; set; } = 1.0f;
public float ReflectionFresnelBias { get; set; } = 0.04f;
public float ClearCoatReflectionStrength { get; set; } = 1.0f;
public bool ReflectionOnlyPreview { get; set; } = false;
```

**Defaults**: Reflection strength 1.0 = current behavior. Fresnel bias 0.04 = current F0 floor. Clear coat strength 1.0 = current behavior. Preview off = current behavior.

### Step 2: Replace Placeholder with Real Controls

**File**: `MainWindow.axaml`

Replace the placeholder "Reflections" expander content (lines 2277–2283) with:

```xml
<Expander Header="Reflections" IsExpanded="False">
    <StackPanel Spacing="6" Margin="0,6,0,0">
        <TextBlock Text="Environment reflection and Fresnel controls." Classes="hint"/>
        <Grid ColumnDefinitions="120,*" ColumnSpacing="8">
            <TextBlock Grid.Column="0" Classes="param-label" Text="Strength"/>
            <controls:ValueInput Grid.Column="1" x:Name="EnvReflectionStrengthInput" Minimum="0" Maximum="4" Step="0.01" DecimalPlaces="2"/>
        </Grid>
        <Grid ColumnDefinitions="120,*" ColumnSpacing="8">
            <TextBlock Grid.Column="0" Classes="param-label" Text="Fresnel bias"/>
            <controls:ValueInput Grid.Column="1" x:Name="EnvReflectionFresnelBiasInput" Minimum="0" Maximum="1" Step="0.001" DecimalPlaces="3"/>
        </Grid>
        <Grid ColumnDefinitions="120,*" ColumnSpacing="8">
            <TextBlock Grid.Column="0" Classes="param-label" Text="Clear coat strength"/>
            <controls:ValueInput Grid.Column="1" x:Name="EnvClearCoatReflectionStrengthInput" Minimum="0" Maximum="4" Step="0.01" DecimalPlaces="2"/>
        </Grid>
        <CheckBox x:Name="EnvReflectionOnlyPreviewCheckBox"
                  Content="Reflection-only preview"/>
    </StackPanel>
</Expander>
```

### Step 3: Wire Inspector Controls

Same pattern as 14A:

1. **`MainWindow.cs`**: Add field declarations.
2. **`MainWindow.Initialization.cs`**: Resolve by name. Wire ValueInputs to `OnEnvironmentChanged`. Wire CheckBox to a similar handler (follow the pattern used for `ShadowEnabledCheckBox` or `IndicatorAssemblyEnabledCheckBox`).
3. **`MainWindow.EnvironmentShadowReadouts.cs`**: Commit to `_project` in `CommitEnvironmentStateFromUi()`.
4. **UI refresh path**: Populate from `_project`.

### Step 4: Pass Reflection Parameters to Shader

**File**: `MetalViewport.MeshAndUniforms.cs`

Use the reserved `.z` and `.w` slots of `EnvironmentMapParams3`, or add a new `Vector4 ReflectionParams` to GpuUniforms. The cleanest approach:

Add a new `Vector4 ReflectionParams` at the end of GpuUniforms:
- `.x` = `reflectionStrength` (multiplier on environment specular accumulation)
- `.y` = `fresnelBias` (overrides the hardcoded 0.04 F0 floor in the shader)
- `.z` = `clearCoatReflectionStrength` (multiplier on clear coat env term)
- `.w` = `reflectionOnlyPreview` (1.0 = show only reflection contribution, 0.0 = normal)

**C# uniform upload**:
```csharp
uniforms.ReflectionParams = new Vector4(
    Math.Clamp(project.ReflectionStrength, 0f, 4f),
    Math.Clamp(project.ReflectionFresnelBias, 0f, 1f),
    Math.Clamp(project.ClearCoatReflectionStrength, 0f, 4f),
    project.ReflectionOnlyPreview ? 1f : 0f);
```

### Step 5: Apply in Fragment Shader

**File**: `MetalPipelineManager.Shaders.cs`

Add `float4 reflectionParams;` to the Metal `GpuUniforms` struct.

In `fragment_main`, modify the environment specular block:

```metal
// Extract reflection params
float reflStrength = uniforms.reflectionParams.x;
float fresnelBias = uniforms.reflectionParams.y;
float clearCoatReflStrength = uniforms.reflectionParams.z;
bool reflectionOnly = uniforms.reflectionParams.w > 0.5;

// Apply Fresnel bias (replace hardcoded F0 floor)
// Current: F0 = max(F0, float3(0.04));
// New: F0 = max(F0, float3(fresnelBias));
```

In the environment specular accumulation:
```metal
// Current:
// accum += envSpecular * roughEnergy * envIntensity * envSpecWeight;
// New:
accum += envSpecular * roughEnergy * envIntensity * envSpecWeight * reflStrength;
```

In the clear coat environment term:
```metal
// Current:
// accum += clearCoatEnvColor * clearCoatFresnelView * envIntensity * clearCoatAmount * clearCoatEnvEnergy;
// New:
accum += clearCoatEnvColor * clearCoatFresnelView * envIntensity * clearCoatAmount * clearCoatEnvEnergy * clearCoatReflStrength;
```

For reflection-only preview, at the end of `fragment_main` before tone mapping:
```metal
if (reflectionOnly)
{
    // Show only the environment specular contribution (no diffuse, no direct lighting)
    // Store envSpecular accumulation in a separate variable earlier in the shader
    finalColor = float4(envReflectionAccum, 1.0);
}
```

**IMPORTANT**: To implement reflection-only preview, you need to accumulate env specular separately from the main `accum`. Add a `float3 envReflectionAccum = float3(0.0);` variable, add to it whenever env specular or clear coat env is added to `accum`, then use it for the preview path.

### Step 6: Add Undo/Reference Profile Support

Same as 14A — add `ReflectionStrength`, `ReflectionFresnelBias`, `ClearCoatReflectionStrength`, `ReflectionOnlyPreview` to undo/reference systems.

### Verification

- Default values produce identical rendering to Phase 13.
- Reflection strength 0 = no environment reflections (diffuse + direct only).
- Reflection strength 2 = double-bright reflections.
- Fresnel bias 0 = reflections only at grazing angles (fully dielectric feel).
- Fresnel bias 1 = reflections everywhere regardless of viewing angle.
- Clear coat strength 0 = no clear coat reflections even with clear coat enabled.
- Reflection-only preview shows a dark scene with only the environment specular highlights visible.
- App compiles and runs without assertion failures.

---

## Subphase 14C — Per-Project-Type Environment Defaults

### Goal

When a user switches project type (or creates a new project of a given type), apply type-appropriate starting values for environment and bloom settings. Currently all types start with the same generic defaults.

### Step 1: Define Per-Type Defaults

**File**: `KnobProject.cs`

In `ApplyInteractorProjectTypeDefaults()`, add environment/bloom default overrides at the end of each type's case block. The method already handles type-specific geometry — extend it with scene settings.

```csharp
// After existing geometry defaults for each type:

case InteractorProjectType.RotaryKnob:
    // Knobs look best with neutral studio lighting
    EnvironmentPreset = EnvironmentPreset.Studio;
    EnvironmentBloomStrength = 0.40f;
    BloomKernelShape = BloomKernelShape.Soft;
    break;

case InteractorProjectType.ThumbSlider:
    // Sliders benefit from subdued lighting to show track detail
    EnvironmentPreset = EnvironmentPreset.Rack;
    EnvironmentBloomStrength = 0.30f;
    BloomKernelShape = BloomKernelShape.Soft;
    break;

case InteractorProjectType.FlipSwitch:
    // Switches look good with moderate lighting and subtle glare
    EnvironmentPreset = EnvironmentPreset.Studio;
    EnvironmentBloomStrength = 0.35f;
    BloomKernelShape = BloomKernelShape.Soft;
    break;

case InteractorProjectType.PushButton:
    // Buttons benefit from showroom lighting to highlight the cap
    EnvironmentPreset = EnvironmentPreset.Showroom;
    EnvironmentBloomStrength = 0.45f;
    BloomKernelShape = BloomKernelShape.Soft;
    break;

case InteractorProjectType.IndicatorLight:
    // LEDs need stronger bloom to sell the glow effect
    EnvironmentPreset = EnvironmentPreset.Dark;
    EnvironmentBloomStrength = 0.80f;
    EnvironmentBloomThreshold = 0.60f;
    BloomKernelShape = BloomKernelShape.Star4;
    break;
```

### Step 2: Also Reset New Phase 14 Properties

In the same method, reset the new Phase 14 properties to their defaults when switching types, so leftover values from a previous type don't carry over:

```csharp
// Reset Phase 14 properties to defaults (all types)
BloomRadius = 1.0f;
BloomTintR = 1.0f;
BloomTintG = 1.0f;
BloomTintB = 1.0f;
GlareRotationDegrees = 0f;
BloomCompositeIntensity = 1.0f;
ReflectionStrength = 1.0f;
ReflectionFresnelBias = 0.04f;
ClearCoatReflectionStrength = 1.0f;
ReflectionOnlyPreview = false;
```

### Step 3: Refresh Inspector After Type Switch

After `ApplyInteractorProjectTypeDefaults()` is called, the UI refresh path must re-read the updated project values and populate the Scene tab controls. Verify that the existing `RefreshInspector()` or equivalent method covers all the new controls. If not, ensure the Scene tab refresh includes the new Phase 14 fields.

### Verification

- Switching to IndicatorLight: bloom strength visibly increases, Star4 kernel activates, Dark preset applies.
- Switching to RotaryKnob: Studio preset applies, Soft kernel, moderate bloom.
- Switching to ThumbSlider: Rack preset applies, lower bloom.
- Existing projects (loaded from file) retain their saved settings — per-type defaults only apply during type switch.
- App compiles and runs without assertion failures.

---

## Subphase 14D — Debug Axis Inspector Section

### Goal

Expose the existing per-subsystem X/Y/Z inversion debug toggles in a collapsible inspector section in the Scene tab, so users can flip light/bloom/camera axes without needing to discover the right-click context menu. Both the inspector section and the context menu must stay in sync.

### Step 1: Add "Debug Axes" Expander to Scene Tab AXAML

**File**: `MainWindow.axaml`

After the "Reflections" expander (after line 2283), add a new section:

```xml
<Expander Header="Debug axes" IsExpanded="False">
    <StackPanel Spacing="6" Margin="0,6,0,0">
        <TextBlock Text="Per-subsystem axis inversion for troubleshooting orientation issues." Classes="hint"/>

        <TextBlock Classes="section-tag" Text="CAMERA BASIS"/>
        <CheckBox x:Name="DebugCameraInvertXCheckBox" Content="Invert X"/>
        <CheckBox x:Name="DebugCameraInvertYCheckBox" Content="Invert Y"/>
        <CheckBox x:Name="DebugCameraInvertZCheckBox" Content="Invert Z"/>
        <CheckBox x:Name="DebugCameraFlip180CheckBox" Content="Flip camera 180°"/>

        <TextBlock Classes="section-tag" Text="LIGHT EFFECTS / ENV LOOKUP"/>
        <CheckBox x:Name="DebugLightEffectInvertXCheckBox" Content="Invert X"/>
        <CheckBox x:Name="DebugLightEffectInvertYCheckBox" Content="Invert Y"/>
        <CheckBox x:Name="DebugLightEffectInvertZCheckBox" Content="Invert Z"/>

        <TextBlock Classes="section-tag" Text="BLOOM / POST-PROCESS"/>
        <CheckBox x:Name="DebugBloomCompositeInvertXCheckBox" Content="Composite invert X"/>
        <CheckBox x:Name="DebugBloomCompositeInvertYCheckBox" Content="Composite invert Y"/>

        <TextBlock Classes="section-tag" Text="GIZMO OVERLAY"/>
        <CheckBox x:Name="DebugGizmoInvertXCheckBox" Content="Invert X"/>
        <CheckBox x:Name="DebugGizmoInvertYCheckBox" Content="Invert Y"/>
        <CheckBox x:Name="DebugGizmoInvertZCheckBox" Content="Invert Z"/>

        <TextBlock Classes="section-tag" Text="GEOMETRY / WINDING"/>
        <CheckBox x:Name="DebugInvertKnobWindingCheckBox" Content="Invert knob front-face winding"/>

        <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,6,0,0">
            <Button x:Name="DebugResetAxesButton" Content="Reset all" MinWidth="80"/>
            <Button x:Name="DebugPrintStateButton" Content="Print state" MinWidth="80"/>
        </StackPanel>
    </StackPanel>
</Expander>
```

### Step 2: Wire CheckBoxes to MetalViewport Debug State

The debug axis fields (`_lightEffectInvertX`, `_gizmoInvertX`, `_orientation.InvertX`, etc.) live on `MetalViewport`, not on `KnobProject`. The inspector section needs to communicate with the viewport instance.

**Approach**: Create a new partial class file `MainWindow.DebugAxes.cs` that:

1. Holds references to all the debug CheckBoxes.
2. On CheckBox change, writes the value to the corresponding `MetalViewport` field via a public method or property.
3. On viewport right-click toggle (context menu), refreshes the CheckBox state.

**MetalViewport side**: Add public properties or a method to get/set all debug axis state:

```csharp
// In MetalViewport (new public properties or a struct accessor)
public bool LightEffectInvertX { get => _lightEffectInvertX; set { _lightEffectInvertX = value; InvalidateGpu(); } }
public bool LightEffectInvertY { get => _lightEffectInvertY; set { _lightEffectInvertY = value; InvalidateGpu(); } }
public bool LightEffectInvertZ { get => _lightEffectInvertZ; set { _lightEffectInvertZ = value; InvalidateGpu(); } }
public bool BloomCompositeInvertX { get => _bloomCompositeInvertX; set { _bloomCompositeInvertX = value; InvalidateGpu(); } }
public bool BloomCompositeInvertY { get => _bloomCompositeInvertY; set { _bloomCompositeInvertY = value; InvalidateGpu(); } }
public bool GizmoInvertX { get => _gizmoInvertX; set { _gizmoInvertX = value; InvalidateGpu(); } }
public bool GizmoInvertY { get => _gizmoInvertY; set { _gizmoInvertY = value; InvalidateGpu(); } }
public bool GizmoInvertZ { get => _gizmoInvertZ; set { _gizmoInvertZ = value; InvalidateGpu(); } }
public bool CameraInvertX { get => _orientation.InvertX; set { _orientation.InvertX = value; InvalidateGpu(); } }
public bool CameraInvertY { get => _orientation.InvertY; set { _orientation.InvertY = value; InvalidateGpu(); } }
public bool CameraInvertZ { get => _orientation.InvertZ; set { _orientation.InvertZ = value; InvalidateGpu(); } }
public bool CameraFlip180 { get => _orientation.FlipCamera180; set { _orientation.FlipCamera180 = value; InvalidateGpu(); } }
public bool InvertKnobWinding { get => _invertKnobFrontFaceWinding; set { _invertKnobFrontFaceWinding = value; InvalidateGpu(); } }
```

**InvalidateGpu()**: If no such method exists, call the viewport's existing invalidation mechanism (look for `InvalidateVisual()`, `_needsRender = true`, or whatever the existing toggle handlers do after flipping a flag — the context menu's `CreateToggleMenuItem` calls an action that just flips the bool and the viewport re-renders on next frame because the render loop checks dirty state).

### Step 3: Sync Between Context Menu and Inspector

When the right-click context menu toggles a value, the inspector CheckBox must update. Add a callback mechanism:

**Option A (event-based)**: Add an `Action? DebugStateChanged` callback on `MetalViewport`. Fire it whenever any debug toggle changes (from context menu or programmatically). In `MainWindow.DebugAxes.cs`, subscribe and refresh CheckBox states.

**Option B (poll-based, simpler)**: In the MainWindow's render cycle or a timer, periodically sync CheckBox state from viewport fields. Less elegant but simpler.

**Recommended**: Option A. In the context menu toggle handler, after flipping the flag, call `DebugStateChanged?.Invoke()`. In `MainWindow.DebugAxes.cs`:

```csharp
private void OnViewportDebugStateChanged()
{
    Dispatcher.UIThread.Post(() =>
    {
        _updatingUi = true;
        try
        {
            if (_debugCameraInvertXCheckBox != null)
                _debugCameraInvertXCheckBox.IsChecked = _metalViewport.CameraInvertX;
            // ... all other checkboxes
        }
        finally
        {
            _updatingUi = false;
        }
    });
}
```

### Step 4: Reset and Print Buttons

Wire `DebugResetAxesButton` to call the viewport's existing `ResetOrientationDebugState()` method and then refresh all CheckBoxes.

Wire `DebugPrintStateButton` to call the viewport's existing `PrintOrientation()` method.

### Verification

- Toggle "Light Effects / Invert X" in the inspector: reflections flip horizontally. Same toggle in context menu also updates the inspector CheckBox.
- Toggle "Camera Basis / Invert Y" in context menu: inspector CheckBox updates to match.
- "Reset all" button returns all toggles to default and all CheckBoxes to unchecked.
- "Print state" prints the same debug output as the context menu's "Print Debug State".
- All existing context menu functionality continues to work unchanged.
- App compiles and runs without assertion failures.

---

## File Touchpoint Table

| Subphase | File | Action |
|---|---|---|
| 14A | `KnobForge.Core/KnobProject.cs` | Add `BloomRadius`, `BloomTintR/G/B`, `GlareRotationDegrees`, `BloomCompositeIntensity` properties |
| 14A | `MainWindow.axaml` | Add 6 controls to "Bloom · Advanced" expander |
| 14A | `MainWindow.cs` | Add field declarations for new controls |
| 14A | `MainWindow.Initialization.cs` | Resolve + wire new controls |
| 14A | `MainWindow.EnvironmentShadowReadouts.cs` | Commit new values to project |
| 14A | `MetalViewport.cs` | Apply bloom radius to blur dispatch; rotate kernel directions; pass tint/intensity |
| 14A | `MetalViewport.ProjectTypesAndBvh.cs` | Add `BloomTintAndIntensity` to C# GpuUniforms |
| 14A | `MetalPipelineManager.Shaders.cs` | Add `bloomTintAndIntensity` to Metal GpuUniforms; use in `fragment_bloom_composite` |
| 14A | Undo/Reference profile files | Add 6 new properties to snapshot/restore |
| 14B | `KnobForge.Core/KnobProject.cs` | Add `ReflectionStrength`, `ReflectionFresnelBias`, `ClearCoatReflectionStrength`, `ReflectionOnlyPreview` properties |
| 14B | `MainWindow.axaml` | Replace placeholder "Reflections" expander with real controls |
| 14B | `MainWindow.cs` | Add field declarations for reflection controls |
| 14B | `MainWindow.Initialization.cs` | Resolve + wire reflection controls |
| 14B | `MainWindow.EnvironmentShadowReadouts.cs` | Commit reflection values to project |
| 14B | `MetalViewport.MeshAndUniforms.cs` | Upload `ReflectionParams` uniform |
| 14B | `MetalViewport.ProjectTypesAndBvh.cs` | Add `ReflectionParams` to C# GpuUniforms |
| 14B | `MetalPipelineManager.Shaders.cs` | Add `reflectionParams` to Metal GpuUniforms; apply strength/bias/preview in `fragment_main` |
| 14B | Undo/Reference profile files | Add 4 new properties to snapshot/restore |
| 14C | `KnobForge.Core/KnobProject.cs` | Extend `ApplyInteractorProjectTypeDefaults()` with per-type env/bloom defaults |
| 14D | `MainWindow.axaml` | Add "Debug axes" expander after "Reflections" |
| 14D | `MainWindow.DebugAxes.cs` | **NEW** — partial class for debug axis inspector wiring |
| 14D | `MetalViewport.cs` or `MetalViewport.CameraAndOrientation.cs` | Add public properties for debug flags; add `DebugStateChanged` callback |

---

## Appendix A: GpuUniforms Extension Plan

New fields added at the END of the struct:

| Field | Type | Subphase | Contents |
|---|---|---|---|
| `bloomTintAndIntensity` | `float4` | 14A | `.rgb` = tint × composite intensity, `.w` = reserved |
| `reflectionParams` | `float4` | 14B | `.x` = reflStrength, `.y` = fresnelBias, `.z` = clearCoatReflStrength, `.w` = reflectionOnly flag |

Both C# and Metal structs must match. Ensure 16-byte alignment.

## Appendix B: Backward Compatibility Defaults

| Setting | Default Value | Matches Current Behavior |
|---|---|---|
| `BloomRadius` | 1.0 | Yes — no scaling applied |
| `BloomTintR/G/B` | 1.0, 1.0, 1.0 | Yes — white = no tint |
| `GlareRotationDegrees` | 0 | Yes — no rotation |
| `BloomCompositeIntensity` | 1.0 | Yes — no scaling |
| `ReflectionStrength` | 1.0 | Yes — full reflections |
| `ReflectionFresnelBias` | 0.04 | Yes — matches hardcoded F0 floor |
| `ClearCoatReflectionStrength` | 1.0 | Yes — full clear coat reflections |
| `ReflectionOnlyPreview` | false | Yes — normal rendering |
| Per-type env defaults | Only on type switch | Yes — existing projects unaffected |
| Debug axis CheckBoxes | Unchecked | Yes — matches default debug state |

## Appendix C: Performance Notes

- **14A**: Bloom radius is zero-cost (just changes direction vector magnitude). Glare rotation is 2 multiplies + 2 adds per direction per frame (negligible). Bloom tint is one extra multiply in the composite shader per fragment.
- **14B**: Reflection strength is one multiply per fragment. Fresnel bias changes an existing comparison. Reflection-only preview adds a branch (predicted well since it's uniform-driven).
- **14C**: Zero runtime cost — only changes property defaults during type switch.
- **14D**: Zero rendering cost — UI only. The debug flags themselves are already free (they modify uniform values that are uploaded regardless).
