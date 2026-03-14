# Codex Implementation Prompt — Phase 7: Node-Based Material Graph

## Your Role

You are implementing Phase 7 of the KnobForge Material Tool Transformation — the capstone feature. Your job is to add a node-based material graph system that lets users build procedural materials by connecting nodes (noise generators, math operations, color transforms, texture inputs) into a directed acyclic graph that drives the PBR material output. Work incrementally — complete each subphase, verify it compiles, then move to the next. Do not skip verification steps. Do not refactor unrelated code.

## Project Context

KnobForge is a .NET 8 / Avalonia 11.x / Metal GPU desktop app (macOS only) that renders skeuomorphic knobs and UI components for audio plugins. It exports spritesheet filmstrips for JUCE, iPlug2, and HISE.

Phases 1–6 are complete:
- **Phase 1**: UV infrastructure — vertex UVs flow through the pipeline.
- **Phase 2**: Texture map import — PBR textures on slots 4–7.
- **Phase 3**: Paint system upgrades — variable resolution paint masks, layer compositing.
- **Phase 4**: Multi-material support — per-SubMesh draw calls with per-material textures.
- **Phase 5**: Texture bake pipeline — CPU material evaluator (`TextureBaker`) exports PBR texture map PNGs.
- **Phase 6**: Inspector control overhaul — all 219 SpriteKnobSliders replaced with compact ValueInput controls.

## What Phase 7 Does

Adds a node-based material graph subsystem. Users create nodes (noise generators, math ops, texture inputs, color ramps), connect them via typed ports, and the graph drives the PBR material output (albedo, roughness, metallic, normal). The graph is evaluated on the CPU per-texel, integrating with the existing TextureBaker for export and eventually with the real-time viewport.

**Explicitly deferred** (do NOT implement):
- GPU shader compilation from graph (7C.2) — too complex, requires runtime Metal shader recompilation
- Visual canvas-based graph editor (7D.2) — a ~2000+ line custom control, deferred to post-MVP
- TimeNode (animated materials) — no animation system exists yet

## ⚠️ CRITICAL CONSTRAINTS

1. **Do NOT modify the existing TextureBaker evaluation path.** Add the graph evaluator as an *alternative* path. When a material has a graph attached, use the graph evaluator; otherwise, fall back to the existing `EvaluateMaterialAtTexel` path.
2. **Do NOT modify MetalPipelineManager.Shaders.cs or any GPU shader code.** The graph is CPU-only for now.
3. **Do NOT modify MaterialNode's existing properties.** Add a new `MaterialGraph?` property to MaterialNode, nullable, defaulting to null (no graph = legacy behavior).
4. **Serialization must be additive-safe.** Missing `MaterialGraph` in old project files → null → legacy path. Old KnobForge versions must be able to load files saved with graphs (they just ignore the unknown property).
5. **Use `System.Text.Json` with `JsonStringEnumConverter`** — matching the existing project serialization pattern.

---

## Subphase 7A: Graph Data Model

### 7A.1: Core Types — New Directory `KnobForge.Core/MaterialGraph/`

#### File: `PortType.cs`

```csharp
namespace KnobForge.Core.MaterialGraph
{
    public enum PortType
    {
        Float,
        Float2,
        Float3,
        Float4,
        Color,      // Alias for Float3 in RGB context
        Texture2D   // Reference to a loaded texture
    }

    public enum PortDirection
    {
        Input,
        Output
    }
}
```

#### File: `GraphPort.cs`

```csharp
namespace KnobForge.Core.MaterialGraph
{
    public sealed class GraphPort
    {
        public string Name { get; set; } = string.Empty;
        public PortType Type { get; set; }
        public PortDirection Direction { get; set; }

        /// <summary>
        /// Default value used when no connection feeds this input.
        /// Stored as float[] with length matching the PortType dimensionality.
        /// Float=1, Float2=2, Float3/Color=3, Float4=4.
        /// Null for Output ports and Texture2D inputs.
        /// </summary>
        public float[]? DefaultValue { get; set; }
    }
}
```

#### File: `GraphConnection.cs`

```csharp
namespace KnobForge.Core.MaterialGraph
{
    public sealed class GraphConnection
    {
        public Guid SourceNodeId { get; set; }
        public string SourcePortName { get; set; } = string.Empty;
        public Guid TargetNodeId { get; set; }
        public string TargetPortName { get; set; } = string.Empty;
    }
}
```

#### File: `GraphNode.cs`

```csharp
using System.Numerics;

namespace KnobForge.Core.MaterialGraph
{
    public abstract class GraphNode
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Type identifier for serialization/registry lookup.
        /// Convention: "PerlinNoise", "Add", "TextureMap", "PBROutput", etc.
        /// </summary>
        public abstract string TypeId { get; }

        /// <summary>Position in the graph editor UI (for future visual editor).</summary>
        public Vector2 EditorPosition { get; set; }

        /// <summary>
        /// Define the ports this node exposes. Called once at creation.
        /// Override in subclasses to declare inputs/outputs.
        /// </summary>
        public abstract IReadOnlyList<GraphPort> GetPorts();

        /// <summary>
        /// Evaluate this node for a given texel. Reads input values from context,
        /// computes output values, and writes them to context.
        /// </summary>
        public abstract void Evaluate(GraphEvaluationContext context);
    }
}
```

#### File: `GraphEvaluationContext.cs`

