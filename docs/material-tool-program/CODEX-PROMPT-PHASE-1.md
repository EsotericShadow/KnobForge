# Codex Implementation Prompt — Phase 1: UV Infrastructure

## Your Role

You are implementing Phase 1 of the KnobForge Material Tool Transformation. Your job is to add UV coordinate support to the Metal rendering pipeline. Work incrementally — complete each subphase, verify it compiles and runs, then move to the next. Do not skip verification steps. Do not refactor unrelated code. Do not change any rendering behavior — the output must be visually identical after every subphase.

## Project Context

KnobForge is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs and UI components for audio plugins. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

The rendering pipeline uses Metal via raw P/Invoke interop (no managed wrapper). Shaders are inline C# strings compiled at runtime. There is no MTLVertexDescriptor — vertex data is passed via `device const MetalVertex* vertices [[buffer(0)]]` and indexed manually.

## What You're Changing and Why

The Metal vertex struct (`MetalVertex`) currently has no UV coordinates. The fragment shader derives UVs from world position: `float2 uv = inVertex.worldPos.xy / (topRadius * 2.0) + 0.5`. This works for the knob top face but is wrong for imported GLB meshes and non-cylindrical geometry. You are adding a `packed_float2 texcoord` field to the vertex struct so proper UVs can flow from mesh builders through the vertex shader to the fragment shader.

## Critical Constraints

1. **ZERO VISUAL REGRESSION.** After every subphase, the viewport rendering, paint system, and spritesheet export must produce pixel-identical results. If anything looks different, you introduced a bug — stop and fix it.
2. **Do not change texture bindings.** The 4 texture slots (0=spiralNormalMap, 1=paintMask, 2=paintColor, 3=environmentMap) and 2 buffer slots (0=vertices, 1=uniforms) must remain exactly as they are.
3. **Do not change GpuUniforms.** The uniform struct is not touched in this phase.
4. **Do not change the paint stamp pipeline.** The paint stamp shaders (`MetalViewport.Shaders.cs`) have their own vertex types (`PaintPickVertexOut`, `PaintStampVertexOut`) that are separate from the main pipeline. Do not change these unless required for correctness.
5. **Build after every task.** If it doesn't compile, fix it before moving on.

## Current State (Verified)

### MetalVertex (shader-side) — 40 bytes
```
File: KnobForge.Rendering/GPU/MetalPipelineManager/MetalPipelineManager.Shaders.cs (lines 11-16)

struct MetalVertex
{
    packed_float3 position;   // offset 0,  12 bytes
    packed_float3 normal;     // offset 12, 12 bytes
    packed_float4 tangent;    // offset 24, 16 bytes
};                            // total: 40 bytes
```

**IMPORTANT:** MetalVertex is also defined in `KnobForge.App/Controls/MetalViewport/MetalViewport.Shaders.cs` (lines 9-14) for the paint pick shader. Both definitions must stay in sync.

### VertexOut (shader-side)
```
File: MetalPipelineManager.Shaders.cs (lines 66-72)

struct VertexOut
{
    float4 position [[position]];
    float3 worldPos;
    float3 worldNormal;
    float4 worldTangentSign;
};
```

### C# MetalVertex struct
```
[StructLayout(LayoutKind.Sequential)]
public readonly struct MetalVertex
{
    public Vector3 Position { get; init; }   // 12 bytes
    public Vector3 Normal { get; init; }     // 12 bytes
    public Vector4 Tangent { get; init; }    // 16 bytes
}                                            // total: 40 bytes
```

### Fragment shader UV derivation (the line you will eventually replace)
```
float2 uv = inVertex.worldPos.xy / (topRadius * 2.0) + 0.5;
```

### Fragment shader signature (4 texture slots — DO NOT CHANGE)
```
fragment float4 fragment_main(
    VertexOut inVertex [[stage_in]],
    constant GpuUniforms& uniforms [[buffer(1)]],
    texture2d<float> spiralNormalMap [[texture(0)]],
    texture2d<float> paintMask [[texture(1)]],
    texture2d<float> paintColor [[texture(2)]],
    texture2d<float> environmentMap [[texture(3)]])
```

