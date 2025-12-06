namespace GameEditor.Controls;

/// <summary>
/// Event arguments for player start position set events.
/// </summary>
public class PlayerStartSetEventArgs : EventArgs
{
    /// <summary>
    /// Gets the X coordinate of the player start position in world coordinates.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Gets the Y coordinate of the player start position in world coordinates.
    /// </summary>
    public float Y { get; }

    public PlayerStartSetEventArgs(float x, float y)
    {
        X = x;
        Y = y;
    }
}