```csharp
using System.Numerics;

namespace KnobForge.Core.MaterialGraph
{
    /// <summary>
    /// Per-texel evaluation context. Stores intermediate port values during graph traversal.
    /// </summary>
    public sealed class GraphEvaluationContext
    {
        private readonly Dictionary<(Guid nodeId, string portName), float[]> _portValues = new();

        /// <summary>Current texel UV coordinates (0-1 range).</summary>
        public Vector2 UV { get; set; }

        /// <summary>World-space position (if available, else zero).</summary>
        public Vector3 WorldPosition { get; set; }

        /// <summary>World-space normal (if available, else up).</summary>
        public Vector3 WorldNormal { get; set; } = Vector3.UnitY;

        /// <summary>Loaded textures available for TextureMapNode sampling.</summary>
        public Dictionary<string, TextureData> LoadedTextures { get; } = new();

        public void SetPortValue(Guid nodeId, string portName, float[] value)
        {
            _portValues[(nodeId, portName)] = value;
        }

        public float[] GetPortValue(Guid nodeId, string portName)
        {
            return _portValues.TryGetValue((nodeId, portName), out var value) ? value : Array.Empty<float>();
        }

        /// <summary>
        /// Get the value feeding a specific input port, resolving the connection.
        /// If no connection, returns the port's default value.
        /// </summary>
        public float[] GetInputValue(Guid nodeId, string portName, MaterialGraph graph)
        {
            // Find connection targeting this input
            var conn = graph.Connections.FirstOrDefault(c =>
                c.TargetNodeId == nodeId && c.TargetPortName == portName);

            if (conn != null)
            {
                return GetPortValue(conn.SourceNodeId, conn.SourcePortName);
            }

            // No connection — use default
            var node = graph.GetNodeById(nodeId);
            var port = node?.GetPorts().FirstOrDefault(p => p.Name == portName && p.Direction == PortDirection.Input);
            return port?.DefaultValue ?? new float[] { 0f };
        }

        public void Clear()
        {
            _portValues.Clear();
        }
    }

    /// <summary>Loaded texture data for CPU sampling.</summary>
    public sealed class TextureData
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public byte[] Rgba8 { get; init; } = Array.Empty<byte>();

        public Vector4 SampleBilinear(float u, float v)
        {
            // Wrap UVs to [0,1]
            u = u - MathF.Floor(u);
            v = v - MathF.Floor(v);

            float fx = u * (Width - 1);
            float fy = v * (Height - 1);
            int x0 = (int)MathF.Floor(fx);
            int y0 = (int)MathF.Floor(fy);
            int x1 = Math.Min(x0 + 1, Width - 1);
            int y1 = Math.Min(y0 + 1, Height - 1);
            float tx = fx - x0;
            float ty = fy - y0;

            Vector4 c00 = ReadPixel(x0, y0);
            Vector4 c10 = ReadPixel(x1, y0);
            Vector4 c01 = ReadPixel(x0, y1);
            Vector4 c11 = ReadPixel(x1, y1);

            Vector4 top = Vector4.Lerp(c00, c10, tx);
            Vector4 bot = Vector4.Lerp(c01, c11, tx);
            return Vector4.Lerp(top, bot, ty);
        }

        private Vector4 ReadPixel(int x, int y)
        {
            int offset = (y * Width + x) * 4;
            if (offset + 3 >= Rgba8.Length) return Vector4.Zero;
            return new Vector4(
                Rgba8[offset] / 255f,
                Rgba8[offset + 1] / 255f,
                Rgba8[offset + 2] / 255f,
                Rgba8[offset + 3] / 255f);
        }
    }
}
```

#### File: `MaterialGraph.cs`

```csharp
namespace KnobForge.Core.MaterialGraph
{
    public sealed class MaterialGraph
    {
        public List<GraphNode> Nodes { get; set; } = new();
        public List<GraphConnection> Connections { get; set; } = new();

        public GraphNode? GetNodeById(Guid id) => Nodes.FirstOrDefault(n => n.Id == id);

        public void AddNode(GraphNode node)
        {
            if (Nodes.Any(n => n.Id == node.Id)) return;
            Nodes.Add(node);
        }

        public void RemoveNode(Guid nodeId)
        {
            Nodes.RemoveAll(n => n.Id == nodeId);
            Connections.RemoveAll(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId);
        }

        public bool Connect(Guid sourceNodeId, string sourcePort, Guid targetNodeId, string targetPort)
        {
            // Remove existing connection to this input (inputs accept only one connection)
            Connections.RemoveAll(c => c.TargetNodeId == targetNodeId && c.TargetPortName == targetPort);

            // Validate nodes exist
            if (GetNodeById(sourceNodeId) == null || GetNodeById(targetNodeId) == null) return false;

            // Check for cycles
            if (WouldCreateCycle(sourceNodeId, targetNodeId)) return false;

            Connections.Add(new GraphConnection
            {
                SourceNodeId = sourceNodeId,
                SourcePortName = sourcePort,
                TargetNodeId = targetNodeId,
                TargetPortName = targetPort
            });
            return true;
        }

        public void Disconnect(Guid targetNodeId, string targetPort)
        {
            Connections.RemoveAll(c => c.TargetNodeId == targetNodeId && c.TargetPortName == targetPort);
        }

        /// <summary>
        /// Returns nodes in topological order (dependencies first).
        /// Throws if the graph contains a cycle.
        /// </summary>
        public List<GraphNode> TopologicalSort()
        {
            var sorted = new List<GraphNode>();
            var visited = new HashSet<Guid>();
            var visiting = new HashSet<Guid>(); // cycle detection

            foreach (var node in Nodes)
            {
                if (!visited.Contains(node.Id))
                {
                    TopologicalVisit(node, visited, visiting, sorted);
                }
            }

            return sorted;
        }

        private void TopologicalVisit(GraphNode node, HashSet<Guid> visited, HashSet<Guid> visiting, List<GraphNode> sorted)
        {
            if (visiting.Contains(node.Id))
                throw new InvalidOperationException($"Cycle detected at node '{node.TypeId}' ({node.Id})");
            if (visited.Contains(node.Id))
                return;

            visiting.Add(node.Id);

            // Visit all nodes that feed into this node's inputs
            foreach (var conn in Connections.Where(c => c.TargetNodeId == node.Id))
            {
                var sourceNode = GetNodeById(conn.SourceNodeId);
                if (sourceNode != null)
                {
                    TopologicalVisit(sourceNode, visited, visiting, sorted);
                }
            }

            visiting.Remove(node.Id);
            visited.Add(node.Id);
            sorted.Add(node);
        }

        private bool WouldCreateCycle(Guid sourceNodeId, Guid targetNodeId)
        {
            if (sourceNodeId == targetNodeId) return true;

            // DFS from source to check if target is already an ancestor
            var visited = new HashSet<Guid>();
            return CanReach(targetNodeId, sourceNodeId, visited);
        }

        private bool CanReach(Guid from, Guid to, HashSet<Guid> visited)
        {
            if (from == to) return true;
            if (!visited.Add(from)) return false;

            foreach (var conn in Connections.Where(c => c.TargetNodeId == from))
            {
                if (CanReach(conn.SourceNodeId, to, visited)) return true;
            }
            return false;
        }

        /// <summary>Find the PBROutputNode, if present.</summary>
        public GraphNode? FindOutputNode() => Nodes.FirstOrDefault(n => n.TypeId == "PBROutput");

        /// <summary>Validate the graph. Returns a list of error messages (empty = valid).</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            var outputNodes = Nodes.Where(n => n.TypeId == "PBROutput").ToList();
            if (outputNodes.Count == 0)
                errors.Add("Graph must contain exactly one PBR Output node.");
            else if (outputNodes.Count > 1)
                errors.Add("Graph contains multiple PBR Output nodes — only one is allowed.");

            try
            {
                TopologicalSort();
            }
            catch (InvalidOperationException ex)
            {
                errors.Add(ex.Message);
            }

            // Check for type mismatches
            foreach (var conn in Connections)
            {
                var sourceNode = GetNodeById(conn.SourceNodeId);
                var targetNode = GetNodeById(conn.TargetNodeId);
                if (sourceNode == null)
                {
                    errors.Add($"Connection references missing source node {conn.SourceNodeId}");
                    continue;
                }
                if (targetNode == null)
                {
                    errors.Add($"Connection references missing target node {conn.TargetNodeId}");
                    continue;
                }

                var sourcePort = sourceNode.GetPorts().FirstOrDefault(p => p.Name == conn.SourcePortName && p.Direction == PortDirection.Output);
                var targetPort = targetNode.GetPorts().FirstOrDefault(p => p.Name == conn.TargetPortName && p.Direction == PortDirection.Input);

                if (sourcePort == null)
                    errors.Add($"Node '{sourceNode.TypeId}' has no output port '{conn.SourcePortName}'");
                if (targetPort == null)
                    errors.Add($"Node '{targetNode.TypeId}' has no input port '{conn.TargetPortName}'");

                // Allow Float→Float, Color↔Float3, etc. (compatible conversions)
                // Only flag truly incompatible connections
                if (sourcePort != null && targetPort != null && !ArePortTypesCompatible(sourcePort.Type, targetPort.Type))
                    errors.Add($"Type mismatch: {sourceNode.TypeId}.{conn.SourcePortName} ({sourcePort.Type}) → {targetNode.TypeId}.{conn.TargetPortName} ({targetPort.Type})");
            }

            return errors;
        }

        private static bool ArePortTypesCompatible(PortType source, PortType target)
        {
            if (source == target) return true;
            // Color is interchangeable with Float3
            if ((source == PortType.Color && target == PortType.Float3) ||
                (source == PortType.Float3 && target == PortType.Color)) return true;
            // Float can promote to Float2/Float3/Float4 (broadcast)
            if (source == PortType.Float && target is PortType.Float2 or PortType.Float3 or PortType.Float4 or PortType.Color) return true;
            return false;
        }
    }
}
```

