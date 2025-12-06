namespace GameEditor.Controls;

/// <summary>
/// Event arguments for tile edit requests.
/// </summary>
public class TileEditEventArgs : EventArgs
{
    /// <summary>
    /// Gets the index of the tile to edit.
    /// </summary>
    public int TileIndex { get; }

    public TileEditEventArgs(int tileIndex)
    {
        TileIndex = tileIndex;
    }
}


