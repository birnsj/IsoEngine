using GameClient.Entities;
using GameCore.Entities;
using GameCore.Services;
using Microsoft.Xna.Framework;

namespace GameClient.Services;

/// <summary>
/// Service that manages enemy entities.
/// </summary>
public class EnemyService : IGameService
{
    private readonly List<Enemy> _enemies = new();
    private readonly List<RangedEnemy> _rangedEnemies = new();
    private readonly ILogger _logger;
    private readonly GameCore.State.GameStateService? _gameStateService;
    private Dictionary<string, GameCore.Items.Item>? _itemDatabase;

    /// <summary>
    /// Initializes a new instance of the EnemyService class.
    /// </summary>
    /// <param name="logger">The logger service. Must not be null.</param>
    /// <param name="gameStateService">Optional game state service for state checking.</param>
    /// <param name="itemDatabase">Optional item database for dropping items.</param>
    /// <exception cref="ArgumentNullException">Thrown if logger is null.</exception>
    public EnemyService(ILogger logger, GameCore.State.GameStateService? gameStateService = null, Dictionary<string, GameCore.Items.Item>? itemDatabase = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gameStateService = gameStateService;
        _itemDatabase = itemDatabase;
    }
    
    /// <summary>
    /// Sets the item database for dropping items.
    /// </summary>
    public void SetItemDatabase(Dictionary<string, GameCore.Items.Item>? itemDatabase)
    {
        _itemDatabase = itemDatabase;
    }

    public void Initialize()
    {
        // Initialization complete via constructor injection
    }

    public void Update(GameTime gameTime)
    {
        // Only update enemies when in the main game state
        if (_gameStateService != null && _gameStateService.CurrentState != GameCore.State.GameState.GameScreen)
        {
            return;
        }

        // Update all active melee enemies
        foreach (var enemy in _enemies)
        {
            if (enemy.IsActive && !enemy.Stats.IsDead)
            {
                enemy.Update(gameTime);
            }
        }
        
        // Update all active ranged enemies
        foreach (var rangedEnemy in _rangedEnemies)
        {
            if (rangedEnemy.IsActive && !rangedEnemy.Stats.IsDead)
            {
                rangedEnemy.Update(gameTime);
            }
        }

        // Remove dead enemies (mark as inactive) and trigger death effects
        var deathEffectService = GameCore.Services.ServiceLocator.Get<GameClient.Rendering.DeathEffectService>();
        var particleSystem = GameCore.Services.ServiceLocator.Get<GameClient.Rendering.ParticleSystem>();
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        // Handle melee enemies
        foreach (var enemy in _enemies)
        {
            if (enemy.Stats.IsDead && enemy.IsActive)
            {
                // Stop all particles attached to this enemy when it dies (freeze them in place)
                if (particleSystem != null)
                {
                    particleSystem.StopParticlesForEnemy(enemy);
                }
            }
            
            // Mark inactive immediately when dead
            if (enemy.Stats.IsDead && enemy.IsActive)
            {
                enemy.IsActive = false;
                _logger.Info($"Enemy died and was marked inactive");
            }
        }
        
        // Handle ranged enemies
        foreach (var rangedEnemy in _rangedEnemies)
        {
            if (rangedEnemy.Stats.IsDead && rangedEnemy.IsActive)
            {
                // Stop all particles attached to this enemy when it dies (freeze them in place)
                if (particleSystem != null)
                {
                    particleSystem.StopParticlesForEnemy(rangedEnemy);
                }
            }
            
            // Mark inactive immediately when dead
            if (rangedEnemy.Stats.IsDead && rangedEnemy.IsActive)
            {
                // Drop key if this enemy is marked to drop one
                if (rangedEnemy.DropsKey)
                {
                    DropKeyForRangedEnemy(rangedEnemy);
                }
                
                rangedEnemy.IsActive = false;
                _logger.Info($"Ranged enemy died and was marked inactive");
            }
        }
    }

    public void Draw(GameTime gameTime)
    {
        // Enemy service doesn't need rendering (enemies are rendered by EntityRenderer)
    }

    /// <summary>
    /// Registers an enemy with the service.
    /// </summary>
    public void RegisterEnemy(Enemy enemy)
    {
        if (enemy == null)
            throw new ArgumentNullException(nameof(enemy));

        if (!_enemies.Contains(enemy))
        {
            _enemies.Add(enemy);
            _logger.Debug($"Registered enemy at {enemy.Position}");
        }
    }

