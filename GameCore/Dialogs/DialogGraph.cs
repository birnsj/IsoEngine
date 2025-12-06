namespace GameCore.Dialogs;

/// <summary>
/// Represents a complete dialog graph (conversation tree).
/// </summary>
public class DialogGraph
{
    /// <summary>
    /// Gets or sets the unique identifier of this dialog graph.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name/description of this dialog graph.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the starting node.
    /// </summary>
    public string StartNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dictionary of dialog nodes, keyed by their IDs.
    /// </summary>
    public Dictionary<string, DialogNode> Nodes { get; set; } = new();

    /// <summary>
    /// Gets a dialog node by its ID.
    /// </summary>
    /// <param name="nodeId">The ID of the node to retrieve.</param>
    /// <returns>The dialog node, or null if not found.</returns>
    public DialogNode? GetNode(string nodeId)
    {
        return Nodes.TryGetValue(nodeId, out var node) ? node : null;
    }

    /// <summary>
    /// Gets the starting node.
    /// </summary>
    /// <returns>The starting dialog node, or null if not found.</returns>
    public DialogNode? GetStartNode()
    {
        return GetNode(StartNodeId);
    }
}

