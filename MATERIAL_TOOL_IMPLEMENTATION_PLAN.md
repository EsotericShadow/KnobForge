# KnobForge Material Tool Transformation — Full Implementation Plan

## Executive Summary

This document lays out the complete technical plan to evolve KnobForge from a specialized knob spritesheet renderer into a proper 3D material tool. It covers seven major systems that need to be built or overhauled, ordered by dependency chain — each phase unlocks the next.

The plan is grounded in the actual codebase architecture: the Metal shader pipeline, the `GpuUniforms` struct, the 4-slot texture binding system, the `MetalVertex` format, the `KnobProject` serialization, and the Avalonia UI layer.

---

## Current Architecture Snapshot

Before diving into changes, here is the exact state of the systems that matter.

**Metal Vertex Format** — The `MetalVertex` struct has 3 attributes: `packed_float3 position`, `packed_float3 normal`, `packed_float4 tangent`. There are no UV coordinates in the vertex buffer. UVs are derived in the shader from world position: `uv = worldPos.xy / (topRadius * 2.0) + 0.5`.

**Texture Binding Slots** — The fragment shader uses exactly 4 texture slots, all occupied: index 0 is `spiralNormalMap` (procedural knob cap ridges), index 1 is `paintMask` (RGBA: rust/wear/gunk/scratch), index 2 is `paintColor` (premultiplied RGBA paint coat), index 3 is `environmentMap` (HDRI).

**GpuUniforms** — ~105 float4 entries (~6720 bytes). Material properties are passed as scalar uniforms: `materialBaseColorAndMetallic`, `materialRoughnessDiffuseSpecMode`, part materials (top/bevel/side), weathering params, scratch expose color, advanced params (clearcoat, anisotropy). There are no texture-based material properties.

**Paint Mask** — Hardcoded 1024x1024 RGBA8 byte array in `KnobProject`. Four channels packed into one texture. Version-tracked for GPU upload. CPU fallback stamping uses identical brush weight functions.

**Scene Graph** — Simple hierarchy: `SceneRootNode` → `ModelNode` → `MaterialNode` + `CollarNode`, plus `LightNode` children. No data flow, no ports, no connections. `MaterialNode` is a flat property bag with ~20 scalar properties and no texture references.

**Imported Meshes** — GLB import reads POSITION and indices via glTF accessor parsing. Normals are read if available, otherwise computed. UVs are computed from cylindrical projection (atan2-based), even though GLB files contain TEXCOORD_0 data. The GLB parser does not read TEXCOORD accessors at all.

---

## Phase 1: UV Infrastructure (Foundation — Everything Depends on This)

### Why This Comes First

Every subsequent feature — texture maps, proper painting, texture baking, node materials — requires real UV coordinates flowing through the pipeline. The current system derives UVs from world position in the shader, which means: there's no per-vertex UV data, no support for artist-authored UVs from GLB files, and the painting system operates in an implicit planar projection space that doesn't map correctly to non-cylindrical geometry. Nothing else works until this is fixed.

### What Changes

**MetalVertex Struct** — Add `packed_float2 texcoord` to the vertex format. This changes the vertex stride from 40 bytes to 48 bytes. Every mesh builder must be updated to emit this field.

```
// Current (40 bytes)
struct MetalVertex {
    packed_float3 position;   // 12 bytes
    packed_float3 normal;     // 12 bytes
    packed_float4 tangent;    // 16 bytes
};

// New (48 bytes)
struct MetalVertex {
    packed_float3 position;   // 12 bytes
    packed_float3 normal;     // 12 bytes
    packed_float4 tangent;    // 16 bytes
    packed_float2 texcoord;   // 8 bytes
};
```

**VertexOut Struct** — Add `float2 texcoord` to the interpolated outputs passed from vertex to fragment shader.

**Vertex Shader** — Pass `texcoord` through from vertex buffer. The existing world-position-derived UV computation stays as a fallback for procedural geometry, but the fragment shader switches to using vertex texcoords when available.

