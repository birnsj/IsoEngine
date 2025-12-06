using GameCore;
using GameCore.Entities;
using GameCore.Interactions;
using GameCore.Items;
using GameCore.Services;
using Microsoft.Xna.Framework;

namespace GameClient.Entities;

/// <summary>
/// An interactive object that represents an item on the ground that can be picked up.
/// </summary>
public class GroundItem : InteractiveObject
{
    private readonly Item _item;
    private readonly int _quantity;

    /// <summary>
    /// Initializes a new instance of the GroundItem class.
    /// </summary>
    /// <param name="position">Position in world coordinates.</param>
    /// <param name="item">The item that can be picked up.</param>
    /// <param name="quantity">The quantity of the item.</param>
    public GroundItem(Vector2 position, Item item, int quantity = 1)
        : base(position, new Vector2(GameConstants.Player.DefaultSize, GameConstants.Player.DefaultSize), item.Name, $"Pick up {item.Name}")
    {
        _item = item;
        _quantity = quantity;
    }

    public override void OnInteract(Player player)
    {
        var logger = ServiceLocator.Get<ILogger>();
        
        // Try to add item to inventory
        var added = player.Inventory.Inventory.AddItem(_item, _quantity);
        
        if (added > 0)
        {
            logger?.Info($"Picked up {added}x {_item.Name}");
            
            // If all items were picked up, remove this object from the world
            if (added >= _quantity)
            {
                // Mark for removal
                var interactionService = ServiceLocator.Get<InteractionService>();
                interactionService?.UnregisterInteractable(this);
                
                // Remove from entity renderer
                var entityRenderer = ServiceLocator.Get<GameClient.Rendering.EntityRenderer>();
                entityRenderer?.RemoveEntity(this);
            }
            else
            {
                logger?.Warning($"Inventory full! Only picked up {added} of {_quantity} {_item.Name}");
            }
        }
        else
        {
            logger?.Warning($"Cannot pick up {_item.Name}: Inventory is full!");
        }
    }
}

