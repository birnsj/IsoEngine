using Microsoft.Xna.Framework;

namespace GameCore.Input;

/// <summary>
/// Interface for input services that handle player input.
/// </summary>
public interface IInputService : Services.IGameService
{
    /// <summary>
    /// Gets the movement vector from directional input (WASD/Arrow keys).
    /// Returns a normalized vector indicating movement direction.
    /// </summary>
    Vector2 GetMovementVector();

    /// <summary>
    /// Checks if a game action is currently being held down.
    /// </summary>
    /// <param name="action">The game action to check.</param>
    /// <returns>True if the action is currently held down, false otherwise.</returns>
    bool IsActionDown(GameAction action);

    /// <summary>
    /// Checks if a game action was just pressed this frame (transition from not pressed to pressed).
    /// </summary>
    /// <param name="action">The game action to check.</param>
    /// <returns>True if the action was just pressed this frame, false otherwise.</returns>
    bool IsActionPressed(GameAction action);

    /// <summary>
    /// Gets the current mouse position in screen coordinates.
    /// </summary>
    Point MousePosition { get; }

    /// <summary>
    /// Checks if the left mouse button was just pressed this frame.
    /// </summary>
    bool IsLeftClickPressed { get; }

    /// <summary>
    /// Checks if the left mouse button is currently held down.
    /// </summary>
    bool IsLeftMouseButtonDown { get; }

    /// <summary>
    /// Checks if the right mouse button was just pressed this frame.
    /// </summary>
    bool IsRightClickPressed { get; }

    /// <summary>
    /// Checks if the middle mouse button is currently held down.
    /// </summary>
    bool IsMiddleMouseButtonDown { get; }

    /// <summary>
    /// Gets the mouse wheel scroll delta for this frame.
    /// Positive values indicate scrolling up/away, negative values indicate scrolling down/toward.
    /// </summary>
    int MouseWheelDelta { get; }
}

