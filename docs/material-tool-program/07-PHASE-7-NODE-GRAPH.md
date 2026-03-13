# Phase 7: Node-Based Material Graph

## Phase Identity

- **Phase**: 7 of 7
- **Name**: Node-Based Material Graph
- **Depends on**: Phases 1-5 (full material pipeline must exist)
- **Unlocks**: Nothing (capstone feature)
- **Risk**: High — largest single feature, new subsystem with its own evaluation engine and UI
- **Milestone**: M7 — Procedural texture generation via connected node graph

## Why This Phase Exists

Without a node graph, every material property is either a flat scalar, an imported texture, or painted by hand. There's no way to procedurally generate textures (noise patterns, gradients, Voronoi cells), blend textures with masks, or build complex material logic. A node graph is what separates a texture viewer from a material authoring tool. This is what Substance Designer, Blender Shader Editor, and Unity ShaderGraph provide.

This phase is explicitly designed to be deferrable. KnobForge is fully useful after Phase 5 — the node graph adds power-user procedural capabilities.

## Subphases

### Subphase 7A: Graph Data Model

#### Project 7A.1: Core Graph Types

**Task 7A.1.1: Define port types**
- New file: `KnobForge.Core/MaterialGraph/PortType.cs`
- Enum: `Float`, `Float2`, `Float3`, `Float4`, `Color`, `Texture2D`
- Each type defines what data flows through a connection

**Task 7A.1.2: Define GraphPort**
- New file: `KnobForge.Core/MaterialGraph/GraphPort.cs`
- Properties: `string Name`, `PortType Type`, `PortDirection Direction` (Input/Output)
- Input ports: have a default value (used when no connection)
- Output ports: produce computed values

**Task 7A.1.3: Define GraphNode base class**
- New file: `KnobForge.Core/MaterialGraph/GraphNode.cs`
- Properties: `Guid Id`, `string TypeId`, `Vector2 Position` (for UI layout), `List<GraphPort> Inputs`, `List<GraphPort> Outputs`, `Dictionary<string, object> Parameters`
- Abstract method: `Evaluate(GraphEvaluationContext context)` — for CPU evaluation
- Abstract method: `EmitShaderCode(ShaderCodeBuilder builder)` — for GPU compilation

**Task 7A.1.4: Define GraphConnection**
- New file: `KnobForge.Core/MaterialGraph/GraphConnection.cs`
- Properties: `Guid SourceNodeId`, `string SourcePortName`, `Guid TargetNodeId`, `string TargetPortName`

**Task 7A.1.5: Define MaterialGraph**
- New file: `KnobForge.Core/MaterialGraph/MaterialGraph.cs`
- Properties: `List<GraphNode> Nodes`, `List<GraphConnection> Connections`
- Methods: `AddNode`, `RemoveNode`, `Connect`, `Disconnect`, `TopologicalSort`, `Validate`
- Validation: detect cycles, type mismatches, disconnected required inputs

#### Project 7A.2: Graph Serialization

**Task 7A.2.1: JSON serialization of graph**
- Serialize within the `.knob` project file as a `MaterialGraph` JSON object
- Each node serializes: TypeId, Id, Position, Parameters
- Connections serialize: source/target node IDs and port names
- Use System.Text.Json with a custom converter for polymorphic GraphNode deserialization

**Task 7A.2.2: Node type registry**
- A static registry mapping TypeId strings to concrete GraphNode subclass types
- Used for deserialization: `"PerlinNoise"` → `typeof(PerlinNoiseNode)`
- Extensible for future node types

---

### Subphase 7B: Node Type Library

#### Project 7B.1: Input Nodes

**Task 7B.1.1: TextureMapNode**
- Inputs: none
- Parameters: `FilePath` (string), `Tiling` (float2), `Offset` (float2)
- Outputs: `Color` (Float4), `R/G/B/A` (Float each)

**Task 7B.1.2: UVInputNode**
- Inputs: none
- Outputs: `UV` (Float2)
- Emits the vertex texcoord

**Task 7B.1.3: WorldPositionNode**
- Inputs: none
- Outputs: `Position` (Float3)

**Task 7B.1.4: VertexNormalNode**
- Inputs: none
- Outputs: `Normal` (Float3)

