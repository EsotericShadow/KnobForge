# Pre-Implementation Research

## Purpose

This document catalogs every technical question that must be answered with backed-up evidence before implementation begins. Each section presents the question, the research findings, the verified facts, the risks exposed by the findings, and any corrections to the planning documents.

---

## Research Area 1: Metal Texture Binding Limits

### Question

The plan assumes slots 4–7 are available for material textures (albedo, normal, roughness, metallic). Is this safe? What are the actual per-stage limits?

### Verified Facts

| GPU Family | Direct Texture Bindings Per Stage | Sampler States Per Stage |
|-----------|----------------------------------|-------------------------|
| Apple1–Apple6 (pre-A14/M1) | **31** | 16 |
| Apple7+ (A14/M1 and later) | **96** | 16 |

Current KnobForge uses 4 texture slots (0–3) and the plan adds 4 more (4–7), totaling 8. This is well within the 31-slot minimum on even the oldest Metal-capable hardware.

If argument buffers are used (not currently in the codebase), the limits jump to 128+ textures on Tier 1 and 500,000+ on Tier 2.

### Current Codebase State (Verified by Audit)

```
Slot 0: spiralNormalMap     (fragment texture(0))
Slot 1: paintMask           (fragment texture(1), also vertex texture(1))
Slot 2: paintColor          (fragment texture(2))
Slot 3: environmentMap      (fragment texture(3))

Buffer 0: MetalVertex array (vertex buffer only)
Buffer 1: GpuUniforms       (both vertex and fragment)
```

No argument buffers. No compute shaders. Direct binding only.

### Risk Assessment

**Low risk.** 8 total slots out of a 31-slot minimum is comfortable. The 16-sampler limit is the tighter constraint — we need at most 8 samplers (one per texture), which is fine.

### Corrections to Plan

None needed. The plan's assumption of slots 4–7 being available is confirmed correct.

### Source

