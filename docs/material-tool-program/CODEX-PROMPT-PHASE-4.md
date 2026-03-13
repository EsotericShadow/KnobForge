# Codex Implementation Prompt — Phase 4: Multi-Material Support

## Your Role

You are implementing Phase 4 of the KnobForge Material Tool Transformation. Your job is to add multi-material support for imported GLB meshes — parsing per-primitive material indices, extracting embedded textures, creating per-SubMesh draw calls, and showing a material list in the inspector. Work incrementally — complete each subphase, verify it compiles and runs, then move to the next. Do not skip verification steps. Do not refactor unrelated code.

## Project Context

KnobForge is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs and UI components for audio plugins. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–3 are complete:
- **Phase 1**: UV infrastructure — vertex UVs flow through the pipeline, GLB TEXCOORD_0 is read, procedural knob geometry has proper UVs.
- **Phase 2**: Texture map import — TextureManager loads/caches PBR textures (albedo, normal, roughness, metallic) on slots 4–7 with real-time preview.
- **Phase 3**: Paint system upgrades — variable resolution paint masks, true layer compositing with blend modes, roughness/metallic paint channels on slot 8.

## ⚠️ CRITICAL ARCHITECTURAL CONSTRAINTS

### TWO SEPARATE UV SYSTEMS (do NOT change)

1. **Paint UVs** (legacy planar projection): `localXY / (referenceRadius * 2.0) + 0.5` — computed at runtime in the shader. Used for paint mask sampling, scratch carving, weathering.
2. **Material UVs** (`inVertex.texcoord`): vertex attribute UVs from mesh build time. Used for PBR texture map sampling.

### EXISTING PART-MATERIALS vs NEW MULTI-MATERIAL

These are two separate systems that do NOT conflict:
- **Part-materials** (existing): A shader-level 3-region split (top/bevel/side) for procedural knobs. Controlled via `MaterialNode.PartMaterialsEnabled`, `TopBaseColor`, `BevelMetallic`, etc. Uses region detection in the shader.
- **Multi-material** (new in Phase 4): A per-SubMesh split for imported GLB meshes. Each glTF primitive has a material index pointing to a separate material definition. Uses multiple draw calls with different uniforms/textures.

Part-materials controls should show only for procedural knobs. Multi-material list should show only for imported multi-material meshes.

### DO NOT CHANGE MetalVertex

`MetalVertex` is 48 bytes: `Vector3 Position`, `Vector3 Normal`, `Vector4 Tangent`, `Vector2 Texcoord`. Do not add material index to the vertex — multi-material is handled via SubMesh index ranges, not per-vertex attributes.

## Current State (Verified)

### MetalVertex (MetalMesh.Core.cs)

```csharp
[StructLayout(LayoutKind.Sequential)]
public readonly struct MetalVertex
{
    public const int ExpectedSizeInBytes = 48;
    public Vector3 Position { get; init; }
    public Vector3 Normal { get; init; }
    public Vector4 Tangent { get; init; }
    public Vector2 Texcoord { get; init; }
}
```

### MetalMesh (MetalMesh.Core.cs)

```csharp
public sealed class MetalMesh
{
    public MetalVertex[] Vertices { get; init; } = Array.Empty<MetalVertex>();
    public uint[] Indices { get; init; } = Array.Empty<uint>();
    public float ReferenceRadius { get; init; }
}
```

### CollarMesh (OuroborosCollarMeshBuilder.cs)

```csharp
public sealed class CollarMesh
{
    public MetalVertex[] Vertices { get; init; } = Array.Empty<MetalVertex>();
    public uint[] Indices { get; init; } = Array.Empty<uint>();
    public Vector4[] Tangents { get; init; } = Array.Empty<Vector4>();
    public float ReferenceRadius { get; init; }
}
```

### ImportedMeshData (ImportedStlCollarMeshBuilder.Types.cs, private)

```csharp
private sealed class ImportedMeshData
{
    public List<Vector3> Positions { get; init; } = new();
    public List<uint> Indices { get; init; } = new();
    public List<Vector3>? Normals { get; init; }
    public List<Vector2>? Texcoords { get; init; }
}
```

### GLB Parsing (ImportedStlCollarMeshBuilder.Glb.cs)