**Task 7B.1.5: TimeNode**
- Inputs: none
- Outputs: `Time` (Float) — animation time for animated materials

**Task 7B.1.6: ConstantNode**
- Parameters: `Value` (float/float2/float3/float4 depending on output type)
- Outputs: `Value` (configured type)

#### Project 7B.2: Math Nodes

**Task 7B.2.1: ArithmeticNode (Add, Subtract, Multiply, Divide)**
- Inputs: `A` (Float), `B` (Float)
- Outputs: `Result` (Float)
- Parameter: `Operation` enum

**Task 7B.2.2: LerpNode**
- Inputs: `A` (Float4), `B` (Float4), `T` (Float)
- Outputs: `Result` (Float4)

**Task 7B.2.3: ClampNode**
- Inputs: `Value` (Float), `Min` (Float), `Max` (Float)
- Outputs: `Result` (Float)

**Task 7B.2.4: RemapNode**
- Inputs: `Value` (Float), `InMin` (Float), `InMax` (Float), `OutMin` (Float), `OutMax` (Float)
- Outputs: `Result` (Float)

**Task 7B.2.5: PowerNode**
- Inputs: `Base` (Float), `Exponent` (Float)
- Outputs: `Result` (Float)

**Task 7B.2.6: DotProductNode, CrossProductNode, NormalizeNode**
- Vector math operations

#### Project 7B.3: Pattern/Procedural Nodes

**Task 7B.3.1: PerlinNoiseNode**
- Inputs: `UV` (Float2)
- Parameters: `Scale` (float), `Octaves` (int), `Persistence` (float), `Lacunarity` (float), `Seed` (int)
- Outputs: `Value` (Float), `Color` (Float3)

**Task 7B.3.2: VoronoiNode**
- Inputs: `UV` (Float2)
- Parameters: `Scale` (float), `Jitter` (float), `Seed` (int)
- Outputs: `CellValue` (Float), `Distance` (Float), `CellColor` (Float3)

**Task 7B.3.3: GradientNode**
- Inputs: `UV` (Float2)
- Parameters: `Type` (Linear/Radial/Angular), `Rotation` (float)
- Outputs: `Value` (Float)

**Task 7B.3.4: CheckerNode**
- Inputs: `UV` (Float2)
- Parameters: `Scale` (float)
- Outputs: `Value` (Float)

**Task 7B.3.5: BrickNode**
- Inputs: `UV` (Float2)
- Parameters: `BrickWidth/Height`, `MortarWidth`, `Offset` (float)
- Outputs: `Value` (Float), `Mortar` (Float)

#### Project 7B.4: Color Nodes

**Task 7B.4.1: ColorRampNode**
- Inputs: `Value` (Float)
- Parameters: `GradientStops` (list of position + color pairs)
- Outputs: `Color` (Float3)

**Task 7B.4.2: HSVNode (RGB ↔ HSV conversion)**
- Inputs: `Color` (Float3) or `H/S/V` (Float each)
- Outputs: `Color` (Float3) or `H/S/V` (Float each)

**Task 7B.4.3: BrightnessContrastNode**
- Inputs: `Color` (Float3), `Brightness` (Float), `Contrast` (Float)
- Outputs: `Color` (Float3)

#### Project 7B.5: Output Node

**Task 7B.5.1: PBROutputNode**
- Inputs: `Albedo` (Float3), `Normal` (Float3), `Roughness` (Float), `Metallic` (Float), `Emission` (Float3), `Alpha` (Float)
- Each input has a sensible default (white albedo, flat normal, 0.5 roughness, 0.0 metallic)
- This is the terminal node — the graph must have exactly one PBROutputNode
- Outputs: none (drives the material)

---

### Subphase 7C: Graph Evaluation

#### Project 7C.1: CPU Evaluation Engine

**Task 7C.1.1: GraphEvaluator class**
- New file: `KnobForge.Core/MaterialGraph/GraphEvaluator.cs`
- Method: `EvaluateAtTexel(MaterialGraph graph, float2 uv, EvaluationContext ctx) → MaterialOutput`
- Algorithm: Topological sort → evaluate each node in order → resolve connections → return PBROutput values
- This is used by the texture baker (Phase 5) and for preview thumbnails