### 7A.2: Graph Serialization

#### File: `GraphNodeTypeRegistry.cs`

```csharp
namespace KnobForge.Core.MaterialGraph
{
    public static class GraphNodeTypeRegistry
    {
        private static readonly Dictionary<string, Type> Registry = new();

        static GraphNodeTypeRegistry()
        {
            // Register all built-in node types
            // These get populated in 7B after each node class is created
        }

        public static void Register(string typeId, Type nodeType)
        {
            Registry[typeId] = nodeType;
        }

        public static GraphNode? CreateByTypeId(string typeId)
        {
            if (Registry.TryGetValue(typeId, out var type))
            {
                return (GraphNode?)Activator.CreateInstance(type);
            }
            return null;
        }

        public static IReadOnlyDictionary<string, Type> GetAllTypes() => Registry;
    }
}
```

#### File: `MaterialGraphJsonConverter.cs`

A custom `JsonConverter<MaterialGraph>` that handles polymorphic `GraphNode` serialization using the `TypeId` field and the registry.

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KnobForge.Core.MaterialGraph
{
    public sealed class MaterialGraphJsonConverter : JsonConverter<MaterialGraph>
    {
        public override MaterialGraph? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var graph = new MaterialGraph();

            // Deserialize nodes
            if (root.TryGetProperty("Nodes", out var nodesArray))
            {
                foreach (var nodeElement in nodesArray.EnumerateArray())
                {
                    string? typeId = nodeElement.GetProperty("TypeId").GetString();
                    if (typeId == null) continue;

                    var node = GraphNodeTypeRegistry.CreateByTypeId(typeId);
                    if (node == null) continue; // Unknown node type — skip gracefully

                    node.Id = nodeElement.GetProperty("Id").GetGuid();

                    if (nodeElement.TryGetProperty("EditorPosition", out var pos))
                    {
                        float x = pos.TryGetProperty("X", out var px) ? px.GetSingle() : 0f;
                        float y = pos.TryGetProperty("Y", out var py) ? py.GetSingle() : 0f;
                        node.EditorPosition = new System.Numerics.Vector2(x, y);
                    }

                    // Deserialize node-specific parameters
                    if (nodeElement.TryGetProperty("Parameters", out var paramsElement))
                    {
                        DeserializeNodeParameters(node, paramsElement, options);
                    }

                    graph.Nodes.Add(node);
                }
            }

            // Deserialize connections
            if (root.TryGetProperty("Connections", out var connectionsArray))
            {
                foreach (var connElement in connectionsArray.EnumerateArray())
                {
                    graph.Connections.Add(new GraphConnection
                    {
                        SourceNodeId = connElement.GetProperty("SourceNodeId").GetGuid(),
                        SourcePortName = connElement.GetProperty("SourcePortName").GetString() ?? "",
                        TargetNodeId = connElement.GetProperty("TargetNodeId").GetGuid(),
                        TargetPortName = connElement.GetProperty("TargetPortName").GetString() ?? ""
                    });
                }
            }

            return graph;
        }

        public override void Write(Utf8JsonWriter writer, MaterialGraph value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // Nodes
            writer.WriteStartArray("Nodes");
            foreach (var node in value.Nodes)
            {
                writer.WriteStartObject();
                writer.WriteString("TypeId", node.TypeId);
                writer.WriteString("Id", node.Id);

                writer.WriteStartObject("EditorPosition");
                writer.WriteNumber("X", node.EditorPosition.X);
                writer.WriteNumber("Y", node.EditorPosition.Y);
                writer.WriteEndObject();

                // Serialize node-specific parameters
                writer.WritePropertyName("Parameters");
                SerializeNodeParameters(writer, node, options);

                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            // Connections
            writer.WriteStartArray("Connections");
            foreach (var conn in value.Connections)
            {
                writer.WriteStartObject();
                writer.WriteString("SourceNodeId", conn.SourceNodeId);
                writer.WriteString("SourcePortName", conn.SourcePortName);
                writer.WriteString("TargetNodeId", conn.TargetNodeId);
                writer.WriteString("TargetPortName", conn.TargetPortName);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        /// <summary>
        /// Each node subclass should implement a Parameters dictionary or have known properties.
        /// Use reflection or a virtual method on GraphNode.
        /// </summary>
        private static void SerializeNodeParameters(Utf8JsonWriter writer, GraphNode node, JsonSerializerOptions options)
        {
            // Serialize the node's public parameter properties as a flat JSON object
            JsonSerializer.Serialize(writer, node, node.GetType(), options);
        }

        private static void DeserializeNodeParameters(GraphNode node, JsonElement paramsElement, JsonSerializerOptions options)
        {
            // Read parameter values back into the node's properties
            string json = paramsElement.GetRawText();
            // Use a helper that applies JSON properties to the existing node instance
            var tempNode = (GraphNode?)JsonSerializer.Deserialize(json, node.GetType(), options);
            if (tempNode != null)
            {
                CopyParameters(tempNode, node);
            }
        }

        private static void CopyParameters(GraphNode source, GraphNode target)
        {
            // Copy parameter properties (skip Id, TypeId, EditorPosition — already set)
            var props = source.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite &&
                       p.Name != nameof(GraphNode.Id) &&
                       p.Name != nameof(GraphNode.EditorPosition));

            foreach (var prop in props)
            {
                try { prop.SetValue(target, prop.GetValue(source)); } catch { /* skip */ }
            }
        }
    }
}
```

### 7A.3: Attach Graph to MaterialNode

Add a nullable property to `MaterialNode`:

```csharp
// In MaterialNode.cs — add this property:
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public MaterialGraph? Graph { get; set; }
```

This is additive-safe: old project files without this field will deserialize with `Graph = null`, falling back to the legacy scalar material path.

Also register `MaterialGraphJsonConverter` in the project serialization options used by `KnobProjectFileStore.cs`:
```csharp
// In ProjectSnapshotJsonOptions, add:
Converters = { new JsonStringEnumConverter(), new MaterialGraphJsonConverter() }
```

### 7A.4: Build Gate

```bash
dotnet build KnobForge.Core
dotnet build KnobForge.App
```

Both must succeed. No runtime changes yet — the graph is purely a data model at this point.

---

## Subphase 7B: Node Type Library

### Directory: `KnobForge.Core/MaterialGraph/Nodes/`

Create one file per node type. Each node class:
1. Inherits `GraphNode`
2. Overrides `TypeId` (string constant)
3. Overrides `GetPorts()` (declares inputs and outputs with types and defaults)
4. Overrides `Evaluate(GraphEvaluationContext context)` (reads inputs, computes outputs, writes to context)
5. Has public properties for any user-configurable parameters (serializable)

### Helper: Port Value Conversion

The evaluation context stores all port values as `float[]`. Provide helper methods:

```csharp
// In a static helper class or as extension methods:
public static float ToFloat(float[] v) => v.Length > 0 ? v[0] : 0f;
public static Vector2 ToFloat2(float[] v) => new(v.Length > 0 ? v[0] : 0f, v.Length > 1 ? v[1] : 0f);
public static Vector3 ToFloat3(float[] v) => new(v.Length > 0 ? v[0] : 0f, v.Length > 1 ? v[1] : 0f, v.Length > 2 ? v[2] : 0f);
public static Vector4 ToFloat4(float[] v) => new(v.Length > 0 ? v[0] : 0f, v.Length > 1 ? v[1] : 0f, v.Length > 2 ? v[2] : 0f, v.Length > 3 ? v[3] : 0f);

public static float[] FromFloat(float f) => new[] { f };
public static float[] FromFloat2(Vector2 v) => new[] { v.X, v.Y };
public static float[] FromFloat3(Vector3 v) => new[] { v.X, v.Y, v.Z };
public static float[] FromFloat4(Vector4 v) => new[] { v.X, v.Y, v.Z, v.W };

// Float broadcast: if input is Float and target is Float3, replicate to all channels
public static float[] BroadcastTo(float[] value, int targetDimension)
{
    if (value.Length >= targetDimension) return value;
    if (value.Length == 1)
    {
        var result = new float[targetDimension];
        Array.Fill(result, value[0]);
        return result;
    }
    // Pad with zeros
    var padded = new float[targetDimension];
    Array.Copy(value, padded, Math.Min(value.Length, targetDimension));
    return padded;
}
```

### 7B.1: Input Nodes

#### `ConstantNode.cs`
- TypeId: `"Constant"`
- Parameters: `float Value = 0f`
- Outputs: `Value` (Float)
- Evaluate: write `FromFloat(Value)` to output

#### `ConstantFloat3Node.cs`
- TypeId: `"ConstantFloat3"`
- Parameters: `float X = 0f, Y = 0f, Z = 0f`
- Outputs: `Value` (Float3)

#### `UVInputNode.cs`
- TypeId: `"UVInput"`
- No parameters
- Outputs: `UV` (Float2)
- Evaluate: write `FromFloat2(context.UV)` to output

#### `WorldPositionNode.cs`
- TypeId: `"WorldPosition"`
- Outputs: `Position` (Float3)
- Evaluate: write `FromFloat3(context.WorldPosition)` to output

#### `VertexNormalNode.cs`
- TypeId: `"VertexNormal"`
- Outputs: `Normal` (Float3)

#### `TextureMapNode.cs`
- TypeId: `"TextureMap"`
- Parameters: `string FilePath = ""`, `float TilingX = 1f, TilingY = 1f`, `float OffsetX = 0f, OffsetY = 0f`
- Inputs: `UV` (Float2, default [0,0] — if unconnected, uses context.UV)
- Outputs: `Color` (Float4), `R` (Float), `G` (Float), `B` (Float), `A` (Float)
- Evaluate:
  1. Get UV from input (or context.UV if unconnected)
  2. Apply tiling + offset: `u = uv.X * TilingX + OffsetX`, `v = uv.Y * TilingY + OffsetY`
  3. Look up texture in `context.LoadedTextures[FilePath]`
  4. Sample bilinearly
  5. Write Color, R, G, B, A outputs

### 7B.2: Math Nodes

#### `ArithmeticNode.cs`
- TypeId: `"Arithmetic"`
- Parameters: `ArithmeticOp Operation = ArithmeticOp.Add` (enum: Add, Subtract, Multiply, Divide)
- Inputs: `A` (Float, default [0]), `B` (Float, default [0])
- Outputs: `Result` (Float)
- For Divide: if B ≈ 0, return 0

#### `ArithmeticFloat3Node.cs`
- TypeId: `"ArithmeticFloat3"`
- Same as above but operates on Float3 component-wise

#### `LerpNode.cs`
- TypeId: `"Lerp"`
- Inputs: `A` (Float3, default [0,0,0]), `B` (Float3, default [1,1,1]), `T` (Float, default [0.5])
- Outputs: `Result` (Float3)

#### `ClampNode.cs`
- TypeId: `"Clamp"`
- Inputs: `Value` (Float), `Min` (Float, default [0]), `Max` (Float, default [1])
- Outputs: `Result` (Float)

#### `RemapNode.cs`
- TypeId: `"Remap"`
- Inputs: `Value` (Float), `InMin` (Float, default [0]), `InMax` (Float, default [1]), `OutMin` (Float, default [0]), `OutMax` (Float, default [1])
- Outputs: `Result` (Float)
- Formula: `outMin + (value - inMin) / (inMax - inMin) * (outMax - outMin)`

#### `PowerNode.cs`
- TypeId: `"Power"`
- Inputs: `Base` (Float, default [0]), `Exponent` (Float, default [1])
- Outputs: `Result` (Float)

#### `DotProductNode.cs`
- TypeId: `"DotProduct"`
- Inputs: `A` (Float3), `B` (Float3)
- Outputs: `Result` (Float)

#### `NormalizeNode.cs`
- TypeId: `"Normalize"`
- Inputs: `Value` (Float3)
- Outputs: `Result` (Float3)

### 7B.3: Pattern/Procedural Nodes

All noise nodes should use the **existing Hash21/ValueNoise2 functions** from TextureBaker. Either:
- Move them to a shared static class `KnobForge.Core.MaterialGraph.NoiseUtils` (preferred), or
- Duplicate them in the Nodes namespace

#### `PerlinNoiseNode.cs`
- TypeId: `"PerlinNoise"`
- Parameters: `float Scale = 8f`, `int Octaves = 4`, `float Persistence = 0.5f`, `float Lacunarity = 2f`, `int Seed = 0`
- Inputs: `UV` (Float2, default from context.UV)
- Outputs: `Value` (Float), `Color` (Float3 — grayscale mapped)
- Implementation: Fractal noise using ValueNoise2 with octave stacking
  ```
  float value = 0, amplitude = 1, frequency = Scale, totalAmp = 0;
  for (int i = 0; i < Octaves; i++) {
      float n = ValueNoise2((uv.X + Seed * 17.3f) * frequency, (uv.Y + Seed * 31.7f) * frequency);
      value += n * amplitude;
      totalAmp += amplitude;
      amplitude *= Persistence;
      frequency *= Lacunarity;
  }
  value /= totalAmp; // Normalize to [0,1]
  ```

#### `VoronoiNode.cs`
- TypeId: `"Voronoi"`
- Parameters: `float Scale = 5f`, `float Jitter = 1f`, `int Seed = 0`
- Inputs: `UV` (Float2)
- Outputs: `Distance` (Float), `CellValue` (Float)
- Implementation: Standard Voronoi F1 distance using Hash21 for cell center jitter

#### `GradientNode.cs`
- TypeId: `"Gradient"`
- Parameters: `GradientType Type = GradientType.Linear` (enum: Linear, Radial, Angular), `float Rotation = 0f`
- Inputs: `UV` (Float2)
- Outputs: `Value` (Float)

#### `CheckerNode.cs`
- TypeId: `"Checker"`
- Parameters: `float Scale = 4f`
- Inputs: `UV` (Float2)
- Outputs: `Value` (Float) — 0 or 1 alternating

#### `BrickNode.cs`
- TypeId: `"Brick"`
- Parameters: `float BrickWidth = 0.5f`, `float BrickHeight = 0.25f`, `float MortarWidth = 0.05f`, `float RowOffset = 0.5f`
- Inputs: `UV` (Float2)
- Outputs: `Value` (Float — 1 inside brick, 0 in mortar), `Mortar` (Float — inverse)

### 7B.4: Color Nodes

#### `ColorRampNode.cs`
- TypeId: `"ColorRamp"`
- Parameters: `List<GradientStop> Stops` where `GradientStop { float Position; float R, G, B; }`
  - Default: 2 stops — black at 0.0, white at 1.0
- Inputs: `Value` (Float, default [0])
- Outputs: `Color` (Float3)
- Evaluate: Linearly interpolate between stops based on input value

#### `HSVToRGBNode.cs`
- TypeId: `"HSVToRGB"`
- Inputs: `H` (Float, default [0]), `S` (Float, default [1]), `V` (Float, default [1])
- Outputs: `Color` (Float3)

#### `RGBToHSVNode.cs`
- TypeId: `"RGBToHSV"`
- Inputs: `Color` (Float3, default [1,1,1])
- Outputs: `H` (Float), `S` (Float), `V` (Float)

#### `BrightnessContrastNode.cs`
- TypeId: `"BrightnessContrast"`
- Inputs: `Color` (Float3), `Brightness` (Float, default [0]), `Contrast` (Float, default [1])
- Outputs: `Color` (Float3)

### 7B.5: Output Node

#### `PBROutputNode.cs`
- TypeId: `"PBROutput"`
- Inputs:
  - `Albedo` (Color/Float3, default [0.8, 0.8, 0.8] — light gray)
  - `Normal` (Float3, default [0.5, 0.5, 1.0] — flat normal in tangent space)
  - `Roughness` (Float, default [0.5])
  - `Metallic` (Float, default [0.0])
  - `Emission` (Float3, default [0, 0, 0])
  - `Alpha` (Float, default [1.0])
- Outputs: none (terminal node)
- Evaluate: Read all input values and store them as named outputs so the evaluator can extract them
  - Write to: `("Albedo", fromFloat3)`, `("Roughness", fromFloat)`, `("Metallic", fromFloat)`, `("Normal", fromFloat3)`, `("Emission", fromFloat3)`, `("Alpha", fromFloat)`

### 7B.6: Register All Nodes

In `GraphNodeTypeRegistry` static constructor, register every node type:

```csharp
static GraphNodeTypeRegistry()
{
    Register("Constant", typeof(ConstantNode));
    Register("ConstantFloat3", typeof(ConstantFloat3Node));
    Register("UVInput", typeof(UVInputNode));
    Register("WorldPosition", typeof(WorldPositionNode));
    Register("VertexNormal", typeof(VertexNormalNode));
    Register("TextureMap", typeof(TextureMapNode));
    Register("Arithmetic", typeof(ArithmeticNode));
    Register("ArithmeticFloat3", typeof(ArithmeticFloat3Node));
    Register("Lerp", typeof(LerpNode));
    Register("Clamp", typeof(ClampNode));
    Register("Remap", typeof(RemapNode));
    Register("Power", typeof(PowerNode));
    Register("DotProduct", typeof(DotProductNode));
    Register("Normalize", typeof(NormalizeNode));
    Register("PerlinNoise", typeof(PerlinNoiseNode));
    Register("Voronoi", typeof(VoronoiNode));
    Register("Gradient", typeof(GradientNode));
    Register("Checker", typeof(CheckerNode));
    Register("Brick", typeof(BrickNode));
    Register("ColorRamp", typeof(ColorRampNode));
    Register("HSVToRGB", typeof(HSVToRGBNode));
    Register("RGBToHSV", typeof(RGBToHSVNode));
    Register("BrightnessContrast", typeof(BrightnessContrastNode));
    Register("PBROutput", typeof(PBROutputNode));
}
```

### 7B.7: Build Gate

```bash
dotnet build KnobForge.Core
```

Must succeed. Write unit-style smoke tests if possible (create a simple graph, topologically sort, evaluate).

---

## Subphase 7C: CPU Evaluation Engine

### File: `KnobForge.Core/MaterialGraph/GraphEvaluator.cs`

```csharp
namespace KnobForge.Core.MaterialGraph
{
    public static class GraphEvaluator
    {
        /// <summary>
        /// Evaluate the material graph at a single texel.
        /// Returns the PBR material output values.
        /// </summary>
        public static MaterialOutput EvaluateAtTexel(
            MaterialGraph graph,
            float u, float v,
            GraphEvaluationContext context)
        {
            context.Clear();
            context.UV = new Vector2(u, v);

            // Topological sort (cache this for the whole bake, not per-texel!)
            var sorted = graph.TopologicalSort();

            // Evaluate each node in order
            foreach (var node in sorted)
            {
                node.Evaluate(context);
            }

            // Extract PBR output
            var outputNode = graph.FindOutputNode();
            if (outputNode == null)
            {
                return MaterialOutput.Default;
            }

            return new MaterialOutput
            {
                Albedo = PortHelpers.ToFloat3(context.GetPortValue(outputNode.Id, "Albedo")),
                Normal = PortHelpers.ToFloat3(context.GetPortValue(outputNode.Id, "Normal")),
                Roughness = PortHelpers.ToFloat(context.GetPortValue(outputNode.Id, "Roughness")),
                Metallic = PortHelpers.ToFloat(context.GetPortValue(outputNode.Id, "Metallic")),
                Emission = PortHelpers.ToFloat3(context.GetPortValue(outputNode.Id, "Emission")),
                Alpha = PortHelpers.ToFloat(context.GetPortValue(outputNode.Id, "Alpha"))
            };
        }

        /// <summary>
        /// Evaluate the full graph at every texel of a target resolution.
        /// Returns four RGBA8 buffers: albedo, normal, roughness (grayscale), metallic (grayscale).
        /// </summary>
        public static GraphBakeResult BakeGraph(
            MaterialGraph graph,
            int width, int height,
            Dictionary<string, TextureData> textures)
        {
            var sorted = graph.TopologicalSort(); // Cache once
            var context = new GraphEvaluationContext();
            foreach (var (path, tex) in textures)
            {
                context.LoadedTextures[path] = tex;
            }

            byte[] albedo = new byte[width * height * 4];
            byte[] normal = new byte[width * height * 4];
            byte[] roughness = new byte[width * height * 4];
            byte[] metallic = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;

                    context.Clear();
                    context.UV = new Vector2(u, v);

                    foreach (var node in sorted)
                    {
                        node.Evaluate(context);
                    }

                    var outputNode = graph.FindOutputNode();
                    var output = outputNode != null
                        ? ExtractOutput(context, outputNode)
                        : MaterialOutput.Default;

                    int offset = (y * width + x) * 4;
                    WriteColorToBuffer(albedo, offset, output.Albedo);
                    WriteColorToBuffer(normal, offset, output.Normal);
                    WriteGrayscaleToBuffer(roughness, offset, output.Roughness);
                    WriteGrayscaleToBuffer(metallic, offset, output.Metallic);
                }
            }

            return new GraphBakeResult(width, height, albedo, normal, roughness, metallic);
        }

        private static MaterialOutput ExtractOutput(GraphEvaluationContext ctx, GraphNode outputNode)
        {
            return new MaterialOutput
            {
                Albedo = PortHelpers.ToFloat3(ctx.GetPortValue(outputNode.Id, "Albedo")),
                Normal = PortHelpers.ToFloat3(ctx.GetPortValue(outputNode.Id, "Normal")),
                Roughness = PortHelpers.ToFloat(ctx.GetPortValue(outputNode.Id, "Roughness")),
                Metallic = PortHelpers.ToFloat(ctx.GetPortValue(outputNode.Id, "Metallic")),
                Emission = PortHelpers.ToFloat3(ctx.GetPortValue(outputNode.Id, "Emission")),
                Alpha = PortHelpers.ToFloat(ctx.GetPortValue(outputNode.Id, "Alpha"))
            };
        }

        private static void WriteColorToBuffer(byte[] buffer, int offset, Vector3 color)
        {
            buffer[offset]     = (byte)Math.Clamp(color.X * 255f + 0.5f, 0, 255);
            buffer[offset + 1] = (byte)Math.Clamp(color.Y * 255f + 0.5f, 0, 255);
            buffer[offset + 2] = (byte)Math.Clamp(color.Z * 255f + 0.5f, 0, 255);
            buffer[offset + 3] = 255;
        }

        private static void WriteGrayscaleToBuffer(byte[] buffer, int offset, float value)
        {
            byte b = (byte)Math.Clamp(value * 255f + 0.5f, 0, 255);
            buffer[offset] = b;
            buffer[offset + 1] = b;
            buffer[offset + 2] = b;
            buffer[offset + 3] = 255;
        }
    }

    public readonly struct MaterialOutput
    {
        public Vector3 Albedo { get; init; }
        public Vector3 Normal { get; init; }
        public float Roughness { get; init; }
        public float Metallic { get; init; }
        public Vector3 Emission { get; init; }
        public float Alpha { get; init; }

        public static MaterialOutput Default => new()
        {
            Albedo = new Vector3(0.8f, 0.8f, 0.8f),
            Normal = new Vector3(0.5f, 0.5f, 1f),
            Roughness = 0.5f,
            Metallic = 0f,
            Emission = Vector3.Zero,
            Alpha = 1f
        };
    }

    public readonly struct GraphBakeResult
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Albedo { get; }
        public byte[] Normal { get; }
        public byte[] Roughness { get; }
        public byte[] Metallic { get; }

        public GraphBakeResult(int w, int h, byte[] albedo, byte[] normal, byte[] roughness, byte[] metallic)
        {
            Width = w; Height = h;
            Albedo = albedo; Normal = normal; Roughness = roughness; Metallic = metallic;
        }
    }
}
```

### 7C.2: Integration with TextureBaker

In `TextureBaker.cs`, modify the bake path to check if the material has a graph:

```csharp
// At the top of the bake method, BEFORE the existing per-texel loop:
if (material.Graph != null && material.Graph.FindOutputNode() != null)
{
    // Use graph evaluator instead of legacy path
    var textures = LoadGraphTextures(material.Graph);
    var graphResult = GraphEvaluator.BakeGraph(material.Graph, width, height, textures);
    // Write graphResult buffers to output images
    // ... (copy to existing output format)
    return;
}

