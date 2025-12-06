using Microsoft.Xna.Framework;

namespace GameCore.Collision;

/// <summary>
/// Represents a collision grid that operates independently of tile size.
/// Uses an adjustable grid resolution for flexible collision editing.
/// </summary>
public class CollisionGrid
{
    private readonly bool[,] _collisionData;
    private readonly float _cellSize;
    private readonly float _worldWidth;
    private readonly float _worldHeight;

    /// <summary>
    /// Gets the width of the grid in cells.
    /// </summary>
    public int GridWidth { get; }

    /// <summary>
    /// Gets the height of the grid in cells.
    /// </summary>
    public int GridHeight { get; }

    /// <summary>
    /// Gets the size of each grid cell in world units.
    /// </summary>
    public float CellSize => _cellSize;

    /// <summary>
    /// Gets the world width covered by this grid.
    /// </summary>
    public float WorldWidth => _worldWidth;

    /// <summary>
    /// Gets the world height covered by this grid.
    /// </summary>
    public float WorldHeight => _worldHeight;

    /// <summary>
    /// Initializes a new collision grid.
    /// </summary>
    /// <param name="worldWidth">Width of the world area to cover in world units.</param>
    /// <param name="worldHeight">Height of the world area to cover in world units.</param>
    /// <param name="cellSize">Size of each grid cell in world units. Smaller = finer resolution.</param>
    public CollisionGrid(float worldWidth, float worldHeight, float cellSize = 1.0f)
    {
        if (worldWidth <= 0 || worldHeight <= 0)
            throw new ArgumentException("World dimensions must be greater than zero");
        if (cellSize <= 0)
            throw new ArgumentException("Cell size must be greater than zero");

        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _cellSize = cellSize;

        GridWidth = (int)Math.Ceiling(worldWidth / cellSize);
        GridHeight = (int)Math.Ceiling(worldHeight / cellSize);

        _collisionData = new bool[GridHeight, GridWidth];
    }

    /// <summary>
    /// Sets whether a grid cell is solid (collidable).
    /// </summary>
    /// <param name="gridX">Grid X coordinate.</param>
    /// <param name="gridY">Grid Y coordinate.</param>
    /// <param name="isSolid">True if the cell should be solid.</param>
    public void SetCell(int gridX, int gridY, bool isSolid)
    {
        if (gridX < 0 || gridX >= GridWidth || gridY < 0 || gridY >= GridHeight)
            return;

        _collisionData[gridY, gridX] = isSolid;
    }

    /// <summary>
    /// Gets whether a grid cell is solid (collidable).
    /// </summary>
    /// <param name="gridX">Grid X coordinate.</param>
    /// <param name="gridY">Grid Y coordinate.</param>
    /// <returns>True if the cell is solid, false otherwise.</returns>
    public bool IsCellSolid(int gridX, int gridY)
    {
        if (gridX < 0 || gridX >= GridWidth || gridY < 0 || gridY >= GridHeight)
            return false; // Out of bounds is not solid

        return _collisionData[gridY, gridX];
    }

    /// <summary>
    /// Sets whether a world position is solid (collidable).
    /// </summary>
    /// <param name="worldPos">World position in world coordinates.</param>
    /// <param name="isSolid">True if the position should be solid.</param>
    public void SetWorldPosition(Vector2 worldPos, bool isSolid)
    {
        var gridX = WorldToGridX(worldPos.X);
        var gridY = WorldToGridY(worldPos.Y);
        SetCell(gridX, gridY, isSolid);
    }

    /// <summary>
    /// Gets whether a world position is solid (collidable).
    /// </summary>
    /// <param name="worldPos">World position in world coordinates.</param>
    /// <returns>True if the position is solid, false otherwise.</returns>
    public bool IsWorldPositionSolid(Vector2 worldPos)
    {
        var gridX = WorldToGridX(worldPos.X);
        var gridY = WorldToGridY(worldPos.Y);
        return IsCellSolid(gridX, gridY);
    }

    /// <summary>
    /// Checks if a rectangle (in world coordinates) overlaps with any solid cells.
    /// </summary>
    /// <param name="worldBounds">Rectangle in world coordinates.</param>
    /// <returns>True if the rectangle overlaps with any solid cells.</returns>
    public bool CheckCollision(RectangleF worldBounds)
    {
        // Convert rectangle bounds to grid coordinates
        var minGridX = WorldToGridX(worldBounds.Left);
        var maxGridX = WorldToGridX(worldBounds.Right);
        var minGridY = WorldToGridY(worldBounds.Top);
        var maxGridY = WorldToGridY(worldBounds.Bottom);

        // Check all cells that the rectangle could overlap
        for (int gridY = minGridY; gridY <= maxGridY; gridY++)
        {
            for (int gridX = minGridX; gridX <= maxGridX; gridX++)
            {
                if (IsCellSolid(gridX, gridY))
                {
                    // Check if this cell actually overlaps with the rectangle
                    var cellWorldX = GridXToWorld(gridX);
                    var cellWorldY = GridYToWorld(gridY);
                    var cellBounds = new RectangleF(cellWorldX, cellWorldY, _cellSize, _cellSize);

                    if (worldBounds.Intersects(cellBounds))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Clears all collision data (makes all cells non-solid).
    /// </summary>
    public void Clear()
    {
        Array.Clear(_collisionData, 0, _collisionData.Length);
    }

    /// <summary>
    /// Fills a rectangular area with solid or non-solid cells.
    /// </summary>
    /// <param name="minGridX">Minimum grid X coordinate.</param>
    /// <param name="minGridY">Minimum grid Y coordinate.</param>
    /// <param name="maxGridX">Maximum grid X coordinate (inclusive).</param>
    /// <param name="maxGridY">Maximum grid Y coordinate (inclusive).</param>
    /// <param name="isSolid">True to make cells solid, false to make them non-solid.</param>
    public void FillArea(int minGridX, int minGridY, int maxGridX, int maxGridY, bool isSolid)
    {
        for (int gridY = minGridY; gridY <= maxGridY; gridY++)
        {
            for (int gridX = minGridX; gridX <= maxGridX; gridX++)
            {
                SetCell(gridX, gridY, isSolid);
            }
        }
    }

    /// <summary>
    /// Converts world X coordinate to grid X coordinate.
    /// </summary>
    public int WorldToGridX(float worldX)
    {
        return (int)Math.Floor(worldX / _cellSize);
    }

    /// <summary>
    /// Converts world Y coordinate to grid Y coordinate.
    /// </summary>
    public int WorldToGridY(float worldY)
    {
        return (int)Math.Floor(worldY / _cellSize);
    }

    /// <summary>
    /// Converts grid X coordinate to world X coordinate (center of cell).
    /// </summary>
    public float GridXToWorld(int gridX)
    {
        return gridX * _cellSize + _cellSize / 2.0f;
    }

    /// <summary>
    /// Converts grid Y coordinate to world Y coordinate (center of cell).
    /// </summary>
    public float GridYToWorld(int gridY)
    {
        return gridY * _cellSize + _cellSize / 2.0f;
    }

    /// <summary>
    /// Gets the world position of a grid cell's center.
    /// </summary>
    public Vector2 GridToWorld(int gridX, int gridY)
    {
        return new Vector2(GridXToWorld(gridX), GridYToWorld(gridY));
    }
}

/// <summary>
/// Simple rectangle with float coordinates for collision detection.
/// </summary>
public struct RectangleF
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public RectangleF(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool Intersects(RectangleF other)
    {
        return Left < other.Right && Right > other.Left &&
               Top < other.Bottom && Bottom > other.Top;
    }
}








