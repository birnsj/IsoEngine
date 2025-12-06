using System.IO;
using System.Text.Json;
using GameEditor.Data;
using GameEditor.Utilities;

namespace GameEditor.Services;

/// <summary>
/// Service for serializing and deserializing weather cycle configuration.
/// </summary>
public static class WeatherCycleSerializer
{
    /// <summary>
    /// Gets the path to the weather cycle configuration file.
    /// </summary>
    private static string GetConfigPath()
    {
        var gameContentPath = PathHelper.GetGameContentPath();
        if (gameContentPath == null)
            return Path.Combine("GameContent", "weather_cycle.json");
        
        return Path.Combine(gameContentPath, "weather_cycle.json");
    }

    /// <summary>
    /// Loads weather cycle data from the configuration file.
    /// </summary>
    /// <returns>Loaded data or default if file doesn't exist.</returns>
    public static WeatherCycleData LoadWeatherCycleData()
    {
        var filePath = GetConfigPath();
        
        if (!File.Exists(filePath))
        {
            // Return default configuration
            return new WeatherCycleData();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<WeatherCycleData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return data ?? new WeatherCycleData();
        }
        catch
        {
            // Return default configuration on error
            return new WeatherCycleData();
        }
    }

    /// <summary>
    /// Saves weather cycle data to the configuration file.
    /// </summary>
    /// <param name="data">The data to save.</param>
    public static void SaveWeatherCycleData(WeatherCycleData data)
    {
        var filePath = GetConfigPath();
        
        try
        {
            // Ensure file is ready for editing in Perforce
            PerforceService.EnsureFileReadyForEdit(filePath);
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(filePath, json);
            
            // Add file to Perforce if it's new
            PerforceService.AddFile(filePath);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

