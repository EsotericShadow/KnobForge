# Codex Implementation Prompt — Phase 5: Texture Bake Pipeline

## Your Role

You are implementing Phase 5 of the KnobForge Material Tool Transformation. Your job is to add a CPU texture bake pipeline that evaluates the full material chain (base material + imported textures + paint layers + weathering + roughness/metallic paint) and writes the results as standalone PBR texture map PNG files. Work incrementally — complete each subphase, verify it compiles and runs, then move to the next. Do not skip verification steps. Do not refactor unrelated code.

## Project Context

KnobForge is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs and UI components for audio plugins. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–4 are complete:
- **Phase 1**: UV infrastructure — vertex UVs flow through the pipeline.
- **Phase 2**: Texture map import — PBR textures (albedo, normal, roughness, metallic) on slots 4–7.
- **Phase 3**: Paint system upgrades — variable resolution paint masks, layer compositing, roughness/metallic paint channels on slot 8.
- **Phase 4**: Multi-material support — per-SubMesh draw calls with per-material textures for imported GLBs.

## What Phase 5 Does

Users build materials in KnobForge (base properties + imported textures + paint weathering). They need to get those materials out as standalone PNG texture maps for use in game engines, other 3D apps, web renderers. The spritesheet export captures rendered frames with lighting — this phase exports the raw material properties *without* lighting.

The bake operates in **paint UV space** (the legacy planar projection, 0–1 range). For each texel in the output image, it evaluates the full material chain and writes the result. The output textures are:

- `{BaseName}_albedo.png` — final base color after all weathering/paint
- `{BaseName}_roughness.png` — final roughness (grayscale)
- `{BaseName}_metallic.png` — final metallic (grayscale)
- `{BaseName}_normal.png` — composed normal map (tangent space RGB)
- `{BaseName}_material.json` — metadata describing the bake

## ⚠️ CRITICAL: Exact GPU-CPU Fidelity

The CPU bake MUST produce output that matches the GPU viewport appearance. The material evaluation chain in the fragment shader (lines 643–721 of MetalPipelineManager.Shaders.cs) is the ground truth. The CPU implementation must replicate it exactly, including the procedural noise functions.

## Current State (Verified)

### Fragment Shader Material Chain (MetalPipelineManager.Shaders.cs, lines 643–721)

This is the EXACT algorithm the CPU baker must replicate. The chain runs in paint UV space.

**Noise functions** (lines 169–186):
```metal
static inline float Hash21(float2 p)
{
    p = fract(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

static inline float ValueNoise2(float2 p)
{
    float2 i = floor(p);
    float2 f = fract(p);
    float a = Hash21(i);
    float b = Hash21(i + float2(1.0, 0.0));
    float c = Hash21(i + float2(0.0, 1.0));
    float d = Hash21(i + float2(1.0, 1.0));
    float2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}
```

**Material chain** (summarized from lines 643–721):