// Existing legacy path continues below unchanged
```

This is the ONLY change to TextureBaker — an early-return branch. The legacy path is untouched.

### 7C.3: Build Gate

```bash
dotnet build KnobForge.Core
dotnet build KnobForge.Rendering
dotnet build KnobForge.App
```

---

## Subphase 7D: Property-Panel Graph UI

### 7D.1: New Tab in Inspector

Add a new `TabItem` to `MainWindow.axaml` in the `InspectorTabControl`:

```xml
<TabItem x:Name="MaterialGraphTabItem" Header="Graph">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel x:Name="MaterialGraphPanel" Spacing="8" Margin="4">
            <TextBlock Text="Material Graph" FontSize="18" FontWeight="SemiBold"/>
            <TextBlock Text="Build procedural materials by connecting nodes"
                       FontSize="11" Foreground="#A9B4BF"/>

            <!-- Graph enable toggle -->
            <CheckBox x:Name="GraphEnabledCheckBox" Content="Enable Material Graph"/>

            <!-- Validation status -->
            <TextBlock x:Name="GraphValidationText" FontSize="11" Foreground="#FF6B6B"/>

            <!-- Node list -->
            <Expander Header="Nodes" IsExpanded="True">
                <StackPanel Spacing="4">
                    <ListBox x:Name="GraphNodeListBox" Height="150"/>
                    <StackPanel Orientation="Horizontal" Spacing="4">
                        <ComboBox x:Name="AddNodeTypeCombo" Width="160"/>
                        <Button x:Name="AddNodeButton" Content="Add Node" Width="80"/>
                        <Button x:Name="RemoveNodeButton" Content="Remove" Width="70"/>
                    </StackPanel>
                </StackPanel>
            </Expander>

            <!-- Selected node properties -->
            <Expander x:Name="NodePropertiesExpander" Header="Node Properties" IsExpanded="True">
                <StackPanel x:Name="NodePropertiesPanel" Spacing="4"/>
            </Expander>

            <!-- Connections -->
            <Expander Header="Connections" IsExpanded="True">
                <StackPanel Spacing="4">
                    <ListBox x:Name="GraphConnectionListBox" Height="120"/>
                    <StackPanel Orientation="Horizontal" Spacing="4">
                        <Button x:Name="AddConnectionButton" Content="Connect..." Width="90"/>
                        <Button x:Name="RemoveConnectionButton" Content="Disconnect" Width="90"/>
                    </StackPanel>
                </StackPanel>
            </Expander>

            <!-- Preview -->
            <Expander Header="Preview" IsExpanded="False">
                <StackPanel Spacing="4">
                    <Button x:Name="GraphPreviewBakeButton" Content="Preview Bake (256×256)" Width="180"/>
                    <Image x:Name="GraphPreviewImage" Width="256" Height="256"/>
                </StackPanel>
            </Expander>
        </StackPanel>
    </ScrollViewer>
