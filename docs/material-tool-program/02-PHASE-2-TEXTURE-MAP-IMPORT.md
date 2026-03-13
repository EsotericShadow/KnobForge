# Phase 2: Texture Map Import

## Phase Identity

- **Phase**: 2 of 6
- **Name**: Texture Map Import
- **Depends on**: Phase 1 (UV Infrastructure)
- **Unlocks**: Phase 4 (Multi-Material), Phase 5 (Texture Bake)
- **Risk**: Medium — new texture slots and shader branching, but isolated from existing rendering path
- **Milestone**: M2 — Users can assign albedo/normal/roughness/metallic maps and see them in real-time preview

## Why This Phase Exists

This is the single highest-impact user-facing feature in the entire program. Without it, every KnobForge model uses flat scalar materials — a single color, a single roughness value. With it, users can bring brushed steel textures, scanned patinas, photoreal surfaces. This is what transforms KnobForge from a procedural knob generator into a material tool.

## Subphases

### Subphase 2A: Data Model (MaterialNode + Serialization)

Add texture path properties to MaterialNode and ensure they serialize/deserialize with projects.

#### Project 2A.1: MaterialNode Texture Properties

**Task 2A.1.1: Add texture path properties**
- File: `KnobForge.Core/Scene/MaterialNode.cs`
- Add: `AlbedoMapPath`, `NormalMapPath`, `RoughnessMapPath`, `MetallicMapPath` (nullable strings)
- Add: Per-map tiling/offset: `AlbedoMapTileX/Y`, `AlbedoMapOffsetX/Y` (floats, default 1/1/0/0)
- Add: Same tiling/offset pattern for each map type
- Add: `NormalMapStrength` (float, 0-2, default 1.0) for normal map intensity control

**Task 2A.1.2: Add texture enable flags**
- File: `MaterialNode.cs`
- Add: Computed read-only properties: `HasAlbedoMap => !string.IsNullOrEmpty(AlbedoMapPath)`
- These are convenience accessors, not serialized separately

**Task 2A.1.3: Serialization support**
- File: `KnobForge.Core/KnobProject.cs` (or wherever project serialization lives)
- Change: Texture paths serialize as relative paths (relative to .knob file location)
- Add: On load, resolve relative paths to absolute. If file missing, clear path and log warning.
- Add: On save, convert absolute paths to relative.

**Task 2A.1.4: Project migration**
- Existing .knob files have no texture path fields. System.Text.Json deserialization naturally treats missing properties as null/default. Verify no crash on loading old projects.

---

### Subphase 2B: Texture Loading Infrastructure

Build a texture manager that loads image files, creates Metal textures, and caches them.

#### Project 2B.1: TextureManager Class

**Task 2B.1.1: Create TextureManager class**
- New file: `KnobForge.Rendering/GPU/TextureManager.cs`
- Responsibility: Load image files → SkiaSharp decode → RGBA8 byte array → Metal texture with mipmaps
- Interface:
  ```
  IntPtr GetOrLoadTexture(string filePath, IntPtr device) // returns Metal texture handle
  void InvalidatePath(string filePath) // marks for reload
  void DisposeAll() // cleanup
  ```

**Task 2B.1.2: Image loading via SkiaSharp**
- Supported formats: PNG, JPG, TIFF, BMP, WebP (all supported by SkiaSharp decode)
- Decode to `SKBitmap` with `SKColorType.Rgba8888`, `SKAlphaType.Premul`
- Handle non-square textures: accept as-is (Metal handles non-power-of-two)
- Handle large textures: clamp to 4096x4096 max (resize if larger)

**Task 2B.1.3: Metal texture creation**
- Pattern: Follow `EnsurePaintMaskTexture` in `MetalViewport.PaintResources.cs`
- Create `MTLTextureDescriptor` with RGBA8Unorm, mipmapped=true
- Upload pixel bytes via `ReplaceRegionMipmapLevelWithBytesBytesPerRow`
- Generate mipmaps via blit encoder `GenerateMipmapsForTexture`

**Task 2B.1.4: Cache invalidation**
- Cache key: absolute file path + file modification time
- On each frame (or on project property change), check if modification time changed
- If changed: reload texture, replace Metal texture handle
- If file deleted: release texture, return fallback

**Task 2B.1.5: Fallback textures**
- Create 1x1 white texture (for albedo/metallic fallback when no map assigned)
- Create 1x1 flat normal texture (RGB = 0.5, 0.5, 1.0) for normal map fallback
- Create 1x1 mid-gray texture for roughness fallback
- These are created once at TextureManager initialization

---

### Subphase 2C: GPU Pipeline Integration

Wire texture maps into the Metal rendering pipeline.

#### Project 2C.1: Texture Binding Expansion

**Task 2C.1.1: Add texture slots 4-7 to fragment shader signature**
- File: `MetalPipelineManager.Shaders.cs`
- Change: Add to fragment_main parameters:
  ```metal
  texture2d<float> albedoMap      [[texture(4)]],
  texture2d<float> normalMap      [[texture(5)]],
  texture2d<float> roughnessMap   [[texture(6)]],
  texture2d<float> metallicMap    [[texture(7)]]
  ```

**Task 2C.1.2: Add texture map uniforms to GpuUniforms**
- File: `MetalPipelineManager.Shaders.cs`
- Add to GpuUniforms struct:
  ```metal
  float4 textureMapFlags;              // x=albedo, y=normal, z=roughness, w=metallic (0 or 1)
  float4 textureMapTilingAlbedo;       // xy=tiling, zw=offset
  float4 textureMapTilingNormal;       // xy=tiling, zw=offset
  float4 textureMapTilingRoughMetal;   // xy=roughness tiling, zw=metallic tiling
  float4 textureMapParams;             // x=normal strength, yzw=reserved
  ```

