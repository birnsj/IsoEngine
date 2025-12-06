using GameCore.Entities;

namespace GameCore.Interactions;

/// <summary>
/// Interface for objects that can be interacted with by the player.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Gets the name of the interactable object.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of the interactable object.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Called when the player interacts with this object.
    /// </summary>
    /// <param name="player">The player that is interacting.</param>
    void OnInteract(Player player);

    /// <summary>
    /// Gets the interaction radius in pixels. Player must be within this distance to interact.
    /// </summary>
    float InteractionRadius { get; }
}

