# Phase 5: Texture Bake Pipeline

## Phase Identity

- **Phase**: 5 of 6
- **Name**: Texture Bake / Export Pipeline
- **Depends on**: Phase 2 (texture maps), Phase 3 (composited paint layers), Phase 4 (multi-material)
- **Unlocks**: Phase 7 (node graph output feeds into bake)
- **Risk**: Low — new pipeline alongside existing export, no modification of existing code paths
- **Milestone**: M5 — Composed texture maps exported as standalone image files

## Why This Phase Exists

Users who build materials in KnobForge need to get those materials out as image files for use in other tools — game engines, other 3D apps, web renderers. The spritesheet export captures rendered frames, but doesn't give you the texture maps themselves. This phase adds the ability to bake the full material evaluation (base textures + weathering paint + roughness/metallic overrides) into exportable PNG/EXR files.

## Subphases

### Subphase 5A: Bake Engine

#### Project 5A.1: TextureBaker Class

**Task 5A.1.1: Create TextureBaker class**
- New file: `KnobForge.Rendering/TextureBaker.cs`
- Responsibility: Evaluate the full material chain at every texel and write results to output images
- Interface:
  ```csharp
  public sealed class TextureBaker
  {
      public BakeResult Bake(
          KnobProject project,
          MaterialNode material,
          TextureBakeSettings settings,
          IProgress<float>? progress,
          CancellationToken ct);
  }
  ```

**Task 5A.1.2: BakeResult and BakeSettings**
- New: `TextureBakeSettings` class:
  - `int Resolution` (256, 512, 1024, 2048, 4096 — independent of paint mask resolution)
  - `bool BakeAlbedo, BakeNormal, BakeRoughness, BakeMetallic, BakeAO`
  - `TextureBakeFormat Format` (Png8, Png16, Exr32)
  - `string OutputFolder`
  - `string BaseName`
- New: `BakeResult` class:
  - `string? AlbedoPath, NormalPath, RoughnessPath, MetallicPath, AOPath`
  - `int Resolution`

**Task 5A.1.3: CPU material evaluation**
- Implement the same material evaluation logic as the fragment shader, but in C#:
  1. Start with base material (color, metallic, roughness) — from scalars or sampled from texture maps
  2. Apply weathering: sample composited paint mask, blend rust/wear/gunk/scratch effects
  3. Apply paint color coat
  4. Apply roughness/metallic paint channels
  5. Output: separated albedo, roughness, metallic values per texel
- Note: This duplicates shader logic on CPU. The alternative (GPU bake) is faster but requires a UV-space render pass, which is significantly more complex. CPU bake is the pragmatic first implementation.

**Task 5A.1.4: Normal map composition**
- Input: imported normal map (if any) + spiral normal detail + scratch displacement
- Composition: blend normals in tangent space
- Output: combined normal map in standard tangent-space format (RGB = XYZ)

**Task 5A.1.5: AO approximation from paint data**
- Gunk paint channel naturally represents crevice/cavity darkening
- Approximate AO by using the gunk mask as a starting point, with some blur/spread
- This is a convenience output, not a true ray-traced AO

#### Project 5A.2: Image Writing

**Task 5A.2.1: PNG 8-bit output via SkiaSharp**
- Encode each bake target as RGBA8 PNG using `SKBitmap.Encode(SKEncodedImageFormat.Png)`
- For single-channel maps (roughness, metallic): write as grayscale (R=G=B=value, A=255)

**Task 5A.2.2: PNG 16-bit output**
- Use SkiaSharp's `SKPngEncoderOptions` for 16-bit depth
- Or: manual PNG writing for 16-bit per channel (SkiaSharp support may be limited)
- Fallback: 8-bit PNG if 16-bit encoding not available

**Task 5A.2.3: EXR 32-bit float output (optional/future)**
- Requires an EXR encoder library (not built into SkiaSharp)
- Options: TinyEXR via P/Invoke, or a C# EXR writer
- Defer to post-M5 if complexity is too high; 8-bit PNG covers most use cases

