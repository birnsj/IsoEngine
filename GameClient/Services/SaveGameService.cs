using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameClient.Entities;
using GameClient.Rendering;
using GameClient.Services;
using GameCore.Entities;
using GameCore.Items;
using GameCore.Save;
using GameCore.Services;
using Microsoft.Xna.Framework;

namespace GameClient.Services;

/// <summary>
/// Service responsible for saving and loading game state.
/// </summary>
public class SaveGameService : IGameService
{
    private const string SavesDirectory = "GameContent/saves";

    private ILogger? _logger;
    private Player? _player;
    private InteractionService? _interactionService;
    private EntityRenderer? _entityRenderer;
    private Dictionary<string, Item>? _itemDatabase;

    public void Initialize()
    {
        _logger = ServiceLocator.Get<ILogger>();
        _player = ServiceLocator.Get<Player>();
        _interactionService = ServiceLocator.Get<InteractionService>();
        _entityRenderer = ServiceLocator.Get<EntityRenderer>();
        _itemDatabase = ServiceLocator.Get<Dictionary<string, Item>>();

        try
        {
            if (!Directory.Exists(SavesDirectory))
            {
                Directory.CreateDirectory(SavesDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"Failed to create saves directory '{SavesDirectory}': {ex.Message}");
        }
    }

    public void Update(GameTime gameTime)
    {
        // Save system doesn't need per-frame updates
    }

    public void Draw(GameTime gameTime)
    {
        // Save system doesn't render anything
    }

    /// <summary>
    /// Saves the current game state to the specified slot.
    /// </summary>
    public void SaveGame(SaveSlot slot)
    {
        // Try to resolve player lazily in case it wasn't available during Initialize
        _player ??= ServiceLocator.Get<Player>();

        if (_player == null)
        {
            _logger?.Warning("Cannot save game: Player not found.");
            return;
        }

        try
        {
            var save = BuildSaveGame();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(save, options);
            var path = GetSavePath(slot);

            File.WriteAllText(path, json);
            _logger?.Info($"Game saved to '{path}'");
        }
        catch (Exception ex)
        {
            _logger?.Error($"Error saving game: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the game state from the specified slot and applies it to the current world.
    /// </summary>
    /// <returns>True if load succeeded, false otherwise.</returns>
    public bool LoadGame(SaveSlot slot)
    {
        // Try to resolve player lazily in case it wasn't available during Initialize
        _player ??= ServiceLocator.Get<Player>();

        if (_player == null)
        {
            _logger?.Warning("Cannot load game: Player not found.");
            return false;
        }

        var path = GetSavePath(slot);
        if (!File.Exists(path))
        {
            _logger?.Warning($"Save file '{path}' does not exist.");
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            var save = JsonSerializer.Deserialize<SaveGame>(json);
            if (save == null)
            {
                _logger?.Error("Failed to deserialize save file (null result).");
                return false;
            }

            ApplySaveGame(save);
            _logger?.Info($"Game loaded from '{path}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"Error loading game from '{path}': {ex.Message}");
            return false;
        }
    }

    private string GetSavePath(SaveSlot slot)
    {
        var fileName = $"save{(int)slot}.json";
        return Path.Combine(SavesDirectory, fileName);
    }

    /// <summary>
    /// Builds a SaveGame snapshot from the current runtime state.
    /// </summary>
    private SaveGame BuildSaveGame()
    {
        if (_player == null)
            throw new InvalidOperationException("Player is not initialized.");

        var save = new SaveGame
        {
            PlayerX = _player.Position.X,
            PlayerY = _player.Position.Y,
            FacingX = _player.FacingDirection.X,
            FacingY = _player.FacingDirection.Y,
            Stats = new PlayerStatsSave
            {
                MaxHealth = _player.Stats.MaxHealth,
                CurrentHealth = _player.Stats.CurrentHealth,
                AttackPower = _player.Stats.AttackPower,
                Defense = _player.Stats.Defense
            }
        };

        // Copy dialog flags as simple world flags
        foreach (var kvp in _player.DialogFlags)
        {
            save.WorldFlags[kvp.Key] = kvp.Value;
        }

        // Inventory contents
        var inventory = _player.Inventory.Inventory;
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventory[i];
            if (!slot.IsEmpty && slot.Item != null && slot.Quantity > 0)
            {
                save.InventoryItems.Add(new InventoryItemSave
                {
                    ItemId = slot.Item.Id,
                    Quantity = slot.Quantity
                });
            }
        }

        // Active interactable names (to avoid respawning removed objects)
        if (_interactionService != null)
        {
            foreach (var interactable in _interactionService.GetAllInteractables())
            {
                if (interactable is InteractiveObject io && io.IsActive)
                {
                    save.ActiveInteractableNames.Add(io.Name);
                }
            }
        }

        // Save time of day and weather state
        var dayNightCycle = ServiceLocator.Get<DayNightCycleService>();
        if (dayNightCycle != null)
        {
            save.CurrentTimeOfDay = dayNightCycle.CurrentTimeOfDay;
        }

        var weatherSystem = ServiceLocator.Get<WeatherSystem>();
        if (weatherSystem != null)
        {
            save.CurrentWeather = (int)weatherSystem.CurrentWeather;
        }

        return save;
    }

    /// <summary>
    /// Applies a SaveGame snapshot to the current runtime state.
    /// </summary>
    private void ApplySaveGame(SaveGame save)
    {
        if (_player == null)
            return;

        // Restore player position and facing
        _player.Position = new Vector2(save.PlayerX, save.PlayerY);

        var facing = new Vector2(save.FacingX, save.FacingY);
        if (facing.LengthSquared() < 0.01f)
        {
            facing = Vector2.UnitY;
        }
        else
        {
            facing.Normalize();
        }
        _player.FacingDirection = facing;

        // Restore stats
        if (save.Stats != null)
        {
            _player.Stats.MaxHealth = save.Stats.MaxHealth;
            _player.Stats.CurrentHealth = Math.Clamp(save.Stats.CurrentHealth, 0, save.Stats.MaxHealth);
            _player.Stats.AttackPower = save.Stats.AttackPower;
            _player.Stats.Defense = save.Stats.Defense;
        }

        // Restore world flags (dialog flags)
        _player.DialogFlags.Clear();
        foreach (var kvp in save.WorldFlags)
        {
            _player.DialogFlags[kvp.Key] = kvp.Value;
        }

        // Restore time of day and weather state
        var dayNightCycle = ServiceLocator.Get<DayNightCycleService>();
        if (dayNightCycle != null)
        {
            dayNightCycle.CurrentTimeOfDay = save.CurrentTimeOfDay;
        }

        var weatherSystem = ServiceLocator.Get<WeatherSystem>();
        if (weatherSystem != null)
        {
            weatherSystem.CurrentWeather = (WeatherType)save.CurrentWeather;
        }

        // Restore inventory
        if (_itemDatabase != null)
        {
            var inventory = _player.Inventory.Inventory;

            // Clear existing inventory
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                inventory[i].Clear();
            }

            // Re-add items from save
            foreach (var itemSave in save.InventoryItems)
            {
                if (_itemDatabase.TryGetValue(itemSave.ItemId, out var item))
                {
                    inventory.AddItem(item, itemSave.Quantity);
                }
                else
                {
                    _logger?.Warning($"Item with ID '{itemSave.ItemId}' not found in item database when loading save.");
                }
            }
        }
        else
        {
            _logger?.Warning("Item database not available when loading save; inventory not restored.");
        }

        // Remove interactables that should not be present
        if (_interactionService != null && save.ActiveInteractableNames.Count > 0)
        {
            var activeNames = new HashSet<string>(save.ActiveInteractableNames);
            var toRemove = new List<InteractiveObject>();

            foreach (var interactable in _interactionService.GetAllInteractables())
            {
                if (interactable is InteractiveObject io)
                {
                    if (!activeNames.Contains(io.Name))
                    {
                        toRemove.Add(io);
                    }
                }
            }

            foreach (var obj in toRemove)
            {
                _interactionService.UnregisterInteractable(obj);
                if (_entityRenderer != null && obj is Entity entity)
                {
                    _entityRenderer.RemoveEntity(entity);
                }
            }
        }
    }
}