```
Input per texel:
  paintUv = (u, v) in [0,1] range
  baseColor = material base color (Vector3, from MaterialNode.BaseColor)
  roughness = material roughness (float, from MaterialNode.Roughness)
  metallic = material metallic (float, from MaterialNode.Metallic)

  If material has texture maps (albedo/roughness/metallic), sample them at the MATERIAL UV.
  NOTE: For CPU bake in paint UV space, material texture sampling is complex because
  material UVs (inVertex.texcoord) differ from paint UVs. For the initial CPU bake,
  sample material textures at paintUv as a reasonable approximation. The exact mapping
  from paint UV to mesh UV requires mesh topology and is deferred to GPU bake (Phase 5C).

Step 1 — Color paint layer:
  colorPaintSample = sample paintColor texture at paintUv (premultiplied RGBA)
  colorPaintMask = clamp(colorPaintSample.w, 0, 1)
  if colorPaintMask > 1e-5:
      colorPaintBase = clamp(colorPaintSample.xyz / colorPaintMask, 0, 1)  // un-premultiply
  baseColor = lerp(baseColor, colorPaintBase, colorPaintMask)
  paintCoatBlend = smoothstep(0.0, 0.85, colorPaintMask)
  roughness = lerp(roughness, paintCoatRoughness, paintCoatBlend)  // default 0.56
  metallic = lerp(metallic, paintCoatMetallic, paintCoatBlend)     // default 0.02

Step 2 — Weathering:
  paintSample = sample paintMask at paintUv → (R:rust, G:wear, B:gunk, A:scratch)
  darknessGain = lerp(0.45, 1.45, brushDarkness)  // default brushDarkness=0.58
  rustRaw = clamp(paintSample.x, 0, 1)
  wearRaw = clamp(paintSample.y, 0, 1)
  gunkRaw = clamp(paintSample.z, 0, 1)
  scratchRaw = clamp(paintSample.w, 0, 1)

  // Procedural rust splotch
  rustNoiseA = ValueNoise2(paintUv * (192, 217) + (11.3, 6.7))
  rustNoiseB = ValueNoise2(paintUv * (67, 59) + (41.1, 13.5))
  rustSplotch = smoothstep(0.32, 0.90, rustNoiseA * 0.72 + rustNoiseB * 0.58)

  rustStrength = lerp(0.30, 1.00, rustAmount)
  wearStrength = lerp(0.15, 0.70, wearAmount)
  gunkStrength = lerp(0.35, 1.20, gunkAmount)
  scratchStrength = lerp(0.30, 1.00, wearAmount)  // NOTE: uses wearAmount, not scratchAmount

  rustMask = clamp(rustRaw * rustSplotch * darknessGain * rustStrength, 0, 1)
  wearMask = clamp(wearRaw * lerp(0.30, 0.80, brushDarkness) * wearStrength, 0, 1)
  gunkMask = clamp(gunkRaw * lerp(0.55, 1.65, brushDarkness) * gunkStrength, 0, 1)
  scratchMask = clamp(scratchRaw * lerp(0.45, 1.00, brushDarkness) * scratchStrength, 0, 1)

  // Rust color variation
  rustHue = ValueNoise2(paintUv * (103, 97) + (3.1, 17.2))
  rustDark = (0.23, 0.08, 0.04)
  rustMid = (0.46, 0.17, 0.07)
  rustOrange = (0.71, 0.29, 0.09)
  rustColor = lerp(lerp(rustDark, rustMid, clamp(rustHue * 1.25, 0, 1)),
                   rustOrange, clamp((rustHue - 0.35) / 0.65, 0, 1))
  gunkColor = (0.02, 0.02, 0.018)
  wearColor = lerp(baseColor, (0.80, 0.79, 0.76), 0.45)

  baseColor = lerp(baseColor, rustColor, clamp(rustMask * 0.88, 0, 1))
  baseColor = lerp(baseColor, gunkColor, clamp(gunkMask * 0.96, 0, 1))
  baseColor = lerp(baseColor, wearColor, clamp(wearMask * 0.24, 0, 1))
  grimeDarken = clamp((rustMask * 0.18 + gunkMask * 0.55) * (0.25 + 0.75 * brushDarkness), 0, 0.85)
  baseColor *= (1.0 - grimeDarken)
  scratchReveal = clamp(scratchMask, 0, 1)
  baseColor = lerp(baseColor, scratchExposeColor, scratchReveal)

  roughness = clamp(roughness + rustMask * 0.34 + gunkMask * 0.62 - wearMask * 0.05, 0.04, 1)
  metallic = clamp(metallic - rustMask * 0.62 - gunkMask * 0.30, 0, 1)
  roughness = lerp(roughness, scratchExposeRoughness, scratchReveal)   // default 0.20
  metallic = lerp(metallic, scratchExposeMetallic, scratchReveal)      // default 0.92

Step 3 — Roughness/metallic paint (paintMask2):
  paintMask2Sample = sample paintMask2 at paintUv → (R:roughTarget, G:metalTarget, B:roughAlpha, A:metalAlpha)
  roughness = lerp(roughness, clamp(paintMask2Sample.x, 0, 1), clamp(paintMask2Sample.z, 0, 1))
  metallic = lerp(metallic, clamp(paintMask2Sample.y, 0, 1), clamp(paintMask2Sample.w, 0, 1))

Output:
  finalAlbedo = baseColor (Vector3, 0–1)
  finalRoughness = roughness (float, 0.04–1)
  finalMetallic = metallic (float, 0–1)
```

