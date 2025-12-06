using System.Collections.Generic;

namespace GameEditor.Data;

/// <summary>
/// Serializable representation of a tile map for saving/loading.
/// </summary>
public class TileMapData
{
    /// <summary>
    /// Gets or sets the width of the map in tiles.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the map in tiles.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the width of a single tile in pixels.
    /// </summary>
    public int TileWidth { get; set; }

    /// <summary>
    /// Gets or sets the height of a single tile in pixels.
    /// </summary>
    public int TileHeight { get; set; }

    /// <summary>
    /// Gets or sets the 2D array of tile indices.
    /// Stored as list of lists for JSON serialization.
    /// </summary>
    public List<List<int>> Tiles { get; set; } = new();


    /// <summary>
    /// Gets or sets the player start position in world coordinates (tile coordinates).
    /// If null, the player will spawn at the map center.
    /// </summary>
    public PositionData? PlayerStartPosition { get; set; }

    /// <summary>
    /// Gets or sets a dictionary mapping tile indices to their custom graphic file paths.
    /// This is optional - if not set, tiles will be loaded from the shared tiles.json library.
    /// Paths are relative to GameContent directory or absolute paths.
    /// </summary>
    public Dictionary<int, string>? TileGraphics { get; set; }

    /// <summary>
    /// Gets the tile index at the specified coordinates.
    /// </summary>
    public int GetTile(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return -1;
        return Tiles[y][x];
    }

    /// <summary>
    /// Sets the tile index at the specified coordinates.
    /// </summary>
    public void SetTile(int x, int y, int tileIndex)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            Tiles[y][x] = tileIndex;
        }
    }

    /// <summary>
    /// Initializes the tile data structure with default values.
    /// </summary>
    public void InitializeTiles()
    {
        Tiles.Clear();
        for (int y = 0; y < Height; y++)
        {
            var row = new List<int>();
            for (int x = 0; x < Width; x++)
            {
                row.Add(0); // Default to tile index 0
            }
            Tiles.Add(row);
        }
    }
}



