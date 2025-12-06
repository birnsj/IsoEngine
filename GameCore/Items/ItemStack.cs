namespace GameCore.Items;

/// <summary>
/// Represents a stack of items in an inventory slot.
/// </summary>
public class ItemStack
{
    /// <summary>
    /// Gets or sets the item in this stack.
    /// </summary>
    public Item? Item { get; set; }

    /// <summary>
    /// Gets or sets the quantity of items in this stack.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets whether this stack is empty.
    /// </summary>
    public bool IsEmpty => Item == null || Quantity <= 0;

    /// <summary>
    /// Gets the remaining space in this stack.
    /// </summary>
    public int RemainingSpace => Item?.Stackable == true ? (Item.MaxStack - Quantity) : 0;

    /// <summary>
    /// Initializes a new instance of the ItemStack class.
    /// </summary>
    public ItemStack(Item? item = null, int quantity = 0)
    {
        Item = item;
        Quantity = quantity;
    }

    /// <summary>
    /// Tries to add items to this stack.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="quantity">The quantity to add.</param>
    /// <returns>The quantity that was actually added.</returns>
    public int TryAdd(Item item, int quantity)
    {
        if (IsEmpty)
        {
            Item = item;
            Quantity = Math.Min(quantity, item.Stackable ? item.MaxStack : 1);
            return Quantity;
        }

        if (Item == null || Item.Id != item.Id || !Item.Stackable)
            return 0;

        var canAdd = Math.Min(quantity, RemainingSpace);
        Quantity += canAdd;
        return canAdd;
    }

    /// <summary>
    /// Removes items from this stack.
    /// </summary>
    /// <param name="quantity">The quantity to remove.</param>
    /// <returns>The quantity that was actually removed.</returns>
    public int Remove(int quantity)
    {
        var toRemove = Math.Min(quantity, Quantity);
        Quantity -= toRemove;

        if (Quantity <= 0)
        {
            Item = null;
            Quantity = 0;
        }

        return toRemove;
    }

    /// <summary>
    /// Clears this stack.
    /// </summary>
    public void Clear()
    {
        Item = null;
        Quantity = 0;
    }
}

