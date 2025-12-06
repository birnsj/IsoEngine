using System;
using System.Linq;
using GameCore.Combat;
using GameCore.Entities;
using GameCore.Rendering;
using GameCore.Services;
using GameClient.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Service that renders entities on the isometric map.
/// </summary>
public class EntityRenderer : IGameService
{
    private readonly Camera2D _camera;
    private readonly IsometricTileMap _tileMap;
    private readonly List<Entity> _entities = new();
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _placeholderTexture;
    private Texture2D? _playerSprite;
    
    /// <summary>
    /// How many pixels one Z unit raises the entity visually on screen.
    /// </summary>
    private const float PixelsPerZUnit = 16f;
    
    // Health bar animation tracking
    private readonly Dictionary<Entity, HealthBarState> _healthBarStates = new();
    
    // Interaction highlight tracking
    private readonly Dictionary<Entity, InteractionHighlightState> _highlightStates = new();
    
    // Loot pulse tracking for dead enemies with inventory
    private readonly Dictionary<Entity, LootPulseState> _lootPulseStates = new();

    public EntityRenderer(Camera2D camera, IsometricTileMap tileMap)
    {
        _camera = camera;
        _tileMap = tileMap;
    }

    /// <summary>
    /// Adds an entity to be rendered.
    /// </summary>
    public void AddEntity(Entity entity)
    {
        if (!_entities.Contains(entity))
        {
            _entities.Add(entity);
            
            // Debug: Log when ranged enemies are added
            if (entity is RangedEnemy)
            {
                var logger = GameCore.Services.ServiceLocator.Get<GameCore.Services.ILogger>();
                logger?.Info($"EntityRenderer.AddEntity: Added ranged enemy at {entity.Position}, IsActive={entity.IsActive}, Total entities now: {_entities.Count}");
            }
        }
    }

    /// <summary>
    /// Removes an entity from rendering.
    /// </summary>
    public void RemoveEntity(Entity entity)
    {
        _entities.Remove(entity);
    }
    
    /// <summary>
    /// Gets the number of entities being rendered.
    /// </summary>
    public int GetEntityCount()
    {
        return _entities.Count;
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update health bar animations
        foreach (var entity in _entities)
        {
            if (entity.HasComponent<StatsComponent>())
            {
                var stats = entity.GetComponent<StatsComponent>();
                if (stats != null && stats.MaxHealth > 0)
                {
                    var currentHealth = stats.HealthPercentage;
                    
                    if (!_healthBarStates.ContainsKey(entity))
                    {
                        _healthBarStates[entity] = new HealthBarState
                        {
                            DisplayedHealth = currentHealth,
                            TargetHealth = currentHealth,
                            FlashTimer = 0.0f
                        };
                    }

                    var state = _healthBarStates[entity];
                    state.TargetHealth = currentHealth;
                    
                    // Smooth interpolation towards target health
                    const float lerpSpeed = 5.0f; // Health per second
                    var healthDiff = state.TargetHealth - state.DisplayedHealth;
                    if (Math.Abs(healthDiff) > 0.001f)
                    {
                        var maxChange = lerpSpeed * deltaTime;
                        var change = Math.Sign(healthDiff) * Math.Min(Math.Abs(healthDiff), maxChange);
                        state.DisplayedHealth += change;
                        
                        // Trigger flash on damage
                        if (healthDiff < 0)
                        {
                            state.FlashTimer = 0.2f; // Flash for 0.2 seconds
                        }
                    }
                    else
                    {
                        state.DisplayedHealth = state.TargetHealth;
                    }

                    // Update flash timer
                    if (state.FlashTimer > 0.0f)
                    {
                        state.FlashTimer -= deltaTime;
                    }
                }
            }
        }

        // Update interaction highlights
        var interactionService = GameCore.Services.ServiceLocator.Get<GameCore.Services.InteractionService>();
        foreach (var entity in _entities)
        {
            if (entity is GameCore.Interactions.IInteractable)
            {
                // Check if this is the nearest interactable (for highlighting)
                var isHovered = false;
                if (interactionService != null)
                {
                    var player = ServiceLocator.Get<Player>();
                    if (player != null)
                    {
                        var nearest = interactionService.GetNearestInteractable(player);
                        isHovered = nearest == entity;
                    }
                }
                
                if (!_highlightStates.ContainsKey(entity))
                {
                    _highlightStates[entity] = new InteractionHighlightState
                    {
                        PulseTimer = 0.0f,
                        IsHovered = false
                    };
                }

                var highlight = _highlightStates[entity];
                highlight.IsHovered = isHovered;
                highlight.PulseTimer += deltaTime;
            }
        }

        // Update loot pulse for dead enemies with inventory
        foreach (var entity in _entities)
        {
            bool isDead = false;
            bool hasLoot = false;
            
            // Check if entity is dead
            if (entity.HasComponent<StatsComponent>())
            {
                var stats = entity.GetComponent<StatsComponent>();
                isDead = stats?.IsDead ?? false;
            }
            
            // Check if entity has items in inventory
            if (entity is Enemy enemy)
            {
                hasLoot = !IsInventoryEmpty(enemy.EnemyInventory);
            }
            
            // Check if ranged enemy has loot (alive or dead)
            bool rangedEnemyHasLoot = false;
            if (entity is RangedEnemy rangedEnemy)
            {
                rangedEnemyHasLoot = !IsInventoryEmpty(rangedEnemy.EnemyInventory);
            }
            
            // Pulse if dead with loot OR if ranged enemy with loot (alive or dead)
            if ((isDead && hasLoot) || rangedEnemyHasLoot)
            {
                if (!_lootPulseStates.ContainsKey(entity))
                {
                    _lootPulseStates[entity] = new LootPulseState { PulseTimer = 0.0f };
                }
                _lootPulseStates[entity].PulseTimer += deltaTime;
            }
            else
            {
                _lootPulseStates.Remove(entity);
            }
        }

        // Clean up health bar states for removed entities
        var entitiesToRemove = _healthBarStates.Keys.Where(e => !_entities.Contains(e)).ToList();
        foreach (var entity in entitiesToRemove)
        {
            _healthBarStates.Remove(entity);
            _highlightStates.Remove(entity);
            _lootPulseStates.Remove(entity);
        }
    }
    
