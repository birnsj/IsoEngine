using GameCore.Combat;
using Microsoft.Xna.Framework;

namespace GameCore.Entities;

/// <summary>
/// Represents the player entity.
/// </summary>
public class Player : Entity
{
    /// <summary>
    /// Gets or sets the player's facing direction as a normalized vector.
    /// </summary>
    public Vector2 FacingDirection { get; set; }

    /// <summary>
    /// Gets or sets the player's movement speed in pixels per second.
    /// </summary>
    public float MovementSpeed { get; set; }

    /// <summary>
    /// Gets the player's inventory component.
    /// </summary>
    public InventoryComponent Inventory { get; }

    /// <summary>
    /// Gets the player's stats component.
    /// </summary>
    public StatsComponent Stats { get; }

    /// <summary>
    /// Gets the dictionary of dialog flags (boolean values) for tracking conversation state.
    /// </summary>
    public Dictionary<string, bool> DialogFlags { get; } = new();

    /// <summary>
    /// Gets or sets a dialog flag value.
    /// </summary>
    /// <param name="flagName">The name of the flag.</param>
    /// <param name="value">The value to set.</param>
    public void SetDialogFlag(string flagName, bool value)
    {
        DialogFlags[flagName] = value;
    }

    /// <summary>
    /// Gets a dialog flag value.
    /// </summary>
    /// <param name="flagName">The name of the flag.</param>
    /// <returns>The flag value, or false if not set.</returns>
    public bool GetDialogFlag(string flagName)
    {
        // Return the actual flag value, or false if not set
        return DialogFlags.TryGetValue(flagName, out var value) ? value : false;
    }

    /// <summary>
    /// Initializes a new instance of the Player class.
    /// </summary>
    /// <param name="position">Initial position.</param>
    public Player(Vector2 position) 
        : base(position, new Vector2(32, 32)) // Default player size: 32x32 pixels
    {
        FacingDirection = Vector2.UnitY; // Face down by default
        MovementSpeed = 150.0f; // 150 pixels per second
        IsSolid = true;
        Inventory = new InventoryComponent(24); // 24 inventory slots
        
        // Add stats component (increased health for better survivability)
        Stats = new StatsComponent(maxHealth: 250, attackPower: 15, defense: 5);
        AddComponent(Stats);
    }
}

