# Codex Implementation Prompt — Phase 3: Paint System Upgrades

## Your Role

You are implementing Phase 3 of the KnobForge Material Tool Transformation. Your job is to upgrade the paint system with variable resolution paint masks, true layer compositing with blend modes, and new roughness/metallic paint channels. Work incrementally — complete each subphase, verify it compiles and runs, then move to the next. Do not skip verification steps. Do not refactor unrelated code.

## Project Context

KnobForge is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs and UI components for audio plugins. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phase 1 (UV Infrastructure) and Phase 2 (Texture Map Import) are complete. The rendering pipeline now supports vertex UVs and PBR texture map sampling on slots 4-7. The paint system uses a separate "legacy planar" UV space for weathering/paint layers.

## ⚠️ CRITICAL: TWO SEPARATE UV SYSTEMS

KnobForge has two independent UV coordinate systems. You MUST keep them separate:

1. **Paint UVs** (legacy planar projection): `localXY / (referenceRadius * 2.0) + 0.5` — computed at runtime in the shader from world position. Used for paint mask sampling, scratch carving, weathering. This is the UV space the paint system operates in.

2. **Material UVs** (`inVertex.texcoord`): vertex attribute UVs computed at mesh build time. Used for texture map sampling (albedo, normal, roughness, metallic maps from Phase 2).

The paint system works exclusively in paint UV space. Do NOT use `inVertex.texcoord` for paint operations. Do NOT use `localXY` projection for material texture sampling.

## Current State (Verified)

### Paint Mask Storage

```
File: KnobForge.Core/KnobProject.cs

public const int DefaultPaintMaskSize = 1024;  // Line 145
private readonly byte[] _paintMaskRgba8 = new byte[DefaultPaintMaskSize * DefaultPaintMaskSize * 4];  // Line 287
private int _paintMaskVersion = 1;  // Line 288

public int PaintMaskSize => DefaultPaintMaskSize;  // Line 484
public int PaintMaskVersion => _paintMaskVersion;   // Line 485
```

Core methods on KnobProject:
- `GetPaintMaskRgba8()` — returns the byte array
- `ClearPaintMask()` — zeros array, increments version
- `SamplePaintMaskBilinear(float u, float v)` — bilinear UV sample, returns Vector4 (0-1)
- `StampPaintMaskUv(...)` — renders a paint stamp to the CPU buffer, returns bool if modified
- `ReadMaskRgba(int x, int y)` — private, reads single pixel as normalized Vector4

### PaintChannel Enum (KnobProject.cs, lines 63-71)

```csharp
public enum PaintChannel
{
    Rust = 0,      // Red channel of paint mask
    Wear = 1,      // Green channel
    Gunk = 2,      // Blue channel
    Scratch = 3,   // Alpha channel
    Erase = 4,     // Special: erases all channels
    Color = 5      // Special: uses separate color texture
}
```

All 4 RGBA channels of the paint mask are occupied: R=Rust, G=Wear, B=Gunk, A=Scratch.

### Paint Layer Data Model

Runtime layers exist but are lightweight — just a name:

```csharp
// MetalViewport.cs, lines 180-188
private sealed class PaintLayerState
{
    public string Name { get; set; }
}
```

Layer info exposed to UI:
```csharp
// MetalViewport.cs, lines 164-178
public readonly struct PaintLayerInfo
{
    public int Index { get; }
    public string Name { get; }
    public bool IsActive { get; }
    public bool IsFocused { get; }
}
```