</TabItem>
```

### 7D.2: Graph UI Code-Behind

Create a new partial class file: `MainWindow.MaterialGraphEditor.cs`

This file handles:

1. **Graph enable/disable**: Toggle creates/removes the `MaterialGraph` on the current `MaterialNode`
2. **Node list**: Populate `GraphNodeListBox` with node type + ID. Selection triggers property editor update.
3. **Add node**: `AddNodeTypeCombo` shows categorized node types from the registry. "Add Node" creates and adds.
4. **Remove node**: Removes selected node and its connections.
5. **Node properties**: When a node is selected, dynamically build property editors:
   - `float` parameters → `ValueInput` controls (reusing Phase 6!)
   - `string` parameters (file paths) → TextBox + Browse button
   - `enum` parameters → ComboBox
   - `int` parameters → ValueInput with Step=1, DecimalPlaces=0
   - `List<GradientStop>` → simplified gradient editor or manual entry
6. **Connection management**: Show connections as "SourceNode.Port → TargetNode.Port" in list. Add Connection opens a dialog/dropdown to pick source output → target input.
7. **Validation**: Run `graph.Validate()` on every change, display errors in `GraphValidationText`.
8. **Preview**: Click "Preview Bake" → call `GraphEvaluator.BakeGraph()` at 256×256 → display albedo result as `WriteableBitmap` in `GraphPreviewImage`.

### 7D.3: Connection Dialog Pattern

For connecting ports, use a simple two-step flow:
1. User clicks "Connect..."
2. A pair of ComboBoxes appears:
   - **Source**: `NodeName : OutputPortName` (filtered to only output ports)
   - **Target**: `NodeName : InputPortName` (filtered to only input ports of the selected node, or any node)
3. User selects both and clicks "OK" → calls `graph.Connect()`
4. Validation runs automatically

### 7D.4: Build Gate

```bash
dotnet build KnobForge.App
```

---

## Current State (Verified)

### TextureBaker.cs (`KnobForge.Rendering/TextureBaker.cs`)

Key evaluation method:
```csharp
private static void EvaluateMaterialAtTexel(
    float u, float v,
    Vector3 baseMaterialColor, float baseMaterialRoughness, float baseMaterialMetallic,
    Vector4 paintSample, Vector4 colorPaintSample, Vector4 paintMask2Sample,
    Vector3? textureAlbedo, float? textureRoughness, float? textureMetallic,
    float rustAmount, float wearAmount, float gunkAmount,
    float brushDarkness, float paintCoatRoughness, float paintCoatMetallic,
    Vector3 scratchExposeColor, float scratchExposeRoughness, float scratchExposeMetallic,
    out Vector3 finalAlbedo, out float finalRoughness, out float finalMetallic)
