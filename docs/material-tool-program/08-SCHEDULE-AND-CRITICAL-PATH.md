# Schedule, Timeline, and Critical Path

## Purpose

This document defines the sequencing of work, estimates effort per phase, identifies the critical path, and establishes review points. Effort is expressed in developer-weeks (one developer working full-time), not calendar dates — calendar dates depend on team size and allocation, which vary.

## Effort Estimates

### Phase-Level Estimates

| Phase | Effort (dev-weeks) | Confidence | Rationale |
|-------|-------------------|------------|-----------|
| Phase 1: UV Infrastructure | 3–4 | High | Well-scoped. MetalVertex change is invasive but mechanically straightforward. Largest risk is the vertex stride change cascading into unexpected pipeline issues. |
| Phase 2: Texture Map Import | 3–4 | High | TextureManager, 4 new texture slots, shader sampling, inspector UI. Each piece is individually small but there are many touch points. |
| Phase 3: Paint Upgrades | 4–5 | Medium | Variable resolution paint masks and true layer compositing are conceptually clear but involve fiddly memory management and blend mode correctness. |
| Phase 4: Multi-Material | 2–3 | High | Mostly plumbing: GLB multi-primitive parsing, per-SubMesh draw calls, material-to-mesh association. The render loop change is the only non-trivial part. |
| Phase 5: Texture Bake | 2–3 | High | CPU material evaluation is slow but simple. File I/O, naming conventions, metadata. UI is a single panel. |
| Phase 6: Inspector Controls | 2–3 | High | One new custom control (~400 lines), then 219 replacements across AXAML and code-behind. High confidence because the replacement is mechanical once the control works. |
| Phase 7: Node Graph | 8–12 | Low | Largest phase. Graph data model is straightforward. Node type library is volume work. CPU evaluator is medium complexity. GPU shader compilation is high complexity. Visual graph editor is a project unto itself. |
| **Total** | **24–34** | — | — |

### Subphase Estimates

#### Phase 1 Breakdown

| Subphase | Effort | Notes |
|----------|--------|-------|
| 1A: Vertex Format Extension | 1 week | The single most invasive change. MetalVertex → 48 bytes, every mesh builder updated, pipeline descriptor updated. |
| 1B: Procedural UV Generation | 0.5 weeks | Cylinder/knob UV math is well-understood. |
| 1C: Imported Mesh UV Reading | 0.5 weeks | GLB TEXCOORD_0 accessor reading — follows existing POSITION reading pattern. |
| 1D: Fragment Shader UV Migration | 1 week | Replace derived UVs with vertex attribute UVs in all shader paths. Verify no visual regression. |

#### Phase 2 Breakdown

| Subphase | Effort | Notes |
|----------|--------|-------|
| 2A: Data Model | 0.5 weeks | MaterialNode property additions, TextureSetPaths class. |
| 2B: Texture Loading | 1 week | TextureManager class, SkiaSharp loading, Metal texture creation, caching. |
| 2C: GPU Pipeline Integration | 1 week | Texture slots 4–7, fragment shader sampling, uniform flags. |
| 2D: Inspector UI | 0.5–1 week | File pickers, texture preview thumbnails, clear buttons. |

#### Phase 3 Breakdown

| Subphase | Effort | Notes |
|----------|--------|-------|
| 3A: Variable Resolution | 1 week | Paint mask size parameterization, migration of hardcoded 1024. |
| 3B: True Layer Compositing | 2 weeks | PaintLayer class, PaintLayerCompositor, blend modes (multiply/overlay/screen/normal), layer UI with reorder/opacity/visibility. |
| 3C: New Paint Channels | 1–2 weeks | Roughness and metallic paint channels, separate brush tools, compositor integration. |

#### Phase 4 Breakdown

