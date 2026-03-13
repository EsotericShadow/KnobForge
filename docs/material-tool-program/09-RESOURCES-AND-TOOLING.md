# Resources, Capacity, and Tooling

## Purpose

This document answers: what does this program require to execute? People, tools, materials, test assets, hardware, and knowledge. It also identifies capacity constraints — the bottlenecks that limit how fast work can proceed.

## Human Resources

### Required Skills

| Skill | Where Needed | Depth Required |
|-------|-------------|----------------|
| Metal GPU programming | Phases 1, 2, 4, 7C | Deep — custom shader authoring, pipeline state management, texture binding, compute shaders. Not a managed wrapper; this is raw P/Invoke interop with Objective-C Metal APIs. |
| C# / .NET 8 | All phases | Strong — nullable reference types, System.Text.Json custom converters, unsafe code for vertex data, span-based memory management. |
| Avalonia 11.x | Phases 2D, 3B, 4D, 5B, 7D | Moderate to deep — standard controls for inspectors, custom Canvas control for visual graph editor. |
| SkiaSharp | Phases 2B, 3, 5 | Moderate — image loading, pixel manipulation, format conversion. Already used in the codebase. |
| glTF/GLB binary format | Phases 1C, 4A | Moderate — accessor reading, multi-primitive parsing. Existing code handles POSITION; extending to TEXCOORD_0 and multi-material is incremental. |
| Graph algorithms | Phase 7 | Moderate — topological sort, cycle detection, DAG evaluation. Standard CS fundamentals. |
| Shader code generation | Phase 7C | Specialized — compiling a node graph into Metal Shading Language fragments. Rare skill. |
| PBR rendering theory | Phases 2, 5, 6 | Moderate — understanding of albedo/normal/roughness/metallic workflows, tangent-space normal mapping, material composition. |

### Capacity Analysis

| Scenario | Estimated Calendar Time | Notes |
|----------|------------------------|-------|
| 1 developer, full-time | 22–31 weeks (5–8 months) | Assumes no major blockers. Phase 7 is the wildcard. |
| 1 developer, half-time | 44–62 weeks (10–15 months) | Context-switching overhead adds ~10–15% on top of the 2x multiplier. |
| 2 developers, full-time | 14–20 weeks (3.5–5 months) | Parallelism gains from Phase 3 running alongside Phases 2/4. Limited by critical path through GPU pipeline. |

### Bottleneck: Metal Expertise

The single biggest capacity constraint is Metal GPU knowledge. Phases 1, 2, 4, and 7C all require hands-on Metal shader and pipeline work. This cannot be parallelized across developers unless both developers have Metal expertise. In a two-developer scenario, the second developer handles Paint/UI work (Phases 3, 5B, 7A/6B/7D) while the Metal-skilled developer handles the GPU pipeline critical path.

## Development Tools

### Required (must have before starting)

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 8.0+ | Build and run the application |
| Rider or Visual Studio for Mac | Current | IDE with C# support, debugging |
| Xcode Command Line Tools | Current | Metal shader compiler (metallib), system frameworks |
| macOS | 13+ (Ventura) | Metal 3 support, target platform |
| Git | Current | Version control |

### Recommended (significantly helps)

| Tool | Purpose |
|------|---------|
| Metal Debugger (Xcode GPU Tools) | GPU frame capture, shader debugging, texture inspection. Essential for diagnosing vertex stride issues in Phase 1 and texture binding issues in Phase 2. |
| RenderDoc (if available for Metal) or GPU profiler | Performance profiling for multi-draw calls (Phase 4) and shader compilation timing (Phase 7). |
| Image comparison tool | Pixel-diff tool for visual regression testing. ImageMagick `compare` works. |
| glTF Viewer (e.g., Khronos glTF Viewer) | Verify GLB test assets render correctly before importing into KnobForge. |
| Blender | Create test GLB assets with known UV layouts, multi-material setups, and reference renders for comparison. |

## Test Assets

Each phase requires specific test assets. These should be created or sourced before development begins to avoid blocking testing.

### Phase 1 Test Assets

