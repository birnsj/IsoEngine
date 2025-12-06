using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GameCore.Rendering;

/// <summary>
/// Helper class for loading tile maps from JSON files.
/// </summary>
public static class TileMapLoader
{
    /// <summary>
    /// Serializable representation of a tile map for JSON.
    /// </summary>
    private class TileMapDataJson
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public List<List<int>> Tiles { get; set; } = new();
        public List<int> SolidTiles { get; set; } = new();
        public PlayerStartPositionJson? PlayerStartPosition { get; set; }
        public Dictionary<string, string>? TileGraphics { get; set; }
    }

    private class PlayerStartPositionJson
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    /// <summary>
    /// Loads a tile map from a JSON file.
    /// </summary>
    public static IsometricTileMap? LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<TileMapDataJson>(json);
            if (data == null)
                return null;

            var tileMap = new IsometricTileMap(data.Width, data.Height, data.TileWidth, data.TileHeight);

            for (int y = 0; y < data.Height && y < data.Tiles.Count; y++)
            {
                for (int x = 0; x < data.Width && x < data.Tiles[y].Count; x++)
                {
                    tileMap.SetTile(x, y, data.Tiles[y][x]);
                }
            }

            return tileMap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Serializable representation of a tile library for JSON.
    /// </summary>
    private class TileLibraryDataJson
    {
        public Dictionary<string, string>? TileGraphics { get; set; }
    }

    /// <summary>
    /// Gets the tile graphics dictionary from the shared tiles.json library.
    /// </summary>
    public static Dictionary<int, string>? GetTileGraphicsFromLibrary(string tilesLibraryPath)
    {
        if (string.IsNullOrEmpty(tilesLibraryPath) || !File.Exists(tilesLibraryPath))
            return null;

        try
        {
            var json = File.ReadAllText(tilesLibraryPath);
            var data = JsonSerializer.Deserialize<TileLibraryDataJson>(json);
            if (data?.TileGraphics == null)
                return null;

            // Convert string keys to int keys
            var result = new Dictionary<int, string>();
            foreach (var kvp in data.TileGraphics)
            {
                if (int.TryParse(kvp.Key, out int tileIndex))
                {
                    result[tileIndex] = kvp.Value;
                }
            }

            return result.Count > 0 ? result : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the tile graphics dictionary from a JSON file.
    /// First tries to load from the shared tiles.json library, then falls back to the map file (for backward compatibility).
    /// </summary>
    public static Dictionary<int, string>? GetTileGraphics(string filePath, string? tilesLibraryPath = null)
    {
        // First try to load from shared tiles.json library
        if (!string.IsNullOrEmpty(tilesLibraryPath))
        {
            var libraryGraphics = GetTileGraphicsFromLibrary(tilesLibraryPath);
            if (libraryGraphics != null && libraryGraphics.Count > 0)
            {
                return libraryGraphics;
            }
        }

        // Fallback: load from map file (for backward compatibility)
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<TileMapDataJson>(json);
            if (data?.TileGraphics == null)
                return null;

            // Convert string keys to int keys
            var result = new Dictionary<int, string>();
            foreach (var kvp in data.TileGraphics)
            {
                if (int.TryParse(kvp.Key, out int tileIndex))
                {
                    result[tileIndex] = kvp.Value;
                }
            }

            return result.Count > 0 ? result : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the player start position from a JSON file.
    /// </summary>
    public static Microsoft.Xna.Framework.Vector2? GetPlayerStartPosition(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<TileMapDataJson>(json);
            if (data?.PlayerStartPosition != null)
            {
                return new Microsoft.Xna.Framework.Vector2(
                    data.PlayerStartPosition.X,
                    data.PlayerStartPosition.Y);
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    /// <summary>
    /// Gets the list of solid tile indices from a JSON file.
    /// </summary>
    public static List<int> GetSolidTilesFromJson(string filePath)
    {
        if (!File.Exists(filePath))
            return new List<int>();

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<TileMapDataJson>(json);
            return data?.SolidTiles ?? new List<int>();
        }
        catch
        {
            return new List<int>();
        }
    }

    /// <summary>
    /// Saves a tile map to a JSON file.
    /// </summary>
    public static bool SaveToJson(IsometricTileMap tileMap, string filePath, List<int> solidTiles, Microsoft.Xna.Framework.Vector2? playerStartPosition = null)
    {
        try
        {
            var data = new TileMapDataJson
            {
                Width = tileMap.Width,
                Height = tileMap.Height,
                TileWidth = tileMap.TileWidth,
                TileHeight = tileMap.TileHeight,
                SolidTiles = new List<int>(solidTiles)
            };

            // Set player start position if provided
            if (playerStartPosition.HasValue)
            {
                data.PlayerStartPosition = new PlayerStartPositionJson
                {
                    X = playerStartPosition.Value.X,
                    Y = playerStartPosition.Value.Y
                };
            }

            // Convert tile map data to list of lists
            for (int y = 0; y < tileMap.Height; y++)
            {
                var row = new List<int>();
                for (int x = 0; x < tileMap.Width; x++)
                {
                    row.Add(tileMap.GetTile(x, y));
                }
                data.Tiles.Add(row);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(data, options);
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

