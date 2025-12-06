using GameCore.Interactions;
using Microsoft.Xna.Framework;

namespace GameCore.Entities;

/// <summary>
/// An entity that can be interacted with by the player.
/// </summary>
public class InteractiveObject : Entity, IInteractable
{
    /// <summary>
    /// Gets or sets the name of this interactive object.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of this interactive object.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the interaction range in pixels.
    /// </summary>
    public float InteractionRange { get; set; } = 50.0f; // Reduced default for closer interaction

    /// <summary>
    /// Gets the interaction radius (same as InteractionRange for IInteractable interface).
    /// </summary>
    public float InteractionRadius => InteractionRange;

    /// <summary>
    /// Initializes a new instance of the InteractiveObject class.
    /// </summary>
    /// <param name="position">Initial position in world coordinates.</param>
    /// <param name="size">Entity size in pixels.</param>
    /// <param name="name">Name of the object.</param>
    /// <param name="description">Description of the object.</param>
    public InteractiveObject(Vector2 position, Vector2 size, string name, string description)
        : base(position, size)
    {
        Name = name;
        Description = description;
        IsSolid = false; // Interactive objects are typically not solid
    }

    /// <summary>
    /// Called when the player interacts with this object.
    /// </summary>
    /// <param name="player">The player that is interacting.</param>
    public virtual void OnInteract(Player player)
    {
        // Base implementation - can be overridden by derived classes
    }
}

