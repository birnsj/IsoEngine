using GameCore.Entities;
using GameCore.Interactions;
using Microsoft.Xna.Framework;

namespace GameCore.Services;

/// <summary>
/// Service that handles player interactions with interactable objects.
/// </summary>
public class InteractionService : IGameService
{
    private readonly List<IInteractable> _interactables = new();
    private SpatialGrid<Entity>? _spatialGrid;
    private bool _useSpatialPartitioning = true;
    private const float SpatialGridCellSize = 100.0f; // Size of each spatial grid cell

    /// <summary>
    /// Enables or disables spatial partitioning for performance optimization.
    /// </summary>
    public void SetSpatialPartitioning(bool enabled, int gridWidth = 100, int gridHeight = 100)
    {
        _useSpatialPartitioning = enabled;
        if (enabled)
        {
            _spatialGrid = new SpatialGrid<Entity>(SpatialGridCellSize, gridWidth, gridHeight);
            // Re-add all entities to the spatial grid
            foreach (var interactable in _interactables)
            {
                if (interactable is Entity entity)
                {
                    _spatialGrid.Add(entity);
                }
            }
        }
        else
        {
            _spatialGrid = null;
        }
    }

    /// <summary>
    /// Registers an interactable object.
    /// </summary>
    /// <param name="interactable">The interactable to register. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if interactable is null.</exception>
    public void RegisterInteractable(IInteractable interactable)
    {
        if (interactable == null)
            throw new ArgumentNullException(nameof(interactable));

        if (!_interactables.Contains(interactable))
        {
            _interactables.Add(interactable);
            
            // Add to spatial grid if enabled
            if (_useSpatialPartitioning && _spatialGrid != null && interactable is Entity entity)
            {
                _spatialGrid.Add(entity);
            }
        }
    }

    /// <summary>
    /// Unregisters an interactable object.
    /// </summary>
    /// <param name="interactable">The interactable to unregister. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if interactable is null.</exception>
    public void UnregisterInteractable(IInteractable interactable)
    {
        if (interactable == null)
            throw new ArgumentNullException(nameof(interactable));

        _interactables.Remove(interactable);
        
        // Remove from spatial grid if enabled
        if (_useSpatialPartitioning && _spatialGrid != null && interactable is Entity entity)
        {
            _spatialGrid.Remove(entity);
        }
    }

    /// <summary>
    /// Gets all registered interactables.
    /// </summary>
    public IEnumerable<IInteractable> GetAllInteractables()
    {
        return _interactables;
    }

    /// <summary>
    /// Tries to find an interactable object that the player can interact with.
    /// Checks distance and facing direction.
    /// </summary>
    /// <param name="player">The player attempting to interact.</param>
    /// <returns>The nearest interactable within range and facing direction, or null if none found.</returns>
    public IInteractable? TryInteract(Player player)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        IInteractable? bestInteractable = null;
        float bestDistance = float.MaxValue;

        // Use spatial grid if enabled, otherwise fall back to linear search
        IEnumerable<IInteractable> candidates;
        if (_useSpatialPartitioning && _spatialGrid != null)
        {
            // Get entities in range using spatial grid
            var maxRange = GameConstants.Interaction.DefaultRange * 2; // Check wider range for spatial grid
            var entitiesInRange = _spatialGrid.GetEntitiesInRange(player.Position, maxRange);
            candidates = entitiesInRange.OfType<IInteractable>();
        }
        else
        {
            candidates = _interactables;
        }

        foreach (var interactable in candidates)
        {
            // Get position of interactable (if it's an entity)
            Vector2 interactablePosition;
            if (interactable is Entity entity)
            {
                interactablePosition = entity.Position;
            }
            else
            {
                // Skip non-entity interactables for now (could extend later)
                continue;
            }

            // Calculate distance to interactable
            var directionToInteractable = interactablePosition - player.Position;
            var distance = directionToInteractable.Length();

            // Get interaction radius from the interactable (use default if not set or 0)
            var interactionRadius = interactable.InteractionRadius > 0 
                ? interactable.InteractionRadius 
                : GameConstants.Interaction.DefaultRange;

            // Check if within range
            if (distance > interactionRadius)
                continue;

            // Check if player is facing the interactable
            var normalizedDirection = directionToInteractable.LengthSquared() > 0 
                ? Vector2.Normalize(directionToInteractable) 
                : Vector2.Zero;
            
            var facingDot = Vector2.Dot(player.FacingDirection, normalizedDirection);

            // Player must be facing roughly toward the interactable
            if (facingDot < GameConstants.Interaction.FacingAngleTolerance)
                continue;

            // Check if this is closer than the current best
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestInteractable = interactable;
            }
        }

        return bestInteractable;
    }

    /// <summary>
    /// Gets the nearest interactable to the player (for UI display), regardless of facing direction.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The nearest interactable within range, or null if none found.</returns>
    public IInteractable? GetNearestInteractable(Player player)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        IInteractable? nearest = null;
        float nearestDistance = float.MaxValue;

        // Use spatial grid if enabled, otherwise fall back to linear search
        IEnumerable<IInteractable> candidates;
        if (_useSpatialPartitioning && _spatialGrid != null)
        {
            // Get entities in range using spatial grid
            var maxRange = GameConstants.Interaction.DefaultRange * 2; // Check wider range for spatial grid
            var entitiesInRange = _spatialGrid.GetEntitiesInRange(player.Position, maxRange);
            candidates = entitiesInRange.OfType<IInteractable>();
        }
        else
        {
            candidates = _interactables;
        }

        foreach (var interactable in candidates)
        {
            if (interactable is not Entity entity)
                continue;

            var directionToInteractable = entity.Position - player.Position;
            var distance = directionToInteractable.Length();

            // Get interaction radius from the interactable (use default if not set or 0)
            var interactionRadius = interactable.InteractionRadius > 0 
                ? interactable.InteractionRadius 
                : GameConstants.Interaction.DefaultRange;

            if (distance <= interactionRadius && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = interactable;
            }
        }

        return nearest;
    }

    public void Initialize()
    {
        // Interaction service doesn't need special initialization
    }

    public void Update(Microsoft.Xna.Framework.GameTime gameTime)
    {
        // Interaction service doesn't need per-frame updates
    }

    public void Draw(Microsoft.Xna.Framework.GameTime gameTime)
    {
        // Interaction service doesn't need rendering
    }
}

