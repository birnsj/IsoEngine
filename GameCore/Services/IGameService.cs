using Microsoft.Xna.Framework;

namespace GameCore.Services;

/// <summary>
/// Interface for game services that participate in the game lifecycle.
/// </summary>
public interface IGameService
{
    /// <summary>
    /// Called once during game initialization, before LoadContent.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Called every frame during the update phase.
    /// </summary>
    /// <param name="gameTime">Snapshot of the game timing state.</param>
    void Update(GameTime gameTime);

    /// <summary>
    /// Called every frame during the draw phase.
    /// </summary>
    /// <param name="gameTime">Snapshot of the game timing state.</param>
    void Draw(GameTime gameTime);
}

