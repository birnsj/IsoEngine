using GameClient.Entities;
using GameClient.Services;
using GameCore.Entities;
using GameCore.Services;
using Microsoft.Xna.Framework;

namespace GameClient.Rendering;

/// <summary>
/// Service that manages projectiles (bullets) fired by the player.
/// </summary>
public class ProjectileService : IGameService
{
    private readonly List<Projectile> _activeProjectiles = new();
    private ILogger? _logger;
    private const float ProjectileSpeed = 400.0f; // Pixels per second
    private const float ProjectileMaxRange = 500.0f; // Maximum range in pixels
    private const int ProjectileDamage = 10; // Base damage per projectile

    public ProjectileService()
    {
        // Logger will be retrieved in Initialize
    }

    public void Initialize()
    {
        _logger = ServiceLocator.Get<ILogger>();
    }

    public void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var combatService = ServiceLocator.Get<CombatService>();
        var enemyService = ServiceLocator.Get<EnemyService>();
        var player = ServiceLocator.Get<GameCore.Entities.Player>();

        // Update all active projectiles
        for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
        {
            var projectile = _activeProjectiles[i];

            // Check if expired
            if (projectile.IsExpired)
            {
                _activeProjectiles.RemoveAt(i);
                continue;
            }

            // Update position
            projectile.Position += projectile.Velocity * deltaTime;

            // Enemy projectiles hit the player
            if (projectile.IsEnemyProjectile && !projectile.HasHit && player != null && !player.Stats.IsDead)
            {
                var distanceToPlayer = Vector2.Distance(projectile.Position, player.Position);
                var hitRadius = player.Size.X / 2 + projectile.Size + 5.0f; // Slightly larger hit radius for player
                if (distanceToPlayer <= hitRadius)
                {
                    HandlePlayerHit(player, projectile);
                    projectile.HasHit = true;
                    continue;
                }
            }

            // Player projectiles hit enemies
            if (!projectile.IsEnemyProjectile && enemyService != null && combatService != null && !projectile.HasHit)
            {
                // Check melee enemies - use larger search range to ensure we don't miss fast projectiles
                var searchRange = 50.0f; // Search within 50 pixels
                var enemies = enemyService.GetEnemiesInRange(projectile.Position, searchRange);
                foreach (var enemy in enemies)
                {
                    if (enemy.Stats.IsDead || !enemy.IsActive)
                        continue;

                    // Check if projectile is close enough to hit (enemy hitbox + projectile size)
                    var distanceToEnemy = Vector2.Distance(projectile.Position, enemy.Position);
                    var hitRadius = enemy.Size.X / 2 + projectile.Size;
                    if (distanceToEnemy <= hitRadius)
                    {
                        HandleEnemyHit(enemy, projectile, combatService, player);
                        projectile.HasHit = true;
                        break; // Projectile can only hit one enemy
                    }
                }
                
                // Check ranged enemies if projectile hasn't hit yet
                if (!projectile.HasHit)
                {
                    var rangedEnemies = enemyService.GetAllRangedEnemies();
                    foreach (var rangedEnemy in rangedEnemies)
                    {
                        if (rangedEnemy.Stats.IsDead || !rangedEnemy.IsActive)
                            continue;

                        // Check if projectile is close enough to hit (enemy hitbox + projectile size)
                        var distanceToEnemy = Vector2.Distance(projectile.Position, rangedEnemy.Position);
                        var hitRadius = rangedEnemy.Size.X / 2 + projectile.Size;
                        if (distanceToEnemy <= hitRadius)
                        {
                            HandleRangedEnemyHit(rangedEnemy, projectile, combatService, player);
                            projectile.HasHit = true;
                            break; // Projectile can only hit one enemy
                        }
                    }
                }
            }
        }
    }

    public void Draw(GameTime gameTime)
    {
        // Projectiles are rendered by ProjectileRenderer
    }

    /// <summary>
    /// Fires a projectile from the specified position in the specified direction.
    /// </summary>
    /// <param name="position">Starting position of the projectile.</param>
    /// <param name="direction">Direction vector (will be normalized).</param>
    /// <param name="damage">Optional damage override. Uses default if not specified.</param>
    /// <param name="isEnemyProjectile">If true, projectile will hit the player. If false, it hits enemies.</param>
    public void FireProjectile(Vector2 position, Vector2 direction, int? damage = null, bool isEnemyProjectile = false)
    {
        if (direction.LengthSquared() < 0.01f)
            return; // Invalid direction

        var normalizedDirection = Vector2.Normalize(direction);
        var velocity = normalizedDirection * ProjectileSpeed;
        var projectileDamage = damage ?? ProjectileDamage;

        var projectile = new Projectile(position, velocity, projectileDamage, ProjectileMaxRange, isEnemyProjectile);
        _activeProjectiles.Add(projectile);

        _logger?.Debug($"Fired {(isEnemyProjectile ? "enemy" : "player")} projectile from {position} in direction {normalizedDirection}");
    }

    /// <summary>
    /// Gets all active projectiles.
    /// </summary>
    public IEnumerable<Projectile> GetActiveProjectiles()
    {
        return _activeProjectiles;
    }

    /// <summary>
    /// Clears all active projectiles.
    /// </summary>
    public void Clear()
    {
        _activeProjectiles.Clear();
    }
    
    /// <summary>
    /// Handles when a projectile hits a melee enemy.
    /// </summary>
    private void HandleEnemyHit(Enemy enemy, Projectile projectile, CombatService? combatService, GameCore.Entities.Player? player)
    {
        // Hit! Apply damage
        var damage = projectile.Damage;
        var actualDamage = enemy.Stats.ApplyDamage(damage);
        
        _logger?.Info($"Projectile hit enemy for {actualDamage} damage (enemy health: {enemy.Stats.CurrentHealth}/{enemy.Stats.MaxHealth})");

        // Trigger hit flash if enemy supports it
        if (enemy is GameCore.Combat.IFlashable flashable)
        {
            flashable.TriggerHitFlash();
        }

        // Create blood splatter effect (tracked by enemy for death fading)
        var particleSystem = ServiceLocator.Get<ParticleSystem>();
        if (particleSystem != null)
        {
            var hitDirection = Vector2.Normalize(projectile.Velocity);
            var bloodEmitter = ParticlePreset.BloodSplatter(enemy.Position, hitDirection, 1.0f);
            particleSystem.CreateEmitter(bloodEmitter, enemy); // Associate with enemy for death fading
        }

        // Make enemy rush the player when hit by projectile
        if (player != null && !enemy.Stats.IsDead && !player.Stats.IsDead)
        {
            // Trigger rush behavior - enemy will rush toward player (only when shot)
            enemy.TriggerRush();
            
            // If player is in attack range, also attack immediately
            if (combatService != null)
            {
                var distanceToPlayer = Vector2.Distance(enemy.Position, player.Position);
                if (distanceToPlayer <= enemy.AttackDistance)
                {
                    // Attack immediately
                    combatService.PerformMeleeAttack(enemy, player);
                    // Reset attack timer to prevent immediate follow-up (half cooldown for reactive attack)
                    enemy.AttackTimer = 1.5f * 0.5f; // Use AttackCooldown value (1.5f) * 0.5f
                }
            }
        }

        // Check if enemy died
        if (enemy.Stats.IsDead)
        {
            _logger?.Info("Enemy killed by projectile!");
        }
    }
    
    /// <summary>
    /// Handles when an enemy projectile hits the player.
    /// </summary>
    private void HandlePlayerHit(GameCore.Entities.Player player, Projectile projectile)
    {
        // Apply damage to player
        var damage = projectile.Damage;
        var actualDamage = player.Stats.ApplyDamage(damage);
        
        _logger?.Info($"Enemy projectile hit player for {actualDamage} damage (player health: {player.Stats.CurrentHealth}/{player.Stats.MaxHealth})");

        // Trigger hit flash on player
        if (player is GameCore.Combat.IFlashable flashable)
        {
            flashable.TriggerHitFlash();
        }

        // Create blood splatter effect
        var particleSystem = ServiceLocator.Get<ParticleSystem>();
        if (particleSystem != null)
        {
            var hitDirection = Vector2.Normalize(projectile.Velocity);
            var bloodEmitter = ParticlePreset.BloodSplatter(player.Position, hitDirection, 1.0f);
            particleSystem.CreateEmitter(bloodEmitter);
        }

        // Check if player died
        if (player.Stats.IsDead)
        {
            _logger?.Info("Player killed by enemy projectile!");
        }
    }

    /// <summary>
    /// Handles when a projectile hits a ranged enemy.
    /// </summary>
    private void HandleRangedEnemyHit(RangedEnemy rangedEnemy, Projectile projectile, CombatService? combatService, GameCore.Entities.Player? player)
    {
        // Hit! Apply damage
        var damage = projectile.Damage;
        var actualDamage = rangedEnemy.Stats.ApplyDamage(damage);
        
        _logger?.Info($"Projectile hit ranged enemy for {actualDamage} damage (enemy health: {rangedEnemy.Stats.CurrentHealth}/{rangedEnemy.Stats.MaxHealth})");

        // Trigger hit flash if enemy supports it
        if (rangedEnemy is GameCore.Combat.IFlashable flashable)
        {
            flashable.TriggerHitFlash();
        }

        // Create blood splatter effect (tracked by enemy for death fading)
        var particleSystem = ServiceLocator.Get<ParticleSystem>();
        if (particleSystem != null)
        {
            var hitDirection = Vector2.Normalize(projectile.Velocity);
            var bloodEmitter = ParticlePreset.BloodSplatter(rangedEnemy.Position, hitDirection, 1.0f);
            particleSystem.CreateEmitter(bloodEmitter, rangedEnemy); // Associate with enemy for death fading
        }

        // Make ranged enemy rush the player when hit
        if (player != null && !rangedEnemy.Stats.IsDead && !player.Stats.IsDead)
        {
            // Trigger rush behavior - enemy will move toward player
            rangedEnemy.TriggerRush();
        }

        // Check if enemy died
        if (rangedEnemy.Stats.IsDead)
        {
            _logger?.Info("Ranged enemy killed by projectile!");
        }
    }
}