Persisted models (MetalViewport.ProjectTypesAndBvh.cs, lines 28-63):
```csharp
private sealed class PaintProjectState
{
    public List<PaintLayerPersisted>? Layers { get; set; }
    public int ActiveLayerIndex { get; set; }
    public int FocusedLayerIndex { get; set; } = -1;
    public int PaintHistoryRevision { get; set; }
    public List<PaintStrokePersisted>? Strokes { get; set; }
}

private sealed class PaintLayerPersisted
{
    public string? Name { get; set; }
}

private sealed class PaintStampPersisted
{
    public float UvX { get; set; }
    public float UvY { get; set; }
    public float UvRadius { get; set; }
    public float Opacity { get; set; }
    public float Spread { get; set; }
    public PaintChannel Channel { get; set; }
    public PaintBrushType BrushType { get; set; }
    public ScratchAbrasionType ScratchAbrasionType { get; set; }
    public float PaintColorX { get; set; }
    public float PaintColorY { get; set; }
    public float PaintColorZ { get; set; }
    public uint Seed { get; set; }
    public int LayerIndex { get; set; }
}
```

Layer management fields (MetalViewport.cs):
```csharp
private readonly List<PaintLayerState> _paintLayers = new();
private readonly List<PaintStampCommand> _pendingPaintStampCommands = new();
private readonly List<PaintStampCommand> _activeStrokeCommands = new();
private readonly List<PaintStrokeRecord> _committedPaintStrokes = new();
private int _activePaintLayerIndex;
private int _focusedPaintLayerIndex = -1;
private int _paintHistoryRevision;
private bool _paintRebuildRequested = true;
```

### GPU Paint Texture (MetalViewport.PaintResources.cs)

`EnsurePaintMaskTexture(KnobProject? project)` creates a Metal texture:
- Format: RGBA8Unorm (pixel format code 70)
- Size: project.PaintMaskSize × project.PaintMaskSize
- Mipmapped: true
- Usage: ShaderRead | RenderTarget

There is also `EnsurePaintColorTexture` — a separate RGBA8 texture for the Color channel.

### Paint Stamp Pipelines (MetalViewport.PaintResources.cs)

6 per-channel GPU pipeline states with color write masks:
- Rust: MTLColorWriteMaskRed
- Wear: MTLColorWriteMaskGreen
- Gunk: MTLColorWriteMaskBlue
- Scratch: MTLColorWriteMaskAlpha
- Erase: MTLColorWriteMaskAll (with zero blend factors)
- Color: MTLColorWriteMaskAll (targets separate color texture)

Each uses:
- sourceRgbBlendFactor: MTLBlendFactorSourceAlpha
- destinationRgbBlendFactor: MTLBlendFactorOneMinusSourceAlpha

### Paint Stamp Shader (MetalViewport.Shaders.cs)

The fragment_paint_stamp shader reads a `PaintStampUniform` with center, radius, opacity, brush type, channel, and seed. It computes brush weight and outputs `float4(1,1,1, alpha)` for standard channels, `float4(0,0,0, alpha)` for erase, and `float4(paintColor, alpha)` for color.

### Fragment Shader Weathering (MetalPipelineManager.Shaders.cs)

Paint mask is sampled in the fragment shader at paint UV coordinates:
```metal
float2 paintUv = localXY / max(referenceRadius * 2.0, 1e-4) + 0.5;
float4 paintSample = paintMask.sample(paintSampler, clamp(paintUv, float2(0.0), float2(1.0)));
```

The RGBA channels drive weathering effects:
- R (rustRaw) → rust color overlay, roughness increase, metallic decrease
- G (wearRaw) → wear color blend, slight roughness decrease
- B (gunkRaw) → gunk color overlay, roughness increase, metallic decrease
- A (scratchRaw) → scratch expose, overrides roughness and metallic

### Texture Slots (Fragment Shader Signature)

```metal
fragment float4 fragment_main(
    VertexOut inVertex [[stage_in]],
    constant GpuUniforms& uniforms [[buffer(1)]],
    texture2d<float> spiralNormalMap [[texture(0)]],
    texture2d<float> paintMask [[texture(1)]],
    texture2d<float> paintColor [[texture(2)]],
    texture2d<float> environmentMap [[texture(3)]],
    texture2d<float> albedoMap [[texture(4)]],
    texture2d<float> normalMap [[texture(5)]],
    texture2d<float> roughnessMap [[texture(6)]],
    texture2d<float> metallicMap [[texture(7)]])
```

Slot 8 is the next available slot.

