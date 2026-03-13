# Phase 3: Paint System Upgrades

## Phase Identity

- **Phase**: 3 of 6
- **Name**: Paint System Upgrades
- **Depends on**: Phase 1 (UV Infrastructure)
- **Unlocks**: Phase 5 (Texture Bake) — composited layers are bake inputs
- **Risk**: Low — isolated subsystem, existing paint behavior preserved as default
- **Milestone**: M3 — Variable resolution, true layer compositing, roughness/metallic channels

## Why This Phase Exists

The paint system is one of KnobForge's differentiators — direct 3D painting of weathering effects. But it's limited: fixed 1024x1024 resolution, no real layer compositing, no blend modes, and channels are locked to rust/wear/gunk/scratch with no way to paint roughness or metallic variation directly. These upgrades make the paint system competitive with dedicated texture painting tools.

## Subphases

### Subphase 3A: Variable Resolution Paint Masks

#### Project 3A.1: Resolution Configuration

**Task 3A.1.1: Make PaintMaskSize a project-level setting**
- File: `KnobForge.Core/KnobProject.cs`
- Change: Replace `public const int DefaultPaintMaskSize = 1024` with a mutable property
- Add: `public int PaintMaskSize { get; private set; } = 1024;`
- Add: `SetPaintMaskResolution(int size)` method that validates (512/1024/2048/4096), reallocates the byte array, clears paint data, and increments version
- Constraint: Resolution changes clear all paint data (warn user in UI)

**Task 3A.1.2: Dynamic byte array allocation**
- File: `KnobProject.cs`
- Change: `_paintMaskRgba8` becomes `new byte[PaintMaskSize * PaintMaskSize * 4]`
- Change: All references to `DefaultPaintMaskSize` in stamping/sampling methods use `PaintMaskSize` instead
- Audit: `StampPaintMaskUv`, `SamplePaintMaskBilinear`, `ClearPaintMask`, `ReadMaskRgba`

**Task 3A.1.3: GPU texture resize**
- File: `MetalViewport.PaintResources.cs`
- Change: `EnsurePaintMaskTexture` already reads `project.PaintMaskSize` for texture creation — verify it recreates when size changes
- Add: Detect size mismatch between existing texture and project setting, dispose and recreate if changed

**Task 3A.1.4: Serialization of resolution setting**
- `PaintMaskSize` serializes in the project file
- Old projects without this field default to 1024

**Task 3A.1.5: UI control**
- File: MainWindow material/paint inspector partial
- Add: Resolution dropdown (512, 1024, 2048, 4096) in the paint section header
- Add: Memory estimate display: "~4 MB" / "~16 MB" / "~64 MB" / "~256 MB"
- Add: Confirmation dialog when changing resolution ("This will clear all paint data")

---

### Subphase 3B: True Layer Compositing

#### Project 3B.1: Per-Layer Texture Storage

**Task 3B.1.1: Layer data model**
- File: `KnobProject.cs` (or new file `PaintLayer.cs`)
- Add: `PaintLayer` class with:
  - `string Name`
  - `float Opacity` (0-1, default 1.0)
  - `PaintBlendMode BlendMode` (enum: Normal, Multiply, Screen, Overlay, Add)
  - `bool Visible` (default true)
  - `byte[] PixelData` (RGBA8, PaintMaskSize x PaintMaskSize x 4)

**Task 3B.1.2: PaintBlendMode enum**
- New file or in `KnobProject.cs`
- Values: `Normal`, `Multiply`, `Screen`, `Overlay`, `Add`
- Each mode defines how a layer's pixel value combines with the layer below

**Task 3B.1.3: Layer list on KnobProject**
- Replace the single `_paintMaskRgba8` with a `List<PaintLayer>` and a composited output buffer
- The composited output is what gets uploaded to the GPU paint mask texture
- Active painting writes to the active layer's `PixelData`
- Compositing runs after each stroke commit or layer property change

#### Project 3B.2: CPU Compositing Engine

**Task 3B.2.1: Create PaintLayerCompositor class**
- New file: `KnobForge.Core/PaintLayerCompositor.cs`
- Method: `Composite(IReadOnlyList<PaintLayer> layers, byte[] outputRgba8, int size)`
- Algorithm:
  1. Clear output to zero
  2. For each visible layer, bottom to top:
     - For each pixel, blend layer pixel into output using BlendMode and Opacity
  3. Increment version

