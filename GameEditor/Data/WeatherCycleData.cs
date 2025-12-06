namespace GameEditor.Data;

/// <summary>
/// Data model for weather cycle editor settings.
/// </summary>
public class WeatherCycleData
{
    /// <summary>
    /// Day/night cycle settings.
    /// </summary>
    public DayNightData DayNight { get; set; } = new();

    /// <summary>
    /// Weather system settings.
    /// </summary>
    public WeatherData Weather { get; set; } = new();

    /// <summary>
    /// Day/night cycle configuration.
    /// </summary>
    public class DayNightData
    {
        /// <summary>
        /// Day duration in seconds (full cycle from midnight to midnight).
        /// </summary>
        public float DayDuration { get; set; } = GameCore.GameConstants.DayNight.DefaultDayDuration;

        /// <summary>
        /// Dawn start time (0.0 to 1.0).
        /// </summary>
        public float DawnStart { get; set; } = GameCore.GameConstants.DayNight.DawnStart;

        /// <summary>
        /// Dawn end time (0.0 to 1.0).
        /// </summary>
        public float DawnEnd { get; set; } = GameCore.GameConstants.DayNight.DawnEnd;

        /// <summary>
        /// Dusk start time (0.0 to 1.0).
        /// </summary>
        public float DuskStart { get; set; } = GameCore.GameConstants.DayNight.DuskStart;

        /// <summary>
        /// Dusk end time (0.0 to 1.0).
        /// </summary>
        public float DuskEnd { get; set; } = GameCore.GameConstants.DayNight.DuskEnd;
    }

    /// <summary>
    /// Weather system configuration.
    /// </summary>
    public class WeatherData
    {
        /// <summary>
        /// Weather probability weights (must sum to 100).
        /// </summary>
        public WeatherProbabilities Probabilities { get; set; } = new();

        /// <summary>
        /// Minimum weather change interval in seconds.
        /// </summary>
        public float MinChangeInterval { get; set; } = GameCore.GameConstants.Weather.MinWeatherChangeInterval;

        /// <summary>
        /// Maximum weather change interval in seconds.
        /// </summary>
        public float MaxChangeInterval { get; set; } = GameCore.GameConstants.Weather.MaxWeatherChangeInterval;
    }

    /// <summary>
    /// Weather probability weights.
    /// </summary>
    public class WeatherProbabilities
    {
        /// <summary>
        /// Probability weight for clear weather (0-100).
        /// </summary>
        public float Clear { get; set; } = GameCore.GameConstants.Weather.ProbabilityClear;

        /// <summary>
        /// Probability weight for light rain (0-100).
        /// </summary>
        public float LightRain { get; set; } = GameCore.GameConstants.Weather.ProbabilityLightRain;

        /// <summary>
        /// Probability weight for heavy rain (0-100).
        /// </summary>
        public float HeavyRain { get; set; } = GameCore.GameConstants.Weather.ProbabilityHeavyRain;

        /// <summary>
        /// Probability weight for snow (0-100).
        /// </summary>
        public float Snow { get; set; } = GameCore.GameConstants.Weather.ProbabilitySnow;

        /// <summary>
        /// Probability weight for fog (0-100).
        /// </summary>
        public float Fog { get; set; } = GameCore.GameConstants.Weather.ProbabilityFog;
    }
}

