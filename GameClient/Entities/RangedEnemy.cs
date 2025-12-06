using GameCore;
using GameCore.Combat;
using GameCore.Entities;
using GameCore.Items;
using GameCore.Rendering;
using GameCore.Services;
using GameClient.Rendering;
using Microsoft.Xna.Framework;

namespace GameClient.Entities;

/// <summary>
/// A ranged enemy that shoots projectiles and patrols between waypoints.
/// </summary>
public class RangedEnemy : Entity, IFlashable
{
    private readonly Player _player;
    private readonly IsometricTileMap _tileMap;
    private CollisionService? _collisionService;
    private ProjectileService? _projectileService;
    
    // Base values for 64x32 tile size (will be scaled based on actual tile size)
    private const int BaseTileWidth = 64;
    private const float BaseMoveSpeed = 40.0f; // Base speed for 64x32 tiles (slower than melee enemies)
    private const float BaseAttackRange = 300.0f; // Base attack range for ranged enemies (much longer)
    private const float BaseMinMoveDistance = 5.0f; // Base minimum move distance for 64x32 tiles
    private const float AttackCooldown = 2.0f; // Seconds between ranged attacks
    private const float HitFlashDuration = 0.2f; // Duration of hit flash in seconds
    private const float RushDuration = 5.0f; // How long enemy rushes player after being hit
    private const float PatrolPauseDuration = 2.0f; // How long to pause at each patrol point
    private const float PatrolArrivalDistance = 10.0f; // Distance to consider "arrived" at patrol point
    
    private float _attackTimer = 0.0f;
    private float _hitFlashTimer = 0.0f;
    private float _rushTimer = 0.0f;
    private float _patrolPauseTimer = 0.0f;
    private int _currentPatrolIndex = 0;
    private Vector2 _facingDirection = Vector2.UnitY;
    private Vector2 _lastFootstepPosition; // Last position where footstep was emitted
    private const float FootstepDistance = 30.0f; // Distance between footsteps
    
    // Computed properties that scale with tile size
    private float TileScale => (float)_tileMap.TileWidth / BaseTileWidth;
    private float MoveSpeed => BaseMoveSpeed * TileScale;
    private float AttackRange => BaseAttackRange * TileScale;
    private float MinMoveDistance => BaseMinMoveDistance * TileScale;
    private float ArrivalDistance => PatrolArrivalDistance * TileScale;
    
    /// <summary>
    /// Gets the enemy's stats component.
    /// </summary>
    public StatsComponent Stats { get; }
    
    /// <summary>
    /// Gets the enemy's inventory for loot drops.
    /// </summary>
    public Inventory EnemyInventory { get; }
    
    /// <summary>
    /// Gets whether the enemy has items in their inventory.
    /// </summary>
    public bool HasLoot => !EnemyInventory.IsEmpty();
    
    /// <summary>
    /// Gets whether the enemy is currently flashing from being hit.
    /// </summary>
    public bool IsFlashing => _hitFlashTimer > 0.0f;
    
    /// <summary>
    /// Gets or sets the patrol waypoints for this enemy.
    /// </summary>
    public List<Vector2> PatrolWaypoints { get; set; } = new();
    
    /// <summary>
    /// Gets or sets whether this enemy drops a key when killed.
    /// </summary>
    public bool DropsKey { get; set; } = false;
    
    /// <summary>
    /// Initializes a new instance of the RangedEnemy class.
    /// </summary>
    /// <param name="position">Initial position in screen coordinates.</param>
    /// <param name="player">The player to target.</param>
    /// <param name="tileMap">The tile map for collision detection.</param>
    /// <param name="patrolWaypoints">List of patrol waypoints in screen coordinates.</param>
    /// <param name="maxHealth">Maximum health.</param>
    /// <param name="attackPower">Attack power (projectile damage).</param>
    /// <param name="defense">Defense value.</param>
    public RangedEnemy(Vector2 position, Player player, IsometricTileMap tileMap, List<Vector2>? patrolWaypoints = null, int maxHealth = 40, int attackPower = 8, int defense = 1)
        : base(position, new Vector2(GameConstants.Player.DefaultSize, GameConstants.Player.DefaultSize))
    {
        _player = player;
        _tileMap = tileMap;
        IsSolid = true;
        _lastFootstepPosition = position; // Initialize footstep position
        
        // Add stats component
        Stats = new StatsComponent(maxHealth, attackPower, defense);
        AddComponent(Stats);
        
        // Create inventory with 9 slots (3x3 grid)
        EnemyInventory = new Inventory(9);
        
        // Set up patrol waypoints
        if (patrolWaypoints != null && patrolWaypoints.Count > 0)
        {
            PatrolWaypoints = new List<Vector2>(patrolWaypoints);
            // Start at first waypoint if not already there
            if (PatrolWaypoints.Count > 0)
            {
                _currentPatrolIndex = 0;
            }
        }
        else
        {
            // Default: single waypoint at starting position
            PatrolWaypoints = new List<Vector2> { position };
        }
    }
    