### GpuUniforms C# Struct (MetalViewport.ProjectTypesAndBvh.cs)

The struct ends with `TextureMapFlags` and `TextureMapParams` before the lights array. New uniform fields must go between `TextureMapParams` and `Light0`, OR you can repurpose reserved components in existing fields. The MSL and C# structs MUST stay perfectly in sync.

### Serialization

System.Text.Json with `knobforge.project.v1` format ID. Missing fields default to null/default on deserialization — additive-safe. The paint history is serialized via MetalViewport's ExportPaintStateJson/TryImportPaintStateJson as a separate JSON blob within the project.

## Execution Order

### SUBPHASE 3A: Variable Resolution Paint Masks (do this FIRST)

**Step 1: Make PaintMaskSize mutable on KnobProject**

- File: `KnobForge.Core/KnobProject.cs`
- Change `public const int DefaultPaintMaskSize = 1024` to keep it as a default constant
- Add a mutable property: `public int PaintMaskSize { get; private set; } = DefaultPaintMaskSize;`
- Add: `SetPaintMaskResolution(int size)` method:
  - Validate size is one of: 512, 1024, 2048, 4096
  - Reallocate `_paintMaskRgba8 = new byte[size * size * 4]`
  - Set PaintMaskSize to new value
  - Clear all paint data (zero the array)
  - Increment `_paintMaskVersion`
- Change the `_paintMaskRgba8` field from `readonly` to non-readonly so it can be reassigned

**Step 2: Replace all DefaultPaintMaskSize references with PaintMaskSize**

- Audit every reference to `DefaultPaintMaskSize` in the codebase
- In `StampPaintMaskUv`, `SamplePaintMaskBilinear`, `ClearPaintMask`, `ReadMaskRgba`: use `PaintMaskSize` instead of `DefaultPaintMaskSize`
- Keep `DefaultPaintMaskSize` as the constant for the initial value only

**Step 3: GPU texture resize detection**

- File: `MetalViewport.PaintResources.cs`
- In `EnsurePaintMaskTexture`: detect size mismatch between existing texture and `project.PaintMaskSize`
- If mismatch: dispose old texture, create new one at the new size
- Same for `EnsurePaintColorTexture`

**Step 4: Serialization**

- `PaintMaskSize` must serialize in the project file
- Old projects without this field default to 1024 via the default property value

**Step 5: UI control**

- Add a resolution dropdown (512, 1024, 2048, 4096) to the paint section of the inspector
- Show estimated memory: "~1 MB" / "~4 MB" / "~16 MB" / "~64 MB" (size × size × 4 bytes per layer)
- When changed: show a confirmation dialog warning that all paint data will be cleared
- After confirmation: call `project.SetPaintMaskResolution(newSize)`, clear paint stamps, invalidate GPU

**VERIFY:** Build and run. Open a project, paint some strokes. Change resolution to 2048 — confirm paint clears and new resolution takes effect. Change back to 1024. Export spritesheet — confirm correct rendering. Load an old project (no PaintMaskSize field) — confirm it defaults to 1024 and renders correctly.

---

### SUBPHASE 3B: True Layer Compositing (after 3A is verified)

The existing layer system stores layers as lightweight metadata (just a name) and strokes are replayed into a single shared paint mask buffer. Phase 3 upgrades this so each layer has its own pixel data buffer, blend mode, and opacity, with CPU compositing merging them into the shared buffer for GPU upload.

**Step 1: Upgrade PaintLayer data model**

- New file (or in `KnobProject.cs`): `PaintLayer` class:
  ```csharp
  public sealed class PaintLayer
  {
      public string Name { get; set; } = "Layer";
      public float Opacity { get; set; } = 1.0f;
      public PaintBlendMode BlendMode { get; set; } = PaintBlendMode.Normal;
      public bool Visible { get; set; } = true;
      public byte[]? PixelData { get; set; }  // Lazy allocation — null until first stroke
  }
  ```
