namespace GameCore.Dialogs;

/// <summary>
/// Represents a single node in a dialog tree.
/// </summary>
public class DialogNode
{
    /// <summary>
    /// Gets or sets the unique identifier of this dialog node.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text displayed in this dialog node.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of choices available from this node.
    /// </summary>
    public List<DialogChoice> Choices { get; set; } = new();
}