    /// <summary>
    /// Checks if an inventory is empty.
    /// </summary>
    private bool IsInventoryEmpty(GameCore.Items.Inventory? inventory)
    {
        if (inventory == null)
            return true;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            if (!inventory[i].IsEmpty)
            {
                return false;
            }
        }
        return true;
    }

    public void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null || _graphicsDevice == null || _placeholderTexture == null)
            return;

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp);

        // Debug: Count ranged enemies in the list
        var rangedEnemyCount = _entities.Count(e => e is RangedEnemy);
        if (rangedEnemyCount > 0)
        {
            var logger = GameCore.Services.ServiceLocator.Get<GameCore.Services.ILogger>();
            logger?.Info($"EntityRenderer.Draw: Found {rangedEnemyCount} ranged enemies in _entities list (total entities: {_entities.Count})");
        }

        // Sort entities by render depth (back to front for painter's algorithm)
        // Entities with lower RenderDepth render first (behind), higher render later (in front)
        var sortedEntities = _entities.OrderBy(e => e.RenderDepth).ToList();

        // Render each entity in depth-sorted order
        foreach (var entity in sortedEntities)
        {
            // Check if this is a ranged enemy or regular enemy
            bool isRangedEnemy = entity is RangedEnemy;
            bool isEnemy = entity is Enemy;
            
            // Debug: Log ranged enemies
            if (isRangedEnemy)
            {
                var logger = GameCore.Services.ServiceLocator.Get<GameCore.Services.ILogger>();
                logger?.Debug($"Rendering ranged enemy: IsActive={entity.IsActive}, Position={entity.Position}, Type={entity.GetType().Name}");
            }
            
            // Always render active entities
            // Also render dead enemies (even if inactive) and ranged enemies (for debugging)
            if (!entity.IsActive)
            {
                // Allow rendering of dead enemies even if inactive
                bool isDeadEnemy = false;
                if (entity.HasComponent<StatsComponent>())
                {
                    var stats = entity.GetComponent<StatsComponent>();
                    if (stats != null && stats.IsDead)
                    {
                        isDeadEnemy = (isEnemy || isRangedEnemy);
                    }
                }
                // Always render ranged enemies (even if inactive - for debugging)
                if (!isDeadEnemy && !isRangedEnemy)
                    continue;
            }

            // Entity position is already in screen coordinates (isometric)
            // Apply visual Z offset: higher Z moves entity up on screen
            var screenPos = entity.Position;
            screenPos.Y -= entity.Z * PixelsPerZUnit;

            // Get death effect service for fade out
            var deathEffectService = GameCore.Services.ServiceLocator.Get<DeathEffectService>();
            var entityAlpha = deathEffectService?.GetEntityAlpha(entity) ?? 1.0f;

            // Check if entity is dead
            bool isDead = false;
            if (entity.HasComponent<StatsComponent>())
            {
                var stats = entity.GetComponent<StatsComponent>();
                if (stats != null)
                {
                    isDead = stats.IsDead;
                }
            }
            
            // Use different colors for different entity types
            // Check RangedEnemy first before Enemy to ensure proper pattern matching
            Color entityColor;
            if (entity is RangedEnemy rangedEnemy)
            {
                if (isDead)
                {
                    entityColor = new Color(100, 0, 0); // Dark red for dead ranged enemies
                }
                else
                {
                    entityColor = rangedEnemy.IsFlashing ? Color.White : Color.Red; // Red for ranged enemies
                }
            }
            else if (entity is Enemy enemy)
            {
                if (isDead)
                {
                    entityColor = new Color(100, 50, 0); // Dark orange/brown for dead enemies
                }
                else
                {
                    entityColor = enemy.IsFlashing ? Color.White : Color.Orange; // Flash white when hit
                }
            }
            else if (entity is Player)
            {
                entityColor = Color.White; // Use white to show original sprite colors
            }
            else if (entity is GroundItem)
            {
                entityColor = Color.Gold;
            }
            else if (entity is InteractiveObject)
            {
                entityColor = Color.Cyan;
            }
            else
            {
                entityColor = Color.Yellow;
            }

            // Apply fade out alpha if entity is dying
            if (entityAlpha < 1.0f)
            {
                entityColor = new Color(entityColor.R, entityColor.G, entityColor.B, (byte)(entityColor.A * entityAlpha));
            }
            
            // Make dead enemies slightly transparent
            if (isDead)
            {
                entityColor = new Color(entityColor.R, entityColor.G, entityColor.B, (byte)(entityColor.A * 0.7f));
                
                // Apply loot pulse effect for dead enemies with inventory
                if (_lootPulseStates.TryGetValue(entity, out var lootPulse))
                {
                    var pulseIntensity = (MathF.Sin(lootPulse.PulseTimer * 4.0f) + 1.0f) * 0.5f; // 0 to 1, faster pulse
                    var glowIntensity = 0.3f + pulseIntensity * 0.7f; // 0.3 to 1.0
                    
                    if (entity is RangedEnemy)
                    {
                        // Pulse red for ranged enemies
                        entityColor = new Color(
                            (byte)Math.Min(255, entityColor.R + (int)((255 - entityColor.R) * glowIntensity)),
                            (byte)Math.Min(255, entityColor.G + (int)((50 - entityColor.G) * glowIntensity * 0.3f)),
                            (byte)Math.Min(255, entityColor.B + (int)((50 - entityColor.B) * glowIntensity * 0.3f)),
                            (byte)Math.Min(255, entityColor.A + (int)((255 - entityColor.A) * glowIntensity * 0.5f))
                        );
                    }
                    else
                    {
                        // Blend toward gold/yellow color for melee enemies
                        entityColor = new Color(
                            (byte)Math.Min(255, entityColor.R + (int)((255 - entityColor.R) * glowIntensity * 0.8f)),
                            (byte)Math.Min(255, entityColor.G + (int)((215 - entityColor.G) * glowIntensity * 0.6f)),
                            (byte)Math.Min(255, entityColor.B + (int)((0 - entityColor.B) * glowIntensity)),
                            (byte)Math.Min(255, entityColor.A + (int)((255 - entityColor.A) * glowIntensity * 0.5f))
                        );
                    }
                }
            }
            
            // Apply loot pulse effect for living ranged enemies with inventory
            if (!isDead && entity is RangedEnemy && _lootPulseStates.TryGetValue(entity, out var rangedLootPulse))
            {
                // Create a subtle pulsing brightness effect
                var pulseIntensity = (MathF.Sin(rangedLootPulse.PulseTimer * 3.0f) + 1.0f) * 0.5f; // 0 to 1
                var brightnessBoost = 0.15f + pulseIntensity * 0.25f; // 0.15 to 0.4
                
                // Brighten the red color slightly to indicate loot
                entityColor = new Color(
                    (byte)Math.Min(255, entityColor.R + (int)((255 - entityColor.R) * brightnessBoost)),
                    (byte)Math.Min(255, entityColor.G + (int)(50 * brightnessBoost)),
                    (byte)Math.Min(255, entityColor.B + (int)(50 * brightnessBoost)),
                    entityColor.A
                );
            }

            // Apply interaction highlight (glow/pulse)
            if (_highlightStates.TryGetValue(entity, out var highlight))
            {
                var pulseIntensity = (MathF.Sin(highlight.PulseTimer * 3.0f) + 1.0f) * 0.5f; // 0 to 1
                var baseIntensity = highlight.IsHovered ? 0.7f : 0.3f;
                var intensity = baseIntensity + pulseIntensity * 0.3f;
                
                // Add glow by brightening the color
                entityColor = new Color(
                    (byte)Math.Min(255, entityColor.R + (255 - entityColor.R) * intensity),
                    (byte)Math.Min(255, entityColor.G + (255 - entityColor.G) * intensity),
                    (byte)Math.Min(255, entityColor.B + (255 - entityColor.B) * intensity),
                    entityColor.A
                );
            }

            // Draw player as sprite (or arrow fallback), other entities as rectangles
            if (entity is Player player)
            {
                if (_playerSprite != null)
                {
                    DrawPlayerSprite(screenPos, player.FacingDirection, entityColor);
                }
                else
                {
                    DrawArrow(screenPos, player.FacingDirection, entity.Size, entityColor);
                }
            }
            else
            {
                // Draw entity as a colored rectangle
                var entityRect = new Rectangle(
                    (int)(screenPos.X - entity.Size.X / 2),
                    (int)(screenPos.Y - entity.Size.Y / 2),
                    (int)entity.Size.X,
                    (int)entity.Size.Y);

                // Debug: Log when drawing ranged enemies
                if (entity is RangedEnemy)
                {
                    var logger = GameCore.Services.ServiceLocator.Get<GameCore.Services.ILogger>();
                    logger?.Debug($"Drawing ranged enemy at {screenPos}, Color={entityColor}, Rect={entityRect}");
                }

                _spriteBatch.Draw(_placeholderTexture, entityRect, entityColor);
            }

            // Draw health bar if entity has stats (but not for dead enemies)
            if (entity.HasComponent<StatsComponent>())
            {
                var stats = entity.GetComponent<StatsComponent>();
                if (stats != null && stats.MaxHealth > 0 && !stats.IsDead)
                {
                    var displayedHealth = stats.HealthPercentage;
                    var isFlashing = false;
                    
                    // Get animated health value if available
                    if (_healthBarStates.ContainsKey(entity))
                    {
                        displayedHealth = _healthBarStates[entity].DisplayedHealth;
                        isFlashing = _healthBarStates[entity].FlashTimer > 0.0f;
                    }
                    
                    DrawHealthBar(screenPos, displayedHealth, stats.HealthPercentage, entity.Size.Y, isFlashing);
                }
            }
        }

        _spriteBatch.End();
    }

    /// <summary>
    /// Draws the player sprite at the specified position.
    /// </summary>
    /// <param name="position">Center position of the player.</param>
    /// <param name="facingDirection">Direction the player is facing (for potential flipping).</param>
    /// <param name="color">Tint color for the sprite.</param>
    private void DrawPlayerSprite(Vector2 position, Vector2 facingDirection, Color color)
    {
        if (_spriteBatch == null || _playerSprite == null)
            return;

        // Scale the sprite to fit the game (adjust as needed)
        const float spriteScale = 0.15f; // Scale down the large sprite
        var scaledWidth = _playerSprite.Width * spriteScale;
        var scaledHeight = _playerSprite.Height * spriteScale;
        
        // Center the sprite on the position (with offset for feet positioning)
        var drawPos = new Vector2(
            position.X - scaledWidth / 2,
            position.Y - scaledHeight + 10 // Offset so feet are at position
        );
        
        // Flip sprite based on facing direction (flip when facing left)
        var flipEffect = facingDirection.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        
        // Draw the sprite
        var destRect = new Rectangle(
            (int)drawPos.X,
            (int)drawPos.Y,
            (int)scaledWidth,
            (int)scaledHeight
        );
        
        _spriteBatch.Draw(_playerSprite, destRect, null, color, 0f, Vector2.Zero, flipEffect, 0f);
    }

    /// <summary>
    /// Draws an arrow pointing in the specified direction.
    /// </summary>
    /// <param name="position">Center position of the arrow.</param>
    /// <param name="direction">Normalized direction vector the arrow should point.</param>
    /// <param name="size">Size of the arrow.</param>
    /// <param name="color">Color of the arrow.</param>
    private void DrawArrow(Vector2 position, Vector2 direction, Vector2 size, Color color)
    {
        if (_spriteBatch == null || _placeholderTexture == null)
            return;

        // Calculate the angle from the direction vector
        // In isometric view, Y increases downward, so we need to account for that
        var angle = (float)Math.Atan2(direction.Y, direction.X);

        // Arrow dimensions
        var arrowLength = size.X * 0.8f; // Length of the arrow
        var arrowWidth = size.Y * 0.6f;  // Width of the arrow head

        // Calculate the three points of the triangle (arrow head)
        // Point 1: Tip of the arrow (in the direction of movement)
        var tip = position + direction * arrowLength * 0.5f;

        // Point 2 and 3: Base of the arrow (perpendicular to direction)
        var perpendicular = new Vector2(-direction.Y, direction.X); // 90 degree rotation
        var baseLeft = position - direction * arrowLength * 0.3f - perpendicular * arrowWidth * 0.5f;
        var baseRight = position - direction * arrowLength * 0.3f + perpendicular * arrowWidth * 0.5f;

        // Draw the arrow as a filled triangle using multiple small rectangles
        // This is a simple approximation - for a better triangle, we'd need primitive rendering
        DrawTriangle(tip, baseLeft, baseRight, color);
    }

    /// <summary>
    /// Draws a filled triangle by drawing multiple horizontal lines.
    /// </summary>
    private void DrawTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Color color)
    {
        if (_spriteBatch == null || _placeholderTexture == null)
            return;

        // Sort points by Y coordinate
        var points = new[] { p1, p2, p3 };
        Array.Sort(points, (a, b) => a.Y.CompareTo(b.Y));

        var top = points[0];
        var middle = points[1];
        var bottom = points[2];

        // Calculate slopes for the edges
        var dx1 = (middle.X - top.X) / (middle.Y - top.Y + 0.0001f); // Avoid division by zero
        var dx2 = (bottom.X - top.X) / (bottom.Y - top.Y + 0.0001f);
        var dx3 = (bottom.X - middle.X) / (bottom.Y - middle.Y + 0.0001f);

        var x1 = top.X;
        var x2 = top.X;

        // Draw top half of triangle
        for (var y = (int)top.Y; y <= (int)middle.Y; y++)
        {
            var startX = (int)Math.Min(x1, x2);
            var endX = (int)Math.Max(x1, x2);
            var width = endX - startX;
            if (width > 0)
            {
                var rect = new Rectangle(startX, y, width, 1);
                _spriteBatch.Draw(_placeholderTexture, rect, color);
            }
            x1 += dx1;
            x2 += dx2;
        }

        // Draw bottom half of triangle
        x1 = middle.X;
        for (var y = (int)middle.Y; y <= (int)bottom.Y; y++)
        {
            var startX = (int)Math.Min(x1, x2);
            var endX = (int)Math.Max(x1, x2);
            var width = endX - startX;
            if (width > 0)
            {
                var rect = new Rectangle(startX, y, width, 1);
                _spriteBatch.Draw(_placeholderTexture, rect, color);
            }
            x1 += dx3;
            x2 += dx2;
        }
    }

    /// <summary>
    /// Draws a health bar above an entity with smooth animations.
    /// </summary>
    private void DrawHealthBar(Vector2 entityPosition, float displayedHealth, float actualHealth, float entityHeight, bool isFlashing)
    {
        if (_spriteBatch == null || _placeholderTexture == null)
            return;

        const int barWidth = 40;
        const int barHeight = 4;
        const int barOffsetY = 20; // Offset above entity

        var barX = (int)(entityPosition.X - barWidth / 2);
        var barY = (int)(entityPosition.Y - entityHeight / 2 - barOffsetY);

        // Draw background (red/dark)
        var bgRect = new Rectangle(barX, barY, barWidth, barHeight);
        _spriteBatch.Draw(_placeholderTexture, bgRect, new Color(50, 0, 0, 255));

        // Draw health with smooth color transition
        var healthWidth = (int)(barWidth * MathHelper.Clamp(displayedHealth, 0.0f, 1.0f));
        if (healthWidth > 0)
        {
            var healthRect = new Rectangle(barX, barY, healthWidth, barHeight);
            
            // Color transitions: Green -> Yellow -> Red
            Color healthColor;
            if (displayedHealth > 0.6f)
            {
                // Green to Yellow transition
                var t = (displayedHealth - 0.6f) / 0.4f;
                healthColor = Color.Lerp(Color.Yellow, Color.Green, t);
            }
            else if (displayedHealth > 0.3f)
            {
                // Yellow to Red transition
                var t = (displayedHealth - 0.3f) / 0.3f;
                healthColor = Color.Lerp(Color.Red, Color.Yellow, t);
            }
            else
            {
                healthColor = Color.Red;
            }

            // Flash white when taking damage
            if (isFlashing)
            {
                healthColor = Color.Lerp(healthColor, Color.White, 0.5f);
            }

            _spriteBatch.Draw(_placeholderTexture, healthRect, healthColor);
        }

        // Draw damage indicator (red section showing lost health)
        if (displayedHealth < actualHealth)
        {
            var damageWidth = (int)(barWidth * (actualHealth - displayedHealth));
            var damageX = barX + healthWidth;
            var damageRect = new Rectangle(damageX, barY, damageWidth, barHeight);
            _spriteBatch.Draw(_placeholderTexture, damageRect, new Color(150, 0, 0, 200));
        }
    }

    /// <summary>
    /// Sets up the renderer with graphics device and sprite batch.
    /// Should be called from LoadContent.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create a simple 1x1 white texture for drawing colored rectangles
        _placeholderTexture = new Texture2D(graphicsDevice, 1, 1);
        _placeholderTexture.SetData(new[] { Color.White });
        
        // Try to load player sprite
        LoadPlayerSprite();
    }
    
    /// <summary>
    /// Loads the player sprite from the sprites folder.
    /// </summary>
    private void LoadPlayerSprite()
    {
        if (_graphicsDevice == null)
            return;
            
        try
        {
            // Try multiple possible paths for the player sprite
            var possiblePaths = new[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameContent", "sprites", "player.png"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "GameContent", "sprites", "player.png"),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "GameContent", "sprites", "player.png"),
                @"D:\Projects\UnrealPlayground\IsoEngine\GameContent\sprites\player.png"
            };
            
            foreach (var path in possiblePaths)
            {
                var normalizedPath = System.IO.Path.GetFullPath(path);
                if (System.IO.File.Exists(normalizedPath))
                {
                    using (var stream = System.IO.File.OpenRead(normalizedPath))
                    {
                        _playerSprite = Texture2D.FromStream(_graphicsDevice, stream);
                    }
                    var logger = ServiceLocator.Get<ILogger>();
                    logger?.Info($"Loaded player sprite from: {normalizedPath}");
                    return;
                }
            }
            
            var log = ServiceLocator.Get<ILogger>();
            log?.Warning("Player sprite not found, using placeholder");
        }
        catch (Exception ex)
        {
            var logger = ServiceLocator.Get<ILogger>();
            logger?.Warning($"Failed to load player sprite: {ex.Message}");
        }
    }
}

/// <summary>
/// Tracks the animation state of a health bar for smooth transitions.
/// </summary>
internal class HealthBarState
{
    public float DisplayedHealth { get; set; }
    public float TargetHealth { get; set; }
    public float FlashTimer { get; set; }
}

/// <summary>
/// Tracks the highlight state for interactable entities.
/// </summary>
internal class InteractionHighlightState
{
    public float PulseTimer { get; set; }
    public bool IsHovered { get; set; }
}

/// <summary>
/// Tracks the loot pulse state for dead enemies with inventory items.
/// </summary>
internal class LootPulseState
{
    public float PulseTimer { get; set; }
}