`TryReadBinaryGlb(string path, out ImportedMeshData meshData)`:
- Reads GLB binary format (magic 0x46546C67, version 2, JSON + BIN chunks)
- Iterates `meshes[].primitives[]`, skips non-triangle modes
- For each primitive: reads POSITION, NORMAL (optional), TEXCOORD_0 (optional) accessors
- Reads indexed and non-indexed geometry
- **Currently flattens all primitives into one merged vertex/index list with no material tracking**
- The `material` property on each primitive is currently ignored
- The `materials` array in the glTF JSON is currently ignored
- No embedded texture extraction

Key code path (lines 116–247): The `foreach (JsonElement primitive in primitivesElement.EnumerateArray())` loop currently merges everything. Phase 4 needs to track per-primitive boundaries and material indices here.

### Mesh Import Entry Point (ImportedStlCollarMeshBuilder.cs / ImportAndComponent partial)

- `TryReadImportedMesh(string path, out ImportedMeshData meshData)` — dispatches to STL or GLB reader based on extension
- `_cachedImportedMeshData` — static cache of last imported mesh
- `TryExtractLikelyCollarComponent()` — attempts to extract the collar sub-component from multi-part meshes
- The mesh eventually becomes a `CollarMesh` that gets uploaded to GPU

### Imported Mesh → CollarMesh conversion (ImportedStlCollarMeshBuilder.StaticMesh.cs)

The final step builds a `CollarMesh` from `ImportedMeshData`:
```csharp
var result = new CollarMesh
{
    Vertices = vertices,
    Indices = indices.ToArray(),
    ReferenceRadius = referenceRadius
};
```

### GPU Mesh Resources (MetalViewport.ProjectTypesAndBvh.cs, line 797)

```csharp
private sealed class MetalMeshGpuResources : IDisposable
{
    public required IMTLBuffer VertexBuffer { get; init; }
    public required IMTLBuffer IndexBuffer { get; init; }
    public required int IndexCount { get; init; }
    public required MTLIndexType IndexType { get; init; }
    public required float ReferenceRadius { get; init; }
    public required Vector3[] Positions { get; init; }
    public required uint[] Indices { get; init; }
    public required Vector3 BoundsMin { get; init; }
    public required Vector3 BoundsMax { get; init; }
    public required CpuTriangleBvh Bvh { get; init; }
}
```

### MaterialNode (KnobForge.Core/Scene/MaterialNode.cs)

```csharp
public sealed class MaterialNode : SceneNode
{
    // Base material
    public Vector3 BaseColor { get; set; }
    public float Metallic { get; set; }         // 0-1
    public float Roughness { get; set; }        // 0.04-1
    public float Pearlescence { get; set; }     // 0-1

    // Part-materials (for procedural knobs)
    public bool PartMaterialsEnabled { get; set; }
    public Vector3 TopBaseColor { get; set; }
    public float TopMetallic { get; set; }
    public float TopRoughness { get; set; }
    // ... Bevel, Side variants

    // Weathering
    public float RustAmount { get; set; }
    public float WearAmount { get; set; }
    public float GunkAmount { get; set; }

    // Surface
    public float RadialBrushStrength { get; set; }
    public float RadialBrushDensity { get; set; }
    public float SurfaceCharacter { get; set; }

    // PBR texture maps (Phase 2)
    public string? AlbedoMapPath { get; set; }
    public string? NormalMapPath { get; set; }
    public string? RoughnessMapPath { get; set; }
    public string? MetallicMapPath { get; set; }
    public float NormalMapStrength { get; set; } = 1.0f;
    public bool HasAlbedoMap => !string.IsNullOrEmpty(AlbedoMapPath);
    public bool HasNormalMap => !string.IsNullOrEmpty(NormalMapPath);
    public bool HasRoughnessMap => !string.IsNullOrEmpty(RoughnessMapPath);
    public bool HasMetallicMap => !string.IsNullOrEmpty(MetallicMapPath);
}
```

MaterialNode inherits from `SceneNode` (has Name, parent-child tree).

**Current attachment**: One `MaterialNode` per `ModelNode`, retrieved via:
```csharp
MaterialNode? material = model.Children.OfType<MaterialNode>().FirstOrDefault();
```

### Scene Graph (KnobForge.Core/Scene/SceneNode.cs)