### Paint Mask Access (KnobProject.cs)

```csharp
// Bilinear sampling at UV coordinates — returns normalized 0-1 RGBA
public Vector4 SamplePaintMaskBilinear(float u, float v)
public Vector4 SamplePaintMask2Bilinear(float u, float v)

// Raw pixel buffers
public byte[] GetPaintMaskRgba8()       // R=rust, G=wear, B=gunk, A=scratch
public byte[] GetPaintColorRgba8()      // Premultiplied RGBA color paint
public byte[] GetPaintMask2Rgba8()      // R=roughTarget, G=metalTarget, B=roughAlpha, A=metalAlpha
public int PaintMaskSize { get; }       // 512, 1024, 2048, or 4096
```

### Weathering Parameters (KnobProject properties)

```csharp
project.BrushDarkness           // float, default 0.58
project.PaintCoatMetallic       // float, default 0.02
project.PaintCoatRoughness      // float, default 0.56
project.ScratchExposeColor      // Vector3, default (0.88, 0.88, 0.90)
project.ScratchExposeMetallic   // float, default 0.92
project.ScratchExposeRoughness  // float, default 0.20
```

Per-material weathering amounts:
```csharp
materialNode.RustAmount         // float, 0-1
materialNode.WearAmount         // float, 0-1
materialNode.GunkAmount         // float, 0-1
```

### Texture Map Loading (TextureManager.cs)

`TextureManager.LoadImageToRgba8()` is **private**. For CPU bake, you'll need to load source images directly via SkiaSharp:

```csharp
using SKBitmap? bitmap = SKBitmap.Decode(filePath);
if (bitmap != null)
{
    using SKBitmap rgba = bitmap.Copy(SKColorType.Rgba8888);
    byte[] pixels = rgba.GetPixelSpan().ToArray();
    // pixels is RGBA8, width=rgba.Width, height=rgba.Height
}
```

Material texture paths are on `MaterialNode`:
```csharp
materialNode.AlbedoMapPath      // string? — path to albedo PNG/JPG
materialNode.NormalMapPath       // string?
materialNode.RoughnessMapPath    // string?
materialNode.MetallicMapPath     // string?
materialNode.NormalMapStrength   // float, 0-2, default 1.0
```

### PaintLayerCompositor (PaintLayerCompositor.cs)

Already fully CPU-based. Blend modes: Normal, Multiply, Screen, Overlay, Add. The composited output is what `GetPaintMaskRgba8()` etc. return — the bake can use the composited buffers directly, no need to re-composite.

### Existing Export UI (RenderSettingsWindow.axaml)

The export window has a 2-column layout: settings panel (left) + preview (right). The settings panel is a `StackPanel` inside a `ScrollViewer` with sections for presets, render settings, camera/views, and output. Phase 5 adds a "Texture Bake" section in this panel.

### Serialization

System.Text.Json, `knobforge.project.v1` format. Missing properties default to null/default. Additive-safe.

## Execution Order

### SUBPHASE 5A: Bake Engine (do this FIRST)

**Goal**: A `TextureBaker` class that evaluates the material chain on CPU and writes PBR texture maps as PNG files. No UI yet.

**Step 1: Create TextureBakeSettings and BakeResult**

- New file: `KnobForge.Core/Export/TextureBakeSettings.cs`
  ```csharp
  public sealed class TextureBakeSettings
  {
      public int Resolution { get; set; } = 1024;  // 256, 512, 1024, 2048, 4096
      public bool BakeAlbedo { get; set; } = true;
      public bool BakeNormal { get; set; } = true;
      public bool BakeRoughness { get; set; } = true;
      public bool BakeMetallic { get; set; } = true;
      public string OutputFolder { get; set; } = "";
      public string BaseName { get; set; } = "bake";
  }
  ```

- New file: `KnobForge.Core/Export/BakeResult.cs`
  ```csharp
  public sealed class BakeResult
  {
      public string? AlbedoPath { get; set; }
      public string? NormalPath { get; set; }
      public string? RoughnessPath { get; set; }
      public string? MetallicPath { get; set; }
      public string? MetadataPath { get; set; }
      public int Resolution { get; set; }
      public bool Success { get; set; }
      public string? Error { get; set; }
  }
  ```