| Subphase | Effort | Notes |
|----------|--------|-------|
| 4A: GLB Multi-Primitive Parsing | 0.5 weeks | Extend existing GLB reader. |
| 4B: Per-Material MaterialNode Creation | 0.5 weeks | Auto-create MaterialNode per GLB material. |
| 4C: Multi-Draw Render Loop | 1 week | Per-SubMesh draw calls with texture/uniform rebinding. |
| 4D: Inspector UI | 0.5 weeks | Material list, material-to-mesh association display. |

#### Phase 5 Breakdown

| Subphase | Effort | Notes |
|----------|--------|-------|
| 5A: Bake Engine | 1–1.5 weeks | TextureBaker class, CPU material evaluation, texel iteration. |
| 5B: Bake UI | 0.5–1 week | Resolution picker, channel selection, output path, progress bar. |
| 5C: GPU Bake Path | Deferred | Metal compute shader baking. Only if CPU bake is too slow for user needs. |

#### Phase 7 Breakdown

| Subphase | Effort | Notes |
|----------|--------|-------|
| 7A: Graph Data Model | 1 week | Port types, GraphNode, GraphConnection, MaterialGraph with validation. |
| 7B: Node Type Library | 2–3 weeks | ~20 node types. Volume work but individually simple. |
| 7C: Graph Evaluation | 2–3 weeks | CPU evaluator (1 week), GPU shader compilation (1–2 weeks). |
| 7D: Graph UI | 3–5 weeks | Property panel UI (1 week), visual graph editor with canvas/bezier curves (2–4 weeks). |

## Critical Path

The critical path is the longest chain of dependent work. Delays on the critical path delay the entire program. Delays off the critical path have slack.

```
CRITICAL PATH (longest dependency chain):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Phase 1A ──→ Phase 1D ──→ Phase 2C ──→ Phase 4C ──→ Phase 5A ──→ Phase 7C
(vertex)     (shader)     (texture     (multi-draw)  (bake        (graph
              UV swap)     GPU bind)                  engine)      eval)

Duration: ~12–16 dev-weeks end-to-end

PARALLEL PATHS (have slack):
━━━━━━━━━━━━━━━━━━━━━━━━━━━

Phase 1B, 1C ──→ (merge into 1D)
Phase 2A, 2B ──→ (merge into 2C)
Phase 2D ──→ (after 2C, parallel with Phase 3/4 start)
Phase 3 entire ──→ (parallel with Phase 2, merges at Phase 5)
Phase 7A, 7B ──→ (can start during Phase 4 or 5, merge into 7C)
Phase 7D ──→ (can start after 7A, parallel with 7B and 7C)
```

### Critical Path Analysis

The critical path runs through the GPU pipeline workstream (Workstream A). This is not surprising — the GPU pipeline is the foundation that everything else renders through. Implications:

1. **Phase 1A is the single highest-risk task in the entire program.** The MetalVertex struct change from 40→48 bytes touches every mesh builder, the pipeline descriptor, and the vertex shader. If this breaks in subtle ways (misaligned stride, wrong offset), debugging is painful because Metal validation errors are often cryptic.

2. **Phase 3 is entirely off the critical path.** Paint upgrades can happen in parallel with Phases 2 and 4 without affecting the program timeline. This is by design — paint is an isolated subsystem.

3. **Phase 7D (Graph UI) is off the critical path.** The visual graph editor is the single largest subphase by effort, but it doesn't block anything else. The property-panel fallback UI (7D.1) is sufficient for testing the graph engine.

## Parallelism Opportunities

### Two-Developer Parallelism

If two developers are available, the optimal split is:

| Developer | Week 1–4 | Week 5–8 | Week 9–12 | Week 13+ |
|-----------|----------|----------|-----------|----------|
| Dev A (GPU) | Phase 1 (all) | Phase 2C, 2D | Phase 4 (all) | Phase 5A, then 7C |
| Dev B (Paint/UI) | — (blocked on Phase 1) | Phase 2A, 2B, then Phase 3 start | Phase 3 (finish) | Phase 5B, then 7A, 7B, 7D |

This reduces the calendar time by roughly 30–40% compared to solo development.

### Solo Developer Sequencing

