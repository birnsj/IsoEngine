using GameCore.Services;
using Microsoft.Xna.Framework;

namespace GameCore.State;

/// <summary>
/// Service that tracks the current high-level game state (title, game, pause).
/// </summary>
public class GameStateService : IGameService
{
    /// <summary>
    /// Gets or sets the current game state.
    /// </summary>
    public GameState CurrentState { get; private set; } = GameState.TitleScreen;

    /// <summary>
    /// Sets the current game state.
    /// </summary>
    public void SetState(GameState newState)
    {
        CurrentState = newState;
    }

    /// <summary>
    /// Returns true if the game world should be updating (in-game).
    /// </summary>
    public bool IsInGame => CurrentState == GameState.GameScreen;

    public void Initialize()
    {
        // No initialization needed
    }

    public void Update(GameTime gameTime)
    {
        // No per-frame logic needed; state is changed by other systems
    }

    public void Draw(GameTime gameTime)
    {
        // No rendering
    }
}


