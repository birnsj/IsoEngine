using System.IO;
using System.Text.Json;
using GameEditor.Data;

namespace GameEditor.Services;

/// <summary>
/// Service for serializing and deserializing tile libraries.
/// </summary>
public static class TileLibrarySerializer
{
    /// <summary>
    /// Saves a tile library to a JSON file.
    /// </summary>
    public static void SaveToJson(TileLibraryData tileLibrary, string filePath)
    {
        // Ensure file is ready for editing in Perforce
        PerforceService.EnsureFileReadyForEdit(filePath);
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(tileLibrary, options);
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        File.WriteAllText(filePath, json);
        
        // Add file to Perforce if it's new
        PerforceService.AddFile(filePath);
    }
    
    /// <summary>
    /// Loads a tile library from a JSON file.
    /// </summary>
    public static TileLibraryData? LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
            return null;
            
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<TileLibraryData>(json);
        }
        catch
        {
            return null;
        }
    }
}

