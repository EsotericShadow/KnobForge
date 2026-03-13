# Scope, Boundaries, and Rationale

## Purpose

This document answers three questions: what are we building, what are we explicitly not building, and why did we draw the line where we did. Scope creep kills programs. This document is the fence.

## Business Case and Rationale

### Why This Program Exists

KnobForge currently produces knob spritesheets where every knob looks like a KnobForge knob — metallic, procedurally shaded, with the same limited material expression. Users can adjust scalar parameters (roughness, metallic, color) and paint 4 predefined mask channels (rust, wear, gunk, scratch), but they cannot bring their own textures, use standard PBR workflows, or create materials that don't look like they came from the same tool.

The audio plugin UI market has matured. Developers expect Substance Designer/Blender-quality materials applied to their custom meshes. KnobForge's core value proposition — spritesheet filmstrips for JUCE/iPlug2/HISE — is strong, but the material system is the ceiling that prevents it from competing with general-purpose 3D material tools.

### Expected Benefits

1. **"Bring your own textures"**: Users import PBR texture sets from Poly Haven, Quixel, Substance, or their own libraries. Immediately transforms the variety of output.
2. **Professional paint workflows**: Variable-resolution masks with real layer compositing matches what users expect from Photoshop/Substance Painter.
3. **Multi-material models**: Users import complex GLB assets and assign different materials to different parts. No more single-material limitation.
4. **Texture export**: Composed materials can be baked and exported for use in other tools. KnobForge becomes a material authoring tool, not just a spritesheet renderer.
5. **Procedural generation** (Phase 7): Power users create materials from noise, patterns, and math. This is what separates a texture viewer from a material authoring tool.

### Priority

This program is the primary development effort for KnobForge. No other feature work should compete with it for resources. The spritesheet export pipeline — KnobForge's core revenue/usage driver — must not regress at any point.

## In Scope

### Definitely In Scope (Phases 1–5)

These are non-negotiable deliverables:

- Vertex UV coordinates flowing through the Metal pipeline (Phase 1)
- GLB TEXCOORD_0 import (Phase 1)
- Proper cylindrical/spherical UVs for procedural knob geometry (Phase 1)
- PBR texture map import: albedo, normal, roughness, metallic (Phase 2)
- TextureManager with caching and GPU upload (Phase 2)
- Variable-resolution paint masks (512–4096) (Phase 3)
- True layer compositing with blend modes (Phase 3)
- Roughness and metallic paint channels (Phase 3)
- Multi-primitive GLB parsing and multi-material rendering (Phase 4)
- Texture bake pipeline: composed materials → exported PNG files (Phase 5)
- Inspector UI for all new features (all phases)
- Project file format migration for backward compatibility (all phases)
- No regression in existing spritesheet export workflow (all phases)

### Conditionally In Scope (Phase 7)

Phase 7 is in scope but explicitly deferrable:

- Node graph data model and serialization (6A) — In scope
- Node type library: ~20 procedural/math/color nodes (6B) — In scope
- CPU graph evaluation engine (6C.1) — In scope
- Property-panel graph UI (6D.1) — In scope
- GPU shader compilation from graph (6C.2) — Conditional: only if CPU evaluation proves too slow for real-time preview
- Visual canvas-based graph editor (6D.2) — Conditional: only if the property-panel UI proves insufficient for usability
- Node preview thumbnails (6D.2.2) — Conditional: depends on 6D.2

### Phase 7 Deferral Criteria

Phase 7 gets cut from the initial release if:

1. Phases 1–5 take more than 130% of estimated effort (schedule overrun)
2. A critical regression is discovered that requires rework of earlier phases
3. User feedback after M5 indicates stronger demand for other features (e.g., more paint tools, better export options) over procedural generation

If Phase 7 is deferred, it becomes the first feature in the next program cycle.

## Explicitly Out of Scope

These items will not be built as part of this program, regardless of how useful they might seem. Each has a rationale.

### Rendering Engine Changes

| Out of Scope | Rationale |
|-------------|-----------|
| Vulkan/OpenGL/DirectX backends | KnobForge is macOS-only. Metal is the only GPU API. Adding cross-platform rendering would be a separate program larger than this one. |
| Ray tracing | Metal supports ray tracing but KnobForge's rasterized pipeline is sufficient for material preview. Ray tracing is a rendering quality feature, not a material authoring feature. |
| Real-time global illumination | Same reasoning. The existing environment map lighting is adequate for material evaluation. |
| Shadow mapping changes | Shadows are a scene rendering concern, not a material concern. The existing shadow implementation is unrelated to this program. |
| Post-processing effects (bloom, DOF, SSAO) | These are viewport quality features that don't affect material authoring. |

### Material Features Beyond PBR

| Out of Scope | Rationale |
|-------------|-----------|
| Subsurface scattering (SSS) | Requires a fundamentally different fragment shader model. The current PBR (albedo/normal/roughness/metallic) pipeline does not support it. Could be a future program. |
| Clearcoat / anisotropy / sheen | Extended PBR parameters that require additional texture channels and shader complexity. The 4-map PBR set (albedo, normal, roughness, metallic) is the industry baseline and sufficient for v1. |
| Displacement mapping / tessellation | Requires vertex shader modification and potentially tessellation stages. Far more invasive than texture mapping. |
| Parallax occlusion mapping | Advanced normal map technique that requires per-pixel ray marching. Adds significant shader complexity for a subtle visual improvement. |

