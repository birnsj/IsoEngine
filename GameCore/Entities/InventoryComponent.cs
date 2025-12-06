using GameCore.Items;

namespace GameCore.Entities;

/// <summary>
/// Component that adds inventory functionality to an entity.
/// </summary>
public class InventoryComponent
{
    /// <summary>
    /// Gets the inventory instance.
    /// </summary>
    public Inventory Inventory { get; }

    /// <summary>
    /// Initializes a new instance of the InventoryComponent class.
    /// </summary>
    /// <param name="slotCount">The number of inventory slots (default: 24).</param>
    public InventoryComponent(int slotCount = 24)
    {
        Inventory = new Inventory(slotCount);
    }
}