    /// <summary>
    /// Unregisters an enemy from the service.
    /// </summary>
    public void UnregisterEnemy(Enemy enemy)
    {
        if (enemy == null)
            throw new ArgumentNullException(nameof(enemy));

        _enemies.Remove(enemy);
        _logger.Debug($"Unregistered enemy");
    }

    /// <summary>
    /// Registers a ranged enemy with the service.
    /// </summary>
    public void RegisterRangedEnemy(RangedEnemy rangedEnemy)
    {
        if (rangedEnemy == null)
            throw new ArgumentNullException(nameof(rangedEnemy));

        if (!_rangedEnemies.Contains(rangedEnemy))
        {
            _rangedEnemies.Add(rangedEnemy);
            _logger.Debug($"Registered ranged enemy at {rangedEnemy.Position}");
        }
    }

    /// <summary>
    /// Gets all registered enemies (both melee and ranged).
    /// </summary>
    public IEnumerable<Enemy> GetAllEnemies()
    {
        return _enemies;
    }
    
    /// <summary>
    /// Gets all registered ranged enemies.
    /// </summary>
    public IEnumerable<RangedEnemy> GetAllRangedEnemies()
    {
        return _rangedEnemies;
    }
    
    /// <summary>
    /// Gets all enemies (melee and ranged) as entities for collision/range checks.
    /// </summary>
    public IEnumerable<Entity> GetAllEnemyEntities()
    {
        foreach (var enemy in _enemies)
        {
            yield return enemy;
        }
        foreach (var rangedEnemy in _rangedEnemies)
        {
            yield return rangedEnemy;
        }
    }

    /// <summary>
    /// Gets enemies within a certain range of a position.
    /// </summary>
    /// <param name="position">The center position.</param>
    /// <param name="range">The range in pixels.</param>
    /// <returns>List of enemies within range.</returns>
    public List<Enemy> GetEnemiesInRange(Vector2 position, float range)
    {
        if (range < 0)
            throw new ArgumentException("Range cannot be negative", nameof(range));
        if (float.IsNaN(position.X) || float.IsNaN(position.Y))
            throw new ArgumentException("Position cannot contain NaN", nameof(position));

        var enemiesInRange = new List<Enemy>();
        var rangeSquared = range * range;

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.Stats.IsDead)
                continue;

            var distanceSquared = (enemy.Position - position).LengthSquared();
            if (distanceSquared <= rangeSquared)
            {
                enemiesInRange.Add(enemy);
            }
        }
        
        // Also check ranged enemies (they can be hit by projectiles too)
        foreach (var rangedEnemy in _rangedEnemies)
        {
            if (!rangedEnemy.IsActive || rangedEnemy.Stats.IsDead)
                continue;

            var distanceSquared = (rangedEnemy.Position - position).LengthSquared();
            if (distanceSquared <= rangeSquared)
            {
                // RangedEnemy inherits from Entity but not Enemy, so we can't add it to the list
                // But we can handle it separately in ProjectileService
            }
        }

        return enemiesInRange;
    }
    
    /// <summary>
    /// Drops a key item at the position of the killed ranged enemy.
    /// </summary>
    private void DropKeyForRangedEnemy(RangedEnemy rangedEnemy)
    {
        // Get interaction service and entity renderer
        var interactionService = GameCore.Services.ServiceLocator.Get<GameCore.Services.InteractionService>();
        var entityRenderer = GameCore.Services.ServiceLocator.Get<GameClient.Rendering.EntityRenderer>();
        
        if (interactionService == null || entityRenderer == null)
        {
            _logger?.Warning("Cannot drop key: missing required services");
            return;
        }
        
        // Try to get the key item from the database
        if (_itemDatabase != null && _itemDatabase.TryGetValue("key", out var keyItem))
        {
            // Create a ground item at the enemy's position
            var groundKey = new GameClient.Entities.GroundItem(rangedEnemy.Position, keyItem, 1);
            
            // Register it as an interactable
            interactionService.RegisterInteractable(groundKey);
            entityRenderer.AddEntity(groundKey);
            
            _logger?.Info($"Key dropped at {rangedEnemy.Position} from killed ranged enemy");
        }
        else
        {
            _logger?.Warning("Cannot drop key: key item not found in item database or database not set");
        }
    }
}

