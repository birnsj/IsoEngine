using Microsoft.Xna.Framework;

namespace GameCore.Entities;

/// <summary>
/// Represents a world object entity (trees, rocks, furniture, etc.).
/// </summary>
public class WorldObject : Entity
{
    /// <summary>
    /// Gets or sets the type of object (e.g., "Tree", "Rock", "Chest").
    /// </summary>
    public string ObjectType { get; set; }

    /// <summary>
    /// Initializes a new instance of the WorldObject class.
    /// </summary>
    /// <param name="position">Initial position in world coordinates.</param>
    /// <param name="size">Entity size in pixels.</param>
    /// <param name="height">Height of the object (Z dimension).</param>
    /// <param name="depth">Depth of the object (3rd dimension size). Default is 1.0f.</param>
    public WorldObject(Vector2 position, Vector2 size, float height, float depth = 1.0f)
        : base(position, size)
    {
        Z = 0.0f; // Base of object at ground level
        ZHeight = depth;
        IsSolid = true; // Objects are solid by default, but can be overridden
        ObjectType = "default";
    }
}








