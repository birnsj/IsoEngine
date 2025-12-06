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
/// A simple enemy entity that moves toward the player and can be attacked.
/// </summary>
public class Enemy : Entity, IFlashable
{
    private readonly Player _player;
    private readonly IsometricTileMap _tileMap;
    private CollisionService? _collisionService;
    
    // Base values for tile size (will be scaled based on actual tile size)
    private const int BaseTileWidth = GameConstants.Tiles.FixedWidth;
    private const float BaseMoveSpeed = 50.0f; // Base speed for 64x32 tiles
    private const float BaseRushSpeed = 30.0f; // Base rush speed (slower than normal movement)
    private const float BaseAttackRange = 60.0f; // Base attack range for 64x32 tiles
    private const float BaseAttackDistance = 40.0f; // Base attack distance for 64x32 tiles
    private const float BaseMinMoveDistance = 5.0f; // Base minimum move distance for 64x32 tiles
    private const float AttackCooldown = 1.5f; // Seconds between melee attacks
    private const float HitFlashDuration = 0.2f; // Duration of hit flash in seconds
    private const float RushDuration = 5.0f; // How long enemy rushes player after being hit
    private float _attackTimer = 0.0f;
    private float _hitFlashTimer = 0.0f;
    private float _rushTimer = 0.0f; // Timer for rushing the player after being hit
    private Vector2 _lastFootstepPosition; // Last position where footstep was emitted
    private const float FootstepDistance = 30.0f; // Distance between footsteps
    
    // Computed properties that scale with tile size
    private float TileScale => (float)_tileMap.TileWidth / BaseTileWidth;
    private float MoveSpeed => BaseMoveSpeed * TileScale;
    private float RushSpeed => BaseRushSpeed * TileScale; // Slower speed when rushing
    
    /// <summary>
    /// Gets the attack/aggro range in pixels. Enemy will move toward player if within this range.
    /// </summary>
    public float AttackRange => BaseAttackRange * TileScale;
    
    /// <summary>
    /// Gets the attack distance in pixels. Enemy will melee attack if player is within this range.
    /// </summary>
    public float AttackDistance => BaseAttackDistance * TileScale;
    
    /// <summary>
    /// Gets or sets the attack cooldown timer. Used to control attack frequency.
    /// </summary>
    internal float AttackTimer
    {
        get => _attackTimer;
        set => _attackTimer = value;
    }
    
    /// <summary>
    /// Triggers the enemy to rush the player (used when hit by projectile).
    /// </summary>
    public void TriggerRush()
    {
        _rushTimer = RushDuration;
    }
    private float MinMoveDistance => BaseMinMoveDistance * TileScale;

    /// <summary>
    /// Gets the enemy's stats component.
    /// </summary>
    public StatsComponent Stats { get; }

    /// <summary>
    /// Gets the enemy's inventory (9 slots for 3x3 grid).
    /// </summary>
    public Inventory EnemyInventory { get; }

    /// <summary>
    /// Gets whether the enemy is currently flashing from being hit.
    /// </summary>
    public bool IsFlashing => _hitFlashTimer > 0.0f;

    /// <summary>
    /// Initializes a new instance of the Enemy class.
    /// </summary>
    /// <param name="position">Initial position in screen coordinates.</param>
    /// <param name="player">The player to target.</param>
    /// <param name="tileMap">The tile map for collision detection.</param>
    /// <param name="maxHealth">Maximum health.</param>
    /// <param name="attackPower">Attack power.</param>
    /// <param name="defense">Defense value.</param>
    public Enemy(Vector2 position, Player player, IsometricTileMap tileMap, int maxHealth = 50, int attackPower = 8, int defense = 1)
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
    }

    /// <summary>
    /// Updates the enemy's AI and movement.
    /// </summary>
    /// <param name="gameTime">The game time.</param>
    public void Update(GameTime gameTime)
    {
        if (!IsActive || Stats.IsDead)
            return;

        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _attackTimer -= deltaTime;
        _hitFlashTimer -= deltaTime;
        _rushTimer -= deltaTime;

        // Get collision service if not already cached
        if (_collisionService == null)
        {
            _collisionService = ServiceLocator.Get<CollisionService>();
        }

        // Calculate direction to player
        var directionToPlayer = _player.Position - Position;
        var distanceToPlayer = directionToPlayer.Length();

        // Check if player is in various ranges
        bool inAttackRange = distanceToPlayer <= AttackRange;
        bool withinAttackDistance = distanceToPlayer <= AttackDistance;
        
        // Trigger rush when player enters close attack radius (if not already rushing)
        if (inAttackRange && _rushTimer <= 0.0f)
        {
            // Player entered attack radius - start rushing
            _rushTimer = RushDuration;
        }
        
        // Rush toward player (when rush timer is active or when hit by projectile)
        bool shouldRush = _rushTimer > 0.0f;
        
        if (shouldRush && distanceToPlayer > MinMoveDistance)
        {
            // Rushing: move slower toward player
            if (directionToPlayer.LengthSquared() > 0.01f)
            {
                var normalizedDirection = Vector2.Normalize(directionToPlayer);
                // Move at slower rush speed
                var moveVector = normalizedDirection * RushSpeed * deltaTime;
                
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
                
                // Try to move
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

        // Melee attack player if close enough and cooldown is ready
        if (distanceToPlayer <= AttackDistance && _attackTimer <= 0.0f)
        {
            var combatService = ServiceLocator.Get<CombatService>();
            if (combatService != null && !_player.Stats.IsDead)
            {
                combatService.PerformMeleeAttack(this, _player);
                _attackTimer = AttackCooldown;
            }
        }
    }

    /// <summary>
    /// Triggers the hit flash effect when the enemy is damaged.
    /// </summary>
    public void TriggerHitFlash()
    {
        _hitFlashTimer = HitFlashDuration;
    }
}