**Step 2: Create TextureBaker class with noise functions**

- New file: `KnobForge.Rendering/TextureBaker.cs`
- First, implement the C# equivalents of the MSL noise functions. These MUST produce identical output:

  ```csharp
  private static float Hash21(float x, float y)
  {
      // Exact port of MSL:
      //   p = fract(p * float2(123.34, 456.21));
      //   p += dot(p, p + 45.32);      ← dot returns scalar, += broadcasts to both x,y
      //   return fract(p.x * p.y);
      float px = Fract(x * 123.34f);
      float py = Fract(y * 456.21f);
      // dot(p, p + 45.32) = px*(px+45.32) + py*(py+45.32)
      float d = px * (px + 45.32f) + py * (py + 45.32f);
      px += d;
      py += d;
      return Fract(px * py);
  }

  private static float ValueNoise2(float x, float y)
  {
      float ix = MathF.Floor(x);
      float iy = MathF.Floor(y);
      float fx = x - ix;
      float fy = y - iy;
      float a = Hash21(ix, iy);
      float b = Hash21(ix + 1f, iy);
      float c = Hash21(ix, iy + 1f);
      float d = Hash21(ix + 1f, iy + 1f);
      float ux = fx * fx * (3f - 2f * fx);
      float uy = fy * fy * (3f - 2f * fy);
      float ab = a + (b - a) * ux;
      float cd = c + (d - c) * ux;
      return ab + (cd - ab) * uy;
  }

  private static float Fract(float v) => v - MathF.Floor(v);
  ```

  **IMPORTANT**: The MSL `fract()` returns `v - floor(v)`. The MSL `mix(a,b,t)` = `a + (b - a) * t`. The MSL `smoothstep(edge0, edge1, x)` = `t*t*(3-2*t)` where `t = clamp((x - edge0) / (edge1 - edge0), 0, 1)`. Implement C# versions of `smoothstep`, `clamp`, `lerp` (or use `float.Lerp` in .NET 8).

**Step 3: Implement the material evaluation function**

  ```csharp
  /// <summary>
  /// Evaluates the full material chain at a single paint-UV texel.
  /// Replicates fragment shader lines 643-721 exactly.
  /// </summary>
  private static void EvaluateMaterialAtTexel(
      float u, float v,
      Vector3 baseMaterialColor, float baseMaterialRoughness, float baseMaterialMetallic,
      // Paint mask samples (pre-fetched or sampled inline)
      Vector4 paintSample,        // R=rust, G=wear, B=gunk, A=scratch
      Vector4 colorPaintSample,   // premultiplied RGBA
      Vector4 paintMask2Sample,   // R=roughTarget, G=metalTarget, B=roughAlpha, A=metalAlpha
      // Material texture samples (or base values if no texture)
      Vector3? textureAlbedo,
      float? textureRoughness,
      float? textureMetallic,
      // Weathering parameters
      float rustAmount, float wearAmount, float gunkAmount,
      float brushDarkness, float paintCoatRoughness, float paintCoatMetallic,
      Vector3 scratchExposeColor, float scratchExposeRoughness, float scratchExposeMetallic,
      // Outputs
      out Vector3 finalAlbedo, out float finalRoughness, out float finalMetallic)
  ```

  Implement the EXACT algorithm from the "Material chain" section above. Every constant, every lerp, every noise call must match.

**Step 4: Implement the bake loop**

  ```csharp
  public BakeResult Bake(
      KnobProject project,
      MaterialNode material,
      TextureBakeSettings settings,
      IProgress<float>? progress = null,
      CancellationToken ct = default)
  ```

  Algorithm:
  1. Allocate output buffers: `byte[resolution * resolution * 4]` for each enabled map
  2. Load material texture maps (if any) via SkiaSharp — hold as `byte[]` + width + height
  3. For each texel (y=0..resolution-1, x=0..resolution-1):
     - `u = (x + 0.5f) / resolution`
     - `v = (y + 0.5f) / resolution`
     - Sample paint masks: `project.SamplePaintMaskBilinear(u, v)`, `SamplePaintMask2Bilinear(u, v)`
     - Sample paint color: bilinear sample of `project.GetPaintColorRgba8()` at (u, v)
     - Sample material textures at (u, v) if loaded
     - Call `EvaluateMaterialAtTexel(...)` with all inputs
     - Write results to output buffers
  4. Report progress: `progress?.Report((float)y / resolution)` every N rows
  5. Check cancellation: `ct.ThrowIfCancellationRequested()` every N rows