**Files that change**: `MetalPipelineManager.Shaders.cs` (shader source), `MetalMesh/` (all mesh builders), `CollarMeshBuilder.cs`, `ImportedStlCollarMeshBuilder/` (all partials), `IndicatorAssemblyMeshBuilder.cs`, `SliderAssemblyMeshBuilder.cs`, `ToggleAssemblyMeshBuilder.cs`, `PushButtonAssemblyMeshBuilder.cs`, `MetalViewport.cs` (vertex descriptor setup), the C# `MetalVertex` struct in the rendering project.

**GLB Import** — Modify `ImportedStlCollarMeshBuilder.Glb.cs` to read `TEXCOORD_0` from glTF accessors (add `TryReadAccessorVector2`). When present, use the mesh's authored UVs. When absent (STL files), fall back to the current cylindrical projection.

**Procedural Knob Geometry** — The main knob mesh builder generates geometry programmatically. Add proper UV generation: for the top cap, use radial projection (existing `uv` logic moved from shader to mesh build time). For the side wall, use cylindrical unwrap (angle → U, height → V). For the bevel, use a transition blend. This gives the procedural knob real per-vertex UVs that match artist expectations.

**Paint System UV Migration** — The paint stamp system currently works in the world-position-derived UV space. Once vertex UVs exist, the paint system should operate in vertex UV space instead. This is a careful migration: the `StampPaintMaskUv` function in `KnobProject.cs` uses UV center/radius parameters, and the GPU paint stamp shader also uses UV space. Both need to reference the same UV domain as the new vertex texcoords. For procedural knobs, the new vertex UVs will produce identical results to the old world-position derivation, so existing paint data remains valid.

### Risk Assessment

This is the riskiest phase because the vertex format change touches every mesh builder and the pipeline descriptor. A stride mismatch between CPU vertex upload and GPU vertex descriptor will produce garbage rendering or a crash. The approach should be: add the `texcoord` field, fill it with (0,0) everywhere first, verify nothing breaks, then progressively fill in correct UVs per mesh builder.

---

## Phase 2: Texture Map Import System

### Why This Comes Second

With vertex UVs flowing through the pipeline, we can now sample user-provided texture maps. Without Phase 1, there's no meaningful UV space to sample textures in.

### What Changes

**MaterialNode** — Add texture path properties: `AlbedoMapPath`, `NormalMapPath`, `RoughnessMapPath`, `MetallicMapPath`. Each is an optional string file path. Add corresponding enable flags and per-map parameters (tiling X/Y, offset X/Y, rotation).

```csharp
// New properties on MaterialNode
public string? AlbedoMapPath { get; set; }
public string? NormalMapPath { get; set; }
public string? RoughnessMapPath { get; set; }
public string? MetallicMapPath { get; set; }

public float AlbedoMapTileX { get; set; } = 1f;
public float AlbedoMapTileY { get; set; } = 1f;
public float AlbedoMapOffsetX { get; set; } = 0f;
public float AlbedoMapOffsetY { get; set; } = 0f;
// ... same pattern for each map
```

**Texture Slot Expansion** — The current pipeline uses slots 0-3. Add slots 4-7 for material maps: index 4 = albedo map, index 5 = normal map, index 6 = roughness map, index 7 = metallic map. Metal supports up to 31 texture slots per shader stage, so there's ample headroom.

**Texture Loading and Caching** — Create a new `TextureManager` class (in `KnobForge.Rendering`) that: loads image files (PNG, JPG, TIFF, EXR) via SkiaSharp, converts to RGBA8 or RGBA16F as appropriate, creates Metal textures with mipmaps, caches by file path + modification time, and disposes when paths change. This is analogous to the existing `EnsurePaintMaskTexture` pattern but generalized.

**GpuUniforms Additions** — Add a `float4 textureMapFlags` uniform: X = albedo map enabled (0/1), Y = normal map enabled (0/1), Z = roughness map enabled (0/1), W = metallic map enabled (0/1). Add `float4 textureMapTilingAlbedoNormal` and `float4 textureMapTilingRoughnessMetallic` for tiling/offset parameters.

