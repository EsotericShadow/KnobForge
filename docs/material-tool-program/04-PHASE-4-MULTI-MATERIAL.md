# Phase 4: Multi-Material Support

## Phase Identity

- **Phase**: 4 of 6
- **Name**: Multi-Material Support
- **Depends on**: Phase 2 (Texture Map Import — texture binding infrastructure required)
- **Unlocks**: Phase 5 (Texture Bake — needs per-material texture evaluation)
- **Risk**: Medium — render loop restructuring, but isolated to imported mesh path
- **Milestone**: M4 — Imported GLB meshes with multiple materials render with per-material textures

## Why This Phase Exists

GLB files from Blender, Substance, or asset stores commonly have multiple materials assigned to different mesh parts (e.g., a knob body in brushed steel, an indicator dot in red enamel, a collar in chrome). Currently KnobForge applies one material to the entire imported mesh. This makes imported models look flat and wrong.

## Subphases

### Subphase 4A: GLB Multi-Primitive Parsing

#### Project 4A.1: Read Per-Primitive Material Indices

**Task 4A.1.1: Parse material index from glTF primitives**
- File: `ImportedStlCollarMeshBuilder.Glb.cs`
- Change: In the primitive parsing loop, read the `material` property from each primitive
- Store: material index alongside position/normal/index data per primitive

**Task 4A.1.2: Parse glTF material definitions**
- File: `ImportedStlCollarMeshBuilder.Glb.cs`
- Add: Read the `materials` array from glTF JSON
- Extract per material: `pbrMetallicRoughness.baseColorFactor`, `metallicFactor`, `roughnessFactor`, `name`
- Extract texture references: `baseColorTexture.index`, `normalTexture.index`, `metallicRoughnessTexture.index`
- Note: Texture images are embedded in GLB binary chunk — need to extract and load them

**Task 4A.1.3: Extract embedded textures from GLB**
- File: `ImportedStlCollarMeshBuilder.Glb.cs` (or new helper)
- Add: Read `images` array from glTF JSON, extract `bufferView` references
- For each image: read bytes from binary chunk, decode via SkiaSharp, store as temporary texture
- Cache: Write extracted textures to a temp directory or hold in memory

#### Project 4A.2: Sub-Mesh Data Structure

**Task 4A.2.1: Define SubMesh struct**
- File: `KnobForge.Rendering/GPU/MetalMesh/MetalMesh.cs` (or new file)
- Add:
  ```csharp
  public readonly struct SubMesh
  {
      public int IndexOffset { get; init; }
      public int IndexCount { get; init; }
      public int MaterialIndex { get; init; }
  }
  ```

**Task 4A.2.2: Extend CollarMesh / MetalMesh with SubMesh list**
- File: `CollarMesh` definition
- Add: `SubMesh[] SubMeshes` property
- When no sub-meshes (single material): single SubMesh covering all indices
- When multi-material: one SubMesh per glTF primitive, each referencing a material index

**Task 4A.2.3: Build SubMesh list during GLB import**
- File: `ImportedStlCollarMeshBuilder.cs`
- Change: Track index offset per primitive during mesh assembly
- Output: Populate `SubMeshes` array on the resulting CollarMesh

---

### Subphase 4B: Per-Material MaterialNode Creation

#### Project 4B.1: Multiple MaterialNodes from GLB

**Task 4B.1.1: Create MaterialNode per glTF material**
- File: Scene setup code (wherever imported meshes attach to the scene graph)
- Change: For each unique material in the GLB, create a `MaterialNode` child under the ModelNode
- Set: `BaseColor`, `Metallic`, `Roughness` from glTF material factors
- Set: Texture paths from extracted embedded textures (if present)

**Task 4B.1.2: Material index mapping**
- Each SubMesh references a material index (0, 1, 2...)
- The scene graph has MaterialNode children in order matching these indices
- The render loop resolves `SubMeshes[i].MaterialIndex → MaterialNodes[materialIndex]`

**Task 4B.1.3: Fallback for STL imports (no materials)**
- STL files have no material data
- Behavior: Single MaterialNode with default properties (current behavior unchanged)
- Single SubMesh covering all indices

