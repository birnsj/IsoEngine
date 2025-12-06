using Microsoft.Xna.Framework.Input;

namespace GameCore.Input;

/// <summary>
/// Configuration class for input keybindings.
/// Makes it easy to change key mappings later.
/// </summary>
public static class InputConfiguration
{
    /// <summary>
    /// Gets the keyboard key mapping for each game action.
    /// </summary>
    public static Dictionary<GameAction, Keys[]> ActionKeys { get; } = new()
    {
        { GameAction.MoveUp, new[] { Keys.W, Keys.Up } },
        { GameAction.MoveDown, new[] { Keys.S, Keys.Down } },
        { GameAction.MoveLeft, new[] { Keys.A, Keys.Left } },
        { GameAction.MoveRight, new[] { Keys.D, Keys.Right } },
        { GameAction.Interact, new[] { Keys.E } },
        { GameAction.Attack, new[] { Keys.LeftControl } },
        { GameAction.Jump, new[] { Keys.Space } },
        { GameAction.OpenInventory, new[] { Keys.I } }
    };
}

