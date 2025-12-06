using GameCore;
using GameCore.Entities;
using GameCore.Interactions;
using GameCore.Services;
using Microsoft.Xna.Framework;

namespace GameClient.Entities;

/// <summary>
/// A test interactable object for demonstration.
/// </summary>
public class TestInteractable : InteractiveObject
{
    public TestInteractable(Vector2 position, string name, string description)
        : base(position, new Vector2(GameConstants.Player.DefaultSize, GameConstants.Player.DefaultSize), name, description)
    {
    }

    public override void OnInteract(Player player)
    {
        var logger = ServiceLocator.Get<ILogger>();
        
        // Special handling for chest - check if player has a key
        if (Name.Contains("Chest", System.StringComparison.OrdinalIgnoreCase))
        {
            // Check if player has a key in inventory
            // The key item ID is "key"
            var inventory = player.Inventory.Inventory;
            var keyCount = inventory.GetItemCount("key");
            
            if (keyCount > 0)
            {
                // Player has a key - open the chest
                logger?.Info("Player opened the chest with a key!");
                logger?.Info("The chest contains valuable treasure!");
                
                // Remove one key from inventory
                inventory.RemoveItem("key", 1);
                
                // Update description to show it's been opened
                Description = "An opened wooden chest. It's empty now.";
            }
            else
            {
                // Player doesn't have a key
                logger?.Info("The chest is locked. You need a key to open it.");
            }
        }
        else
        {
            logger?.Info($"Player interacted with: {Name}");
            logger?.Info($"Description: {Description}");
        }
    }
}