### Vertex shader uses implicit binding (no vertex descriptor)
The vertex shader reads vertices as: `device const MetalVertex* vertices [[buffer(0)]]`
Vertices are indexed manually: `MetalVertex v = vertices[vid];`
There is NO MTLVertexDescriptor. The stride is implicit from the struct size.

### CollarMesh already has separate UVs
```csharp
public sealed class CollarMesh
{
    public MetalVertex[] Vertices { get; init; }
    public uint[] Indices { get; init; }
    public Vector2[] UVs { get; init; }        // ← UVs exist but are NOT in MetalVertex
    public Vector4[] Tangents { get; init; }
    public float ReferenceRadius { get; init; }
}
```
These UVs need to be merged INTO MetalVertex.Texcoord rather than stored separately.

### GLB import currently reads POSITION only
```
File: ImportedStlCollarMeshBuilder.Glb.cs

✓ POSITION  — read via TryReadAccessorVector3
✓ Indices   — read via TryReadAccessorIndices
✗ NORMAL    — computed from geometry
✗ TANGENT   — computed from geometry
✗ TEXCOORD_0 — NOT read (cylindrical UVs computed instead)
```

### All mesh builders that create MetalVertex arrays (you must update ALL of these):
- `MetalMeshBuilder.cs` — main knob body
- `MetalMesh.Detail.cs` — indicator hard walls
- `CollarMeshBuilder.cs` — base collar
- `OuroborosCollarMeshBuilder/` — ouroboros collar (multiple partial files)
- `ImportedStlCollarMeshBuilder.cs` — imported GLB collar
- `IndicatorAssemblyMeshBuilder.cs` — indicator assembly
- `SliderAssemblyMeshBuilder.cs` — slider assembly
- `ToggleAssemblyMeshBuilder.cs` — toggle assembly
- `PushButtonAssemblyMeshBuilder.cs` — push button assembly

Search for `new MetalVertex` and any struct initializer pattern to find every construction site. Do not assume this list is complete — verify by searching the codebase.

## Execution Order

### SUBPHASE 1A: Vertex Format Extension (do this FIRST, verify, then continue)

**Step 1: Add texcoord to MSL MetalVertex (BOTH files)**

In `MetalPipelineManager.Shaders.cs` AND `MetalViewport.Shaders.cs`, add `packed_float2 texcoord;` after `packed_float4 tangent;`:

```metal
struct MetalVertex
{
    packed_float3 position;
    packed_float3 normal;
    packed_float4 tangent;
    packed_float2 texcoord;   // NEW — 8 bytes, total now 48
};
```

**Step 2: Add Texcoord to C# MetalVertex struct**

Add `public Vector2 Texcoord { get; init; }` to the C# struct. Verify `Marshal.SizeOf<MetalVertex>()` == 48 (add a debug assertion or console print).

**Step 3: Add texcoord to VertexOut and vertex_main**

VertexOut gets `float2 texcoord;`. The vertex shader gets `out.texcoord = float2(v.texcoord);`.

**Step 4: Zero-fill ALL mesh builders**

Every place that constructs a MetalVertex must now include `Texcoord = Vector2.Zero`. Search the entire codebase for MetalVertex construction. This is the tedious but critical step — if you miss one, the stride mismatch will crash the Metal pipeline or produce garbage rendering.

For `CollarMesh` builders that store UVs in a separate array: for now, set `Texcoord = Vector2.Zero` in the MetalVertex. We'll merge the real UVs in Subphase 1B.

**Step 5: BUILD AND VERIFY**

The app must compile, launch, and render identically to before. Run the app, open a knob project, rotate the viewport, paint a stroke, export a spritesheet. Everything must work. If anything is wrong, the vertex stride is mismatched somewhere — check that every MetalVertex construction includes the Texcoord field and that both MSL definitions match the C# struct.

### SUBPHASE 1B: Procedural UV Generation (after 1A is verified)

Replace the `Vector2.Zero` texcoords with real UV values in each mesh builder:

