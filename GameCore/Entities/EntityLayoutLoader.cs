using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GameCore.Entities;

/// <summary>
/// Helper class for loading entity layouts from JSON files.
/// </summary>
public static class EntityLayoutLoader
{
    /// <summary>
    /// Serializable representation of entity placement data.
    /// </summary>
    private class EntityLayoutDataJson
    {
        public List<EntityDataJson> Entities { get; set; } = new();
    }

    /// <summary>
    /// Serializable representation of a single entity.
    /// </summary>
    private class EntityDataJson
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PositionDataJson Position { get; set; } = new();
        public string? DialogId { get; set; }
        public string? ItemId { get; set; }
        public int? Quantity { get; set; }
        public EnemyStatsDataJson? EnemyStats { get; set; }
        public PlacementBoundsDataJson? PlacementBounds { get; set; }
    }

    /// <summary>
    /// Serializable representation of a 3D position.
    /// </summary>
    private class PositionDataJson
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    /// <summary>
    /// Serializable representation of enemy statistics.
    /// </summary>
    private class EnemyStatsDataJson
    {
        public int MaxHealth { get; set; }
        public int AttackPower { get; set; }
        public int Defense { get; set; }
    }

    /// <summary>
    /// Serializable representation of placement bounds.
    /// </summary>
    private class PlacementBoundsDataJson
    {
        public float Width { get; set; } = 1f;
        public float Height { get; set; } = 1f;
        public float ZHeight { get; set; } = 1f;
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
    }

    /// <summary>
    /// Represents a loaded entity definition.
    /// </summary>
    public class LoadedEntityData
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Microsoft.Xna.Framework.Vector2 Position { get; set; }
        public float Z { get; set; }
        public string? DialogId { get; set; }
        public string? ItemId { get; set; }
        public int? Quantity { get; set; }
        public EnemyStats? Stats { get; set; }
        public PlacementBounds? Bounds { get; set; }
    }

    /// <summary>
    /// Enemy statistics for loaded entities.
    /// </summary>
    public class EnemyStats
    {
        public int MaxHealth { get; set; }
        public int AttackPower { get; set; }
        public int Defense { get; set; }
    }

    /// <summary>
    /// Placement bounds for loaded entities.
    /// </summary>
    public class PlacementBounds
    {
        public float Width { get; set; } = 1f;
        public float Height { get; set; } = 1f;
        public float ZHeight { get; set; } = 1f;
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
    }

    /// <summary>
    /// Loads an entity layout from a JSON file.
    /// </summary>
    public static List<LoadedEntityData> LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
            return new List<LoadedEntityData>();

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<EntityLayoutDataJson>(json);
            if (data == null)
                return new List<LoadedEntityData>();

            var result = new List<LoadedEntityData>();
            foreach (var entityJson in data.Entities)
            {
                var entity = new LoadedEntityData
                {
                    Type = entityJson.Type,
                    Name = entityJson.Name,
                    Description = entityJson.Description,
                    Position = new Microsoft.Xna.Framework.Vector2(entityJson.Position.X, entityJson.Position.Y),
                    Z = entityJson.Position.Z,
                    DialogId = entityJson.DialogId,
                    ItemId = entityJson.ItemId,
                    Quantity = entityJson.Quantity
                };

                if (entityJson.EnemyStats != null)
                {
                    entity.Stats = new EnemyStats
                    {
                        MaxHealth = entityJson.EnemyStats.MaxHealth,
                        AttackPower = entityJson.EnemyStats.AttackPower,
                        Defense = entityJson.EnemyStats.Defense
                    };
                }

                if (entityJson.PlacementBounds != null)
                {
                    entity.Bounds = new PlacementBounds
                    {
                        Width = entityJson.PlacementBounds.Width,
                        Height = entityJson.PlacementBounds.Height,
                        ZHeight = entityJson.PlacementBounds.ZHeight,
                        OffsetX = entityJson.PlacementBounds.OffsetX,
                        OffsetY = entityJson.PlacementBounds.OffsetY
                    };
                }

                result.Add(entity);
            }

            return result;
        }
        catch
        {
            return new List<LoadedEntityData>();
        }
    }
}




