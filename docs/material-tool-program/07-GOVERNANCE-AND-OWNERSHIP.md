# Governance, Ownership, and Communication

## Purpose

This document defines who owns what, how decisions get made, how work gets tracked, and how changes to the plan are controlled. A plan without governance is a wish list.

## Ownership Model

### Phase Ownership

Every phase has a single owner. The owner is responsible for all deliverables within the phase, including subphases they delegate. Ownership means: you decide task order within the phase, you flag blockers, you sign off on the verification checklist, and you take the blame if it ships broken.

| Phase | Owner Role | Rationale |
|-------|-----------|-----------|
| Phase 1: UV Infrastructure | GPU/Rendering Lead | Touches MetalVertex, shaders, pipeline descriptors — the most invasive structural change. Requires deep Metal and vertex pipeline knowledge. |
| Phase 2: Texture Map Import | GPU/Rendering Lead | Extension of Phase 1 work — texture slot binding, shader sampling, TextureManager. Same person avoids handoff friction. |
| Phase 3: Paint Upgrades | Paint System Lead | Isolated from GPU pipeline changes. Can run in parallel with Phase 2. Requires understanding of the paint mask CPU pipeline and layer compositing. |
| Phase 4: Multi-Material | GPU/Rendering Lead | Multi-draw render loop changes, per-SubMesh texture rebinding — continuation of GPU pipeline work from Phases 1-2. |
| Phase 5: Texture Bake | Paint System Lead or new contributor | CPU-side material evaluation, image export. Lower GPU coupling. Good onboarding point for a new contributor. |
| Phase 7: Node Graph | Dedicated owner or deferred | Largest single feature. Requires both graph algorithm knowledge and shader code generation. Consider deferring ownership assignment until Phase 5 is near completion. |

### Workstream Ownership

Workstreams cut across phases. Each workstream has a technical steward who ensures consistency across phases.

| Workstream | Steward Role | Responsibility |
|------------|-------------|----------------|
| A: GPU Pipeline | GPU/Rendering Lead | Vertex format consistency, shader correctness, Metal pipeline state management, texture binding conventions |
| B: Paint & Compositing | Paint System Lead | Paint mask format, layer compositing correctness, brush pipeline, memory management for variable-resolution masks |
| C: Data & UI | UI/Data Lead | MaterialNode property schema, project serialization backward compatibility, inspector UI consistency, export UI |

### Decision Authority Matrix

Not every decision needs the same level of approval.

| Decision Type | Who Decides | Examples |
|---------------|------------|----------|
| **Architecture** (irreversible, high-impact) | Phase owner + peer review | MetalVertex struct layout, texture slot allocation scheme, MaterialGraph serialization format |
| **Implementation** (reversible, contained) | Task owner | Which SkiaSharp API to use for image loading, specific UI control choice in Avalonia |
| **Scope change** (adding/removing features) | Program level | Adding a new node type to Phase 7, deferring GPU bake path, cutting a subphase |
| **Bug vs. feature** (is this in scope?) | Phase owner | "Should the texture loader handle DDS format?" — Phase 2 owner decides |
| **Emergency** (blocking regression, broken build) | Whoever finds it, immediate fix | Vertex stride mismatch crashes Metal pipeline — fix immediately, document later |

## Change Control

### What Requires a Change Request

Any modification to the following requires explicit documentation before implementation:

- MetalVertex struct layout (affects every mesh builder and shader)
- Texture slot assignments (affects all fragment shader code)
- GpuUniforms struct layout (affects CPU-GPU data contract)
- MaterialNode property schema (affects serialization, UI, and export)
- Project file format (affects backward compatibility)
- Phase dependencies (affects scheduling)

### Change Request Process

1. **Identify**: Document what you want to change and why
2. **Impact analysis**: List every file and subsystem affected (use the file impact tables in each phase document)
3. **Review**: Phase owner reviews. Architecture changes require a second reviewer.
4. **Approve or reject**: Decision recorded in commit message or PR description
5. **Implement**: Make the change
6. **Verify**: Run the verification checklist for the affected phase

### What Does NOT Require a Change Request

- Internal implementation details that don't affect public interfaces
- Adding tests
- Fixing bugs that don't change behavior
- Documentation updates
- UI polish within an existing inspector panel

## Tracking and Reporting

### Progress Tracking

Each phase document contains a verification checklist. Progress is measured by checklist completion, not by "percentage done" estimates.

| Tracking Level | Mechanism | Update Frequency |
|---------------|-----------|-----------------|
| Task | Git commits referencing task IDs (e.g., `[6A.1.1] Define port types`) | Per commit |
| Project | Phase verification checklist items | Per project completion |
| Phase | Milestone sign-off | Per phase completion |
| Program | Program-level status table in `00-PROGRAM.md` | Per phase completion |

### Status Definitions

| Status | Meaning |
|--------|---------|
| Not started | No code written, no design work begun |
| In progress | Active development, at least one task committed |
| Blocked | Cannot proceed — document the blocker and who owns unblocking |
| Review | Code complete, awaiting verification checklist sign-off |
| Complete | Verification checklist passed, milestone criteria met |

### Blocker Escalation

1. Task owner identifies blocker
2. Task owner notifies phase owner within 24 hours
3. Phase owner either resolves or escalates to program level
4. If blocker crosses phase boundaries (e.g., Phase 2 blocked on Phase 1 deliverable), both phase owners coordinate resolution
5. Unresolved blockers older than 1 week trigger a scope review: can the blocked work be restructured to proceed without the blocker?

## Communication Cadence

### For Solo Developer

If this is a solo project (likely given the codebase characteristics), governance simplifies to self-discipline:

- **Daily**: Review what you committed yesterday, what you're doing today, what's blocking you. Write it in a dev log or commit messages.
- **Per milestone**: Run the full verification checklist. Do not skip items. Record results.
- **Per phase**: Update the status column in `00-PROGRAM.md`. Review whether the next phase's assumptions still hold.

### For Team (2+ developers)

- **Weekly sync**: 15 minutes. Each phase owner reports: status, blockers, upcoming architectural decisions.
- **Per milestone**: Full verification checklist walkthrough. All affected parties present.
- **Architecture decisions**: Written proposal → 48-hour review window → decision recorded.

## Backward Compatibility Policy

### Project File Versioning

The `.knob` project file format will change in Phase 1 (new UV data), Phase 2 (texture paths), Phase 3 (layer data), and Phase 4 (multi-material associations). Each change must:

1. Increment a `formatVersion` field in the project JSON
2. Include migration logic: opening a v1 file in v2 code must either auto-migrate or show a clear error
3. Never silently discard data from older formats

### Rendering Regression Policy

Phase 1's verification checklist includes "existing rendering is identical (no visual regression)." This standard applies to every phase: new features must not change the appearance of existing projects that don't use the new features. Verification method: render a reference knob project before and after, compare output images pixel-by-pixel.
