using System;
using GameCore.Entities;
using GameCore.Input;
using GameCore.Interactions;
using GameCore.Rendering;
using GameCore.Services;
using GameClient.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Entities;

/// <summary>
/// Service that controls player movement based on input.
/// </summary>
public class PlayerControllerService : IGameService
{
    private readonly Player _player;
    private readonly Camera2D _camera;
    private readonly IsometricTileMap _tileMap;
    private CollisionService? _collisionService;
    private InteractionService? _interactionService;
    private CombatService? _combatService;
    private GraphicsDevice? _graphicsDevice;
    private ClickEffectRenderer? _clickEffectRenderer;
    private ParticleSystem? _particleSystem;
    private ProjectileService? _projectileService;
    private Vector2? _targetPosition; // Target position for click-to-move
    private Vector2 _currentVelocity; // Current velocity for smooth acceleration
    private bool _wasPausedLastFrame = false; // Track if we were paused to reset state when resuming
    private Func<bool>? _isInventoryOpenCheck; // Function to check if inventory is open
    private Vector2 _lastFootstepPosition; // Last position where footstep was emitted
    private const float FootstepDistance = 30.0f; // Distance between footsteps
    private IInteractable? _pendingInteractable; // Interactable to interact with when player reaches it
    
    // Base values for 64x32 tile size (will be scaled based on actual tile size)
    private const int BaseTileWidth = 64;
    private const float BaseArrivalDistance = 5.0f;
    private const float BaseAttackRange = 80.0f;
    private const float BaseInteractionRange = 80.0f;
    private const float BaseAcceleration = 800.0f;
    private const float BaseDeceleration = 1000.0f;
    private const float BaseMovementSpeed = 150.0f; // Base movement speed for 64x32 tiles
    private const float BaseInteractableClickRadius = 30.0f; // Base click radius for interactables
    private const float BaseEnemyClickRadius = 40.0f; // Base click radius for enemies (larger for easier targeting)
    
    // Computed properties that scale with tile size
    private float TileScale => (float)_tileMap.TileWidth / BaseTileWidth;
    private float ArrivalDistance => BaseArrivalDistance * TileScale;
    private float AttackRange => BaseAttackRange * TileScale;
    private float InteractionRange => BaseInteractionRange * TileScale;
    private float Acceleration => BaseAcceleration * TileScale;
    private float Deceleration => BaseDeceleration * TileScale;
    private float InteractableClickRadius => BaseInteractableClickRadius * TileScale;
    private float EnemyClickRadius => BaseEnemyClickRadius * TileScale;

    public PlayerControllerService(Player player, Camera2D camera, IsometricTileMap tileMap)
    {
        _player = player;
        _camera = camera;
        _tileMap = tileMap;
    }

    public void Initialize()
    {
        _collisionService = ServiceLocator.Get<CollisionService>();
        _interactionService = ServiceLocator.Get<InteractionService>();
        _combatService = ServiceLocator.Get<CombatService>();
        _particleSystem = ServiceLocator.Get<ParticleSystem>();
        _projectileService = ServiceLocator.Get<ProjectileService>();
        _lastFootstepPosition = _player.Position;
    }

    public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public void SetClickEffectRenderer(ClickEffectRenderer renderer)
    {
        _clickEffectRenderer = renderer;
    }

    /// <summary>
    /// Sets a function to check if the inventory window is open (for pausing player controls).
    /// </summary>
    public void SetInventoryOpenCheck(Func<bool> checkFunction)
    {
        _isInventoryOpenCheck = checkFunction;
    }