[Metal Feature Set Tables (Apple Developer)](https://developer.apple.com/metal/Metal-Feature-Set-Tables.pdf)

---

## Research Area 2: MetalVertex Stride Change (40 → 48 bytes)

### Question

The plan adds `packed_float2 texcoord` to MetalVertex, changing the struct from 40 to 48 bytes. What are Metal's alignment requirements for packed types? Will this cause issues?

### Verified Facts

**Current MetalVertex layout (shader-side):**
```metal
struct MetalVertex {
    packed_float3 position;   // offset 0,  size 12, alignment 4
    packed_float3 normal;     // offset 12, size 12, alignment 4
    packed_float4 tangent;    // offset 24, size 16, alignment 4
};                            // total: 40 bytes
```

**Proposed MetalVertex layout:**
```metal
struct MetalVertex {
    packed_float3 position;   // offset 0,  size 12, alignment 4
    packed_float3 normal;     // offset 12, size 12, alignment 4
    packed_float4 tangent;    // offset 24, size 16, alignment 4
    packed_float2 texcoord;   // offset 40, size 8,  alignment 4
};                            // total: 48 bytes
```

Key facts about packed types in Metal:

- `packed_float2` has 4-byte alignment and 8-byte size — no padding inserted between packed_float4 and packed_float2
- `packed_float3` has 4-byte alignment and 12-byte size — this is why the current struct is exactly 40 bytes with no gaps
- The minimum stride alignment for vertex buffer layouts is **4 bytes** on all Metal hardware
- 48 bytes is 4-byte aligned, so no padding is needed at the end

**C# side (verified from codebase):**
```csharp
[StructLayout(LayoutKind.Sequential)]
public readonly struct MetalVertex
{
    public Vector3 Position { get; init; }   // 12 bytes (System.Numerics.Vector3)
    public Vector3 Normal { get; init; }     // 12 bytes
    public Vector4 Tangent { get; init; }    // 16 bytes
}                                            // total: 40 bytes
```

Adding `Vector2 Texcoord` (8 bytes) to the C# struct gives 48 bytes. `System.Numerics.Vector2` is 8 bytes, sequential layout means no padding between Vector4 (16) and Vector2 (8).

### Risk Assessment

**Medium risk** — not because of alignment (which is safe), but because of the cascade:

1. **Every mesh builder must be updated.** Verified mesh builders that create MetalVertex arrays:
   - `MetalMesh.Core.cs` — main knob geometry builder
   - `ImportedStlCollarMeshBuilder.cs` — GLB collar mesh
   - `OuroborosCollarMeshBuilder.cs` — procedural collar mesh (already computes UVs separately!)
   - Any other builder found in the codebase

2. **The shader string must be updated.** The MetalVertex struct is defined in an inline C# string in `MetalPipelineManager.Shaders.cs`. The C# struct and the MSL struct must match exactly.

3. **No MTLVertexDescriptor is used.** The codebase uses implicit vertex binding (the struct is passed as `device const MetalVertex* vertices [[buffer(0)]]` and indexed manually). This means there's no descriptor to update — the stride is implicit from the struct size. This is actually simpler than if a descriptor were used, but it means there's no validation layer checking offsets.

### Critical Finding: OuroborosCollarMeshBuilder Already Has UVs

The `CollarMesh` class already stores UVs in a **separate `Vector2[] UVs` array**, not in the MetalVertex struct:

```csharp
public sealed class CollarMesh
{
    public MetalVertex[] Vertices { get; init; }
    public uint[] Indices { get; init; }
    public Vector2[] UVs { get; init; }        // ← UVs exist but are separate
    public Vector4[] Tangents { get; init; }
    public float ReferenceRadius { get; init; }
}
```

This means the collar mesh pipeline already computes proper UVs. In Phase 1, these need to be merged into the MetalVertex struct's new texcoord field rather than stored separately.

### Corrections to Plan

Phase 1 should account for the CollarMesh UV merge — this is not a new computation but a data migration from `CollarMesh.UVs[]` into `MetalVertex.Texcoord`. This reduces the effort for Subphase 1B (procedural UV generation) because part of the work is already done.

### Sources

- [Apple Developer Forums: packed_float2 vs float2](https://developer.apple.com/forums/thread/64057)
- [Metal by Example: Vertex Descriptors](https://metalbyexample.com/vertex-descriptors/)

---

## Research Area 3: SkiaSharp Image Format Support

### Question

The plan assumes SkiaSharp can load PNG, JPEG, WebP, and TIFF for texture import. What formats does SkiaSharp actually support? What about HDR formats (EXR, HDR) that material artists commonly use?

### Verified Facts

**SkiaSharp Decode (reading) — Supported formats:**

| Format | Decode Support | Notes |
|--------|---------------|-------|
| PNG | Yes | Full support including 8-bit and 16-bit per channel |
| JPEG | Yes | Full support |
| WebP | Yes | Full support (lossy and lossless) |
| GIF | Yes | Decoded to single frame |
| BMP | Yes | Full support |
| ICO | Yes | Full support |
| WBMP | Yes | Full support |
| HEIF | Yes | Platform-dependent (requires system codec) |
| **TIFF** | **No** | Filed as [Issue #433](https://github.com/mono/SkiaSharp/issues/433) (closed 2018) and [Issue #2993](https://github.com/mono/SkiaSharp/issues/2993) (feature request). Skia's upstream TIFF codec was removed. **Not supported.** |
| **EXR** | **No** | Not supported. Skia does not include an OpenEXR codec. |
| **HDR (Radiance)** | **No** | Not supported. |
| **TGA** | **No** | Not supported. |

**SkiaSharp Encode (writing) — Supported formats:**

| Format | Encode Support |
|--------|---------------|
| PNG | Yes |
| JPEG | Yes |
| WebP | Yes |
| All others | No |

### Impact on Phase 2

This is a significant finding. Material artists commonly distribute texture sets in these formats:

- **PNG** — universal, always works ✓
- **JPEG** — common for albedo, works ✓ (but lossy, not ideal for normal maps)
- **EXR** — industry standard for HDR data, displacement maps, and high-precision textures. **NOT SUPPORTED.**
- **TIFF** — common in photography and some texture pipelines. **NOT SUPPORTED.**
- **TGA** — legacy but still common in game asset pipelines. **NOT SUPPORTED.**

### Risk Assessment

**Medium risk.** For the initial material tool, PNG + JPEG + WebP covers the majority of PBR texture sets downloaded from sources like Poly Haven, ambientCG, and Quixel (which distribute as PNG or JPEG). However, the lack of EXR support limits the tool's usefulness for professional workflows that need high dynamic range or 16-bit+ precision.

### Mitigation Options

1. **Accept the limitation for v1.** PNG (8-bit and 16-bit) covers most PBR texture needs. Document supported formats clearly.
2. **Add a dedicated EXR loader later.** The .NET ecosystem has [OpenEXR.NET](https://github.com/AcademySoftwareFoundation/openexr) or similar bindings. This is additional work but isolatable.
3. **Add TGA support via a lightweight decoder.** TGA is a simple format; a manual decoder is ~200 lines of C#.
4. **For TIFF, use ImageSharp as a fallback.** SixLabors.ImageSharp supports TIFF decoding. However, the plan specifically avoids adding new dependencies. This is a tradeoff decision.

### Corrections to Plan

Phase 2 document should list supported formats explicitly (PNG, JPEG, WebP) and note EXR/TIFF/TGA as known gaps with a deferred solution. The Phase 2 KPI for "supported formats" should be updated to reflect reality.

---

## Research Area 4: GLB Import — TEXCOORD_0 and TANGENT Attributes

### Question

The plan assumes reading TEXCOORD_0 from GLB is straightforward. The GLB importer currently reads only POSITION and indices. What about TANGENT? Does the existing tangent computation in KnobForge align with glTF's mikktspace convention?

### Verified Facts

**glTF 2.0 specification for vertex attributes:**

| Attribute | Type | Description |
|-----------|------|-------------|
| POSITION | VEC3 (float) | Vertex position in local space |
| NORMAL | VEC3 (float) | Unit-length normal vector |
| TANGENT | **VEC4 (float)** | Unit-length XYZ tangent + W handedness sign (±1.0) |
| TEXCOORD_0 | VEC2 (float) | Primary UV coordinates |
| TEXCOORD_1 | VEC2 (float) | Secondary UV coordinates (rare) |

**Critical: glTF TANGENT is vec4, not vec3.** The W component encodes the handedness of the tangent basis. The bitangent is reconstructed as: `bitangent = cross(normal, tangent.xyz) * tangent.w`

**Current KnobForge GLB reading (verified from codebase):**
```
✓ POSITION  — read via TryReadAccessorVector3
✓ Indices   — read via TryReadAccessorIndices (UByte/UShort/UInt)
✗ NORMAL    — not read (computed from geometry)
✗ TANGENT   — not read (computed from geometry)
✗ TEXCOORD_0 — not read (UVs derived in shader or computed cylindrically for collars)
```

**Current UV derivation paths (verified from codebase):**

1. **Fragment shader (top face):** `uv = worldPos.xy / (topRadius * 2.0) + 0.5` — planar projection, not mesh UVs
2. **Collar mesh builder:** Computes cylindrical UVs `u = atan2(y,x)/(2π)+0.5, v = (z-minZ)/zSpan` — stored in separate `CollarMesh.UVs[]` array, NOT in MetalVertex
3. **Ouroboros collar:** Computes body/head UVs from ring parameterization — also stored in separate `CollarMesh.UVs[]`

**Current tangent computation:** The MetalVertex already has a `packed_float4 tangent` field in the shader (vec4 with W component). The C# side stores `Vector4 Tangent`. The existing tangent computation appears to be geometry-derived (from mesh builder code), not from glTF TANGENT attribute.

### What Needs to Be Read from GLB

For Phase 1 (UV infrastructure):
- **TEXCOORD_0**: Required. Follows the same pattern as POSITION reading — `TryReadAccessorVector2` (new method, but same binary accessor logic for VEC2 instead of VEC3)

For Phase 2 (normal mapping correctness):
- **TANGENT**: Strongly recommended. Without glTF-authored tangents, normal maps will render incorrectly on imported meshes. The bitangent must be derived from `cross(normal, tangent.xyz) * tangent.w` per the mikktspace convention.
- **NORMAL**: Should also be read when present. The current approach of computing normals from geometry is fine for procedural shapes but may not match the intended normals for imported meshes (especially for hard-edge models).

### Risk Assessment

**Medium risk for TEXCOORD_0** — mechanically straightforward (same accessor pattern), but the accessor code needs a new `TryReadAccessorVector2` method.

**High risk for tangent-space normal mapping** — this is the most technically subtle part of the entire program. If the TBN matrix construction in the fragment shader doesn't match the convention used to bake the normal maps, every normal-mapped surface will look wrong (inverted bumps, wrong light response). The glTF spec mandates mikktspace, but many texture sets in the wild were baked with other conventions.

### Corrections to Plan

1. Phase 1 should include reading NORMAL from GLB (not just TEXCOORD_0) to prevent normal-map-related issues in Phase 2
2. Phase 2 should have a dedicated subtask for TBN matrix validation with a known-correct reference (Blender render of the same mesh + normal map)
3. The plan should note that TANGENT reading from GLB is optional but strongly recommended — the fallback is mikktspace computation at load time, which is more complex than just reading the attribute

### Sources

- [glTF 2.0 Specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)
- [glTF Tangent Space Issues (KhronosGroup)](https://github.com/KhronosGroup/glTF/issues/2056)
- [LearnOpenGL: Normal Mapping](https://learnopengl.com/Advanced-Lighting/Normal-Mapping)

---

## Research Area 5: Project Serialization Extensibility

### Question

The plan requires adding texture paths, layer data, and graph definitions to the project format across multiple phases. How extensible is the current format? What's the migration story?

### Verified Facts (From Codebase Audit)

**Project file structure (.knob):**
```json
{
  "Format": "knobforge.project.v1",
  "DisplayName": "My Knob",
  "SavedUtc": "2026-03-12T16:30:00Z",
  "SnapshotJson": "{...serialized InspectorUndoSnapshot...}",
  "PaintStateJson": "{...serialized PaintProjectState...}",
  "ViewportStateJson": "{...serialized ViewportProjectState...}",
  "ThumbnailPngBase64": "iVBORw0KGgo..."
}
```

**Critical findings:**

1. **Nested JSON strings.** The envelope contains JSON strings (not nested objects). `SnapshotJson` is a JSON string containing a serialized `InspectorUndoSnapshot`. This means adding fields to the snapshot doesn't change the envelope structure — it changes what's inside the SnapshotJson string.

2. **Paint data is NOT raw pixels.** Paint masks are stored as **stroke history** (list of `PaintStampPersisted` objects), not as raw RGBA byte arrays. The mask is reconstructed by replaying strokes. This is important — it means paint data is compact but reconstruction time scales with stroke count.

3. **No migration logic exists.** The format ID is `"knobforge.project.v1"` and the loader simply rejects unknown formats. There is no version-to-version migration code.

4. **System.Text.Json with `JsonStringEnumConverter` only.** No custom converters, no polymorphic serialization. Adding new properties to snapshot classes will use default JSON serialization.

5. **System.Text.Json defaults are additive-safe.** When deserializing, unknown JSON properties are silently ignored by default. When serializing new properties, they appear in the output. This means **adding new properties to existing classes is backward-compatible for reading** (old files missing new properties will get default values) but **old versions of the app cannot read new files** if they reject unknown properties or if the format ID changes.

### Extensibility Assessment

| Change Type | Backward Compatible? | Forward Compatible? | Notes |
|------------|---------------------|--------------------|----|
| Add new property to MaterialNode | Yes (defaults to 0/null) | Yes (old app ignores unknown JSON keys) | Safest change type |
| Add new class (TextureSetPaths) | Yes (null if missing) | Yes (old app ignores) | Need nullable property on parent |
| Add new section to envelope (e.g., `GraphStateJson`) | Yes (null if missing) | Yes (old app ignores) | Follow the pattern of `PaintStateJson` |
| Change format ID to "v2" | No (old files rejected) | No (new files rejected by old app) | Only do this for breaking changes |
| Change paint mask from stroke-replay to raw bytes | No (old stroke data unusable) | No (new raw data unreadable by old app) | Major migration, avoid |

### Risk Assessment

**Low risk for Phases 1–4.** Adding texture paths, UV data, and multi-material associations are all additive changes that fit the existing serialization model.

**Medium risk for Phase 3 (layer data).** The paint system currently stores stroke history. Adding layers means either: (a) storing layer assignments per stroke (additive, backward-compatible), or (b) storing raw layer pixel data (breaking change, requires migration). Option (a) is strongly preferred.

**Medium risk for Phase 7 (graph data).** A node graph is a complex data structure that needs a new envelope section (`GraphStateJson`). This is structurally similar to `PaintStateJson` and follows the same pattern.

### Corrections to Plan

1. Phase 1 should NOT bump the format version. UV data in MetalVertex is runtime-only (derived from geometry); nothing changes in the project file.
2. Phase 2 should add texture paths as nullable properties on MaterialNode. Old projects without textures will deserialize with null paths.
3. Phase 3 should store layer assignments per stroke, not raw pixel data. This preserves the stroke-replay architecture.
4. The plan should include a `FormatId` bump strategy: stay on "v1" for as long as possible by using additive-only changes. Only bump to "v2" if a truly breaking change is unavoidable.

### Source

Codebase audit: `KnobProjectFileStore.cs`, `MainWindow.ProjectFiles.cs`, `MetalViewport.StateAndPaintLayers.cs`

---

## Research Area 6: Avalonia Custom Controls for Graph Editor

### Question

Phase 7 requires a visual node graph editor. Can Avalonia 11 support custom-drawn interactive canvases with bezier curves, draggable nodes, and connection wires?

### Verified Facts

**Avalonia's custom rendering capabilities:**

- `DrawingContext` API provides full custom rendering: lines, bezier curves, rectangles, text, transforms, clipping
- Custom controls can override `Render(DrawingContext context)` for custom drawing
- Pointer events (PointerPressed, PointerMoved, PointerReleased) are available on all controls for implementing drag interaction
- `Canvas` control provides absolute positioning of child elements (useful for placing node panels)
- Selective invalidation via `InvalidateVisual()` enables efficient redraws

**Two architectural approaches for the graph editor:**

1. **Canvas + child controls:** Each node is an Avalonia control (e.g., a `Border` with content) positioned on a `Canvas` via `Canvas.Left`/`Canvas.Top`. Connections drawn in a custom overlay. Pros: nodes get standard Avalonia styling, text input, property editing for free. Cons: potentially slower with 100+ nodes due to layout overhead.

2. **Fully custom rendered:** Single custom control that draws everything — node bodies, text, ports, connections — in `Render()`. Pros: total control, potentially faster for large graphs. Cons: must reimplement text rendering, hit testing, focus management.

**Recommendation for KnobForge:** Approach 1 (Canvas + child controls). The graph editor will have 10–50 nodes typically, not thousands. The Avalonia layout overhead is irrelevant at this scale, and getting standard controls (text fields, combo boxes, color pickers) inside nodes is far more valuable than raw drawing performance.

**Drag and drop:** Avalonia has built-in drag-and-drop support and third-party libraries (`Avalonia.DragDrop`, `Avalonia.Xaml.Interactions.DragAndDrop`). For node dragging, the simpler pointer-event-based approach (track PointerPressed → PointerMoved → PointerReleased) is sufficient and avoids the complexity of the full D&D API.

### Risk Assessment

**Medium risk.** Avalonia can technically do everything needed, but building a smooth, polished graph editor is a UI engineering project with many edge cases: zoom/pan with transforms, connection snapping, node selection, multi-select, undo/redo integration, minimap. The Phase 7 schedule estimate of 3–5 weeks for the visual editor is reasonable but could easily double if polish requirements are high.

### Corrections to Plan

Phase 7D should specify Approach 1 (Canvas + child controls) as the implementation strategy. This de-risks the UI work by leveraging Avalonia's existing control infrastructure.

### Sources

- [Avalonia Drawing and Graphics Samples](https://deepwiki.com/AvaloniaUI/Avalonia.Samples/8-drawing-and-graphics)
- [Avalonia Canvas Documentation](https://docs.avaloniaui.net/docs/reference/controls/canvas)

---

## Research Area 7: Tangent-Space Normal Mapping in Metal

### Question

Phase 2 adds normal map support. The fragment shader must construct a TBN (tangent-bitangent-normal) matrix to transform sampled normals from tangent space to world space. What does this require, and what can go wrong?

### Verified Facts

**The TBN matrix construction:**
```metal
float3 T = normalize(inVertex.worldTangentSign.xyz);
float3 N = normalize(inVertex.worldNormal);
float3 B = cross(N, T) * inVertex.worldTangentSign.w;  // w = handedness
float3x3 TBN = float3x3(T, B, N);
```

**Current state in KnobForge (verified from shader audit):**

The vertex shader already outputs `worldTangentSign` (a float4 containing xyz=tangent, w=sign):
```metal
struct VertexOut {
    float4 position [[position]];
    float3 worldPos;
    float3 worldNormal;
    float4 worldTangentSign;  // ← tangent already flows through pipeline
};
```

This is very good news. The tangent data is already in the fragment shader. Phase 2 only needs to:
1. Sample the normal map texture
2. Construct the TBN matrix (3 lines of code)
3. Transform the sampled normal: `float3 worldNormal = normalize(TBN * sampledNormal)`

**Common failure modes:**

| Failure Mode | Symptom | Cause | Prevention |
|-------------|---------|-------|------------|
| Inverted green channel | Bumps appear inverted on Y axis | DirectX vs. OpenGL normal map convention. OpenGL (and glTF) uses Y-up; DirectX uses Y-down. | Detect convention at load time or provide a "flip Y" toggle in the inspector. |
| Wrong handedness | Shading artifacts at UV seams | `tangent.w` sign not propagated correctly | Already propagated in KnobForge's VertexOut. Verify sign is ±1.0 in test meshes. |
| Non-normalized TBN vectors | Subtle shading errors, especially at mesh edges | Interpolation across triangles denormalizes tangent/normal | Always normalize T and N before computing B. |
| Missing tangents on imported mesh | Normal map has no effect or looks flat | GLB file doesn't include TANGENT attribute | Fallback: compute mikktspace tangents at import time. This requires POSITION + NORMAL + TEXCOORD_0. |

**The tangent.w sign convention:**

glTF specifies that `tangent.w` is `+1.0` or `-1.0`. The bitangent is `cross(N, T) * tangent.w`. KnobForge's MetalVertex already stores tangent as a vec4 with w component. The question is whether the current tangent computation (geometry-derived) uses the same convention as glTF-exported tangents.

### What Must Be Researched at Implementation Time

1. **Test with a known-correct model:** Import a GLB from Blender with a normal map, render in KnobForge, compare against Blender's output. This is the only reliable way to verify the entire TBN pipeline end-to-end.
2. **Determine the green channel convention:** Poly Haven, ambientCG, and most PBR sources use OpenGL convention (Y-up). KnobForge should default to this and provide a flip toggle.
3. **Verify mikktspace compatibility:** If KnobForge implements mikktspace tangent generation as a fallback, it must match the mikktspace implementation used by common normal map bakers (Substance, Blender, xNormal).

### Risk Assessment

**High risk.** This is the single most technically subtle task in the entire program. A bug in TBN construction is visually obvious but can be extremely difficult to diagnose — the symptoms (inverted bumps, seam artifacts, wrong light response) are similar for many different root causes. Budget extra debugging time.

### Corrections to Plan

Phase 2 should include:
1. A dedicated "TBN validation" subtask with a reference Blender render
2. A normal map green channel flip toggle in the inspector
3. Mikktspace tangent generation as a fallback for GLB files without TANGENT attribute (requires POSITION + NORMAL + TEXCOORD_0, so this depends on Phase 1 completing first)

### Sources

- [LearnOpenGL: Normal Mapping](https://learnopengl.com/Advanced-Lighting/Normal-Mapping)
- [glTF Tangent Space Issues](https://github.com/KhronosGroup/glTF/issues/2056)
- [glTF Tangent Basis Workflow](https://github.com/KhronosGroup/glTF/issues/1252)

---

## Research Area 8: Paint Mask Architecture for Layer Support

### Question

Phase 3 adds layer compositing. The current paint system stores stroke history, not raw pixel data. How does this affect the layer architecture?

### Verified Facts (From Codebase Audit)

**Current paint storage model:**

The `.knob` file stores `PaintProjectState` containing:
- `List<PaintLayerPersisted>` — layer metadata (name, visibility, blend mode, opacity)
- `List<PaintStrokePersisted>` — individual paint stamps with: UV position, radius, opacity, spread, channel, brush type, scratch type, color, seed, and **layer index**

**Wait — layers already partially exist.** The `PaintStampPersisted` class already has a `LayerIndex` field, and `PaintProjectState` already has a `List<PaintLayerPersisted>`. The serialization structure is already layer-aware.

**What's missing is the runtime compositing.** At runtime, all strokes are replayed onto a single `byte[1024*1024*4]` paint mask. There's no per-layer mask buffer and no runtime layer compositing — the layers in the serialization may be UI-only grouping without true per-layer rendering.

**Paint mask GPU upload path:**
1. CPU: `byte[] _paintMaskRgba8` (4 MB at 1024x1024)
2. GPU: `replaceRegion:mipmapLevel:withBytes:bytesPerRow:` uploads the entire mask
3. Mipmaps generated via blit command encoder
4. Texture bound at slot 1 (fragment and vertex stages)

### Architecture Decision for Phase 3

**Option A: Composite on CPU, upload single mask (extend current architecture)**
- Each layer gets its own `byte[]` buffer
- CPU composites all visible layers into the existing single mask using blend modes
- Upload the composite result (same GPU path as today)
- Pros: Minimal GPU changes, compositor is easier to debug
- Cons: 4 MB per layer at 1024x1024, compositor must run on every change

**Option B: Composite on GPU (new architecture)**
- Each layer uploaded as a separate Metal texture
- Fragment shader composites layers in real-time
- Pros: No CPU compositor, real-time blend mode preview
- Cons: Each layer consumes a texture slot (conflicts with material texture slots), shader complexity increases significantly, debugging is harder

**Recommendation: Option A.** It's simpler, preserves the existing GPU pipeline, and the CPU compositing cost is trivial (compositing 8 layers of 1024x1024 RGBA data is ~32 MB of memory operations, which is < 10ms on any modern CPU).

### Memory Impact Analysis

| Resolution | Per Layer | 4 Layers | 8 Layers | 16 Layers |
|-----------|----------|----------|----------|-----------|
| 512x512 | 1 MB | 4 MB | 8 MB | 16 MB |
| 1024x1024 | 4 MB | 16 MB | 32 MB | 64 MB |
| 2048x2048 | 16 MB | 64 MB | 128 MB | 256 MB |
| 4096x4096 | 64 MB | 256 MB | 512 MB | 1024 MB |

At 2048x2048 with 8 layers (a reasonable professional configuration), memory usage is 128 MB for paint data alone. This is significant but manageable.

At 4096x4096 with 8+ layers, memory becomes a concern. The plan should cap the maximum resolution or implement lazy allocation (only allocate layer buffers when the layer has content).

### Corrections to Plan

1. Phase 3 should use CPU compositing (Option A) — update the architecture description
2. Add lazy layer allocation to Phase 3: empty layers don't allocate a full buffer
3. Add a maximum resolution cap (or warn at 4096+ with multiple layers)
4. Note that `PaintStampPersisted.LayerIndex` already exists — Phase 3 is partially prepared for layers at the serialization level

---

## Research Area 9: GpuUniforms Struct Size and Extension

### Question

The plan adds uniform flags for texture presence (hasAlbedoMap, hasNormalMap, etc.) and new material parameters. How much room is there in the current GpuUniforms struct?

### Verified Facts (From Shader Audit)

**Current GpuUniforms layout:**
- 27 named `float4` entries for material parameters, light settings, camera data
- 16 dynamic light entries (each a `float4`)
- Total: **43 float4 entries = 688 bytes**

(Earlier estimates of ~105 float4 / ~6720 bytes appear to have been overstated based on the shader audit. The actual struct is smaller.)

**Metal constant buffer alignment:** Constant buffers (bound via `[[buffer(1)]]`) have no specific size limit beyond available GPU memory. Adding a few more float4 entries is trivial.

**What Phase 2 needs to add:**
```metal
float4 textureFlags;      // x=hasAlbedo, y=hasNormal, z=hasRoughness, w=hasMetallic
float4 textureScaleOffset; // x=scaleU, y=scaleV, z=offsetU, w=offsetV (optional)
```

That's 2 additional float4 entries — negligible.

### Risk Assessment

**Low risk.** The GpuUniforms struct has effectively unlimited room for new entries. The only concern is keeping the C# and MSL definitions in sync, which is a manual process since there's no code generation.

### Corrections to Plan

None needed. The plan's assumption of extending GpuUniforms is confirmed safe.

---

## Research Area 10: Export Pipeline — Spritesheet Format Enforcement

### Question

The user already modified `KnobExporter.cs` to force PNG for spritesheets. Is this implementation complete and correct?

### Verified Facts (From Codebase — User's Modifications)

**What the user changed:**
1. `KnobExporter.cs`: `const string spritesheetOutputExtension = "png"` — spritesheets are always PNG
2. `KnobExporter.cs`: Frame extension uses `GetOutputExtension(settings.ImageFormat)` — frames respect the user's format choice (including WebP)
3. `KnobExporter.cs`: Spritesheet saving changed from `SaveBitmap` (format-aware) to `SavePngWithCompression` (always PNG)
4. `KnobExporter.ValidationAndPaths.cs`: `ResolveExportPaths` now takes separate `frameExtension` and `spritesheetExtension` parameters

**Assessment:** The implementation looks correct for the stated goal. Spritesheets are always PNG (safe for JUCE/iPlug2/HISE). Individual frames can be WebP (user's choice). The path resolution handles the split correctly.

**Remaining UX issue:** The export window (RenderSettingsWindow) still shows the format selector at row 17, far from the output strategy at the top. Users might select WebP thinking it applies to everything, not realizing spritesheets are always PNG. The export window UX overhaul (separate from the material tool program) should address this by either:
- Adding a note next to the format selector: "Applies to individual frames only. Spritesheets are always PNG."
- Or splitting the format selector into two: frame format + spritesheet format (with spritesheet locked to PNG and greyed out)

### Corrections to Plan

This is already implemented. No Phase-level work needed. The export window UX improvements are a separate effort.

---

## Summary: Research Gaps That Block Implementation

### Must Resolve Before Phase 1 Starts

| # | Research Gap | Resolution Method | Estimated Effort |
|---|-------------|-------------------|-----------------|
| 1 | Enumerate ALL mesh builders that create MetalVertex arrays | Grep for `new MetalVertex` and `MetalVertex[]` across entire codebase | 1 hour |
| 2 | Verify CollarMesh.UVs[] values are correct and match expected cylindrical projection | Load a test GLB collar, dump UV values, verify against known-correct values | 2 hours |
| 3 | Create reference render for visual regression testing | Render the default knob project, save as reference PNG at the exact resolution used in CI | 30 minutes |

### Must Resolve Before Phase 2 Starts

| # | Research Gap | Resolution Method | Estimated Effort |
|---|-------------|-------------------|-----------------|
| 4 | Verify tangent.w sign convention in KnobForge's geometry-derived tangents vs. glTF convention | Read the tangent computation code, trace the sign, compare against `cross(N,T)*w` convention | 2 hours |
| 5 | Create test GLB with known UVs and tangents for TBN validation | Export from Blender with a checkerboard texture + normal map, render in Blender as reference | 2 hours |
| 6 | Test SkiaSharp 16-bit PNG decoding | Load a 16-bit/channel PNG via SKBitmap.Decode, verify pixel values are correct (not clamped to 8-bit) | 1 hour |
| 7 | Determine normal map green channel convention for target texture sources (Poly Haven, ambientCG) | Download sample normal maps, inspect green channel direction | 1 hour |

### Must Resolve Before Phase 3 Starts

| # | Research Gap | Resolution Method | Estimated Effort |
|---|-------------|-------------------|-----------------|
| 8 | Profile CPU paint mask compositing performance at 2048x2048 × 8 layers | Write a microbenchmark: composite 8 random RGBA8 buffers with multiply blend mode, measure time | 1 hour |
| 9 | Verify that PaintLayerPersisted and LayerIndex in PaintStampPersisted are actually functional (not dead code) | Trace the paint recording path from UI to serialization, confirm layers are stored correctly | 2 hours |

### Must Resolve Before Phase 7 Starts

| # | Research Gap | Resolution Method | Estimated Effort |
|---|-------------|-------------------|-----------------|
| 10 | Prototype Avalonia Canvas + child control approach for node positioning | Build a minimal PoC: 5 draggable rectangles on a Canvas with bezier curve connections, measure layout performance | 4 hours |
| 11 | Research Metal shader compilation from string at runtime (for GPU node evaluation) | Test `MTLDevice.newLibraryWithSource:options:error:` with a dynamically generated shader string, measure compile time | 2 hours |

**Total pre-implementation research: ~18 hours (2–3 days of focused work)**

This research investment prevents weeks of debugging during implementation. Every item on this list has caused real problems in similar projects when skipped.