| Asset | Description | Source |
|-------|-------------|--------|
| `test-knob-reference.png` | Pixel-exact render of the default knob with current code. Used for regression comparison after UV changes. | Render from current build before starting Phase 1. |
| `test-cube-with-uvs.glb` | Simple cube with explicit TEXCOORD_0 data and a checkerboard texture. | Create in Blender. |
| `test-cylinder-with-uvs.glb` | Cylinder with cylindrical UV mapping. Verifies the procedural UV generation matches expectations. | Create in Blender. |

### Phase 2 Test Assets

| Asset | Description | Source |
|-------|-------------|--------|
| Standard PBR texture set | Albedo + normal + roughness + metallic maps for a single material. 1024x1024 PNG. | Download from Poly Haven, ambientCG, or similar CC0 source. |
| Edge case textures | Non-square textures, NPOT dimensions, 16-bit PNG, JPEG, WebP — to test loader robustness. | Create or source manually. |

### Phase 3 Test Assets

| Asset | Description | Source |
|-------|-------------|--------|
| Reference layer composite | A Photoshop/GIMP file with known layers, blend modes, and opacities. The expected composite result. | Create manually. Used to verify KnobForge's layer compositor matches industry-standard blending. |

### Phase 4 Test Assets

| Asset | Description | Source |
|-------|-------------|--------|
| Multi-material GLB | A GLB with 3+ primitives, each assigned a different material with distinct textures. | Create in Blender. Export with glTF settings that produce separate primitives. |
| Mixed GLB | A GLB where some primitives have textures and others use flat colors. Tests mixed material handling. | Create in Blender. |

### Phase 5 Test Assets

| Asset | Description | Source |
|-------|-------------|--------|
| Known-output material | A material with specific scalar values and textures whose baked output can be computed by hand. | Create from Phase 2 test assets with specific parameter values. |

### Phase 7 Test Assets

| Asset | Description | Source |
|-------|-------------|--------|
| Reference noise images | Perlin noise, Voronoi, checkerboard patterns at known parameters. Used to verify node evaluation correctness. | Generate with a reference implementation (Python/NumPy). |

## External Dependencies

| Dependency | Used By | Risk |
|-----------|---------|------|
| SkiaSharp NuGet package | Phases 2, 3, 5 | Low — already in the project, stable, well-maintained. |
| System.Text.Json | All phases (serialization) | Low — part of .NET 8 SDK. |
| Avalonia 11.x NuGet package | All phases (UI) | Low — already in the project. Custom Canvas control for Phase 7D may require deeper Avalonia knowledge. |
| Metal system framework | Phases 1, 2, 4, 7C | Low — ships with macOS. No version risk. |
| glTF/GLB spec | Phases 1C, 4A | Low — stable specification. KnobForge already partially implements it. |

No new external NuGet packages are required for Phases 1–5. Phase 7 might benefit from a graph layout library for the visual editor (7D.2), but this is optional and can be deferred.

## Hardware Requirements

### Development

- macOS machine with Apple Silicon or Intel + discrete GPU (for Metal development and testing)
- Minimum 16 GB RAM (Phase 3 variable-resolution paint masks at 4096x4096 × multiple layers can consume significant memory)

### Testing

- Target the same hardware as development. KnobForge is a macOS-only application.
- Test on both Apple Silicon and Intel Macs if possible — Metal behavior can differ between GPU architectures.

## Knowledge Gaps and Learning Investment

| Gap | Investment Needed | When |
|-----|-------------------|------|
| Metal texture binding beyond 4 slots | Low — straightforward extension of existing code, just need to verify slot numbering and argument buffer limits | Before Phase 2C |
| Tangent-space normal map sampling in Metal | Medium — correct TBN matrix construction, handedness handling, mikktspace conventions | Before Phase 2C (normal map integration) |
| Metal compute shaders | Medium — only needed if GPU bake path (5C) or GPU node evaluation (7C) is pursued | Before Phase 5C or 7C |
| Graph algorithms (topological sort, cycle detection) | Low — standard algorithms, well-documented | Before Phase 7A |
| Metal shader code generation from a DAG | High — novel work, no off-the-shelf solution for Metal. Requires understanding MSL syntax, variable naming, type coercion. | Before Phase 7C.2 |