    public void Update(GameTime gameTime)
    {
        // Do not update player controls when not in main game state
        var gameStateService = ServiceLocator.Get<GameCore.State.GameStateService>();
        if (gameStateService != null && gameStateService.CurrentState != GameCore.State.GameState.GameScreen)
        {
            _player.Velocity = Vector2.Zero;
            _wasPausedLastFrame = true;
            return;
        }
        
        // Check if dialog or inventory is active (pauses gameplay)
        var dialogService = ServiceLocator.Get<GameCore.Services.DialogService>();
        bool isDialogActive = dialogService?.IsDialogActive ?? false;
        bool isInventoryOpen = _isInventoryOpenCheck?.Invoke() ?? false;
        bool isPaused = isDialogActive || isInventoryOpen;
        
        if (isPaused)
        {
            // Dialog/inventory is active - pause player controls
            _player.Velocity = Vector2.Zero;
            _wasPausedLastFrame = true;
            return;
        }
        
        // Check if we were paused last frame (by dialog/inventory) and just resumed
        // Reset any stale state that might cause issues
        if (_wasPausedLastFrame)
        {
            // Clear target position and velocity to ensure clean state after pause
            _targetPosition = null;
            _currentVelocity = Vector2.Zero;
            _player.Velocity = Vector2.Zero;
            _wasPausedLastFrame = false;
        }

        var inputService = ServiceLocator.Get<IInputService>();
        if (inputService == null)
            return;

        var timeManager = ServiceLocator.Get<GameTimeManager>();
        var deltaTime = timeManager?.DeltaTime ?? (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Handle mouse click to move/interact/attack (only check interactions on click, not while held)
        if (inputService.IsLeftClickPressed && _graphicsDevice != null)
        {
            // Convert mouse screen position to world coordinates (accounting for camera)
            // Camera position is in isometric screen coordinates, so ScreenToWorld gives us isometric screen position
            var mouseScreenPos = new Vector2(inputService.MousePosition.X, inputService.MousePosition.Y);
            var mouseWorldPos = _camera.ScreenToWorld(mouseScreenPos, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);

            bool handled = false;

            // Priority 1: Check if clicked on an interactable object (NPCs, items, etc.) - exclude enemies
            if (_interactionService != null)
            {
                foreach (var interactable in _interactionService.GetAllInteractables())
                {
                    if (interactable is not Entity entity)
                        continue;

                    // Skip enemies - they're handled separately
                    if (entity is Enemy)
                        continue;

                    var distanceToClick = Vector2.Distance(mouseWorldPos, entity.Position);
                    if (distanceToClick <= InteractableClickRadius)
                    {
                        // Get interaction radius
                        var entityInteractionRadius = interactable.InteractionRadius > 0 
                            ? interactable.InteractionRadius 
                            : InteractionRange;

                        // Calculate direction from entity to player
                        var dirFromEntityToPlayer = _player.Position - entity.Position;
                        var distanceToInteractable = dirFromEntityToPlayer.Length();

                        // Check if already in interaction range
                        if (distanceToInteractable <= entityInteractionRadius)
                        {
                            // Already in range - interact immediately
                            interactable.OnInteract(_player);
                            _pendingInteractable = null;
                            handled = true;
                        }
                        else
                        {
                            // Calculate target position at the edge of interaction radius
                            // Position player just outside the interaction radius so they can interact
                            Vector2 targetPos;
                            if (dirFromEntityToPlayer.LengthSquared() > 0.01f)
                            {
                                var normalizedDir = Vector2.Normalize(dirFromEntityToPlayer);
                                // Position at interaction radius (slightly inside to ensure we're in range)
                                targetPos = entity.Position + normalizedDir * (entityInteractionRadius * 0.95f);
                            }
                            else
                            {
                                // If player is exactly on entity, move to a default position
                                targetPos = entity.Position + new Vector2(entityInteractionRadius * 0.95f, 0);
                            }

                            // Set target position and store interactable for auto-interaction
                            _targetPosition = targetPos;
                            _pendingInteractable = interactable;
                            
                            // Create click effect at interactable position
                            _clickEffectRenderer?.CreateClickEffect(entity.Position, new Color(100, 255, 100, 255)); // Green for interactable
                            handled = true;
                        }
                        break;
                    }
                }
            }

            // Priority 2: Check if clicked on an enemy (if we didn't click an interactable)
            if (!handled)
            {
                var enemyService = ServiceLocator.Get<GameClient.Services.EnemyService>();
                if (enemyService != null)
                {
                    // Check melee enemies
                    foreach (var enemy in enemyService.GetAllEnemies())
                    {
                        if (!enemy.IsActive || enemy.Stats.IsDead)
                            continue;

                        var distanceToClick = Vector2.Distance(mouseWorldPos, enemy.Position);
                        if (distanceToClick <= EnemyClickRadius)
                        {
                            handled = true;

                            // Face the enemy
                            var dirToEnemy = enemy.Position - _player.Position;
                            if (dirToEnemy.LengthSquared() > 0.01f)
                            {
                                _player.FacingDirection = Vector2.Normalize(dirToEnemy);
                            }

                            // If enemy is in attack range, attack immediately
                            var distanceToEnemy = dirToEnemy.Length();
                            if (_combatService != null && !_player.Stats.IsDead && distanceToEnemy <= AttackRange)
                            {
                                var wasAlive = !enemy.Stats.IsDead;
                                _combatService.PerformMeleeAttack(_player, enemy);
                                
                                // Create blood splatter effect when enemy is hit (even if it dies from the attack)
                                if (_particleSystem != null && wasAlive)
                                {
                                    var hitDirection = Vector2.Normalize(dirToEnemy);
                                    var bloodEmitter = ParticlePreset.BloodSplatter(enemy.Position, hitDirection, 1.0f);
                                    _particleSystem.CreateEmitter(bloodEmitter, enemy); // Associate with enemy for death fading
                                }
                            }
                            else
                            {
                                // Otherwise, move toward the enemy
                                _targetPosition = enemy.Position;
                                // Create click effect at enemy position
                                _clickEffectRenderer?.CreateClickEffect(enemy.Position, new Color(255, 100, 100, 255)); // Red for enemy target
                            }

                            break;
                        }
                    }
                    
                    // Check ranged enemies if we haven't clicked a melee enemy
                    if (!handled)
                    {
                        foreach (var rangedEnemy in enemyService.GetAllRangedEnemies())
                        {
                            if (!rangedEnemy.IsActive || rangedEnemy.Stats.IsDead)
                                continue;

                            var distanceToClick = Vector2.Distance(mouseWorldPos, rangedEnemy.Position);
                            if (distanceToClick <= EnemyClickRadius)
                            {
                                handled = true;

                                // Face the enemy
                                var dirToEnemy = rangedEnemy.Position - _player.Position;
                                if (dirToEnemy.LengthSquared() > 0.01f)
                                {
                                    _player.FacingDirection = Vector2.Normalize(dirToEnemy);
                                }

                                // If enemy is in attack range, attack immediately
                                var distanceToEnemy = dirToEnemy.Length();
                                if (_combatService != null && !_player.Stats.IsDead && distanceToEnemy <= AttackRange)
                                {
                                    var wasAlive = !rangedEnemy.Stats.IsDead;
                                    // Apply damage directly since ranged enemies don't use the same combat system
                                    var damage = _player.Stats.AttackPower;
                                    rangedEnemy.Stats.ApplyDamage(damage);
                                    rangedEnemy.TriggerHitFlash();
                                    
                                    // Create blood splatter effect
                                    if (_particleSystem != null && wasAlive)
                                    {
                                        var hitDirection = Vector2.Normalize(dirToEnemy);
                                        var bloodEmitter = ParticlePreset.BloodSplatter(rangedEnemy.Position, hitDirection, 1.0f);
                                        _particleSystem.CreateEmitter(bloodEmitter, rangedEnemy);
                                    }
                                }
                                else
                                {
                                    // Otherwise, move toward the enemy
                                    _targetPosition = rangedEnemy.Position;
                                    // Create click effect at enemy position
                                    _clickEffectRenderer?.CreateClickEffect(rangedEnemy.Position, new Color(255, 100, 100, 255)); // Red for enemy target
                                }

                                break;
                            }
                        }
                    }
                }
            }

            // Priority 3: If we didn't click anything special, just move to the clicked position
            if (!handled)
            {
                _targetPosition = mouseWorldPos;
                _pendingInteractable = null; // Clear any pending interactable when moving to a new position
                // Create click effect at the target position
                _clickEffectRenderer?.CreateClickEffect(mouseWorldPos);
            }
        }

        // Handle continuous cursor following when left mouse button is held down
        if (inputService.IsLeftMouseButtonDown && _graphicsDevice != null)
        {
            // Convert mouse screen position to world coordinates (accounting for camera)
            var mouseScreenPos = new Vector2(inputService.MousePosition.X, inputService.MousePosition.Y);
            var mouseWorldPos = _camera.ScreenToWorld(mouseScreenPos, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
            
            // Continuously update target position to cursor while mouse is held down
            // This creates smooth following behavior with the same movement speed as click-to-move
            _targetPosition = mouseWorldPos;
        }
        else if (!inputService.IsLeftMouseButtonDown && _targetPosition.HasValue)
        {
            // Clear target position when mouse button is released (player will decelerate to stop)
            // Don't clear immediately - let the existing arrival logic handle it
        }
        
        // Update player to face cursor - always update when game is active and GraphicsDevice is available
        if (_graphicsDevice != null)
        {
            // Convert mouse screen position to isometric screen coordinates
            var mouseScreenPos = new Vector2(inputService.MousePosition.X, inputService.MousePosition.Y);
            var mouseIsometricScreenPos = _camera.ScreenToWorld(mouseScreenPos, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
            
            // Player position is in isometric screen space, so we can directly calculate direction
            var directionToCursor = mouseIsometricScreenPos - _player.Position;
            if (directionToCursor.LengthSquared() > 0.1f)
            {
                _player.FacingDirection = Vector2.Normalize(directionToCursor);
            }
        }

        // Handle click-to-move with smooth acceleration/deceleration
        if (_targetPosition.HasValue)
        {
            // Check if we have a pending interactable and are now in range
            if (_pendingInteractable != null && _pendingInteractable is Entity entity)
            {
                var dirToInteractable = entity.Position - _player.Position;
                var distanceToInteractable = dirToInteractable.Length();
                var entityInteractionRadius = _pendingInteractable.InteractionRadius > 0 
                    ? _pendingInteractable.InteractionRadius 
                    : InteractionRange;

                if (distanceToInteractable <= entityInteractionRadius)
                {
                    // Face the interactable
                    if (dirToInteractable.LengthSquared() > 0.01f)
                    {
                        _player.FacingDirection = Vector2.Normalize(dirToInteractable);
                    }

                    // Interact automatically
                    _pendingInteractable.OnInteract(_player);
                    _pendingInteractable = null;
                    _targetPosition = null;
                    _currentVelocity = Vector2.Zero;
                    _player.Velocity = Vector2.Zero;
                    return; // Stop movement and interaction
                }
            }

            var directionToTarget = _targetPosition.Value - _player.Position;
            var distanceToTarget = directionToTarget.Length();

            if (distanceToTarget > ArrivalDistance)
            {
                // Calculate desired velocity
                var normalizedDirection = directionToTarget.LengthSquared() > 0.01f 
                    ? Vector2.Normalize(directionToTarget) 
                    : Vector2.Zero;
                // Use player's movement speed (fixed value, does not scale with tile size)
                var desiredVelocity = normalizedDirection * _player.MovementSpeed;

                // Smoothly accelerate towards desired velocity
                var velocityDiff = desiredVelocity - _currentVelocity;
                var accelerationDirection = velocityDiff.LengthSquared() > 0.01f 
                    ? Vector2.Normalize(velocityDiff) 
                    : Vector2.Zero;
                
                var accelerationAmount = Acceleration * deltaTime;
                if (velocityDiff.Length() < accelerationAmount)
                {
                    // Close enough, use desired velocity directly
                    _currentVelocity = desiredVelocity;
                }
                else
                {
                    // Accelerate towards desired velocity
                    _currentVelocity += accelerationDirection * accelerationAmount;
                }

                _player.Velocity = _currentVelocity;
                _player.FacingDirection = normalizedDirection;
            }
            else
            {
                // Reached target - check if we need to interact with a pending interactable
                if (_pendingInteractable != null)
                {
                    // Check if we're now in interaction range
                    if (_pendingInteractable is Entity interactableEntity)
                    {
                        var dirToInteractable = interactableEntity.Position - _player.Position;
                        var distanceToInteractable = dirToInteractable.Length();
                        var entityInteractionRadius = _pendingInteractable.InteractionRadius > 0 
                            ? _pendingInteractable.InteractionRadius 
                            : InteractionRange;

                        if (distanceToInteractable <= entityInteractionRadius)
                        {
                            // Face the interactable
                            if (dirToInteractable.LengthSquared() > 0.01f)
                            {
                                _player.FacingDirection = Vector2.Normalize(dirToInteractable);
                            }

                            // Interact automatically
                            _pendingInteractable.OnInteract(_player);
                            _pendingInteractable = null;
                            _targetPosition = null;
                            _currentVelocity = Vector2.Zero;
                            _player.Velocity = Vector2.Zero;
                            return; // Skip the rest of movement logic
                        }
                    }
                }

                // Reached target - smoothly decelerate
                var currentSpeed = _currentVelocity.Length();
                if (currentSpeed > 0.1f)
                {
                    // Decelerate
                    var decelerationAmount = Deceleration * deltaTime;
                    if (currentSpeed <= decelerationAmount)
                    {
                        _currentVelocity = Vector2.Zero;
                    }
                    else
                    {
                        var decelDirection = Vector2.Normalize(_currentVelocity);
                        _currentVelocity -= decelDirection * decelerationAmount;
                    }
                    _player.Velocity = _currentVelocity;
                }
                else
                {
                    // Fully stopped
                    _targetPosition = null;
                    _currentVelocity = Vector2.Zero;
                    _player.Velocity = Vector2.Zero;
                    _pendingInteractable = null; // Clear pending interactable if we stopped
                }
            }
        }
        else
        {
            // No target - smoothly decelerate to stop
            var currentSpeed = _currentVelocity.Length();
            if (currentSpeed > 0.1f)
            {
                var decelerationAmount = Deceleration * deltaTime;
                if (currentSpeed <= decelerationAmount)
                {
                    _currentVelocity = Vector2.Zero;
                }
                else
                {
                    var decelDirection = Vector2.Normalize(_currentVelocity);
                    _currentVelocity -= decelDirection * decelerationAmount;
                }
                _player.Velocity = _currentVelocity;
            }
            else
            {
                _currentVelocity = Vector2.Zero;
                _player.Velocity = Vector2.Zero;
            }
        }

        // Handle interaction input
        if (inputService.IsActionPressed(GameAction.Interact))
        {
            var interactable = _interactionService?.TryInteract(_player);
            if (interactable != null)
            {
                interactable.OnInteract(_player);
            }
        }

        // Handle attack input
        if (inputService.IsActionPressed(GameAction.Attack))
        {
            TryAttack();
        }

        // Handle right mouse button shooting
        if (inputService.IsRightClickPressed && _graphicsDevice != null && _projectileService != null)
        {
            // Convert mouse screen position to world coordinates
            var mouseScreenPos = new Vector2(inputService.MousePosition.X, inputService.MousePosition.Y);
            var mouseWorldPos = _camera.ScreenToWorld(mouseScreenPos, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
            
            // Calculate direction from player to mouse cursor
            var directionToMouse = mouseWorldPos - _player.Position;
            
            if (directionToMouse.LengthSquared() > 0.01f)
            {
                // Fire projectile in the direction of the mouse cursor
                _projectileService.FireProjectile(_player.Position, directionToMouse);
                
                // Create muzzle flash effect
                _particleSystem?.CreateEmitter(ParticlePreset.HitSparks(_player.Position, 0.3f));
            }
        }

        // Apply velocity to position with true directional movement (continuous, not grid-snapped)
        if (_player.Velocity.LengthSquared() > 0)
        {
            // Check if we should emit footstep particles
            var distanceSinceLastFootstep = Vector2.Distance(_player.Position, _lastFootstepPosition);
            if (distanceSinceLastFootstep >= FootstepDistance)
            {
                // Emit footstep dust at player's feet (slightly behind the center)
                var footPosition = _player.Position - _player.FacingDirection * (_player.Size.Y * 0.3f);
                _particleSystem?.CreateEmitter(ParticlePreset.FootstepDust(footPosition, 1.0f));
                _lastFootstepPosition = _player.Position;
            }

            // Move in the exact direction of velocity (true directional movement)
            var proposedPosition = _player.Position + _player.Velocity * deltaTime;

            // Resolve collisions with improved, more lenient detection
            _collisionService?.ResolveCollision(_player, proposedPosition);
            
            // If we hit a wall while moving to target, clear the target
            if (_targetPosition.HasValue)
            {
                var distanceAfterMove = Vector2.Distance(_player.Position, _targetPosition.Value);
                if (distanceAfterMove < ArrivalDistance)
                {
                    _targetPosition = null;
                }
            }
        }
    }

    /// <summary>
    /// Attempts to attack an enemy in front of the player.
    /// </summary>
    private void TryAttack()
    {
        if (_combatService == null || _player.Stats.IsDead)
            return;

        // Find enemies in attack range in front of player
        var enemyService = ServiceLocator.Get<GameClient.Services.EnemyService>();
        if (enemyService == null)
        {
            var logger = ServiceLocator.Get<ILogger>();
            logger?.Warning("EnemyService not found when trying to attack");
            return;
        }

        var enemies = enemyService.GetEnemiesInRange(_player.Position, AttackRange);
        
        if (enemies.Count == 0)
        {
            // No enemies in range - this is fine, just return
            return;
        }

        // Find the closest enemy that's roughly in front of the player
        Enemy? closestEnemy = null;
        float closestDistance = float.MaxValue;
        
        foreach (var enemy in enemies)
        {
            if (enemy.Stats.IsDead || !enemy.IsActive)
                continue;

            var directionToEnemy = enemy.Position - _player.Position;
            var distance = directionToEnemy.Length();
            
            if (distance <= 0.1f)
                continue;

            var normalizedDirection = Vector2.Normalize(directionToEnemy);
            var facingDot = Vector2.Dot(_player.FacingDirection, normalizedDirection);
            
            // Player must be facing roughly toward the enemy (wider cone - 60 degrees)
            // 0.5f = 60 degrees, 0.7f = 45 degrees, 0.9f = 25 degrees
            if (facingDot >= 0.5f)
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = enemy;
                }
            }
        }

        // Attack the closest valid enemy
        if (closestEnemy != null)
        {
            var wasAlive = !closestEnemy.Stats.IsDead;
            _combatService.PerformMeleeAttack(_player, closestEnemy);
            
            // Create blood splatter effect when enemy is hit (even if it dies from the attack)
            if (_particleSystem != null && wasAlive)
            {
                var directionToEnemy = closestEnemy.Position - _player.Position;
                if (directionToEnemy.LengthSquared() > 0.01f)
                {
                    var hitDirection = Vector2.Normalize(directionToEnemy);
                    var bloodEmitter = ParticlePreset.BloodSplatter(closestEnemy.Position, hitDirection, 1.0f);
                    _particleSystem.CreateEmitter(bloodEmitter, closestEnemy); // Associate with enemy for death fading
                }
            }
        }
    }

    public void Draw(GameTime gameTime)
    {
        // Player controller doesn't need rendering
    }
    
    /// <summary>
    /// Forces a reset of the pause state - call this when dialog/inventory closes to ensure controls unlock.
    /// </summary>
    public void ForceResetPauseState()
    {
        _wasPausedLastFrame = false;
        _targetPosition = null;
        _currentVelocity = Vector2.Zero;
        if (_player != null)
        {
            _player.Velocity = Vector2.Zero;
        }
    }
}

