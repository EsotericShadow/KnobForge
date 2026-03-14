namespace KnobForge.Core.MaterialGraph;

public sealed class MaterialGraph
{
    public List<GraphNode> Nodes { get; set; } = new();
    public List<GraphConnection> Connections { get; set; } = new();

    public GraphNode? GetNodeById(Guid id) => Nodes.FirstOrDefault(n => n.Id == id);

    public void AddNode(GraphNode node)
    {
        if (Nodes.Any(n => n.Id == node.Id))
        {
            return;
        }

        Nodes.Add(node);
    }

    public void RemoveNode(Guid nodeId)
    {
        Nodes.RemoveAll(n => n.Id == nodeId);
        Connections.RemoveAll(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId);
    }

    public bool Connect(Guid sourceNodeId, string sourcePort, Guid targetNodeId, string targetPort)
    {
        Connections.RemoveAll(c => c.TargetNodeId == targetNodeId && string.Equals(c.TargetPortName, targetPort, StringComparison.Ordinal));

        if (GetNodeById(sourceNodeId) == null || GetNodeById(targetNodeId) == null)
        {
            return false;
        }

        if (WouldCreateCycle(sourceNodeId, targetNodeId))
        {
            return false;
        }

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
        Connections.RemoveAll(c => c.TargetNodeId == targetNodeId && string.Equals(c.TargetPortName, targetPort, StringComparison.Ordinal));
    }

    public List<GraphNode> TopologicalSort()
    {
        var sorted = new List<GraphNode>();
        var visited = new HashSet<Guid>();
        var visiting = new HashSet<Guid>();

        foreach (GraphNode node in Nodes)
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
        {
            throw new InvalidOperationException($"Cycle detected at node '{node.TypeId}' ({node.Id})");
        }

        if (visited.Contains(node.Id))
        {
            return;
        }

        visiting.Add(node.Id);
        foreach (GraphConnection conn in Connections.Where(c => c.TargetNodeId == node.Id))
        {
            GraphNode? sourceNode = GetNodeById(conn.SourceNodeId);
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
        if (sourceNodeId == targetNodeId)
        {
            return true;
        }

        return CanReach(targetNodeId, sourceNodeId, new HashSet<Guid>());
    }

    private bool CanReach(Guid fromNodeId, Guid toNodeId, HashSet<Guid> visited)
    {
        if (fromNodeId == toNodeId)
        {
            return true;
        }

        if (!visited.Add(fromNodeId))
        {
            return false;
        }

        foreach (GraphConnection conn in Connections.Where(c => c.SourceNodeId == fromNodeId))
        {
            if (CanReach(conn.TargetNodeId, toNodeId, visited))
            {
                return true;
            }
        }

        return false;
    }

    public GraphNode? FindOutputNode() => Nodes.FirstOrDefault(n => string.Equals(n.TypeId, "PBROutput", StringComparison.Ordinal));

    public List<string> Validate()
    {
        var errors = new List<string>();

        List<GraphNode> outputNodes = Nodes.Where(n => string.Equals(n.TypeId, "PBROutput", StringComparison.Ordinal)).ToList();
        if (outputNodes.Count == 0)
        {
            errors.Add("Graph must contain exactly one PBR Output node.");
        }
        else if (outputNodes.Count > 1)
        {
            errors.Add("Graph contains multiple PBR Output nodes - only one is allowed.");
        }

        try
        {
            TopologicalSort();
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
        }

        foreach (GraphConnection conn in Connections)
        {
            GraphNode? sourceNode = GetNodeById(conn.SourceNodeId);
            GraphNode? targetNode = GetNodeById(conn.TargetNodeId);
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

            GraphPort? sourcePort = sourceNode.GetPorts().FirstOrDefault(p => p.Direction == PortDirection.Output && string.Equals(p.Name, conn.SourcePortName, StringComparison.Ordinal));
            GraphPort? targetPort = targetNode.GetPorts().FirstOrDefault(p => p.Direction == PortDirection.Input && string.Equals(p.Name, conn.TargetPortName, StringComparison.Ordinal));

            if (sourcePort == null)
            {
                errors.Add($"Node '{sourceNode.TypeId}' has no output port '{conn.SourcePortName}'");
            }

            if (targetPort == null)
            {
                errors.Add($"Node '{targetNode.TypeId}' has no input port '{conn.TargetPortName}'");
            }

            if (sourcePort != null && targetPort != null && !ArePortTypesCompatible(sourcePort.Type, targetPort.Type))
            {
                errors.Add($"Type mismatch: {sourceNode.TypeId}.{conn.SourcePortName} ({sourcePort.Type}) -> {targetNode.TypeId}.{conn.TargetPortName} ({targetPort.Type})");
            }
        }

        return errors;
    }

    private static bool ArePortTypesCompatible(PortType source, PortType target)
    {
        if (source == target)
        {
            return true;
        }

        if ((source == PortType.Color && target == PortType.Float3) ||
            (source == PortType.Float3 && target == PortType.Color))
        {
            return true;
        }

        if (source == PortType.Float && target is PortType.Float2 or PortType.Float3 or PortType.Float4 or PortType.Color)
        {
            return true;
        }

        return false;
    }
}
