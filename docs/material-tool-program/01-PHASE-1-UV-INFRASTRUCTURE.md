# Phase 1: UV Infrastructure

## Phase Identity

- **Phase**: 1 of 6
- **Name**: UV Infrastructure
- **Depends on**: Nothing (foundation phase)
- **Unlocks**: Phase 2 (Texture Map Import), Phase 3 (Paint Upgrades)
- **Risk**: High — vertex format change touches every mesh builder and the GPU pipeline descriptor
- **Milestone**: M1 — Vertex UVs flow through pipeline with zero visual regression

## Why This Phase Exists

Every feature in the material tool program requires UV coordinates: texture map sampling needs UVs, the paint system needs a well-defined UV space to paint in, texture baking needs UVs to write into, and the node graph needs UV inputs. The current system derives UVs from world position inside the fragment shader (`uv = worldPos.xy / (topRadius * 2.0) + 0.5`), which is a planar top-down projection that breaks for non-cylindrical geometry and discards any UV data that imported GLB meshes contain.

## Subphases

### Subphase 1A: Vertex Format Extension

Extend the GPU vertex format to carry UV coordinates from the CPU mesh builders through the vertex shader to the fragment shader.

#### Project 1A.1: MetalVertex Struct Change

Add `packed_float2 texcoord` to the Metal shader vertex input struct and the corresponding C# struct.

**Task 1A.1.1: Update Metal shader MetalVertex struct**
- File: `KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs`
- Change: Add `packed_float2 texcoord;` after `packed_float4 tangent;`
- New stride: 48 bytes (was 40)

