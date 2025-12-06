using System.IO;
using System.Text.Json;

namespace GameCore.Configuration;

/// <summary>
/// Configuration data for the day/night cycle and weather system.
/// </summary>
public class WeatherCycleConfig
{
    /// <summary>
    /// Day/night cycle configuration.
    /// </summary>
    public DayNightConfig DayNight { get; set; } = new();

    /// <summary>
    /// Weather system configuration.
    /// </summary>
    public WeatherConfig Weather { get; set; } = new();

    /// <summary>
    /// Day/night cycle settings.
    /// </summary>
    public class DayNightConfig
    {
        /// <summary>
        /// Day duration in seconds (full cycle from midnight to midnight).
        /// </summary>
        public float DayDuration { get; set; } = GameConstants.DayNight.DefaultDayDuration;

        /// <summary>
        /// Dawn start time (0.0 to 1.0).
        /// </summary>
        public float DawnStart { get; set; } = GameConstants.DayNight.DawnStart;

        /// <summary>
        /// Dawn end time (0.0 to 1.0).
        /// </summary>
        public float DawnEnd { get; set; } = GameConstants.DayNight.DawnEnd;

        /// <summary>
        /// Dusk start time (0.0 to 1.0).
        /// </summary>
        public float DuskStart { get; set; } = GameConstants.DayNight.DuskStart;

        /// <summary>
        /// Dusk end time (0.0 to 1.0).
        /// </summary>
        public float DuskEnd { get; set; } = GameConstants.DayNight.DuskEnd;
    }

    /// <summary>
    /// Weather system settings.
    /// </summary>
    public class WeatherConfig
    {
        /// <summary>
        /// Weather probability weights (must sum to 100).
        /// </summary>
        public WeatherProbabilities Probabilities { get; set; } = new();

        /// <summary>
        /// Minimum weather change interval in seconds.
        /// </summary>
        public float MinChangeInterval { get; set; } = GameConstants.Weather.MinWeatherChangeInterval;

        /// <summary>
        /// Maximum weather change interval in seconds.
        /// </summary>
        public float MaxChangeInterval { get; set; } = GameConstants.Weather.MaxWeatherChangeInterval;
    }

    /// <summary>
    /// Weather probability weights.
    /// </summary>
    public class WeatherProbabilities
    {
        /// <summary>
        /// Probability weight for clear weather (0-100).
        /// </summary>
        public float Clear { get; set; } = GameConstants.Weather.ProbabilityClear;

        /// <summary>
        /// Probability weight for light rain (0-100).
        /// </summary>
        public float LightRain { get; set; } = GameConstants.Weather.ProbabilityLightRain;

        /// <summary>
        /// Probability weight for heavy rain (0-100).
        /// </summary>
        public float HeavyRain { get; set; } = GameConstants.Weather.ProbabilityHeavyRain;

        /// <summary>
        /// Probability weight for snow (0-100).
        /// </summary>
        public float Snow { get; set; } = GameConstants.Weather.ProbabilitySnow;

        /// <summary>
        /// Probability weight for fog (0-100).
        /// </summary>
        public float Fog { get; set; } = GameConstants.Weather.ProbabilityFog;
    }

    /// <summary>
    /// Loads configuration from a JSON file, or returns default configuration if file doesn't exist.
    /// </summary>
    /// <param name="filePath">Path to the configuration JSON file.</param>
    /// <returns>Loaded configuration or default if file doesn't exist.</returns>
    public static WeatherCycleConfig LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            // Return default configuration
            return new WeatherCycleConfig();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<WeatherCycleConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Return loaded config or default if deserialization failed
            return config ?? new WeatherCycleConfig();
        }
        catch
        {
            // Return default configuration on error
            return new WeatherCycleConfig();
        }
    }

    /// <summary>
    /// Saves configuration to a JSON file.
    /// </summary>
    /// <param name="filePath">Path to save the configuration JSON file.</param>
    public void SaveToFile(string filePath)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

