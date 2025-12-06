using System.Collections.Generic;

namespace GameCore.Save;

/// <summary>
/// Serializable representation of the game state for saving/loading.
/// </summary>
public class SaveGame
{
    /// <summary>
    /// Player position in world/screen coordinates.
    /// </summary>
    public float PlayerX { get; set; }
    public float PlayerY { get; set; }

    /// <summary>
    /// Player facing direction (normalized vector).
    /// </summary>
    public float FacingX { get; set; }
    public float FacingY { get; set; }

    /// <summary>
    /// Player stats.
    /// </summary>
    public PlayerStatsSave Stats { get; set; } = new();

    /// <summary>
    /// Inventory contents.
    /// </summary>
    public List<InventoryItemSave> InventoryItems { get; set; } = new();

    /// <summary>
    /// Simple world flags (for quests, removed objects, etc.).
    /// Uses string keys so gameplay code can define their own semantics.
    /// </summary>
    public Dictionary<string, bool> WorldFlags { get; set; } = new();

    /// <summary>
    /// Names of interactable objects that are currently present in the world.
    /// Used to avoid respawning removed objects when loading.
    /// </summary>
    public List<string> ActiveInteractableNames { get; set; } = new();

    /// <summary>
    /// Current time of day (0.0 to 1.0, where 0.0 = midnight, 0.5 = noon).
    /// </summary>
    public float CurrentTimeOfDay { get; set; } = 0.0f;

    /// <summary>
    /// Current weather type (0 = Clear, 1 = LightRain, 2 = HeavyRain, 3 = Snow, 4 = Fog).
    /// </summary>
    public int CurrentWeather { get; set; } = 0;
}

/// <summary>
/// Serializable snapshot of player stats.
/// </summary>
public class PlayerStatsSave
{
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public int AttackPower { get; set; }
    public int Defense { get; set; }
}

/// <summary>
/// Serializable representation of an inventory entry.
/// </summary>
public class InventoryItemSave
{
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}