**Step 5: Write PNG output via SkiaSharp**

  For each enabled map:
  ```csharp
  using var bitmap = new SKBitmap(resolution, resolution, SKColorType.Rgba8888, SKAlphaType.Unpremul);
  // Copy buffer to bitmap pixels
  var span = bitmap.GetPixelSpan();
  outputBuffer.CopyTo(span);
  using var image = SKImage.FromBitmap(bitmap);
  using var data = image.Encode(SKEncodedImageFormat.Png, 100);
  using var fileStream = File.OpenWrite(outputPath);
  data.SaveTo(fileStream);
  ```

  For single-channel maps (roughness, metallic), write as grayscale: R=G=B=value, A=255.

**Step 6: Write material metadata JSON**

  After all maps are written:
  ```json
  {
    "version": 1,
    "resolution": 2048,
    "maps": {
      "albedo": "MyKnob_albedo.png",
      "normal": "MyKnob_normal.png",
      "roughness": "MyKnob_roughness.png",
      "metallic": "MyKnob_metallic.png"
    },
    "workflow": "metallic-roughness",
    "normalSpace": "tangent",
    "source": "KnobForge"
  }
  ```

**Step 7: Normal map composition (simplified)**

  For the normal map bake:
  - If the material has an imported normal map, start from that
  - If no imported normal, output a flat normal (0.5, 0.5, 1.0) — default tangent-space "no perturbation"
  - Scratch displacement: where scratchMask > 0, perturb the normal slightly (simple edge-detect of scratch mask to create bump)
  - This is a simplified approximation — exact normal composition matching the GPU spiral normal detail is not required for the first pass

**Step 8: Paint color bilinear sampling helper**

  `KnobProject` has `SamplePaintMaskBilinear` and `SamplePaintMask2Bilinear`, but you also need to sample the **paint color** texture bilinearly. Either:
  - Add `SamplePaintColorBilinear(float u, float v)` to KnobProject following the same pattern, OR
  - Implement bilinear sampling directly in TextureBaker against `project.GetPaintColorRgba8()`

  The bilinear sampling pattern (from the existing SamplePaintMaskBilinear):
  ```
  px = u * size - 0.5,  py = v * size - 0.5
  ix = floor(px),  iy = floor(py)
  fx = px - ix,  fy = py - iy
  clamp ix, iy to [0, size-1]
  sample 4 neighbors, bilinear blend with fx, fy
  ```

**VERIFY (BUILD GATE):** Build and run. Call `TextureBaker.Bake()` programmatically (add a temporary test button or call from a debug menu) with a project that has paint data. Open the output PNGs in any image viewer. Verify:
- Albedo shows base color with rust/gunk/wear/scratch weathering applied
- Roughness shows variation where paint and weathering are present
- Metallic shows variation (low where rust/gunk is heavy)
- Normal shows a flat blue map (or with scratch bumps if implemented)
- Files are valid PNGs readable in external tools

---

### SUBPHASE 5B: Bake UI (after 5A is verified)

**Goal**: Add bake controls to the export window so users can configure and execute bakes.

**Step 1: Add "Texture Bake" section to RenderSettingsWindow**

- File: `RenderSettingsWindow.axaml`
- Add a new `Border` section in the SettingsPanel (after the existing output section):
  ```xml
  <Border Background="#1E232A" CornerRadius="8" Padding="16" Margin="0,8,0,0">
      <StackPanel Spacing="8">
          <TextBlock Text="Texture Bake" FontWeight="SemiBold" FontSize="14"/>
          <TextBlock Text="Export composed PBR texture maps" FontSize="11" Foreground="#A9B4BF"/>
          <!-- Resolution dropdown -->
          <!-- Map checkboxes: Albedo, Normal, Roughness, Metallic -->
          <!-- Output folder browse -->
          <!-- Base name text field -->
          <!-- Bake button + progress bar -->
      </StackPanel>
  </Border>
  ```