---

### Subphase 4C: Multi-Draw Render Loop

#### Project 4C.1: Per-SubMesh Draw Calls

**Task 4C.1.1: Restructure draw call in render loop**
- File: `MetalViewport.OffscreenRender.cs`
- Change: Instead of one `drawIndexedPrimitives` call per mesh, iterate SubMeshes
- For each SubMesh:
  1. Look up the corresponding MaterialNode
  2. Upload material uniforms (base color, metallic, roughness, texture flags)
  3. Bind material textures (albedo/normal/roughness/metallic maps) from TextureManager
  4. Issue `drawIndexedPrimitives` with SubMesh's index offset and count

**Task 4C.1.2: Uniform buffer per material**
- The GpuUniforms struct is large (~6720 bytes). Most of it is shared (camera, lights, etc.)
- Strategy: Keep one shared uniform buffer for camera/lights/environment. Add a small per-material uniform buffer for material-specific properties.
- Alternative: Rebuild the full uniform buffer per SubMesh draw call (simpler, slightly more CPU work, acceptable for <8 materials)

**Task 4C.1.3: Texture rebinding per SubMesh**
- For slots 4-7 (material textures): rebind per SubMesh based on each material's texture paths
- For slots 0-3 (spiral normal, paint mask, paint color, env map): keep bound across all SubMeshes (shared)

**Task 4C.1.4: Preserve single-material fast path**
- When a mesh has only one SubMesh (the common case for procedural knobs): skip the SubMesh iteration overhead
- The existing single draw call path remains as-is

#### Project 4C.2: Export Pipeline Multi-Material Support

**Task 4C.2.1: Multi-draw in export renderer**
- File: `KnobExporter.cs` (the offscreen render frame provider path)
- Change: Same SubMesh iteration as viewport, applied to export frames
- Verify: Spritesheets render correctly with multi-material models

---

### Subphase 4D: Inspector UI for Multi-Material

#### Project 4D.1: Material List UI

**Task 4D.1.1: Material list in inspector**
- File: MainWindow inspector partials
- Add: When a model has multiple MaterialNodes, show them as a list
- Each item shows: material name (from GLB or auto-generated), color swatch preview
- Selecting a material shows its full property inspector (existing sliders + texture maps from Phase 2)

**Task 4D.1.2: Material name editing**
- Allow renaming materials in the inspector
- Names serialize with the project

**Task 4D.1.3: Part-materials coexistence**
- The existing part-materials system (top/bevel/side) is a shader-level 3-region split for procedural knobs
- Multi-material from GLB is a per-SubMesh split for imported meshes
- These don't conflict: part-materials apply within a single-material knob, multi-material applies across SubMeshes
- UI: Show part-materials controls only for procedural knobs; show material list only for imported multi-material meshes

---

## Verification Checklist (Phase 4 Complete)

- [ ] GLB files with multiple primitives/materials import correctly
- [ ] Each primitive gets its own SubMesh with correct material index
- [ ] Embedded GLB textures are extracted and loaded
- [ ] Per-SubMesh draw calls render with correct materials
- [ ] Texture maps are correctly bound per SubMesh
- [ ] Export pipeline produces correct spritesheets for multi-material models
- [ ] Single-material models (procedural knobs, STL imports) render identically to before
- [ ] Inspector shows material list for multi-material models
- [ ] Each material's properties (color, metallic, roughness, textures) are independently editable

## Files Touched

| File | Nature of Change |
|------|-----------------|
| `ImportedStlCollarMeshBuilder.Glb.cs` | Multi-primitive parsing, material extraction, texture extraction |
| `ImportedStlCollarMeshBuilder.cs` | SubMesh list building |
| `CollarMesh` / `MetalMesh` definitions | SubMesh array property |
| `MetalViewport.OffscreenRender.cs` | Multi-draw loop, per-SubMesh uniform/texture binding |
| `KnobExporter.cs` | Multi-draw in export frame provider |
| Scene setup code | Multi-MaterialNode creation from GLB data |
| MainWindow inspector partials | Material list UI |
