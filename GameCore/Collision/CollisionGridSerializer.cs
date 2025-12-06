using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GameCore.Collision;

/// <summary>
/// Handles serialization and deserialization of collision grids.
/// </summary>
public static class CollisionGridSerializer
{
    /// <summary>
    /// Serializable representation of a collision grid for JSON.
    /// </summary>
    private class CollisionGridData
    {
        public float WorldWidth { get; set; }
        public float WorldHeight { get; set; }
        public float CellSize { get; set; }
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }
        public List<List<bool>> CollisionData { get; set; } = new();
    }

    /// <summary>
    /// Saves a collision grid to a JSON file.
    /// </summary>
    public static bool SaveToJson(CollisionGrid grid, string filePath)
    {
        if (grid == null)
            return false;

        try
        {
            var data = new CollisionGridData
            {
                WorldWidth = grid.WorldWidth,
                WorldHeight = grid.WorldHeight,
                CellSize = grid.CellSize,
                GridWidth = grid.GridWidth,
                GridHeight = grid.GridHeight
            };

            // Convert 2D array to list of lists for JSON serialization
            for (int y = 0; y < grid.GridHeight; y++)
            {
                var row = new List<bool>();
                for (int x = 0; x < grid.GridWidth; x++)
                {
                    row.Add(grid.IsCellSolid(x, y));
                }
                data.CollisionData.Add(row);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(data, options);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads a collision grid from a JSON file.
    /// </summary>
    public static CollisionGrid? LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<CollisionGridData>(json);
            
            if (data == null)
                return null;

            // Create grid with saved dimensions
            var grid = new CollisionGrid(data.WorldWidth, data.WorldHeight, data.CellSize);

            // Restore collision data
            for (int y = 0; y < Math.Min(data.CollisionData.Count, grid.GridHeight); y++)
            {
                var row = data.CollisionData[y];
                for (int x = 0; x < Math.Min(row.Count, grid.GridWidth); x++)
                {
                    grid.SetCell(x, y, row[x]);
                }
            }

            return grid;
        }
        catch
        {
            return null;
        }
    }
}








