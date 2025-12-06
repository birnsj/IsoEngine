using GameCore.Collision;
using GameCore.Entities;
using GameCore.Rendering;
using Microsoft.Xna.Framework;
using System.Linq;

namespace GameCore.Services;

/// <summary>
/// Service that handles collision detection between entities and a collision grid.
/// </summary>
public class CollisionService : IGameService
{
    private IsometricTileMap? _tileMap;
    private CollisionGrid? _collisionGrid;
    private bool _isEnabled = true;

    /// <summary>
    /// Gets or sets whether collision detection is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <summary>
    /// Sets the tile map (used for coordinate conversion).
    /// </summary>
    /// <param name="tileMap">The tile map. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if tileMap is null.</exception>
    public void SetTileMap(IsometricTileMap tileMap)
    {
        _tileMap = tileMap ?? throw new ArgumentNullException(nameof(tileMap));
    }

    /// <summary>
    /// Sets the collision grid to use for collision detection.
    /// </summary>
    /// <param name="collisionGrid">The collision grid. Can be null to disable collision.</param>
    public void SetCollisionGrid(CollisionGrid? collisionGrid)
    {
        _collisionGrid = collisionGrid;
    }

    /// <summary>
    /// Gets the collision grid.
    /// </summary>
    public CollisionGrid? GetCollisionGrid()
    {
        return _collisionGrid;
    }

    /// <summary>
    /// Checks if a world position is solid.
    /// </summary>
    public bool IsWorldPositionSolid(Vector2 worldPos)
    {
        if (_collisionGrid == null)
            return false;

        return _collisionGrid.IsWorldPositionSolid(worldPos);
    }

    /// <summary>
    /// Checks if a rectangle (in screen coordinates) overlaps with any solid collision cells.
    /// </summary>
    public bool CheckCollisionWithTiles(Rectangle bounds)
    {
        if (!_isEnabled || _tileMap == null || _collisionGrid == null)
            return false;

        // Convert the bounding box from screen coordinates to world coordinates
        // We need to check all corners to find the world bounds
        var corners = new Vector2[]
        {
            new Vector2(bounds.Left, bounds.Top),
            new Vector2(bounds.Right, bounds.Top),
            new Vector2(bounds.Right, bounds.Bottom),
            new Vector2(bounds.Left, bounds.Bottom)
        };

        // Find the bounding box in world coordinates
        float minWorldX = float.MaxValue;
        float maxWorldX = float.MinValue;
        float minWorldY = float.MaxValue;
        float maxWorldY = float.MinValue;

        foreach (var corner in corners)
        {
            var worldPos = _tileMap.ScreenToWorld(corner);
            minWorldX = Math.Min(minWorldX, worldPos.X);
            maxWorldX = Math.Max(maxWorldX, worldPos.X);
            minWorldY = Math.Min(minWorldY, worldPos.Y);
            maxWorldY = Math.Max(maxWorldY, worldPos.Y);
        }

        // Create a rectangle in world coordinates
        var worldBounds = new RectangleF(
            minWorldX,
            minWorldY,
            maxWorldX - minWorldX,
            maxWorldY - minWorldY);

        // Check collision with the collision grid
        return _collisionGrid.CheckCollision(worldBounds);
    }

    /// <summary>
    /// Resolves collision for an entity by adjusting its position to prevent overlap with solid tiles.
    /// </summary>
    /// <param name="entity">The entity to resolve collision for. Must not be null.</param>
    /// <param name="proposedPosition">The proposed new position.</param>
    /// <exception cref="ArgumentNullException">Thrown if entity is null.</exception>
    public void ResolveCollision(Entity entity, Vector2 proposedPosition)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        if (float.IsNaN(proposedPosition.X) || float.IsNaN(proposedPosition.Y))
            throw new ArgumentException("Proposed position cannot contain NaN", nameof(proposedPosition));

        if (!_isEnabled || !entity.IsSolid || _tileMap == null || _collisionGrid == null)
        {
            // If collision is disabled or no collision grid, allow movement without any checks
            entity.Position = proposedPosition;
            return;
        }

        // Create bounding box at proposed position (in screen coordinates)
        var proposedBounds = new Rectangle(
            (int)(proposedPosition.X - entity.Size.X / 2),
            (int)(proposedPosition.Y - entity.Size.Y / 2),
            (int)entity.Size.X,
            (int)entity.Size.Y);

        // Check collision
        bool hasCollision = CheckCollisionWithTiles(proposedBounds);
        if (!hasCollision)
        {
            // No collision, update position
            entity.Position = proposedPosition;
            return;
        }
        
        // Collision detected - try sliding along one axis

        // Try X-axis only
        var xOnlyPosition = new Vector2(proposedPosition.X, entity.Position.Y);
        var xOnlyBounds = new Rectangle(
            (int)(xOnlyPosition.X - entity.Size.X / 2),
            (int)(xOnlyPosition.Y - entity.Size.Y / 2),
            (int)entity.Size.X,
            (int)entity.Size.Y);
        
        if (!CheckCollisionWithTiles(xOnlyBounds))
        {
            entity.Position = xOnlyPosition;
            return;
        }

        // Try Y-axis only
        var yOnlyPosition = new Vector2(entity.Position.X, proposedPosition.Y);
        var yOnlyBounds = new Rectangle(
            (int)(yOnlyPosition.X - entity.Size.X / 2),
            (int)(yOnlyPosition.Y - entity.Size.Y / 2),
            (int)entity.Size.X,
            (int)entity.Size.Y);
        
        if (!CheckCollisionWithTiles(yOnlyBounds))
        {
            entity.Position = yOnlyPosition;
            return;
        }

        // Can't move in either direction, stay put
    }

    public void Initialize()
    {
        // Collision service doesn't need special initialization
    }

    public void Update(Microsoft.Xna.Framework.GameTime gameTime)
    {
        // Collision service doesn't need per-frame updates
    }

    public void Draw(Microsoft.Xna.Framework.GameTime gameTime)
    {
        // Collision service doesn't need rendering
    }
}

