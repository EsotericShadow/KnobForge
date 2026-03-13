# Success Measures, KPIs, and Review Criteria

## Purpose

This document defines how we know the program is working — not in vague terms like "it feels better," but in concrete, testable assertions. Every metric here is either pass/fail or quantifiable.

## Program-Level Success Criteria

The program succeeds if a user can perform the following workflow end-to-end, which is impossible today:

1. Import a GLB model with multiple materials and UV coordinates
2. Assign PBR texture sets (albedo, normal, roughness, metallic) to each material
3. Paint directly on the model with variable-resolution brushes across multiple layers
4. Preview the fully composed material in real-time in the 3D viewport
5. Bake the composed material to standalone texture files for use in external tools
6. Export spritesheets exactly as before, with no regression in existing functionality

If all seven steps work, the program has achieved its target state. Phase 7 (node graph) adds procedural generation on top of this but is explicitly a power-user capstone, not a success criterion for the core material tool transformation.

## Per-Phase KPIs

### Phase 1: UV Infrastructure

| KPI | Target | Measurement |
|-----|--------|-------------|
| Visual regression | Zero pixel difference | Render the default knob before and after Phase 1. Pixel-diff must show zero change. |
| Vertex UV correctness | UVs in [0,1] range for all procedural geometry | Debug-render UV coordinates as color (R=U, G=V). Verify full [0,1] coverage on knob top face. |
| GLB UV import | TEXCOORD_0 values match source | Load a test GLB, extract UV values, compare against Blender's exported values. |
| Performance | No measurable frame rate change | Render benchmark scene before and after. Frame time delta < 0.5ms (within noise). The 8-byte vertex size increase should not measurably affect performance. |

### Phase 2: Texture Map Import

| KPI | Target | Measurement |
|-----|--------|-------------|
| Texture display correctness | Imported textures match source appearance | Load a known PBR set, compare viewport render against a Blender reference render of the same textures on the same geometry. Visual match within artistic tolerance (not pixel-exact due to different renderers). |
| Supported formats | PNG, JPEG, WebP, TIFF at minimum | Load test images in each format. All must display correctly. |
| Texture resolution | Up to 4096x4096 without crash | Load a 4K texture set. App must not crash, hang, or exceed 2 GB memory. |
| Normal map correctness | Tangent-space normals render correctly | Load a brick normal map. Light direction changes must produce correct shading on curved surfaces. This is the most common failure mode in normal map implementations. |
| Load time | < 2 seconds for a 4-texture 1024x1024 set | Measure from file path assignment to texture appearing in viewport. |

### Phase 3: Paint Upgrades

| KPI | Target | Measurement |
|-----|--------|-------------|
| Resolution flexibility | 512, 1024, 2048, 4096 all work | Create paint masks at each resolution. Paint, save, reload. No corruption. |
| Layer compositing accuracy | Blend modes match Photoshop/GIMP within ±1/255 per channel | Create a test composition with known colors and blend modes. Compare output against a Photoshop reference at the byte level. |
| Layer count | At least 8 layers without performance degradation | Create 8 layers with different content, toggle visibility, change blend modes. Compositor must update in < 100ms. |
| Memory usage | < 500 MB for 4 layers at 2048x2048 | Profile memory with 4 active layers. Each layer is 2048×2048×4 bytes = 16 MB raw, but compositor intermediates and GPU copies add overhead. |
| Undo/redo | Layer operations are undoable | Create layer, paint, change blend mode, undo each step. Verify state restoration. |

### Phase 4: Multi-Material

| KPI | Target | Measurement |
|-----|--------|-------------|
| Material assignment correctness | Each mesh primitive renders with its assigned material | Load a 3-material GLB. Verify each region shows the correct albedo color/texture. |
| Draw call count | One draw call per SubMesh | Use Metal GPU frame capture to verify draw call count equals SubMesh count. No redundant draws. |
| Material independence | Changing one material doesn't affect others | Modify material A's roughness. Verify material B's appearance is unchanged. |
| Performance | < 2ms frame time increase for 5-material model vs. 1-material | Benchmark with single material, then 5 materials. Difference must be small. |

### Phase 5: Texture Bake

| KPI | Target | Measurement |
|-----|--------|-------------|
| Bake correctness | Baked textures reproduce the viewport appearance | Bake a composed material. Re-import the baked textures as a texture set. Visual match must be high. |
| Output format | PNG export produces valid, loadable files | Open baked PNGs in Photoshop, Blender, GIMP. All must load without errors. |
| Resolution options | 512, 1024, 2048, 4096 all produce correct output | Bake at each resolution. Verify proportional detail scaling. |
| Bake time | < 10 seconds for 1024x1024 on CPU | Time the bake operation. If it exceeds 10 seconds, the GPU bake path (5C) becomes a priority. |
| Channel correctness | Each baked channel (albedo, normal, roughness, metallic) is independent and correct | Bake with a known material. Open each output image. Verify channel values match expected values. |