- **Knob top cap**: `u = localX / (topRadius * 2) + 0.5`, `v = localY / (topRadius * 2) + 0.5` — this MUST match the current shader derivation exactly so paint masks don't shift.
- **Knob side wall**: `u = atan2(y, x) / (2π) + 0.5`, `v = (z - minZ) / (maxZ - minZ)` — cylindrical unwrap.
- **Knob bevel**: Interpolate between cap and side UVs based on bevel factor.
- **Collar meshes**: Merge the existing `CollarMesh.UVs[]` into `MetalVertex.Texcoord`. The UVs are already computed — just put them in the right place.
- **Assembly meshes** (indicator, slider, toggle, pushbutton): Planar projection for flat faces, cylindrical for round parts.

**VERIFY:** Paint strokes must land in exactly the same positions. Render before/after comparison must show zero difference for procedural knob geometry.

### SUBPHASE 1C: GLB UV Import (after 1B is verified)

Add TEXCOORD_0 reading to the GLB importer:

1. Add a `TryReadAccessorVector2` method following the pattern of the existing `TryReadAccessorVector3` (same binary accessor logic, but reading 2 floats instead of 3).
2. In the GLB primitive parsing, check for `TEXCOORD_0` in the attributes dictionary. If present, read it with `TryReadAccessorVector2`.
3. When building MetalVertex arrays from imported GLB data, use the imported UVs if available. Fall back to the existing cylindrical computation if TEXCOORD_0 is absent.
4. Also read NORMAL from GLB when present (follow existing TryReadAccessorVector3 pattern). Use imported normals instead of computing them from geometry. This prevents normal map issues in Phase 2.

**VERIFY:** Import a GLB from Blender that has authored UV coordinates. Debug-print or debug-render the UV values (R=U, G=V as vertex color). Verify they match the expected UV layout. Import an STL or GLB without UVs — verify the cylindrical fallback still works.

### SUBPHASE 1D: Fragment Shader UV Migration (after 1C is verified)

Switch the fragment shader from derived UVs to vertex UVs:

1. In `fragment_main`, replace: `float2 uv = inVertex.worldPos.xy / (topRadius * 2.0) + 0.5;` with: `float2 uv = inVertex.texcoord;`
2. Keep the old line as a comment: `// OLD: float2 uv = inVertex.worldPos.xy / (topRadius * 2.0) + 0.5;`
3. Check if the vertex shader also derives UVs for scratch displacement or paint mask vertex-stage sampling. If so, update those too.
4. Check `StampPaintMaskUv` on the CPU side — this computes UV from a mouse ray intersection. Verify it computes UVs consistent with the vertex UVs. If it uses the same `worldPos / (topRadius * 2) + 0.5` formula, it should still match because 1B.1.1 uses the same formula for the top cap.

**VERIFY (FULL PHASE 1 VERIFICATION):**
- [ ] App launches without crash
- [ ] Viewport renders identically to before Phase 1
- [ ] Paint strokes land correctly on procedural knob geometry
- [ ] Paint strokes land correctly on imported GLB geometry
- [ ] Scratch carve displacement still works
- [ ] Spritesheet export produces pixel-identical output for existing .knob projects
- [ ] Imported GLB with authored UVs displays correct UV mapping (debug render)
- [ ] Imported GLB without UVs falls back to cylindrical projection
- [ ] `Marshal.SizeOf<MetalVertex>()` == 48

## What NOT to Do

- Do not add texture map import (that's Phase 2)
- Do not change the paint mask resolution (that's Phase 3)
- Do not add multi-material support (that's Phase 4)
- Do not refactor the shader string into separate files
- Do not introduce new NuGet dependencies
- Do not change the project file format (.knob serialization)
- Do not change the export pipeline format logic
- Do not optimize anything — correctness first
- Do not rename existing variables or methods unless required for the new field

## After Phase 1 Is Complete

Update `docs/material-tool-program/00-PROGRAM.md` — change Phase 1 status from "Not started" to "Complete". Then read `docs/material-tool-program/02-PHASE-2-TEXTURE-MAP-IMPORT.md` for the next phase.