- `PaintBlendMode` enum: `Normal, Multiply, Screen, Overlay, Add`
- IMPORTANT: `PixelData` is allocated lazily (null until first stroke on that layer). Size = `PaintMaskSize * PaintMaskSize * 4`. This prevents 4 empty 16MB arrays at 2048 resolution.

**Step 2: Add layer list to KnobProject**

- Replace or supplement the single `_paintMaskRgba8` with:
  - `List<PaintLayer> _paintLayers`
  - `byte[] _compositedPaintMask` — the merged output that gets uploaded to GPU
- `_compositedPaintMask` is always `PaintMaskSize * PaintMaskSize * 4` bytes
- Active painting writes to the active layer's `PixelData`
- After each stroke commit or layer property change, recomposite into `_compositedPaintMask`
- `GetPaintMaskRgba8()` returns `_compositedPaintMask` (not individual layer data)
- `StampPaintMaskUv` writes to the active layer's `PixelData`, then triggers recomposite

**Step 3: PaintLayerCompositor**

- New file: `KnobForge.Core/PaintLayerCompositor.cs`
- Method: `static void Composite(IReadOnlyList<PaintLayer> layers, byte[] outputRgba8, int size)`
- Algorithm:
  1. Clear output to all zeros
  2. For each visible layer, bottom to top:
     - If PixelData is null, skip (empty layer)
     - For each pixel, blend layer pixel into output using BlendMode and Opacity
  3. Do NOT increment version — caller does that

- Blend mode implementations (all operate on normalized 0-1 values):
  - **Normal**: `out = lerp(dst, src, srcA * layerOpacity)`
  - **Multiply**: `result.rgb = dst.rgb * src.rgb`, then apply opacity: `out = lerp(dst, result, srcA * layerOpacity)`
  - **Screen**: `result.rgb = 1 - (1 - dst.rgb) * (1 - src.rgb)`, then apply opacity
  - **Overlay**: `result.rgb = dst < 0.5 ? 2*dst*src : 1 - 2*(1-dst)*(1-src)`, then apply opacity
  - **Add**: `result.rgb = min(1, dst.rgb + src.rgb)`, then apply opacity
  - Alpha: standard Porter-Duff over compositing

- Performance note: At 2048×2048 with 4 layers this is ~67 million pixel ops. Keep it simple — no SIMD, no parallelism. If it takes >50ms, add a "dirty layer index" optimization to only recomposite from the lowest changed layer upward.

**Step 4: Migration from single buffer to layer system**

- When loading old projects (no layer data): create a single "Default" layer containing the existing `_paintMaskRgba8` as its `PixelData`
- When loading old projects via `TryImportPaintStateJson`: replay strokes into a single Default layer
- New projects start with one empty "Default" layer

**Step 5: Update PaintLayerPersisted**

- Add `Opacity`, `BlendMode`, `Visible` fields to `PaintLayerPersisted`
- Old persisted data without these fields defaults to opacity=1, blend=Normal, visible=true

**Step 6: Update MetalViewport layer management**

- `PaintLayerState` gets `Opacity`, `BlendMode`, `Visible` fields
- `PaintLayerInfo` gets `Opacity`, `BlendMode`, `Visible` fields
- The paint stamp replay logic must write to per-layer PixelData buffers
- After stroke commit: trigger compositing, then upload composited result to GPU

**Step 7: Layer UI enhancements**

- Per-layer opacity slider (0-100%)
- Blend mode dropdown per layer
- Visibility toggle (eye icon or checkbox) per layer
- Memory indicator: "{layerCount} layers × {resolution}px = {totalMB} MB"

**VERIFY:** Build and run. Create a new project. Paint rust on Layer 1. Add Layer 2, paint wear. Change Layer 2 blend mode to Multiply — verify visual difference. Change Layer 2 opacity to 50% — verify blending. Toggle Layer 2 visibility off — verify only Layer 1 shows. Save project, close, reopen — verify layers and settings restored. Load an old project with no layer data — verify it migrates to a single Default layer with existing paint intact.

---

### SUBPHASE 3C: Roughness and Metallic Paint Channels (after 3B is verified)

