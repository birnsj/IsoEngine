namespace GameCore.Items;

/// <summary>
/// Represents an item that can be stored in inventory.
/// </summary>
public class Item
{
    /// <summary>
    /// Gets the unique identifier of the item.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets the display name of the item.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the description of the item.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether this item can be stacked (multiple quantities in one slot).
    /// </summary>
    public bool Stackable { get; set; }

    /// <summary>
    /// Gets or sets the maximum stack size for this item.
    /// </summary>
    public int MaxStack { get; set; }

    /// <summary>
    /// Gets or sets the path to the item's image/graphic file.
    /// Path is relative to GameContent directory or absolute path.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Initializes a new instance of the Item class.
    /// </summary>
    public Item(string id, string name, string description, bool stackable = true, int maxStack = 99, string? imagePath = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Stackable = stackable;
        MaxStack = maxStack;
        ImagePath = imagePath;
    }
}