**Task 2C.1.3: Bind textures in render loop**
- File: `MetalViewport.OffscreenRender.cs`
- Change: After existing texture binds (slots 0-3), add conditional binds for slots 4-7
- For each slot: if MaterialNode has a map path and TextureManager has a loaded texture, bind it. Otherwise bind the appropriate fallback texture.
- Subtask: Store TextureManager instance on MetalViewport
- Subtask: Wire MaterialNode property reads into the uniform upload

**Task 2C.1.4: Upload texture map uniforms**
- File: `MetalViewport.OffscreenRender.cs` (or the uniform building method)
- Change: Read MaterialNode texture flags and tiling params into the new GpuUniforms fields
- The existing uniform struct is already built per-frame; extend it with the new fields

#### Project 2C.2: Fragment Shader Material Sampling

**Task 2C.2.1: Add texture map sampling before weathering pass**
- File: `MetalPipelineManager.Shaders.cs`, in `fragment_main`
- Insert after UV setup, before weathering:
  ```metal
  float2 matUV = inVertex.texcoord;

  if (textureMapFlags.x > 0.5) {
      float2 aUV = matUV * tilingAlbedo.xy + tilingAlbedo.zw;
      float4 albedoSample = albedoMap.sample(linearSampler, aUV);
      baseColor = albedoSample.rgb;
  }
  ```
- Same pattern for roughness, metallic (single channel reads)
- Normal map: full TBN transform using worldTangentSign from vertex shader

**Task 2C.2.2: Normal map TBN application**
- File: `MetalPipelineManager.Shaders.cs`
- The shader already has `worldNormal` and `worldTangentSign`. Build TBN matrix:
  ```metal
  float3 T = normalize(inVertex.worldTangentSign.xyz);
  float3 B = cross(normal, T) * inVertex.worldTangentSign.w;
  float3 N = normal;
  ```
- Sample normal map, decode from [0,1] to [-1,1], apply strength, transform to world space

**Task 2C.2.3: Layering order verification**
- Texture maps apply to the base material BEFORE weathering/paint
- The weathering pass (rust/wear/gunk/scratch) paints OVER the textured surface
- The paint color coat paints OVER weathering
- Verify this order produces correct visual results

---

### Subphase 2D: Inspector UI

Build the UI for assigning texture maps to materials.

#### Project 2D.1: Texture Map Inspector Section

**Task 2D.1.1: Add "Texture Maps" section to material inspector**
- File: `MainWindow` material inspector partial (identify correct partial file)
- Add: A new collapsible section below color/metallic/roughness sliders
- Layout: For each map type (Albedo, Normal, Roughness, Metallic):
  - Label (map name)
  - File path display (TextBox, read-only)
  - "Browse..." button (opens file dialog)
  - "Clear" button (removes map)

**Task 2D.1.2: File dialog integration**
- Use Avalonia `StorageProvider.OpenFilePickerAsync` for file selection
- Filter: Image files (*.png, *.jpg, *.jpeg, *.tiff, *.bmp, *.webp)
- On selection: set MaterialNode path property, trigger TextureManager reload

**Task 2D.1.3: Tiling/offset controls**
- For each map: collapsible sub-section with Tile X, Tile Y, Offset X, Offset Y sliders
- Tile range: 0.1 to 10.0 (default 1.0)
- Offset range: -1.0 to 1.0 (default 0.0)

**Task 2D.1.4: Normal map strength slider**
- Range: 0.0 to 2.0 (default 1.0)
- Appears only when normal map is assigned

**Task 2D.1.5: Texture preview thumbnails (optional enhancement)**
- Show a small thumbnail of each assigned texture next to the path
- Generate from SkiaSharp decode, resize to ~48x48

---

## Verification Checklist (Phase 2 Complete)

- [ ] MaterialNode has 4 texture path properties that serialize/deserialize correctly
- [ ] TextureManager loads PNG/JPG/TIFF to Metal textures with mipmaps
- [ ] Fallback textures prevent shader sampling errors when no map assigned
- [ ] Fragment shader samples albedo map and replaces base color correctly
- [ ] Fragment shader samples normal map with TBN transform
- [ ] Fragment shader samples roughness and metallic maps
- [ ] Texture maps render correctly in real-time viewport preview
- [ ] Texture maps render correctly in export pipeline (spritesheet output)
- [ ] Weathering paint layers correctly overlay on top of texture-mapped materials
- [ ] Old projects without texture maps load and render identically to before
- [ ] UI allows browsing, assigning, and clearing texture maps
- [ ] Tiling and offset controls work in real-time

## Files Touched

| File | Nature of Change |
|------|-----------------|
| `MaterialNode.cs` | New properties: 4 texture paths, tiling/offset params, normal strength |
| `KnobProject.cs` | Relative path serialization, migration handling |
| `MetalPipelineManager.Shaders.cs` | New texture slots 4-7, GpuUniforms extension, sampling code |
| `MetalViewport.OffscreenRender.cs` | Texture binding for new slots, uniform upload |
| `MetalPipelineManager.cs` | Potentially: ensure pipeline descriptor allows 8+ textures |
| MainWindow material inspector partial | New UI section for texture maps |

## New Files

| File | Purpose |
|------|---------|
| `KnobForge.Rendering/GPU/TextureManager.cs` | Texture load, cache, GPU upload, dispose |
