using System.IO;
using System.Text.Json;
using GameEditor.Data;

namespace GameEditor.Services;

/// <summary>
/// Service for serializing and deserializing tile maps.
/// </summary>
public static class TileMapSerializer
{
    /// <summary>
    /// Saves a tile map to a JSON file.
    /// </summary>
    public static void SaveToJson(TileMapData tileMap, string filePath)
    {
        // Ensure file is ready for editing in Perforce
        PerforceService.EnsureFileReadyForEdit(filePath);
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(tileMap, options);
        File.WriteAllText(filePath, json);
        
        // Add file to Perforce if it's new
        PerforceService.AddFile(filePath);
    }

    /// <summary>
    /// Loads a tile map from a JSON file.
    /// </summary>
    public static TileMapData? LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<TileMapData>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a TileMapData to a GameCore IsometricTileMap.
    /// </summary>
    public static GameCore.Rendering.IsometricTileMap? ToIsometricTileMap(TileMapData data)
    {
        if (data == null)
            return null;

        var tileMap = new GameCore.Rendering.IsometricTileMap(
            data.Width,
            data.Height,
            data.TileWidth,
            data.TileHeight
        );

        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                tileMap.SetTile(x, y, data.GetTile(x, y));
            }
        }

        return tileMap;
    }

    /// <summary>
    /// Converts a GameCore IsometricTileMap to TileMapData.
    /// </summary>
    public static TileMapData FromIsometricTileMap(GameCore.Rendering.IsometricTileMap tileMap)
    {
        var data = new TileMapData
        {
            Width = tileMap.Width,
            Height = tileMap.Height,
            TileWidth = tileMap.TileWidth,
            TileHeight = tileMap.TileHeight,
        };

        data.InitializeTiles();

        for (int y = 0; y < tileMap.Height; y++)
        {
            for (int x = 0; x < tileMap.Width; x++)
            {
                data.SetTile(x, y, tileMap.GetTile(x, y));
            }
        }

        return data;
    }
}