    /// <summary>
    /// Updates the enemy's AI, movement, and patrol behavior.
    /// </summary>
    public void Update(GameTime gameTime)
    {
        if (!IsActive || Stats.IsDead)
            return;
            
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _attackTimer -= deltaTime;
        _hitFlashTimer -= deltaTime;
        _rushTimer -= deltaTime;
        _patrolPauseTimer -= deltaTime;
        
        // Get services if not already cached
        if (_collisionService == null)
        {
            _collisionService = ServiceLocator.Get<CollisionService>();
        }
        if (_projectileService == null)
        {
            _projectileService = ServiceLocator.Get<ProjectileService>();
        }
        
        // Calculate direction to player
        var directionToPlayer = _player.Position - Position;
        var distanceToPlayer = directionToPlayer.Length();
        
        // Check if player is in attack range
        bool playerInRange = distanceToPlayer <= AttackRange;
        bool shouldRush = _rushTimer > 0.0f;
        
        // Face the player if in range or rushing
        if ((playerInRange || shouldRush) && directionToPlayer.LengthSquared() > 0.01f)
        {
            _facingDirection = Vector2.Normalize(directionToPlayer);
        }
        
        // Shoot at player if in range and cooldown is ready
        if (playerInRange && _attackTimer <= 0.0f && _projectileService != null && !_player.Stats.IsDead)
        {
            _projectileService.FireProjectile(Position, directionToPlayer, Stats.AttackPower, isEnemyProjectile: true);
            _attackTimer = AttackCooldown;
        }
        
        // Move toward player only if rushing (when hit by projectile)
        if (shouldRush && distanceToPlayer > MinMoveDistance)
        {
            var normalizedDirection = Vector2.Normalize(directionToPlayer);
            var moveVector = normalizedDirection * MoveSpeed * deltaTime;
            
            // Check if we should emit footstep particles
            var distanceSinceLastFootstep = Vector2.Distance(Position, _lastFootstepPosition);
            if (distanceSinceLastFootstep >= FootstepDistance)
            {
                // Emit footstep dust at enemy's feet (slightly behind the center)
                var footPosition = Position - normalizedDirection * (Size.Y * 0.3f);
                var particleSystem = ServiceLocator.Get<ParticleSystem>();
                particleSystem?.CreateEmitter(ParticlePreset.FootstepDust(footPosition, 1.0f));
                _lastFootstepPosition = Position;
            }
            
            var proposedPosition = Position + moveVector;
            if (_collisionService != null)
            {
                _collisionService.ResolveCollision(this, proposedPosition);
            }
            else
            {
                Position = proposedPosition;
            }
        }
        // No patrolling - ranged enemies stay in place
    }
    
    /// <summary>
    /// Updates patrol behavior - moves between waypoints and pauses at them.
    /// </summary>
    private void UpdatePatrol(float deltaTime)
    {
        if (PatrolWaypoints.Count == 0)
            return;
            
        // If paused at a waypoint, wait
        if (_patrolPauseTimer > 0.0f)
        {
            return; // Stay paused
        }
        
        // Get current target waypoint
        var targetWaypoint = PatrolWaypoints[_currentPatrolIndex];
        var directionToWaypoint = targetWaypoint - Position;
        var distanceToWaypoint = directionToWaypoint.Length();
        
        // Check if we've arrived at the waypoint
        if (distanceToWaypoint <= ArrivalDistance)
        {
            // Arrived! Pause at this waypoint
            _patrolPauseTimer = PatrolPauseDuration;
            
            // Move to next waypoint (loop back to start)
            _currentPatrolIndex = (_currentPatrolIndex + 1) % PatrolWaypoints.Count;
        }
        else
        {
            // Move toward current waypoint
            if (directionToWaypoint.LengthSquared() > 0.01f)
            {
                var normalizedDirection = Vector2.Normalize(directionToWaypoint);
                _facingDirection = normalizedDirection;
                
                var moveVector = normalizedDirection * MoveSpeed * deltaTime;
                var proposedPosition = Position + moveVector;
                
                if (_collisionService != null)
                {
                    _collisionService.ResolveCollision(this, proposedPosition);
                }
                else
                {
                    Position = proposedPosition;
                }
            }
        }
    }
    
    /// <summary>
    /// Triggers the enemy to rush the player (used when hit by projectile).
    /// </summary>
    public void TriggerRush()
    {
        _rushTimer = RushDuration;
    }
    
    /// <summary>
    /// Triggers the hit flash effect when the enemy is damaged.
    /// </summary>
    public void TriggerHitFlash()
    {
        _hitFlashTimer = HitFlashDuration;
    }
}