**Fragment Shader Changes** — In the material evaluation chain, before the existing scalar material application, add texture map sampling:

```metal
// After UV setup, before lighting
float2 matUV = inVertex.texcoord;

if (textureMapFlags.x > 0.5) {
    float2 albedoUV = matUV * albedoTiling.xy + albedoTiling.zw;
    float4 albedoSample = albedoMap.sample(linearSampler, albedoUV);
    baseColor = albedoSample.rgb;
    // Optional: albedo alpha as opacity mask
}

if (textureMapFlags.y > 0.5) {
    float2 normalUV = matUV * normalTiling.xy + normalTiling.zw;
    float3 normalSample = normalMap.sample(linearSampler, normalUV).rgb * 2.0 - 1.0;
    // Apply tangent-space normal mapping using TBN matrix
    normal = normalize(T * normalSample.x + B * normalSample.y + N * normalSample.z);
}

if (textureMapFlags.z > 0.5) {
    float2 roughnessUV = matUV * roughnessTiling.xy + roughnessTiling.zw;
    roughness = roughnessMap.sample(linearSampler, roughnessUV).r; // Green channel for glTF convention
}

if (textureMapFlags.w > 0.5) {
    float2 metallicUV = matUV * metallicTiling.xy + metallicTiling.zw;
    metallic = metallicMap.sample(linearSampler, metallicUV).r;
}
```

Texture maps are applied *before* the weathering/paint pass, so rust/wear/gunk/scratch paint over the texture-mapped base material. This is the correct layering order — the base material (now texture-driven) is the substrate, and the painting system adds surface effects on top.

**Texture Binding** — In `MetalViewport.OffscreenRender.cs`, after the existing 4 texture binds, add conditional binds for slots 4-7. If a texture map is not loaded, bind a 1x1 white fallback texture (for albedo/metallic) or a 1x1 flat normal (for normal map) to prevent shader sampling errors.

