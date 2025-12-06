using Microsoft.Xna.Framework;

namespace GameCore.Services;

/// <summary>
/// Service that provides easy access to game time information.
/// Exposes delta time and total elapsed time.
/// </summary>
public class GameTimeManager : IGameService
{
    private GameTime? _currentGameTime;

    /// <summary>
    /// Gets the delta time (time elapsed since last frame) in seconds.
    /// </summary>
    public float DeltaTime => (float)(_currentGameTime?.ElapsedGameTime.TotalSeconds ?? 0.0);

    /// <summary>
    /// Gets the total elapsed game time in seconds.
    /// </summary>
    public double TotalTime => _currentGameTime?.TotalGameTime.TotalSeconds ?? 0.0;

    /// <summary>
    /// Gets the raw GameTime object from the current frame.
    /// </summary>
    public GameTime? CurrentGameTime => _currentGameTime;

    public void Initialize()
    {
        // Time manager doesn't need special initialization
    }

    public void Update(GameTime gameTime)
    {
        _currentGameTime = gameTime;
    }

    public void Draw(GameTime gameTime)
    {
        // Time manager doesn't need rendering
    }
}