**Step 2: Resolution dropdown**

- Options: 256, 512, 1024, 2048, 4096
- Default: 1024
- Show estimated memory: `"{res}×{res} × {mapCount} maps ≈ {mb} MB"`

**Step 3: Map checkboxes**

- Four checkboxes: Albedo, Normal, Roughness, Metallic (all checked by default)

**Step 4: Output folder**

- Default: same folder as the project file (if saved) or Desktop
- Browse button opens a folder picker
- Show the selected path as a truncated text block

**Step 5: Base name**

- Text field, default from project name (or "bake")
- Preview: show one example filename like `"{baseName}_albedo.png"`

**Step 6: Bake button with progress**

- "Bake Textures" button triggers the bake on `Task.Run()` (background thread)
- Progress bar shows 0–100%
- Status text: "Baking albedo..." → "Baking roughness..." etc.
- Cancel button (sets CancellationTokenSource)
- On completion: show summary ("4 maps exported to {folder}") and "Open Folder" button (call `Process.Start("open", folderPath)` on macOS)

**Step 7: Wire to TextureBaker**

  ```csharp
  private async void OnBakeClicked(object? sender, RoutedEventArgs e)
  {
      var settings = new TextureBakeSettings
      {
          Resolution = selectedResolution,
          BakeAlbedo = albedoCheckbox.IsChecked == true,
          // ... etc
          OutputFolder = outputFolder,
          BaseName = baseName
      };
      _bakeCts = new CancellationTokenSource();
      var progress = new Progress<float>(p => bakeProgressBar.Value = p * 100);
      try
      {
          BakeResult result = await Task.Run(() =>
              new TextureBaker().Bake(_project!, _material!, settings, progress, _bakeCts.Token));
          // Show success summary
      }
      catch (OperationCanceledException)
      {
          // Show "Bake cancelled"
      }
      catch (Exception ex)
      {
          // Show error
      }
  }
  ```

**Step 8: Multi-material bake support**

- If the project has multiple materials (Phase 4), add a material dropdown to select which material to bake
- Or: "Bake All Materials" button that iterates all MaterialNodes and bakes each with `{baseName}_{materialName}_{map}.png`
- Single-material projects skip the material selector

**VERIFY (BUILD GATE):** Build and run. Open the export window. Verify the "Texture Bake" section appears with all controls. Set resolution to 512, enable all maps, choose an output folder, click "Bake Textures". Verify:
- Progress bar advances
- PNG files appear in the output folder
- Output matches what you'd see in the viewport
- "Open Folder" reveals the files in Finder
- Cancel works during a long bake (try 4096)
- Baking works for both single-material and multi-material projects

---

## Critical Constraints

1. **ZERO VISUAL REGRESSION on existing features.** The bake pipeline is a new parallel path — it must not modify any existing render, export, or paint logic.
2. **CPU-GPU fidelity.** The CPU material evaluation must match the GPU fragment shader. All constants, noise functions, and blending order must be identical.
3. **No new NuGet dependencies.** Use SkiaSharp (already available) for all image I/O.
4. **Background thread safety.** The bake runs on a background thread. All KnobProject/MaterialNode property reads must be safe. Paint mask data should be snapshotted before bake starts if there's any risk of concurrent modification.
5. **Build after every task.** If it doesn't compile, fix it before moving on.

## What NOT to Do

- Do not add GPU bake / UV-space rasterization (that's Phase 5C, deferred)
- Do not add EXR export (deferred to post-M5)
- Do not add AO bake (nice-to-have, skip if the implementation is getting long)
- Do not add a node graph (that's Phase 7)
- Do not modify the fragment shader
- Do not modify the existing spritesheet export pipeline
- Do not introduce new NuGet dependencies
- Do not refactor unrelated code

## After Phase 5 Is Complete

Update `docs/material-tool-program/00-PROGRAM.md` — change Phase 5 status from "Not started" to "Complete". Phase 6 (Inspector Control Overhaul) and Phase 7 (Node Graph) remain.