**UI** — Add a "Texture Maps" section to the material inspector in `MainWindow`. For each map slot: a file path display, a "Browse..." button (using Avalonia's `OpenFileDialog`), a "Clear" button, and tiling/offset sliders. This section appears below the existing color/metallic/roughness sliders and above the weathering controls.

**Serialization** — `KnobProject` serialization already uses System.Text.Json. The texture paths are relative to the project file location. Add path resolution on load (resolve relative paths against project directory). Handle missing files gracefully — if a referenced texture file doesn't exist at load time, clear the path and log a warning.

### Ordering Within Phase 2

1. Add the 4 texture path properties to `MaterialNode` and serialize them
2. Build the `TextureManager` with load/cache/dispose
3. Add the uniform fields and shader sampling (with fallback textures)
4. Wire up the texture binding in the viewport render path
5. Build the UI inspector controls
6. Test with real PBR texture sets (downloaded metal/wood textures)

---

## Phase 3: Paint System Upgrades

### Why This Comes Third

The paint system already works, but it's limited to 1024x1024 fixed resolution, has no real layer compositing, and lacks blending modes. With proper UVs (Phase 1) and the texture management infrastructure (Phase 2), we can now upgrade it meaningfully.

### Variable Resolution Paint Masks

**Remove the hardcoded 1024 constant.** Make `PaintMaskSize` a project-level setting with options: 512, 1024, 2048, 4096. Default remains 1024. The `_paintMaskRgba8` byte array allocation becomes `new byte[PaintMaskSize * PaintMaskSize * 4]`. All code that references `DefaultPaintMaskSize` switches to the instance property.

Memory impact: 4096x4096 RGBA8 = 64 MB per paint mask. With a color paint texture of the same size, that's 128 MB. This is within reason for a desktop tool but warrants a warning in the UI when selecting 4096.

**Texture upload** — `EnsurePaintMaskTexture` already reads `project.PaintMaskSize` for the texture descriptor size. It will naturally create the correct size texture as long as the descriptor uses the project's paint mask size (it already does). The mipmap generation pass also handles arbitrary sizes.

**Paint stamp scaling** — The UV-space brush radius is resolution-independent (it's in 0-1 UV space), so brush stamps automatically scale correctly with higher resolution masks. The pixel-space loop bounds in `StampPaintMaskUv` use `size` from the paint mask, which will be correct.

### True Layer Compositing

The current "layers" are just named collections of paint strokes that replay onto the same byte array. To get real compositing:

**Per-layer paint textures** — Each paint layer gets its own RGBA8 texture (same resolution as the paint mask). The final composited result is computed by blending layers in order using the layer's blending mode and opacity.

**Layer properties** — Add to each paint layer: `Opacity` (0-1), `BlendMode` (Normal, Multiply, Screen, Overlay, Add), `Visible` (bool).

**Compositing pass** — Add a GPU compute or fragment shader pass that composites all visible layers into the final `paintMask` texture before the main render. This runs whenever a layer property changes or a stroke is committed. For the initial implementation, CPU compositing is simpler: iterate layers bottom-to-top, blend each layer's RGBA8 data into the output buffer using the specified blend mode and opacity. This composited buffer is what gets uploaded to the GPU.

**Memory consideration** — Each layer at 1024x1024 = 4 MB. 10 layers = 40 MB. At 4096x4096 per layer, 10 layers = 640 MB. This means layer count should have a practical limit (perhaps 16 at 1024, 8 at 2048, 4 at 4096) with clear memory indicators in the UI.

### Roughness/Metallic Paint Channels

Currently the 4 paint channels are rust/wear/gunk/scratch — all interpreted as fixed weathering effects with hardcoded shader logic. Add two new channels: `PaintRoughness` and `PaintMetallic`, which directly paint roughness and metallic values into dedicated texture layers.

This requires either expanding the paint mask beyond RGBA (use a second paint mask texture for additional channels, or use RG16 for roughness/metallic as a separate texture), or more practically: these become additional layers in the compositing system with dedicated blend targets.

---

## Phase 4: Multi-Material Support

### Why This Comes Fourth

With texture maps working and the paint system upgraded, the next limitation is that imported meshes can only have one material. Complex GLB models ship with multiple materials assigned to different mesh primitives.

### What Changes

**MaterialNode per Mesh Primitive** — When importing a GLB, read the `material` index from each glTF primitive. Create a `MaterialNode` child for each unique material index. Store a `MaterialIndex` property on each vertex (or use a material ID buffer).

**Option A: Material ID Buffer** — Add an additional vertex attribute `uint8 materialId` to `MetalVertex`. The fragment shader indexes into a small material array in the uniform buffer (up to 8 materials). This is the simpler approach and avoids multiple draw calls.

**Option B: Multi-Draw** — Split the mesh into sub-meshes per material and issue separate draw calls with different uniform buffers. This is more flexible (each material can have its own textures) but requires significant changes to the render loop.

**Recommended: Option B** for full texture support. Each material needs its own set of texture bindings (albedo/normal/roughness/metallic), which can't be indexed per-pixel with Option A without using texture arrays or bindless textures.

**Implementation** — Modify `CollarMesh` / `MetalMesh` to store multiple sub-meshes, each with their own index buffer range and material reference. The render loop in `MetalViewport.OffscreenRender.cs` iterates sub-meshes, binds the appropriate material uniforms and textures, and draws each range.

**The existing part-materials system** (top/bevel/side) becomes a special case of multi-material: it's a 3-material system with shader-computed material IDs rather than per-vertex IDs. Both systems can coexist — part materials for procedural knobs, multi-material for imported meshes.

---

## Phase 5: Texture Bake / Export Pipeline

### Why This Comes Fifth

With all the material inputs working (texture maps, paint layers, weathering), users need to get the composed result out as exportable texture maps for use in other tools and game engines.

### What Changes

**Bake Targets** — Add export options to produce: composed albedo map (base texture + weathering + paint color), composed normal map (imported normal + spiral detail + scratch displacement), composed roughness map (base roughness + weathering modifications), composed metallic map (base metallic + weathering modifications), composed ambient occlusion (from paint/weathering data).

**Bake Renderer** — A new `TextureBaker` class that: sets up an orthographic UV-space render (rendering the mesh unwrapped into UV space), evaluates the full material chain for each texel, and writes the result to an output image. This is essentially a UV-space rasterization pass.

**Simpler alternative for initial implementation** — Since the paint system already operates in UV space, the bake can be done by: reading the final composited paint mask, sampling each texel through the material evaluation logic (the same math the fragment shader does, but on CPU), and writing out separate channel maps. This avoids building a full UV-space GPU rasterizer.

**Export UI** — Add a "Bake Textures" section to the export window (or a separate dialog). Options: resolution (independent of paint mask resolution — can upscale or downscale), format (PNG 8-bit, PNG 16-bit, EXR 32-bit float), which maps to bake (checkboxes for albedo/normal/roughness/metallic/AO), output folder.

**File naming** — `{BaseName}_albedo.png`, `{BaseName}_normal.png`, `{BaseName}_roughness.png`, `{BaseName}_metallic.png`. Optionally a `{BaseName}_material.json` metadata file describing the maps and their intended usage (for import into other tools).

---

## Phase 6: Node-Based Material Graph (Long-Term)

### Why This Comes Last

This is the largest single feature and the one with the highest risk-to-value ratio. The preceding phases give KnobForge 80% of the material tool capability (texture maps, painting, multi-material, baking) without a node graph. The node graph adds procedural texture generation and complex material layering, which is powerful but architecturally invasive.

### Architecture Design

**Graph Model** — A directed acyclic graph (DAG) where each node has typed input/output ports. Port types: `Float`, `Float2`, `Float3`, `Float4`, `Texture2D`, `Color`. Connections transfer data from output port to input port. The graph evaluates by topological sort.

**Node Types (Initial Set)**:

*Input nodes* — Texture Map (loads an image file), Vertex UV, World Position, Vertex Normal, Camera Direction, Time.

*Math nodes* — Add, Multiply, Lerp, Clamp, Remap, Power, Dot Product, Cross Product, Normalize.

*Pattern nodes* — Perlin Noise, Voronoi, Gradient (linear/radial/angular), Checker, Brick.

*Color nodes* — HSV to RGB, RGB to HSV, Brightness/Contrast, Color Ramp (gradient mapping).

*Material nodes* — PBR Output (final node: takes albedo, normal, roughness, metallic, emission inputs).

*Weathering nodes* — Rust Generator, Wear Generator, Gunk Generator (parameterized versions of the current hardcoded shader logic, now as graph nodes with tunable inputs).

**Evaluation Strategy** — For real-time preview, compile the graph into a Metal shader function. Each node becomes a shader code fragment. The graph compiler performs topological sort, generates variable declarations for each node output, and emits the evaluation code inline in the fragment shader.

This is the approach used by Unity ShaderGraph, Unreal Material Editor, and Godot VisualShader. It produces optimal GPU code at the cost of shader recompilation when the graph changes.

**Alternative: CPU evaluation with texture caching** — Evaluate the graph on CPU, write results to textures, and upload to GPU. Simpler to implement but slower for complex graphs and loses real-time interactivity. This could be the initial implementation with GPU compilation added later.

**Graph UI** — Avalonia does not have a built-in node graph control. Options: build a custom `Canvas`-based node graph control (significant UI work), use an existing Avalonia node editor library (limited options as of 2025), or embed a web-based node editor (e.g., rete.js) in an Avalonia WebView.

**Recommended initial approach** — Start with the CPU evaluation strategy and a simple property-panel UI (no visual graph editor). Each node is represented as a list item with configurable inputs. This delivers the procedural texture generation capability without the massive UI investment. The visual graph editor can be added as a second pass.

**Serialization** — The graph structure serializes as JSON within the `.knob` project file. Each node has a type ID, a GUID, parameter values, and a list of connections (source node GUID + port name → destination node GUID + port name).

---

## Phase Dependency Map

```
Phase 1: UV Infrastructure
    ↓
Phase 2: Texture Map Import  ←→  Phase 3: Paint Upgrades (parallel-safe)
    ↓                                ↓
Phase 4: Multi-Material (needs Phase 2)
    ↓
Phase 5: Texture Bake (needs Phases 2, 3, 4)
    ↓
Phase 6: Node Graph (needs all above)
```

Phases 2 and 3 can be developed in parallel once Phase 1 is complete. Phase 4 depends on Phase 2 (texture binding infrastructure). Phase 5 depends on the material pipeline being complete (Phases 2-4). Phase 6 is the capstone that builds on everything.

---

## File Impact Summary

### Core Rendering (Highest Impact)

| File | Changes |
|------|---------|
| `MetalPipelineManager.Shaders.cs` | MetalVertex struct, VertexOut struct, vertex_main (UV passthrough), fragment_main (texture sampling, material evaluation chain) |
| `MetalViewport.OffscreenRender.cs` | Texture binding for slots 4-7, material uniform upload |
| `MetalViewport.PaintResources.cs` | Variable resolution textures, layer compositing |
| `MetalMesh/` (all builders) | UV generation for all procedural geometry |
| `ImportedStlCollarMeshBuilder.Glb.cs` | Read TEXCOORD_0 from glTF, multi-primitive material indices |
| `MetalPipelineTypes.cs` | Updated C# MetalVertex struct with texcoord field |

### Core Data (Medium Impact)

| File | Changes |
|------|---------|
| `MaterialNode.cs` | Texture path properties, tiling params, multi-material support |
| `KnobProject.cs` | Variable paint mask size, layer compositing, texture path serialization |
| `KnobExportSettings.cs` | Texture bake options |
| `SceneNode.cs` | Potential additions for graph node base class |

### New Files

| File | Purpose |
|------|---------|
| `TextureManager.cs` | Texture loading, caching, GPU upload, disposal |
| `TextureBaker.cs` | UV-space texture bake/export |
| `MaterialGraph/` (new directory) | Graph model, node types, evaluator, compiler |
| `PaintLayerCompositor.cs` | Multi-layer blend mode compositing |

### UI (KnobForge.App)

| File | Changes |
|------|---------|
| `MainWindow` (material inspector partials) | Texture map slots UI, paint layer properties, variable resolution selector |
| `RenderSettingsWindow.axaml` | Texture bake export options |
| New: material graph editor control | Node graph UI (Phase 6) |

---

## Estimated Scope

| Phase | Effort | Files Touched | New Files | Risk |
|-------|--------|---------------|-----------|------|
| Phase 1: UV Infrastructure | Large | ~15 | 0 | High (vertex format change is invasive) |
| Phase 2: Texture Map Import | Large | ~8 | 1-2 | Medium (new texture slots, shader changes) |
| Phase 3: Paint Upgrades | Medium | ~5 | 1 | Low (isolated subsystem) |
| Phase 4: Multi-Material | Medium | ~6 | 0 | Medium (render loop changes) |
| Phase 5: Texture Bake | Medium | ~4 | 1-2 | Low (new pipeline, minimal existing code changes) |
| Phase 6: Node Graph | Very Large | ~10 | 10+ | High (new subsystem, shader compilation) |

---

## Recommended Execution Order

Start with Phase 1 (UVs) — it's the foundation and the riskiest, so it should be tackled first to de-risk everything downstream. Then Phase 2 (texture import) because it delivers the single most impactful user-visible feature. Phase 3 (paint upgrades) can overlap with late Phase 2 work. Phase 4 (multi-material) follows naturally from Phase 2. Phase 5 (bake) is the "export completeness" milestone. Phase 6 (node graph) is a separate project that can be deferred indefinitely — the tool is useful without it.

The minimum viable material tool is Phases 1 + 2: vertex UVs and texture map import. That alone transforms KnobForge from "every knob looks like KnobForge" to "bring your own textures."
