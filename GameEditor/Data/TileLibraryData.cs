using System.Collections.Generic;

namespace GameEditor.Data;

/// <summary>
/// Serializable representation of a tile library for saving/loading.
/// This provides a shared tile library that can be used across multiple maps.
/// </summary>
public class TileLibraryData
{
    /// <summary>
    /// Gets or sets the total number of tiles in the library.
    /// This determines how many tiles are available in the editor.
    /// </summary>
    public int NumTiles { get; set; } = 8;
    
    /// <summary>
    /// Gets or sets a dictionary mapping tile indices to their custom graphic file paths.
    /// Paths are relative to GameContent directory or absolute paths.
    /// </summary>
    public Dictionary<int, string> TileGraphics { get; set; } = new();
    
    /// <summary>
    /// Gets or sets tile metadata (names, descriptions, etc.) - optional for future use.
    /// </summary>
    public Dictionary<int, TileMetadata>? TileMetadata { get; set; }
}

/// <summary>
/// Metadata for a tile (optional, for future use).
/// </summary>
public class TileMetadata
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