**Task 5A.2.4: File naming convention**
- `{BaseName}_albedo.png`
- `{BaseName}_normal.png`
- `{BaseName}_roughness.png`
- `{BaseName}_metallic.png`
- `{BaseName}_ao.png`

---

### Subphase 5B: Bake UI

#### Project 5B.1: Bake Dialog

**Task 5B.1.1: Add "Bake Textures" button to export window**
- File: `RenderSettingsWindow.axaml` / `.axaml.cs`
- Add: A separate section or button that opens a bake dialog
- Alternative: Add a "Bake" tab alongside the existing spritesheet export controls

**Task 5B.1.2: Bake settings UI**
- Resolution dropdown: 256, 512, 1024, 2048, 4096
- Checkboxes: Albedo, Normal, Roughness, Metallic, AO
- Format dropdown: PNG 8-bit, PNG 16-bit
- Output folder: Browse button + path display
- Base name: Text field (default from project name)

**Task 5B.1.3: Bake execution with progress**
- "Bake" button starts the bake on a background thread
- Progress bar shows completion percentage
- Status text shows current map being baked
- Cancel button for long bakes (4096 resolution)

**Task 5B.1.4: Post-bake summary**
- Show: list of files written, total size, resolution
- Option: "Open folder" to reveal output in Finder

#### Project 5B.2: Metadata Export

**Task 5B.2.1: Material metadata JSON**
- Write `{BaseName}_material.json` alongside baked textures
- Contents:
  ```json
  {
    "version": 1,
    "resolution": 2048,
    "maps": {
      "albedo": "KnobA_albedo.png",
      "normal": "KnobA_normal.png",
      "roughness": "KnobA_roughness.png",
      "metallic": "KnobA_metallic.png"
    },
    "workflow": "metallic-roughness",
    "normalSpace": "tangent",
    "source": "KnobForge"
  }
  ```
- Purpose: Enables automated import into game engines and other tools

---

### Subphase 5C: GPU Bake Path (Future Optimization)

#### Project 5C.1: UV-Space Rasterization (Deferred)

**Task 5C.1.1: Design UV-space render pass**
- Concept: Render the mesh "unfolded" — vertex positions are replaced with UV coordinates, and the fragment shader writes material properties instead of lit color
- This produces baked textures at GPU speed (~100x faster than CPU evaluation)
- Requires: A separate render pipeline state with a UV-space vertex shader and material-output fragment shader

**Task 5C.1.2: Implementation (deferred to post-M5)**
- Priority is getting the CPU bake working correctly first
- GPU bake is an optimization for users who bake frequently at high resolution

---

## Verification Checklist (Phase 5 Complete)

- [ ] CPU material evaluator produces correct albedo output matching viewport appearance
- [ ] Normal map composition correctly blends imported normals, spiral detail, and scratches
- [ ] Roughness and metallic bake outputs reflect all material modifications (texture maps + paint)
- [ ] AO approximation produces reasonable ambient occlusion from paint data
- [ ] PNG 8-bit output is correct and loadable in external tools
- [ ] File naming follows convention and metadata JSON is valid
- [ ] Bake UI allows configuration of all settings
- [ ] Progress reporting works for long bakes
- [ ] Cancel functionality works without leaving partial files
- [ ] Baked textures look correct when reimported into Blender/Unity/Unreal as verification

## Files Touched

| File | Nature of Change |
|------|-----------------|
| `RenderSettingsWindow.axaml` / `.axaml.cs` | Bake UI section or button |
| Possibly MainWindow | If bake dialog is separate from export window |

## New Files

| File | Purpose |
|------|---------|
| `KnobForge.Rendering/TextureBaker.cs` | CPU material evaluation and bake orchestration |
| `KnobForge.Core/Export/TextureBakeSettings.cs` | Bake configuration data model |
| `KnobForge.Core/Export/BakeResult.cs` | Bake output descriptor |
