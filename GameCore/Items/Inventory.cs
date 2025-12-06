namespace GameCore.Items;

/// <summary>
/// Represents a player inventory with a fixed number of slots.
/// </summary>
public class Inventory
{
    private readonly ItemStack[] _slots;
    private readonly int _slotCount;

    /// <summary>
    /// Gets the number of inventory slots.
    /// </summary>
    public int SlotCount => _slotCount;

    /// <summary>
    /// Gets whether the inventory is completely empty.
    /// </summary>
    public bool IsEmpty()
    {
        for (int i = 0; i < _slotCount; i++)
        {
            if (!_slots[i].IsEmpty)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Gets an inventory slot by index.
    /// </summary>
    public ItemStack this[int index]
    {
        get
        {
            if (index < 0 || index >= _slotCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _slots[index];
        }
    }

    /// <summary>
    /// Initializes a new instance of the Inventory class.
    /// </summary>
    /// <param name="slotCount">The number of inventory slots (default: 24).</param>
    public Inventory(int slotCount = 24)
    {
        _slotCount = slotCount;
        _slots = new ItemStack[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            _slots[i] = new ItemStack();
        }
    }

    /// <summary>
    /// Tries to add an item to the inventory.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="quantity">The quantity to add.</param>
    /// <returns>The quantity that was actually added.</returns>
    public int AddItem(Item item, int quantity = 1)
    {
        if (quantity <= 0)
            return 0;

        var remaining = quantity;

        // First, try to add to existing stacks of the same item
        if (item.Stackable)
        {
            for (int i = 0; i < _slotCount && remaining > 0; i++)
            {
                var slotItem = _slots[i].Item;
                if (!_slots[i].IsEmpty && slotItem != null && slotItem.Id == item.Id)
                {
                    var added = _slots[i].TryAdd(item, remaining);
                    remaining -= added;
                }
            }
        }

        // Then, try to add to empty slots
        for (int i = 0; i < _slotCount && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
            {
                var toAdd = item.Stackable ? Math.Min(remaining, item.MaxStack) : 1;
                _slots[i].Item = item;
                _slots[i].Quantity = toAdd;
                remaining -= toAdd;
            }
            else if (item.Stackable && _slots[i].Item?.Id == item.Id)
            {
                // Try to add to this existing stack
                var added = _slots[i].TryAdd(item, remaining);
                remaining -= added;
            }
        }

        return quantity - remaining;
    }

    /// <summary>
    /// Removes an item from the inventory.
    /// </summary>
    /// <param name="itemId">The ID of the item to remove.</param>
    /// <param name="quantity">The quantity to remove.</param>
    /// <returns>The quantity that was actually removed.</returns>
    public int RemoveItem(string itemId, int quantity = 1)
    {
        if (quantity <= 0)
            return 0;

        var remaining = quantity;

        for (int i = 0; i < _slotCount && remaining > 0; i++)
        {
            var slotItem = _slots[i].Item;
            if (!_slots[i].IsEmpty && slotItem != null && slotItem.Id == itemId)
            {
                var removed = _slots[i].Remove(remaining);
                remaining -= removed;
            }
        }

        return quantity - remaining;
    }

    /// <summary>
    /// Gets the total quantity of an item in the inventory.
    /// </summary>
    /// <param name="itemId">The ID of the item.</param>
    /// <returns>The total quantity.</returns>
    public int GetItemCount(string itemId)
    {
        int count = 0;
        for (int i = 0; i < _slotCount; i++)
        {
            var slotItem = _slots[i].Item;
            if (!_slots[i].IsEmpty && slotItem != null && slotItem.Id == itemId)
            {
                count += _slots[i].Quantity;
            }
        }
        return count;
    }

    /// <summary>
    /// Checks if the inventory has space for an item.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <param name="quantity">The quantity to check.</param>
    /// <returns>True if there's space, false otherwise.</returns>
    public bool HasSpace(Item item, int quantity = 1)
    {
        var remaining = quantity;

        // Check existing stacks
        if (item.Stackable)
        {
            for (int i = 0; i < _slotCount && remaining > 0; i++)
            {
                var slotItem = _slots[i].Item;
                if (!_slots[i].IsEmpty && slotItem != null && slotItem.Id == item.Id)
                {
                    remaining -= Math.Min(remaining, _slots[i].RemainingSpace);
                }
            }
        }

        // Check empty slots
        for (int i = 0; i < _slotCount && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
            {
                remaining -= item.Stackable ? Math.Min(remaining, item.MaxStack) : 1;
            }
        }

        return remaining <= 0;
    }

    /// <summary>
    /// Swaps the items in two inventory slots.
    /// </summary>
    /// <param name="slotIndex1">Index of the first slot.</param>
    /// <param name="slotIndex2">Index of the second slot.</param>
    public void SwapSlots(int slotIndex1, int slotIndex2)
    {
        if (slotIndex1 < 0 || slotIndex1 >= _slotCount || slotIndex2 < 0 || slotIndex2 >= _slotCount)
            return;

        if (slotIndex1 == slotIndex2)
            return;

        // Swap the item stacks
        var tempItem = _slots[slotIndex1].Item;
        var tempQuantity = _slots[slotIndex1].Quantity;

        _slots[slotIndex1].Item = _slots[slotIndex2].Item;
        _slots[slotIndex1].Quantity = _slots[slotIndex2].Quantity;

        _slots[slotIndex2].Item = tempItem;
        _slots[slotIndex2].Quantity = tempQuantity;
    }
}

