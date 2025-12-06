using System.Collections.Generic;

namespace GameEditor.Data;

/// <summary>
/// Serializable representation of entity placement data.
/// </summary>
public class EntityLayoutData
{
    /// <summary>
    /// Gets or sets the list of entities in this layout.
    /// </summary>
    public List<EntityData> Entities { get; set; } = new();
}

/// <summary>
/// Serializable representation of a single entity.
/// </summary>
public class EntityData
{
    /// <summary>
    /// Gets or sets the type of entity (NPC, Enemy, GroundItem, Interactable).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the entity.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the entity.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the world position of the entity (in tile coordinates).
    /// </summary>
    public PositionData Position { get; set; } = new();

    /// <summary>
    /// Gets or sets the dialog ID for NPCs (if applicable).
    /// </summary>
    public string? DialogId { get; set; }

    /// <summary>
    /// Gets or sets the item ID for ground items (if applicable).
    /// </summary>
    public string? ItemId { get; set; }

    /// <summary>
    /// Gets or sets the quantity for ground items (if applicable).
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// Gets or sets enemy stats (if applicable).
    /// </summary>
    public EnemyStatsData? EnemyStats { get; set; }

    /// <summary>
    /// Gets or sets the placement bounds for collision during placement.
    /// If null, uses default bounds based on entity type.
    /// </summary>
    public PlacementBoundsData? PlacementBounds { get; set; }

    /// <summary>
    /// Gets or sets the sprite path for this entity (relative to GameContent).
    /// </summary>
    public string? SpritePath { get; set; }
}

/// <summary>
/// Serializable representation of a 3D position.
/// </summary>
public class PositionData
{
    /// <summary>
    /// Gets or sets the X coordinate.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Gets or sets the Z coordinate (height/elevation). Default is 0 (ground level).
    /// </summary>
    public float Z { get; set; }
}

/// <summary>
/// Serializable representation of placement bounds for collision during entity placement.
/// </summary>
public class PlacementBoundsData
{
    /// <summary>
    /// Gets or sets the width of the placement footprint in world units.
    /// </summary>
    public float Width { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the height (depth) of the placement footprint in world units.
    /// </summary>
    public float Height { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the vertical extent of the entity (how tall it is in Z).
    /// Used for 3D collision checking.
    /// </summary>
    public float ZHeight { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the X offset of bounds from entity center.
    /// </summary>
    public float OffsetX { get; set; }

    /// <summary>
    /// Gets or sets the Y offset of bounds from entity center.
    /// </summary>
    public float OffsetY { get; set; }
}

/// <summary>
/// Serializable representation of enemy statistics.
/// </summary>
public class EnemyStatsData
{
    /// <summary>
    /// Gets or sets the maximum health.
    /// </summary>
    public int MaxHealth { get; set; }

    /// <summary>
    /// Gets or sets the attack power.
    /// </summary>
    public int AttackPower { get; set; }

    /// <summary>
    /// Gets or sets the defense value.
    /// </summary>
    public int Defense { get; set; }
}




