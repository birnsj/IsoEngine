using Microsoft.Xna.Framework;

namespace GameCore.Rendering;

/// <summary>
/// Represents an isometric tile map with tile data and coordinate conversion utilities.
/// </summary>
public class IsometricTileMap
{
    private readonly int[,] _tileData;

    /// <summary>
    /// Gets the width of the map in tiles.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the map in tiles.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the width of a single tile in pixels.
    /// </summary>
    public int TileWidth { get; }

    /// <summary>
    /// Gets the height of a single tile in pixels.
    /// </summary>
    public int TileHeight { get; }

    /// <summary>
    /// Gets the tile data at the specified coordinates.
    /// </summary>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>The tile index, or -1 if out of bounds.</returns>
    public int GetTile(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return -1;
        return _tileData[y, x];
    }

    /// <summary>
    /// Sets the tile data at the specified coordinates.
    /// </summary>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <param name="tileIndex">The tile index to set.</param>
    public void SetTile(int x, int y, int tileIndex)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            _tileData[y, x] = tileIndex;
        }
    }

    /// <summary>
    /// Initializes a new instance of the IsometricTileMap class.
    /// </summary>
    /// <param name="width">Width of the map in tiles.</param>
    /// <param name="height">Height of the map in tiles.</param>
    /// <param name="tileWidth">Width of a single tile in pixels.</param>
    /// <param name="tileHeight">Height of a single tile in pixels.</param>
    public IsometricTileMap(int width, int height, int tileWidth, int tileHeight)
    {
        Width = width;
        Height = height;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        _tileData = new int[height, width];
    }

    /// <summary>
    /// Converts a world position to screen coordinates using isometric projection.
    /// 
    /// IMPORTANT: This formula must match exactly with the editor's WorldToScreen conversion
    /// in EntityPlacementCanvas.cs to ensure coordinate accuracy between editor and game.
    /// 
    /// Formula: 
    ///   screenX = (worldX - worldY) * (tileWidth / 2)
    ///   screenY = (worldX + worldY) * (tileHeight / 2)
    /// </summary>
    /// <param name="worldPos">World position (in tile coordinates, can be fractional).</param>
    /// <returns>Screen position in pixels.</returns>
    public Vector2 WorldToScreen(Vector2 worldPos)
    {
        // Isometric projection: diamond-shaped tiles
        // screenX = (worldX - worldY) * (tileWidth / 2)
        // screenY = (worldX + worldY) * (tileHeight / 2)
        var screenX = (worldPos.X - worldPos.Y) * (TileWidth / 2.0f);
        var screenY = (worldPos.X + worldPos.Y) * (TileHeight / 2.0f);
        return new Vector2(screenX, screenY);
    }

    /// <summary>
    /// Converts a screen position to world coordinates using isometric projection.
    /// 
    /// IMPORTANT: This formula must match exactly with the editor's ScreenToWorld conversion
    /// in EntityPlacementCanvas.cs to ensure coordinate accuracy between editor and game.
    /// Note: The editor's version accounts for pan/zoom, but the core formula must match.
    /// 
    /// Formula:
    ///   worldX = (screenX / (tileWidth/2) + screenY / (tileHeight/2)) / 2
    ///   worldY = (screenY / (tileHeight/2) - screenX / (tileWidth/2)) / 2
    /// </summary>
    /// <param name="screenPos">Screen position in pixels.</param>
    /// <returns>World position (in tile coordinates, can be fractional).</returns>
    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        // Inverse isometric projection
        // worldX = (screenX / (tileWidth/2) + screenY / (tileHeight/2)) / 2
        // worldY = (screenY / (tileHeight/2) - screenX / (tileWidth/2)) / 2
        var worldX = (screenPos.X / (TileWidth / 2.0f) + screenPos.Y / (TileHeight / 2.0f)) / 2.0f;
        var worldY = (screenPos.Y / (TileHeight / 2.0f) - screenPos.X / (TileWidth / 2.0f)) / 2.0f;
        return new Vector2(worldX, worldY);
    }

    /// <summary>
    /// Gets the tile coordinates from a world position.
    /// </summary>
    /// <param name="worldPos">World position (in tile coordinates).</param>
    /// <returns>Tile coordinates (x, y).</returns>
    public Point WorldToTile(Vector2 worldPos)
    {
        return new Point((int)Math.Floor(worldPos.X), (int)Math.Floor(worldPos.Y));
    }

    /// <summary>
    /// Converts a world position to screen coordinates using fixed tile size for entities.
    /// This ensures entity positions don't scale with tile size changes.
    /// Uses base tile size of 64x32 pixels.
    /// </summary>
    /// <param name="worldPos">World position (in tile coordinates, can be fractional).</param>
    /// <returns>Screen position in pixels using fixed tile size conversion.</returns>
    public Vector2 WorldToScreenFixed(Vector2 worldPos)
    {
        // Fixed tile size for entity positioning (does not scale with actual tile size)
        const int FixedTileWidth = 64;
        const int FixedTileHeight = 32;
        
        // Isometric projection using fixed tile dimensions
        var screenX = (worldPos.X - worldPos.Y) * (FixedTileWidth / 2.0f);
        var screenY = (worldPos.X + worldPos.Y) * (FixedTileHeight / 2.0f);
        return new Vector2(screenX, screenY);
    }

    /// <summary>
    /// Converts a screen position to world coordinates using fixed tile size for entities.
    /// This ensures entity positions don't scale with tile size changes.
    /// Uses base tile size of 64x32 pixels.
    /// </summary>
    /// <param name="screenPos">Screen position in pixels.</param>
    /// <returns>World position (in tile coordinates, can be fractional) using fixed tile size conversion.</returns>
    public Vector2 ScreenToWorldFixed(Vector2 screenPos)
    {
        // Fixed tile size for entity positioning (does not scale with actual tile size)
        const int FixedTileWidth = 64;
        const int FixedTileHeight = 32;
        
        // Inverse isometric projection using fixed tile dimensions
        var worldX = (screenPos.X / (FixedTileWidth / 2.0f) + screenPos.Y / (FixedTileHeight / 2.0f)) / 2.0f;
        var worldY = (screenPos.Y / (FixedTileHeight / 2.0f) - screenPos.X / (FixedTileWidth / 2.0f)) / 2.0f;
        return new Vector2(worldX, worldY);
    }
}

