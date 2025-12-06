using GameCore.Entities;
using Microsoft.Xna.Framework;

namespace GameCore.Services;

/// <summary>
/// Simple spatial grid for efficient spatial queries.
/// Divides the world into a grid of cells for O(1) lookups instead of O(n) linear searches.
/// </summary>
internal class SpatialGrid<T> where T : Entity
{
    private readonly Dictionary<int, List<T>> _cells = new();
    private readonly float _cellSize;
    private readonly int _gridWidth;
    private readonly int _gridHeight;

    /// <summary>
    /// Initializes a new spatial grid.
    /// </summary>
    /// <param name="cellSize">Size of each grid cell in world units.</param>
    /// <param name="gridWidth">Width of the grid in cells.</param>
    /// <param name="gridHeight">Height of the grid in cells.</param>
    public SpatialGrid(float cellSize, int gridWidth, int gridHeight)
    {
        if (cellSize <= 0)
            throw new ArgumentException("Cell size must be greater than zero", nameof(cellSize));
        if (gridWidth <= 0 || gridHeight <= 0)
            throw new ArgumentException("Grid dimensions must be greater than zero", nameof(gridWidth));

        _cellSize = cellSize;
        _gridWidth = gridWidth;
        _gridHeight = gridHeight;
    }

    /// <summary>
    /// Adds an entity to the spatial grid.
    /// </summary>
    public void Add(T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var cellKey = GetCellKey(entity.Position);
        if (!_cells.TryGetValue(cellKey, out var cell))
        {
            cell = new List<T>();
            _cells[cellKey] = cell;
        }

        if (!cell.Contains(entity))
        {
            cell.Add(entity);
        }
    }

    /// <summary>
    /// Removes an entity from the spatial grid.
    /// </summary>
    public void Remove(T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var cellKey = GetCellKey(entity.Position);
        if (_cells.TryGetValue(cellKey, out var cell))
        {
            cell.Remove(entity);
            if (cell.Count == 0)
            {
                _cells.Remove(cellKey);
            }
        }
    }

    /// <summary>
    /// Gets all entities within a specified range of a position.
    /// </summary>
    public IEnumerable<T> GetEntitiesInRange(Vector2 position, float range)
    {
        if (range < 0)
            throw new ArgumentException("Range cannot be negative", nameof(range));

        var rangeSquared = range * range;
        var minCellX = Math.Max(0, (int)Math.Floor((position.X - range) / _cellSize));
        var maxCellX = Math.Min(_gridWidth - 1, (int)Math.Floor((position.X + range) / _cellSize));
        var minCellY = Math.Max(0, (int)Math.Floor((position.Y - range) / _cellSize));
        var maxCellY = Math.Min(_gridHeight - 1, (int)Math.Floor((position.Y + range) / _cellSize));

        var foundEntities = new HashSet<T>();

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                var cellKey = GetCellKey(cellX, cellY);
                if (_cells.TryGetValue(cellKey, out var cell))
                {
                    foreach (var entity in cell)
                    {
                        if (!foundEntities.Contains(entity))
                        {
                            var distanceSquared = (entity.Position - position).LengthSquared();
                            if (distanceSquared <= rangeSquared)
                            {
                                foundEntities.Add(entity);
                                yield return entity;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Clears all entities from the grid.
    /// </summary>
    public void Clear()
    {
        _cells.Clear();
    }

    private int GetCellKey(Vector2 position)
    {
        var cellX = Math.Clamp((int)Math.Floor(position.X / _cellSize), 0, _gridWidth - 1);
        var cellY = Math.Clamp((int)Math.Floor(position.Y / _cellSize), 0, _gridHeight - 1);
        return GetCellKey(cellX, cellY);
    }

    private int GetCellKey(int cellX, int cellY)
    {
        return cellY * _gridWidth + cellX;
    }
}