### Phase 6: Inspector Control Overhaul

| KPI | Target | Measurement |
|-----|--------|-------------|
| Control replacement completeness | Zero remaining SpriteKnobSlider instances | Grep codebase for `SpriteKnobSlider`. Must return zero matches. |
| Interaction fidelity | All 4 input modes work (text, arrows, drag, scroll) | Manual test each mode on at least 5 different controls across different sections. |
| Value accuracy | Every control reads/writes the correct project property | Adjust every section's controls, verify render updates match the property name. |
| Modifier keys | Fine (Shift/Cmd) and coarse (Alt) modifiers produce expected step scaling | Test modifier combinations on drag and arrow interactions. |
| Memory reduction | Inspector memory usage decreases vs. sprite sheet approach | Profile before/after — no more 2048×1664 sprite sheet bitmaps per control. |

### Phase 7: Node Graph

| KPI | Target | Measurement |
|-----|--------|-------------|
| Graph evaluation correctness | CPU evaluation matches expected output for all node types | Unit test each node type with known inputs. Verify outputs within floating-point epsilon. |
| Serialization round-trip | Graph serializes and deserializes without data loss | Create a complex graph (10+ nodes), save project, reload. Compare graph structure before and after. |
| Cycle detection | Cycles are detected and reported before evaluation | Attempt to create a cycle in the graph. Verify the system prevents it or reports an error before evaluation hangs. |
| Preview responsiveness | Parameter changes reflect in viewport within 500ms | Change a PerlinNoise scale parameter. Measure time until viewport updates. |
| Bake integration | Node graph materials bake correctly through Phase 5 pipeline | Create a procedural material (noise → color ramp → PBR output), bake it. Verify output textures. |

## Regression KPIs (Apply to Every Phase)

These must hold true after every phase completion:

| KPI | Target | Measurement |
|-----|--------|-------------|
| Existing projects load | All .knob projects created before the change load without error | Maintain a set of 3–5 reference project files. Load each after every phase. |
| Spritesheet export unchanged | Existing export presets produce identical output | Export using each of the 6 output strategy presets. Pixel-compare against pre-change reference exports. |
| No new crashes | Zero crash regressions | Run the app through a standard workflow (open project, rotate view, paint, export) after each phase. No crashes allowed. |
| Memory baseline | App idle memory < 200 MB (without large textures loaded) | Profile memory after startup with a simple project. No memory leaks from new subsystems. |

## Post-Phase Review Process

After each phase is marked complete:

1. **Verification checklist**: Run every item in the phase's verification checklist (defined in each phase document). All items must pass.
2. **Regression suite**: Run all regression KPIs above. All must pass.
3. **Performance benchmark**: Run the frame time benchmark. Compare against the previous phase's baseline. Any regression > 1ms requires investigation.
4. **Documentation update**: Update the status column in `00-PROGRAM.md`. Record the completion date and any deviations from the plan.
5. **Lessons learned**: Record what took longer than expected, what was easier than expected, and any architectural decisions that should be revisited. This feeds into schedule estimates for future phases.

## Post-Program Review

After the final milestone (M5 for core, M6 for full), conduct a full review:

1. **Did we hit the target state?** Can a user perform the 6-step workflow defined in Program-Level Success Criteria?
2. **What was the actual vs. estimated effort?** Compare per-phase actuals against the estimates in `08-SCHEDULE-AND-CRITICAL-PATH.md`.
3. **What architectural decisions should we revisit?** Are there shortcuts or compromises that need cleanup?
4. **What user feedback are we getting?** If the tool is in use, what do users actually want changed?
5. **What's next?** With the material pipeline complete, what features have the highest user value? (Likely: more node types, texture set presets, material library/sharing.)

## Quality Gates Summary

A quality gate is a mandatory checkpoint. Work on the next phase does not begin until the gate passes.

| Gate | Located After | Pass Criteria |
|------|---------------|--------------|
| G1 | Phase 1 | All Phase 1 KPIs pass + all regression KPIs pass |
| G2 | Phase 2 | All Phase 2 KPIs pass + all regression KPIs pass + G1 still passes |
| G3 | Phase 3 | All Phase 3 KPIs pass + all regression KPIs pass |
| G4 | Phase 4 | All Phase 4 KPIs pass + all regression KPIs pass + G2 still passes |
| G5 | Phase 5 | All Phase 5 KPIs pass + all regression KPIs pass + G3 and G4 still pass |
| G6 | Phase 6 | All Phase 6 KPIs pass + all regression KPIs pass + G5 still passes |
| G7 | Phase 7 | All Phase 7 KPIs pass + all regression KPIs pass + G6 still passes |

Note that G2 re-checks G1 and G4 re-checks G2 — this catches regressions introduced by later phases that silently break earlier guarantees. G5 re-checks both G3 and G4 because Phase 5 depends on work from both. G6 (Inspector Controls) is independent of the pipeline phases but still must not introduce regressions.