### Mesh and Scene Features

| Out of Scope | Rationale |
|-------------|-----------|
| Mesh editing (sculpting, vertex manipulation) | KnobForge is a material tool, not a modeling tool. Users bring models in; KnobForge applies materials. |
| Animation / skeletal rigging | Spritesheet output is already animated by camera rotation. Material animation (scrolling UVs, animated noise) is in scope only for Phase 7's TimeNode. |
| FBX, OBJ, USD import | GLB/glTF is the industry standard interchange format. Supporting additional formats adds testing burden with minimal user value. |
| Scene composition (multiple models) | KnobForge works on a single model at a time. Multi-model scenes are a scene management feature, not a material feature. |
| Physics / simulation | Not relevant to material authoring. |

### Export Features

| Out of Scope | Rationale |
|-------------|-----------|
| glTF/GLB export | KnobForge imports GLB but does not export it. The output is spritesheets and baked textures. Adding GLB export is a separate feature with its own complexity (packing textures into the binary, writing accessors). |
| UDIM texture export | Multi-tile UV workflows (UDIMs) are for film/VFX pipelines. KnobForge's target audience (audio plugin UIs) uses single-tile UV layouts. |
| Substance .sbs/.sbsar export | Proprietary format. Not implementable without licensing. |
| Texture atlas packing | Spritesheet layout handles filmstrip assembly. General-purpose texture atlas packing (fitting multiple textures into a single image) is a different problem. |

### UI/UX Features

| Out of Scope | Rationale |
|-------------|-----------|
| Undo/redo system overhaul | The existing undo system is assumed to work. New operations (layer add/remove, node add/remove) must integrate with the existing undo framework, but redesigning the framework itself is out of scope. |
| Plugin/extension system | A node graph could theoretically support user-defined nodes as plugins. This level of extensibility is a separate architecture effort. Phase 7's node type registry is extensible by code, not by end-user plugins. |
| Localization / internationalization | Not relevant to the material tool transformation. |
| Cross-platform support (Windows/Linux) | KnobForge is macOS-only due to Metal dependency. Cross-platform is a platform effort, not a material effort. |

## Scope Boundary Rationale: The "Material Tool" Frame

The guiding principle for scope decisions is: **does this feature help a user author, compose, or export materials?**

- UV infrastructure: Yes — materials need UVs to map textures. In scope.
- Texture import: Yes — materials are built from textures. In scope.
- Layer compositing: Yes — materials are composed from layers. In scope.
- Multi-material: Yes — real models have multiple materials. In scope.
- Texture bake: Yes — authored materials need to be exported. In scope.
- Node graph: Yes — procedural materials are generated from node graphs. In scope (deferrable).
- Ray tracing: No — this improves the preview, not the material. Out of scope.
- Mesh editing: No — this changes the model, not the material. Out of scope.
- Cross-platform: No — this changes where the tool runs, not what it does. Out of scope.

## Assumptions

These are things we assume to be true. If any assumption is wrong, the plan needs revision.

| Assumption | If Wrong |
|-----------|----------|
| Metal supports up to 31 texture slots per fragment stage | If the limit is lower on some hardware, the texture binding scheme in Phase 2 needs to use argument buffers or texture arrays instead of individual bindings. |
| SkiaSharp can load all common PBR texture formats (PNG, JPEG, WebP, TIFF) | If SkiaSharp can't load a format users need, we must add a format-specific loader or accept the gap. |
| Existing paint mask byte-array storage is acceptable for variable-resolution masks up to 4096x4096 | At 4096x4096x4 bytes = 64 MB per layer, 8 layers = 512 MB. If this exceeds acceptable memory, we need tiled/sparse storage. |
| The .knob project file can accommodate the additional data (texture paths, layer data, graph definitions) without becoming unmanageably large | If project files become > 100 MB due to embedded paint data, we may need to externalize paint mask storage to sidecar files. |
| Users have PBR texture sets ready to import | If users primarily want to create textures from scratch, Phase 7 becomes higher priority than estimated. |
| The existing fragment shader structure can accommodate 4 additional texture samples without performance issues | If 8 texture samples per fragment significantly impacts performance on older GPUs, we may need LOD-based texture reduction. |

## Constraints

These are hard limits that cannot be negotiated.

| Constraint | Source | Impact |
|-----------|--------|--------|
| Metal-only GPU backend | Architecture decision, macOS-only target | Cannot use Vulkan/OpenGL abstractions. All GPU code is Metal-specific. |
| .NET 8 / C# | Existing codebase | No C++ interop beyond the existing P/Invoke pattern for Metal APIs. |
| Avalonia 11.x UI framework | Existing codebase | Custom controls (like a visual graph editor) must be Avalonia controls, not native Cocoa views. |
| Spritesheet export must not regress | Core product value | Every code change must be validated against spritesheet export. This is the most important regression constraint. |
| No new runtime dependencies beyond NuGet packages | Deployment simplicity | Cannot require users to install external tools, runtimes, or libraries. |
