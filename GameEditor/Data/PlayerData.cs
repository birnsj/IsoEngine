namespace GameEditor.Data;

/// <summary>
/// Data class representing player configuration for the editor.
/// </summary>
public class PlayerData
{
    /// <summary>
    /// Player's display name.
    /// </summary>
    public string Name { get; set; } = "Player";
    
    /// <summary>
    /// Maximum health points.
    /// </summary>
    public int MaxHealth { get; set; } = 100;
    
    /// <summary>
    /// Attack power.
    /// </summary>
    public int AttackPower { get; set; } = 15;
    
    /// <summary>
    /// Defense value.
    /// </summary>
    public int Defense { get; set; } = 5;
    
    /// <summary>
    /// Movement speed.
    /// </summary>
    public float MovementSpeed { get; set; } = 150.0f;
    
    /// <summary>
    /// Path to the player sprite image (relative to GameContent).
    /// </summary>
    public string SpritePath { get; set; } = "sprites/player.png";
    
    /// <summary>
    /// Scale of the sprite when rendering.
    /// </summary>
    public float SpriteScale { get; set; } = 0.15f;
    
    /// <summary>
    /// Starting position X in world coordinates.
    /// </summary>
    public float StartPositionX { get; set; } = 10.0f;
    
    /// <summary>
    /// Starting position Y in world coordinates.
    /// </summary>
    public float StartPositionY { get; set; } = 10.0f;
}