**Task 1A.1.2: Update C# MetalVertex struct**
- File: `KnobForge.Rendering/GPU/MetalMesh/MetalMesh.cs` (or wherever the C# vertex struct lives)
- Change: Add `public Vector2 Texcoord;` field
- Verify: `Marshal.SizeOf<MetalVertex>()` returns 48

**Task 1A.1.3: Update vertex descriptor in pipeline setup**
- File: `KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.cs`
- Change: Add attribute descriptor for texcoord (attribute index 3, format float2, offset 40, buffer index 0)
- Subtask: Verify the vertex descriptor attribute count increments from 3 to 4
- Subtask: Verify stride is set to 48

#### Project 1A.2: Shader Passthrough

Pass UVs through the vertex shader to the fragment shader as an interpolated varying.

**Task 1A.2.1: Update VertexOut struct**
- File: `MetalPipelineManager.Shaders.cs`
- Change: Add `float2 texcoord;` to the VertexOut struct

**Task 1A.2.2: Update vertex_main**
- File: `MetalPipelineManager.Shaders.cs`
- Change: In `vertex_main`, set `out.texcoord = float2(v.texcoord);`
- Note: This is a pure passthrough — no transformation needed

**Task 1A.2.3: Verify fragment_main can read texcoord**
- File: `MetalPipelineManager.Shaders.cs`
- Change: Add `float2 vertexUV = inVertex.texcoord;` at the top of fragment_main
- Initially: Don't use it yet. Just verify the pipeline compiles and renders without crashing.

#### Project 1A.3: Zero-Fill All Existing Mesh Builders

Every mesh builder must emit the new texcoord field. Initial pass: fill with (0,0) to verify the stride change doesn't break rendering.

**Task 1A.3.1: Update MetalMeshBuilder (main knob)**
- File: `KnobForge.Rendering/GPU/MetalMesh/MetalMeshBuilder.cs`
- Change: Set `Texcoord = Vector2.Zero` in every MetalVertex construction
- Subtask: Audit every `new MetalVertex(...)` call in this file

**Task 1A.3.2: Update MetalMesh.Detail (indicator hard walls)**
- File: `KnobForge.Rendering/GPU/MetalMesh/MetalMesh.Detail.cs`
- Change: Set `Texcoord = Vector2.Zero` in all vertex constructions

**Task 1A.3.3: Update CollarMeshBuilder**
- File: `KnobForge.Rendering/GPU/CollarMeshBuilder.cs`
- Change: Set `Texcoord = Vector2.Zero` in all vertex constructions

**Task 1A.3.4: Update OuroborosCollarMeshBuilder**
- File: `KnobForge.Rendering/GPU/OuroborosCollarMeshBuilder/`
- Change: Set `Texcoord = Vector2.Zero` in all vertex constructions in all partials

**Task 1A.3.5: Update ImportedStlCollarMeshBuilder**
- File: `KnobForge.Rendering/GPU/ImportedStlCollarMeshBuilder/ImportedStlCollarMeshBuilder.cs`
- Change: Set `Texcoord = Vector2.Zero` (will be replaced with real UVs in 1C)

**Task 1A.3.6: Update IndicatorAssemblyMeshBuilder**
- File: `KnobForge.Rendering/GPU/IndicatorAssemblyMeshBuilder.cs`
- Change: Set `Texcoord = Vector2.Zero` in all vertex constructions

**Task 1A.3.7: Update SliderAssemblyMeshBuilder**
- File: `KnobForge.Rendering/GPU/SliderAssemblyMeshBuilder.cs`
- Change: Set `Texcoord = Vector2.Zero` in all vertex constructions

**Task 1A.3.8: Update ToggleAssemblyMeshBuilder**
- File: `KnobForge.Rendering/GPU/ToggleAssemblyMeshBuilder.cs`
- Change: Set `Texcoord = Vector2.Zero` in all vertex constructions

**Task 1A.3.9: Update PushButtonAssemblyMeshBuilder**
- File: `KnobForge.Rendering/GPU/PushButtonAssemblyMeshBuilder.cs`
- Change: Set `Texcoord = Vector2.Zero` in all vertex constructions

**Task 1A.3.10: Smoke test — build, run, verify identical rendering**
- Verify: Application launches, viewport renders, no visual differences from before
- Verify: Export pipeline produces identical spritesheets
- Verify: Paint system still works (stamps still land correctly)

---

### Subphase 1B: Procedural UV Generation

Generate proper UV coordinates for all procedurally-built geometry (knobs, collars, indicators, assemblies).

#### Project 1B.1: Knob Body UV Generation

**Task 1B.1.1: Top cap radial UVs**
- File: `MetalMeshBuilder.cs`
- Change: For top cap vertices, compute UV from local XY position: `u = localX / (topRadius * 2) + 0.5`, `v = localY / (topRadius * 2) + 0.5`
- Note: This matches the current shader-derived UV exactly, so paint mask data remains valid
- Subtask: Verify paint mask alignment is identical before and after

**Task 1B.1.2: Side wall cylindrical UVs**
- File: `MetalMeshBuilder.cs`
- Change: For side wall vertices, compute UV from angle and height: `u = atan2(y, x) / (2*PI) + 0.5`, `v = (z - minZ) / (maxZ - minZ)`
- Note: The side wall was previously using the same top-down projection as the cap, which mapped incorrectly. This gives it a proper cylindrical unwrap.

**Task 1B.1.3: Bevel transition UVs**
- File: `MetalMeshBuilder.cs`
- Change: For bevel vertices (transition between top and side), interpolate between cap UVs and cylindrical UVs based on the bevel blend factor

**Task 1B.1.4: Bottom face UVs**
- File: `MetalMeshBuilder.cs`
- Change: Mirror of top cap UVs, or simple planar projection

#### Project 1B.2: Collar UV Generation

**Task 1B.2.1: Procedural collar UVs (OuroborosCollarMeshBuilder)**
- File: `OuroborosCollarMeshBuilder/` partials
- Change: Generate cylindrical UVs for the collar ring geometry

**Task 1B.2.2: Base collar UVs (CollarMeshBuilder)**
- File: `CollarMeshBuilder.cs`
- Change: Generate appropriate UVs for the base collar geometry

#### Project 1B.3: Assembly UV Generation

**Task 1B.3.1: Indicator assembly UVs**
- File: `IndicatorAssemblyMeshBuilder.cs`
- Change: Planar projection for indicator light surfaces

**Task 1B.3.2: Slider assembly UVs**
- File: `SliderAssemblyMeshBuilder.cs`
- Change: Planar/cylindrical UVs for slider components

**Task 1B.3.3: Toggle assembly UVs**
- File: `ToggleAssemblyMeshBuilder.cs`
- Change: Appropriate UVs for toggle switch geometry

**Task 1B.3.4: Push button assembly UVs**
- File: `PushButtonAssemblyMeshBuilder.cs`
- Change: Planar UVs for button top face, cylindrical for sides

---

### Subphase 1C: Imported Mesh UV Reading

Read UV coordinates from imported GLB files instead of computing cylindrical projection.

#### Project 1C.1: GLB TEXCOORD_0 Parsing

**Task 1C.1.1: Add TryReadAccessorVector2 method**
- File: `ImportedStlCollarMeshBuilder.Glb.cs`
- Change: Add a new method `TryReadAccessorVector2` that reads VEC2/FLOAT accessors from glTF binary buffer
- Pattern: Follow the existing `TryReadAccessorVector3` implementation but for 2-component vectors

**Task 1C.1.2: Read TEXCOORD_0 from glTF primitive attributes**
- File: `ImportedStlCollarMeshBuilder.Glb.cs`
- Change: In the primitive parsing loop, check for `TEXCOORD_0` attribute and read it with `TryReadAccessorVector2`
- Fallback: If TEXCOORD_0 is not present, use the existing cylindrical projection (current behavior)

**Task 1C.1.3: Pass imported UVs to MetalVertex construction**
- File: `ImportedStlCollarMeshBuilder.cs` (the main BuildFromImported method)
- Change: Use imported UVs when available, otherwise compute cylindrical UVs as current fallback
- Note: The `uvs` array already exists at line 157 and is populated with cylindrical projection. Replace with imported data when available.

**Task 1C.1.4: Test with real GLB files that have UV data**
- Verify: Import a GLB with authored UVs (e.g., from Blender), confirm UVs are read correctly
- Verify: Import an STL (no UVs), confirm cylindrical fallback still works
- Verify: Import a GLB without TEXCOORD_0, confirm cylindrical fallback still works

---

### Subphase 1D: Fragment Shader UV Migration

Switch the fragment shader from world-position-derived UVs to vertex UVs.

#### Project 1D.1: Shader UV Source Switch

**Task 1D.1.1: Replace world-position UV derivation with vertex UV**
- File: `MetalPipelineManager.Shaders.cs`
- Change: Replace `float2 uv = inVertex.worldPos.xy / (topRadius * 2.0) + 0.5;` with `float2 uv = inVertex.texcoord;`
- Note: For procedural knob geometry, the vertex UVs (from 1B.1.1) produce identical values to the old world-position derivation, so this should be a no-op visually
- Subtask: Keep the old world-position derivation as a commented fallback for debugging

**Task 1D.1.2: Verify paint mask sampling still works**
- Verify: Paint strokes land in the same positions as before
- Verify: Existing projects with paint data render identically
- Verify: The spiral normal map samples correctly

**Task 1D.1.3: Verify scratch carve displacement still works**
- The vertex shader also samples the paint mask for scratch displacement (line ~352 in the current shader)
- Verify: Scratch carving still displaces vertices correctly using the new vertex UVs

**Task 1D.1.4: Update paint stamp UV derivation**
- File: `MetalViewport.Shaders.cs` (paint stamp shader)
- Change: If the paint stamp shader derives UVs from world position for hit testing, update to use vertex UVs
- Note: The paint stamp shader is a fullscreen pass that writes to the paint mask texture, so it may not need vertex UVs directly. But the CPU-side hit test (`StampPaintMaskUv`) receives UV coordinates from the mouse ray-mesh intersection. Verify this intersection code computes UVs consistent with the new vertex UVs.

---

## Verification Checklist (Phase 1 Complete)

- [ ] `MetalVertex` struct is 48 bytes with position, normal, tangent, texcoord
- [ ] All mesh builders emit correct texcoord values
- [ ] GLB import reads TEXCOORD_0 when available
- [ ] Fragment shader uses vertex UVs for all texture sampling
- [ ] Paint system strokes land correctly on procedural and imported geometry
- [ ] Scratch carve displacement works correctly
- [ ] Spritesheet export produces pixel-identical output for existing projects
- [ ] No visual regression in viewport rendering
- [ ] Application launches and runs on macOS without crashes

## Files Touched (Complete List)

| File | Nature of Change |
|------|-----------------|
| `MetalPipelineManager.Shaders.cs` | MetalVertex, VertexOut, vertex_main, fragment_main |
| `MetalPipelineManager.cs` | Vertex descriptor attribute count and stride |
| `MetalMesh/MetalMeshBuilder.cs` | UV generation for knob body |
| `MetalMesh/MetalMesh.Detail.cs` | UV generation for indicator hard walls |
| `MetalMesh/MetalMesh.cs` | C# MetalVertex struct |
| `CollarMeshBuilder.cs` | UV generation for base collar |
| `OuroborosCollarMeshBuilder/` (all partials) | UV generation for ouroboros collar |
| `ImportedStlCollarMeshBuilder.cs` | UV passthrough from imported/computed UVs |
| `ImportedStlCollarMeshBuilder.Glb.cs` | TEXCOORD_0 reading, TryReadAccessorVector2 |
| `IndicatorAssemblyMeshBuilder.cs` | UV generation for indicator assembly |
| `SliderAssemblyMeshBuilder.cs` | UV generation for slider assembly |
| `ToggleAssemblyMeshBuilder.cs` | UV generation for toggle assembly |
| `PushButtonAssemblyMeshBuilder.cs` | UV generation for push button assembly |
| `MetalViewport.cs` | Vertex descriptor setup (if separate from pipeline manager) |