```csharp
public class SceneNode
{
    public string Name { get; set; }
    public IReadOnlyList<SceneNode> Children { get; }
    public void AddChild(SceneNode child);
    public void RemoveChild(SceneNode child);
}
```

### TextureManager (KnobForge.Rendering/GPU/TextureManager.cs)

```csharp
public enum TextureMapType { Albedo, Normal, Roughness, Metallic }

public sealed class TextureManager : IDisposable
{
    public TextureManager(IntPtr metalDevice);
    public IntPtr FallbackAlbedo { get; }
    public IntPtr FallbackNormal { get; }
    public IntPtr FallbackRoughness { get; }
    public IntPtr FallbackMetallic { get; }
    public IntPtr GetOrLoadTexture(string? filePath, TextureMapType mapType);
    public void InvalidatePath(string? filePath);
}
```

- Uses SkiaSharp `SKBitmap.Decode()` for image loading
- Max dimension: 4096px (downscales larger images)
- Cache key: file path + modification time ticks
- Mipmaps generated via MTL blit encoder

### Texture Binding in Render Loop (MetalViewport.OffscreenRender.cs, lines 495–544)

Fragment texture slots are bound once before all draw calls:
```
Slot 0: _spiralNormalTexture
Slot 1: _paintMaskTexture
Slot 2: _paintColorTexture
Slot 3: _environmentMapTexture
Slot 4: materialNode albedo → ResolveMaterialTexture(materialNode, TextureMapType.Albedo)
Slot 5: materialNode normal → ResolveMaterialTexture(materialNode, TextureMapType.Normal)
Slot 6: materialNode roughness → ResolveMaterialTexture(materialNode, TextureMapType.Roughness)
Slot 7: materialNode metallic → ResolveMaterialTexture(materialNode, TextureMapType.Metallic)
Slot 8: _paintMask2Texture
```

Slots 0–3 and 8 are shared (not per-material). Slots 4–7 are material-specific — Phase 4 must rebind these per SubMesh.

### Uniform Binding (MetalViewport.MeshAndUniforms.cs)

```csharp
// Material texture flags (lines 781-790)
uniforms.TextureMapFlags = new Vector4(
    materialNode?.HasAlbedoMap == true ? 1f : 0f,
    materialNode?.HasNormalMap == true ? 1f : 0f,
    materialNode?.HasRoughnessMap == true ? 1f : 0f,
    materialNode?.HasMetallicMap == true ? 1f : 0f);

uniforms.TextureMapParams = new Vector4(
    Math.Clamp(materialNode?.NormalMapStrength ?? 1f, 0f, 2f),
    0f, 0f, 0f);
```

`ResolveMaterialTexture()` method (lines 1053–1069) maps `TextureMapType` to `MaterialNode` texture paths and calls `TextureManager.GetOrLoadTexture()`.

### Draw Call Pattern (OffscreenRender.cs)

Each mesh component follows this pattern:
```csharp
// 1. Set front face winding
MetalPipelineManager.SetFrontFacingWinding(encoder, clockwise);
// 2. Bind vertex buffer
ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(encoder, SetVertexBufferOffsetAtIndex, resources.VertexBuffer.Handle, 0, 0);
// 3. Upload uniforms
UploadUniforms(encoder, componentUniforms);
// 4. Draw
ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(encoder, DrawIndexedPrimitives, 3, indexCount, indexType, indexBuffer, 0);
```

**CRITICAL**: The collar draw call (imported mesh path) is at lines 836–857. The knob draw call (procedural mesh) is at lines 868–888. Shadow passes also draw these meshes. Phase 4's multi-draw must update ALL of these draw sites.

