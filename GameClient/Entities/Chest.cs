using GameCore;
using GameCore.Entities;
using GameCore.Interactions;
using GameCore.Items;
using GameCore.Services;
using Microsoft.Xna.Framework;

namespace GameClient.Entities;

/// <summary>
/// A chest that can be opened with a key and contains items in an inventory.
/// </summary>
public class Chest : InteractiveObject
{
    private bool _isOpened = false;
    private readonly bool _requiresKey;
    
    /// <summary>
    /// Gets whether the chest has been opened.
    /// </summary>
    public bool IsOpened => _isOpened;
    
    /// <summary>
    /// Gets the chest's inventory (9 slots for 3x3 grid).
    /// </summary>
    public Inventory ChestInventory { get; }

    public Chest(Vector2 position, string name = "Chest", string description = "A wooden chest. It appears to be locked.", bool requiresKey = true)
        : base(position, new Vector2(GameConstants.Player.DefaultSize, GameConstants.Player.DefaultSize), name, description)
    {
        _requiresKey = requiresKey;
        // Create inventory with 9 slots (3x3 grid)
        ChestInventory = new Inventory(9);
        
        // If unlocked, mark as opened immediately
        if (!_requiresKey)
        {
            _isOpened = true;
            Description = "An unlocked wooden chest.";
        }
    }

    public override void OnInteract(Player player)
    {
        var logger = ServiceLocator.Get<ILogger>();
        
        // Check if chest is already opened
        if (_isOpened)
        {
            // If opened, open the inventory window
            OpenChestInventory(player);
            return;
        }
        
        // If chest doesn't require a key, open it immediately
        if (!_requiresKey)
        {
            _isOpened = true;
            Description = "An unlocked wooden chest.";
            OpenChestInventory(player);
            return;
        }
        
        // Check if player has a key in inventory
        var inventory = player.Inventory.Inventory;
        var keyCount = inventory.GetItemCount("key");
        
        if (keyCount > 0)
        {
            // Player has a key - open the chest
            logger?.Info("Player opened the chest with a key!");
            
            // Remove one key from inventory
            inventory.RemoveItem("key", 1);
            
            // Mark as opened
            _isOpened = true;
            
            // Update description
            Description = "An opened wooden chest.";
            
            // Open the chest inventory window
            OpenChestInventory(player);
        }
        else
        {
            // Player doesn't have a key
            logger?.Info("The chest is locked. You need a key to open it.");
        }
    }
    
    /// <summary>
    /// Opens the chest inventory window using the enemy inventory window system.
    /// </summary>
    private void OpenChestInventory(Player player)
    {
        // Get the enemy inventory window (we'll use it for chests too)
        var enemyInventoryWindow = ServiceLocator.Get<GameClient.UI.EnemyInventoryWindow>();
        if (enemyInventoryWindow != null)
        {
            enemyInventoryWindow.OpenForChest(this);
        }
    }
}

