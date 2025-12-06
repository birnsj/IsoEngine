using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameCore.Rendering;

namespace GameClient.Utilities;

/// <summary>
/// Serializable representation of a tile map for JSON.
/// </summary>
internal class TileMapDataJson
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int TileWidth { get; set; }
    public int TileHeight { get; set; }
    public List<List<int>> Tiles { get; set; } = new();
    public List<int> SolidTiles { get; set; } = new();
    public PlayerStartPositionJson? PlayerStartPosition { get; set; }
}

internal class PlayerStartPositionJson
{
    public float X { get; set; }
    public float Y { get; set; }
}

/// <summary>
/// Serializable representation of entity placement data.
/// </summary>
internal class EntityLayoutDataJson
{
    public List<EntityDataJson> Entities { get; set; } = new();
}

/// <summary>
/// Serializable representation of a single entity.
/// </summary>
internal class EntityDataJson
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PositionDataJson Position { get; set; } = new();
    public string? DialogId { get; set; }
    public string? ItemId { get; set; }
    public int? Quantity { get; set; }
    public EnemyStatsDataJson? EnemyStats { get; set; }
}

/// <summary>
/// Serializable representation of a 2D position.
/// </summary>
internal class PositionDataJson
{
    public float X { get; set; }
    public float Y { get; set; }
}

/// <summary>
/// Serializable representation of enemy statistics.
/// </summary>
internal class EnemyStatsDataJson
{
    public int MaxHealth { get; set; }
    public int AttackPower { get; set; }
    public int Defense { get; set; }
}

/// <summary>
/// Utility class for exporting game data to editable files.
/// </summary>
public static class GameDataExporter
{
    /// <summary>
    /// Exports the current tile map to world.json.
    /// </summary>
    public static void ExportTileMap(IsometricTileMap tileMap, List<int> solidTiles, Microsoft.Xna.Framework.Vector2? playerStartPosition = null)
    {
        try
        {
            var mapPath = GameContentPathHelper.GetWorldMapPath();
            if (mapPath == null)
            {
                Console.WriteLine("Error exporting tile map: Could not resolve path to world.json");
                return;
            }
            
            var data = new TileMapDataJson
            {
                Width = tileMap.Width,
                Height = tileMap.Height,
                TileWidth = tileMap.TileWidth,
                TileHeight = tileMap.TileHeight,
                SolidTiles = new List<int>(solidTiles)
            };

            // Set player start position if provided
            if (playerStartPosition.HasValue)
            {
                data.PlayerStartPosition = new PlayerStartPositionJson
                {
                    X = playerStartPosition.Value.X,
                    Y = playerStartPosition.Value.Y
                };
            }

            // Convert tile map data to list of lists
            for (int y = 0; y < tileMap.Height; y++)
            {
                var row = new List<int>();
                for (int x = 0; x < tileMap.Width; x++)
                {
                    row.Add(tileMap.GetTile(x, y));
                }
                data.Tiles.Add(row);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(mapPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting tile map: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports all entities/interactables and enemies to entities.json.
    /// </summary>
    public static void ExportEntities(List<GameCore.Interactions.IInteractable> interactables, 
        List<GameClient.Entities.Enemy>? enemies, IsometricTileMap tileMap)
    {
        try
        {
            var entityPath = GameContentPathHelper.GetWorldEntitiesPath();
            if (entityPath == null)
            {
                Console.WriteLine("Error exporting entities: Could not resolve path to world_entities.json");
                return;
            }
            var entityLayout = new EntityLayoutDataJson();

            foreach (var interactable in interactables)
            {
                if (interactable is GameCore.Entities.InteractiveObject io)
                {
                    // Convert screen position back to world position using fixed tile size
                    var worldPos = tileMap.ScreenToWorldFixed(io.Position);
                    
                    var entityData = new EntityDataJson
                    {
                        Type = GetEntityType(io),
                        Name = io.Name,
                        Description = io.Description,
                        Position = new PositionDataJson
                        {
                            X = worldPos.X,
                            Y = worldPos.Y
                        }
                    };

                    // Set specific properties based on entity type
                    if (io is GameClient.Entities.DialogNPC)
                    {
                        // Try to infer dialog ID from name or use default
                        // If name contains "Guard", use guard_dialog
                        if (io.Name.Contains("Guard", StringComparison.OrdinalIgnoreCase))
                        {
                            entityData.DialogId = "guard_dialog";
                        }
                    }
                    else if (io is GameClient.Entities.GroundItem)
                    {
                        // Try to infer item ID from name
                        // This is a workaround since GroundItem doesn't expose the item directly
                        var itemName = io.Name.ToLower();
                        if (itemName.Contains("coin"))
                            entityData.ItemId = "gold_coin";
                        else if (itemName.Contains("potion"))
                            entityData.ItemId = "health_potion";
                        else if (itemName.Contains("sword"))
                            entityData.ItemId = "sword";
                        else if (itemName.Contains("shield"))
                            entityData.ItemId = "shield";
                        else if (itemName.Contains("bread"))
                            entityData.ItemId = "bread";
                        else if (itemName.Contains("key"))
                            entityData.ItemId = "key";
                        
                        entityData.Quantity = 1; // Default quantity
                    }
                    // Note: Enemies are handled separately, not as InteractiveObjects

                    entityLayout.Entities.Add(entityData);
                }
            }

            // Also export enemies if provided
            if (enemies != null)
            {
                foreach (var enemy in enemies)
                {
                    // Convert screen position back to world position using fixed tile size
                    var worldPos = tileMap.ScreenToWorldFixed(enemy.Position);
                    var entityData = new EntityDataJson
                    {
                        Type = "Enemy",
                        Name = "Enemy",
                        Description = "A hostile enemy",
                        Position = new PositionDataJson
                        {
                            X = worldPos.X,
                            Y = worldPos.Y
                        },
                        EnemyStats = new EnemyStatsDataJson
                        {
                            MaxHealth = enemy.Stats.MaxHealth,
                            AttackPower = enemy.Stats.AttackPower,
                            Defense = enemy.Stats.Defense
                        }
                    };
                    entityLayout.Entities.Add(entityData);
                }
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(entityLayout, options);
            File.WriteAllText(entityPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting entities: {ex.Message}");
        }
    }

    private static string GetEntityType(GameCore.Entities.InteractiveObject io)
    {
        return io switch
        {
            GameClient.Entities.DialogNPC => "NPC",
            GameClient.Entities.GroundItem => "GroundItem",
            _ => "Interactable"
        };
    }

    /// <summary>
    /// Exports all game data (map and entities) to editable files.
    /// </summary>
    public static void ExportAllGameData(IsometricTileMap tileMap, List<int> solidTiles, 
        List<GameCore.Interactions.IInteractable> interactables, List<GameClient.Entities.Enemy>? enemies = null)
    {
        ExportTileMap(tileMap, solidTiles);
        ExportEntities(interactables, enemies, tileMap);
    }
}

