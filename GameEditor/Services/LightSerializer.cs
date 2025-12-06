using System.IO;
using System.Text.Json;
using GameEditor.Data;

namespace GameEditor.Services;

/// <summary>
/// Service for serializing and deserializing light layouts.
/// </summary>
public static class LightSerializer
{
    /// <summary>
    /// Saves a light layout to a JSON file.
    /// </summary>
    public static void SaveToJson(LightLayoutData layout, string filePath)
    {
        // Ensure file is ready for editing in Perforce
        PerforceService.EnsureFileReadyForEdit(filePath);
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(layout, options);
        File.WriteAllText(filePath, json);
        
        // Add file to Perforce if it's new
        PerforceService.AddFile(filePath);
    }

    /// <summary>
    /// Loads a light layout from a JSON file.
    /// </summary>
    public static LightLayoutData? LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<LightLayoutData>(json);
        }
        catch
        {
            return null;
        }
    }
}