**Architecture Decision: Second Paint Mask Texture**

The existing RGBA paint mask has all 4 channels occupied (R=rust, G=wear, B=gunk, A=scratch). Roughness and metallic need their own channels. Use a second paint mask texture:

- `_paintMask2Rgba8` — second RGBA8 buffer where R=roughness paint, G=metallic paint, B=reserved, A=reserved
- GPU texture slot 8: `paintMask2 [[texture(8)]]`

This keeps the existing weathering system completely untouched and adds the new channels as an independent layer.

**Step 1: Add new PaintChannel values**

- File: `KnobProject.cs`
- Add to PaintChannel enum: `Roughness = 6`, `Metallic = 7`

**Step 2: Second paint mask buffer and texture**

- KnobProject: add `_paintMask2Rgba8` byte array (same size as primary paint mask)
- KnobProject: add stamping support for Roughness/Metallic channels into the second mask
  - Roughness stamps write to byte index 0 (R channel) of `_paintMask2Rgba8`
  - Metallic stamps write to byte index 1 (G channel) of `_paintMask2Rgba8`
  - The stamp VALUE (not just opacity) matters — when painting roughness, you're painting a specific roughness value (e.g., 0.8) at a specific opacity
- Add `_paintMask2Version` tracking
- Add `GetPaintMask2Rgba8()`, `ClearPaintMask2()`
- Add `SamplePaintMask2Bilinear(float u, float v)` — same pattern as the primary

**Step 3: GPU texture for second mask**

- File: `MetalViewport.PaintResources.cs`
- Add `EnsurePaintMask2Texture` — same pattern as `EnsurePaintMaskTexture`
- Add `_paintMask2Texture` IntPtr field on MetalViewport
- Create corresponding pipeline states for roughness and metallic channels:
  - Roughness pipeline: MTLColorWriteMaskRed targeting paintMask2
  - Metallic pipeline: MTLColorWriteMaskGreen targeting paintMask2

**Step 4: Fragment shader integration**

- File: `MetalPipelineManager.Shaders.cs`
- Add texture slot 8: `texture2d<float> paintMask2 [[texture(8)]]`
- After the existing weathering block (after line ~707 where scratch overrides roughness/metallic):
  ```metal
  // Roughness/metallic paint override
  float4 paintMask2Sample = float4(0.0);
  if (all(paintUv >= float2(0.0)) && all(paintUv <= float2(1.0)))
  {
      paintMask2Sample = paintMask2.sample(paintSampler, clamp(paintUv, float2(0.0), float2(1.0)));
  }
  float roughnessPaintMask = clamp(paintMask2Sample.x, 0.0, 1.0);   // R channel
  float metallicPaintMask = clamp(paintMask2Sample.y, 0.0, 1.0);    // G channel
  // The paint mask stores the target value * alpha in the channel, and alpha separately
  // Actually: roughness paint should store painted-roughness-value in R and paint-alpha in a companion
  ```

  **IMPORTANT**: The roughness/metallic channels need to store both a VALUE and an OPACITY. The value is what roughness/metallic to paint (e.g., 0.2 for smooth, 0.9 for rough). The opacity is how strongly to apply it. Options:
  - **Option A (simple)**: Pack into single channel — store `value * alpha` and reconstruct. Problem: can't distinguish "no paint" from "paint black at full opacity."
  - **Option B (recommended)**: Use R=roughness value, G=metallic value, B=roughness alpha, A=metallic alpha. This uses all 4 channels of the second mask cleanly.

  With Option B, the shader becomes:
  ```metal
  float roughnessTarget = paintMask2Sample.x;
  float metallicTarget = paintMask2Sample.y;
  float roughnessAlpha = clamp(paintMask2Sample.z, 0.0, 1.0);
  float metallicAlpha = clamp(paintMask2Sample.w, 0.0, 1.0);
  roughness = mix(roughness, roughnessTarget, roughnessAlpha);
  metallic = mix(metallic, metallicTarget, metallicAlpha);
  ```

**Step 5: Bind texture slot 8 in both render paths**