**Task 3B.2.2: Blend mode implementations**
- Normal: `out = lerp(dst, src, srcA * layerOpacity)`
- Multiply: `out.rgb = dst.rgb * src.rgb` (then apply opacity)
- Screen: `out.rgb = 1 - (1 - dst.rgb) * (1 - src.rgb)`
- Overlay: `out.rgb = dst < 0.5 ? 2*dst*src : 1 - 2*(1-dst)*(1-src)`
- Add: `out.rgb = min(1, dst.rgb + src.rgb)`
- All modes process RGBA channels; alpha compositing uses standard Porter-Duff over

**Task 3B.2.3: Compositing trigger points**
- After paint stroke commit: recomposite
- After layer property change (opacity, blend mode, visibility): recomposite
- After layer reorder: recomposite
- After layer delete: recomposite
- Optimization: only recomposite from the lowest changed layer upward

**Task 3B.2.4: Migration from single-buffer to layer system**
- On loading old projects (no layers): create a single "Default" layer containing the existing `_paintMaskRgba8` data
- New projects start with one empty "Default" layer

#### Project 3B.3: Layer UI Enhancements

**Task 3B.3.1: Per-layer opacity slider**
- File: MainWindow paint layers partial
- Add: Opacity slider (0-100%) next to each layer in the layer list

**Task 3B.3.2: Blend mode dropdown**
- Add: ComboBox per layer with blend mode options

**Task 3B.3.3: Layer visibility toggle**
- Add: Eye icon / checkbox per layer for visibility

**Task 3B.3.4: Memory indicator**
- Show total paint memory usage: `{layerCount} layers x {resolution}px = {totalMB} MB`

---

### Subphase 3C: New Paint Channels

#### Project 3C.1: Roughness and Metallic Paint Channels

**Task 3C.1.1: Add PaintChannel enum values**
- File: `KnobProject.cs`
- Add: `PaintChannel.Roughness = 6`, `PaintChannel.Metallic = 7`

**Task 3C.1.2: Dedicated roughness/metallic paint texture**
- The existing RGBA paint mask uses R=rust, G=wear, B=gunk, A=scratch — all 4 channels are taken
- Add: A second paint mask texture (`_paintMask2Rgba8`) where R=roughness, G=metallic, B=reserved, A=reserved
- Or: Use the layer system — roughness/metallic channels paint onto dedicated layers with a different interpretation in the shader

**Task 3C.1.3: Shader integration**
- File: `MetalPipelineManager.Shaders.cs`
- Add: Sample the second paint mask (or new channels from composited layer)
- Apply: Roughness channel overrides base roughness; metallic channel overrides base metallic
- Subtask: Add a new texture binding slot (index 8) for the second paint mask, or pack into the composited output

**Task 3C.1.4: Brush UI for new channels**
- File: MainWindow paint brush handlers
- Add: Roughness and Metallic options in the channel selector
- Add: Value slider (what roughness/metallic value to paint) — unlike rust/wear which are effect masks, roughness/metallic paint specific values

**Task 3C.1.5: Paint stamp pipeline for new channels**
- File: `MetalViewport.PaintResources.cs`
- Add: New pipeline states for roughness and metallic channels (similar to existing per-channel pipelines)
- Color write masks target the new texture's R and G channels respectively

---

## Verification Checklist (Phase 3 Complete)

- [ ] Paint mask resolution can be set to 512, 1024, 2048, or 4096
- [ ] Resolution change correctly reallocates and clears paint data
- [ ] GPU texture resizes to match new resolution
- [ ] Old projects load with default 1024 resolution and existing paint data intact
- [ ] Multiple paint layers can be created, reordered, deleted
- [ ] Each layer has independent opacity and blend mode
- [ ] Compositing produces correct visual results for all blend modes
- [ ] Roughness channel paints a specific roughness value onto the surface
- [ ] Metallic channel paints a specific metallic value onto the surface
- [ ] Performance remains interactive at 2048 resolution with 4+ layers

## Files Touched

| File | Nature of Change |
|------|-----------------|
| `KnobProject.cs` | Variable resolution, layer list, new channels, serialization |
| `MetalViewport.PaintResources.cs` | Dynamic texture sizing, new channel pipeline states |
| `MetalPipelineManager.Shaders.cs` | New paint mask sampling for roughness/metallic |
| `MetalViewport.OffscreenRender.cs` | Bind new paint textures |
| `MetalViewport.Shaders.cs` | Paint stamp shader for new channels |
| MainWindow paint inspector partials | Resolution control, layer properties, channel selector |

## New Files

| File | Purpose |
|------|---------|
| `KnobForge.Core/PaintLayerCompositor.cs` | CPU blend mode compositing engine |
| `KnobForge.Core/PaintLayer.cs` | Layer data model (or inline in KnobProject) |
