namespace GameCore.Configuration;

/// <summary>
/// Simple configuration class for game window settings.
/// </summary>
public static class Configuration
{
    /// <summary>
    /// Default window width in pixels.
    /// </summary>
    public const int WindowWidth = 1024;

    /// <summary>
    /// Default window height in pixels.
    /// </summary>
    public const int WindowHeight = 768;

    /// <summary>
    /// Game window title.
    /// </summary>
    public const string WindowTitle = "IsoEngine";

    /// <summary>
    /// Master volume (0.0 to 1.0) for the game.
    /// Note: currently not wired to an audio system, but can be used when sound is added.
    /// </summary>
    public static float MasterVolume { get; set; } = 1.0f;

    /// <summary>
    /// Whether the game should run in fullscreen mode.
    /// </summary>
    public static bool IsFullscreen { get; set; } = false;
}