```

Noise functions (lines 600-625): `Hash21(float x, float y)` and `ValueNoise2(float x, float y)` — static private methods. Move to or duplicate in a shared `NoiseUtils` class for graph nodes to use.

### MaterialNode.cs (`KnobForge.Core/Scene/MaterialNode.cs`)

Key properties that map to PBROutput defaults:
```csharp
public Vector3 BaseColor { get; set; } = new Vector3(0.55f, 0.16f, 0.16f);
public float Metallic { get; set; }        // [0, 1]
public float Roughness { get; set; }       // [0.04, 1]
public string? AlbedoMapPath { get; set; }
public string? NormalMapPath { get; set; }
public string? RoughnessMapPath { get; set; }
public string? MetallicMapPath { get; set; }
```

### Project Serialization

- Format: `knobforge.project.v1`
- Serializer: `System.Text.Json` with `JsonStringEnumConverter`
- Pattern: additive-safe (unknown properties silently ignored on deserialize)
- Envelope: `KnobProjectFileEnvelope` → `SnapshotJson` → `MaterialNodeSnapshot`

### MainWindow Inspector Tabs (7 current)

```
Lighting | Model | Brush | Camera | Environment | Glare & Effects | Shadows
```

The "Graph" tab will be added after "Shadows" (or after "Model" if preferred).

### Namespace Conventions

```
KnobForge.Core.MaterialGraph       — Graph data model, evaluator
KnobForge.Core.MaterialGraph.Nodes — Individual node types
KnobForge.App.Views                — Graph UI (MainWindow partial)
```

---

## File Impact Summary

### New Files (~30 files)

| File | Purpose |
|------|---------|
| `KnobForge.Core/MaterialGraph/PortType.cs` | Port type and direction enums |
| `KnobForge.Core/MaterialGraph/GraphPort.cs` | Port definition class |
| `KnobForge.Core/MaterialGraph/GraphNode.cs` | Abstract base node class |
| `KnobForge.Core/MaterialGraph/GraphConnection.cs` | Connection data class |
| `KnobForge.Core/MaterialGraph/MaterialGraph.cs` | Graph container with validation and topological sort |
| `KnobForge.Core/MaterialGraph/GraphEvaluationContext.cs` | Per-texel evaluation state |
| `KnobForge.Core/MaterialGraph/GraphEvaluator.cs` | CPU evaluation engine |
| `KnobForge.Core/MaterialGraph/GraphNodeTypeRegistry.cs` | TypeId → Type registry |
| `KnobForge.Core/MaterialGraph/MaterialGraphJsonConverter.cs` | JSON serialization |
| `KnobForge.Core/MaterialGraph/PortHelpers.cs` | Float/Vector conversion helpers |
| `KnobForge.Core/MaterialGraph/NoiseUtils.cs` | Shared Hash21/ValueNoise2 |
| `KnobForge.Core/MaterialGraph/Nodes/ConstantNode.cs` | Constant float |
| `KnobForge.Core/MaterialGraph/Nodes/ConstantFloat3Node.cs` | Constant vector |
| `KnobForge.Core/MaterialGraph/Nodes/UVInputNode.cs` | UV coordinate input |
| `KnobForge.Core/MaterialGraph/Nodes/WorldPositionNode.cs` | Position input |
| `KnobForge.Core/MaterialGraph/Nodes/VertexNormalNode.cs` | Normal input |
| `KnobForge.Core/MaterialGraph/Nodes/TextureMapNode.cs` | Texture sampling |
| `KnobForge.Core/MaterialGraph/Nodes/ArithmeticNode.cs` | Math ops (float) |
| `KnobForge.Core/MaterialGraph/Nodes/ArithmeticFloat3Node.cs` | Math ops (float3) |
| `KnobForge.Core/MaterialGraph/Nodes/LerpNode.cs` | Linear interpolation |
| `KnobForge.Core/MaterialGraph/Nodes/ClampNode.cs` | Value clamp |
| `KnobForge.Core/MaterialGraph/Nodes/RemapNode.cs` | Range remap |
| `KnobForge.Core/MaterialGraph/Nodes/PowerNode.cs` | Power function |
| `KnobForge.Core/MaterialGraph/Nodes/DotProductNode.cs` | Dot product |
| `KnobForge.Core/MaterialGraph/Nodes/NormalizeNode.cs` | Vector normalize |
| `KnobForge.Core/MaterialGraph/Nodes/PerlinNoiseNode.cs` | Fractal noise |
| `KnobForge.Core/MaterialGraph/Nodes/VoronoiNode.cs` | Voronoi cells |
| `KnobForge.Core/MaterialGraph/Nodes/GradientNode.cs` | Gradient patterns |
| `KnobForge.Core/MaterialGraph/Nodes/CheckerNode.cs` | Checker pattern |
| `KnobForge.Core/MaterialGraph/Nodes/BrickNode.cs` | Brick pattern |
| `KnobForge.Core/MaterialGraph/Nodes/ColorRampNode.cs` | Color gradient |
| `KnobForge.Core/MaterialGraph/Nodes/HSVToRGBNode.cs` | HSV→RGB |
| `KnobForge.Core/MaterialGraph/Nodes/RGBToHSVNode.cs` | RGB→HSV |
| `KnobForge.Core/MaterialGraph/Nodes/BrightnessContrastNode.cs` | B/C adjust |
| `KnobForge.Core/MaterialGraph/Nodes/PBROutputNode.cs` | Terminal output |
| `KnobForge.App/Views/MainWindow.MaterialGraphEditor.cs` | Graph UI code-behind |

### Modified Files

| File | Change |
|------|--------|
| `MaterialNode.cs` | Add `MaterialGraph? Graph` property |
| `TextureBaker.cs` | Add graph-bake early-return branch (move Hash21/ValueNoise2 to shared) |
| `MainWindow.axaml` | Add Material Graph tab |
| `MainWindow/MainWindow.cs` | Add graph editor field declarations |
| `MainWindow.Initialization.cs` | Wire graph editor handlers |
| `KnobProjectFileStore.cs` | Register `MaterialGraphJsonConverter` |
| `InspectorNavigationAndHistory/...Types.cs` | Add `MaterialGraphJson` to snapshot (optional) |

---

## Build & Verification Checklist

- [ ] `dotnet build` succeeds for all projects
- [ ] Graph data model: create graph, add nodes, connect ports, topological sort works
- [ ] Cycle detection: connecting A→B→A raises error
- [ ] Type validation: Float→Float3 compatible, Texture2D→Float incompatible
- [ ] Noise nodes: PerlinNoise output matches TextureBaker's ValueNoise2 (same algorithm)
- [ ] Voronoi: produces distinct cell regions
- [ ] PBROutput: correctly collects all material channels
- [ ] Evaluator: simple graph (Noise→ColorRamp→PBROutput.Albedo) produces expected gradient
- [ ] Serialization: save project with graph, reload, graph is intact
- [ ] Serialization: load old project without graph, `Graph` is null, legacy path works
- [ ] UI: can create nodes from dropdown, edit parameters, connect/disconnect ports
- [ ] UI: validation errors display in real-time
- [ ] Preview bake: 256×256 preview image renders correctly
- [ ] TextureBaker: when `material.Graph != null`, uses graph path; when null, legacy path unchanged
