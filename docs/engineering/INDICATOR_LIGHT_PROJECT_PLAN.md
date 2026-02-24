# Indicator Light Project Plan

## Objective
- Add a new interactor project type: `IndicatorLight`.
- Ship an industrial LED bezel workflow with real dynamic point lights from the emitter core.
- Keep controls and light behavior consistent across all project types.
- Preserve save/load compatibility and preview/export parity.

## Locked Product Decisions
- Default style: `Industrial LED bezel`.
- Flicker default: `Neutral (user tuned)`.
- Export default: `24-frame loop`.
- Point-light cap (v1): `8` active dynamic lights.
- Dynamic shadows from emitter lights (v1): `Off` (deferred).
- Cross-project shared dynamic lights default (v1): `Disabled`.
- Flicker profiles in v1: `Neutral + 3 presets`.

## Non-Negotiable Technical Requirements
- Emitter core in indicator mode drives **real point lights** in-scene (not emissive-only fake glow).
- Point lights affect viewport and export consistently.
- Preview frame state and export frame state use the same interactor timeline driver.
- Camera fitting and viewpoint application order remain identical between preview and export.

## Current Architecture Touchpoints
- Project type model:
  - `KnobForge.Core/KnobProject.cs`
- Project type switching UI:
  - `KnobForge.App/Views/MainWindow.ProjectTypeCommands.cs`
  - `KnobForge.App/Views/MainWindow.InteractorInspectorCapabilities.cs`
- Render/export frame-state drivers:
  - `KnobForge.Rendering/KnobExporter/KnobExporter.cs`
  - `KnobForge.App/Views/RenderSettingsWindow/RenderSettingsWindow.RotaryPreview.cs`
- GPU drawing and offscreen pass flow:
  - `KnobForge.App/Controls/MetalViewport.cs`
  - `KnobForge.App/Controls/MetalViewport/*.cs`

## Build/Decision Gate Policy
1. Decision gate before model/schema changes:
   - review migration impact and default behavior.
2. Build gate after each phase:
   - `dotnet build KnobForge.sln`.
3. Regression gate after each behavior change:
   - run `KnobForge.Regressions`.
4. Preview/export parity gate:
   - same project + same frame index should produce equivalent visual state.
5. Serialization gate:
   - legacy project files load without type corruption or value loss.

## Phase Plan

### Phase 0: Guardrails and Baseline
- Record baseline behavior for existing project types.
- Add/update regression matrix for type switching and round-trip persistence.
- Snapshot current preview/export parity checks for switch/slider/button.

### Phase 1: Shared Dynamic Light Domain
- Add project-agnostic light data model:
  - light nodes (position, color, intensity, radius, falloff, enabled).
  - animation envelope (steady/pulse/flicker/custom).
- Add serialization fields with safe defaults and backward compatibility.
- Keep all new features dormant by default.

### Phase 2: IndicatorLight Project Type Scaffolding
- Extend `InteractorProjectType` with `IndicatorLight`.
- Add defaults + migration logic in `KnobProject`.
- Add project type picker entry and project-type descriptions.
- Ensure inspector routing can show/hide indicator sections.

### Phase 3: Procedural Indicator Geometry
- Add procedural builders for:
  - dome lens,
  - side housing/bezel,
  - base,
  - inner reflector,
  - emitter core anchors (3 nodes).
- Add independent material regions for lens/housing/base/reflector/emitter visuals.

### Phase 4: Dynamic Point Light Integration (Renderer)
- Feed indicator emitter nodes into shared scene dynamic lights.
- Apply dynamic point lights in main viewport + offscreen render path.
- Keep hard cap at 8 lights and skip overflow deterministically.
- Leave per-emitter dynamic shadowing disabled for v1.

### Phase 5: Timeline Driver Unification
- Introduce shared interactor frame-state abstraction (single source of truth).
- Route preview and export to that same driver for:
  - rotary rotation,
  - switch states,
  - slider thumb travel,
  - button press depth,
  - indicator light animation/flicker.

### Phase 6: Indicator UX and Controls
- Add indicator-specific sections:
  - Geometry: dome, bezel/housing, base.
  - Emitters: node spacing, depth, color/intensity/radius.
  - Animation: steady/pulse/flicker/custom.
  - Flicker controls: speed/amount/dropout/smoothing/seed.
- Add 3 preset buttons plus neutral default.

### Phase 7: Export Semantics
- Indicator export defaults:
  - `FrameCount = 24`,
  - time-based loop.
- Validate non-empty frame output and stable loop endpoints.
- Keep existing rotary/switch/slider/button export semantics unchanged.

### Phase 8: Cross-Project Shared Lighting Controls
- Add a reusable "Dynamic Lights" section for all project types.
- Keep default disabled to avoid accidental scene changes.
- Ensure shared control labels and units are identical across types.

## Risks and Mitigations
- Risk: preview/export mismatch.
  - Mitigation: one frame-state driver + parity tests.
- Risk: performance drop with dynamic lights.
  - Mitigation: hard light cap + deterministic culling + disabled-by-default.
- Risk: serialization regressions.
  - Mitigation: additive schema + migration tests + round-trip checks.
- Risk: inspector complexity.
  - Mitigation: strict section capability map by project type.

## Success Criteria
- IndicatorLight projects render and export with real emitter-driven dynamic lighting.
- Existing project types remain behaviorally stable.
- Regression suite remains green with added indicator coverage.
- No camera-dependent blank-frame regressions introduced.
