# KnobForge Material Tool Program

## Program Overview

Transform KnobForge from a specialized knob spritesheet renderer into a professional 3D material authoring tool, while preserving the existing spritesheet export workflow that is core to the audio plugin UI market.

## Program Identity

- **Program Name**: KnobForge Material Tool Transformation
- **Codename**: MTT
- **Repository**: KnobForge (monorepo, .NET 8 / Avalonia / Metal)

## Current State

KnobForge is a macOS-first desktop application that produces spritesheet filmstrips for audio plugin UIs (JUCE, iPlug2, HISE, Kontakt). Materials are slider-driven scalars (color, metallic, roughness). Texturing is limited to a 1024x1024 4-channel paint mask (rust/wear/gunk/scratch) and a procedural spiral normal map. There are no texture map imports, no real UV coordinates, no node-based materials, no texture export, and no multi-material support for imported meshes.

## Target State

A tool where users can import PBR texture sets, paint directly on 3D models with proper UV-aware brushes, compose materials from multiple texture layers, assign multiple materials to imported GLB mesh parts, bake composed texture maps for external use, and optionally build procedural materials through a node graph. All while retaining the existing spritesheet export pipeline.

## Phase Summary

| Phase | Name | Dependency | Risk | Status |
|-------|------|------------|------|--------|
| 1 | UV Infrastructure | None (foundation) | High | Complete |
| 2 | Texture Map Import | Phase 1 | Medium | Complete |
| 3 | Paint System Upgrades | Phase 1 | Low | Complete |
| 4 | Multi-Material Support | Phase 2 | Medium | Not started |
| 5 | Texture Bake Pipeline | Phases 2, 3, 4 | Low | Not started |
| 6 | Inspector Control Overhaul | None (independent) | Low | Not started |
| 7 | Node-Based Material Graph | Phases 1-5 | High | Not started |

## Workstreams

Work is organized into three parallel workstreams that interleave across phases:

- **Workstream A: GPU Pipeline** — Vertex format, shader changes, texture binding, render loop modifications. This is the critical path through Phases 1, 2, and 4.
- **Workstream B: Paint & Compositing** — Paint mask resolution, layer system, blend modes, new channels. Primarily Phase 3, with prep work in Phase 1.
- **Workstream C: Data & UI** — MaterialNode properties, project serialization, inspector UI, export UI. Spans all phases.

## Dependency Graph

```
Phase 1: UV Infrastructure
    |
    +---> Phase 2: Texture Map Import ----> Phase 4: Multi-Material
    |                                              |
    +---> Phase 3: Paint Upgrades                  |
    |         |                                    |
    |         v                                    v
    +-------> Phase 5: Texture Bake <--------------+
                  |
                  v
              Phase 6: Inspector Controls (independent — can run anytime)
                  |
                  v
              Phase 7: Node Graph
```

## Milestone Definitions

- **M1: UV Foundation** — Phase 1 complete. Vertex UVs flow through pipeline. GLB TEXCOORD_0 is read. Procedural knob geometry has proper UVs. Existing rendering is identical (no visual regression).
- **M2: Texture-Mapped Materials** — Phase 2 complete. Users can assign albedo/normal/roughness/metallic maps to materials. Real-time preview shows textured models.
- **M3: Professional Paint** — Phase 3 complete. Variable resolution paint masks, true layer compositing with blend modes, roughness/metallic paint channels.
- **M4: Multi-Material Models** — Phase 4 complete. Imported GLB meshes with multiple materials render correctly with per-material textures.
- **M5: Full Material Pipeline** — Phase 5 complete. Composed texture maps can be baked and exported as standalone image files.
- **M6: Professional Inspector** — Phase 6 complete. All sprite knob sliders replaced with compact Choroboros-style value inputs (text + arrows + drag + scroll).
- **M7: Procedural Materials** — Phase 7 complete. Node-based material graph with procedural texture generation.

## Minimum Viable Material Tool

**Phases 1 + 2** constitute the minimum viable material tool. With vertex UVs and texture map import, users can bring external PBR texture sets into KnobForge. This alone transforms the tool from "every knob looks like KnobForge" to "bring your own textures."

## Document Index

### Phase Breakdowns (Work Breakdown Structure)