**Task 7C.1.2: Texture caching during evaluation**
- Pattern nodes (Perlin, Voronoi) are expensive per-texel
- For baking: evaluate the full texture at target resolution once, cache as bitmap
- For preview: evaluate at reduced resolution (256x256)

#### Project 7C.2: GPU Shader Compilation (Future)

**Task 7C.2.1: ShaderCodeBuilder class**
- New file: `KnobForge.Rendering/MaterialGraph/ShaderCodeBuilder.cs`
- Concept: Each node emits a Metal shader code fragment
- The builder topologically sorts nodes, assigns variable names, and concatenates code
- Output: A complete Metal fragment shader function that replaces the material evaluation portion of fragment_main

**Task 7C.2.2: Runtime shader recompilation**
- When the graph changes: rebuild the shader source, recompile the Metal library, swap the pipeline state
- Use the existing `MetalPipelineManager` pattern for library/function/pipeline creation
- Latency: Shader compilation takes ~50-200ms on Metal. Debounce graph changes (recompile 300ms after last edit)

**Task 7C.2.3: Fallback to CPU when GPU compilation fails**
- If shader compilation produces errors (malformed graph, unsupported node combination): fall back to CPU evaluation for preview
- Show a warning in the UI that GPU preview is unavailable

---

### Subphase 7D: Graph UI

#### Project 7D.1: Property-Panel UI (Initial Implementation)

**Task 7D.1.1: Node list panel**
- File: New MainWindow partial or separate window
- Display: List of all nodes in the graph, selectable
- Each node shows: type icon, name, a summary of connections
- "Add Node" dropdown with categorized node types

**Task 7D.1.2: Node property editor**
- When a node is selected: show its parameters in an inspector panel
- Parameter types map to UI controls: float → slider, color → color picker, enum → dropdown, file → browse button
- Input ports: show connection source (or "default: {value}" if unconnected), with a "Connect from..." action

**Task 7D.1.3: Connection management**
- Connect: Select output port on source node, then select input port on target node
- Disconnect: Click a connection and delete
- Validation: Show errors for type mismatches, cycles, missing PBROutput

#### Project 7D.2: Visual Graph Editor (Future Enhancement)

**Task 7D.2.1: Canvas-based node graph control**
- Custom Avalonia `Canvas` control with draggable node boxes and bezier curve connections
- This is a significant UI engineering effort (~2000+ lines of custom control code)
- Defer until the property-panel UI proves the graph evaluation works correctly

**Task 7D.2.2: Node preview thumbnails**
- Each node shows a small thumbnail of its output (evaluated at 64x64 or 128x128)
- Thumbnails update in real-time as parameters change
- Uses the CPU evaluator for thumbnail generation

---

## Verification Checklist (Phase 7 Complete)

- [ ] Graph data model correctly represents nodes, ports, connections
- [ ] Topological sort handles complex graphs without cycles
- [ ] Cycle detection reports errors to user
- [ ] All pattern nodes (Perlin, Voronoi, Checker, etc.) produce correct output
- [ ] Color nodes correctly transform between color spaces
- [ ] PBROutputNode drives material properties in the viewport
- [ ] CPU evaluation produces identical results to GPU shader (when GPU compilation is implemented)
- [ ] Graph serializes/deserializes correctly in project files
- [ ] UI allows creating, connecting, and editing nodes
- [ ] Texture bake correctly evaluates the graph to produce output maps

## New Files (Extensive)

| File | Purpose |
|------|---------|
| `KnobForge.Core/MaterialGraph/PortType.cs` | Port type enum |
| `KnobForge.Core/MaterialGraph/GraphPort.cs` | Port definition |
| `KnobForge.Core/MaterialGraph/GraphNode.cs` | Base node class |
| `KnobForge.Core/MaterialGraph/GraphConnection.cs` | Connection model |
| `KnobForge.Core/MaterialGraph/MaterialGraph.cs` | Graph container with validation |
| `KnobForge.Core/MaterialGraph/GraphEvaluator.cs` | CPU evaluation engine |
| `KnobForge.Core/MaterialGraph/Nodes/` (directory) | One file per node type (~20 files) |
| `KnobForge.Rendering/MaterialGraph/ShaderCodeBuilder.cs` | Graph → Metal shader compiler |
| MainWindow graph editor partial | Graph UI (property panel or canvas) |