For a single developer, the recommended order minimizes context-switching:

1. Phase 1 (all subphases) — get UVs flowing
2. Phase 2A + 2B — data model and texture loading (CPU-side)
3. Phase 2C + 2D — GPU integration and UI (complete Phase 2)
4. Phase 3A — variable resolution paint masks (quick win, isolated)
5. Phase 4 (all subphases) — multi-material while GPU pipeline knowledge is fresh
6. Phase 3B + 3C — layer compositing and new channels (return to paint system)
7. Phase 5 (all subphases) — texture bake pulls together Phases 2–4
8. Phase 7A + 7B — graph data model and node library (can be done incrementally)
9. Phase 7C — graph evaluation (depends on 7A/7B)
10. Phase 7D — graph UI (last, largest, most deferrable)

Note: this interleaves Phase 3 into the middle rather than doing it strictly in order. This keeps the GPU pipeline work (Phases 1→2→4) in a continuous block while the developer's mental model of the Metal pipeline is active.

## Review Points

### Milestone Gates

Each milestone is a gate. Do not proceed to the next phase until the current milestone's verification checklist passes.

| Gate | Criteria | Verification Method |
|------|----------|-------------------|
| M1: UV Foundation | Vertex UVs flow through pipeline. GLB TEXCOORD_0 is read. No visual regression. | Render reference knob, pixel-compare before/after. Load GLB with known UVs, verify in debug output. |
| M2: Texture-Mapped Materials | Users can assign albedo/normal/roughness/metallic maps. Preview shows textured models. | Load a standard PBR texture set (e.g., Poly Haven material), verify each channel renders correctly. |
| M3: Professional Paint | Variable resolution masks, true layer compositing, roughness/metallic paint channels. | Create a 3-layer paint setup with different blend modes, verify compositor output against reference image. |
| M4: Multi-Material Models | Imported GLB with multiple materials renders correctly. | Load a GLB with 3+ materials, verify each material's textures bind to the correct mesh region. |
| M5: Full Material Pipeline | Composed texture maps bake and export as standalone images. | Bake a textured + painted material, verify output images open correctly in an external tool (e.g., Photoshop, Blender). |
| M6: Procedural Materials | Node graph produces procedural textures that drive the PBR output. | Create a Perlin noise → color ramp → PBROutput graph, verify the material renders correctly and bakes to a texture. |

### Mid-Phase Checkpoints

For phases longer than 2 weeks, insert a mid-phase checkpoint:

- **Phase 1 checkpoint** (after 1A + 1B): Does the app still run? Does the vertex stride change break anything? Run Metal validation layer.
- **Phase 3 checkpoint** (after 3A + 3B): Does layer compositing produce correct output for all blend modes? Memory usage acceptable at 2048x2048?
- **Phase 7 checkpoint** (after 7A + 7B): Can a simple graph be constructed in code and evaluated by the CPU evaluator? Serialization round-trips correctly?

## Schedule Risks and Contingency

| Risk | Impact on Schedule | Contingency |
|------|-------------------|-------------|
| MetalVertex stride change breaks render pipeline in non-obvious ways | +1–2 weeks to Phase 1 | Write a comprehensive test harness for vertex data before changing the struct. Run Metal validation layer on every build. |
| SkiaSharp texture loading has format gaps (e.g., EXR, 16-bit PNG) | +0.5 weeks to Phase 2 | Accept format limitations for v1. Document supported formats. Add format support incrementally. |
| Layer compositing blend modes have edge cases | +1 week to Phase 3 | Implement only Normal and Multiply blend modes for v1. Add others in a follow-up. |
| GPU shader compilation from node graph is harder than estimated | +2–4 weeks to Phase 7 | CPU-only evaluation is the fallback. Defer GPU compilation entirely if it threatens the Phase 7 timeline. |
| Visual graph editor UI is a tar pit | +2–4 weeks to Phase 7 | Ship with property-panel UI only. Visual editor becomes a separate project. |