**NOTE on imported mesh components**: KnobForge has multiple imported-mesh slots beyond the collar — slider backplate, slider thumb, toggle base, toggle lever, toggle sleeve. Each has its own `_*Resources` field and draw call. For Phase 4, focus on the **collar** import path first (it's the most common multi-material case). The same multi-draw pattern can later be extended to other imported components, but the collar is the priority.

**NOTE on CollarNode vs MaterialNode**: The collar is a `CollarNode` (child of `ModelNode`), not a `ModelNode` itself. It has its own material properties (baseColor, metallic, roughness, pearlescence). `CollarPreset` enum values include `ImportedStl` for user-imported GLBs. For multi-material collars, each SubMesh should resolve its material from `GlbMaterialDef` data. You'll need to either: (a) store per-SubMesh material data alongside the collar, or (b) create MaterialNode children under the CollarNode for per-SubMesh material data. Option (b) is preferred because it reuses existing MaterialNode infrastructure and serialization.

### GpuUniforms MSL Struct (MetalPipelineManager.Shaders.cs, lines 27–67)

```metal
struct GpuUniforms
{
    float4 cameraPosAndReferenceRadius;
    float4 rightAndScaleX;
    float4 upAndScaleY;
    float4 forwardAndScaleZ;
    float4 projectionOffsetsAndLightCount;
    float4 materialBaseColorAndMetallic;       // xyz=baseColor, w=metallic
    float4 materialRoughnessDiffuseSpecMode;   // x=roughness, y=diffuse, z=spec, w=mode
    float4 materialPartTopColorAndMetallic;
    float4 materialPartBevelColorAndMetallic;
    float4 materialPartSideColorAndMetallic;
    float4 materialPartRoughnessAndEnable;
    float4 materialSurfaceBrushParams;
    float4 weatherParams;
    float4 scratchExposeColorAndStrength;
    float4 advancedMaterialParams;
    // ... indicator, environment, shadow, postprocess params ...
    float4 textureMapFlags;                    // x=hasAlbedo, y=hasNormal, z=hasRoughness, w=hasMetallic
    float4 textureMapParams;                   // x=normalMapStrength
    GpuLight lights[MAX_LIGHTS];               // MAX_LIGHTS = 8
    float4 dynamicLightParams;
    GpuLight dynamicLights[MAX_LIGHTS];
};
```

The C# GpuUniforms struct must match EXACTLY. New fields go between `textureMapParams` and `lights[MAX_LIGHTS]`, or after `dynamicLights`. The MSL and C# struct field order and alignment MUST stay perfectly in sync.

### Fragment Shader Signature (MetalPipelineManager.Shaders.cs)

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
    texture2d<float> metallicMap [[texture(7)]],
    texture2d<float> paintMask2 [[texture(8)]])