- File: `MetalViewport.cs` (live viewport) — bind `_paintMask2Texture` to fragment texture slot 8
- File: `MetalViewport.OffscreenRender.cs` (export) — same binding
- Use fallback texture (1×1 black/zero) when no paint mask 2 exists

**Step 6: Paint stamp pipeline for new channels**

- The existing stamp pipeline renders into paintMask texture (slot 1 render target)
- New roughness/metallic stamps must render into paintMask2 texture
- The stamp shader output for roughness: `float4(targetValue, 0, alpha, 0)` — but this conflicts with the single-output pipeline approach
- Better approach: For roughness stamps, the fragment_paint_stamp outputs `float4(targetRoughness, 0, targetRoughness, 0)` with alpha, and the pipeline writes to R+B channels (R=value, B=alpha). For metallic: `float4(0, targetMetallic, 0, targetMetallic)` writing to G+A channels.
- Create 2 new pipeline states with appropriate color write masks

**Step 7: CPU-side stamping**

- Update `StampPaintMaskUv` (or add a parallel method) to handle Roughness and Metallic channels
- These channels need a "target value" parameter — the roughness/metallic value to paint
- The stamp writes to `_paintMask2Rgba8`:
  - Roughness: R = lerp(existing_R, targetValue, stampAlpha), B = max(existing_B, stampAlpha)
  - Metallic: G = lerp(existing_G, targetValue, stampAlpha), A = max(existing_A, stampAlpha)

**Step 8: Brush UI for new channels**

- Add Roughness and Metallic options to the channel selector dropdown
- When Roughness or Metallic is selected, show a "Target Value" slider (0.0 to 1.0)
- The target value is what roughness/metallic value gets painted
- Hide scratch-specific controls when roughness/metallic is selected

**Step 9: Layer compositing for second paint mask**

- If you implemented per-layer compositing in 3B, the second paint mask also needs per-layer support
- Each PaintLayer gets an optional `byte[]? PixelData2` for the second mask
- PaintLayerCompositor handles compositing both masks
- OR: keep roughness/metallic paint on a single flat buffer without layer support initially (simpler, and layers can be added later)

**VERIFY:** Build and run. Select Roughness channel, set target value to 0.2 (smooth). Paint a stroke on a rough surface — verify the painted area becomes visually smoother. Select Metallic channel, set target value to 0.9. Paint a stroke on a non-metallic surface — verify the painted area becomes reflective. Verify that existing rust/wear/gunk/scratch channels still work identically. Export spritesheet — confirm new channels render in export. Save and reload — verify roughness/metallic paint data persists.

---

## Critical Constraints

1. **ZERO VISUAL REGRESSION on existing features.** After every subphase, rust/wear/gunk/scratch weathering, color painting, texture maps, spritesheet export — all must work identically to before.
2. **Do not change the two UV systems.** Paint UVs stay on legacy planar projection. Material UVs stay on `inVertex.texcoord`.
3. **Do not change existing texture slot bindings 0-7.** Add new bindings at slot 8+.
4. **Do not change the vertex format.** MetalVertex is 48 bytes with position, normal, tangent, texcoord — unchanged in Phase 3.
5. **Build after every task.** If it doesn't compile, fix it before moving on.
6. **GpuUniforms: MSL and C# must stay in sync.** If you add fields to the MSL struct, add them at the same position in the C# struct.

## What NOT to Do

- Do not add multi-material support (that's Phase 4)
- Do not add texture bake export (that's Phase 5)
- Do not add a node graph (that's Phase 7)
- Do not refactor the shader string into separate files
- Do not introduce new NuGet dependencies
- Do not rename existing variables or methods unless required for new functionality
- Do not optimize anything prematurely — correctness first
- Do not change the Material UV system or vertex shader

## After Phase 3 Is Complete

Update `docs/material-tool-program/00-PROGRAM.md` — change Phase 3 status from "Not started" to "Complete". Then read `docs/material-tool-program/04-PHASE-4-MULTI-MATERIAL.md` for the next phase.
