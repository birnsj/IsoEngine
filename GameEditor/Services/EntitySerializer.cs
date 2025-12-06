using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameEditor.Data;

namespace GameEditor.Services;

/// <summary>
/// Service for serializing and deserializing entity layouts.
/// </summary>
public static class EntitySerializer
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Saves an entity layout to a JSON file.
    /// </summary>
    public static void SaveToJson(EntityLayoutData layout, string filePath)
    {
        // Ensure file is ready for editing in Perforce
        PerforceService.EnsureFileReadyForEdit(filePath);
        
        var json = JsonSerializer.Serialize(layout, SerializeOptions);
        File.WriteAllText(filePath, json);
        
        // Add file to Perforce if it's new
        PerforceService.AddFile(filePath);
    }

    /// <summary>
    /// Loads an entity layout from a JSON file.
    /// </summary>
    public static EntityLayoutData? LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<EntityLayoutData>(json, DeserializeOptions);
        }
        catch
        {
            return null;
        }
    }
}