| Document | Contents |
|----------|----------|
| `00-PROGRAM.md` | This file — program overview, phases, milestones |
| `01-PHASE-1-UV-INFRASTRUCTURE.md` | Phase 1: Subphases, projects, tasks, subtasks, file impacts, verification checklist |
| `02-PHASE-2-TEXTURE-MAP-IMPORT.md` | Phase 2: Subphases, projects, tasks, subtasks, file impacts, verification checklist |
| `03-PHASE-3-PAINT-UPGRADES.md` | Phase 3: Subphases, projects, tasks, subtasks, file impacts, verification checklist |
| `04-PHASE-4-MULTI-MATERIAL.md` | Phase 4: Subphases, projects, tasks, subtasks, file impacts, verification checklist |
| `05-PHASE-5-TEXTURE-BAKE.md` | Phase 5: Subphases, projects, tasks, subtasks, file impacts, verification checklist |
| `06-PHASE-6-INSPECTOR-CONTROLS.md` | Phase 6: Inspector control overhaul — replace SpriteKnobSliders with Choroboros ValueInput |
| `07-PHASE-7-NODE-GRAPH.md` | Phase 7: Subphases, projects, tasks, subtasks, file impacts, verification checklist |

### Program Management

| Document | Contents |
|----------|----------|
| `07-GOVERNANCE-AND-OWNERSHIP.md` | Ownership model, decision authority, change control, tracking, communication cadence, backward compatibility policy |
| `08-SCHEDULE-AND-CRITICAL-PATH.md` | Effort estimates, critical path analysis, parallelism opportunities, solo vs. team sequencing, review points, schedule risks |
| `09-RESOURCES-AND-TOOLING.md` | Required skills, capacity analysis, development tools, test assets, external dependencies, hardware requirements, knowledge gaps |
| `10-SUCCESS-MEASURES-AND-KPIS.md` | Program-level success criteria, per-phase KPIs, regression KPIs, quality gates, post-phase and post-program review process |
| `11-SCOPE-AND-BOUNDARIES.md` | Business case, in-scope/out-of-scope decisions with rationale, assumptions, constraints, deferral criteria |
| `12-PRE-IMPLEMENTATION-RESEARCH.md` | Verified technical findings, codebase audit results, format support research, risk corrections, blockers list |
| `CODEX-PROMPT-PHASE-1.md` | Self-contained implementation prompt for Phase 1 — hand to Codex or any coding agent |
| `CODEX-PROMPT-PHASE-2.md` | Self-contained implementation prompt for Phase 2 — hand to Codex or any coding agent |
| `CODEX-PROMPT-PHASE-3.md` | Self-contained implementation prompt for Phase 3 — hand to Codex or any coding agent |

### Planning Framework Coverage

| Planning Dimension | Primary Document | Supporting Documents |
|-------------------|-----------------|---------------------|
| Objective / Goals | `00-PROGRAM.md` (Target State) | `11-SCOPE-AND-BOUNDARIES.md` (Business Case) |
| Scope / Boundaries | `11-SCOPE-AND-BOUNDARIES.md` | `00-PROGRAM.md` (Minimum Viable Material Tool) |
| Requirements | `01`–`06` phase docs (per-task specifications) | `09-RESOURCES-AND-TOOLING.md` (Test Assets) |
| Deliverables | `01`–`06` phase docs (New Files tables) | `10-SUCCESS-MEASURES-AND-KPIS.md` (what "done" looks like) |
| Work Breakdown | `01`–`06` phase docs (Subphase → Project → Task → Subtask) | `00-PROGRAM.md` (Workstreams) |
| Schedule / Timeline | `08-SCHEDULE-AND-CRITICAL-PATH.md` | `00-PROGRAM.md` (Dependency Graph) |
| Resources / Capacity | `09-RESOURCES-AND-TOOLING.md` | `08-SCHEDULE-AND-CRITICAL-PATH.md` (Capacity Analysis) |
| Risks / Constraints | `11-SCOPE-AND-BOUNDARIES.md` (Assumptions, Constraints) | `08-SCHEDULE-AND-CRITICAL-PATH.md` (Schedule Risks), each phase doc (Risk field) |
| Governance / Tracking | `07-GOVERNANCE-AND-OWNERSHIP.md` | `10-SUCCESS-MEASURES-AND-KPIS.md` (Quality Gates) |
| Success Measures / KPIs | `10-SUCCESS-MEASURES-AND-KPIS.md` | Each phase doc (Verification Checklist) |
| Ownership / Roles | `07-GOVERNANCE-AND-OWNERSHIP.md` | `09-RESOURCES-AND-TOOLING.md` (Required Skills) |

## Technical Constraints

- Metal GPU backend only (no Vulkan/OpenGL). Texture slots limited to 31 per stage (ample headroom).
- SkiaSharp for CPU image loading/processing. No ImageSharp or System.Drawing dependency.
- Avalonia 11.x for UI. No WPF or WinForms dependencies.
- .NET 8 target framework. C# nullable reference types enabled throughout.
- Project files are JSON-serialized via System.Text.Json. Binary blobs (paint masks) are inline byte arrays.
- The `MetalVertex` struct change (Phase 1) is the single most invasive change in the entire program. Every mesh builder, the pipeline descriptor, and the vertex shader all depend on the vertex stride being exactly correct.
