namespace GameCore.Dialogs;

/// <summary>
/// Represents a choice option in a dialog node.
/// </summary>
public class DialogChoice
{
    /// <summary>
    /// Gets or sets the text displayed for this choice.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the next dialog node to transition to.
    /// If null or empty, the dialog will end.
    /// </summary>
    public string? NextNodeId { get; set; }

    /// <summary>
    /// Gets or sets the flag name required for this choice to be available.
    /// If null, the choice is always available.
    /// </summary>
    public string? RequiredFlag { get; set; }

    /// <summary>
    /// Gets or sets the required value for the flag.
    /// Only used if RequiredFlag is set.
    /// </summary>
    public bool RequiredValue { get; set; } = true;

    /// <summary>
    /// Gets or sets the flag to set when this choice is selected.
    /// </summary>
    public string? SetFlag { get; set; }

    /// <summary>
    /// Gets or sets the value to set for the flag.
    /// Only used if SetFlag is set.
    /// </summary>
    public bool SetFlagValue { get; set; } = true;
}