```

### ObjC Memory Management

Convenience constructors (e.g., `MTLTextureDescriptor.texture2DDescriptorWithPixelFormat`) return autoreleased objects — do NOT explicitly release them. Only release objects you `alloc`/`init` or `new`.

### Serialization

System.Text.Json with `knobforge.project.v1` format ID. Missing properties default to null/default — additive-safe.

## Execution Order

### SUBPHASE 4A: GLB Multi-Primitive Parsing (do this FIRST)

**Goal**: When reading a GLB, track which indices belong to which primitive (with its material index), and parse the glTF `materials` array. Do NOT change the render loop yet.

**Step 1: Add SubMesh struct**

- File: `KnobForge.Rendering/GPU/MetalMesh/MetalMesh.Core.cs`
- Add below `MetalMesh`:
  ```csharp
  public readonly struct SubMesh
  {
      public int IndexOffset { get; init; }
      public int IndexCount { get; init; }
      public int MaterialIndex { get; init; }
  }
  ```

**Step 2: Add SubMeshes to CollarMesh**

- File: `KnobForge.Rendering/GPU/OuroborosCollarMeshBuilder/OuroborosCollarMeshBuilder.cs`
- Add property to `CollarMesh`:
  ```csharp
  public SubMesh[] SubMeshes { get; init; } = Array.Empty<SubMesh>();
  ```

**Step 3: Add GlbMaterialDef to capture parsed material definitions**

- File: `ImportedStlCollarMeshBuilder.Types.cs`
- Add inside the partial class:
  ```csharp
  private sealed class GlbMaterialDef
  {
      public string Name { get; init; } = "Material";
      public Vector3 BaseColor { get; init; } = new(0.8f, 0.8f, 0.8f);
      public float Metallic { get; init; } = 1.0f;
      public float Roughness { get; init; } = 0.5f;
      public int? BaseColorTextureIndex { get; init; }
      public int? NormalTextureIndex { get; init; }
      public int? MetallicRoughnessTextureIndex { get; init; }
  }
  ```

**Step 4: Add per-primitive tracking to ImportedMeshData**

- File: `ImportedStlCollarMeshBuilder.Types.cs`
- Add to `ImportedMeshData`:
  ```csharp
  public List<SubMesh>? SubMeshes { get; init; }
  public List<GlbMaterialDef>? Materials { get; init; }
  public List<byte[]>? EmbeddedImages { get; init; }  // raw image bytes per glTF image index
  ```

**Step 5: Track per-primitive index ranges in GLB parser**

- File: `ImportedStlCollarMeshBuilder.Glb.cs`
- In `TryReadBinaryGlb()`, before the primitive loop: create `var subMeshes = new List<SubMesh>();`
- Before each primitive adds indices: record `int indexOffsetBefore = indices.Count;`
- Read the `material` property: `int materialIndex = primitive.TryGetProperty("material", out var matEl) && matEl.TryGetInt32(out int mi) ? mi : 0;`
- After each primitive's indices are added: `subMeshes.Add(new SubMesh { IndexOffset = indexOffsetBefore, IndexCount = indices.Count - indexOffsetBefore, MaterialIndex = materialIndex });`
- Set `meshData.SubMeshes = subMeshes;`

**Step 6: Parse glTF materials array**

- In `TryReadBinaryGlb()`, after the mesh/primitive loop but before returning:
- If `root.TryGetProperty("materials", out JsonElement materialsElement)`:
  - For each material element, read:
    - `name` (string, default "Material N")
    - `pbrMetallicRoughness.baseColorFactor` (float[4], default [1,1,1,1]) → extract RGB as Vector3
    - `pbrMetallicRoughness.metallicFactor` (float, default 1.0)
    - `pbrMetallicRoughness.roughnessFactor` (float, default 1.0)
    - `pbrMetallicRoughness.baseColorTexture.index` (int?, optional)
    - `normalTexture.index` (int?, optional)
    - `pbrMetallicRoughness.metallicRoughnessTexture.index` (int?, optional)
  - Store as `List<GlbMaterialDef>`
- Set `meshData.Materials = materialDefs;`

**Step 7: Extract embedded images from GLB binary chunk**

- In `TryReadBinaryGlb()`, after parsing materials:
- If `root.TryGetProperty("images", out JsonElement imagesElement)`:
  - For each image: read `bufferView` index
  - Resolve bufferView → get byteOffset + byteLength in binary chunk
  - Extract raw bytes: `binaryChunk.AsSpan(offset, length).ToArray()`
  - Store as `List<byte[]>`
- If `root.TryGetProperty("textures", out JsonElement texturesElement)`:
  - For each texture: read `source` (index into images array)
  - The material's `baseColorTexture.index` is a texture index, which points to `textures[i].source` → `images[sourceIndex]`
  - Build a texture-index-to-image-bytes lookup
- Set `meshData.EmbeddedImages = imageBytesList;`
- IMPORTANT: The indirection is `material.baseColorTexture.index` → `textures[textureIdx].source` → `images[sourceIdx]`. Do not skip the textures array lookup.

**Step 8: Propagate SubMeshes through to CollarMesh**

- In `ImportedStlCollarMeshBuilder.StaticMesh.cs` (where `CollarMesh` is built from `ImportedMeshData`):
- If `sourceMesh.SubMeshes` has entries, pass them through to the CollarMesh:
  ```csharp
  SubMeshes = sourceMesh.SubMeshes?.ToArray() ?? new[] { new SubMesh { IndexOffset = 0, IndexCount = indices.Length, MaterialIndex = 0 } }
  ```
- When there are no SubMeshes (STL, single-material GLB): create one SubMesh covering all indices

**VERIFY (BUILD GATE):** Build and run. Import a multi-material GLB (e.g., a knob with body + indicator in different materials). Set a breakpoint or add a debug log to confirm SubMeshes has multiple entries with distinct MaterialIndex values. The visual output should still look the same (one material applied to everything) — we're just parsing, not rendering differently yet.

---

### SUBPHASE 4B: Per-Material MaterialNode Creation (after 4A is verified)

**Goal**: When a GLB has multiple materials, auto-create multiple `MaterialNode` children under the `ModelNode`, initialized from the parsed glTF material data.

**Step 1: Store embedded textures as temporary files**

- When GLB import produces `EmbeddedImages`, write each to a temp directory:
  - Path: `Path.Combine(Path.GetTempPath(), "KnobForge", "embedded_textures", $"{glbFileHash}_{imageIndex}.png")`
  - Use SkiaSharp to decode the raw bytes into `SKBitmap`, then re-encode as PNG to a file
  - Store the resulting file paths so MaterialNodes can reference them
  - IMPORTANT: Handle the metallicRoughness combined texture — glTF packs metallic in B channel and roughness in G channel of a single image. You'll need to split this into separate roughness and metallic images for the TextureManager (which expects separate files). Use SkiaSharp to extract: roughness = green channel → grayscale image, metallic = blue channel → grayscale image.

**Step 2: Create MaterialNode per glTF material**

- File: Wherever the scene graph is set up after mesh import (look for where `MaterialNode` is created and added as child of `ModelNode` — likely in `KnobProject.cs` `EnsureMaterialNode()` method or scene setup code)
- When a GLB import produces multiple `GlbMaterialDef` entries:
  1. Remove the existing single MaterialNode (if any)
  2. For each GlbMaterialDef, create a new MaterialNode:
     - `Name` = material name from GLB (or "Material 0", "Material 1", ...)
     - `BaseColor` = parsed baseColorFactor RGB
     - `Metallic` = parsed metallicFactor
     - `Roughness` = parsed roughnessFactor
     - `AlbedoMapPath` = temp file path for base color texture (if present)
     - `NormalMapPath` = temp file path for normal texture (if present)
     - `RoughnessMapPath` = extracted roughness channel path (if present)
     - `MetallicMapPath` = extracted metallic channel path (if present)
  3. Add all MaterialNodes as children of ModelNode, in order matching material indices

**Step 3: Material index resolution**

- Add a helper method on `ModelNode` (or utility class):
  ```csharp
  public MaterialNode? GetMaterialByIndex(int index)
  {
      var materials = Children.OfType<MaterialNode>().ToArray();
      return index >= 0 && index < materials.Length ? materials[index] : materials.FirstOrDefault();
  }
  ```
- Fallback: if material index is out of range, use the first material (graceful degradation)

**Step 4: Single-material and STL fallback**

- STL imports and single-material GLBs: continue creating one MaterialNode (existing behavior)
- The single SubMesh with MaterialIndex=0 points to the single MaterialNode
- No behavioral change for existing single-material projects

**Step 5: Serialization**

- Multiple MaterialNode children must serialize correctly in the project file
- Test: save a multi-material project, close, reopen — verify all MaterialNodes are restored with their properties
- The existing SceneNode child serialization should handle this if MaterialNode children are already in the tree. If not, extend the project serialization to capture multiple MaterialNode children.

**VERIFY (BUILD GATE):** Build and run. Import a multi-material GLB. In the debugger or via logging, verify that ModelNode now has N MaterialNode children (one per glTF material) with correct BaseColor/Metallic/Roughness values and texture paths. The render should still use only the first material for everything — multi-draw comes next.

---

### SUBPHASE 4C: Multi-Draw Render Loop (after 4B is verified)

**Goal**: Instead of one draw call per mesh, iterate SubMeshes and issue a draw call per SubMesh with the correct material's uniforms and textures.

**Step 1: Add SubMeshes to MetalMeshGpuResources**

- File: `MetalViewport.ProjectTypesAndBvh.cs`
- Add to `MetalMeshGpuResources`:
  ```csharp
  public required SubMesh[] SubMeshes { get; init; }
  ```
- When building GPU resources from a `CollarMesh`, propagate the SubMeshes array
- When building from a `MetalMesh` (procedural knob), create a single SubMesh:
  ```csharp
  SubMeshes = new[] { new SubMesh { IndexOffset = 0, IndexCount = indices.Length, MaterialIndex = 0 } }
  ```

**Step 2: Create helper method for per-SubMesh draw**

- File: `MetalViewport.OffscreenRender.cs` (or a new partial)
- Add:
  ```csharp
  private void DrawMeshWithMaterials(
      IntPtr encoderPtr,
      MetalMeshGpuResources resources,
      GpuUniforms baseUniforms,
      ModelNode? modelNode,
      bool frontFacingClockwise)
  {
      MetalPipelineManager.SetFrontFacingWinding(
          new MTLRenderCommandEncoderHandle(encoderPtr), frontFacingClockwise);
      ObjC.Void_objc_msgSend_IntPtr_UInt_UInt(
          encoderPtr, Selectors.SetVertexBufferOffsetAtIndex,
          resources.VertexBuffer.Handle, 0, 0);

      SubMesh[] subMeshes = resources.SubMeshes;
      if (subMeshes.Length <= 1)
      {
          // Fast path: single material, same as before
          UploadUniforms(encoderPtr, baseUniforms);
          ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
              encoderPtr,
              Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
              3, (nuint)resources.IndexCount, (nuint)resources.IndexType,
              resources.IndexBuffer.Handle, 0);
          return;
      }

      // Multi-material path
      for (int i = 0; i < subMeshes.Length; i++)
      {
          SubMesh sub = subMeshes[i];
          if (sub.IndexCount <= 0) continue;

          MaterialNode? mat = modelNode?.GetMaterialByIndex(sub.MaterialIndex);

          // Rebind material textures (slots 4-7)
          BindMaterialTextures(encoderPtr, mat);

          // Rebuild uniforms with this material's properties
          GpuUniforms subUniforms = baseUniforms;
          ApplyMaterialToUniforms(ref subUniforms, mat);
          UploadUniforms(encoderPtr, subUniforms);

          // Draw this SubMesh's index range
          nuint indexBufferOffset = (nuint)(sub.IndexOffset * sizeof(uint));
          ObjC.Void_objc_msgSend_UInt_UInt_UInt_IntPtr_UInt(
              encoderPtr,
              Selectors.DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset,
              3, (nuint)sub.IndexCount, (nuint)resources.IndexType,
              resources.IndexBuffer.Handle, indexBufferOffset);
      }
  }
  ```

**Step 3: Create BindMaterialTextures helper**

```csharp
private void BindMaterialTextures(IntPtr encoderPtr, MaterialNode? mat)
{
    ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex,
        ResolveMaterialTexture(mat, TextureMapType.Albedo), 4);
    ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex,
        ResolveMaterialTexture(mat, TextureMapType.Normal), 5);
    ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex,
        ResolveMaterialTexture(mat, TextureMapType.Roughness), 6);
    ObjC.Void_objc_msgSend_IntPtr_UInt(encoderPtr, Selectors.SetFragmentTextureAtIndex,
        ResolveMaterialTexture(mat, TextureMapType.Metallic), 7);
}
```

**Step 4: Create ApplyMaterialToUniforms helper**

This method should update the material-specific fields in the uniforms struct:
- `materialBaseColorAndMetallic` = `(mat.BaseColor.X, mat.BaseColor.Y, mat.BaseColor.Z, mat.Metallic)`
- `materialRoughnessDiffuseSpecMode` = `(mat.Roughness, mat.DiffuseStrength, mat.SpecularStrength, ...)`
- `textureMapFlags` = `(hasAlbedo, hasNormal, hasRoughness, hasMetallic)`
- `textureMapParams` = `(normalMapStrength, 0, 0, 0)`

Look at how `collarUniforms` / `knobUniforms` are already built in the render loop for the exact field mappings, and replicate that logic.

**Step 5: Replace collar draw call with DrawMeshWithMaterials**

- File: `MetalViewport.OffscreenRender.cs`
- Replace lines 836–857 (the `if (drawCollar)` block) with:
  ```csharp
  if (drawCollar)
  {
      DrawMeshWithMaterials(encoderPtr, _collarResources!, collarUniforms, collarModelNode, frontFacingClockwiseCollar);
      collarDrawExecuted = true;
  }
  ```
- You'll need to pass the `ModelNode` for the collar to resolve materials. Find where `collarNode` is available — it's likely the `CollarNode` from the project scene, not a `ModelNode`. Trace how materials are currently attached.

**Step 6: Preserve single-material knob path**

- The knob draw call (procedural mesh, lines 868–888) should also use `DrawMeshWithMaterials` for consistency, but since procedural knobs always have one SubMesh, it will take the fast path.

**Step 7: Update shadow passes**

- The shadow pass also draws the collar (line 906):
  ```csharp
  RenderShadowPasses(encoderPtr, collarUniforms, shadowConfig, _collarResources!);
  ```
- Shadow passes typically don't need material textures (they only care about geometry), so they may not need multi-draw. But verify: if `RenderShadowPasses` uses one `DrawIndexedPrimitives` call with the full index count, it can stay as-is. Shadows render all geometry regardless of material.

**Step 8: Update the viewport (live preview) render path**

- The live viewport render path (not just offscreen export) has similar draw calls. Find and update those too. Search for all `DrawIndexedPrimitivesIndexCountIndexTypeIndexBufferIndexBufferOffset` calls that draw the collar.

**Step 9: Update the export render path**

- File: `KnobExporter.cs` and/or the `_gpuFrameProvider` callback
- The export path calls `TryRenderFrameToBitmap()` which is the offscreen renderer — so if you updated it in Step 5, the export path is already covered. Verify by checking the call chain.

**VERIFY (BUILD GATE):** Build and run. Import a multi-material GLB. Each SubMesh should now render with its own material's base color, metallic, roughness, and texture maps. Verify: different parts of the mesh should appear in different materials (e.g., brushed steel body vs red enamel indicator). Export a spritesheet — verify multi-material renders correctly in export. Load a single-material model — verify no regression.

---

### SUBPHASE 4D: Inspector UI for Multi-Material (after 4C is verified)

**Goal**: Show a material list in the inspector when a model has multiple materials, with each material's properties independently editable.

**Step 1: Add material list UI**

- File: New partial or extend existing MainWindow inspector partial
- When the active project's collar/model has multiple MaterialNodes:
  - Show a `ListBox` or equivalent with material names and color swatch previews
  - Selecting a material populates the existing material property sliders with that material's values
  - The texture map browse buttons apply to the selected material

**Step 2: Track selected material index**

- Add `_selectedMaterialIndex` field (default 0)
- When user selects a material in the list, update `_selectedMaterialIndex`
- All existing material slider handlers should read from / write to `model.GetMaterialByIndex(_selectedMaterialIndex)` instead of the single first MaterialNode

**Step 3: Material name editing**

- Allow renaming materials in the inspector list
- The name persists on the MaterialNode and serializes with the project

**Step 4: Conditional UI visibility**

- Show part-materials controls (Top/Bevel/Side region dropdown) ONLY for procedural knobs (where `CollarPreset` is not an imported mesh)
- Show material list ONLY for imported multi-material meshes (where SubMeshes.Length > 1)
- For single-material imported meshes: show the regular single-material controls (same as today)

**Step 5: Invalidation**

- When any material property changes, invalidate the GPU render
- When switching selected material, update all sliders/readouts to reflect the selected material's values
- Texture map path changes on any material should invalidate the TextureManager cache for that path

**VERIFY (BUILD GATE):** Build and run. Import a multi-material GLB. Verify the material list appears. Select different materials and verify the sliders update. Change a material's base color — verify only that SubMesh changes visually. Rename a material. Save, close, reopen — verify material names and per-material properties persist. Import a single-material model — verify no material list appears (standard behavior).

---

## Critical Constraints

1. **ZERO VISUAL REGRESSION on existing features.** After every subphase, procedural knobs, single-material imports, paint, weathering, texture maps, spritesheet export — all must work identically to before.
2. **Do NOT change MetalVertex.** It stays at 48 bytes. Material assignment is per-SubMesh, not per-vertex.
3. **Do NOT change the two UV systems.** Paint UVs stay legacy planar. Material UVs stay on `inVertex.texcoord`.
4. **Do NOT change existing texture slot bindings 0–8.** Material textures stay on slots 4–7; rebind them per SubMesh.
5. **Do NOT change the GpuUniforms struct layout** unless absolutely necessary. Prefer rebuilding the full uniform buffer per SubMesh draw call (Strategy B from the plan: simpler, acceptable for <8 materials).
6. **Build after every task.** If it doesn't compile, fix it before moving on.
7. **GpuUniforms: MSL and C# must stay in sync.** If you add fields, add them at the same position in both structs.

## What NOT to Do

- Do not add texture bake/export (that's Phase 5)
- Do not add a node graph (that's Phase 7)
- Do not add the Choroboros ValueInput inspector control (that's Phase 6)
- Do not refactor the shader string into separate files
- Do not introduce new NuGet dependencies
- Do not rename existing variables or methods unless required for new functionality
- Do not optimize prematurely — correctness first
- Do not change the vertex format
- Do not add multi-material to the procedural knob mesh builder (procedural knobs use part-materials, not SubMeshes)

## After Phase 4 Is Complete

Update `docs/material-tool-program/00-PROGRAM.md` — change Phase 4 status from "Not started" to "Complete". Then read `docs/material-tool-program/05-PHASE-5-TEXTURE-BAKE.md` for the next phase.
