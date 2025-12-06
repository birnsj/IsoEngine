using System.Text.Json;
using System.Text.Json.Serialization;
using GameEditor.Data;
using GameEditor.Utilities;

namespace GameEditor.Services;

/// <summary>
/// Service for saving and loading player configuration data.
/// </summary>
public static class PlayerSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Gets the default path to the player.json file.
    /// </summary>
    public static string? GetPlayerFilePath()
    {
        var gameContentPath = PathHelper.GetGameContentPath();
        if (gameContentPath == null)
            return null;
        
        return Path.Combine(gameContentPath, "player.json");
    }

    /// <summary>
    /// Loads player data from the default player.json file.
    /// </summary>
    public static PlayerData Load()
    {
        var filePath = GetPlayerFilePath();
        if (filePath == null || !File.Exists(filePath))
        {
            return new PlayerData(); // Return defaults
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<PlayerData>(json, JsonOptions);
            return data ?? new PlayerData();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading player data: {ex.Message}");
            return new PlayerData();
        }
    }

    /// <summary>
    /// Saves player data to the default player.json file.
    /// </summary>
    public static void Save(PlayerData data)
    {
        var filePath = GetPlayerFilePath();
        if (filePath == null)
        {
            throw new InvalidOperationException("Could not determine player.json path");
        }

        try
        {
            // Ensure file is ready for editing in Perforce
            PerforceService.EnsureFileReadyForEdit(filePath);
            
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(filePath, json);
            
            // Add file to Perforce if it's new
            PerforceService.AddFile(filePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error saving player data: {ex.Message}", ex);
        }
    }
}

